"use client";

import type { Locale } from "../lib/messages";
import { formText, type StudioChoices, type StudioSchema } from "../lib/studio-forms";

export function visibleFormAnswers(schema: StudioSchema, values: Record<string, string>) {
  const accepted: Record<string, string> = Object.create(null);
  for (const field of schema.fields) {
    if (["heading", "description", "file"].includes(field.type)) continue;
    if (!field.whenKey || accepted[field.whenKey] === field.whenValue)
      accepted[field.key] = Object.hasOwn(values, field.key) ? values[field.key].trim() : field.defaultValue ?? "";
  }
  return accepted;
}

export function StudioFormFields({ schema, locale, values, onChange, choices, step }: {
  schema: StudioSchema; locale: Locale; values: Record<string, string>; onChange: (key: string, value: string) => void;
  choices?: StudioChoices;
  step?: number;
}) {
  const t = formText[locale];
  const visible = visibleFormAnswers(schema, values);
  return <div className="studio-answer-grid">{schema.fields.filter(field =>
    (!step || (field.step ?? 1) === step) && (!field.whenKey || visible[field.whenKey] === field.whenValue)).map(field =>
    <div key={field.key} className={field.halfWidth ? "studio-answer-half" : undefined}>
      {renderField(field)}
    </div>)}</div>;

  function renderField(field: StudioSchema["fields"][number]) {
    const required = field.required || !!field.requiredWhenKey && visible[field.requiredWhenKey] === field.requiredWhenValue;
    if (field.type === "file") return <label>{field.label}<input type="file" name="attachment" required={required}
      accept={field.fileExtensions?.map(extension => `.${extension}`).join(",")} />
      {field.help && <small className="muted">{field.help}</small>}
    </label>;
    if (field.type === "heading") return <h3 key={field.key}>{field.label}</h3>;
    if (field.type === "description") return <p key={field.key}>{field.label}</p>;
    const value = Object.hasOwn(values, field.key) ? values[field.key] : field.defaultValue ?? "";
    if (field.type === "dateRange") {
      const [from = "", to = ""] = value.split("/");
      return <fieldset className="form-row" key={field.key}><legend>{field.label}</legend>
        <label>{t.from}<input type="date" required={required || !!to} value={from}
          max={to || undefined} onChange={event => onChange(field.key, event.target.value || to ? `${event.target.value}/${to}` : "")} /></label>
        <label>{t.to}<input type="date" required={required || !!from} value={to}
          min={from || undefined} onChange={event => onChange(field.key, from || event.target.value ? `${from}/${event.target.value}` : "")} /></label>
      </fieldset>;
    }
    if (field.type === "radio") return <fieldset className="settings-panel" key={field.key}><legend>{field.label}</legend>
      {field.options?.map(option => <label className="!flex items-center" key={option}>
        <input className="!w-auto" type="radio" name={`studio-choice-${field.key}`} value={option} checked={value === option}
          required={required} onChange={() => onChange(field.key, option)} />{option}
      </label>)}
    </fieldset>;
    if (field.type === "multiSelect") {
      let selections: string[] = [];
      try { const decoded: unknown = JSON.parse(value); if (Array.isArray(decoded)) selections = decoded; } catch { /* Empty or incomplete default. */ }
      return <label key={field.key}>{field.label}<select multiple required={required} value={selections}
        onChange={event => { const next = Array.from(event.target.selectedOptions, item => item.value);
          onChange(field.key, next.length ? JSON.stringify(next) : ""); }}>
        {field.options?.map(option => <option key={option}>{option}</option>)}
      </select></label>;
    }
    const props = {
      value,
      required,
      onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
        onChange(field.key, event.target.value),
    };
    return <label key={field.key}>{field.label}
      {field.type === "textarea" ? <textarea {...props} placeholder={field.placeholder ?? undefined} minLength={field.minLength ?? undefined} maxLength={field.maxLength ?? 2000} /> :
        field.type === "user" || field.type === "department" ? <select {...props} disabled={!choices}>
          <option value="">{t.selectOption}</option>
          {(field.type === "user" ? choices?.users : choices?.departments)?.map(option =>
            <option key={option.value} value={option.value}>{option.label}</option>)}
        </select> : field.type === "select" || field.type === "checkbox" || field.type === "toggle" ? <select {...props}>
          <option value="">{t.selectOption}</option>
          {field.type !== "select" ? <><option value="true">{t.yes}</option><option value="false">{t.no}</option></> :
            field.options?.map(option => <option key={option}>{option}</option>)}
        </select> : <input {...props} placeholder={field.placeholder ?? undefined}
          minLength={field.minLength ?? undefined} maxLength={field.maxLength ?? 2000}
          min={field.minimum ?? undefined} max={field.maximum ?? undefined}
          type={field.type === "phone" ? "tel" : field.type === "decimal" || field.type === "currency" ? "number" : field.type}
          step={field.type === "decimal" ? "any" : field.type === "currency" ? "0.01" : undefined} />}
      {field.help && <small className="muted">{field.help}</small>}
    </label>;
  }
}
