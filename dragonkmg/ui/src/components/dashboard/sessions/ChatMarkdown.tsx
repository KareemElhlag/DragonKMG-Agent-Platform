import type { ReactNode } from "react";

function inline(text: string, keyPrefix: string): ReactNode[] {
  const token = /(\[[^\]]+\]\(https?:\/\/[^\s)]+\)|`[^`]+`|\*\*[^*]+\*\*|__[^_]+__|\*[^*]+\*)/g;
  const nodes: ReactNode[] = [];
  let offset = 0;
  let match: RegExpExecArray | null;
  while ((match = token.exec(text))) {
    if (match.index > offset) nodes.push(text.slice(offset, match.index));
    const value = match[0];
    if (value.startsWith("[")) {
      const link = /^\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)$/.exec(value);
      if (link) nodes.push(<a key={`${keyPrefix}-${match.index}`} href={link[2]} target="_blank" rel="noreferrer noopener">{link[1]}</a>);
      else nodes.push(value);
    } else if (value.startsWith("`")) {
      nodes.push(<code key={`${keyPrefix}-${match.index}`} dir="ltr">{value.slice(1, -1)}</code>);
    } else if (value.startsWith("**") || value.startsWith("__")) {
      nodes.push(<strong key={`${keyPrefix}-${match.index}`}>{value.slice(2, -2)}</strong>);
    } else {
      nodes.push(<em key={`${keyPrefix}-${match.index}`}>{value.slice(1, -1)}</em>);
    }
    offset = match.index + value.length;
  }
  if (offset < text.length) nodes.push(text.slice(offset));
  return nodes;
}

export function ChatMarkdown({ source }: { source: string }) {
  const lines = source.replace(/\r\n?/g, "\n").split("\n");
  const blocks: ReactNode[] = [];
  let paragraph: string[] = [];
  let list: string[] = [];
  let ordered = false;
  let code: string[] | null = null;
  const flushParagraph = () => {
    if (!paragraph.length) return;
    blocks.push(<p key={`p-${blocks.length}`} dir="auto">{inline(paragraph.join(" "), `p-${blocks.length}`)}</p>);
    paragraph = [];
  };
  const flushList = () => {
    if (!list.length) return;
    const Tag = ordered ? "ol" : "ul";
    blocks.push(<Tag key={`list-${blocks.length}`}>{list.map((item, index) => <li key={index} dir="auto">{inline(item, `li-${blocks.length}-${index}`)}</li>)}</Tag>);
    list = [];
  };

  for (const [index, line] of lines.entries()) {
    const fence = /^\s*```/.test(line);
    if (code) {
      if (fence) {
        blocks.push(<pre key={`code-${index}`} dir="ltr"><code>{code.join("\n")}</code></pre>);
        code = null;
      } else code.push(line);
      continue;
    }
    if (fence) { flushParagraph(); flushList(); code = []; continue; }
    if (!line.trim()) { flushParagraph(); flushList(); continue; }
    const heading = /^(#{1,6})\s+(.+)$/.exec(line);
    if (heading) {
      flushParagraph(); flushList();
      const level = heading[1].length;
      const content = inline(heading[2], `h-${index}`);
      const key = `h-${index}`;
      if (level === 1) blocks.push(<h3 key={key} dir="auto">{content}</h3>);
      else if (level === 2) blocks.push(<h3 key={key} dir="auto">{content}</h3>);
      else if (level === 3) blocks.push(<h4 key={key} dir="auto">{content}</h4>);
      else blocks.push(<h5 key={key} dir="auto">{content}</h5>);
      continue;
    }
    const bullet = /^\s*[-*+]\s+(.+)$/.exec(line);
    const number = /^\s*\d+[.)]\s+(.+)$/.exec(line);
    if (bullet || number) {
      flushParagraph();
      const isOrdered = Boolean(number);
      if (list.length && ordered !== isOrdered) flushList();
      ordered = isOrdered;
      list.push((bullet ?? number)![1]);
      continue;
    }
    const quote = /^\s*>\s?(.*)$/.exec(line);
    if (quote) {
      flushParagraph(); flushList();
      blocks.push(<blockquote key={`q-${index}`} dir="auto">{inline(quote[1], `q-${index}`)}</blockquote>);
      continue;
    }
    flushList();
    paragraph.push(line);
  }
  if (code) blocks.push(<pre key={`code-${blocks.length}`} dir="ltr"><code>{code.join("\n")}</code></pre>);
  flushParagraph(); flushList();
  return <div className="chat-markdown">{blocks}</div>;
}
