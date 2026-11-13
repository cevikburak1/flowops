import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../../components/shell";
import { ApprovalMatrixEditor } from "../../../../../components/approval-matrix";
import { matrixText, type MatrixSettings } from "../../../../../lib/approval-matrix";
import { canManageOrganization, organizationText, type OrganizationSettings } from "../../../../../lib/organization";
import { getApi, getLocale, type Profile } from "../../../../../lib/server";

export default async function ApprovalMatrixPage({ params }: { params: Promise<{ organizationId: string }> }) {
  const [{ organizationId }, profile, locale] = await Promise.all([params, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role)) notFound();
  const base = `/api/organizations/${organizationId}`;
  const api = `${base}/approval-matrix`;
  const [matrix, settings] = await Promise.all([getApi<MatrixSettings>(api), getApi<OrganizationSettings>(`${base}/settings`)]);
  const t = matrixText[locale];
  return <Shell profile={profile} organization={organization} locale={locale} active="settings">
    <Link className="back-link" href={`/workspace/${organizationId}/settings`}>{organizationText[locale].settings}</Link>
    <header className="page-header"><div><h1>{t.title}</h1><p>{t.help}</p></div></header>
    <ApprovalMatrixEditor api={api} initial={matrix} departments={settings.groups.filter(group => group.kind === "department")}
      reviewers={settings.members.filter(member => ["OrganizationOwner", "OrganizationAdmin", "Manager"].includes(member.role))} locale={locale} />
  </Shell>;
}
