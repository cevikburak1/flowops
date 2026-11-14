"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { label, type Locale } from "../lib/messages";
import { formText, type StudioSchema } from "../lib/studio-forms";
import { graphText, type PurchaseGraph } from "../lib/purchase-graph";
import { PurchaseExecution } from "./purchase-execution";

const copy = {
  en: {
    heading: "Workflow Copilot", forms: "Generate a form with Copilot", analytics: "Ask about workflow performance",
    failure: "Analyze failed run", prompt: "Your request", privacy: "Leave out personal or confidential information.",
    edit: "Propose an edit", explain: "Explain workflow", optimize: "Suggest improvements", run: "Ask Copilot",
    busy: "Working…", unavailable: "Copilot is unavailable. Ask your operator to check the provider settings.",
    error: "The request could not be completed. Review your prompt and try again.", limit: "Copilot usage limit reached.",
    save: "Save as form draft", apply: "Apply to workflow draft", discard: "Discard", history: "Previous questions",
    draft: "Current draft", version: "Workflow version", published: "Published version", window: "Period (days)",
    changed: "The workflow changed. Generate a new proposal before applying it.", applied: "Draft updated.",
    threshold: "Finance threshold", name: "Workflow name", requests: "Requests this month",
    graph: "Proposed workflow", notification: "Add a notification on the connection",
    action: "Copilot action",
    savedQuestions: "Saved questions", saveQuestion: "Save this question", saveProposal: "Save proposal", clearQuestions: "Clear saved questions",
    retention: "Questions expire after", daysUnit: "days", none: "No saved questions.",
    saveExchange: "Save question and answer", readAnswer: "Read saved answer",
    processes: "Process runs", purchases: "Purchase requests", overdue: "Overdue approvals now",
    timing: "Mean minutes from submission to a decision", sample: "Decisions measured",
    input: "Reported input tokens", output: "Reported output tokens", cost: "Estimated known cost (USD)",
  },
  tr: {
    heading: "İş Akışı Copilot", forms: "Copilot ile form oluştur", analytics: "İş akışı performansını sor",
    failure: "Başarısız çalışmayı incele", prompt: "İsteğin", privacy: "Kişisel veya gizli bilgi ekleme.",
    edit: "Düzenleme öner", explain: "Akışı açıkla", optimize: "İyileştirme öner", run: "Copilot’a sor",
    busy: "İşleniyor…", unavailable: "Copilot kullanılamıyor. Sağlayıcı ayarlarını sistem yöneticisine kontrol ettir.",
    error: "İstek tamamlanamadı. İstemi kontrol edip yeniden dene.", limit: "Copilot kullanım sınırına ulaşıldı.",
    save: "Form taslağı olarak kaydet", apply: "İş akışı taslağına uygula", discard: "Kaldır", history: "Önceki sorular",
    draft: "Mevcut taslak", version: "İş akışı sürümü", published: "Yayınlanan sürüm", window: "Dönem (gün)",
    changed: "İş akışı değişti. Uygulamadan önce yeni bir öneri oluştur.", applied: "Taslak güncellendi.",
    threshold: "Finans eşiği", name: "İş akışı adı", requests: "Bu ayki istekler",
    graph: "Önerilen iş akışı", notification: "Bağlantıya bildirim ekle",
    action: "Copilot işlemi",
    savedQuestions: "Kaydedilen sorular", saveQuestion: "Bu soruyu kaydet", saveProposal: "Öneriyi kaydet", clearQuestions: "Kaydedilen soruları sil",
    retention: "Soruların saklanma süresi", daysUnit: "gün", none: "Kayıtlı soru yok.",
    saveExchange: "Soruyu ve yanıtı kaydet", readAnswer: "Kayıtlı yanıtı oku",
    processes: "Süreç çalışmaları", purchases: "Satın alma talepleri", overdue: "Şu an geciken onaylar",
    timing: "Gönderimden karara ortalama dakika", sample: "Ölçülen karar sayısı",
    input: "Bildirilen girdi token’ları", output: "Bildirilen çıktı token’ları", cost: "Bilinen tahmini maliyet (USD)",
  },
};

