import Link from "next/link";
import { notFound } from "next/navigation";
import { Shell } from "../../../../../components/shell";
import { WorkflowDraftForm } from "../../../../../components/workflow-draft";
import { canManageOrganization } from "../../../../../lib/organization";
import { getApi, getLocale, type Profile } from "../../../../../lib/server";
import { workflowText } from "../../../../../lib/workflows";

export default async function NewWorkflow({ params }: { params: Promise<{ organizationId: string }> }) {
  const [{ organizationId }, profile, locale] = await Promise.all([params, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization || !canManageOrganization(organization.role)) notFound();
  const base = `/workspace/${organizationId}/workflows`;
  const t = workflowText[locale];
  return <Shell profile={profile} organization={organization} locale={locale} active="workflows">
    <Link className="back-link" href={base}>{t.title}</Link>
    <header className="page-header"><h1>{t.create}</h1></header>
    <section className="panel form-panel workflow-editor-panel">
      <p className="muted">{t.templateHint}</p>
      <WorkflowDraftForm api={`/api/organizations/${organizationId}/workflows`} href={base} currency={organization.currency} locale={locale} />
    </section>
  </Shell>;
}
