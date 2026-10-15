/** Canonical brand mark. Decorative beside a wordmark; name standalone marks explicitly.
 * Fixed dimensions prevent layout shift. The silhouette intentionally never mirrors in RTL.
 * Use the shared SVG rather than copying paths into feature components.
 */
export function DragonLogo({
  size = 40,
  decorative = true,
  className,
}: {
  size?: number;
  decorative?: boolean;
  className?: string;
}) {
  return (
    <img
      src="/dragon-mark.svg"
      width={size}
      height={size}
      alt={decorative ? "" : "DragonKMG"}
      aria-hidden={decorative ? true : undefined}
      className={className}
      draggable={false}
    />
  );
}
