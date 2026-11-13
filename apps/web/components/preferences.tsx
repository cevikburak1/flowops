"use client";

import { Languages, Moon } from "lucide-react";
import { useRouter } from "next/navigation";
import { messages, type Locale } from "../lib/messages";

export function Preferences({ locale }: { locale: Locale }) {
  const t = messages[locale];
  const router = useRouter();
  function setPreference(name: string, value: string) {
    document.cookie = `flowops.${name}=${value}; Path=/; Max-Age=31536000; SameSite=Lax${location.protocol === "https:" ? "; Secure" : ""}`;
  }
  return <>
    <button type="button" className="button icon-button" aria-label={t.language} onClick={() => {
      setPreference("locale", locale === "en" ? "tr" : "en");
      router.refresh();
    }}><Languages size={17} aria-hidden /><span>{locale === "en" ? "EN" : "TR"}</span></button>
    <button type="button" className="button icon-button" aria-label={t.theme} onClick={() => {
      const theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
      document.documentElement.dataset.theme = theme;
      setPreference("theme", theme);
      router.refresh();
    }}><Moon size={17} aria-hidden /></button>
  </>;
}
