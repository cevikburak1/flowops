"use client";

import { Background, Controls, MiniMap, ReactFlow, applyNodeChanges, MarkerType, Position, type Node, type OnNodesChange } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { useState, type FormEvent } from "react";
import { Button } from "./ui/button";
import type { Locale } from "../lib/messages";
import { graphText, isCompleteGraph, purchaseNodeKinds, purchaseTemplate, type PurchaseBranch, type PurchaseGraph, type PurchaseNodeKind } from "../lib/purchase-graph";

export function PurchaseGraphEditor({ graph, onChange, locale }: {
  graph: PurchaseGraph; onChange: (graph: PurchaseGraph) => void; locale: Locale;
}) {
  const t = graphText[locale];
  const [source, setSource] = useState("");
  const [target, setTarget] = useState("");
  const [branch, setBranch] = useState<PurchaseBranch>("above");
  const [selected, setSelected] = useState<string | null>(null);
  const [fitKey, setFitKey] = useState(0);
  const nodes: Node[] = graph.nodes.map(node => ({
    id: node.id, data: { label: t[node.kind] }, position: { x: node.x, y: node.y },
    sourcePosition: Position.Right, targetPosition: Position.Left,
    selected: selected === node.id, ariaLabel: t[node.kind],
    type: node.kind === "start" ? "input" : node.kind === "end" ? "output" : "default",
  }));
  const sourceNode = graph.nodes.find(node => node.id === source);
  const targetNode = graph.nodes.find(node => node.id === target);
  const options: PurchaseBranch[] = sourceNode?.kind === "threshold" ? ["above", "below"] :
    sourceNode?.kind === "decision" ? ["approved", "rejected"] : [];
  const currentBranch = options.includes(branch) ? branch : options[0] ?? null;
  const connectable = sourceNode && targetNode && canConnect(source, target, currentBranch);
  const edgeId = (edge: PurchaseGraph["edges"][number]) => [edge.source, edge.target, edge.branch].join(":");

  function canConnect(from: string, to: string, path: PurchaseBranch | null) {
    return from !== to && graph.edges.length < 8 &&
      !graph.edges.some(edge => edge.source === from && edge.target === to && edge.branch === path);
  }

  function addConnection(from: string, to: string, path: PurchaseBranch | null) {
    if (canConnect(from, to, path))
      onChange({ ...graph, edges: [...graph.edges, { source: from, target: to, branch: path }] });
  }

  const move: OnNodesChange = changes => {
    for (const change of changes)
      if (change.type === "select" && change.selected) setSelected(change.id);
    const positions = applyNodeChanges(changes.filter(change => change.type === "position"), nodes);
    if (!changes.some(change => change.type === "position")) return;
    onChange({ ...graph, nodes: graph.nodes.map((node, index) => ({
      ...node, x: positions[index].position.x, y: positions[index].position.y,
    })) });
  };

  function removeNode(id: string) {
    onChange({ ...graph, nodes: graph.nodes.filter(node => node.id !== id),
      edges: graph.edges.filter(edge => edge.source !== id && edge.target !== id) });
    if (source === id) setSource("");
    if (target === id) setTarget("");
    if (selected === id) setSelected(null);
  }

  function addNode(kind: PurchaseNodeKind) {
    const position = purchaseTemplate(true).nodes.find(node => node.kind === kind) ??
      { id: kind, kind, x: kind === "decision" ? 420 : 630, y: 230 };
    onChange({ ...graph, nodes: [...graph.nodes, position] });
    setSelected(position.id);
  }

  function connect(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!connectable) return;
    addConnection(source, target, currentBranch);
  }

  function applyTemplate(finance: boolean) {
    onChange(purchaseTemplate(finance));
    setSelected(null);
    setSource("");
    setTarget("");
    setFitKey(value => value + 1);
  }

  const nodeName = (id: string) => t[graph.nodes.find(node => node.id === id)!.kind];

  return <section className="settings-panel" aria-label={t.editor}>
    <div className="form-actions flex-wrap" role="group" aria-label={t.templates}>
      <Button type="button" variant="secondary" onClick={() => applyTemplate(false)}>{t.managerOnly}</Button>
      <Button type="button" variant="secondary" onClick={() => applyTemplate(true)}>{t.withFinance}</Button>
    </div>
    <p className="muted">{t.canvasHint}</p>
    <div className="purchase-canvas" role="region" aria-label={t.canvas}>
      <ReactFlow key={fitKey} nodes={nodes} edges={graph.edges.map(edge => ({
        id: edgeId(edge), source: edge.source, target: edge.target,
        label: edge.branch ? t[edge.branch] : undefined, markerEnd: { type: MarkerType.ArrowClosed },
      }))} onNodesChange={move} onPaneClick={() => setSelected(null)}
        onNodeClick={(_, node) => setSelected(node.id)}
        fitView fitViewOptions={{ padding: 0.16, maxZoom: 1 }} minZoom={0.18} maxZoom={2}
        nodeExtent={[[-10000, -10000], [10000, 10000]]}
        onConnect={connection => {
          const from = graph.nodes.find(node => node.id === connection.source);
          const to = graph.nodes.find(node => node.id === connection.target);
          if (!from || !to) return;
          const path = from.kind === "threshold" ? to.kind === "finance" ? "above" : "below" :
            from.kind === "decision" ? branch === "rejected" ? "rejected" : "approved" : null;
          addConnection(from.id, to.id, path);
        }}
        deleteKeyCode={null} zoomOnScroll={false}
        ariaLabelConfig={{ "node.a11yDescription.default": t.nodeHelp, "controls.ariaLabel": t.controls,
          "controls.zoomIn.ariaLabel": t.zoomIn, "controls.zoomOut.ariaLabel": t.zoomOut,
          "controls.fitView.ariaLabel": t.fit, "minimap.ariaLabel": t.minimap }}>
        <Background gap={20} />
        <Controls showInteractive={false} />
        <MiniMap className="purchase-map" />
      </ReactFlow>
    </div>
    <p role="status" className={isCompleteGraph(graph) ? "muted" : "error-text"}>{isCompleteGraph(graph) ? t.valid : t.invalid}</p>
    <div className="form-actions flex-wrap" role="group" aria-label={t.nodes}>
      {purchaseNodeKinds.map(kind => {
        const node = graph.nodes.find(item => item.kind === kind);
        return <Button key={kind} type="button" variant="secondary" aria-pressed={!!node}
          onClick={() => node ? removeNode(node.id) : addNode(kind)}>{t[kind]}</Button>;
      })}
    </div>
    <details>
      <summary>{t.connections}</summary>
      <ul className="decisions">
        {graph.edges.map(edge => {
          const label = `${nodeName(edge.source)} → ${nodeName(edge.target)}${edge.branch ? ` (${t[edge.branch]})` : ""}`;
          return <li key={edgeId(edge)} className="form-actions flex-wrap justify-between">
            <span>{label}</span>
            <Button type="button" variant="secondary" aria-label={`${t.remove}: ${label}`}
              onClick={() => onChange({ ...graph, edges: graph.edges.filter(item => item !== edge) })}>{t.remove}</Button>
          </li>;
        })}
      </ul>
      <form className="stack-form mt-4" onSubmit={connect}>
        <div className="form-row">
          <label>{t.source}<select value={source} onChange={event => setSource(event.target.value)}>
            <option value="">{t.select}</option>{graph.nodes.map(node => <option key={node.id} value={node.id}>{t[node.kind]}</option>)}
          </select></label>
          <label>{t.target}<select value={target} onChange={event => setTarget(event.target.value)}>
            <option value="">{t.select}</option>{graph.nodes.map(node => <option key={node.id} value={node.id}>{t[node.kind]}</option>)}
          </select></label>
        </div>
        {options.length > 0 && <label>{t.branch}<select value={currentBranch ?? ""} onChange={event => {
          const next = options.find(option => option === event.target.value);
          if (next) setBranch(next);
        }}>
          {options.map(option => <option key={option} value={option}>{t[option]}</option>)}
        </select></label>}
        <Button type="submit" variant="secondary" disabled={!connectable}>{t.connect}</Button>
      </form>
    </details>
  </section>;
}
