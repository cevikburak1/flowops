import { Bell, ChartNoAxesColumnIncreasing, ChevronsUpDown, ClipboardCheck, FileText, GitMerge, LayoutGrid, ListTodo, LogOut, Menu, Plus, Settings2, ShieldCheck, Workflow } from "lucide-react";
import Link from "next/link";
import type { Profile, Organization } from "../lib/server";
import { messages, type Locale } from "../lib/messages";
import { Preferences } from "./preferences";
import { canManageOrganization, organizationText } from "../lib/organization";
import { workflowText } from "../lib/workflows";
import { taskText } from "../lib/tasks";
import { LiveNotifications } from "./live-notifications";
import { reportText } from "../lib/reports";
import { processText } from "../lib/processes";

export function Shell({ children, profile, organization, locale, active }: {
  children: React.ReactNode; profile: Profile; organization?: Organization; locale: Locale; active?: string;
}) {
  const t = messages[locale];
  const base = organization ? `/workspace/${organization.id}` : "/workspace";
  const operations = [
    { key: "overview", href: base, icon: LayoutGrid, label: t.overview },
    { key: "requests", href: `${base}?view=requests`, icon: FileText, label: t.requests },
    { key: "approvals", href: `${base}?view=approvals`, icon: ClipboardCheck, label: t.approvals },
    { key: "tasks", href: `${base}/tasks`, icon: ListTodo, label: taskText[locale].title },
    { key: "notifications", href: `${base}?view=notifications`, icon: Bell, label: t.notifications },
  ];
  const configuration = [
    { key: "workflows", href: `${base}/workflows`, icon: GitMerge, label: workflowText[locale].title },
    { key: "processes", href: `${base}/processes`, icon: Workflow, label: processText[locale].title },
    ...(canManageOrganization(organization?.role) ? [
      { key: "reports", href: `${base}/reports`, icon: ChartNoAxesColumnIncreasing, label: reportText[locale].title },
      { key: "settings", href: `${base}/settings`, icon: Settings2, label: organizationText[locale].settings },
    ] : []),
  ];
  const renderLinks = (items: typeof operations) => items.map(({ key, href, icon: Icon, label }) =>
    <Link key={key} href={href} aria-current={active === key ? "page" : undefined}><Icon size={17} aria-hidden />{label}</Link>);
  const nav = <nav className="workspace-nav" aria-label={t.menu}>
    {organization && <>
      <div className="nav-group"><span>{t.operations}</span>{renderLinks(operations)}</div>
      <div className="nav-group"><span>{t.configuration}</span>{renderLinks(configuration)}</div>
    </>}
    {profile.isOperator && <Link href="/workspace/platform" aria-current={active === "platform" ? "page" : undefined}>
      <ShieldCheck size={17} aria-hidden />{reportText[locale].platform}
    </Link>}
    <Link className="nav-create" href="/workspace/organizations/new"><Plus size={17} aria-hidden />{t.newOrganization}</Link>
  </nav>;
  const initials = (name: string) => name.trim().split(/\s+/).slice(0, 2).map(part => part.charAt(0).toLocaleUpperCase(locale)).join("");
  return <div className="shell">
    {organization && <LiveNotifications />}
    <aside className="sidebar">
      <Link className="brand" href="/workspace"><span className="brand-mark"><GitMerge size={19} aria-hidden /></span>FlowOps</Link>
      {organization && <div className="workspace-marker">
        <span className="workspace-initial" aria-hidden>{initials(organization.name)}</span>
        <div><span>{t.workspace}</span><strong>{organization.name}</strong></div>
      </div>}
      {nav}
    </aside>
    <div className="shell-body">
      <header className="topbar">
        <div className="topbar-start">
          <details className="mobile-nav"><summary aria-label={t.menu}><Menu size={21} aria-hidden /></summary>{nav}</details>
          {profile.organizations.length > 0 && <details className="org-menu">
            <summary><span>{organization?.name ?? t.organization}</span><ChevronsUpDown size={14} aria-hidden /></summary>
            <ul>{profile.organizations.map(org => <li key={org.id}><Link href={`/workspace/${org.id}`}>{org.name}</Link></li>)}</ul>
          </details>}
        </div>
        <div className="header-actions">
          <Preferences locale={locale} />
          {profile.name && <span className="profile-name"><span className="profile-initial" aria-hidden>{initials(profile.name)}</span>{profile.name}</span>}
          <form action="/auth/logout" method="post"><button type="submit" className="button icon-button" aria-label={t.signOut}><LogOut size={17} aria-hidden /></button></form>
        </div>
      </header>
      <main className="workspace-main" id="main-content">{children}</main>
    </div>
  </div>;
}
