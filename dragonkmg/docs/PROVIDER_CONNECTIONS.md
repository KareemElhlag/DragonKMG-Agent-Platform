# Provider connections and per-connection models

Implemented locally on 2026-10-15. No provider credentials are bundled.

## Dashboard workflow

1. Open Connections > Add connection.
2. Set a descriptive name, trust zone, protocol and API base URL.
   - Local: loopback only; HTTP allowed on loopback.
   - Remote: HTTPS self-hosted/proxy endpoint.
   - Cloud: HTTPS hosted service; explicitly enable cloud egress after saving before discovery/testing.
3. Choose authentication: API key encrypted in the Windows DPAPI vault, env:NAME/vault:NAME reference, or no authentication.
   The plaintext key is submitted only to the authenticated local management API; responses/database contain a reference, not the key.
   Windows vault keys belong to the current OS account. Other systems must use environment references until a native vault adapter exists.
4. Save, then Discover models, or add the exact provider model ID manually.
5. Edit display names, enable/disable individual models, choose one default per connection and save.
6. Test connection checks the model catalog without inference. Test model sends a short inference request (up to 32 output tokens), which can incur provider charges.
7. In Agents, choose the connection and one of its enabled models; manual model IDs remain possible for providers without discovery.
   Existing agents retain their explicit binding; changing a default does not silently migrate them.

## Supported protocols and boundaries

| Protocol | Inference | Discovery |
| --- | --- | --- |
| OpenAI-compatible / Custom OpenAI-compatible | /v1/chat/completions | /v1/models |
| Ollama native | /api/chat | /api/tags |
| Claude / Anthropic | /v1/messages | /v1/models |
| Gemini | /v1beta/models/{id}:generateContent | /v1beta/models |

Base URLs retain proxy prefixes and existing version segments. Native version segments are supplied when absent.
Custom means OpenAI-compatible JSON, not arbitrary REST schemas. APIs with different authentication/body/response contracts need an adapter.
This implementation handles text, non-streaming completions; it does not claim multimodal/tool-call or streaming support.

Official protocol references:
- [Ollama chat](https://docs.ollama.com/api/chat), [catalog](https://docs.ollama.com/api/tags)
- [Claude messages](https://platform.claude.com/docs/en/api/messages/create), [models](https://platform.claude.com/docs/en/api/models/list)
- [Gemini generation](https://ai.google.dev/api/generate-content), [models](https://ai.google.dev/api/models)

## Safety and persistence

- Model registry persisted in SQLite, scoped by connection ID, with a unique default index.
- Discovery adds models without overwriting manual names, disabled states or defaults.
- Disabled-model checks canonicalize Gemini models/ prefixes idempotently, including repeated prefixes.
- Credentials in URL userinfo/query/fragment are rejected; redirects are disabled to avoid credential forwarding.
- Discovery uses a 15-second timeout, bounded JSON (4 MiB), maximum 2,000 entries and 10 pages.
- Model tests use a 60-second timeout; provider response excerpts are limited to 300 characters.
- Provider errors return safe codes instead of upstream response bodies.
- Cloud access remains explicit; the application does not automatically send user tasks to newly added hosts.

## Verification

Automated provider tests cover endpoint normalization, native payloads/auth headers and token parsing, four catalog formats, Gemini canonicalization, persisted defaults and preservation of disabled/manual metadata.
Full test suite: 33 passing tests at implementation checkpoint; UI build, lint and four-locale validation passed (191 keys each).
Local browser verified authenticated Connections form and translated protocol/authentication fields.
Provider protocol tests use mocked HTTP responses. Live external inference requires the user's configured endpoint/key and has not been claimed as verified.
