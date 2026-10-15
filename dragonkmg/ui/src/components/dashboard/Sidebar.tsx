import { useRef, useState } from "react";
import { useI18n } from "../../i18n";
import { Icon } from "../ui/Icon";
import { DragonLogo } from "../ui/DragonLogo";

const groups = [
  { label: "Workspace", items: [{ key: "Sessions", icon: "chat" }, { key: "Workflows", icon: "workflow" }] },
  { label: "Configuration", items: [{ key: "Agents", icon: "agents" }, { key: "Skills", icon: "skills" }, { key: "Connections", icon: "connections" }, { key: "Integrations", icon: "integrations" }] },
  { label: "Administration", items: [{ key: "Identity", icon: "user" }, { key: "Security", icon: "shield" }] },
] as const;
export type DashboardSection = (typeof groups)[number]["items"][number]["key"];

export function Sidebar({ active, onSelect }: { active: DashboardSection; onSelect: (section: DashboardSection) => void }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const toggle = useRef<HTMLButtonElement>(null);
  return <aside className="sidebar">
    <div className="sidebar-top">
      <a className="brand" href="#workspace"><DragonLogo className="brand-mark" /><span>DragonKMG<small>AGENT PLATFORM</small></span></a>
      <button type="button" ref={toggle} className="header-control mobile-nav-toggle" aria-expanded={open} aria-controls="workspace-navigation" aria-label={t("Navigation")} onClick={() => setOpen(!open)}>
        <Icon name="menu" /><span>{t(active)}</span><Icon name="chevron" />
      </button>
    </div>
    <nav id="workspace-navigation" className={"sidebar-nav" + (open ? " is-open" : "")} aria-label={t("Navigation")}
      onKeyDown={event => { if (event.key === "Escape") { setOpen(false); toggle.current?.focus(); } }}>
      {groups.map(group => <div className="nav-group" key={group.label}>
        <p className="nav-group-title">{t(group.label)}</p>
        {group.items.map(item => <button type="button" key={item.key} className={"nav-item" + (active === item.key ? " active" : "")} aria-current={active === item.key ? "page" : undefined}
          onClick={() => { onSelect(item.key); setOpen(false); if (matchMedia("(max-width: 700px)").matches) toggle.current?.focus(); }}>
          <Icon name={item.icon} /><span>{t(item.key)}</span>
        </button>)}
      </div>)}
    </nav>
    <div className="sidebar-footer"><span className="status-dot" />{t("LOCAL CONTROL PLANE")}</div>
  </aside>;
}
