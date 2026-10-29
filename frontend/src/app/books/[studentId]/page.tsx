import { notFound, redirect } from "next/navigation";
import { GUID } from "@/lib/media/binary-proxy";

/** `BookEntry` notifications to staff link to /books/{studentId}; the book lives in the console. */
export default async function BookRedirect({ params }: { params: Promise<{ studentId: string }> }) {
  const { studentId } = await params;
  if (!GUID.test(studentId)) notFound();
  redirect(`/admin/books/${studentId}`);
}
