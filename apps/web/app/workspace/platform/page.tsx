import Link from "next/link";
import { notFound } from "next/navigation";
import { ChangePlan } from "../../../components/organization-settings";
import { Shell } from "../../../components/shell";
import { reportText, type PlatformOrganizations } from "../../../lib/reports";
import { getApi, getLocale, messages, type Profile } from "../../../lib/server";

export default async function PlatformPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  const [{ page }, profile, locale] = await Promise.all([searchParams, getApi<Profile>("/api/me"), getLocale()]);
  if (!profile.isOperator) notFound();
  const data = await getApi<PlatformOrganizations>(`/api/platform/organizations?page=${/^\d{1,5}$/.test(page ?? "") ? page : "1"}`);
  const t = reportText[locale];
  const labels = messages[locale];
  const number = new Intl.NumberFormat(locale);
  return <Shell profile={profile} locale={locale} active="platform">
    <header className="page-header"><h1>{t.platform}</h1></header>
    <section className="panel data-panel" aria-labelledby="platform-organizations">
      <h2 id="platform-organizations">{t.organizations}</h2>
      {data.items.length ? <div className="table-scroll" tabIndex={0} role="region" aria-labelledby="platform-organizations"><table>
        <thead><tr><th scope="col">{t.organization}</th><th scope="col">{t.members}</th>
          <th scope="col">{t.workflows}</th><th scope="col">{t.plan}</th></tr></thead>
        <tbody>{data.items.map(org => <tr key={org.id}>
          <td><strong>{org.name}</strong><span className="row-meta muted">{org.slug}</span></td>
          <td>{number.format(org.members)}</td><td>{number.format(org.workflows)}</td>
          <td><ChangePlan id={org.id} name={org.name} plan={org.plan} locale={locale} /></td>
        </tr>)}</tbody>
      </table></div> : <p className="empty-state muted">{t.noOrganizations}</p>}
    </section>
    <div className="flex gap-2 pager">
      {data.page > 1 && <Link className="button" href={`?page=${data.page - 1}`}>{labels.previous}</Link>}
      {data.hasMore && <Link className="button" href={`?page=${data.page + 1}`}>{labels.next}</Link>}
    </div>
  </Shell>;
}
