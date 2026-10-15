# Dashboard control center

## Scope and ownership

The dashboard uses eight navigation sections: Agents, Connections, Skills, Workflows, Sessions, Integrations, Security, and Identity/roles. Each section renders a dedicated component under `ui/src/components/dashboard`. `App.tsx` owns navigation and layout; `hooks/useDashboard.ts` owns shared server state and mutations; `services/api.ts` owns authenticated HTTP requests.

The existing shared Panel and List were extracted to `components/ui`. They translate interface text and empty states centrally; they do not own requests. Native labels, inputs, fieldsets and radio controls provide keyboard behavior. A shared disabled fieldset prevents edits while operations or agent-detail requests are pending. Loading failure clears agent selection to prevent replacing existing permissions from an empty view. Session generation guards prevent out-of-order message responses.

## Connections and keys

- Connection creation supports Local, RemoteSelfHosted, and Cloud trust zones and provider protocols already accepted by the backend.
- Provider secrets remain `env:NAME` or `vault:NAME` references.
- The global management token has its own AccessSettings component and stays in browser session storage.
- A default model connection is persisted in SQLite through authenticated GET/PUT `/api/settings/default-connection`.
- The default is used once when creating an agent. Existing agents retain independent connection/model bindings, editable in the Agents section.
- Public-key identity management is explicitly marked unavailable. Existing identity contracts/tables are not an authentication implementation.
- The Integrations section accurately displays MCP/n8n implementation status. Live webhook configuration and MCP invocation are not enabled by this UI refactor.

## Localization

`locales/ar.json`, `en.json`, `es.json`, and `de.json` share 162 keys. The language provider validates saved locale values, persists language selection, updates document lang/dir, and translates labels, actions, placeholders, errors, notices, empty states, operating modes, run statuses, and fixed council workflow text. Names/prompts/messages entered by users and provider outputs remain unchanged.

The language chooser uses menu radio buttons, focuses the selected language on opening, supports arrows/Home/End, closes on outside click or Escape, and returns focus to its trigger. Navigation is grouped into Workspace (Sessions, Workflows), Team setup (Agents, Skills, Connections, Integrations), and Administration (Identity, Security). A mobile disclosure replaces horizontal scrolling; selecting a section closes it and restores focus to the trigger. Logical CSS properties support RTL without changing the underlying data.

## Header and appearance update

- Header shows the current section with a workspace breadcrumb; language, appearance, and refresh controls use shared SVG icons.
- Light and dark palettes cover the full dashboard using semantic CSS variables, including form controls, status messages, navigation, and popovers.
- The appearance toggle persists `dragonkmg.theme`. First visit follows the system preference; an initialization script applies the theme before React renders to avoid a palette flash.
- Browser checks verified dark and light switching, saved appearance after reload, keyboard language selection and Escape focus return.
- Mobile tests at 390px verified the header and language popover remain inside the viewport in Arabic RTL and English LTR. An RTL popover alignment defect found during this check was corrected.
- Independent scoped header/theme source review returned ACCEPT. Build, lint and locale parity checks passed. No backend changes were required for this appearance update.

## Verification — 2026-10-15

- .NET solution build: 0 warnings, 0 errors.
- .NET tests: 16/16 passed, including default persistence, invalid/disabled default rejection, and preserving existing agent bindings after default changes.
- UI production build and lint passed.
- Locale check validates key parity, nonempty values, literal translation references, and shared panel text.
- Real browser: authenticated API read succeeded; German Connections, Spanish council stages/checklists, English Skills, Arabic RTL and language persistence checked.
- Responsive browser measurements: widths 390, 768 and 1280 had document width no greater than viewport width. Navigation intentionally scrolls within its own region on phones.
- JavaScript gzip: 73.87 kB baseline to 83.80 kB after adding all dictionaries and modules. This records bundle size only, not an interaction-performance benchmark.
- Independent source reviewer returned ACCEPT after stale selection, failed-detail-load, run-state labeling, and token-description fixes.

Decision: ACCEPTED WITH FOLLOW-UP for dashboard modularization/localization. Full live-provider runs, device certification, real multi-user identity and live integrations remain separate delivery work. No production certification is claimed.
