import { redirect } from "next/navigation";
import { getApi, type Profile } from "../../lib/server";

export default async function WorkspaceIndex() {
  const profile = await getApi<Profile>("/api/me");
  const organization = profile.organizations[0];
  redirect(organization ? `/workspace/${organization.id}` : "/workspace/organizations/new");
}
