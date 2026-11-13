import { Bell, ClipboardCheck, FileText, GitMerge, LayoutGrid, LogOut, Menu, Settings2 } from "lucide-react";
import Link from "next/link";
import type { Profile, Organization } from "../lib/server";
import { messages, type Locale } from "../lib/messages";
import { Preferences } from "./preferences";
import { canManageOrganization, organizationText } from "../lib/organization";
import { workflowText } from "../lib/workflows";

export function Shell({ children, profile, organization, locale, active }: {
  children: React.ReactNode; profile: Profile; organization?: Organization; locale: Locale; active?: string;
}) {
  const t = messages[locale];
  const base = organization ? `/workspace/${organization.id}` : "/workspace";
  const navigation = [
    { key: "overview", href: base, icon: LayoutGrid },
    { key: "requests", href: `${base}?view=requests`, icon: FileText },
    { key: "approvals", href: `${base}?view=approvals`, icon: ClipboardCheck },
    { key: "notifications", href: `${base}?view=notifications`, icon: Bell },
  ] as const;
  const nav = <nav aria-label={t.menu}>{organization && navigation.map(({ key, href, icon: Icon }) =>
    <Link key={key} href={href} aria-current={active === key ? "page" : undefined}><Icon size={17} aria-hidden />{t[key]}</Link>
  )}{organization && <Link href={`${base}/workflows`} aria-current={active === "workflows" ? "page" : undefined}>
    <GitMerge size={17} aria-hidden />{workflowText[locale].title}
  </Link>}{canManageOrganization(organization?.role) && <Link href={`${base}/settings`} aria-current={active === "settings" ? "page" : undefined}>
    <Settings2 size={17} aria-hidden />{organizationText[locale].settings}
  </Link>}<Link href="/workspace/organizations/new">{t.newOrganization}</Link></nav>;
  return <div className="shell">
    <aside className="sidebar">
      <Link className="brand" href="/workspace"><GitMerge size={23} aria-hidden />FlowOps</Link>
      <div className="workspace-marker"><span>{t.workspace}</span><strong>{organization?.name ?? "FlowOps"}</strong></div>
      {nav}
    </aside>
    <div className="shell-body">
      <header className="topbar">
        <div className="topbar-start">
          <details className="mobile-nav"><summary aria-label={t.menu}><Menu size={21} aria-hidden /></summary>{nav}</details>
          {profile.organizations.length > 0 && <details className="org-menu">
            <summary>{organization?.name ?? t.organization}</summary>
            <ul>{profile.organizations.map(org => <li key={org.id}><Link href={`/workspace/${org.id}`}>{org.name}</Link></li>)}</ul>
          </details>}
        </div>
        <div className="header-actions">
          <Preferences locale={locale} />
          <span className="profile-name">{profile.name}</span>
          <form action="/auth/logout" method="post"><button type="submit" className="button icon-button" aria-label={t.signOut}><LogOut size={17} aria-hidden /></button></form>
        </div>
      </header>
      <main className="workspace-main" id="main-content">{children}</main>
    </div>
  </div>;
}
