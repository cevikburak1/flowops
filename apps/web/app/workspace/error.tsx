"use client";

import { messages } from "../../lib/messages";

export default function WorkspaceError({ reset }: { reset: () => void }) {
  const t = messages[typeof document !== "undefined" && document.documentElement.lang === "tr" ? "tr" : "en"];
  return <main className="standalone-state"><h1>{t.pageError}</h1><button className="button primary" onClick={reset}>{t.tryAgain}</button></main>;
}
