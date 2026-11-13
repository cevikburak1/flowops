import type { PurchaseGraph } from "./purchase-graph";

const en = {
  title: "Workflows", create: "New purchase workflow", template: "Purchase approval",
  templateHint: "Send purchases to the requester’s manager, then to finance when the amount exceeds the threshold.",
  name: "Workflow name", threshold: "Finance threshold", save: "Save draft", publish: "Publish version",
  archive: "Archive workflow", archiveHint: "Archiving stops new requests. Requests already submitted keep their version.",
  confirmArchive: "Confirm archive", draft: "Draft", unpublishedChanges: "Unpublished changes",
  archived: "Archived", version: "Version", published: "Published", versions: "Published versions",
  runs: "Purchase executions", noWorkflows: "Published purchase workflows will appear here.",
  noVersions: "Publish the draft to make it available for new requests.", noRuns: "No requests have used this workflow yet.",
  use: "Submit a request", settings: "Draft settings", changed: "The workflow changed. Reload the page and try again.",
  planLimit: "This organization has reached its workflow limit. Archive a workflow or contact your administrator.",
  invalid: "Enter a workflow name and a valid finance threshold.", failed: "The workflow could not be saved. Try again.",
  saved: "Draft saved", saveBeforePublish: "Save your changes before publishing.",
  search: "Search by name", filter: "Status", all: "All statuses", apply: "Apply filters", clear: "Clear filters",
  noMatches: "No workflows match these filters.", unpublished: "Unpublished",
  unpublish: "Unpublish workflow", confirmUnpublish: "Confirm unpublish",
  unpublishHint: "New requests will stop. You can publish again when ready.",
  duplicate: "Duplicate as draft", copyName: "Name for the copy", copySuffix: " copy",
};
const tr: typeof en = {
  title: "İş akışları", create: "Yeni satın alma akışı", template: "Satın alma onayı",
  templateHint: "Talepler önce talep sahibinin yöneticisine, eşik aşıldığında ardından finansa gider.",
  name: "İş akışı adı", threshold: "Finans eşiği", save: "Taslağı kaydet", publish: "Sürümü yayınla",
  archive: "İş akışını arşivle", archiveHint: "Arşivleme yeni talepleri durdurur. Gönderilmiş talepler kendi sürümünde devam eder.",
  confirmArchive: "Arşivlemeyi onayla", draft: "Taslak", unpublishedChanges: "Yayınlanmamış değişiklikler",
  archived: "Arşivlendi", version: "Sürüm", published: "Yayınlandı", versions: "Yayınlanan sürümler",
  runs: "Satın alma yürütmeleri", noWorkflows: "Yayınlanan satın alma akışları burada görünür.",
  noVersions: "Yeni taleplerde kullanılabilmesi için taslağı yayınlayın.", noRuns: "Henüz bu akışı kullanan talep yok.",
  use: "Talep gönder", settings: "Taslak ayarları", changed: "İş akışı değişti. Sayfayı yenileyip tekrar deneyin.",
  planLimit: "Kuruluş iş akışı sınırına ulaştı. Bir iş akışını arşivleyin veya yöneticinize başvurun.",
  invalid: "İş akışı adını ve geçerli bir finans eşiğini girin.", failed: "İş akışı kaydedilemedi. Tekrar deneyin.",
  saved: "Taslak kaydedildi", saveBeforePublish: "Yayınlamadan önce değişiklikleri kaydedin.",
  search: "Ada göre ara", filter: "Durum", all: "Tüm durumlar", apply: "Filtrele", clear: "Filtreleri temizle",
  noMatches: "Bu filtrelere uyan iş akışı yok.", unpublished: "Yayından kaldırıldı",
  unpublish: "Yayından kaldır", confirmUnpublish: "Yayından kaldırmayı onayla",
  unpublishHint: "Yeni talep alınmaz. Hazır olduğunda yeniden yayınlayabilirsin.",
  duplicate: "Taslak olarak kopyala", copyName: "Kopyanın adı", copySuffix: " kopyası",
};
export const workflowText = { en, tr };

export interface WorkflowItem { id: string; name: string; archived: boolean; unpublished: boolean; publishedVersion: number | null; hasDraft: boolean }
export const workflowStatuses = ["draft", "published", "unpublished", "archived"] as const;
export function workflowStatus(workflow: WorkflowItem) {
  if (workflow.archived) return "archived";
  if (workflow.unpublished) return "unpublished";
  return workflow.publishedVersion === null ? "draft" : "published";
}
export interface WorkflowPage { page: number; hasMore: boolean }
export interface WorkflowList extends WorkflowPage { items: WorkflowItem[] }
export interface WorkflowDetail extends WorkflowItem, WorkflowPage {
  graph: PurchaseGraph;
  revision: number;
  financeThreshold: number;
  versionPage: number;
  versionsHaveMore: boolean;
  runsHaveMore: boolean;
  versions: { number: number; name: string; financeThreshold: number; publishedAt: string }[];
  runs: { id: string; title: string; status: string; workflowVersion: number; createdAt: string }[];
}
export interface PublishedWorkflow { id: string; name: string; number: number }
