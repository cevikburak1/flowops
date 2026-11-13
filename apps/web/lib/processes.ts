import type { StudioChoices, StudioSchema } from "./studio-forms";

export type Step =
  | { kind: "timer"; seconds: number }
  | { kind: "http"; target: string }
  | { kind: "assignment"; title: string; assignee: string }
  | { kind: "parallel"; branches: Step[][] };

export interface ProcessField {
  key: string;
  label: string;
  type: "text" | "number" | "date";
  required: boolean;
}

export interface ProcessSpec {
  name: string;
  trigger: "manual" | "webhook";
  fields: ProcessField[];
  steps: Step[];
  attachment: "none" | "optional" | "required";
  layout?: Record<string, { x: number; y: number }> | null;
  form?: { id: string; version: number; inputMap?: Record<string, string> | null } | null;
}

export interface ProcessMember { subject: string; email: string | null }

export interface ProcessList {
  canEdit: boolean;
  targets: string[];
  members: ProcessMember[];
  items: { id: string; name: string; version: number }[];
  page: number;
  hasMore: boolean;
}

export interface ProcessDetail {
  id: string;
  revision: number;
  version: number;
  canEdit: boolean;
  canRun: boolean;
  draft: ProcessSpec | null;
  published: ProcessSpec | null;
  form: StudioSchema | null;
  formChoices: StudioChoices;
  submissionDraft: { revision: number; answers: Record<string, string> } | null;
  targets: string[];
  members: ProcessMember[];
  attachmentLimit: number;
  page: number;
  hasMore: boolean;
  tasks: { runId: string; stepId: string; title: string; assignee: string; completedAt: string | null; dueAt: string | null;
    escalatedAt: string | null; canComplete: boolean }[];
  runs: { id: string; version: number; status: "running" | "completed" | "failed"; createdAt: string; dueAt: string | null; escalatedAt: string | null;
    answers: string; attachment: string | null }[];
}

const en = {
  title: "Processes", new: "New process", name: "Process name", trigger: "Trigger", manual: "Form submission",
  webhook: "Webhook", attachment: "Attachment", none: "Disabled", optional: "Optional", required: "Required",
  fields: "Form fields", addField: "Add field", label: "Field label", type: "Field type",
  text: "Text", number: "Number", date: "Date", steps: "Execution steps", timer: "Timer",
  http: "HTTP POST", parallel: "Parallel branches", branch: "Branch", seconds: "Wait in seconds",
  target: "HTTP target", select: "Select a target", noTargets: "The operator has not configured HTTP targets.",
  addTimer: "Add timer", addHttp: "Add HTTP step", addParallel: "Add parallel branches", addBranch: "Add branch",
  remove: "Remove", earlier: "Move up", later: "Move down", draft: "Draft", version: "Version",
  save: "Save draft", publish: "Publish version", saved: "Draft saved", published: "Version published",
  run: "Start process", runs: "Execution history", noRuns: "No executions yet.", noProcesses: "No processes available.",
  deadline: "Deadline",
  running: "Running", completed: "Completed", failed: "Failed", started: "Process queued",
  invalidProcess: "Check the process name, fields, and step settings.",
  processChanged: "The process changed. Refresh the page and try again.",
  invalidAnswers: "Check the form fields and complete the required answers.",
  invalidAttachment: "Check the attachment requirement and maximum file size.",
  error: "The operation failed. Try again.",
  endpoint: "Authenticated webhook endpoint", answers: "Submitted answers", saveFirst: "Save the draft before publishing.",
  webhookBody: "Example request body",
  canvas: "Process canvas", start: "Start", end: "End", join: "Join",
  assignment: "Assigned task", addAssignment: "Add assigned task", assignee: "Assignee", taskTitle: "Task title",
  selectAssignee: "Select a member",
  completeTask: "Complete task", taskCompleted: "Task completed", myTasks: "Assigned process tasks",
  storageUnavailable: "File storage is unavailable. Try again later.",
  assigneeUnavailable: "A task assignee is no longer a member. Update and publish the process.",
};

const tr: typeof en = {
  title: "Süreçler", new: "Yeni süreç", name: "Süreç adı", trigger: "Tetikleyici", manual: "Form gönderimi",
  webhook: "Webhook", attachment: "Dosya eki", none: "Kapalı", optional: "İsteğe bağlı", required: "Zorunlu",
  fields: "Form alanları", addField: "Alan ekle", label: "Alan etiketi", type: "Alan türü",
  text: "Metin", number: "Sayı", date: "Tarih", steps: "Yürütme adımları", timer: "Zamanlayıcı",
  http: "HTTP POST", parallel: "Paralel kollar", branch: "Kol", seconds: "Saniye olarak bekleme",
  target: "HTTP hedefi", select: "Hedef seçin", noTargets: "Operatör henüz HTTP hedefi tanımlamadı.",
  addTimer: "Zamanlayıcı ekle", addHttp: "HTTP adımı ekle", addParallel: "Paralel kollar ekle", addBranch: "Kol ekle",
  remove: "Kaldır", earlier: "Yukarı taşı", later: "Aşağı taşı", draft: "Taslak", version: "Sürüm",
  save: "Taslağı kaydet", publish: "Sürümü yayınla", saved: "Taslak kaydedildi", published: "Sürüm yayınlandı",
  run: "Süreci başlat", runs: "Çalıştırma geçmişi", noRuns: "Henüz çalıştırma yok.", noProcesses: "Süreç bulunamadı.",
  deadline: "Son tarih",
  running: "Çalışıyor", completed: "Tamamlandı", failed: "Başarısız", started: "Süreç kuyruğa alındı",
  invalidProcess: "Süreç adını, form alanlarını ve adım ayarlarını kontrol edin.",
  processChanged: "Süreç değişti. Sayfayı yenileyip tekrar deneyin.",
  invalidAnswers: "Form alanlarını kontrol edin ve zorunlu yanıtları doldurun.",
  invalidAttachment: "Dosya eki gereksinimini ve boyut sınırını kontrol edin.",
  error: "İşlem tamamlanamadı. Tekrar deneyin.",
  endpoint: "Kimlik doğrulamalı webhook adresi", answers: "Gönderilen yanıtlar", saveFirst: "Yayınlamadan önce taslağı kaydedin.",
  webhookBody: "Örnek istek gövdesi",
  canvas: "Süreç tuvali", start: "Başlangıç", end: "Bitiş", join: "Birleşme",
  assignment: "Atanan görev", addAssignment: "Atanan görev ekle", assignee: "Atanan kişi", taskTitle: "Görev başlığı",
  selectAssignee: "Üye seçin",
  completeTask: "Görevi tamamla", taskCompleted: "Görev tamamlandı", myTasks: "Atanan süreç görevleri",
  storageUnavailable: "Dosya deposuna erişilemiyor. Daha sonra tekrar deneyin.",
  assigneeUnavailable: "Atanan kişilerden biri artık üye değil. Süreci güncelleyip yayınlayın.",
};

export const processText = { en, tr };
export type ProcessText = typeof en;
