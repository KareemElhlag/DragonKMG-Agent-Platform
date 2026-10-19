#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
ui_root="$repo_root/ui"
api_project="$repo_root/src/DragonKMG.Api/DragonKMG.Api.csproj"
cli_project="$repo_root/src/DragonKMG.Cli/DragonKMG.Cli.csproj"
with_cli=0
check_only=0

for arg in "$@"; do
  case "$arg" in
    --with-cli) with_cli=1 ;;
    --check-only) check_only=1 ;;
    *) printf 'Unknown option: %s\nUsage: bash scripts/start-local.sh [--with-cli] [--check-only]\n' "$arg" >&2; exit 2 ;;
  esac
done

command -v dotnet >/dev/null || { echo 'Install the .NET 9 SDK, then retry.' >&2; exit 1; }
command -v node >/dev/null || { echo 'Install Node.js 20.19+ or 22.12+, then retry.' >&2; exit 1; }
command -v npm >/dev/null || { echo 'Install npm with Node.js, then retry.' >&2; exit 1; }
command -v curl >/dev/null || { echo 'Install curl, then retry.' >&2; exit 1; }
dotnet_version="$(dotnet --version)"
[[ "$dotnet_version" == 9.* ]] || { printf 'DragonKMG requires the .NET 9 SDK; found %s.\n' "$dotnet_version" >&2; exit 1; }
node_version="$(node --version | sed 's/^v//')"
node_major="${node_version%%.*}"
node_minor="$(printf '%s' "$node_version" | cut -d. -f2)"
if ! { (( node_major == 20 && node_minor >= 19 )) || (( node_major >= 22 && (node_major > 22 || node_minor >= 12) )); }; then
  printf 'Vite requires Node.js 20.19+ or 22.12+; found %s.\n' "$node_version" >&2
  exit 1
fi
[[ -f "$api_project" && -f "$cli_project" ]] || { echo 'Run this script from a complete DragonKMG source checkout.' >&2; exit 1; }

if [[ -z "${DRAGONKMG_DB_PATH:-}" ]]; then
  if [[ "$(uname -s)" == Darwin* ]]; then
    data_root="${XDG_DATA_HOME:-$HOME/Library/Application Support}"
  else
    data_root="${XDG_DATA_HOME:-$HOME/.local/share}"
  fi
  data_dir="$data_root/DragonKMG"
  preferred_db="$data_dir/dragonkmg.db"
  candidates=()
  for candidate in \
    "$repo_root/src/DragonKMG.Api/bin/Debug/net9.0/dragonkmg.db" \
    "$repo_root/src/DragonKMG.Api/bin/Release/net9.0/dragonkmg.db" \
    "$preferred_db"; do
    [[ -f "$candidate" ]] && candidates+=("$candidate")
  done
  if ((${#candidates[@]})); then
    db_path="$(node -e 'const fs=require("node:fs");const p=process.argv.slice(1);p.sort((a,b)=>fs.statSync(b).mtimeMs-fs.statSync(a).mtimeMs);process.stdout.write(p[0])' "${candidates[@]}")"
  else
    db_path="$preferred_db"
  fi
else
  db_path="$DRAGONKMG_DB_PATH"
fi

if [[ ! -d "$ui_root/node_modules/vite" ]]; then
  if ((check_only)); then
    echo 'Dashboard dependencies are missing; a normal start will install them from package-lock.json.'
  else
    echo 'Installing dashboard dependencies from package-lock.json...'
    (cd "$ui_root" && npm ci)
  fi
fi

printf 'DragonKMG local setup check passed (.NET %s, Node.js %s).\n' "$dotnet_version" "$node_version"
printf 'Shared session database: %s\n' "$db_path"
if ((check_only)); then
  echo 'Check-only mode: no services were started and no token was created.'
  exit 0
fi

check_port() {
  node -e 'const net=require("node:net");const p=Number(process.argv[1]);const s=net.createServer();s.once("error",()=>process.exit(1));s.listen(p,"127.0.0.1",()=>s.close(()=>process.exit(0)))' "$1"
}
for port in 5188 5173; do
  if ! check_port "$port"; then
    printf 'Port %s is already in use. Close the application using it and retry; DragonKMG will not stop existing processes.\n' "$port" >&2
    exit 1
  fi
done
mkdir -p "$(dirname -- "$db_path")"

token="$(node -e 'process.stdout.write(require("node:crypto").randomBytes(32).toString("base64url"))')"
api_pid=''
ui_pid=''
cleanup() {
  trap - EXIT INT TERM
  [[ -z "$ui_pid" ]] || kill "$ui_pid" 2>/dev/null || true
  [[ -z "$api_pid" ]] || kill "$api_pid" 2>/dev/null || true
  [[ -z "$ui_pid" ]] || wait "$ui_pid" 2>/dev/null || true
  [[ -z "$api_pid" ]] || wait "$api_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

(cd "$repo_root" && ASPNETCORE_ENVIRONMENT=Development DRAGONKMG_LOCAL_API_TOKEN="$token" DRAGONKMG_DB_PATH="$db_path" DRAGONKMG_URL=http://127.0.0.1:5188 dotnet run --project "$api_project" --no-launch-profile) &
api_pid=$!
ready=0
for _ in $(seq 1 60); do
  if curl --silent --fail --max-time 2 http://127.0.0.1:5188/health >/dev/null; then ready=1; break; fi
  if ! kill -0 "$api_pid" 2>/dev/null; then break; fi
  sleep 1
done
if (( ! ready )); then
  echo 'The API did not become healthy within 60 seconds. Review the API output above.' >&2
  exit 1
fi

(cd "$ui_root" && env -u DRAGONKMG_LOCAL_API_TOKEN npm run dev -- --host 127.0.0.1 --strictPort) &
ui_pid=$!
for _ in $(seq 1 30); do
  if curl --silent --fail --max-time 2 http://127.0.0.1:5173/ >/dev/null; then break; fi
  if ! kill -0 "$ui_pid" 2>/dev/null; then echo 'Dashboard failed to start; review the UI output above.' >&2; exit 1; fi
  sleep 1
done
if ! curl --silent --fail --max-time 2 http://127.0.0.1:5173/ >/dev/null; then
  echo 'Dashboard did not become ready within 30 seconds.' >&2
  exit 1
fi

printf '\nDragonKMG is ready at http://127.0.0.1:5173/\nPaste this temporary local access token into the dashboard:\n%s\n' "$token"
echo 'The token exists only for this run. Keep this terminal private. Press Ctrl+C to stop services.'
if [[ "${DRAGONKMG_NO_BROWSER:-0}" != 1 ]]; then
  if command -v open >/dev/null; then open http://127.0.0.1:5173/ >/dev/null 2>&1 || true
  elif command -v xdg-open >/dev/null; then xdg-open http://127.0.0.1:5173/ >/dev/null 2>&1 || true
  fi
fi

if ((with_cli)); then
  (cd "$repo_root" && DRAGONKMG_DB_PATH="$db_path" dotnet run --project "$cli_project" -- tui)
else
  while kill -0 "$api_pid" 2>/dev/null && kill -0 "$ui_pid" 2>/dev/null; do sleep 2; done
  echo 'A DragonKMG service stopped. Press Ctrl+C to close the remaining service.'
  while kill -0 "$api_pid" 2>/dev/null || kill -0 "$ui_pid" 2>/dev/null; do sleep 2; done
fi
