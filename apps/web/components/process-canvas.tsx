"use client";

import { Background, Controls, MarkerType, Position, ReactFlow, type Edge, type Node } from "@xyflow/react";
import { useState } from "react";
import "@xyflow/react/dist/style.css";
import type { ProcessSpec, ProcessText, Step } from "../lib/processes";

export function ProcessCanvas({ steps, layout, change, t }: {
  steps: Step[]; layout: ProcessSpec["layout"]; change: (layout: NonNullable<ProcessSpec["layout"]>) => void; t: ProcessText;
}) {
  const [dragging, setDragging] = useState<Record<string, { x: number; y: number }>>({});
  const nodes: Node[] = [];
  const edges: Edge[] = [];
  function node(id: string, label: string, x: number, y: number) {
    nodes.push({ id, position: dragging[id] ?? layout?.[id] ?? { x, y },
      sourcePosition: Position.Right, targetPosition: Position.Left, className: "process-canvas-node", data: { label } });
  }
  function connect(source: string, target: string) {
    edges.push({ id: `${source}:${target}`, source, target, markerEnd: { type: MarkerType.ArrowClosed } });
  }
  node("start", t.start, 0, 0);
  let previous = "start";
  let x = 210;
  steps.forEach((step, index) => {
    const id = `root-${index}`;
    node(id, step.kind === "assignment" ? step.title || t.assignment : t[step.kind], x, 0);
    connect(previous, id);
    previous = id;
    x += 210;
    if (step.kind !== "parallel") return;
    const ends = step.branches.map((branch, branchIndex) => {
      let tail = id;
      branch.forEach((item, stepIndex) => {
        const key = `${id}-branch-${branchIndex}-${stepIndex}`;
        node(key, item.kind === "assignment" ? item.title || t.assignment : t[item.kind],
          x + stepIndex * 210, (branchIndex - (step.branches.length - 1) / 2) * 130);
        connect(tail, key);
        tail = key;
      });
      return tail;
    });
    x += Math.max(...step.branches.map(branch => branch.length)) * 210;
    previous = `${id}-join`;
    node(previous, t.join, x, 0);
    ends.forEach(tail => connect(tail, previous));
    x += 210;
  });
  node("end", t.end, x, 0);
  connect(previous, "end");
  return <section className="settings-panel" aria-label={t.canvas}>
    <h2>{t.canvas}</h2>
    <div className="purchase-canvas">
      <ReactFlow nodes={nodes} edges={edges} nodesConnectable={false} edgesFocusable={false}
        zoomOnScroll={false} fitView fitViewOptions={{ padding: 0.15, maxZoom: 1 }} minZoom={0.08}
        onNodesChange={changes => {
          const positions = changes.flatMap(item => item.type === "position" && item.position
            ? [{ id: item.id, position: item.position, dragging: item.dragging }] : []);
          if (!positions.length) return;
          const patch = Object.fromEntries(positions.map(item => [item.id, {
            x: Math.max(-10000, Math.min(10000, Math.round(item.position.x))),
            y: Math.max(-10000, Math.min(10000, Math.round(item.position.y))),
          }]));
          if (positions.some(item => item.dragging)) setDragging(current => ({ ...current, ...patch }));
          else {
            change({ ...layout, ...dragging, ...patch });
            setDragging({});
          }
        }}>
        <Background gap={20} /><Controls showInteractive={false} />
      </ReactFlow>
    </div>
  </section>;
}
