import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../../components/shell";
import { WorkflowDraftForm } from "../../../../../components/workflow-draft";
import { canManageOrganization } from "../../../../../lib/organization";
import { label } from "../../../../../lib/messages";
import { getApi, getLocale, messages, type Profile } from "../../../../../lib/server";
import { workflowStatus, workflowText, type WorkflowDetail } from "../../../../../lib/workflows";

export default async function Workflow({ params, searchParams }: {
  params: Promise<{ organizationId: string; workflowId: string }>; searchParams: Promise<{ page?: string }>;
}) {
  const [{ organizationId, workflowId }, { page }, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role) || !/^[0-9a-f-]{36}$/i.test(workflowId)) notFound();
  const base = `/workspace/${organizationId}`;
  const api = `/api/organizations/${organizationId}/workflows/${workflowId}`;
  const workflow = await getApi<WorkflowDetail>(`${api}?page=${encodeURIComponent(page ?? "1")}`);
  const t = workflowText[locale];
  const common = messages[locale];
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeZone: "UTC" });
  const money = new Intl.NumberFormat(locale, { style: "currency", currency: organization.currency });
  const status = workflowStatus(workflow);
  return <Shell profile={profile} organization={organization} locale={locale} active="workflows">
    <Link className="back-link" href={`${base}/workflows`}>{t.title}</Link>
    <header className="page-header"><h1>{workflow.name}</h1>
      <span className="status">{status === "published" && workflow.hasDraft ? t.unpublishedChanges : t[status]}</span>
    </header>
    <div className="detail-main">
      <section className="panel detail-panel settings-panel">
        <h2>{workflow.archived ? t.duplicate : t.settings}</h2>
        <WorkflowDraftForm key={workflow.id} api={api} href={`${base}/workflows`} currency={organization.currency} locale={locale} workflow={workflow} />
      </section>
      <div className="detail-grid">
        <section className="panel detail-panel">
          <h2>{t.runs}</h2>
          {workflow.runs.length ? <ul className="decisions">{workflow.runs.map(run =>
            <li key={run.id}>
              <Link className="underline break-all" href={`${base}/purchases/${run.id}`}>{run.title}</Link>
              <p className="muted">{t.version} {run.workflowVersion} / {label(locale, run.status)}</p>
              <time dateTime={run.createdAt}>{date.format(new Date(run.createdAt))}</time>
            </li>
          )}</ul> : <p className="muted mt-3">{t.noRuns}</p>}
        </section>
      <section className="panel detail-panel">
        <h2>{t.versions}</h2>
        {workflow.versions.length ? <ol className="decisions">{workflow.versions.map(version =>
          <li key={version.number}>
            <h3>{t.version} {version.number} / {version.name}</h3>
            <p className="muted">{t.threshold} {money.format(version.financeThreshold)}</p>
            <time dateTime={version.publishedAt}>{date.format(new Date(version.publishedAt))}</time>
          </li>
        )}</ol> : <p className="muted mt-3">{t.noVersions}</p>}
      </section>
      </div>
    </div>
    <div className="pager">
      {workflow.page > 1 && <Link className="button secondary" href={`?page=${workflow.page - 1}`}>{common.previous}</Link>}
      {workflow.hasMore && <Link className="button secondary" href={`?page=${workflow.page + 1}`}>{common.next}</Link>}
    </div>
  </Shell>;
}
