import { notFound } from "next/navigation";
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
  const settings = await getApi<OrganizationSettings>(`${base}/settings`);
  const page = (value?: string) => typeof value === "string" && /^\d{1,5}$/.test(value) ? Math.max(1, Number(value)) : 1;
  return <Shell profile={profile} organization={organization} locale={locale} active="settings">
    <header className="page-header"><h1>{organizationText[locale].settings}</h1></header>
    <Settings data={settings} base={base} currency={organization.currency} locale={locale}
      pages={{ members: page(query.members), invitations: page(query.invitations), groups: page(query.groups) }} />
  </Shell>;
}
