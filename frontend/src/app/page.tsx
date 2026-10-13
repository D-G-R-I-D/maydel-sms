import { redirect } from "next/navigation";

/** proxy.ts sends signed-in users to their home; this is the fallback. The public site arrives later. */
export default function RootPage() {
  redirect("/login");
}
