"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { Locale } from "../lib/messages";
import type { PurchaseGraph } from "../lib/purchase-graph";
import { PurchaseExecution } from "./purchase-execution";

const copy = {
  en: {
    heading: "Draft with Copilot", prompt: "Describe the purchase approval",
    hint: "Describe who reviews the request and when finance approval is needed. Your prompt is sent to the configured AI provider. Leave out personal or confidential information.",
    generate: "Generate proposal", busy: "Generating proposal…", save: "Save as workflow draft", discard: "Discard proposal",
    disabled: "Copilot is unavailable. An operator must enable it and configure a provider and model.",
    error: "The proposal could not be completed. Try again.", limit: "The usage limit was reached. Try again later.",
    unsupported: "These requirements need manual configuration:", invalid: "The provider returned an invalid proposal. Revise the prompt and try again.",
    saved: "Saving draft…", quota: "The organization has reached its workflow limit.",
    usage: "Requests this month", preview: "Review the proposed workflow", threshold: "Finance threshold",
    simulation: "Simulated steps for a sample approval", savingError: "The workflow draft could not be saved.",
  },
  tr: {
    heading: "Copilot ile taslak oluştur", prompt: "Satın alma onayını tarif et",
    hint: "Talebi kimin inceleyeceğini ve ne zaman finans onayı gerektiğini yaz. İstem yapılandırılan yapay zekâ sağlayıcısına gönderilir. Kişisel veya gizli bilgi ekleme.",
    generate: "Öneri oluştur", busy: "Öneri oluşturuluyor…", save: "İş akışı taslağı olarak kaydet", discard: "Öneriyi kaldır",
    disabled: "Copilot kullanılamıyor. Sistem yöneticisinin özelliği açıp sağlayıcıyı ve modeli yapılandırması gerekiyor.",
    error: "Öneri oluşturulamadı. Tekrar dene.", limit: "Kullanım sınırına ulaşıldı. Daha sonra tekrar dene.",
    unsupported: "Bu gereksinimler elle yapılandırılmalı:", invalid: "Sağlayıcı geçersiz bir öneri döndürdü. İstemi düzenleyip tekrar dene.",
    saved: "Taslak kaydediliyor…", quota: "Kuruluş iş akışı sınırına ulaştı.",
    usage: "Bu ayki istekler", preview: "İş akışı önerisini incele", threshold: "Finans eşiği",
    simulation: "Örnek onayın simülasyon adımları", savingError: "İş akışı taslağı kaydedilemedi.",
  },
};

interface Proposal {
  draft: { name: string; financeThreshold: number; revision: number; graph: PurchaseGraph };
  simulation: { steps: { nodeId: string }[] };
}

export function WorkflowCopilot({ api, base, locale, currency, autoOpen }: {
  api: string; base: string; locale: Locale; currency: string; autoOpen?: boolean;
}) {
  const router = useRouter();
  const t = copy[locale];
  const [status, setStatus] = useState<{ enabled: boolean; requests: number; monthlyLimit: number }>();
  const [prompt, setPrompt] = useState("");
  const [proposal, setProposal] = useState<Proposal>();
  const [limitations, setLimitations] = useState<string[]>([]);
  const [error, setError] = useState("");
  const [pending, setPending] = useState<"generate" | "save">();
  const endpoint = api + "/copilot/v1";
  async function loadStatus() {
    try {
      const response = await fetch(endpoint + "/status", { cache: "no-store" });
      if (!response.ok) throw new Error();
      setStatus(await response.json());
    } catch { setError(t.error); }
  }
  async function generate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending("generate");
    setProposal(undefined);
    setError("");
    setLimitations([]);
    try {
      const response = await fetch(endpoint + "/purchase-proposals", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ prompt }),
      });
      const data = await response.json();
      if (response.ok) setProposal(data);
      else if (data.code === "unsupportedRequirements") {
        setError(t.unsupported);
        setLimitations(data.limitations);
      } else setError(response.status === 429 ? t.limit : response.status === 503 ? t.disabled :
        response.status === 422 ? t.invalid : t.error);
      await loadStatus();
    } catch { setError(t.error); }
    finally { setPending(undefined); }
  }
  async function save() {
    if (!proposal) return;
    setError("");
    setPending("save");
    try {
      const response = await fetch(api + "/workflows", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(proposal.draft),
      });
      const data = await response.json();
      if (response.ok) router.push(base + "/workflows/" + data.id);
      else setError(data.code === "workflowPlanLimit" ? t.quota : t.savingError);
    } catch { setError(t.savingError); }
    finally { setPending(undefined); }
  }
  return <details id="workflow-copilot" open={autoOpen} className="panel detail-panel mb-4"
    onToggle={event => { if (event.currentTarget.open && !status) void loadStatus(); }}>
    <summary>{t.heading}</summary>
    {status && <p className="muted mt-2">{t.usage} {status.requests} / {status.monthlyLimit}</p>}
    {status?.enabled === false && <p role="status" className="mt-2">{t.disabled}</p>}
    {status?.enabled && <form className="stack-form mt-4" onSubmit={generate}>
      <label>{t.prompt}<textarea rows={4} required minLength={10} maxLength={2000} value={prompt}
        aria-describedby="copilot-prompt-hint" onChange={event => setPrompt(event.target.value)} /></label>
      <p id="copilot-prompt-hint" className="muted">{t.hint}</p>
      <button type="submit" className="button primary" disabled={!!pending}>{pending === "generate" ? t.busy : t.generate}</button>
    </form>}
    {error && <p className="mt-2" role="alert">{error}</p>}
    {limitations.length > 0 && <ul className="decisions">{limitations.map((item, index) => <li key={index}>{item}</li>)}</ul>}
    {proposal && <div className="stack-form mt-4">
      <h2>{t.preview}</h2>
      <strong className="break-words">{proposal.draft.name}</strong>
      {proposal.draft.graph.nodes.some(node => node.kind === "finance") && <p>{t.threshold}{" "}
        {new Intl.NumberFormat(locale, { style: "currency", currency }).format(proposal.draft.financeThreshold)}</p>}
      <p className="muted">{t.simulation}</p>
      <PurchaseExecution locale={locale} terminal execution={{ graph: proposal.draft.graph,
        nodes: proposal.simulation.steps.map(step => ({ id: step.nodeId, status: "Completed", completedAt: "simulated" })) }} />
      <div className="form-actions flex-wrap">
        <button type="button" className="button primary" disabled={!!pending} onClick={save}>{pending === "save" ? t.saved : t.save}</button>
        <button type="button" className="button secondary" disabled={!!pending} onClick={() => setProposal(undefined)}>{t.discard}</button>
      </div>
    </div>}
  </details>;
}
