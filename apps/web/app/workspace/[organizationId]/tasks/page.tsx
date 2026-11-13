import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../components/shell";
import { Pagination } from "../../../../components/pagination";
import { label } from "../../../../lib/messages";
import { getApi, getLocale, messages, type Profile } from "../../../../lib/server";
import { priorities, taskStatuses, taskText, type TaskPage } from "../../../../lib/tasks";
import { processText } from "../../../../lib/processes";

export default async function Tasks({ params, searchParams }: {
  params: Promise<{ organizationId: string }>;
  searchParams: Promise<{ status?: string; priority?: string; page?: string; processPage?: string }>;
}) {
  const [{ organizationId }, query, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization) notFound();
  const status = taskStatuses.find(item => item === query.status) ?? "pending";
  const priority = priorities.find(item => item === query.priority);
  const page = typeof query.page === "string" && /^\d{1,5}$/.test(query.page) ? Number(query.page) : 1;
  const processPage = typeof query.processPage === "string" && /^\d{1,5}$/.test(query.processPage) ? query.processPage : "1";
  const base = `/workspace/${organization.id}`;
  function filters(pageNumber: number) {
    const values = new URLSearchParams({ status, page: String(pageNumber) });
    if (priority) values.set("priority", priority);
    return values.toString();
  }
  const [data, processTasks] = await Promise.all([
    getApi<TaskPage>(`/api/organizations/${organization.id}/tasks?${filters(page)}`),
    getApi<{ items: { processId: string; runId: string; stepId: string; title: string }[]; page: number; hasMore: boolean }>(
      `/api/organizations/${organization.id}/process-tasks?page=${processPage}`)
  ]);
  const t = taskText[locale];
  const common = messages[locale];
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });

  return <Shell profile={profile} organization={organization} locale={locale} active="tasks">
    <header className="page-header"><h1>{t.title}</h1></header>
    {(processTasks.items?.length > 0 || processTasks.page > 1) && <section className="panel detail-panel mb-4">
      <h2 id="process-tasks">{processText[locale].myTasks}</h2>
      <ul className="decisions">{processTasks.items.map(task => <li key={`${task.runId}:${task.stepId}`}>
        <Link className="underline break-words" href={`${base}/processes/${task.processId}?runId=${task.runId}#task-${task.runId}-${task.stepId}`}>{task.title}</Link>
      </li>)}</ul>
      <Pagination page={processTasks.page} more={processTasks.hasMore}
        href={next => `?${filters(data.page)}&processPage=${next}#process-tasks`} locale={locale} label={processText[locale].myTasks} />
    </section>}
    <section className="panel detail-panel">
      <form className="stack-form" action={`${base}/tasks`}>
        <div className="form-row">
          <label>{common.status}<select name="status" defaultValue={status}>
            {taskStatuses.map(item => <option key={item} value={item}>{t[item]}</option>)}
          </select></label>
          <label>{t.priority}<select name="priority" defaultValue={priority ?? ""}>
            <option value="">{t.allPriorities}</option>
            {priorities.map(item => <option key={item} value={item}>{t[item]}</option>)}
          </select></label>
        </div>
        <button type="submit" className="button secondary">{t.apply}</button>
      </form>
      {data.items.length === 0 ? <p className="empty-state mt-4">{t.empty}</p> :
        <div className="table-scroll mt-4" role="region" aria-label={t.title} tabIndex={0}>
          <table>
            <thead><tr><th scope="col">{common.purchase}</th><th scope="col">{common.status}</th>
              <th scope="col">{t.priority}</th><th scope="col">{t.dueAt} (UTC)</th></tr></thead>
            <tbody>{data.items.map(task => <tr key={`${task.id}:${task.stage}`}>
              <td><Link href={`${base}/purchases/${task.id}`}>{task.title}</Link>
                <span className="row-meta">{task.stage === 1 ? common.manager : common.finance}</span></td>
              <td><span className="status" data-status={task.decision ?? (task.overdue ? "rejected" : "managerReview")}>
                {task.decision ? label(locale, task.decision) : task.overdue ? t.overdue : t.pending}
              </span></td>
              <td>{t[task.priority]}</td>
              <td><time dateTime={task.dueAt}>{date.format(new Date(task.dueAt))}</time></td>
            </tr>)}</tbody>
          </table>
        </div>}
    </section>
    <Pagination page={data.page} more={data.hasMore} href={next => `?${filters(next)}&processPage=${processPage}`} locale={locale} label={t.title} />
  </Shell>;
}
