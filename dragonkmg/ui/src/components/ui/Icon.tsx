type IconName = "sun" | "moon" | "globe" | "chevron" | "refresh" | "check" | "chat" | "workflow" | "agents" | "skills" | "connections" | "integrations" | "user" | "shield" | "menu";
const paths: Record<IconName, string[]> = {
  chat: ["M21 11.5a8.5 8.5 0 0 1-8.5 8.5H4l-2 2V11.5a9.5 9.5 0 0 1 19 0Z", "M7 9h9M7 13h6"],
  workflow: ["M9 3H3v6h6V3ZM21 15h-6v6h6v-6ZM6 9v9h9M9 6h9v9"],
  agents: ["M8 7V4h8v3M4 7h16v13H4V7ZM8 12h.01M16 12h.01M8 16h8M1 11v5M23 11v5"],
  skills: ["m12 3 2.5 5.5L20 11l-5.5 2.5L12 19l-2.5-5.5L4 11l5.5-2.5L12 3ZM20 17v5M17.5 19.5h5"],
  connections: ["M9 8 7 6a3 3 0 0 0-4 4l4 4a3 3 0 0 0 4 0M15 16l2 2a3 3 0 0 0 4-4l-4-4a3 3 0 0 0-4 0M8 16l8-8"],
  integrations: ["M8 3v5M16 3v5M5 8h14v3a7 7 0 0 1-14 0V8ZM12 18v4"],
  user: ["M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0M4 21v-2a8 8 0 0 1 16 0v2"],
  shield: ["m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z", "m8 12 3 3 5-6"],
  menu: ["M4 6h16M4 12h16M4 18h16"],
  sun: [
    "M12 3v1m0 16v1M3 12h1m16 0h1M5.64 5.64l.71.71m11.3 11.3.71.71M5.64 18.36l.71-.71m11.3-11.3.71-.71",
    "M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
  ],
  moon: ["M20.9 13A9 9 0 0 1 11 3.1 9 9 0 1 0 20.9 13Z"],
  globe: [
    "M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0",
    "M3 12h18M12 3c4 5 4 13 0 18-4-5-4-13 0-18",
  ],
  chevron: ["m7 10 5 5 5-5"],
  refresh: [
    "M20 7v5h-5M4 17v-5h5",
    "M6.1 7a7 7 0 0 1 11.55-2.55L20 7M4 17l2.35 2.55A7 7 0 0 0 17.9 17",
  ],
  check: ["m5 12 4 4L19 6"],
};
export function Icon({
  name,
  className = "",
}: {
  name: IconName;
  className?: string;
}) {
  return (
    <svg
      className={"icon " + className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {paths[name].map((path) => (
        <path d={path} key={path} />
      ))}
    </svg>
  );
}
