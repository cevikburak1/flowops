import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../../components/shell";
import { Pagination } from "../../../../../components/pagination";
import { StudioFormEditor } from "../../../../../components/studio-form-editor";
import { CopilotTools } from "../../../../../components/copilot-tools";
import { canManageOrganization } from "../../../../../lib/organization";
import { getApi, getLocale, type Profile } from "../../../../../lib/server";
import { formText, type StudioFormDetail, type StudioForms } from "../../../../../lib/studio-forms";

export default async function FormsPage({ params, searchParams }: {
  params: Promise<{ organizationId: string; formPath?: string[] }>; searchParams: Promise<{ page?: string; copilot?: string }>;
}) {
  const [{ organizationId, formPath }, { page, copilot }, locale, profile] = await Promise.all([
    params, searchParams, getLocale(), getApi<Profile>("/api/me"),
  ]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  const id = formPath?.[0];
  if (!organization || (formPath?.length ?? 0) > 1 || id && id !== "new" && !/^[0-9a-f-]{36}$/i.test(id)) notFound();
  const current = typeof page === "string" && /^\d{1,5}$/.test(page) ? page : "1";
  const api = `/api/organizations/${organizationId}/forms`;
  const href = `/workspace/${organizationId}/forms`;
  const t = formText[locale];
  const form = id && id !== "new" ? await getApi<StudioFormDetail>(`${api}/${id}?page=${current}`) : undefined;
  const list = !form ? await getApi<StudioForms>(`${api}?page=${current}`) : undefined;
  return <Shell profile={profile} organization={organization} locale={locale} active="forms">
    {id && <Link className="back-link" href={href}>{t.forms}</Link>}
    <header className="page-header"><h1>{form?.draft.name ?? (id ? t.create : t.forms)}</h1>
      {!id && <Link className="button primary" href={`${href}/new`}>{t.create}</Link>}
    </header>
    {!id && canManageOrganization(organization.role) && <CopilotTools organizationId={organizationId} locale={locale} kind="forms" autoOpen={copilot === "1"} />}
    {id ? <StudioFormEditor key={id} form={form} api={form ? `${api}/${id}` : api} href={href} locale={locale}
      pagination={form && <Pagination page={form.page} more={form.hasMore} href={next => `?page=${next}`} locale={locale} label={t.history} />} /> :
      <section className="panel data-panel">
        {list?.items.length ? <ul className="simulation-trace">{list.items.map(item => <li key={item.id}>
          <Link className="panel-link" href={`${href}/${item.id}`}>{item.name}</Link>
          <span>{item.version ? `${t.version} ${item.version}` : t.draft}</span>
        </li>)}</ul> : <p className="empty-state">{t.empty}</p>}
        {list && <div className="px-4 pb-3"><Pagination page={list.page} more={list.hasMore} href={next => `?page=${next}`}
          locale={locale} label={t.forms} /></div>}
      </section>}
  </Shell>;
}
