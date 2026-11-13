import { OrganizationForm } from "../../../../components/forms";
import { Shell } from "../../../../components/shell";
import { getApi, getLocale, messages, type Profile } from "../../../../lib/server";

export default async function NewOrganization() {
  const [profile, locale] = await Promise.all([getApi<Profile>("/api/me"), getLocale()]);
  const t = messages[locale];
  return <Shell profile={profile} locale={locale}>
    <header className="page-header"><h1>{t.newOrganization}</h1></header>
    <section className="panel form-panel">
      {profile.organizations.length === 0 && <p className="muted">{t.noOrganizations}</p>}
      <OrganizationForm locale={locale} />
    </section>
  </Shell>;
}
