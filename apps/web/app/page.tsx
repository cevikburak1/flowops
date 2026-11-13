import { ArrowRight, Check, FileText, GitMerge, ShieldCheck } from "lucide-react";
import Link from "next/link";
import { getLocale, messages } from "../lib/server";
import { Preferences } from "../components/preferences";

export default async function Home() {
  const locale = await getLocale();
  const t = messages[locale];
  const steps = [[FileText, t.stepSubmit, t.stepSubmitBody], [ShieldCheck, t.stepReview, t.stepReviewBody], [Check, t.stepRecord, t.stepRecordBody]] as const;
  return (
    <div className="landing">
      <header className="public-header">
        <Link className="brand" href="/"><GitMerge size={23} aria-hidden />FlowOps</Link>
        <div className="header-actions"><Preferences locale={locale} /><a className="button secondary" href="/auth/login">{t.signIn}</a></div>
      </header>
      <main className="landing-main">
        <section className="hero">
          <div>
            <h1>{t.heroTitle}</h1>
            <p>{t.heroBody}</p>
            <Link className="button primary" href="/workspace" prefetch={false}>{t.openWorkspace}<ArrowRight size={16} aria-hidden /></Link>
          </div>
          <ol className="process" aria-label={t.purchaseWorkflow}>
            {steps.map(([Icon, title, body]) =>
              <li key={title}><span className="step-icon"><Icon size={19} aria-hidden /></span><div><h2>{title}</h2><p>{body}</p></div></li>
            )}
          </ol>
        </section>
      </main>
    </div>
  );
}
