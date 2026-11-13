import Link from "next/link";
import { ArrowRight, Bell, Check, ClipboardCheck, Clock3, FileText, Plus, X } from "lucide-react";
import { notFound } from "next/navigation";
import { Shell } from "../../../components/shell";
import { MarkRead } from "../../../components/forms";
import { Pagination } from "../../../components/pagination";
import { PurchaseActivity, type ActivityPeriod } from "../../../components/purchase-activity";
import { getApi, getLocale, messages, type Profile, type Purchase, type Workspace } from "../../../lib/server";
import { label, type Locale } from "../../../lib/messages";

function PurchaseTable({ title, items, base, locale, empty, all }: {
  title: string; items: Purchase[]; base: string; locale: Locale; empty: string; all?: boolean;
}) {
  const t = messages[locale];
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeZone: "UTC" });
  return <section className="panel data-panel">
    <div className="panel-heading"><h2>{title}</h2>{all && <Link className="panel-link" href={`${base}?view=requests`}>{t.viewRequests}<ArrowRight size={15} aria-hidden /></Link>}</div>
    {items.length === 0 ? <p className="empty-state">{empty}</p> : <div className="table-scroll" role="region" aria-label={title} tabIndex={0}>
      <table className="purchase-table">
        <thead><tr><th scope="col">{t.purchase}</th><th scope="col">{t.status}</th><th scope="col" className="amount">{t.amount}</th><th scope="col" className="purchase-date">{t.date}</th></tr></thead>
        <tbody>{items.map(item => <tr key={item.id}>
          <td><Link href={`${base}/purchases/${item.id}`}>{item.title}</Link><span className="row-meta">{label(locale, item.category)}</span>
            <time className="row-meta purchase-date-compact" dateTime={item.createdAt}>{date.format(new Date(item.createdAt))}</time></td>
          <td><span className="status" data-status={item.status}>{label(locale, item.status)}</span></td>
          <td className="amount">{new Intl.NumberFormat(locale, { style: "currency", currency: item.currency }).format(item.amount)}</td>
          <td className="purchase-date"><time dateTime={item.createdAt}>{date.format(new Date(item.createdAt))}</time></td>
        </tr>)}</tbody>
      </table>
    </div>}
  </section>;
}

function NotificationList({ items, base, api, locale }: {
  items: Workspace["notifications"]; base: string; api?: string; locale: Locale;
}) {
  const t = messages[locale];
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeZone: "UTC" });
  if (!items.length) return <p className="empty-state">{t.emptyNotifications}</p>;
  return <ul className="notification-list">
    {items.map(item => <li key={item.id} data-unread={!item.readAt || undefined}>
      <span className="activity-icon" aria-hidden>{item.kind.startsWith("approval") ? <ClipboardCheck size={17} /> : <Bell size={17} />}</span>
      <div className="notification-copy">
        <Link href={`${base}/purchases/${item.purchaseId}`}>{item.title}</Link>
        <span className="row-meta">{label(locale, item.kind)}</span>
        <time className="row-meta" dateTime={item.createdAt}>{date.format(new Date(item.createdAt))}</time>
      </div>
      {!item.readAt && (api ? <MarkRead path={`${api}/notifications/${item.id}/read`} locale={locale} /> :
        <span className="unread-dot" role="img" aria-label={t.unread} />)}
    </li>)}
  </ul>;
}

