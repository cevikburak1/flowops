import { notFound } from "next/navigation";
import Link from "next/link";
import { ApprovalSlaEditor, type SlaSettings } from "../../../../components/approval-sla";
import { matrixText } from "../../../../lib/approval-matrix";
import { Settings } from "../../../../components/organization-settings";
import { Shell } from "../../../../components/shell";
import { canManageOrganization, organizationText, type OrganizationSettings } from "../../../../lib/organization";
import { getApi, getLocale, type Profile } from "../../../../lib/server";

export default async function OrganizationSettingsPage({ params, searchParams }: {
  params: Promise<{ organizationId: string }>; searchParams: Promise<{ members?: string; invitations?: string; groups?: string }>;
}) {
  const [{ organizationId }, query, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role)) notFound();
  const base = `/api/organizations/${organization.id}`;
  const [settings, sla] = await Promise.all([
    getApi<OrganizationSettings>(`${base}/settings`), getApi<SlaSettings>(`${base}/approval-sla`),
  ]);
  const page = (value?: string) => typeof value === "string" && /^\d{1,5}$/.test(value) ? Math.max(1, Number(value)) : 1;
  return <Shell profile={profile} organization={organization} locale={locale} active="settings">
    <header className="page-header"><h1>{organizationText[locale].settings}</h1>
      <Link className="button secondary" href={`/workspace/${organization.id}/settings/approval-matrix`}>{matrixText[locale].title}</Link>
    </header>
    <div className="detail-main">
      <Settings data={settings} base={base} currency={organization.currency} locale={locale}
        pages={{ members: page(query.members), invitations: page(query.invitations), groups: page(query.groups) }} />
      <ApprovalSlaEditor api={`${base}/approval-sla`} settings={sla} locale={locale} members={settings.members} />
    </div>
  </Shell>;
}
