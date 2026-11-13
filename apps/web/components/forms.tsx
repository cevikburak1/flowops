"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { categories, label, messages, type Locale } from "../lib/messages";
import { Button } from "./ui/button";
import { workflowText, type PublishedWorkflow } from "../lib/workflows";

async function send(path: string, payload?: object) {
  return fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload ?? {}) });
}

function signIn() {
  location.assign(new URL("/auth/login", location.origin));
}

export function PurchaseForm({ organizationId, currency, locale, workflow }: {
  organizationId: string; currency: string; locale: Locale; workflow?: PublishedWorkflow;
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
    <label>{t.title}<input name="title" required minLength={3} maxLength={120} {...validation("title")} />{feedback("title", t.titleInvalid)}</label>
    <div className="form-row">
      <label>{t.amount} ({currency})<input name="amount" type="number" inputMode="decimal" min="0.01" max="999999999.99" step="0.01" required {...validation("amount")} />{feedback("amount", t.amountInvalid)}</label>
      <label>{t.category}<select name="category" defaultValue="" required {...validation("category")}>
        <option value="" disabled>{t.chooseCategory}</option>{categories.map(category => <option key={category} value={category}>{label(locale, category)}</option>)}
      </select>{feedback("category", t.categoryInvalid)}</label>
    </div>
    <label>{t.description}<textarea name="description" required minLength={5} maxLength={2000} rows={5} {...validation("description")} />{feedback("description", t.descriptionInvalid)}</label>
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
  const [decision, setDecision] = useState<"approved" | "rejected" | null>(null);
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
    <label>{t.comment}<textarea name="comment" maxLength={1000} rows={2} /></label>
    {error && <p role="alert" className="error-text">{error}</p>}
    <div className="form-actions" key={decision ?? "choose"}>{decision ? <>
      <Button type="submit" variant={decision === "approved" ? "primary" : "danger"} disabled={busy}>
        {busy ? t.saving : `${t.confirmation} (${decision === "approved" ? t.approve : t.reject})`}
      </Button>
      <Button type="button" variant="secondary" onClick={() => setDecision(null)} disabled={busy}>{t.cancel}</Button>
    </> : <>
      <Button type="button" onClick={() => setDecision("approved")}>{t.approve}</Button>
      <Button type="button" variant="secondary" onClick={() => setDecision("rejected")}>{t.reject}</Button>
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
