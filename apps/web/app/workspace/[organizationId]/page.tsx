import Link from "next/link";
import { Plus } from "lucide-react";
import { notFound } from "next/navigation";
import { Shell } from "../../../components/shell";
import { MarkRead } from "../../../components/forms";
import { getApi, getLocale, messages, type Profile, type Purchase, type Workspace } from "../../../lib/server";
import { label, type Locale } from "../../../lib/messages";

function PurchaseTable({ title, items, base, locale, empty }: {
  title: string; items: Purchase[]; base: string; locale: Locale; empty: string;
}) {
  const t = messages[locale];
  return <section className="panel data-panel">
    <h2>{title}</h2>
    {items.length === 0 ? <p className="empty-state">{empty}</p> : <div className="table-scroll" role="region" aria-label={title} tabIndex={0}>
      <table>
        <thead><tr><th scope="col">{t.purchase}</th><th scope="col">{t.status}</th><th scope="col" className="amount">{t.amount}</th><th scope="col">{t.date}</th></tr></thead>
        <tbody>{items.map(item => <tr key={item.id}>
          <td><Link href={`${base}/purchases/${item.id}`}>{item.title}</Link><span className="row-meta">{label(locale, item.category)}</span></td>
          <td><span className="status" data-status={item.status}>{label(locale, item.status)}</span></td>
          <td className="amount">{new Intl.NumberFormat(locale, { style: "currency", currency: item.currency }).format(item.amount)}</td>
          <td>{new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeZone: "UTC" }).format(new Date(item.createdAt))}</td>
        </tr>)}</tbody>
      </table>
    </div>}
  </section>;
}

export default async function OrganizationWorkspace({ params, searchParams }: {
  params: Promise<{ organizationId: string }>; searchParams: Promise<{ view?: string; page?: string }>;
}) {
  const [{ organizationId }, query, locale, profile] = await Promise.all([params, searchParams, getLocale(), getApi<Profile>("/api/me")]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization) notFound();
  const view = query.view === "requests" || query.view === "approvals" || query.view === "notifications" ? query.view : "overview";
  const base = `/workspace/${organization.id}`;
  const api = `/api/organizations/${organization.id}`;
  const data = await getApi<Workspace>(`${api}/workspace?page=${encodeURIComponent(query.page ?? "1")}`);
  const t = messages[locale];
  const submitted = data.statuses.reduce((count, entry) => count + entry.count, 0);
  const inReview = data.statuses.filter(entry => ["starting", "managerReview", "financeReview"].includes(entry.status))
    .reduce((count, entry) => count + entry.count, 0);
  const total = view === "approvals" ? data.pendingCount : submitted;
  return <Shell profile={profile} organization={organization} locale={locale} active={view}>
    <header className="page-header"><h1>{t[view]}</h1>{data.canSubmit && <Link className="button primary" href={`${base}/purchases/new`}><Plus size={17} aria-hidden />{t.newRequest}</Link>}</header>
    {view === "overview" && !data.canSubmit && ["Employee", "OrganizationOwner"].includes(organization.role) &&
      <p className="workspace-note muted">{t.noManager}</p>}
    {view === "overview" && <dl className="summary">
      <div><dt><Link href={`${base}?view=requests`}>{t.requests}</Link></dt><dd>{submitted}</dd></div>
      <div><dt>{t.review}</dt><dd>{inReview}</dd></div>
      <div><dt><Link href={`${base}?view=approvals`}>{t.pending}</Link></dt><dd>{data.pendingCount}</dd></div>
    </dl>}
    {view === "approvals" && <PurchaseTable title={t.queue} items={data.approvals} base={base} locale={locale} empty={t.emptyApprovals} />}
    {(view === "overview" || view === "requests") && <PurchaseTable title={t.requests} items={data.purchases} base={base} locale={locale} empty={t.emptyRequests} />}
    {view === "notifications" && <section className="panel data-panel">
      <h2>{t.notifications}</h2>
      {data.notifications.length === 0 ? <p className="empty-state">{t.emptyNotifications}</p> : <ul className="notification-list">
        {data.notifications.map(item => <li key={item.id}><div><Link href={`${base}/purchases/${item.purchaseId}`}>{item.title}</Link>
          <span className="row-meta">{label(locale, item.kind)}</span><time className="row-meta" dateTime={item.createdAt}>
            {new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(new Date(item.createdAt))}
          </time></div>
          {!item.readAt && <MarkRead path={`${api}/notifications/${item.id}/read`} locale={locale} />}</li>)}
      </ul>}
    </section>}
    {(view === "approvals" || view === "requests" || view === "overview") && (data.page > 1 || total > data.pageSize) &&
      <div className="pager">{data.page > 1 && <Link className="button secondary" href={`${base}?view=${view}&page=${data.page - 1}`}>{t.previous}</Link>}
        {total > data.page * data.pageSize && <Link className="button secondary" href={`${base}?view=${view}&page=${data.page + 1}`}>{t.next}</Link>}</div>}
  </Shell>;
}
