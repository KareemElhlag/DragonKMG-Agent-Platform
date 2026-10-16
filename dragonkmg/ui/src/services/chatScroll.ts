export interface ScrollMetrics { top: number; height: number; viewport: number }
export interface ScrollAnchor { id: string; offset: number }

export function isNearLatest(metrics: ScrollMetrics): boolean {
  return metrics.height - metrics.viewport - metrics.top <= 80;
}

/** Keep the same visible message, not a fraction of the changing transcript. */
export function restoredScrollTop(metrics: ScrollMetrics, follow: boolean, anchorDelta = 0): number {
  const maximum = Math.max(0, metrics.height - metrics.viewport);
  return follow ? maximum : Math.max(0, Math.min(maximum, metrics.top + anchorDelta));
}
