import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../../../components/shell";
import { WorkflowSimulator } from "../../../../../../components/workflow-simulator";
import { Pagination } from "../../../../../../components/pagination";
import { canManageOrganization } from "../../../../../../lib/organization";
import { getApi, getLocale, type Profile } from "../../../../../../lib/server";
import { simulationText, type SimulationCatalog } from "../../../../../../lib/simulation";

export default async function SimulationPage({ params, searchParams }: {
  params: Promise<{ organizationId: string; workflowId: string }>;
  searchParams: Promise<{ page?: string; versions?: string }>;
}) {
  const [{ organizationId, workflowId }, query, profile, locale] = await Promise.all([
    params, searchParams, getApi<Profile>("/api/me"), getLocale(),
  ]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role) || !/^[0-9a-f-]{36}$/i.test(workflowId)) notFound();
  const api = `/api/organizations/${organizationId}/workflows/${workflowId}/simulation`;
  const validPage = (value?: string) => value && /^\d{1,5}$/.test(value) ? value : "1";
  const data = await getApi<SimulationCatalog>(`${api}?page=${validPage(query.page)}&versionPage=${validPage(query.versions)}`);
  const t = simulationText[locale];
  return <Shell profile={profile} organization={organization} locale={locale} active="workflows">
    <Link className="back-link" href={`/workspace/${organizationId}/workflows/${workflowId}`}>{t.back} / {data.name}</Link>
    <header className="page-header"><div><h1>{t.title}</h1><p>{t.hint}</p></div></header>
    <WorkflowSimulator api={api} data={data} currency={organization.currency} locale={locale}
      versionPaging={<Pagination page={data.versionPage} more={data.moreVersions} locale={locale} label={t.versionPage}
        href={next => `?page=${data.page}&versions=${next}`} />}
      historyPaging={<Pagination page={data.page} more={data.hasMore} locale={locale} label={t.history}
        href={next => `?page=${next}&versions=${data.versionPage}`} />} />
  </Shell>;
}
