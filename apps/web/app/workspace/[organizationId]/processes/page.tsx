import Link from "next/link";
import { notFound } from "next/navigation";
import { ProcessEditor } from "../../../../components/process-editor";
import { Shell } from "../../../../components/shell";
import { Pagination } from "../../../../components/pagination";
import { processText, type ProcessList } from "../../../../lib/processes";
import { getApi, getLocale, type Profile } from "../../../../lib/server";

export default async function ProcessesPage({ params, searchParams }: {
  params: Promise<{ organizationId: string }>;
  searchParams: Promise<{ view?: string; page?: string }>;
}) {
  const [{ organizationId }, { view, page }, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization) notFound();
  const href = `/workspace/${organizationId}/processes`;
  const api = `/api/organizations/${organizationId}/processes`;
  const current = typeof page === "string" && /^\d{1,5}$/.test(page) ? page : "1";
  const data = await getApi<ProcessList>(`${api}?page=${current}`);
  const t = processText[locale];
  const creating = view === "new";
  if (creating && !data.canEdit) notFound();
  return <Shell profile={profile} organization={organization} locale={locale} active="processes">
    <header className="page-header">
      <h1>{creating ? t.new : t.title}</h1>
      {creating ? <Link className="button secondary" href={href}>{t.title}</Link> :
        data.canEdit && <Link className="button primary" href="?view=new">{t.new}</Link>}
    </header>
    {creating ? <ProcessEditor api={api} href={href} locale={locale} targets={data.targets} members={data.members} /> :
      <section className="panel data-panel" aria-label={t.title}>
        {data.items.length === 0 ? <p className="empty-state">{t.noProcesses}</p> :
          <div className="table-scroll" tabIndex={0} role="region" aria-label={t.title}>
            <table><thead><tr><th scope="col">{t.name}</th><th scope="col">{t.version}</th></tr></thead>
              <tbody>{data.items.map(item => <tr key={item.id}>
                <td><Link href={`${href}/${item.id}`}>{item.name}</Link></td>
                <td>{item.version > 0 ? item.version : t.draft}</td>
              </tr>)}</tbody>
            </table>
          </div>}
      </section>}
    {!creating && <Pagination page={data.page} more={data.hasMore} href={next => `?page=${next}`} locale={locale} label={t.title} />}
  </Shell>;
}
