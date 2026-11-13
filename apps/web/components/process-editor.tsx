"use client";

import { useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "./ui/button";
import { processText, type ProcessDetail, type ProcessMember, type ProcessSpec, type ProcessText, type Step } from "../lib/processes";
import type { Locale } from "../lib/messages";
import { ProcessCanvas } from "./process-canvas";

function StepList({ steps, change, targets, members, t, nested = false }: {
  steps: Step[]; change: (steps: Step[]) => void; targets: string[]; members: ProcessMember[]; t: ProcessText; nested?: boolean;
}) {
  function move(index: number, offset: number) {
    const updated = [...steps];
    [updated[index], updated[index + offset]] = [updated[index + offset], updated[index]];
    change(updated);
  }
  return <div className="settings-panel">
    <ol className="decisions space-y-3">
      {steps.map((step, index) => {
        const update = (value: Step) => change(steps.map((entry, at) => at === index ? value : entry));
        return <li key={index} className="settings-panel">
          <h3>{t[step.kind]}</h3>
          {step.kind === "timer" && <label>{t.seconds}<input type="number" min={1} max={604800}
            required value={step.seconds || ""} onChange={event => update({ kind: "timer", seconds: event.target.valueAsNumber || 0 })} /></label>}
          {step.kind === "http" && <label>{t.target}<select value={step.target} required
            onChange={event => update({ kind: "http", target: event.target.value })}>
            <option value="">{t.select}</option>
            {targets.map(target => <option key={target}>{target}</option>)}
          </select></label>}
          {step.kind === "assignment" && <div className="form-row">
            <label>{t.taskTitle}<input value={step.title} required maxLength={100}
              onChange={event => update({ ...step, title: event.target.value })} /></label>
            <label>{t.assignee}<select value={step.assignee} required onChange={event => update({ ...step, assignee: event.target.value })}>
              <option value="">{t.selectAssignee}</option>
              {members.map(member => <option key={member.subject} value={member.subject}>{member.email ?? member.subject}</option>)}
            </select></label>
          </div>}
          {step.kind === "parallel" && <div className="grid gap-3">
            {step.branches.map((branch, branchIndex) => <fieldset key={branchIndex} className="min-w-0 border border-[var(--line)] p-3">
              <legend>{t.branch} {branchIndex + 1}</legend>
              <StepList steps={branch} targets={targets} members={members} t={t} nested
                change={value => update({ kind: "parallel", branches: step.branches.map((entry, at) => at === branchIndex ? value : entry) })} />
              {step.branches.length > 2 && <Button type="button" variant="secondary" className="mt-2"
                aria-label={`${t.remove} ${t.branch} ${branchIndex + 1}`}
                onClick={() => update({ kind: "parallel", branches: step.branches.filter((_, at) => at !== branchIndex) })}>
                {t.remove}
              </Button>}
            </fieldset>)}
            {step.branches.length < 4 && <Button type="button" variant="secondary" className="justify-self-start"
              onClick={() => update({ kind: "parallel", branches: [...step.branches, [{ kind: "timer", seconds: 1 }]] })}>{t.addBranch}</Button>}
          </div>}
          <div className="form-actions flex-wrap">
            {index > 0 && <Button type="button" variant="secondary" onClick={() => move(index, -1)}>{t.earlier}</Button>}
            {index < steps.length - 1 && <Button type="button" variant="secondary" onClick={() => move(index, 1)}>{t.later}</Button>}
            {steps.length > 1 && <Button type="button" variant="secondary"
              onClick={() => change(steps.filter((_, at) => at !== index))}>{t.remove}</Button>}
          </div>
        </li>;
      })}
    </ol>
    {steps.length < (nested ? 8 : 12) && <div className="form-actions flex-wrap">
      <Button type="button" variant="secondary" onClick={() => change([...steps, { kind: "timer", seconds: 1 }])}>{t.addTimer}</Button>
      <Button type="button" variant="secondary" disabled={!targets.length}
        onClick={() => change([...steps, { kind: "http", target: targets[0] }])}>{t.addHttp}</Button>
      <Button type="button" variant="secondary" onClick={() => change([...steps, { kind: "assignment", title: "", assignee: "" }])}>
        {t.addAssignment}
      </Button>
      {!nested && <Button type="button" variant="secondary"
        onClick={() => change([...steps, { kind: "parallel", branches: [[{ kind: "timer", seconds: 1 }], [{ kind: "timer", seconds: 1 }]] }])}>
        {t.addParallel}
      </Button>}
    </div>}
  </div>;
}

async function submit(path: string, body: unknown) {
  const response = await fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
  if (response.status === 401) location.assign(new URL("/auth/login", location.origin));
  if (!response.ok) {
    const issue = await response.json().catch(() => ({})) as { code?: string };
    throw new Error(issue.code ?? "error");
  }
  return response;
}

function message(t: ProcessText, error: unknown) {
  const key = error instanceof Error ? error.message : "";
  return Object.hasOwn(t, key) ? t[key as keyof ProcessText] : t.error;
}

export function ProcessEditor({ api, href, locale, targets, members, process }: {
  api: string; href: string; locale: Locale; targets: string[]; members: ProcessMember[]; process?: ProcessDetail;
}) {
  const router = useRouter();
  const t = processText[locale];
  const [spec, setSpec] = useState<ProcessSpec>(process?.draft ?? {
    name: "", trigger: "manual", fields: [], steps: [{ kind: "timer", seconds: 1 }], attachment: "none"
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const dirty = JSON.stringify(spec) !== JSON.stringify(process?.draft);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setStatus("");
    try {
      if (process) {
        await submit(`${api}/draft`, { revision: process.revision, spec });
        setStatus(t.saved);
        router.refresh();
      } else {
        const { id } = await (await submit(api, spec)).json();
        router.push(`${href}/${id}`);
      }
    } catch (failure) { setError(message(t, failure)); }
    finally { setBusy(false); }
  }

  async function publish() {
    if (!process) return;
    setBusy(true);
    setError("");
    setStatus("");
    try {
      await submit(`${api}/publish`, { revision: process.revision });
      setStatus(t.published);
      router.refresh();
    } catch (failure) { setError(message(t, failure)); }
    finally { setBusy(false); }
  }

  return <section className="panel detail-panel">
    <form className="stack-form" onSubmit={save}>
      <label>{t.name}<input required minLength={3} maxLength={100} value={spec.name}
        onChange={event => setSpec({ ...spec, name: event.target.value })} /></label>
      <div className="form-row">
        <label>{t.trigger}<select value={spec.trigger} onChange={event => setSpec({ ...spec, trigger: event.target.value as ProcessSpec["trigger"] })}>
          <option value="manual">{t.manual}</option><option value="webhook">{t.webhook}</option>
        </select></label>
        <label>{t.attachment}<select name="attachmentMode" value={spec.attachment}
          onChange={event => setSpec({ ...spec, attachment: event.target.value as ProcessSpec["attachment"] })}>
          {(["none", "optional", "required"] as const).map(value => <option key={value} value={value}>{t[value]}</option>)}
        </select></label>
      </div>
      <section className="settings-panel">
        <h2>{t.fields}</h2>
        {spec.fields.map((field, index) => {
          const update = (value: Partial<typeof field>) =>
            setSpec({ ...spec, fields: spec.fields.map(item => item.key === field.key ? { ...item, ...value } : item) });
          return <div key={field.key} className="settings-panel">
            <div className="form-row">
              <label>{t.label} {index + 1}<input required maxLength={80} value={field.label}
                onChange={event => update({ label: event.target.value })} /></label>
              <label>{t.type}<select value={field.type} onChange={event => update({ type: event.target.value as typeof field.type })}>
                {(["text", "number", "date"] as const).map(type => <option key={type} value={type}>{t[type]}</option>)}
              </select></label>
            </div>
            <div className="form-actions flex-wrap">
              <label className="!flex"><input type="checkbox" className="!w-auto" checked={field.required}
                onChange={event => update({ required: event.target.checked })} />{t.required}</label>
              <Button type="button" variant="secondary" onClick={() => setSpec({ ...spec, fields: spec.fields.filter(item => item.key !== field.key) })}>{t.remove}</Button>
            </div>
          </div>;
        })}
        {spec.fields.length < 12 && <Button type="button" variant="secondary" className="justify-self-start"
          onClick={() => setSpec({ ...spec, fields: [...spec.fields, {
            key: crypto.randomUUID().replaceAll("-", ""), label: "", type: "text", required: false
          }] })}>{t.addField}</Button>}
      </section>
      <ProcessCanvas steps={spec.steps} layout={spec.layout} change={layout => setSpec(current => ({ ...current, layout }))} t={t} />
      <section className="settings-panel">
        <h2>{t.steps}</h2>
        {!targets.length && <p className="muted">{t.noTargets}</p>}
        <StepList steps={spec.steps} change={steps => setSpec({ ...spec, steps })} targets={targets} members={members} t={t} />
      </section>
      <div className="form-actions flex-wrap">
        <Button type="submit" disabled={busy}>{t.save}</Button>
        {process && <Button type="button" variant="secondary" disabled={busy || dirty} onClick={publish}>{t.publish}</Button>}
      </div>
      {process && dirty && <p className="muted">{t.saveFirst}</p>}
      {status && <p role="status">{status}</p>}
      {error && <p role="alert" className="error-text">{error}</p>}
    </form>
  </section>;
}

export function ProcessRunForm({ api, locale, process }: { api: string; locale: Locale; process: ProcessDetail }) {
  const router = useRouter();
  const requestId = useRef<string | null>(null);
  const t = processText[locale];
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [started, setStarted] = useState(false);
  const spec = process.published!;

  async function run(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setStarted(false);
    const form = event.currentTarget;
    try {
      const values = new FormData(form);
      const file = values.get("attachment");
      let attachment;
      if (file instanceof File && file.name) {
        if (file.size === 0 || file.size > process.attachmentLimit) throw new Error("invalidAttachment");
        const content = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => resolve(String(reader.result).split(",", 2)[1]);
          reader.onerror = () => reject(new Error("invalidAttachment"));
          reader.readAsDataURL(file);
        });
        attachment = { name: file.name, content };
      }
      requestId.current ??= crypto.randomUUID();
      await submit(`${api}/run`, {
        id: requestId.current, version: process.version,
        answers: Object.fromEntries(spec.fields.map(field => [field.key, values.get(field.key)])), attachment
      });
      requestId.current = null;
      form.reset();
      setStarted(true);
      router.refresh();
    } catch (failure) { setError(message(t, failure)); }
    finally { setBusy(false); }
  }

  return <form className="stack-form" onSubmit={run} onChange={() => { requestId.current = null; }}>
    {spec.fields.map(field => <label key={field.key}>{field.label}
      <input name={field.key} type={field.type} required={field.required}
        step={field.type === "number" ? "any" : undefined} maxLength={field.type === "text" ? 2000 : undefined} /></label>)}
    {spec.attachment !== "none" && <label>{t.attachment} ({process.attachmentLimit / 1048576} MiB)
      <input type="file" name="attachment" required={spec.attachment === "required"} /></label>}
    <Button type="submit" disabled={busy}>{t.run}</Button>
    {started && <p role="status">{t.started}</p>}
    {error && <p role="alert" className="error-text">{error}</p>}
  </form>;
}

export function CompleteProcessTask({ path, locale }: { path: string; locale: Locale }) {
  const router = useRouter();
  const t = processText[locale];
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return <div>
    <Button type="button" variant="secondary" disabled={busy} onClick={async () => {
      setBusy(true);
      setError("");
      try {
        await submit(path, {});
        router.refresh();
      } catch (failure) { setError(message(t, failure)); }
      finally { setBusy(false); }
    }}>{t.completeTask}</Button>
    {error && <p role="alert" className="error-text">{error}</p>}
  </div>;
}