type Metric = { status: string; count: number };
type Edit = { name: string; financeThreshold: number; graph: PurchaseGraph };
type Result = {
  answer?: string; draft?: StudioSchema | Edit; before?: Edit;
  notifyOn?: { source: string; target: string; branch: string | null };
  evidence?: { runs?: Metric[]; purchases?: Metric[]; overdueNow?: number;
    approvalTiming?: { count: number; averageMinutesSinceSubmission: number } | null };
};
type Status = { enabled: boolean; requests: number; monthlyLimit: number;
  inputTokens?: number; outputTokens?: number; estimatedCostUsd?: number | null };

export function CopilotTools({ organizationId, locale, kind, resourceId, versions = [], autoOpen }: {
  organizationId: string; locale: Locale; kind: "workflow" | "forms" | "analytics" | "failure";
  resourceId?: string; versions?: number[]; autoOpen?: boolean;
}) {
  const t = copy[locale];
  const router = useRouter();
  const api = "/api/organizations/" + organizationId;
  const endpoint = api + "/copilot/v1";
  const [status, setStatus] = useState<Status>();
  const [result, setResult] = useState<Result>();
  const [message, setMessage] = useState("");
  const [limits, setLimits] = useState<string[]>([]);
  const [pending, setPending] = useState(false);
  const [prompt, setPrompt] = useState("");
  const [mode, setMode] = useState<"edit" | "explain" | "optimize">("explain");
  const [version, setVersion] = useState("");
  const [days, setDays] = useState(30);
  const [history, setHistory] = useState<string[]>([]);
  const [saved, setSaved] = useState<{ retentionDays: number; items: { id: string; text: string; answer?: string }[] }>();
  const [exchange, setExchange] = useState<{ prompt: string; answer: string }>();
  const [historyBusy, setHistoryBusy] = useState(false);
  const historyPath = endpoint + "/workflows/" + resourceId + "/history";
  const heading = kind === "workflow" ? t.heading : t[kind];
  const schema = kind === "forms" ? result?.draft as StudioSchema | undefined : undefined;
  const edit = result?.draft && "financeThreshold" in result.draft ? result.draft : undefined;
  function nodeLabel(id: string) {
    const node = result?.before?.graph.nodes.find(node => node.id === id);
    return node ? graphText[locale][node.kind] : id;
  }
  async function readStatus() {
    const response = await fetch(endpoint + "/status", { cache: "no-store" });
    if (!response.ok) throw new Error();
    setStatus(await response.json());
  }
  async function readSaved() {
    const response = await fetch(historyPath, { cache: "no-store" });
    if (!response.ok) throw new Error();
    setSaved(await response.json());
  }
  async function updateSaved(method: "POST" | "DELETE") {
    setHistoryBusy(true);
    setMessage("");
    try {
      const response = await fetch(historyPath, method === "POST" ? {
        method, headers: { "Content-Type": "application/json" },
        body: JSON.stringify(exchange?.prompt === prompt ? exchange : { prompt,
          answer: result?.draft ? JSON.stringify(result.draft) : undefined }),
      } : { method });
      if (!response.ok) throw new Error();
      await readSaved();
    } catch { setMessage(t.error); }
    finally { setHistoryBusy(false); }
  }
  async function ask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const path = kind === "workflow" ? "/workflows/" + resourceId + "/" + mode :
      kind === "forms" ? "/form-proposals" : kind === "failure" ? "/process-failures/" + resourceId : "/analytics";
    const input = kind === "workflow" ? { prompt, version: version ? Number(version) : null, history } :
      kind === "analytics" ? { prompt, days } : { prompt };
    setPending(true);
    setResult(undefined);
    setMessage("");
    setLimits([]);
    try {
      const response = await fetch(endpoint + path, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(input),
      });
      const data = await response.json();
      if (!response.ok) {
        setMessage(response.status === 429 ? t.limit : response.status === 503 ? t.unavailable : t.error);
        if (Array.isArray(data.limitations)) setLimits(data.limitations);
      } else {
        setResult(data);
        setExchange(typeof data.answer === "string" ? { prompt, answer: data.answer } : undefined);
        if (kind === "workflow") setHistory(previous => [...previous, prompt.trim().slice(0, 300)].slice(-4));
      }
      await readStatus();
    } catch { setMessage(t.error); }
    finally { setPending(false); }
  }
  async function apply() {
    if (!result?.draft) return;
    const path = kind === "forms" ? "/forms" : "/workflows/" + resourceId + "/draft";
    setPending(true);
    setMessage("");
    try {
      const response = await fetch(api + path, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(result.draft),
      });
      if (!response.ok) {
        setMessage(response.status === 409 ? t.changed : t.error);
        return;
      }
      setResult(undefined);
      if (kind === "forms") router.push("/workspace/" + organizationId + "/forms/" + (await response.json()).id);
      else { setMessage(t.applied); router.refresh(); }
    } catch { setMessage(t.error); }
    finally { setPending(false); }
  }
  return <details id={kind === "forms" ? "form-copilot" : kind === "failure" ? undefined : kind + "-copilot"}
    open={autoOpen} className="panel detail-panel mb-4"
    onToggle={event => { if (event.currentTarget.open && !status) void readStatus().catch(() => setMessage(t.error)); }}>
    <summary>{heading}</summary>
    {status && <p className="muted mt-2">{t.requests} {status.requests} / {status.monthlyLimit}</p>}
    {status && kind === "analytics" && <dl className="facts mt-2">
      <div><dt>{t.input}</dt><dd>{status.inputTokens ?? "—"}</dd></div>
      <div><dt>{t.output}</dt><dd>{status.outputTokens ?? "—"}</dd></div>
      <div><dt>{t.cost}</dt><dd>{status.estimatedCostUsd ?? "—"}</dd></div>
    </dl>}
    {status?.enabled === false && <p role="status" className="mt-2">{t.unavailable}</p>}
    {status?.enabled && <form className="stack-form mt-4" onSubmit={ask}>
      {kind === "workflow" && <div className="form-row">
        <label>{t.action}<select aria-label={t.action} value={mode} onChange={event => { setMode(event.target.value as typeof mode); setResult(undefined); }}>
          <option value="explain">{t.explain}</option><option value="edit">{t.edit}</option><option value="optimize">{t.optimize}</option>
        </select></label>
        <label>{t.version}<select value={version} onChange={event => { setVersion(event.target.value); setResult(undefined); setHistory([]); }}>
          <option value="">{t.draft}</option>
          {versions.map(value => <option key={value} value={value}>{t.published} {value}</option>)}
        </select></label>
      </div>}
      {kind === "analytics" && <label>{t.window}<select value={days} onChange={event => setDays(Number(event.target.value))}>
        {[7, 30, 90].map(value => <option key={value}>{value}</option>)}</select></label>}
      <label>{t.prompt}<textarea required minLength={10} maxLength={2000} rows={3} value={prompt}
        onChange={event => setPrompt(event.target.value)} /></label>
      <p className="muted">{t.privacy}</p>
      <button type="submit" className="button primary" disabled={pending}>{pending ? t.busy : t.run}</button>
    </form>}
    {message && <p role="status" className="mt-2">{message}</p>}
    {limits.length > 0 && <ul className="decisions">{limits.map((value, index) => <li key={index}>{value}</li>)}</ul>}
    {history.length > 0 && <details className="mt-4"><summary>{t.history}</summary>
      <ol className="decisions">{history.map((value, index) => <li className="break-words" key={index}>{value}</li>)}</ol>
    </details>}
    {kind === "workflow" && <details className="mt-4"
      onToggle={event => { if (event.currentTarget.open) void readSaved().catch(() => setMessage(t.error)); }}>
      <summary>{t.savedQuestions}</summary>
      {saved && <>
        {saved.retentionDays > 0 && <p className="muted">{t.retention} {saved.retentionDays} {t.daysUnit}</p>}
        {saved.items.length ? <ul className="decisions">{saved.items.map(item => <li key={item.id}>
          <button type="button" className="panel-link break-all text-left" onClick={() => setPrompt(item.text)}>{item.text}</button>
          {item.answer && <details><summary>{t.readAnswer}</summary><p className="whitespace-pre-wrap break-words">{item.answer}</p></details>}
        </li>)}</ul> : <p className="muted">{t.none}</p>}
        <div className="form-actions flex-wrap mt-2">
          {saved.retentionDays > 0 && status?.enabled && <button type="button" className="button secondary"
            disabled={historyBusy || prompt.trim().length < 10} onClick={() => void updateSaved("POST")}>
            {exchange?.prompt === prompt ? t.saveExchange : result?.draft ? t.saveProposal : t.saveQuestion}
          </button>}
          {saved.items.length > 0 && <button type="button" className="button secondary" disabled={historyBusy}
            onClick={() => void updateSaved("DELETE")}>{t.clearQuestions}</button>}
        </div>
      </>}
    </details>}
    {result?.answer && <p className="mt-4 whitespace-pre-wrap break-words">{result.answer}</p>}
    {result?.evidence?.runs && <div className="form-row mt-4">
      {[[t.processes, result.evidence.runs], [t.purchases, result.evidence.purchases ?? []]].map(([caption, values]) =>
        <div key={caption as string}><h3>{caption as string}</h3><ul className="decisions">{(values as Metric[]).map(row => <li key={row.status}>
          {label(locale, row.status)} <meter className="mx-2" min={0} max={Math.max(1, ...(values as Metric[]).map(value => value.count))}
            value={row.count} aria-label={label(locale, row.status)} /> {row.count}</li>)}</ul></div>)}
      <p>{t.overdue} {result.evidence.overdueNow}</p>
      {result.evidence.approvalTiming && <dl className="facts">
        <div><dt>{t.timing}</dt><dd>{new Intl.NumberFormat(locale, { maximumFractionDigits: 1 })
          .format(result.evidence.approvalTiming.averageMinutesSinceSubmission)}</dd></div>
        <div><dt>{t.sample}</dt><dd>{result.evidence.approvalTiming.count}</dd></div>
      </dl>}
    </div>}
    {schema && <div className="mt-4"><h3 className="break-words">{schema.name}</h3>
      <ol className="decisions">{schema.fields.map(field => <li key={field.key} className="break-words">
        <strong>{field.label}</strong> <span className="muted">{formText[locale][field.type]}</span>
        {field.help && <p>{field.help}</p>}
      </li>)}</ol>
    </div>}
    {result?.before && edit && <div className="stack-form mt-4">
      <dl className="facts">
        <div><dt>{t.name}</dt><dd className="break-words">{result.before.name} → {edit.name}</dd></div>
        <div><dt>{t.threshold}</dt><dd>{result.before.financeThreshold} → {edit.financeThreshold}</dd></div>
      </dl>
      {result.notifyOn && <p>{t.notification} {nodeLabel(result.notifyOn.source)} → {nodeLabel(result.notifyOn.target)}
        {result.notifyOn.branch && <> ({graphText[locale][result.notifyOn.branch as "approved" | "rejected" | "above" | "below"]})</>}
      </p>}
      <h3>{t.graph}</h3>
      <PurchaseExecution locale={locale} terminal={false} execution={{ graph: edit.graph, nodes: [] }}
        activeNode={result.notifyOn ? edit.graph.nodes.find(node => node.kind === "notify")?.id : undefined} />
    </div>}
    {result?.draft && <div className="form-actions mt-4 flex-wrap">
      <button type="button" className="button primary" disabled={pending} onClick={apply}>{kind === "forms" ? t.save : t.apply}</button>
      <button type="button" className="button secondary" disabled={pending} onClick={() => setResult(undefined)}>{t.discard}</button>
    </div>}
  </details>;
}

