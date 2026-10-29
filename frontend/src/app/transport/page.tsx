import { redirect } from "next/navigation";

/** The `BusCheckFlagged` notification links to /transport; the admin screens live in the console. */
export default function TransportRedirect() {
  redirect("/admin/transport");
}
