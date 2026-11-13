import { notFound, redirect } from "next/navigation";
import Link from "next/link";
import { PurchaseForm } from "../../../../../components/forms";
import { Shell } from "../../../../../components/shell";
import { getApi, getLocale, messages, type Profile, type Workspace, type PurchaseDetail } from "../../../../../lib/server";
import { workflowText, type PublishedWorkflow } from "../../../../../lib/workflows";

export default async function NewPurchase({ params, searchParams }: {
  params: Promise<{ organizationId: string }>; searchParams: Promise<{ workflow?: string; revisionOf?: string }>;
}) {
  const [{ organizationId }, query, profile, locale] = await Promise.all([params, searchParams, getApi<Profile>("/api/me"), getLocale()]);
  const organization = profile.organizations.find(item => item.id === organizationId);
  if (!organization) notFound();
  const workspace = await getApi<Workspace>(`/api/organizations/${organization.id}/workspace`);
  if (!workspace.canSubmit) redirect(`/workspace/${organization.id}`);
  if (query.revisionOf && !/^[0-9a-f-]{36}$/i.test(query.revisionOf)) notFound();
  const previous = query.revisionOf ? await getApi<PurchaseDetail>(`/api/organizations/${organization.id}/purchases/${query.revisionOf}`) : undefined;
  if (previous && !previous.canResubmit) notFound();
  const workflowId = previous ? previous.workflowId : query.workflow;
  if (workflowId && !/^[0-9a-f-]{36}$/i.test(workflowId)) notFound();
  const workflow = workflowId ? await getApi<PublishedWorkflow>(`/api/organizations/${organization.id}/workflows/${workflowId}/published`) : undefined;
  const t = messages[locale];
  return <Shell profile={profile} organization={organization} locale={locale} active="requests">
    <Link className="back-link" href={`/workspace/${organization.id}`}>{t.back}</Link>
    <header className="page-header"><h1>{previous ? t.revisePurchase : t.newRequest}</h1></header>
    <section className="panel form-panel">
      {workflow && <p>{workflow.name} / {workflowText[locale].version} {workflow.number}</p>}
      <PurchaseForm organizationId={organization.id} currency={organization.currency} locale={locale} workflow={workflow} previous={previous} />
    </section>
  </Shell>;
}
