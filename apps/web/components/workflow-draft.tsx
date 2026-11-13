"use client";

import { useState, useTransition, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "./ui/button";
import { messages, type Locale } from "../lib/messages";
import { workflowText, type WorkflowDetail } from "../lib/workflows";
import { PurchaseGraphEditor } from "./purchase-graph-editor";
import { graphText, isCompleteGraph, purchaseTemplate } from "../lib/purchase-graph";

export function WorkflowDraftForm({ api, href, locale, currency, workflow }: {
  api: string; href: string; locale: Locale; currency: string; workflow?: WorkflowDetail;
}) {
  const router = useRouter();
  const t = workflowText[locale];
  const common = messages[locale];
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [confirmation, setConfirmation] = useState<"archive" | "unpublish" | null>(null);
  const [copying, setCopying] = useState(false);
  const [refreshing, startRefresh] = useTransition();
  const [name, setName] = useState(workflow?.name ?? t.template);
  const [threshold, setThreshold] = useState(String(workflow?.financeThreshold ?? 5000));
  const [graph, setGraph] = useState(() => workflow?.graph ?? purchaseTemplate(true));
  const complete = isCompleteGraph(graph);
  const dirty = !!workflow && (name.trim() !== workflow.name || threshold === "" || Number(threshold) !== workflow.financeThreshold ||
    JSON.stringify(graph) !== JSON.stringify(workflow.graph));
  const pending = busy || refreshing;

  async function change(action: "draft" | "publish" | "archive" | "unpublish" | "duplicate", draft?: object) {
    setBusy(true);
    setError("");
    setSaved(false);
    try {
      const response = await fetch(workflow ? `${api}/${action}` : api, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...draft, revision: workflow?.revision }),
      });
      if (response.status === 401) { location.assign(new URL("/auth/login", location.origin)); return; }
      if (!response.ok) {
        setError(response.status === 409 ? t.changed : response.status === 400
          ? action === "publish" ? graphText[locale].invalid : t.invalid : t.failed);
        return;
      }
      if (!workflow || action === "duplicate") {
        const { id } = await response.json();
        router.push(`${href}/${id}`);
        return;
      }
      setSaved(action === "draft");
      setConfirmation(null);
      startRefresh(() => router.refresh());
    } catch { setError(t.failed); }
    finally { setBusy(false); }
  }

  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    void change("draft", { name: data.get("name"), financeThreshold: Number(data.get("financeThreshold")), graph });
  }

  return <div className="settings-panel">
    {!workflow?.archived && <PurchaseGraphEditor graph={graph} onChange={setGraph} locale={locale} />}
    {!workflow?.archived && <form className="stack-form" onSubmit={save}>
      <label>{t.name}<input name="name" required minLength={3} maxLength={120} value={name} onChange={event => setName(event.target.value)} /></label>
      <label>{t.threshold} ({currency})<input type="number" name="financeThreshold" required min="0" max="999999999.99" step="0.01"
        value={threshold} onChange={event => setThreshold(event.target.value)} /></label>
      <Button type="submit" disabled={pending}>{t.save}</Button>
    </form>}
    {workflow && !workflow.archived && <div className="form-actions flex-wrap">
      {(workflow.hasDraft || dirty) && <Button type="button" variant="secondary" disabled={pending || dirty || !complete} onClick={() => change("publish")}>{t.publish}</Button>}
      {!confirmation && <>
        {workflow.publishedVersion !== null && !workflow.unpublished &&
          <Button type="button" variant="secondary" disabled={pending} onClick={() => setConfirmation("unpublish")}>{t.unpublish}</Button>}
        <Button type="button" variant="secondary" disabled={pending} onClick={() => setConfirmation("archive")}>{t.archive}</Button>
      </>}
      {confirmation && <>
        <p className="muted basis-full">{confirmation === "archive" ? t.archiveHint : t.unpublishHint}</p>
        <Button type="button" variant="danger" disabled={pending} onClick={() => change(confirmation)}>
          {confirmation === "archive" ? t.confirmArchive : t.confirmUnpublish}
        </Button>
        <Button type="button" variant="secondary" disabled={pending} onClick={() => setConfirmation(null)}>{common.cancel}</Button>
      </>}
    </div>}
    {workflow && !copying && <div>
      <Button type="button" variant="secondary" disabled={pending} onClick={() => setCopying(true)}>{t.duplicate}</Button>
    </div>}
    {workflow && copying && <form className="stack-form" onSubmit={event => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      void change("duplicate", { name: data.get("copyName") });
    }}>
      <label>{t.copyName}<input name="copyName" required minLength={3} maxLength={120}
        defaultValue={workflow.name.slice(0, 120 - t.copySuffix.length) + t.copySuffix} /></label>
      <div className="form-actions flex-wrap">
        <Button type="submit" disabled={pending}>{t.duplicate}</Button>
        <Button type="button" variant="secondary" disabled={pending} onClick={() => setCopying(false)}>{common.cancel}</Button>
      </div>
    </form>}
    {error && <p role="alert" className="error-text">{error}</p>}
    {dirty && <p className="muted">{t.saveBeforePublish}</p>}
    {saved && !dirty && <p role="status">{t.saved}</p>}
  </div>;
}
