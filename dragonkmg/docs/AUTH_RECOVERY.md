# Dashboard authentication recovery

The dashboard requires the local management token. This is separate from a model-provider API key. Authentication remains enforced by the API.

The browser stores the token in sessionStorage only after explicit form submission. Separate tabs may require their own sign-in; credentials are not placed in URLs or persistent localStorage.

Missing or rejected credentials show one recovery form instead of empty agent/session selectors and duplicate errors. Successful authentication reloads workspace data and restores the selected section and URL session. A delayed 401 for a replaced token cannot invalidate the new token.

Verification: UI build, lint and four-language parity; live browser invalid-token rejection and valid-token workspace recovery. No backend permissions, model keys, or database records changed. Rollback is limited to the frontend auth-gate and recovery-form changes; no migration is required.
