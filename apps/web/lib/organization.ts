import type { Locale } from "./messages";

const en = {
  settings: "Organization settings", members: "Members", invite: "Invite a member", email: "Email address", role: "Role",
  manager: "Manager", noManager: "Unassigned", save: "Save", saved: "Saved", pending: "Pending invitations",
  noInvites: "There are no pending invitations.", revoke: "Revoke", expires: "Expires",
  routing: "Purchase routing", threshold: "Finance approval threshold", finance: "Finance approver",
  automatic: "Any eligible approver", routingHint: "Purchases above this amount go to finance after manager approval.",
  inviteHint: "Share the link with the named recipient. They need a Keycloak account with this verified email.",
  inviteLink: "Invitation link", copy: "Copy link", copied: "Link copied", accept: "Accept invitation",
  joinHint: "Sign in with the verified email on your invitation.",
  signIn: "Sign in to accept", joinMissing: "Open the invitation link you received to join this organization.",
  error: "The change could not be saved. Try again.", invalidInvitation: "This invitation is invalid for your account or has expired.",
  verifiedEmailRequired: "Verify your email in Keycloak before accepting the invitation.",
  memberHasAssignments: "Reassign the member’s active approvals and routing before changing their role.",
  invalidManager: "Choose an eligible manager from this organization.",
  invalidFinance: "Choose a member with the Approver role.", invalidThreshold: "Enter a valid threshold.",
  invalidRole: "Choose a permitted role.", invalidEmail: "Enter a valid email address.", alreadyMember: "This email is already a member.",
  alreadyAccepted: "This invitation was already accepted.", member: "Member",
  OrganizationOwner: "Owner", OrganizationAdmin: "Administrator", Manager: "Manager",
  Approver: "Approver", Employee: "Employee", Viewer: "Viewer",
};

const tr: typeof en = {
  settings: "Kuruluş ayarları", members: "Üyeler", invite: "Üye davet et", email: "E-posta adresi", role: "Rol",
  manager: "Yönetici", noManager: "Atanmamış", save: "Kaydet", saved: "Kaydedildi", pending: "Bekleyen davetler",
  noInvites: "Bekleyen davet yok.", revoke: "İptal et", expires: "Son geçerlilik",
  routing: "Satın alma yönlendirmesi", threshold: "Finans onayı eşiği", finance: "Finans onaylayıcısı",
  automatic: "Uygun herhangi bir onaylayıcı", routingHint: "Bu tutarı aşan talepler, yönetici onayından sonra finansa gider.",
  inviteHint: "Bağlantıyı ilgili kişiyle paylaşın. Alıcının bu doğrulanmış e-posta adresine sahip bir Keycloak hesabı olmalıdır.",
  inviteLink: "Davet bağlantısı", copy: "Bağlantıyı kopyala", copied: "Bağlantı kopyalandı", accept: "Daveti kabul et",
  joinHint: "Davetinizdeki doğrulanmış e-posta adresiyle giriş yapın.",
  signIn: "Kabul etmek için giriş yap", joinMissing: "Kuruluşa katılmak için size gönderilen davet bağlantısını açın.",
  error: "Değişiklik kaydedilemedi. Tekrar deneyin.", invalidInvitation: "Bu davet hesabınız için geçersiz veya süresi dolmuş.",
  verifiedEmailRequired: "Daveti kabul etmeden önce Keycloak'ta e-posta adresinizi doğrulayın.",
  memberHasAssignments: "Rolü değiştirmeden önce üyenin aktif onaylarını ve yönlendirmelerini yeniden atayın.",
  invalidManager: "Bu kuruluştan uygun bir yönetici seçin.",
  invalidFinance: "Onaylayıcı rolüne sahip bir üye seçin.", invalidThreshold: "Geçerli bir eşik girin.",
  invalidRole: "İzin verilen bir rol seçin.", invalidEmail: "Geçerli bir e-posta adresi girin.", alreadyMember: "Bu e-posta zaten bir üyeye ait.",
  alreadyAccepted: "Bu davet zaten kabul edildi.", member: "Üye",
  OrganizationOwner: "Kuruluş sahibi", OrganizationAdmin: "Kuruluş yöneticisi", Manager: "Yönetici",
  Approver: "Onaylayıcı", Employee: "Çalışan", Viewer: "İzleyici",
};

export const organizationText = { en, tr };
export const roles = ["OrganizationAdmin", "Manager", "Approver", "Employee", "Viewer"] as const;
export type Role = typeof roles[number] | "OrganizationOwner";
export function organizationError(locale: Locale, code: string) {
  const t = organizationText[locale];
  return Object.hasOwn(t, code) ? t[code as keyof typeof t] : t.error;
}
export const canManageOrganization = (role?: string) => role === "OrganizationOwner" || role === "OrganizationAdmin";

export interface Member { subject: string; email: string | null; role: Role; manager: string | null }
export interface OrganizationSettings {
  members: Member[];
  invitations: { id: string; email: string; role: Role; expiresAt: string }[];
  financeThreshold: number;
  financeApprover: string | null;
  canAssignAdmin: boolean;
}
