import { getLocale, messages } from "../../lib/server";

export default async function WorkspaceLoading() {
  const t = messages[await getLocale()];
  return <main className="standalone-state" aria-busy="true" aria-label={t.workspace}>
    <div className="skeleton skeleton-title" /><div className="skeleton skeleton-panel" />
  </main>;
}
