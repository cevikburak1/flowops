import type { Locale } from "./messages";

const en = {
  title: "Operations report", platform: "Platform administration", period: "Reporting period",
  days: "days", apply: "Apply", purchaseStatus: "Purchases by status", status: "Status", count: "Requests",
  amount: "Total amount", noPurchases: "No purchases were submitted in this period.",
  overdue: "Overdue approvals", members: "Members", workflows: "Workflows", plan: "Plan",
  onTime: "Approvals within deadline",
  activity: "Audit log", time: "Time", actor: "Actor", action: "Action", target: "Target",
  auditSource: "Event source", auditAll: "All events", auditOrganization: "Organization", auditPurchase: "Purchases",
  auditPagination: "Audit log pagination",
  noActivity: "No audit events in this period.", deliveries: "Delivery retries", channel: "Channel",
  queued: "Waiting", nextAttempt: "Next attempt", noRetries: "No deliveries are waiting to retry.",
  organizations: "Organizations", starter: "Starter", business: "Business", enterprise: "Enterprise",
  savePlan: "Save plan", organization: "Organization", realtime: "Live updates",
  rabbitmq: "RabbitMQ", email: "Email", webhook: "Webhook", system: "System",
  invited: "Invitation created", invitationRevoked: "Invitation revoked", invitationAccepted: "Invitation accepted",
  memberUpdated: "Member updated", routingUpdated: "Routing updated", groupCreated: "Group created",
  processReminder: "Process task deadline approaching", processOverdue: "Process overdue",
  processTaskOverdue: "Process task overdue", processTaskReassigned: "Process task reassigned",
  groupUpdated: "Group updated", webhookEnabled: "Webhook enabled", webhookDisabled: "Webhook disabled",
  planChanged: "Plan changed", noOrganizations: "No organizations on this page.",
  memberRemoved: "Member removed",
  processPermissionsUpdated: "Process permissions updated",
  approvalMatrixUpdated: "Approval matrix updated",
  approvalSlaUpdated: "Approval deadline policy updated",
  formCreated: "Studio form created", formSaved: "Studio form draft saved", formPublished: "Studio form published", formSubmitted: "Studio form submitted",
};

const tr: typeof en = {
  title: "Operasyon raporu", platform: "Platform yönetimi", period: "Rapor dönemi",
  days: "gün", apply: "Uygula", purchaseStatus: "Duruma göre satın alma talepleri", status: "Durum", count: "Talepler",
  amount: "Toplam tutar", noPurchases: "Bu dönemde satın alma talebi gönderilmedi.",
  overdue: "Geciken onaylar", members: "Üyeler", workflows: "İş akışları", plan: "Plan",
  onTime: "Süresinde karara bağlanan onaylar",
  activity: "Denetim kaydı", time: "Zaman", actor: "İşlemi yapan", action: "İşlem", target: "Hedef",
  auditSource: "Olay kaynağı", auditAll: "Tüm olaylar", auditOrganization: "Kuruluş", auditPurchase: "Satın almalar",
  auditPagination: "Denetim kaydı sayfaları",
  noActivity: "Bu dönemde denetim kaydı yok.", deliveries: "Teslimat denemeleri", channel: "Kanal",
  queued: "Bekleyen", nextAttempt: "Sonraki deneme", noRetries: "Yeniden denenmeyi bekleyen teslimat yok.",
  organizations: "Kuruluşlar", starter: "Başlangıç", business: "İşletme", enterprise: "Kurumsal",
  savePlan: "Planı kaydet", organization: "Kuruluş", realtime: "Canlı güncellemeler",
  rabbitmq: "RabbitMQ", email: "E-posta", webhook: "Webhook", system: "Sistem",
  invited: "Davet oluşturuldu", invitationRevoked: "Davet iptal edildi", invitationAccepted: "Davet kabul edildi",
  memberUpdated: "Üye güncellendi", routingUpdated: "Yönlendirme güncellendi", groupCreated: "Grup oluşturuldu",
  processReminder: "Süreç görevinin son tarihi yaklaşıyor", processOverdue: "Süreç gecikti",
  processTaskOverdue: "Süreç görevi gecikti", processTaskReassigned: "Süreç görevi yeniden atandı",
  groupUpdated: "Grup güncellendi", webhookEnabled: "Webhook açıldı", webhookDisabled: "Webhook kapatıldı",
  planChanged: "Plan değiştirildi", noOrganizations: "Bu sayfada kuruluş yok.",
  memberRemoved: "Üye çıkarıldı",
  processPermissionsUpdated: "Süreç yetkileri güncellendi",
  approvalMatrixUpdated: "Onay matrisi güncellendi",
  approvalSlaUpdated: "Onay süresi politikası güncellendi",
  formCreated: "Stüdyo formu oluşturuldu", formSaved: "Stüdyo formu taslağı kaydedildi", formPublished: "Stüdyo formu yayınlandı", formSubmitted: "Stüdyo formu gönderildi",
};

export const reportText = { en, tr };
export const plans = ["starter", "business", "enterprise"] as const;
export type Plan = typeof plans[number];
export type DeliveryChannel = "realtime" | "rabbitmq" | "email" | "webhook";
export function reportLabel(locale: Locale, value: string) {
  const t = reportText[locale];
  return Object.hasOwn(t, value) ? t[value as keyof typeof t] : value;
}

export interface Report {
  days: number;
  statuses: { status: string; count: number; amount: number }[];
  overdue: number;
  slaCompleted: number;
  slaOnTime: number;
  members: number;
  workflows: number;
  plan: Plan;
  audit: { id: string; actor: string; action: string; target: string | null; at: string }[];
  auditPage: number;
  auditHasMore: boolean;
  deliveries: { channel: DeliveryChannel; count: number; nextAttemptAt: string }[];
}

export interface PlatformOrganizations {
  items: { id: string; name: string; slug: string; plan: Plan; members: number; workflows: number }[];
  page: number;
  hasMore: boolean;
}
