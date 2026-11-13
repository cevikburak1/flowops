import Link from "next/link";
import { notFound } from "next/navigation";
import { DecisionForm, RefreshPending } from "../../../../../components/forms";
import { Shell } from "../../../../../components/shell";
import { getApi, getLocale, messages, type Profile, type PurchaseDetail } from "../../../../../lib/server";
import { label } from "../../../../../lib/messages";
import { workflowText } from "../../../../../lib/workflows";
import { PurchaseExecution } from "../../../../../components/purchase-execution";

export default async function PurchasePage({ params }: { params: Promise<{ organizationId: string; purchaseId: string }> }) {
  const [{ organizationId, purchaseId }, profile, locale] = await Promise.all([params, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !/^[0-9a-f-]{36}$/i.test(purchaseId)) notFound();
  const api = `/api/organizations/${organization.id}/purchases/${purchaseId}`;
  const item = await getApi<PurchaseDetail>(api);
  const t = messages[locale];
  const formatDate = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" });
  return <Shell profile={profile} organization={organization} locale={locale}>
    <Link className="back-link" href={`/workspace/${organization.id}`}>{t.back}</Link>
    <header className="page-header"><h1>{item.title}</h1><span className="status" data-status={item.status}>{label(locale, item.status)}</span></header>
    {item.processing && <RefreshPending locale={locale} />}
    {item.execution && <div className="mb-5"><PurchaseExecution execution={item.execution} locale={locale}
      terminal={item.status === "approved" || item.status === "rejected"} /></div>}
    <div className="detail-grid">
      <div className="detail-main">
        <section className="panel detail-panel"><h2>{t.requestContext}</h2>
          <dl className="facts"><div><dt>{t.amount}</dt><dd>{new Intl.NumberFormat(locale, { style: "currency", currency: item.currency }).format(item.amount)}</dd></div>
            <div><dt>{t.category}</dt><dd>{label(locale, item.category)}</dd></div><div><dt>{t.date}</dt><dd>{formatDate.format(new Date(item.createdAt))}</dd></div></dl>
          <p className="description">{item.description}</p>
          {item.workflow && <p className="muted mt-4">{item.workflow.name} / {workflowText[locale].version} {item.workflow.number}</p>}
        </section>
        {item.stages.length > 0 && <section className="panel detail-panel"><h2>{t.decisions}</h2>
          <ol className="decisions">{item.stages.map(stage => <li key={stage.stage}><h3>{stage.stage === 1 ? t.manager : t.finance}</h3>
            {stage.canDecide ? <DecisionForm path={`${api}/approvals/${stage.stage}`} locale={locale} /> : <p>{stage.decision ? label(locale, stage.decision) : t.pending}</p>}
            {stage.comment && <p className="muted description">{stage.comment}</p>}
          </li>)}</ol>
        </section>}
      </div>
      <section className="panel detail-panel timeline"><h2>{t.activity}</h2>
        <ol>{item.timeline.map((event, index) => <li key={`${event.at}-${index}`}><strong>{label(locale, event.action)}</strong>
          <time dateTime={event.at}>{formatDate.format(new Date(event.at))}</time></li>)}</ol>
      </section>
    </div>
  </Shell>;
}
