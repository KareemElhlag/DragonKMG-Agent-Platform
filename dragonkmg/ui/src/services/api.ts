import type { TranslationKey } from "../i18n";
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  constructor(status: number, code = "") {
    super("API request failed");
    this.status = status;
    this.code = code;
  }
}
const api = import.meta.env.VITE_DRAGONKMG_API_URL ?? "";
export async function request<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const token = sessionStorage.getItem("dragonkmg.localApiToken");
  const response = await fetch(`${api}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { "X-DragonKMG-Local-Token": token } : {}),
      ...options?.headers,
    },
  });
  if (!response.ok) {
    if (response.status === 401 && token === sessionStorage.getItem("dragonkmg.localApiToken")) {
      window.dispatchEvent(new Event("dragonkmg:authentication-required"));
    }
    let code = "";
    try { const body: unknown = await response.json(); if (body && typeof body === "object" && "code" in body && typeof body.code === "string") code = body.code; } catch { /* Do not display raw provider errors. */ }
    throw new ApiError(response.status, code);
  }
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}
export function providerErrorKey(error: unknown): TranslationKey {
  const codes = ["CloudBlocked", "ConnectionUnavailable", "ConnectionDisabled", "SecretUnavailable", "ModelDisabled", "VaultUnavailable", "InvalidConfiguration", "ProviderTimeout", "ProviderFailed"] as const;
  if (error instanceof ApiError) {
    if (error.status === 401) return "Access denied. Check the local API token.";
    const code = codes.find(value => value === error.code);
    if (code) return code;
  }
  return "The operation could not be completed.";
}
