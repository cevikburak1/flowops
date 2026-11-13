"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { categories, label, messages, type Locale } from "../lib/messages";
import { Button } from "./ui/button";
import { workflowText, type PublishedWorkflow } from "../lib/workflows";
import { priorities, taskText } from "../lib/tasks";
import type { PurchaseDetail } from "../lib/server";

async function send(path: string, payload?: object) {
  return fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload ?? {}) });
}

function signIn() {
  location.assign(new URL("/auth/login", location.origin));
}

export function CancelPurchase({ path, locale }: { path: string; locale: Locale }) {
  const t = messages[locale];
  const router = useRouter();
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  return <div className="form-actions flex-wrap">
    <Button type="button" variant={confirm ? "danger" : "secondary"} disabled={busy} onClick={async () => {
      if (!confirm) { setConfirm(true); return; }
      setBusy(true);
      setError(false);
      try {
        if (!(await send(path)).ok) throw new Error();
        router.refresh();
      } catch { setError(true); }
      finally { setBusy(false); }
    }}>{confirm ? t.confirmation : t.cancelPurchase}</Button>
    {confirm && <Button type="button" variant="secondary" disabled={busy} onClick={() => setConfirm(false)}>{t.cancel}</Button>}
    {error && <p className="error-text" role="alert">{t.conflict}</p>}
  </div>;
}

export function PurchaseForm({ organizationId, currency, locale, workflow, previous }: {
  organizationId: string; currency: string; locale: Locale; workflow?: PublishedWorkflow; previous?: PurchaseDetail;
}) {
  const t = messages[locale];
  const router = useRouter();
  const requestId = useRef<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setFieldErrors({});
    const fields = new FormData(event.currentTarget);
    requestId.current ??= crypto.randomUUID();
    try {
      const response = await send(`/api/organizations/${organizationId}/purchases`, {
        ...Object.fromEntries(fields), id: requestId.current, amount: Number(fields.get("amount")), currency,
        workflowId: workflow?.id, workflowVersion: workflow?.number,
        previousPurchaseId: previous?.id,
      });
      if (response.status === 401) { signIn(); return; }
      if (response.status === 400) {
        setFieldErrors((await response.json()).errors ?? {});
        setError(t.apiError);
      } else if (response.status === 409) {
        const { code } = await response.json();
        setError(code === "workflowChanged" ? workflowText[locale].changed : t.conflict);
      } else if (workflow && response.status === 404) setError(workflowText[locale].changed);
      else if (!response.ok) setError(t.apiError);
      else {
        const { id } = await response.json();
        router.push(`/workspace/${organizationId}/purchases/${id}`);
        router.refresh();
      }
    } catch { setError(t.apiError); }
    finally { setBusy(false); }
  }
  const feedback = (field: string, message: string) =>
    fieldErrors[field] ? <span className="error-text" id={`error-${field}`}>{message}</span> : null;
  const validation = (field: string) => ({
    "aria-invalid": !!fieldErrors[field],
    "aria-describedby": fieldErrors[field] ? `error-${field}` : undefined,
  });
  return <form className="stack-form" onSubmit={submit}>
    <label>{t.title}<input name="title" defaultValue={previous?.title} required minLength={3} maxLength={120} {...validation("title")} />{feedback("title", t.titleInvalid)}</label>
    <div className="form-row">
      <label>{t.amount} ({currency})<input name="amount" defaultValue={previous?.amount} type="number" inputMode="decimal" min="0.01" max="999999999.99" step="0.01" required {...validation("amount")} />{feedback("amount", t.amountInvalid)}</label>
      <label>{t.category}<select name="category" defaultValue={previous?.category ?? ""} required {...validation("category")}>
        <option value="" disabled>{t.chooseCategory}</option>{categories.map(category => <option key={category} value={category}>{label(locale, category)}</option>)}
      </select>{feedback("category", t.categoryInvalid)}</label>
    </div>
    <label>{taskText[locale].priority}<select name="priority" defaultValue={previous?.priority ?? "normal"}>
      {priorities.map(priority => <option key={priority} value={priority}>{taskText[locale][priority]}</option>)}
    </select></label>
    <label>{t.description}<textarea name="description" defaultValue={previous?.description} required minLength={5} maxLength={2000} rows={5} {...validation("description")} />{feedback("description", t.descriptionInvalid)}</label>
    {error && <p className="error-text" role="alert">{error}</p>}
    <Button type="submit" disabled={busy}>{busy ? t.saving : t.submit}</Button>
  </form>;
}

