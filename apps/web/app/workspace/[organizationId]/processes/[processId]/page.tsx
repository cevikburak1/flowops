import Link from "next/link";
import { notFound } from "next/navigation";
import { RefreshPending } from "../../../../../components/forms";
import { CompleteProcessTask, ProcessEditor, ProcessRunForm } from "../../../../../components/process-editor";
import { Shell } from "../../../../../components/shell";
import { Pagination } from "../../../../../components/pagination";
import { CopilotTools } from "../../../../../components/copilot-tools";
import { canManageOrganization } from "../../../../../lib/organization";
import { processText, type ProcessDetail } from "../../../../../lib/processes";
import { getApi, getLocale, messages, type Profile } from "../../../../../lib/server";

export default async function ProcessPage({ params, searchParams }: {
  params: Promise<{ organizationId: string; processId: string }>; searchParams: Promise<{ runId?: string; page?: string }>;
}) {
  const [{ organizationId, processId }, { runId, page }, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !/^[0-9a-f-]{36}$/i.test(processId)) notFound();
  const href = `/workspace/${organizationId}/processes`;
  const api = `/api/organizations/${organizationId}/processes/${processId}`;
  const current = typeof page === "string" && /^\d{1,5}$/.test(page) ? page : "1";
  const query = new URLSearchParams({ page: current });
  if (typeof runId === "string" && /^[0-9a-f-]{36}$/i.test(runId)) query.set("runId", runId);
  const data = await getApi<ProcessDetail>(`${api}?${query}`);
  const t = processText[locale];
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });
  return <Shell profile={profile} organization={organization} locale={locale} active="processes">
    <Link className="back-link" href={href}>{t.title}</Link>
    <header className="page-header">
      <h1>{data.draft?.name ?? data.published?.name}</h1>
      <span className="status">{data.version ? `${t.version} ${data.version}` : t.draft}</span>
    </header>
    <div className="detail-grid">
      <div className="detail-main">
        {data.canEdit && <ProcessEditor api={api} href={href} locale={locale} process={data} targets={data.targets} members={data.members} />}
        {data.canRun && data.published && <section className="panel detail-panel settings-panel">
          <h2>{data.published.trigger === "manual" ? t.run : t.endpoint}</h2>
          {data.published.trigger === "manual" ? <ProcessRunForm key={data.version} api={api} locale={locale} process={data} /> :
            <>
              <code className="break-all">POST {api}/webhook</code>
              <details>
                <summary>{t.webhookBody}</summary>
                <pre className="overflow-auto text-sm"><code>{JSON.stringify({
                  id: "00000000-0000-4000-8000-000000000001",
                  version: data.version,
                  answers: Object.fromEntries((data.form?.fields ?? data.published.fields).filter(field =>
                    !("whenKey" in field && field.whenKey)).map(field => [field.key,
                      field.type === "date" ? "2026-01-01" : field.type === "number" || field.type === "decimal" ? "1" :
                        field.type === "email" ? "example@example.test" : field.type === "checkbox" ? "false" :
                          field.type === "select" && "options" in field ? field.options?.[0] : field.label])),
                  ...(data.published.attachment === "required" ? { attachment: { name: "reference.txt", content: "cmVmZXJlbmNl" } } : {})
                }, null, 2)}</code></pre>
              </details>
            </>}
        </section>}
      </div>
      <section className="panel detail-panel timeline">
        <h2 id="process-runs">{t.runs}</h2>
        {data.runs.some(run => run.status === "running") && <RefreshPending locale={locale} />}
        {data.runs.length === 0 ? <p className="muted">{t.noRuns}</p> :
          <ol>{data.runs.map(run => <li key={run.id}>
            <strong>{t[run.status]}</strong>
            {run.status === "failed" && canManageOrganization(organization.role) &&
              <CopilotTools organizationId={organizationId} locale={locale} kind="failure" resourceId={run.id} />}
            <time dateTime={run.createdAt}>{date.format(new Date(run.createdAt))} UTC</time>
            {run.dueAt && <time dateTime={run.dueAt}>{t.deadline} {date.format(new Date(run.dueAt))} UTC</time>}
            {run.status === "running" && run.escalatedAt && <span className="status" data-status="rejected">{messages[locale].processOverdue}</span>}
            <span className="muted">{t.version} {run.version}</span>
            {run.attachment && <a className="back-link break-all" href={`${api}/runs/${run.id}/attachment`}>{run.attachment}</a>}
            {data.tasks.filter(task => task.runId === run.id).map(task => <div key={task.stepId} id={`task-${task.runId}-${task.stepId}`}>
              <p className="description">{task.title}</p>
              <p className="muted break-all">{task.assignee}</p>
              {task.dueAt && <time dateTime={task.dueAt}>{t.deadline} {date.format(new Date(task.dueAt))} UTC</time>}
              {!task.completedAt && task.escalatedAt && <span className="status" data-status="rejected">{messages[locale].processTaskOverdue}</span>}
              {task.completedAt ? <span className="muted">{t.taskCompleted}</span> :
                task.canComplete && run.status === "running" && <CompleteProcessTask locale={locale}
                  path={`${api}/runs/${run.id}/tasks/${task.stepId}/complete`} />}
            </div>)}
            <details><summary>{t.answers}</summary>
              <dl className="facts">{(JSON.parse(run.answers) as { label: string; value: string }[]).map((answer, index) =>
                <div key={index}><dt>{answer.label}</dt><dd className="description">{answer.value || "—"}</dd></div>)}</dl>
            </details>
          </li>)}</ol>}
        <Pagination page={data.page} more={data.hasMore} href={next => {
          const values = new URLSearchParams(query);
          values.set("page", String(next));
          return `?${values}#process-runs`;
        }} locale={locale} label={t.runs} />
      </section>
    </div>
  </Shell>;
}
