import { JoinOrganization } from "../../components/join-organization";
import { getLocale } from "../../lib/server";

export default async function JoinPage() {
  return <JoinOrganization locale={await getLocale()} />;
}
