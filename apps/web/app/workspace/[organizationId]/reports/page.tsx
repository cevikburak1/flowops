import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../components/shell";
import { label } from "../../../../lib/messages";
import { canManageOrganization } from "../../../../lib/organization";
import { reportLabel, reportText, type Report } from "../../../../lib/reports";
import { getApi, getLocale, type Profile } from "../../../../lib/server";

export default async function ReportPage({ params, searchParams }: {
  params: Promise<{ organizationId: string }>;
  searchParams: Promise<{ days?: string }>;
}) {
  const [{ organizationId }, query, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(org => org.id === organizationId);
  if (!organization || !canManageOrganization(organization.role)) notFound();
  const days = query.days === "7" || query.days === "90" ? query.days : "30";
  const data = await getApi<Report>(`/api/organizations/${organization.id}/reports?days=${days}`);
  const t = reportText[locale];
  const money = new Intl.NumberFormat(locale, { style: "currency", currency: organization.currency });
  const number = new Intl.NumberFormat(locale);
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });
  return <Shell profile={profile} organization={organization} locale={locale} active="reports">
    <header className="page-header"><h1>{t.title}</h1><span className="status">{t.plan} / {t[data.plan]}</span></header>
    <dl className="summary">
      <div><dt>{t.overdue}</dt><dd>{number.format(data.overdue)}</dd></div>
      <div><dt>{t.members}</dt><dd>{number.format(data.members)}</dd></div>
      <div><dt>{t.workflows}</dt><dd>{number.format(data.workflows)}</dd></div>
    </dl>
    <div className="flex flex-wrap items-center gap-2 mb-4" role="group" aria-label={t.period}>
      {[7, 30, 90].map(period => <Link key={period} href={`?days=${period}`}
        className={period === data.days ? "button primary" : "button"}
        aria-current={period === data.days ? "page" : undefined}>{period} {t.days}</Link>)}
    </div>
    <div className="detail-main">
      <section className="panel data-panel" aria-labelledby="purchase-status">
        <h2 id="purchase-status">{t.purchaseStatus}</h2>
        {data.statuses.length ? <div className="table-scroll" tabIndex={0} role="region" aria-labelledby="purchase-status"><table>
          <thead><tr><th scope="col">{t.status}</th><th scope="col" className="amount">{t.count}</th>
            <th scope="col" className="amount">{t.amount}</th></tr></thead>
          <tbody>{data.statuses.map(row => <tr key={row.status}>
            <td><span className="status" data-status={row.status}>{label(locale, row.status)}</span></td>
            <td className="amount">{number.format(row.count)}</td><td className="amount">{money.format(row.amount)}</td>
          </tr>)}</tbody>
        </table></div> : <p className="empty-state muted">{t.noPurchases}</p>}
      </section>
      <section className="panel data-panel" aria-labelledby="delivery-retries">
        <h2 id="delivery-retries">{t.deliveries}</h2>
        {data.deliveries.length ? <div className="table-scroll" tabIndex={0} role="region" aria-labelledby="delivery-retries"><table>
          <thead><tr><th scope="col">{t.channel}</th><th scope="col">{t.queued}</th><th scope="col">{t.nextAttempt} (UTC)</th></tr></thead>
          <tbody>{data.deliveries.map(row => <tr key={row.channel}><td>{t[row.channel]}</td>
            <td>{number.format(row.count)}</td><td><time dateTime={row.nextAttemptAt}>{date.format(new Date(row.nextAttemptAt))}</time></td>
          </tr>)}</tbody>
        </table></div> : <p className="empty-state muted">{t.noRetries}</p>}
      </section>
      <section className="panel data-panel" aria-labelledby="report-audit">
        <h2 id="report-audit">{t.activity}</h2>
        {data.audit.length ? <div className="table-scroll" tabIndex={0} role="region" aria-labelledby="report-audit"><table>
          <thead><tr><th scope="col">{t.action}</th><th scope="col">{t.time} (UTC)</th><th scope="col">{t.actor}</th><th scope="col">{t.target}</th></tr></thead>
          <tbody>{data.audit.map(event => <tr key={event.id}>
            <td>{label(locale, reportLabel(locale, event.action))}</td>
            <td><time dateTime={event.at}>{date.format(new Date(event.at))}</time></td>
            <td>{event.actor === "system" ? t.system : event.actor}</td>
            <td>{event.target ?? "—"}</td>
          </tr>)}</tbody>
        </table></div> : <p className="empty-state muted">{t.noActivity}</p>}
      </section>
    </div>
  </Shell>;
}
