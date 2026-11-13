export const priorities = ["low", "normal", "high"] as const;
export const taskStatuses = ["pending", "overdue", "completed"] as const;
export type Priority = typeof priorities[number];

const en = {
  title: "My tasks", priority: "Priority", low: "Low", normal: "Normal", high: "High",
  pending: "Pending", overdue: "Overdue", completed: "Completed", dueAt: "Due date",
  allPriorities: "All priorities", apply: "Apply filters", empty: "No tasks match these filters.",
};
const tr: typeof en = {
  title: "Görevlerim", priority: "Öncelik", low: "Düşük", normal: "Normal", high: "Yüksek",
  pending: "Bekliyor", overdue: "Gecikmiş", completed: "Tamamlanmış", dueAt: "Son tarih",
  allPriorities: "Tüm öncelikler", apply: "Filtrele", empty: "Bu filtrelere uyan görev yok.",
};
export const taskText = { en, tr };

export interface TaskPage {
  items: { id: string; title: string; priority: Priority; stage: number; decision: string | null; dueAt: string; overdue: boolean }[];
  page: number;
  hasMore: boolean;
}
