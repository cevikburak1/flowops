import type { Locale } from "./messages";

export const fieldTypes = ["text", "textarea", "number", "decimal", "currency", "date", "dateRange", "email", "phone",
  "select", "radio", "multiSelect", "checkbox", "toggle", "heading", "description", "user", "department", "file"] as const;
export const choiceFields = ["select", "radio", "multiSelect"];
export interface StudioField {
  key: string; label: string; type: typeof fieldTypes[number]; required: boolean;
  whenKey?: string | null; whenValue?: string | null; options?: string[] | null; help?: string | null; maxLength?: number | null;
  placeholder?: string | null; defaultValue?: string | null; minimum?: number | null; maximum?: number | null;
  minLength?: number | null; pattern?: string | null;
  halfWidth?: boolean; requiredWhenKey?: string | null; requiredWhenValue?: string | null; step?: number;
  fileLimitKiB?: number | null; fileExtensions?: string[] | null;
}
export interface StudioSchema { name: string; fields: StudioField[] }
export interface StudioChoices { users: { value: string; label: string }[]; departments: { value: string; label: string }[] }
export interface StudioFormItem { id: string; name: string; version: number }
export interface StudioForms { items: StudioFormItem[]; page: number; hasMore: boolean }
export interface StudioFormDetail {
  id: string; revision: number; version: number; publishedRevision: number; draft: StudioSchema;
  versions: { number: number; publishedAt: string }[]; page: number; hasMore: boolean;
}

const en = {
  forms: "Studio forms", create: "New form", name: "Form name", save: "Save draft", publish: "Publish form",
  add: "Add field", remove: "Remove field", up: "Move up", down: "Move down", type: "Field type",
  label: "Field label", help: "Help text", length: "Maximum length", required: "Required",
  condition: "Show when field", equals: "Matches value", always: "Always visible",
  options: "Options, one per line", preview: "Preview", mobile: "Narrow preview", desktop: "Full-width preview",
  history: "Published versions", empty: "Create a form to reuse its fields in a process.",
  version: "Version", draft: "Draft", saved: "Draft saved", dirty: "Unsaved changes", saving: "Saving…",
  conflict: "Someone changed this form. Copy any unsaved edits, then reload.",
  invalid: "Check field rules and step order. Place a single file field in the last step.", error: "Could not save the form.",
  order: "Keep a conditional field below the field it depends on.",
  saveSubmission: "Save answers as draft", submissionSaved: "Draft answers saved",
  submissionConflict: "The saved answers changed. Reload the form before continuing.",
  binding: "Published form", formId: "Form ID", useInline: "Use inline process fields",
  bindingHint: "Copy the form ID and version from Studio forms.", copy: "Copy form ID", copied: "Form ID copied",
  undo: "Undo", redo: "Redo", yes: "Yes", no: "No", selectOption: "Select an option",
  text: "Text", textarea: "Long text", number: "Whole number", decimal: "Decimal", date: "Date", email: "Email",
  select: "Dropdown", checkbox: "Checkbox",
  currency: "Currency amount", dateRange: "Date range", phone: "Phone", radio: "Radio group", multiSelect: "Multiple choices",
  toggle: "Toggle", heading: "Heading", description: "Description", from: "Start date", to: "End date",
  placeholder: "Placeholder", defaultValue: "Default value", minimum: "Minimum", maximum: "Maximum",
  minLength: "Minimum length", pattern: "Validation pattern",
  halfWidth: "Half width on desktop", requiredWhen: "Require when field",
  loadVersion: "Load published fields", inputName: "Elsa input name", mapping: "Input mapping",
  user: "User", department: "Department",
  step: "Form step", nextStep: "Next step", previousStep: "Previous step",
  file: "File upload", fileLimit: "File limit (KiB)", fileExtensions: "File extensions, separated by commas",
};
const tr: typeof en = {
  forms: "Stüdyo formları", create: "Yeni form", name: "Form adı", save: "Taslağı kaydet", publish: "Formu yayınla",
  add: "Alan ekle", remove: "Alanı kaldır", up: "Yukarı taşı", down: "Aşağı taşı", type: "Alan türü",
  label: "Alan etiketi", help: "Yardım metni", length: "En fazla karakter", required: "Zorunlu",
  condition: "Alan şu değerdeyken göster", equals: "Eşleşen değer", always: "Her zaman görünür",
  options: "Her satıra bir seçenek", preview: "Önizleme", mobile: "Dar önizleme", desktop: "Tam genişlikte önizleme",
  history: "Yayınlanan sürümler", empty: "Alanlarını bir süreçte kullanmak için form oluşturun.",
  version: "Sürüm", draft: "Taslak", saved: "Taslak kaydedildi", dirty: "Kaydedilmemiş değişiklikler", saving: "Kaydediliyor…",
  conflict: "Bu form başka bir oturumda değişti. Kaydedilmemiş düzenlemeleri kopyalayıp sayfayı yenileyin.",
  invalid: "Alan kurallarını ve adım sırasını kontrol edin. Son adımda tek bir dosya alanı kullanın.", error: "Form kaydedilemedi.",
  order: "Koşullu alanı bağlı olduğu alanın altında tutun.",
  saveSubmission: "Yanıtları taslak olarak kaydet", submissionSaved: "Taslak yanıtlar kaydedildi",
  submissionConflict: "Kayıtlı yanıtlar değişti. Devam etmeden önce formu yenileyin.",
  binding: "Yayınlanan form", formId: "Form kimliği", useInline: "Süreç alanlarını kullan",
  bindingHint: "Form kimliğini ve sürümünü Stüdyo formlarından alın.", copy: "Form kimliğini kopyala", copied: "Form kimliği kopyalandı",
  undo: "Geri al", redo: "Yinele", yes: "Evet", no: "Hayır", selectOption: "Seçenek seçin",
  text: "Metin", textarea: "Uzun metin", number: "Tam sayı", decimal: "Ondalık sayı", date: "Tarih", email: "E-posta",
  select: "Açılır liste", checkbox: "Onay kutusu",
  currency: "Para tutarı", dateRange: "Tarih aralığı", phone: "Telefon", radio: "Tek seçim", multiSelect: "Çoklu seçim",
  toggle: "Anahtar", heading: "Başlık", description: "Açıklama", from: "Başlangıç tarihi", to: "Bitiş tarihi",
  placeholder: "Yer tutucu", defaultValue: "Varsayılan değer", minimum: "Alt sınır", maximum: "Üst sınır",
  minLength: "En az karakter", pattern: "Doğrulama deseni",
  halfWidth: "Masaüstünde yarım genişlik", requiredWhen: "Alan şu değerdeyken zorunlu",
  loadVersion: "Yayınlanan alanları yükle", inputName: "Elsa girdi adı", mapping: "Girdi eşleştirme",
  user: "Kullanıcı", department: "Departman",
  step: "Form adımı", nextStep: "Sonraki adım", previousStep: "Önceki adım",
  file: "Dosya yükleme", fileLimit: "Dosya sınırı (KiB)", fileExtensions: "Dosya uzantıları, virgülle ayırın",
};
export const formText: Record<Locale, typeof en> = { en, tr };
