"use client";

import { useState, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import { organizationError, organizationText } from "../lib/organization";
import type { Locale } from "../lib/messages";
import { Button } from "./ui/button";

const storageKey = "flowops.invitation";
function invitationCode() {
  return location.hash.slice(1) || sessionStorage.getItem(storageKey) || "";
}
function subscribe(callback: () => void) {
  window.addEventListener("hashchange", callback);
  return () => window.removeEventListener("hashchange", callback);
}

export function JoinOrganization({ locale }: { locale: Locale }) {
  const t = organizationText[locale];
  const router = useRouter();
  const code = useSyncExternalStore(subscribe, invitationCode, () => null);
  const [busy, setBusy] = useState(false);
  const [signIn, setSignIn] = useState(false);
  const [error, setError] = useState("");

  async function accept() {
    if (!code) return;
    setBusy(true);
    setError("");
    try {
      const response = await fetch("/api/invitations/accept", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code }),
      });
      if (response.status === 401) { setSignIn(true); return; }
      const data = await response.json().catch(() => ({}));
      if (!response.ok) { setError(organizationError(locale, data.code ?? "error")); return; }
      sessionStorage.removeItem(storageKey);
      router.replace(`/workspace/${data.organizationId}`);
    } catch { setError(t.error); }
    finally { setBusy(false); }
  }

  return <main className="standalone-state">
    <h1>{t.accept}</h1>
    {code === "" && <p>{t.joinMissing}</p>}
    {code && <><p>{t.joinHint}</p>{signIn
      ? <a className="button primary" href="/auth/login?destination=invitation" onClick={() => sessionStorage.setItem(storageKey, code)}>{t.signIn}</a>
      : <Button type="button" onClick={accept} disabled={busy}>{t.accept}</Button>}</>}
    {error && <p className="error-text" role="alert">{error}</p>}
  </main>;
}
