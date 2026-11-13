export const purchaseNodeKinds = ["start", "manager", "decision", "threshold", "finance", "notify", "end"] as const;
export type PurchaseNodeKind = typeof purchaseNodeKinds[number];
export type PurchaseBranch = "above" | "below" | "approved" | "rejected";
export interface PurchaseGraph {
  schemaVersion: number;
  useApprovalMatrix?: boolean;
  nodes: { id: string; kind: PurchaseNodeKind; x: number; y: number }[];
  edges: { source: string; target: string; branch: PurchaseBranch | null }[];
}

const en = {
  editor: "Workflow editor", canvas: "Purchase workflow canvas", nodes: "Steps", connections: "Connections",
  start: "Start", manager: "Manager approval", threshold: "Finance threshold", finance: "Finance approval", end: "End",
  above: "Above threshold", below: "At or below threshold", source: "From step", target: "To step", branch: "Branch",
  connect: "Connect steps", select: "Select a step", remove: "Remove connection", save: "Save graph", saved: "Graph saved",
  managerOnly: "Manager only", withFinance: "Manager and finance", templates: "Apply a template",
  invalid: "Connect Start through Manager approval to End and complete each decision’s branches.",
  valid: "Ready to publish", canvasHint: "Drag steps to arrange them. Join the handles or use the connection controls below.",
  zoomIn: "Zoom in", zoomOut: "Zoom out", fit: "Fit workflow", controls: "Canvas controls", minimap: "Workflow map",
  nodeHelp: "Press Enter to select a step. Use arrow keys to move it.",
  decision: "Manager decision", notify: "Notify requester", approved: "Approved", rejected: "Rejected",
  execution: "Workflow execution", completed: "Completed", active: "Active", skipped: "Skipped", queued: "Not started", faulted: "Failed",
};

const tr: typeof en = {
  editor: "İş akışı düzenleyicisi", canvas: "Satın alma akışı tuvali", nodes: "Adımlar", connections: "Bağlantılar",
  start: "Başlangıç", manager: "Yönetici onayı", threshold: "Finans eşiği", finance: "Finans onayı", end: "Bitiş",
  above: "Eşiğin üstünde", below: "Eşiğe eşit veya altında", source: "Kaynak adım", target: "Hedef adım", branch: "Dal",
  connect: "Adımları bağla", select: "Adım seç", remove: "Bağlantıyı kaldır", save: "Akışı kaydet", saved: "Akış kaydedildi",
  managerOnly: "Yalnızca yönetici", withFinance: "Yönetici ve finans", templates: "Şablon uygula",
  invalid: "Başlangıcı yönetici onayı üzerinden bitişe bağlayın ve karar dallarını tamamlayın.",
  valid: "Yayınlamaya hazır", canvasHint: "Adımları sürükleyerek yerleştirin. Bağlantı noktalarını birleştirin veya aşağıdaki kontrolleri kullanın.",
  zoomIn: "Yakınlaştır", zoomOut: "Uzaklaştır", fit: "Akışı sığdır", controls: "Tuval kontrolleri", minimap: "Akış haritası",
  nodeHelp: "Adım seçmek için Enter tuşuna basın. Taşımak için ok tuşlarını kullanın.",
  decision: "Yönetici kararı", notify: "Talep sahibine bildir", approved: "Onaylandı", rejected: "Reddedildi",
  execution: "İş akışı yürütmesi", completed: "Tamamlandı", active: "Aktif", skipped: "Atlandı", queued: "Başlamadı", faulted: "Başarısız",
};
export const graphText = { en, tr };

export function purchaseTemplate(finance: boolean): PurchaseGraph {
  const nodes: PurchaseGraph["nodes"] = [
    { id: "start", kind: "start", x: 0, y: 110 },
    { id: "manager", kind: "manager", x: 210, y: 110 },
  ];
  const edges: PurchaseGraph["edges"] = [{ source: "start", target: "manager", branch: null }];
  if (finance) {
    nodes.push({ id: "threshold", kind: "threshold", x: 420, y: 110 }, { id: "finance", kind: "finance", x: 630, y: 0 });
    edges.push({ source: "manager", target: "threshold", branch: null }, { source: "threshold", target: "end", branch: "below" },
      { source: "threshold", target: "finance", branch: "above" }, { source: "finance", target: "end", branch: null });
  } else {
    edges.push({ source: "manager", target: "end", branch: null });
  }
  nodes.push({ id: "end", kind: "end", x: finance ? 840 : 420, y: 110 });
  return { schemaVersion: 1, nodes, edges };
}

export function isCompleteGraph(graph: PurchaseGraph) {
  const kinds = new Map(graph.nodes.map(node => [node.id, node.kind]));
  const present = new Set(kinds.values());
  const start = graph.nodes.find(node => node.kind === "start");
  if (!start || !present.has("manager") || !present.has("end") || present.has("threshold") !== present.has("finance")) return false;
  const visited = new Set<string>();
  function walk(id: string, manager: boolean, above: boolean, rejected: boolean, path: Set<string>): boolean {
    if (path.has(id)) return false;
    const kind = kinds.get(id);
    if (!kind) return false;
    visited.add(id);
    const links = graph.edges.filter(edge => edge.source === id);
    const linear = links.length === 1 && links[0].branch === null;
    const branches = (first: PurchaseBranch, second: PurchaseBranch) =>
      links.length === 2 && links.some(edge => edge.branch === first) && links.some(edge => edge.branch === second);
    if (kind === "end") return manager && links.length === 0;
    if (kind === "threshold") {
      if (!manager || rejected || !branches("above", "below") ||
          kinds.get(links.find(edge => edge.branch === "above")!.target) !== "finance") return false;
    } else if (kind === "decision") {
      if (!manager || !branches("approved", "rejected")) return false;
    } else if (!linear || (kind === "finance" && (!manager || !above || rejected))) return false;
    const next = new Set(path).add(id);
    return links.every(edge => walk(edge.target, manager || kind === "manager",
      above || edge.branch === "above", rejected || edge.branch === "rejected", next));
  }
  return walk(start.id, false, false, false, new Set()) && visited.size === graph.nodes.length;
}
