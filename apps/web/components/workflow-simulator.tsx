"use client";

import { Download, Play } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent, type ReactNode } from "react";
import { graphText } from "../lib/purchase-graph";
import type { Locale } from "../lib/messages";
import { simulationText, type Decision, type SimulationCatalog, type SimulationInput, type SimulationRun } from "../lib/simulation";
import { PurchaseExecution } from "./purchase-execution";

export function WorkflowSimulator({ api, data, currency, locale, versionPaging, historyPaging }: {
  api: string; data: SimulationCatalog; currency: string; locale: Locale; versionPaging: ReactNode; historyPaging: ReactNode;
}) {
  const t = simulationText[locale];
  const graph = graphText[locale];
  const router = useRouter();
  const [version, setVersion] = useState<number | null>(null);
  const [amount, setAmount] = useState("");
  const [manager, setManager] = useState<Decision>("approved");
  const [finance, setFinance] = useState<Decision>("approved");
  const [scenario, setScenario] = useState("");
  const [run, setRun] = useState<SimulationRun>();
  const [comparison, setComparison] = useState<SimulationRun>();
  const [cursor, setCursor] = useState(-1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const resultHeading = useRef<HTMLHeadingElement>(null);
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "short", timeStyle: "medium" });
  const money = new Intl.NumberFormat(locale, { style: "currency", currency });
  const milliseconds = new Intl.NumberFormat(locale, { maximumFractionDigits: 2 });
  const versions = version && !data.versions.includes(version) ? [version, ...data.versions] : data.versions;

  function loadScenario(input: SimulationInput) {
    setVersion(input.version);
    setAmount(String(input.amount));
    setManager(input.manager);
    setFinance(input.finance);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      const response = await fetch(api, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ input: { version, revision: version === null ? data.revision : null, amount: Number(amount), manager, finance },
          scenarioName: scenario.trim() || null }) });
      if (response.status === 409) { setError(t.draftChanged); return; }
      if (response.status === 400) { setError(t.invalidInput); return; }
      if (!response.ok) throw new Error();
      setRun(await response.json() as SimulationRun);
      setCursor(-1);
      setScenario("");
      resultHeading.current?.focus();
      router.refresh();
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }

  async function loadRun(id: string, compare: boolean) {
    if (!id) { if (compare) setComparison(undefined); return; }
    setBusy(true);
    setError("");
    try {
      const response = await fetch(`${api}/runs/${id}`, { cache: "no-store" });
      if (!response.ok) throw new Error();
      const saved = await response.json() as SimulationRun;
      if (compare) setComparison(saved);
      else {
        setRun(saved);
        setCursor(-1);
        if (comparison?.id === saved.id) setComparison(undefined);
        loadScenario(saved.snapshot.input);
        resultHeading.current?.focus();
      }
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }

  function exportRun(current: SimulationRun) {
    const url = URL.createObjectURL(new Blob([JSON.stringify(current, null, 2)], { type: "application/json" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `flowops-simulation-${current.id}.json`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  const outcome = run?.snapshot.result;
  const trace = outcome?.steps ?? [];
  const visible = cursor < 0 ? trace : trace.slice(0, cursor + 1);
  const path = new Set((cursor < 0 ? trace : trace.slice(0, cursor))
    .filter(step => step.nextNodeId).map(step => [step.nodeId, step.nextNodeId, step.branch].join(":")));
  const runOption = (item: SimulationCatalog["runs"][number]) =>
    <option key={item.id} value={item.id}>{date.format(new Date(item.createdAt))} / {t[item.summary]}</option>;
  return <div className="simulation-grid">
      <section className="panel detail-panel settings-panel simulation-inputs">
        <h2>{t.inputs}</h2>
        <p className="muted">{t.isolated}</p>
        <form className="stack-form" onSubmit={submit}>
          <label>{t.source}<select value={version ?? ""} onChange={event => setVersion(Number(event.target.value) || null)}>
            <option value="">{t.draft} / {data.revision}</option>
            {versions.map(number => <option key={number} value={number}>{t.version} {number}</option>)}
          </select></label>
          {versionPaging}
          <label>{t.amount} ({currency})<input required type="number" min="0.01" max="999999999.99" step="0.01"
            value={amount} onChange={event => setAmount(event.target.value)} /></label>
          <label>{t.manager}<select value={manager} onChange={event => setManager(event.target.value as Decision)}>
            <option value="approved">{t.approved}</option><option value="rejected">{t.rejected}</option>
          </select></label>
          <label>{t.finance}<select value={finance} onChange={event => setFinance(event.target.value as Decision)}>
            <option value="approved">{t.approved}</option><option value="rejected">{t.rejected}</option>
          </select></label>
          <label>{t.scenario}<input maxLength={80} value={scenario} onChange={event => setScenario(event.target.value)} /></label>
          <button className="button primary" disabled={busy} type="submit"><Play size={16} aria-hidden />{busy ? t.running : t.run}</button>
          {error && <p className="error-text" role="alert">{error}</p>}
        </form>
      </section>
      <section className="panel detail-panel settings-panel simulation-history">
        <h2>{t.history}</h2>
        <div className="stack-form">
          <label>{t.scenarioLoad}<select disabled={!data.scenarios.length} defaultValue="" onChange={event => {
            const saved = data.scenarios.find(item => item.id === event.target.value);
            if (saved) loadScenario(saved.input);
            event.target.value = "";
          }}>
            <option value="">{t.none}</option>{data.scenarios.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}
          </select></label>
          {data.scenarios.some(item => item.input.version === null) && <p className="muted">{t.currentDraft}</p>}
          <label>{t.runHistory}<select disabled={busy || !data.runs.length} defaultValue="" onChange={event => void loadRun(event.target.value, false)}>
            <option value="">{t.none}</option>{data.runs.map(runOption)}
          </select></label>
          <label>{t.compare}<select disabled={busy || !run || !data.runs.length} defaultValue=""
            onChange={event => void loadRun(event.target.value, true)}>
            <option value="">{t.none}</option>{data.runs.filter(item => item.id !== run?.id).map(runOption)}
          </select></label>
        </div>
        {historyPaging}
      </section>
    <div className="detail-main simulation-output">
      <section className="panel detail-panel settings-panel">
        <div className="simulation-heading"><h2 tabIndex={-1} ref={resultHeading}>{t.results}</h2>
          {run && <button className="button secondary" onClick={() => exportRun(run)}><Download size={16} aria-hidden />{t.export}</button>}
        </div>
        {run && outcome ? <>
          <div role="status" className="simulation-summary">
            <span className="status" data-status={outcome.outcome}>{t[outcome.outcome]}</span>
            <span>{run.snapshot.input.version ? `${t.version} ${run.snapshot.input.version}` : `${t.draft} ${run.snapshot.input.revision}`}</span>
            <span>{money.format(run.snapshot.input.amount)}</span>
            <span>{milliseconds.format(outcome.durationMs)} {t.ms}</span>
          </div>
          {run.snapshot.graph.useApprovalMatrix && <p className="muted">{t.matrixScope}</p>}
          {outcome.issues.length > 0 && <ul className="simulation-issues">{outcome.issues.map(issue => <li key={issue.code + issue.nodeId}>
            {t[issue.code]}{issue.nodeId ? ` (${issue.nodeId})` : ""}
          </li>)}</ul>}
        </> : <p className="muted">{t.empty}</p>}
      </section>
      {run && outcome && <>
        <div className="simulation-graph"><PurchaseExecution key={run.id} execution={{ graph: run.snapshot.graph, nodes: visible.map(step => ({
          id: step.nodeId, completedAt: run.createdAt, status: "Completed",
        })) }} terminal={cursor < 0 && outcome.issues.length === 0} locale={locale}
          activeNode={cursor < 0 ? undefined : trace[cursor]?.nodeId} visitedEdges={path}
          failedNode={outcome.outcome === "failed" ? outcome.issues[0]?.nodeId ?? undefined : undefined} /></div>
        <section className="panel data-panel simulation-trace-panel">
          <div className="panel-heading simulation-heading"><h2>{t.trace}</h2>
            {trace.length > 0 && <label className="simulation-replay">{t.replay}<select value={cursor}
              onChange={event => setCursor(Number(event.target.value))}>
              <option value={-1}>{t.full}</option>{trace.map((step, index) => <option key={step.nodeId} value={index}>{graph[step.kind]}</option>)}
            </select></label>}
          </div>
          <ol className="simulation-trace">
            {visible.map(step => <li key={step.nodeId}>
              <strong>{graph[step.kind]}</strong>
              <span>{step.kind === "threshold" && run.snapshot.input.manager === "rejected" ? t.financeSkipped :
                step.decision ? t[step.decision] : step.branch ? graph[step.branch] :
                step.mockNotification ? t.mockNotification : graph.completed}
                {step.comparedAmount !== null && step.threshold !== null &&
                  <small>{money.format(step.comparedAmount)} / {t.threshold} {money.format(step.threshold)}</small>}
              </span>
              <span>{milliseconds.format(step.durationMs)} {t.ms}</span>
            </li>)}
          </ol>
        </section>
        {comparison && <section className="panel detail-panel settings-panel">
          <h2>{t.comparison}</h2>
          <div className="simulation-comparison">{([[t.current, run], [t.runHistory, comparison]] as const).map(([name, compared]) =>
            <div key={name}><h3>{name}</h3>
              <p>{t[compared.snapshot.result.outcome]} / {money.format(compared.snapshot.input.amount)}</p>
              <ol>{compared.snapshot.result.steps.map(step => <li key={step.nodeId}>{graph[step.kind]}</li>)}</ol>
            </div>)}</div>
        </section>}
      </>}
    </div>
  </div>;
}