export function OrganizationForm({ locale }: { locale: Locale }) {
  const t = messages[locale];
  const router = useRouter();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setBusy(true);
    try {
      const response = await send("/api/organizations", Object.fromEntries(new FormData(event.currentTarget)));
      if (response.status === 401) { signIn(); return; }
      if (!response.ok) { setError(response.status === 409 ? t.conflict : t.apiError); return; }
      const { id } = await response.json();
      router.push(`/workspace/${id}`);
      router.refresh();
    } catch { setError(t.apiError); }
    finally { setBusy(false); }
  }
  return <form className="stack-form" onSubmit={submit}>
    <label>{t.name}<input name="name" required minLength={2} maxLength={100} /></label>
    <label>{t.slug}<input name="slug" required minLength={2} maxLength={48} pattern="[a-zA-Z0-9](?:[a-zA-Z0-9\\-]*[a-zA-Z0-9])" /></label>
    {error && <p role="alert" className="error-text">{error}</p>}
    <Button type="submit" disabled={busy}>{busy ? t.saving : t.create}</Button>
  </form>;
}

export function DecisionForm({ path, locale }: { path: string; locale: Locale }) {
  const t = messages[locale];
  const router = useRouter();
  const [error, setError] = useState("");
  const [decision, setDecision] = useState<"approved" | "rejected" | "changesRequested" | null>(null);
  const [busy, setBusy] = useState(false);
  const [accepted, setAccepted] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!decision) return;
    setBusy(true);
    setError("");
    try {
      const response = await send(path, { decision, comment: new FormData(event.currentTarget).get("comment") });
      if (response.status === 401) { signIn(); return; }
      if (response.ok) { setAccepted(true); router.refresh(); }
      else setError(response.status === 409 ? t.conflict : t.apiError);
    } catch { setError(t.apiError); }
    finally { setBusy(false); }
  }
  if (accepted) return <p role="status">{t.decisionSaved}</p>;
  return <form className="stack-form decision-form" onSubmit={submit}>
    <label>{t.comment}<textarea name="comment" maxLength={1000} rows={2} required={decision === "changesRequested"} /></label>
    {error && <p role="alert" className="error-text">{error}</p>}
    <div className="form-actions" key={decision ?? "choose"}>{decision ? <>
      <Button type="submit" variant={decision === "approved" ? "primary" : "danger"} disabled={busy}>
        {busy ? t.saving : `${t.confirmation} (${decision === "approved" ? t.approve : decision === "rejected" ? t.reject : t.requestChanges})`}
      </Button>
      <Button type="button" variant="secondary" onClick={() => setDecision(null)} disabled={busy}>{t.cancel}</Button>
    </> : <>
      <Button type="button" onClick={() => setDecision("approved")}>{t.approve}</Button>
      <Button type="button" variant="secondary" onClick={() => setDecision("rejected")}>{t.reject}</Button>
      <Button type="button" variant="secondary" onClick={() => setDecision("changesRequested")}>{t.requestChanges}</Button>
    </>}</div>
  </form>;
}

export function RefreshPending({ locale }: { locale: Locale }) {
  const router = useRouter();
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") router.refresh();
    }, 3000);
    return () => window.clearInterval(timer);
  }, [router]);
  return <p className="muted" role="status">{messages[locale].processing}</p>;
}

export function MarkRead({ path, locale }: { path: string; locale: Locale }) {
  const router = useRouter();
  const [state, setState] = useState("idle");
  if (state === "done") return null;
  return <Button type="button" variant="secondary" disabled={state === "busy"} onClick={async () => {
    setState("busy");
    try {
      const response = await send(path);
      if (response.status === 401) { signIn(); return; }
      if (!response.ok) { setState("error"); return; }
      setState("done");
      router.refresh();
    } catch { setState("error"); }
  }}>{state === "error" ? messages[locale].tryAgain : messages[locale].read}</Button>;
}
