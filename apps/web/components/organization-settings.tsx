"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "./ui/button";
import { Pagination } from "./pagination";
import { organizationError, organizationText, roles, type Member, type OrganizationSettings, type OrganizationGroup } from "../lib/organization";
import type { Locale } from "../lib/messages";
import { plans, reportText, type Plan } from "../lib/reports";

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

function GroupForm({ group, managers, base, locale }: {
  group?: OrganizationGroup; managers: Member[]; base: string; locale: Locale;
}) {
  const router = useRouter();
  const t = organizationText[locale];
  return <SettingsForm locale={locale} submit={group ? t.save : t.addGroup} action={async fields => {
    await post(group ? `${base}/groups/${group.id}` : `${base}/groups`, {
      kind: group?.kind ?? fields.get("kind"), name: fields.get("name"), lead: fields.get("lead"),
    });
    router.refresh();
  }}>
    {!group && <label>{t.role}<select name="kind"><option value="team">{t.team}</option>
      <option value="department">{t.department}</option></select></label>}
    <label>{t.groupName}<input name="name" required minLength={2} maxLength={80} defaultValue={group?.name} /></label>
    <label>{t.groupLead}<select name="lead" defaultValue={group?.lead ?? ""}>
      <option value="">{t.noManager}</option>
      {managers.map(manager => <option key={manager.subject} value={manager.subject}>{memberLabel(manager)}</option>)}
    </select></label>
  </SettingsForm>;
}

export function ReassignApproval({ locale, path, current, members, returnToTasks }: {
  locale: Locale; path: string; current: string; members: Pick<Member, "subject" | "email">[]; returnToTasks?: string;
}) {
  const router = useRouter();
  const t = organizationText[locale];
  const action = returnToTasks ? t.delegate : t.assign;
  return <SettingsForm locale={locale} submit={action} action={async fields => {
    await post(path, { assignee: fields.get("assignee"), expectedAssignee: current });
    if (returnToTasks) router.push(returnToTasks);
    else router.refresh();
  }}>
    <label>{action}<select name="assignee" required defaultValue="">
      <option value="" disabled>{t.noManager}</option>
      {members.map(member => <option key={member.subject} value={member.subject}>{member.email ?? member.subject}</option>)}
    </select></label>
  </SettingsForm>;
}

export function ChangePlan({ locale, id, name, plan }: { locale: Locale; id: string; name: string; plan: Plan }) {
  const t = reportText[locale];
  const router = useRouter();
  return <SettingsForm locale={locale} submit={t.savePlan} action={async fields => {
    await post(`/api/platform/organizations/${id}/plan`, { plan: fields.get("plan") });
    router.refresh();
  }}>
    <select aria-label={`${t.plan} / ${name}`} name="plan" defaultValue={plan}>
      {plans.map(option => <option key={option} value={option}>{t[option]}</option>)}
    </select>
  </SettingsForm>;
}

