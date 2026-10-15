import { useLayoutEffect, useState } from "react";

type Theme = "light" | "dark";
const storageKey = "dragonkmg.theme";
function savedTheme(): Theme | null {
  try {
    const value = localStorage.getItem(storageKey);
    return value === "light" || value === "dark" ? value : null;
  } catch {
    return null;
  }
}
export function useTheme() {
  const [theme, setTheme] = useState<Theme>(
    () =>
      savedTheme() ??
      (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"),
  );
  useLayoutEffect(() => {
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = theme;
  }, [theme]);
  const toggleTheme = () => {
    const next = theme === "dark" ? "light" : "dark";
    try {
      localStorage.setItem(storageKey, next);
    } catch {
      /* Theme remains usable when storage is unavailable. */
    }
    setTheme(next);
  };
  return { theme, toggleTheme };
}
