"use client";

import { useState, type FormEvent } from "react";
import type { Locale } from "../lib/messages";
import type { Member } from "../lib/organization";

export interface SlaSettings {
  approvalSlaRevision: number;
  policy: { businessMinutes: number; warningMinutes: number | null; workflowMinutes?: number; action?: string; fallback?: string;
    calendar: { timeZone: string; workingDays: number[]; opens: string; closes: string; holidays: string[] } } | null;
}
const en = { title: "Approval deadlines", enabled: "Use business hours", zone: "Time zone (IANA)", opens: "Opens", closes: "Closes",
  days: "Working days", holidays: "Holidays (YYYY-MM-DD, one per line)", minutes: "Business minutes to deadline",
  workflowMinutes: "Business minutes for the full purchase workflow",
  warning: "Business minutes before deadline to remind", save: "Save deadline policy", saved: "Policy saved",
  conflict: "The policy changed. Reload before saving.", error: "Check the calendar and deadline values.",
  action: "At the deadline", administrator: "Notify an administrator", supervisor: "Notify the reviewer’s manager",
  department: "Notify the department lead", mark: "Mark overdue", reassign: "Reassign to an eligible reviewer", fallback: "Fallback reviewer" };
const tr: typeof en = { title: "Onay süreleri", enabled: "Çalışma saatlerini kullan", zone: "Saat dilimi (IANA)", opens: "Başlangıç", closes: "Bitiş",
  days: "Çalışma günleri", holidays: "Tatiller (YYYY-MM-DD, her satıra bir tarih)", minutes: "Son tarihe kadar iş dakikası",
  workflowMinutes: "Satın alma akışının tamamı için iş dakikası",
  warning: "Hatırlatma için son tarihten önceki iş dakikası", save: "Süre politikasını kaydet", saved: "Politika kaydedildi",
  conflict: "Politika değişti. Kaydetmeden önce sayfayı yenileyin.", error: "Takvimi ve süre değerlerini kontrol edin.",
  action: "Son tarihte", administrator: "Kuruluş yöneticisine bildir", supervisor: "Onaylayanın yöneticisine bildir",
  department: "Departman sorumlusuna bildir", mark: "Gecikmiş olarak işaretle", reassign: "Uygun onaylayıcıya ata", fallback: "Yedek onaylayıcı" };

export function ApprovalSlaEditor({ api, settings, locale, members }: { api: string; settings: SlaSettings; locale: Locale; members: Member[] }) {
  const t = locale === "tr" ? tr : en;
  const [enabled, setEnabled] = useState(settings.policy !== null);
  const [revision, setRevision] = useState(settings.approvalSlaRevision);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const [action, setAction] = useState(settings.policy?.action ?? "administrator");
  const policy = settings.policy;
  const calendar = policy?.calendar;
  const weekday = new Intl.DateTimeFormat(locale, { weekday: "long", timeZone: "UTC" });
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setStatus("");
    const fields = new FormData(event.currentTarget);
    try {
      const response = await fetch(api, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ revision, policy: enabled ? {
          calendar: { timeZone: String(fields.get("zone")).trim(), workingDays: fields.getAll("day").map(Number),
            opens: `${fields.get("opens")}:00`, closes: `${fields.get("closes")}:00`,
            holidays: String(fields.get("holidays")).split(/\r?\n/).map(value => value.trim()).filter(Boolean) },
          businessMinutes: Number(fields.get("minutes")), warningMinutes: Number(fields.get("warning")) || null,
          action, fallback: action === "reassign" ? fields.get("fallback") : null,
          workflowMinutes: Number(fields.get("workflowMinutes")) || null,
        } : null }) });
      if (response.status === 409) { setError(t.conflict); return; }
      if (!response.ok) throw new Error();
      setRevision((await response.json() as Pick<SlaSettings, "approvalSlaRevision">).approvalSlaRevision);
      setStatus(t.saved);
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }
  return <section className="panel detail-panel settings-panel"><h2>{t.title}</h2>
    <form className="stack-form" onSubmit={save}>
      <label className="!flex items-center"><input className="!w-auto" type="checkbox" checked={enabled}
        onChange={event => setEnabled(event.target.checked)} />{t.enabled}</label>
      <fieldset disabled={!enabled} className={enabled ? "settings-panel" : "hidden"}>
        <label>{t.zone}<input name="zone" required maxLength={100} defaultValue={calendar?.timeZone ?? "Europe/Istanbul"} /></label>
        <div className="form-row">
          <label>{t.opens}<input type="time" name="opens" required defaultValue={calendar?.opens.slice(0, 5) ?? "09:00"} /></label>
          <label>{t.closes}<input type="time" name="closes" required defaultValue={calendar?.closes.slice(0, 5) ?? "17:00"} /></label>
        </div>
        <label>{t.action}<select name="action" value={action} onChange={event => setAction(event.target.value)}>
          {(["administrator", "supervisor", "department", "mark", "reassign"] as const).map(action => <option key={action} value={action}>{t[action]}</option>)}
        </select></label>
        {action === "reassign" && <label>{t.fallback}<select name="fallback" required defaultValue={policy?.fallback ?? ""}>
          <option value="">—</option>
          {members.filter(member => ["OrganizationOwner", "OrganizationAdmin", "Manager", "Approver"].includes(member.role))
            .map(member => <option key={member.subject} value={member.subject}>{member.email ?? member.subject}</option>)}
        </select></label>}
        <fieldset><legend>{t.days}</legend><div className="form-actions flex-wrap">
          {[1, 2, 3, 4, 5, 6, 0].map(day => <label key={day} className="!flex items-center">
            <input type="checkbox" className="!w-auto" name="day" value={day} defaultChecked={(calendar?.workingDays ?? [1, 2, 3, 4, 5]).includes(day)} />
            {weekday.format(new Date(Date.UTC(2026, 0, 4 + day)))}
          </label>)}
        </div></fieldset>
        <label>{t.holidays}<textarea name="holidays" rows={3} maxLength={5000} defaultValue={calendar?.holidays.join("\n")} /></label>
        <div className="form-row">
          <label>{t.minutes}<input name="minutes" type="number" min="1" max="14400" required defaultValue={policy?.businessMinutes ?? 480} /></label>
          <label>{t.warning}<input name="warning" type="number" min="1" max="14399" defaultValue={policy?.warningMinutes ?? undefined} /></label>
          <label>{t.workflowMinutes}<input name="workflowMinutes" type="number" min="1" max="14400" defaultValue={policy?.workflowMinutes} /></label>
        </div>
      </fieldset>
      <button className="button primary" type="submit" disabled={busy}>{t.save}</button>
      {status && <p role="status">{status}</p>}
      {error && <p className="error-text" role="alert">{error}</p>}
    </form>
  </section>;
}
