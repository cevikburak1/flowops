"use client";

import { useState, type FormEvent } from "react";
import { matrixText, type MatrixSettings, type MatrixRule } from "../lib/approval-matrix";
import { categories, label, type Locale } from "../lib/messages";
import { organizationText, type Member, type OrganizationGroup } from "../lib/organization";

export function ApprovalMatrixEditor({ api, initial, departments, reviewers, locale }: {
  api: string; initial: MatrixSettings; departments: OrganizationGroup[]; reviewers: Member[]; locale: Locale;
}) {
  const t = matrixText[locale];
  const org = organizationText[locale];
  const [rules, setRules] = useState(initial.rules);
  const [revision, setRevision] = useState(initial.approvalMatrixRevision);
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState<{ route: MatrixRule["route"]; priority: number | null }>();
  const roles = ["OrganizationOwner", "OrganizationAdmin", "Manager", "Employee", "Approver"] as const;

  function update(index: number, value: Partial<MatrixRule>) {
    setRules(current => current.map((rule, i) => i === index ? { ...rule, ...value } : rule));
    setStatus("");
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setStatus("");
    try {
      const response = await fetch(api, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ revision, rules }) });
      if (response.status === 409) { setError(t.conflict); return; }
      if (!response.ok) throw new Error();
      const result = await response.json() as Pick<MatrixSettings, "approvalMatrixRevision">;
      setRevision(result.approvalMatrixRevision);
      setStatus(t.saved);
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }

  async function runPreview(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setPreview(undefined);
    setError("");
    const fields = new FormData(event.currentTarget);
    try {
      const response = await fetch(`${api}/preview`, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...Object.fromEntries(fields), amount: Number(fields.get("amount")),
          departmentId: fields.get("departmentId") || null, rules }) });
      if (!response.ok) throw new Error();
      setPreview(await response.json() as NonNullable<typeof preview>);
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }

  return <div className="detail-main"><form className="panel detail-panel stack-form" onSubmit={save}>
    {rules.map((rule, index) => <fieldset className="studio-field settings-panel" key={index}>
      <legend>{t.priority} {rule.priority}</legend>
      <div className="form-row">
        <label>{t.priority}<input required type="number" min="1" max="1000" value={rule.priority}
          onChange={event => update(index, { priority: Number(event.target.value) })} /></label>
        <label>{t.route}<select value={rule.route} onChange={event => update(index, { route: event.target.value as MatrixRule["route"] })}>
          <option value="manager">{t.manager}</option><option value="managerFinance">{t.managerFinance}</option>
        </select></label>
        <label>{t.reviewers}<select multiple value={rule.additionalReviewers ?? []} onChange={event => {
          const selected = Array.from(event.target.selectedOptions, option => option.value);
          update(index, { additionalReviewers: selected, minimumApprovals: selected.length ? Math.min(rule.minimumApprovals ?? 1, selected.length + 1) : null });
        }}>
          {reviewers.map(member => <option key={member.subject} value={member.subject}>{member.email ?? member.subject}</option>)}
        </select></label>
        {!!rule.additionalReviewers?.length && <label>{t.quorum}<input type="number" min="1" max={rule.additionalReviewers.length + 1}
          required value={rule.minimumApprovals ?? 1} onChange={event => update(index, { minimumApprovals: Number(event.target.value) })} /></label>}
        <label>{t.minimum} ({rule.currency})<input required type="number" min="0" step=".01" value={rule.minimum}
          onChange={event => update(index, { minimum: Number(event.target.value) })} /></label>
        <label>{t.maximum} ({rule.currency})<input type="number" min="0" step=".01" value={rule.maximum ?? ""}
          onChange={event => update(index, { maximum: event.target.value ? Number(event.target.value) : null })} /></label>
        <label>{t.category}<select value={rule.category ?? ""} onChange={event => update(index, { category: event.target.value || null })}>
          <option value="">{t.any}</option>{categories.map(category => <option key={category} value={category}>{label(locale, category)}</option>)}
        </select></label>
        <label>{t.role}<select value={rule.requesterRole ?? ""} onChange={event => update(index, { requesterRole: event.target.value || null })}>
          <option value="">{t.any}</option>{roles.map(role => <option key={role} value={role}>{org[role]}</option>)}
        </select></label>
        <label>{t.department}<select value={rule.departmentId ?? ""} onChange={event => update(index, { departmentId: event.target.value || null })}>
          <option value="">{t.any}</option>{departments.map(department => <option key={department.id} value={department.id}>{department.name}</option>)}
        </select></label>
      </div>
      <button className="button secondary justify-self-start" type="button" onClick={() => {
        setRules(items => items.filter((_, i) => i !== index)); setStatus("");
      }}>{t.remove}</button>
    </fieldset>)}
    <div className="form-actions flex-wrap">
      <button className="button secondary" type="button" disabled={busy || rules.length >= 30} onClick={() => {
        const priority = Array.from({ length: 1000 }, (_, i) => i + 1).find(value => !rules.some(rule => rule.priority === value))!;
        setRules(items => [...items, { priority, currency: initial.currency, minimum: 0, maximum: null,
          category: null, requesterRole: null, departmentId: null, route: "manager" }]); setStatus("");
      }}>{t.add}</button>
      <button className="button primary" disabled={busy} type="submit">{t.save}</button>
    </div>
    {status && <p role="status">{status}</p>}
    {error && <p role="alert" className="error-text">{error}</p>}
  </form>
    <form className="panel detail-panel stack-form" onSubmit={runPreview}>
      <h2>{t.preview}</h2>
      <div className="form-row">
        <label>{t.amount}<input name="amount" type="number" min=".01" step=".01" required /></label>
        <label>{t.category}<select name="category">{categories.map(category =>
          <option key={category} value={category}>{label(locale, category)}</option>)}</select></label>
        <label>{t.role}<select name="role" defaultValue="Employee">{roles.map(role =>
          <option key={role} value={role}>{org[role]}</option>)}</select></label>
        <label>{t.department}<select name="departmentId"><option value="">{t.any}</option>
          {departments.map(department => <option key={department.id} value={department.id}>{department.name}</option>)}
        </select></label>
      </div>
      <button className="button secondary" disabled={busy} type="submit">{t.preview}</button>
      {preview && <p role="status">{t[preview.route]} / {preview.priority === null ? t.fallback : `${t.priority} ${preview.priority}`}</p>}
    </form>
  </div>;
}
