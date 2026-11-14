import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../components/shell";
import { Pagination } from "../../../../components/pagination";
import { WorkflowCopilot } from "../../../../components/workflow-copilot";
import { canManageOrganization } from "../../../../lib/organization";
import { getApi, getLocale, type Profile, type Workspace } from "../../../../lib/server";
import { workflowStatus, workflowStatuses, workflowText, type WorkflowList } from "../../../../lib/workflows";

export default async function Workflows({ params, searchParams }: {
  params: Promise<{ organizationId: string }>; searchParams: Promise<{ page?: string; q?: string; status?: string; copilot?: string }>;
}) {
  const [{ organizationId }, query, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization) notFound();
  const api = `/api/organizations/${organizationId}`;
  const base = `/workspace/${organizationId}`;
  const canManage = canManageOrganization(organization.role);
  const q = typeof query.q === "string" ? query.q.trim().slice(0, 120) : "";
  const status = workflowStatuses.find(value => value === query.status && (canManage || value === "published")) ?? "";
  const parsedPage = typeof query.page === "string" && /^\d{1,5}$/.test(query.page) ? query.page : "1";
  const queryFor = (page: number | string) => new URLSearchParams({ q, status, page: String(page) }).toString();
  const [data, workspace] = await Promise.all([
    getApi<WorkflowList>(`${api}/workflows?${queryFor(parsedPage)}`),
    getApi<Workspace>(`${api}/workspace`),
  ]);
  const t = workflowText[locale];
  return <Shell profile={profile} organization={organization} locale={locale} active="workflows">
    <header className="page-header"><h1>{t.title}</h1>
      {canManage && <Link className="button primary" href={`${base}/workflows/new`}>{t.create}</Link>}
    </header>
    {canManage && <WorkflowCopilot api={api} base={base} locale={locale} currency={organization.currency} autoOpen={query.copilot === "1"} />}
    <section className="panel detail-panel">
      <h2>{t.template}</h2>
      <p className="muted mt-2">{t.templateHint}</p>
      <form action={`${base}/workflows`} className="stack-form mt-4">
        <div className="form-row">
          <label>{t.search}<input type="search" name="q" maxLength={120} defaultValue={q} /></label>
          {canManage && <label>{t.filter}<select name="status" defaultValue={status}>
            <option value="">{t.all}</option>
            {workflowStatuses.map(value => <option key={value} value={value}>{t[value]}</option>)}
          </select></label>}
        </div>
        <div className="form-actions flex-wrap">
          <button className="button secondary" type="submit">{t.apply}</button>
          {(q || status) && <Link className="button secondary" href={`${base}/workflows`}>{t.clear}</Link>}
        </div>
      </form>
      {data.items.length ? <ul className="decisions">
        {data.items.map(item => <li key={item.id} className="flex flex-wrap items-center justify-between gap-3">
          <div><h3>{canManage ? <Link className="underline break-all" href={`${base}/workflows/${item.id}`}>{item.name}</Link> : item.name}</h3>
            <p className="muted">{workflowStatus(item) === "published" ? `${t.version} ${item.publishedVersion}` : t[workflowStatus(item)]}</p>
            {item.hasDraft && workflowStatus(item) === "published" && <p className="muted">{t.unpublishedChanges}</p>}
          </div>
          {workflowStatus(item) === "published" && workspace.canSubmit &&
            <Link className="button secondary" href={`${base}/purchases/new?workflow=${item.id}`}>{t.use}</Link>}
        </li>)}
      </ul> : <p className="empty-state mt-4">{q || status ? t.noMatches : t.noWorkflows}</p>}
    </section>
    <Pagination page={data.page} more={data.hasMore} href={next => `?${queryFor(next)}`} locale={locale} label={t.title} />
  </Shell>;
}
