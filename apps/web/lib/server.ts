import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import type { Locale } from "./messages";
import type { PurchaseExecutionData } from "../components/purchase-execution";
export { messages } from "./messages";

export interface Organization { id: string; name: string; slug: string; role: string; currency: string }
export interface Profile { name: string; organizations: Organization[] }
export interface Purchase {
  id: string; title: string; category: string; amount: number; currency: string; status: string; createdAt: string;
}
export interface Workspace {
  purchases: Purchase[];
  approvals: (Purchase & { stage: number })[];
  pendingCount: number;
  statuses: { status: string; count: number }[];
  notifications: { id: string; purchaseId: string; title: string; kind: string; createdAt: string; readAt: string | null }[];
  canSubmit: boolean;
  page: number;
  pageSize: number;
}
export interface PurchaseDetail extends Purchase {
  workflow: { name: string; number: number } | null;
  execution: PurchaseExecutionData | null;
  description: string;
  processing: boolean;
  timeline: { action: string; at: string }[];
  stages: { stage: number; decision: string | null; comment: string | null; canDecide: boolean }[];
}

export async function getLocale(): Promise<Locale> {
  return (await cookies()).get("flowops.locale")?.value === "tr" ? "tr" : "en";
}

export async function getApi<T>(path: string): Promise<T> {
  const cookie = (await cookies()).toString();
  const response = await fetch(new URL(path, process.env.FLOWOPS_API_ORIGIN ?? "http://127.0.0.1:5080"), {
    headers: { cookie },
    cache: "no-store",
  });
  if ([401, 403].includes(response.status)) redirect("/auth/login");
  if (response.status === 404) notFound();
  if (!response.ok) throw new Error(`FlowOps API returned ${response.status}`);
  return response.json() as Promise<T>;
}
