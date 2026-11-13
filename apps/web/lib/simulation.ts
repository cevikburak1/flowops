import type { Locale } from "./messages";
import type { PurchaseBranch, PurchaseGraph, PurchaseNodeKind } from "./purchase-graph";

export type Decision = "approved" | "rejected";
export type Outcome = Decision | "invalid" | "failed";
export type Issue = "invalidInput" | "invalidGraph" | "unsupportedGraph" | "unreachable" | "timeout" | "stepLimit";
export interface SimulationInput { version: number | null; revision: number | null; amount: number; manager: Decision; finance: Decision }
export interface SimulationStep {
  nodeId: string; kind: PurchaseNodeKind; branch: PurchaseBranch | null; nextNodeId: string | null;
  decision: Decision | null; comparedAmount: number | null; threshold: number | null;
  mockNotification: boolean; durationMs: number;
}
export interface SimulationRun {
  id: string; createdAt: string;
  snapshot: {
    input: SimulationInput; threshold: number; graph: PurchaseGraph;
    result: { outcome: Outcome; durationMs: number; steps: SimulationStep[]; issues: { code: Issue; nodeId: string | null }[] };
  };
}
export interface SimulationCatalog {
  name: string; revision: number; versions: number[]; page: number; hasMore: boolean;
  versionPage: number; moreVersions: boolean;
  runs: { id: string; createdAt: string; summary: Outcome }[];
  scenarios: { id: string; name: string; input: SimulationInput }[];
}

const en = {
  title: "Workflow simulator", hint: "Run a saved purchase graph with sample decisions and inspect the path.",
  inputs: "Simulation inputs", source: "Workflow version", draft: "Saved draft", version: "Version", versionPage: "Published versions",
  amount: "Sample amount", manager: "Simulated manager decision", finance: "Simulated finance decision",
  scenario: "Save as scenario (optional)", scenarioLoad: "Load scenario", run: "Run simulation", running: "Running…",
  results: "Simulation result", empty: "Run a simulation to see its path and execution trace.",
  trace: "Execution trace", step: "Step", result: "Result", duration: "Duration", ms: "ms",
  approved: "Approved", rejected: "Rejected", invalid: "Invalid graph", failed: "Failed",
  invalidInput: "Enter a valid amount and choose a workflow version and decisions.",
  invalidGraph: "Complete the graph connections and decision branches in the workflow editor.",
  unsupportedGraph: "This graph contains unsupported steps or an invalid structure.",
  unreachable: "Unreachable step", timeout: "Simulation timed out", stepLimit: "Step limit reached",
  draftChanged: "The saved draft changed. Reload the page and try again.",
  error: "Could not load or run the simulation. Try again.",
  mockNotification: "Simulated notification to requester", history: "Run history and scenarios", runHistory: "Previous run",
  compare: "Compare with another run", comparison: "Comparison", current: "Selected run",
  export: "Export JSON", none: "Select", threshold: "Finance threshold", back: "Back to workflow",
  replay: "Review recorded steps", full: "Entire path", noComparison: "Choose a previous run to compare outcomes and paths.",
  isolated: "Sample decisions leave live requests, tasks, and notifications untouched.",
  currentDraft: "Saved draft scenarios reuse their inputs with the current saved draft.",
  matrixScope: "This trace uses the graph and finance threshold. Review matrix routing and approval counts in Approval matrix.",
  financeSkipped: "Finance skipped after manager rejection",
};
const tr: typeof en = {
  title: "İş akışı simülatörü", hint: "Kaydedilmiş satın alma akışını örnek kararlarla çalıştırın ve izlenen yolu inceleyin.",
  inputs: "Simülasyon girdileri", source: "İş akışı sürümü", draft: "Kayıtlı taslak", version: "Sürüm", versionPage: "Yayınlanan sürümler",
  amount: "Örnek tutar", manager: "Örnek yönetici kararı", finance: "Örnek finans kararı",
  scenario: "Senaryo olarak kaydet (isteğe bağlı)", scenarioLoad: "Senaryo yükle", run: "Simülasyonu çalıştır", running: "Çalıştırılıyor…",
  results: "Simülasyon sonucu", empty: "İzlenen yolu ve adımları görmek için bir simülasyon çalıştırın.",
  trace: "Yürütme izi", step: "Adım", result: "Sonuç", duration: "Süre", ms: "ms",
  approved: "Onaylandı", rejected: "Reddedildi", invalid: "Geçersiz akış", failed: "Başarısız",
  invalidInput: "Geçerli bir tutar girin, akış sürümünü ve kararları seçin.",
  invalidGraph: "Düzenleyicide akış bağlantılarını ve karar dallarını tamamlayın.",
  unsupportedGraph: "Akışta desteklenmeyen adımlar veya geçersiz bir yapı var.",
  unreachable: "Ulaşılamayan adım", timeout: "Simülasyon zaman aşımına uğradı", stepLimit: "Adım sınırına ulaşıldı",
  draftChanged: "Kayıtlı taslak değişti. Sayfayı yenileyip tekrar deneyin.",
  error: "Simülasyon yüklenemedi veya çalıştırılamadı. Tekrar deneyin.",
  mockNotification: "Talep sahibine örnek bildirim", history: "Çalıştırma geçmişi ve senaryolar", runHistory: "Önceki çalıştırma",
  compare: "Başka bir çalıştırmayla karşılaştır", comparison: "Karşılaştırma", current: "Seçilen çalıştırma",
  export: "JSON indir", none: "Seçin", threshold: "Finans eşiği", back: "İş akışına dön",
  replay: "Kayıtlı adımları incele", full: "Tüm yol", noComparison: "Sonuçları ve yolları karşılaştırmak için önceki bir çalıştırmayı seçin.",
  isolated: "Örnek kararlar canlı talepleri, görevleri ve bildirimleri değiştirmez.",
  currentDraft: "Kayıtlı taslak senaryolarının girdileri güncel taslakla çalıştırılır.",
  matrixScope: "Bu iz, grafiği ve finans eşiğini kullanır. Matris yönlendirmesini ve onay sayılarını Onay matrisi ekranından inceleyin.",
  financeSkipped: "Yönetici reddettiği için finans atlandı",
};
export const simulationText: Record<Locale, typeof en> = { en, tr };