function Overview({ data, base, locale, period }: { data: Workspace; base: string; locale: Locale; period: ActivityPeriod }) {
  const t = messages[locale];
  const count = (status: string) => data.statuses.find(entry => entry.status === status)?.count ?? 0;
  const submitted = data.statuses.reduce((total, entry) => total + entry.count, 0);
  const approved = count("approved");
  const rejected = count("rejected");
  const inReview = count("starting") + count("managerReview") + count("financeReview");
  const metrics = [
    { name: t.requests, value: submitted, Icon: FileText },
    { name: t.review, value: inReview, Icon: Clock3, status: "review" },
    { name: t.approved, value: approved, Icon: Check, status: "approved" },
    { name: t.rejected, value: rejected, Icon: X, status: "rejected" },
  ];
  const number = new Intl.NumberFormat(locale);
  return <div className="overview-content">
    <div className="overview-grid">
      <section className="panel overview-status">
        <h2>{t.requestBreakdown}</h2>
        <dl className="overview-metrics">
          {metrics.map(({ name, value, Icon, status }) => <div key={name} data-status={status}>
            <dt><Icon size={15} aria-hidden />{name}</dt><dd>{number.format(value)}</dd>
          </div>)}
        </dl>
        {submitted > 0 && <div className="status-distribution" aria-hidden>
          {metrics.filter(metric => metric.status && metric.value > 0).map(metric =>
            <span key={metric.status} data-status={metric.status} style={{ flexGrow: metric.value }} />)}
        </div>}
      </section>
      <section className="panel overview-queue">
        <div className="queue-title"><span className="activity-icon"><ClipboardCheck size={19} aria-hidden /></span>
          <h2>{t.pending}</h2><strong>{number.format(data.pendingCount)}</strong>
        </div>
        {data.approvals.length ? <ul>
          {data.approvals.slice(0, 2).map(item => <li key={`${item.id}:${item.stage}`}>
            <Link href={`${base}/purchases/${item.id}`}>{item.title}</Link>
            <div className="queue-meta row-meta"><span>{item.stage === 1 ? t.manager : t.finance}</span>
              <span className="amount">{new Intl.NumberFormat(locale, { style: "currency", currency: item.currency }).format(item.amount)}</span></div>
          </li>)}
        </ul> : <p className="muted">{t.emptyApprovals}</p>}
        <Link className="panel-link" href={`${base}?view=approvals`}>{t.viewApprovals}<ArrowRight size={15} aria-hidden /></Link>
      </section>
    </div>
    <PurchaseActivity key={period} rows={data.activity} asOf={data.asOf} period={period} base={base} locale={locale} />
    <div className="overview-grid overview-detail">
      <PurchaseTable title={t.recentRequests} items={data.purchases.slice(0, 6)} base={base} locale={locale} empty={t.emptyRequests} all />
      <section className="panel data-panel">
        <div className="panel-heading"><h2>{t.recentActivity}</h2></div>
        <NotificationList items={data.notifications.slice(0, 4)} base={base} locale={locale} />
        <div className="panel-footer"><Link className="panel-link" href={`${base}?view=notifications`}>{t.viewNotifications}<ArrowRight size={15} aria-hidden /></Link></div>
      </section>
    </div>
  </div>;
}

export default async function OrganizationWorkspace({ params, searchParams }: {
  params: Promise<{ organizationId: string }>; searchParams: Promise<{ view?: string; page?: string; months?: string }>;
}) {
  const [{ organizationId }, query, locale, profile] = await Promise.all([params, searchParams, getLocale(), getApi<Profile>("/api/me")]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization) notFound();
  const view = query.view === "requests" || query.view === "approvals" || query.view === "notifications" ? query.view : "overview";
  const base = `/workspace/${organization.id}`;
  const api = `/api/organizations/${organization.id}`;
  const page = typeof query.page === "string" && /^\d{1,5}$/.test(query.page) ? query.page : "1";
  const period = query.months === "6" ? 6 : query.months === "48" ? 48 : 12;
  const paramsForData = new URLSearchParams(view === "overview" ? { months: String(period) } :
    view === "notifications" ? { notificationPage: page } : { page });
  const data = await getApi<Workspace>(`${api}/workspace?${paramsForData}`);
  const t = messages[locale];
  const submitted = data.statuses.reduce((count, entry) => count + entry.count, 0);
  const total = view === "approvals" ? data.pendingCount : submitted;
  return <Shell profile={profile} organization={organization} locale={locale} active={view}>
    <header className="page-header"><div><h1>{t[view]}</h1><p>{t[`${view}Hint`]}</p></div>
      {data.canSubmit && <Link className="button primary" href={`${base}/purchases/new`}><Plus size={17} aria-hidden />{t.newRequest}</Link>}
    </header>
    {view === "overview" && !data.canSubmit && ["Employee", "OrganizationOwner"].includes(organization.role) &&
      <p className="workspace-note muted">{t.noManager}</p>}
    {view === "overview" && <Overview data={data} base={base} locale={locale} period={period} />}
    {view === "approvals" && <PurchaseTable title={t.queue} items={data.approvals} base={base} locale={locale} empty={t.emptyApprovals} />}
    {view === "requests" && <PurchaseTable title={t.requests} items={data.purchases} base={base} locale={locale} empty={t.emptyRequests} />}
    {view === "notifications" && <section className="panel data-panel" aria-label={t.notifications}>
      <NotificationList items={data.notifications} base={base} api={api} locale={locale} />
    </section>}
    {view !== "overview" && <Pagination page={view === "notifications" ? data.notificationPage : data.page}
      more={view === "notifications" ? data.notificationHasMore : total > data.page * data.pageSize}
      href={next => `${base}?view=${view}&page=${next}`} locale={locale} label={t[view]} />}
  </Shell>;
}
