import { notFound } from "next/navigation";
import { Settings } from "../../../../components/organization-settings";
import { Shell } from "../../../../components/shell";
import { canManageOrganization, organizationText, type OrganizationSettings } from "../../../../lib/organization";
import { getApi, getLocale, type Profile } from "../../../../lib/server";

export default async function OrganizationSettingsPage({ params }: { params: Promise<{ organizationId: string }> }) {
  const [{ organizationId }, profile, locale] = await Promise.all([params, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role)) notFound();
  const base = `/api/organizations/${organization.id}`;
  const settings = await getApi<OrganizationSettings>(`${base}/settings`);
  return <Shell profile={profile} organization={organization} locale={locale} active="settings">
    <header className="page-header"><h1>{organizationText[locale].settings}</h1></header>
    <Settings data={settings} base={base} currency={organization.currency} locale={locale} />
  </Shell>;
}
