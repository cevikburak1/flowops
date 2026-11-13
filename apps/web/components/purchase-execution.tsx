"use client";

import { Background, Controls, MarkerType, Position, ReactFlow } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { graphText, type PurchaseGraph } from "../lib/purchase-graph";
import type { Locale } from "../lib/messages";

export interface PurchaseExecutionData {
  graph: PurchaseGraph;
  nodes: { id: string; status: string; completedAt: string | null }[];
}

export function PurchaseExecution({ execution, terminal, locale, activeNode, visitedEdges, failedNode }: {
  execution: PurchaseExecutionData; terminal: boolean; locale: Locale;
  activeNode?: string; visitedEdges?: ReadonlySet<string>; failedNode?: string;
}) {
  const t = graphText[locale];
  const records = new Map(execution.nodes.map(node => [node.id, node]));
  const nodes = execution.graph.nodes.map(node => {
    const record = records.get(node.id);
    const status = node.id === failedNode || record?.status === "Faulted" ? "faulted" : node.id === activeNode ? "active" : record?.completedAt ? "completed" :
      record ? "active" : terminal ? "skipped" : "queued";
    return { id: node.id, position: { x: node.x, y: node.y },
      sourcePosition: Position.Right, targetPosition: Position.Left,
      className: `execution-node execution-node-${status}`, ariaLabel: `${t[node.kind]}: ${t[status]}`,
      data: { label: <><span>{t[node.kind]}</span><strong>{t[status]}</strong></> } };
  });
  const edges = execution.graph.edges.map(edge => {
    const id = [edge.source, edge.target, edge.branch].join(":");
    return { id, source: edge.source, target: edge.target,
      className: visitedEdges?.has(id) ? "simulation-visited" : undefined,
      label: edge.branch ? t[edge.branch] : undefined, markerEnd: { type: MarkerType.ArrowClosed },
    };
  });
  return <section className="panel detail-panel settings-panel" aria-label={t.execution}>
    <h2>{t.execution}</h2>
    <div className="purchase-canvas">
      <ReactFlow nodes={nodes} edges={edges} nodesDraggable={false} nodesConnectable={false}
        elementsSelectable={false} nodesFocusable={false} edgesFocusable={false} zoomOnScroll={false}
        fitView fitViewOptions={{ padding: 0.16, maxZoom: 1 }} minZoom={0.18}
        ariaLabelConfig={{ "controls.ariaLabel": t.controls, "controls.zoomIn.ariaLabel": t.zoomIn,
          "controls.zoomOut.ariaLabel": t.zoomOut, "controls.fitView.ariaLabel": t.fit }}>
        <Background gap={20} /><Controls showInteractive={false} />
      </ReactFlow>
    </div>
    <ul className="form-actions flex-wrap" aria-label={t.nodes}>
      {nodes.map(node => <li key={node.id} className="status">{node.ariaLabel}</li>)}
    </ul>
  </section>;
}
