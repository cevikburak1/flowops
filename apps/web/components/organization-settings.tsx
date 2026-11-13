"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "./ui/button";
import { organizationError, organizationText, roles, type Member, type OrganizationSettings } from "../lib/organization";
import type { Locale } from "../lib/messages";

async function post(path: string, data: object) {
  const response = await fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(data) });
  if (response.status === 401) {
    location.assign(new URL("/auth/login", location.origin));
    throw new Error("sessionExpired");
  }
  if (!response.ok) {
    const result = await response.json().catch(() => ({}));
    throw new Error(result.code ?? "error");
  }
  return response;
}

function SettingsForm({ locale, action, children, submit }: {
  locale: Locale; action: (data: FormData) => Promise<void>; children?: React.ReactNode; submit?: string;
}) {
  const t = organizationText[locale];
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [failed, setFailed] = useState(false);
  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage("");
    setFailed(false);
    try {
      await action(new FormData(event.currentTarget));
      setMessage(t.saved);
    } catch (error) {
      setFailed(true);
      setMessage(organizationError(locale, error instanceof Error ? error.message : "error"));
    } finally { setBusy(false); }
  }
  return <form className="stack-form" onSubmit={onSubmit}>
    {children}
    <Button type="submit" variant="secondary" disabled={busy}>{submit ?? t.save}</Button>
    {message && <p role={failed ? "alert" : "status"} className={failed ? "error-text" : "muted"}>{message}</p>}
  </form>;
}

function memberLabel(member: Member) { return member.email ?? member.subject; }

export function Settings({ data, base, currency, locale }: {
  data: OrganizationSettings; base: string; currency: string; locale: Locale;
}) {
  const t = organizationText[locale];
  const router = useRouter();
  const [link, setLink] = useState("");
  const [copied, setCopied] = useState(false);
  const assignable = roles.filter(role => data.canAssignAdmin || role !== "OrganizationAdmin");
  const managers = data.members.filter(member => ["OrganizationOwner", "OrganizationAdmin", "Manager"].includes(member.role));
  const approvers = data.members.filter(member => member.role === "Approver");

  return <div className="detail-grid">
    <div className="detail-main">
      <section className="panel detail-panel">
        <h2>{t.members}</h2>
        <ul className="decisions">
          {data.members.map(member => {
            const locked = !data.canAssignAdmin && ["OrganizationOwner", "OrganizationAdmin"].includes(member.role);
            const identity = <><span>{memberLabel(member)}</span><span className="status">{t[member.role]}</span></>;
            if (locked) return <li key={member.subject} className="member-identity">{identity}</li>;
            return <li key={member.subject}>
              <details className="member-details">
                <summary>{identity}</summary>
                <SettingsForm locale={locale} action={async fields => {
                  await post(`${base}/members/${encodeURIComponent(member.subject)}/settings`, {
                    role: fields.get("role") ?? member.role, manager: fields.get("manager"),
                  });
                  router.refresh();
                }}>
                  <div className="form-row">
                    <label>{t.role}<select name="role" defaultValue={member.role} disabled={member.role === "OrganizationOwner"}>
                      {member.role === "OrganizationOwner" && <option value={member.role}>{t[member.role]}</option>}
                      {assignable.map(role => <option key={role} value={role}>{t[role]}</option>)}
                    </select></label>
                    <label>{t.manager}<select name="manager" defaultValue={member.manager ?? ""}>
                      <option value="">{t.noManager}</option>
                      {managers.filter(manager => manager.subject !== member.subject).map(manager =>
                        <option key={manager.subject} value={manager.subject}>{memberLabel(manager)}</option>)}
                    </select></label>
                  </div>
                </SettingsForm>
              </details>
            </li>;
          })}
        </ul>
      </section>
      <section className="panel detail-panel">
        <h2>{t.pending}</h2>
        <ul className="decisions">{data.invitations.map(invitation => <li key={invitation.id}>
          <h3>{invitation.email}</h3>
          <p className="muted">{t[invitation.role]}</p>
          <p className="muted">{t.expires} <time dateTime={invitation.expiresAt}>
            {new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeZone: "UTC" }).format(new Date(invitation.expiresAt))}
          </time></p>
          <SettingsForm locale={locale} submit={t.revoke} action={async () => {
            await post(`${base}/invitations/${invitation.id}/revoke`, {});
            router.refresh();
          }} />
        </li>)}</ul>
        {data.invitations.length === 0 && <p className="muted">{t.noInvites}</p>}
      </section>
    </div>
    <div className="detail-main">
      <section className="panel detail-panel settings-panel">
        <h2>{t.invite}</h2>
        <p className="muted">{t.inviteHint}</p>
        <SettingsForm locale={locale} submit={t.invite} action={async fields => {
          setCopied(false);
          setLink("");
          const response = await post(`${base}/invitations`, { email: fields.get("email"), role: fields.get("role") });
          const { code } = await response.json();
          setLink(new URL(`/join#${code}`, location.origin).href);
          router.refresh();
        }}>
          <label>{t.email}<input name="email" type="email" required maxLength={254} /></label>
          <label>{t.role}<select name="role" defaultValue="Employee">
            {assignable.map(role => <option key={role} value={role}>{t[role]}</option>)}
          </select></label>
        </SettingsForm>
        {link && <div className="stack-form">
          <label>{t.inviteLink}<input value={link} readOnly onFocus={event => event.target.select()} /></label>
          <Button variant="secondary" type="button" onClick={async () => {
            try { await navigator.clipboard.writeText(link); setCopied(true); }
            catch { setCopied(false); }
          }}>{copied ? t.copied : t.copy}</Button>
        </div>}
      </section>
      <section className="panel detail-panel settings-panel">
        <h2>{t.routing}</h2>
        <p className="muted">{t.routingHint}</p>
        <SettingsForm locale={locale} action={async fields => {
          await post(`${base}/routing`, { threshold: Number(fields.get("threshold")), financeApprover: fields.get("financeApprover") });
          router.refresh();
        }}>
          <label>{t.threshold} ({currency})<input name="threshold" type="number" required min="0" max="999999999.99" step="0.01" defaultValue={data.financeThreshold} /></label>
          <label>{t.finance}<select name="financeApprover" defaultValue={data.financeApprover ?? ""}>
            <option value="">{t.automatic}</option>
            {approvers.map(approver => <option key={approver.subject} value={approver.subject}>{memberLabel(approver)}</option>)}
          </select></label>
        </SettingsForm>
      </section>
    </div>
  </div>;
}
