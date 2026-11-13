"use client";

import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { ArrowDown, ArrowUp, GripVertical, Plus } from "lucide-react";
import type { Locale } from "../lib/messages";
import { choiceFields, fieldTypes, formText, type StudioField, type StudioFormDetail, type StudioSchema } from "../lib/studio-forms";
import { StudioFormFields } from "./studio-form-fields";

export function StudioFormEditor({ api, href, locale, form, pagination }: {
  api: string; href: string; locale: Locale; form?: StudioFormDetail; pagination?: ReactNode;
}) {
  const t = formText[locale];
  const router = useRouter();
  const [draft, setDraft] = useState<StudioSchema>(form?.draft ?? { name: "", fields: [] });
  const [past, setPast] = useState<StudioSchema[]>([]);
  const [future, setFuture] = useState<StudioSchema[]>([]);
  const [revision, setRevision] = useState(form?.revision ?? 0);
  const [saved, setSaved] = useState(JSON.stringify(draft));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [narrow, setNarrow] = useState(false);
  const [preview, setPreview] = useState<Record<string, string>>({});
  const [previewStep, setPreviewStep] = useState(1);
  const steps = Math.max(1, ...draft.fields.map(field => field.step ?? 1));
  const [copied, setCopied] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const dirty = JSON.stringify(draft) !== saved;

  function change(next: StudioSchema) {
    setPast(items => [...items.slice(-29), draft]);
    setFuture([]);
    setDraft(next);
    if (error === t.invalid || error === t.order) setError("");
  }

  function move(from: number, to: number) {
    if (to < 0 || to >= draft.fields.length || from === to) return;
    const fields = [...draft.fields];
    const [field] = fields.splice(from, 1);
    fields.splice(to, 0, field);
    if (fields.some((item, index) => [item.whenKey, item.requiredWhenKey].some(key =>
      key && !fields.slice(0, index).some(previous => previous.key === key)))) {
      setError(t.order);
      return;
    }
    change({ ...draft, fields });
  }

  function update(key: string, field: Partial<StudioField>) {
    change({ ...draft, fields: draft.fields.map(item => item.key === key ? { ...item, ...field } : item) });
  }

  async function write(publish = false) {
    if (busy) return;
    setBusy(true);
    setError("");
    const schema = { ...draft, fields: draft.fields.map(field => ({ ...field,
      options: choiceFields.includes(field.type) ? field.options?.map(value => value.trim()).filter(Boolean) : null,
      fileExtensions: field.type === "file" ? field.fileExtensions?.map(value => value.trim()).filter(Boolean) || null : null,
    })) };
    try {
      const response = await fetch(form ? `${api}/${publish ? "publish" : "draft"}` : api, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(form ? { revision, schema: publish ? null : schema } : schema),
      });
      if (response.status === 409) { setError(t.conflict); return; }
      if (response.status === 400) { setError(t.invalid); return; }
      if (!response.ok) throw new Error();
      if (!form) {
        const result = await response.json() as { id: string };
        router.push(`${href}/${result.id}`);
      } else if (!publish) {
        const latest = await response.json() as { revision: number };
        setRevision(latest.revision);
        setSaved(JSON.stringify(draft));
      }
      router.refresh();
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }

  useEffect(() => {
    if (!form || !dirty || busy || error || draft.name.trim().length < 3 || draft.fields.some(field => !field.label.trim())) return;
    const timeout = window.setTimeout(() => {
      if (formRef.current?.checkValidity()) formRef.current.requestSubmit();
    }, 1200);
    return () => window.clearTimeout(timeout);
  }, [form, dirty, busy, error, draft]);

  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); void write(); }
  return <div className="simulation-grid studio-form-grid">
    <section className="panel detail-panel settings-panel">
      <form ref={formRef} className="stack-form" onSubmit={submit}>
        <label>{t.name}<input required minLength={3} maxLength={100} value={draft.name}
          onChange={event => change({ ...draft, name: event.target.value })} /></label>
        <div className="form-actions flex-wrap">
          <button className="button secondary" type="button" disabled={!past.length} onClick={() => {
            setFuture(items => [draft, ...items]); setDraft(past.at(-1)!); setPast(items => items.slice(0, -1));
          }}>{t.undo}</button>
          <button className="button secondary" type="button" disabled={!future.length} onClick={() => {
            setPast(items => [...items, draft]); setDraft(future[0]); setFuture(items => items.slice(1));
          }}>{t.redo}</button>
          <button className="button secondary" type="button" disabled={draft.fields.length >= 20} onClick={() =>
            change({ ...draft, fields: [...draft.fields, { key: crypto.randomUUID().replaceAll("-", ""), label: "", type: "text", required: false }] })}>
            <Plus size={16} aria-hidden />{t.add}</button>
        </div>
        {draft.fields.map((field, index) => <fieldset key={field.key} className="studio-field settings-panel"
          onDragOver={event => event.preventDefault()} onDrop={event => {
            event.preventDefault();
            const source = draft.fields.findIndex(item => item.key === event.dataTransfer.getData("text/plain"));
            if (source >= 0) move(source, index);
          }}>
          <legend>{field.label || t.label}</legend>
          <div className="form-actions flex-wrap">
            <span draggable onDragStart={event => event.dataTransfer.setData("text/plain", field.key)} aria-hidden><GripVertical size={19} /></span>
            <button className="button icon-button" type="button" aria-label={t.up} disabled={index === 0} onClick={() => move(index, index - 1)}><ArrowUp size={16} aria-hidden /></button>
            <button className="button icon-button" type="button" aria-label={t.down} disabled={index === draft.fields.length - 1} onClick={() => move(index, index + 1)}><ArrowDown size={16} aria-hidden /></button>
            <button className="button secondary" type="button" onClick={() => change({ ...draft, fields: draft.fields
              .filter(item => item.key !== field.key).map(item => ({
                ...item,
                ...(item.whenKey === field.key ? { whenKey: null, whenValue: null } : {}),
                ...(item.requiredWhenKey === field.key ? { requiredWhenKey: null, requiredWhenValue: null } : {}),
              })) })}>{t.remove}</button>
          </div>
          <label>{t.label}<input required maxLength={80} value={field.label} onChange={event => update(field.key, { label: event.target.value })} /></label>
          <label>{t.type}<select value={field.type} onChange={event => update(field.key, {
            type: event.target.value as StudioField["type"], options: choiceFields.includes(event.target.value) ? [] : null,
            required: ["heading", "description"].includes(event.target.value) ? false : field.required,
            defaultValue: null, minimum: null, maximum: null,
            requiredWhenKey: null, requiredWhenValue: null,
            fileLimitKiB: null, fileExtensions: null,
          })}>{fieldTypes.map(type => <option key={type} value={type}>{t[type]}</option>)}</select></label>
          {choiceFields.includes(field.type) && <label>{t.options}<textarea value={field.options?.join("\n") ?? ""}
            onChange={event => update(field.key, { options: event.target.value.split("\n") })} /></label>}
          {field.type === "file" && <div className="form-row">
            <label>{t.fileLimit}<input type="number" min="1" max="10240" value={field.fileLimitKiB ?? ""}
              onChange={event => update(field.key, { fileLimitKiB: Number(event.target.value) || null })} /></label>
            <label>{t.fileExtensions}<input value={field.fileExtensions?.join(",") ?? ""}
              onChange={event => update(field.key, { fileExtensions: event.target.value ? event.target.value.split(",") : null })} /></label>
          </div>}
          <label>{t.help}<input maxLength={180} value={field.help ?? ""} onChange={event => update(field.key, { help: event.target.value })} /></label>
          <label>{t.length}<input type="number" min="1" max="2000" value={field.maxLength ?? ""}
            onChange={event => update(field.key, { maxLength: Number(event.target.value) || null })} /></label>
          <label className="!flex items-center"><input type="checkbox" className="!w-auto" checked={!!field.halfWidth}
            onChange={event => update(field.key, { halfWidth: event.target.checked })} />{t.halfWidth}</label>
          <label>{t.step}<select value={field.step ?? 1} onChange={event => update(field.key, { step: Number(event.target.value) })}>
            {[1, 2, 3, 4, 5].map(step => <option key={step} value={step}>{step}</option>)}
          </select></label>
          {!["heading", "description"].includes(field.type) && <>
            <label className="!flex items-center"><input type="checkbox" className="!w-auto" checked={field.required}
              onChange={event => update(field.key, { required: event.target.checked })} />{t.required}</label>
            <details className="settings-panel"><summary>{t.pattern} / {t.defaultValue}</summary>
              <label>{t.placeholder}<input maxLength={120} value={field.placeholder ?? ""} onChange={event => update(field.key, { placeholder: event.target.value })} /></label>
              {!["user", "department", "file"].includes(field.type) && <label>{t.defaultValue}<input maxLength={2000}
                value={field.defaultValue ?? ""} onChange={event => update(field.key, { defaultValue: event.target.value || null })} /></label>}
              <label>{t.minLength}<input type="number" min="0" max="2000" value={field.minLength ?? ""}
                onChange={event => update(field.key, { minLength: event.target.value ? Number(event.target.value) : null })} /></label>
              <label>{t.pattern}<input maxLength={128} value={field.pattern ?? ""} onChange={event => update(field.key, { pattern: event.target.value || null })} /></label>
              <label>{t.requiredWhen}<select value={field.requiredWhenKey ?? ""} onChange={event => update(field.key, {
                requiredWhenKey: event.target.value || null, requiredWhenValue: event.target.value ? "" : null,
              })}><option value="">—</option>
                {draft.fields.slice(0, index).filter(item => !["heading", "description", "file"].includes(item.type)).map(item =>
                  <option key={item.key} value={item.key}>{item.label}</option>)}
              </select></label>
              {field.requiredWhenKey && <label>{t.equals}<input maxLength={100} value={field.requiredWhenValue ?? ""}
                onChange={event => update(field.key, { requiredWhenValue: event.target.value })} /></label>}
              {["number", "decimal", "currency"].includes(field.type) && <div className="form-row">
                {(["minimum", "maximum"] as const).map(key => <label key={key}>{t[key]}<input type="number" step="any" value={field[key] ?? ""}
                  onChange={event => update(field.key, { [key]: event.target.value ? Number(event.target.value) : null })} /></label>)}
              </div>}
            </details>
          </>}
          <label>{t.condition}<select value={field.whenKey ?? ""} onChange={event => update(field.key, {
            whenKey: event.target.value || null, whenValue: event.target.value ? "" : null,
          })}><option value="">{t.always}</option>
            {draft.fields.filter((item, position) => !["heading", "description", "file"].includes(item.type) &&
              (position < index || item.key === field.whenKey)).map(item =>
              <option key={item.key} value={item.key}>{item.label}</option>)}
          </select></label>
          {field.whenKey && <label>{t.equals}<input maxLength={100} value={field.whenValue ?? ""}
            onChange={event => update(field.key, { whenValue: event.target.value })} /></label>}
        </fieldset>)}
        <div className="form-actions flex-wrap">
          <button className="button primary" type="submit" disabled={busy}>{t.save}</button>
          {form && <button className="button secondary" type="button" disabled={busy || dirty || !draft.fields.length || form.publishedRevision === revision}
            onClick={() => void write(true)}>{t.publish}</button>}
        </div>
        {form && <p role="status" className="muted">{busy ? t.saving : dirty ? t.dirty : t.saved}</p>}
        {error && <p role="alert" className="error-text">{error}</p>}
      </form>
    </section>
    <div className="detail-main">
      <section className="panel detail-panel settings-panel">
        <div className="simulation-heading"><h2>{t.preview}</h2>
          <button type="button" className="button secondary" aria-pressed={narrow} onClick={() => setNarrow(!narrow)}>
            {narrow ? t.desktop : t.mobile}
          </button>
        </div>
        <div className={narrow ? "stack-form studio-narrow" : "stack-form"}>
          {steps > 1 && <label>{t.step}<select value={Math.min(previewStep, steps)} onChange={event => setPreviewStep(Number(event.target.value))}>
            {Array.from({ length: steps }, (_, index) => <option key={index + 1} value={index + 1}>{index + 1}</option>)}
          </select></label>}
          <StudioFormFields schema={draft} locale={locale} values={preview} step={Math.min(previewStep, steps)}
            onChange={(key, value) => setPreview(current => ({ ...current, [key]: value }))} />
        </div>
      </section>
      {form && <section className="panel detail-panel settings-panel">
        <h2>{t.history}</h2>
        <button type="button" className="button secondary justify-self-start" onClick={async () => {
          try { await navigator.clipboard.writeText(form.id); setCopied(true); }
          catch { setCopied(false); }
        }}>{copied ? t.copied : t.copy}</button>
        <ol className="simulation-trace">{form.versions.map(version => <li key={version.number}>
          <strong>{t.version} {version.number}</strong>
          <time dateTime={version.publishedAt}>{new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(new Date(version.publishedAt))}</time>
        </li>)}</ol>
        {pagination}
      </section>}
    </div>
  </div>;
}
