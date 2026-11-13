import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../../components/shell";
import { Pagination } from "../../../../../components/pagination";
import { WorkflowDraftForm } from "../../../../../components/workflow-draft";
import { canManageOrganization } from "../../../../../lib/organization";
import { label } from "../../../../../lib/messages";
import { getApi, getLocale, type Profile } from "../../../../../lib/server";
import { workflowStatus, workflowText, type WorkflowDetail } from "../../../../../lib/workflows";
import { simulationText } from "../../../../../lib/simulation";

export default async function Workflow({ params, searchParams }: {
  params: Promise<{ organizationId: string; workflowId: string }>; searchParams: Promise<{ page?: string; versionPage?: string }>;
}) {
  const [{ organizationId, workflowId }, { page, versionPage }, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role) || !/^[0-9a-f-]{36}$/i.test(workflowId)) notFound();
  const base = `/workspace/${organizationId}`;
  const api = `/api/organizations/${organizationId}/workflows/${workflowId}`;
  const validPage = (value: string | undefined) => typeof value === "string" && /^\d{1,5}$/.test(value) ? value : "1";
  const workflow = await getApi<WorkflowDetail>(`${api}?page=${validPage(page)}&versionPage=${validPage(versionPage)}`);
  const t = workflowText[locale];
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeZone: "UTC" });
  const money = new Intl.NumberFormat(locale, { style: "currency", currency: organization.currency });
  const status = workflowStatus(workflow);
  return <Shell profile={profile} organization={organization} locale={locale} active="workflows">
    <Link className="back-link" href={`${base}/workflows`}>{t.title}</Link>
    <header className="page-header"><h1>{workflow.name}</h1>
      <div className="form-actions flex-wrap">
        <span className="status">{status === "published" && workflow.hasDraft ? t.unpublishedChanges : t[status]}</span>
        <Link className="button secondary" href={`${base}/workflows/${workflowId}/simulation`}>{simulationText[locale].title}</Link>
      </div>
    </header>
    <div className="detail-main">
      <section className="panel detail-panel settings-panel">
        <h2>{workflow.archived ? t.duplicate : t.settings}</h2>
        <WorkflowDraftForm key={workflow.id} api={api} href={`${base}/workflows`} currency={organization.currency} locale={locale} workflow={workflow} />
      </section>
      <div className="detail-grid">
        <section className="panel detail-panel">
          <h2 id="workflow-runs">{t.runs}</h2>
          {workflow.runs.length ? <ul className="decisions">{workflow.runs.map(run =>
            <li key={run.id}>
              <Link className="underline break-all" href={`${base}/purchases/${run.id}`}>{run.title}</Link>
              <p className="muted">{t.version} {run.workflowVersion} / {label(locale, run.status)}</p>
              <time dateTime={run.createdAt}>{date.format(new Date(run.createdAt))}</time>
            </li>
          )}</ul> : <p className="muted mt-3">{t.noRuns}</p>}
          <Pagination page={workflow.page} more={workflow.runsHaveMore}
            href={next => `?page=${next}&versionPage=${workflow.versionPage}#workflow-runs`} locale={locale} label={t.runs} />
        </section>
      <section className="panel detail-panel">
        <h2 id="workflow-versions">{t.versions}</h2>
        {workflow.versions.length ? <ol className="decisions">{workflow.versions.map(version =>
          <li key={version.number}>
            <h3>{t.version} {version.number} / {version.name}</h3>
            <p className="muted">{t.threshold} {money.format(version.financeThreshold)}</p>
            <time dateTime={version.publishedAt}>{date.format(new Date(version.publishedAt))}</time>
          </li>
        )}</ol> : <p className="muted mt-3">{t.noVersions}</p>}
        <Pagination page={workflow.versionPage} more={workflow.versionsHaveMore}
          href={next => `?page=${workflow.page}&versionPage=${next}#workflow-versions`} locale={locale} label={t.versions} />
      </section>
      </div>
    </div>
  </Shell>;
}
