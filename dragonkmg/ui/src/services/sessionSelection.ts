const key = "dragonkmg.lastSessionId";
const validId = (value: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);

export function readSessionSelection(url: string, remembered: string | null): string {
  const explicit = new URL(url).searchParams.get("session");
  if (explicit !== null) return validId(explicit) ? explicit : "";
  return remembered && validId(remembered) ? remembered : "";
}

export function initialSessionSelection(): string {
  try { return readSessionSelection(location.href, localStorage.getItem(key)); }
  catch { return readSessionSelection(location.href, null); }
}

export function rememberSession(value: string, push = false) {
  const url = new URL(location.href);
  if (value) url.searchParams.set("session", value); else url.searchParams.delete("session");
  url.hash = "sessions";
  if (push) history.pushState(null, "", url); else history.replaceState(null, "", url);
  try { if (value) localStorage.setItem(key, value); else localStorage.removeItem(key); } catch { /* Navigation still works when browser storage is unavailable. */ }
}