export function Settings({ data, base, currency, locale, pages }: {
  data: OrganizationSettings; base: string; currency: string; locale: Locale;
  pages: { members: number; invitations: number; groups: number };
}) {
  const t = organizationText[locale];
  const router = useRouter();
  const [link, setLink] = useState("");
  const [copied, setCopied] = useState(false);
  const assignable = roles.filter(role => data.canAssignAdmin || role !== "OrganizationAdmin");
  const managers = data.members.filter(member => ["OrganizationOwner", "OrganizationAdmin", "Manager"].includes(member.role));
  const approvers = data.members.filter(member => member.role === "Approver");
  const size = 8;
  const list = <T,>(key: keyof typeof pages, items: T[]) => {
    const page = Math.min(pages[key], Math.max(1, Math.ceil(items.length / size)));
    return { items: items.slice((page - 1) * size, page * size), page, more: items.length > page * size };
  };
  const members = list("members", data.members);
  const invitations = list("invitations", data.invitations);
  const groups = list("groups", data.groups);
  const pageLink = (key: keyof typeof pages, page: number) =>
    `?${new URLSearchParams({ members: String(members.page), invitations: String(invitations.page), groups: String(groups.page), [key]: String(page) })}#settings-${key}`;

  return <div className="detail-grid">
    <div className="detail-main">
      <section className="panel detail-panel">
        <h2 id="settings-members">{t.members}</h2>
        <ul className="decisions">
          {members.items.map(member => {
            const locked = !data.canAssignAdmin && ["OrganizationOwner", "OrganizationAdmin"].includes(member.role);
            const identity = <><span>{memberLabel(member)}</span><span className="status">{t[member.role]}</span></>;
            if (locked) return <li key={member.subject} className="member-identity">{identity}</li>;
            return <li key={member.subject}>
              <details className="member-details">
                <summary>{identity}</summary>
                <SettingsForm locale={locale} action={async fields => {
                  await post(`${base}/members/${encodeURIComponent(member.subject)}/settings`, {
                    role: fields.get("role") ?? member.role, manager: fields.get("manager"),
                    groups: { teamId: fields.get("team") || null, departmentId: fields.get("department") || null },
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
                  <div className="form-row">
                    {(["team", "department"] as const).map(kind => <label key={kind}>{t[kind]}
                      <select name={kind} defaultValue={(kind === "team" ? member.teamId : member.departmentId) ?? ""}>
                        <option value="">{t.noGroup}</option>
                        {data.groups.filter(group => group.kind === kind).map(group =>
                          <option key={group.id} value={group.id}>{group.name}</option>)}
                      </select>
                    </label>)}
                  </div>
                </SettingsForm>
                {member.role !== "OrganizationOwner" && member.subject !== data.actorSubject &&
                  <details className="mt-3">
                    <summary>{t.remove}</summary>
                    <SettingsForm locale={locale} submit={t.confirmRemove} action={async () => {
                      await post(`${base}/members/${encodeURIComponent(member.subject)}/remove`, {});
                      router.refresh();
                    }}>
                      <p>{t.removeHint}</p>
                    </SettingsForm>
                  </details>}
              </details>
            </li>;
          })}
        </ul>
        <Pagination page={members.page} more={members.more} href={next => pageLink("members", next)} locale={locale} label={t.members} />
      </section>
      <section className="panel detail-panel">
        <h2 id="settings-invitations">{t.pending}</h2>
        <ul className="decisions">{invitations.items.map(invitation => <li key={invitation.id}>
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
        <Pagination page={invitations.page} more={invitations.more} href={next => pageLink("invitations", next)} locale={locale} label={t.pending} />
      </section>
    </div>
    <div className="detail-main">
      {data.canAssignAdmin && <section className="panel detail-panel settings-panel">
        <h2>{t.processPermissions}</h2>
        <SettingsForm locale={locale} action={async fields => {
          const grants = Object.fromEntries(Object.keys(data.processPermissions).map(role => [role, {
            edit: fields.has(`${role}.edit`), run: fields.has(`${role}.run`), inspect: fields.has(`${role}.inspect`)
          }]));
          await post(`${base}/settings/process-permissions`, grants);
          router.refresh();
        }}>
          {Object.entries(data.processPermissions).map(([role, grant]) => <fieldset key={role} className="min-w-0">
            <legend className="font-medium">{t[role as Member["role"]]}</legend>
            <div className="flex flex-wrap gap-3">
              {([["edit", t.editProcess], ["run", t.runProcess], ["inspect", t.inspectProcess]] as const).map(([key, title]) =>
                <label key={key} className="!flex items-center"><input type="checkbox" className="!w-auto"
                  name={`${role}.${key}`} defaultChecked={grant[key]} />{title}</label>)}
            </div>
          </fieldset>)}
        </SettingsForm>
      </section>}
      <section className="panel detail-panel settings-panel">
        <h2>{t.integrations}</h2>
        {data.webhookAvailable || data.webhookEnabled ? <SettingsForm locale={locale} action={async fields => {
          await post(`${base}/integrations/webhook`, { enabled: fields.get("enabled") === "on" });
          router.refresh();
        }}>
          <label><span>{t.webhook}</span><input className="w-auto" type="checkbox" name="enabled" defaultChecked={data.webhookEnabled} /></label>
        </SettingsForm> : <p className="muted">{t.webhookUnavailable}</p>}
      </section>
      <section className="panel detail-panel settings-panel">
        <h2 id="settings-groups">{t.groups}</h2>
        {groups.items.map(group => <details key={group.id} className="member-details">
          <summary><span>{group.name}</span><span className="status">{t[group.kind]}</span></summary>
          <GroupForm group={group} base={base} managers={managers} locale={locale} />
        </details>)}
        <Pagination page={groups.page} more={groups.more} href={next => pageLink("groups", next)} locale={locale} label={t.groups} />
        <GroupForm base={base} managers={managers} locale={locale} />
      </section>
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
