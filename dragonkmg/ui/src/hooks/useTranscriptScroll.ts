import { useLayoutEffect, useRef, useState } from "react";
import { isNearLatest, restoredScrollTop, type ScrollAnchor } from "../services/chatScroll";

export function useTranscriptScroll(sessionId: string, revision: unknown) {
  const transcript = useRef<HTMLDivElement>(null);
  const content = useRef<HTMLDivElement>(null);
  const following = useRef(true);
  const anchor = useRef<ScrollAnchor | null>(null);
  const session = useRef(sessionId);
  const [showLatest, setShowLatest] = useState(false);

  function remember() {
    const element = transcript.current;
    if (!element) return;
    const top = element.getBoundingClientRect().top;
    const visible = Array.from(element.querySelectorAll<HTMLElement>("[data-scroll-id]"))
      .find(item => item.getBoundingClientRect().bottom > top);
    anchor.current = visible ? { id: visible.dataset.scrollId!, offset: visible.getBoundingClientRect().top - top } : null;
  }
  function reconcile() {
    const element = transcript.current;
    if (!element) return;
    const saved = anchor.current;
    const target = saved ? Array.from(element.querySelectorAll<HTMLElement>("[data-scroll-id]"))
      .find(item => item.dataset.scrollId === saved.id) : null;
    const delta = target && saved ? target.getBoundingClientRect().top - element.getBoundingClientRect().top - saved.offset : 0;
    element.scrollTop = restoredScrollTop({ top: element.scrollTop, height: element.scrollHeight, viewport: element.clientHeight }, following.current, delta);
    setShowLatest(!following.current && !isNearLatest({ top: element.scrollTop, height: element.scrollHeight, viewport: element.clientHeight }));
    remember();
  }
  useLayoutEffect(() => {
    if (session.current !== sessionId) {
      session.current = sessionId;
      following.current = true;
      anchor.current = null;
    }
    reconcile();
  }, [sessionId, revision]);
  useLayoutEffect(() => {
    // Observe both the viewport and actual content: details, wrapping and process rows can grow independently.
    const observer = new ResizeObserver(reconcile);
    if (transcript.current) observer.observe(transcript.current);
    if (content.current) observer.observe(content.current);
    return () => observer.disconnect();
  }, []);
  return { transcript, content, showLatest, onScroll() {
    const element = transcript.current;
    if (!element) return;
    following.current = isNearLatest({ top: element.scrollTop, height: element.scrollHeight, viewport: element.clientHeight });
    setShowLatest(!following.current);
    remember();
  }, jumpToLatest() { following.current = true; reconcile(); } };
}
