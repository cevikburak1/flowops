import type { Metadata } from "next";
import { cookies } from "next/headers";
import "@fontsource-variable/geist";
import "./globals.css";

export const metadata: Metadata = {
  title: "FlowOps",
  description: "Purchase requests and approvals for your operations team.",
};

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const preferences = await cookies();
  const lang = preferences.get("flowops.locale")?.value === "tr" ? "tr" : "en";
  const theme = preferences.get("flowops.theme")?.value === "dark" ? "dark" : "light";
  return <html lang={lang} data-theme={theme}><body>{children}</body></html>;
}
