"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import type { Locale } from "../lib/messages";

export interface DelegationSettings {
  delegation: { delegate: string; startsAt: string; endsAt: string } | null;
  candidates: { subject: string; email: string | null }[];
}

const strings = {
  en: { title: "Scheduled delegation", hint: "New single-reviewer assignments transfer during the selected UTC period.",
    target: "Delegate", start: "Starts (UTC)", end: "Ends (UTC)", save: "Save delegation", remove: "Remove delegation",
    saved: "Delegation updated", error: "Choose an eligible reviewer and a period of up to 90 days." },
  tr: { title: "Planlı onay devri", hint: "Seçilen UTC aralığında açılan tek onaylayıcılı görevler devredilir.",
    target: "Devralan", start: "Başlangıç (UTC)", end: "Bitiş (UTC)", save: "Devri kaydet", remove: "Devri kaldır",
    saved: "Onay devri güncellendi", error: "Uygun bir onaylayıcı ve en fazla 90 günlük bir dönem seçin." },
};

export function ApprovalDelegationEditor({ api, data, locale }: { api: string; data: DelegationSettings; locale: Locale }) {
  const t = strings[locale];
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("");
  const [error, setError] = useState(false);
  const current = data.delegation;
  async function save(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();
    setBusy(true);
    setStatus("");
    setError(false);
    const fields = event ? new FormData(event.currentTarget) : null;
    try {
      const response = await fetch(api, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(fields ? { delegate: fields.get("delegate"),
          startsAt: `${fields.get("start")}:00Z`, endsAt: `${fields.get("end")}:00Z` } : { delegate: null }) });
      if (!response.ok) throw new Error();
      setStatus(t.saved);
      router.refresh();
    } catch { setError(true); }
    finally { setBusy(false); }
  }
  if (!data.candidates.length && !current) return null;
  return <details className="panel detail-panel mb-4">
    <summary>{t.title}</summary>
    <p className="muted mt-2">{t.hint}</p>
    <form key={current?.endsAt ?? "new"} className="stack-form mt-3" onSubmit={save}>
      <label>{t.target}<select name="delegate" required defaultValue={current?.delegate ?? ""}>
        <option value="">—</option>{data.candidates.map(candidate =>
          <option key={candidate.subject} value={candidate.subject}>{candidate.email ?? candidate.subject}</option>)}
      </select></label>
      <div className="form-row">
        <label>{t.start}<input type="datetime-local" name="start" required defaultValue={current?.startsAt.slice(0, 16)} /></label>
        <label>{t.end}<input type="datetime-local" name="end" required defaultValue={current?.endsAt.slice(0, 16)} /></label>
      </div>
      <div className="form-actions flex-wrap">
        <button className="button primary" disabled={busy} type="submit">{t.save}</button>
        {current && <button className="button secondary" disabled={busy} type="button" onClick={() => void save()}>{t.remove}</button>}
      </div>
      {status && <p role="status">{status}</p>}
      {error && <p className="error-text" role="alert">{t.error}</p>}
    </form>
  </details>;
}
