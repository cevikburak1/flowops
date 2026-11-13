import type { Locale } from "./messages";

export interface MatrixRule {
  priority: number; currency: string; minimum: number; maximum: number | null;
  category: string | null; requesterRole: string | null; departmentId: string | null;
  route: "manager" | "managerFinance";
  additionalReviewers?: string[] | null; minimumApprovals?: number | null;
}
export interface MatrixSettings { currency: string; approvalMatrixRevision: number; rules: MatrixRule[] }
const en = {
  title: "Approval matrix", help: "Rules apply to purchases started without a published workflow. The lowest matching priority wins.",
  priority: "Rule priority", minimum: "Minimum amount", maximum: "Maximum amount (exclusive)", route: "Approval route",
  manager: "Manager only", managerFinance: "Manager and finance", category: "Category", role: "Requester role",
  department: "Department", any: "Any", add: "Add rule", remove: "Remove rule", save: "Save matrix", saved: "Matrix saved",
  conflict: "Another administrator saved changes. Reload before editing.", error: "Check rule priorities, amounts, and departments.",
  preview: "Preview routing", amount: "Sample amount", fallback: "Organization threshold",
  reviewers: "Additional parallel reviewers", quorum: "Minimum approvals, including the assigned manager",
};
const tr: typeof en = {
  title: "Onay matrisi", help: "Kurallar, yayımlanmış akış seçilmeden açılan taleplere uygulanır. Eşleşen en küçük öncelik kullanılır.",
  priority: "Kural önceliği", minimum: "Alt tutar", maximum: "Üst tutar (hariç)", route: "Onay rotası",
  manager: "Yalnızca yönetici", managerFinance: "Yönetici ve finans", category: "Kategori", role: "Talep sahibi rolü",
  department: "Departman", any: "Tümü", add: "Kural ekle", remove: "Kuralı kaldır", save: "Matrisi kaydet", saved: "Matris kaydedildi",
  conflict: "Başka bir yönetici değişiklik kaydetti. Düzenlemeden önce sayfayı yenileyin.", error: "Öncelikleri, tutarları ve departmanları kontrol edin.",
  preview: "Rotayı önizle", amount: "Örnek tutar", fallback: "Kuruluş eşiği",
  reviewers: "Ek paralel onaylayıcılar", quorum: "Yönetici dahil asgari onay sayısı",
};
export const matrixText: Record<Locale, typeof en> = { en, tr };
