import type { Metadata } from "next";
import { BookPageView } from "@/components/book/book-page";

export const metadata: Metadata = { title: "Communication book" };

export default async function FamilyBookPage({ params }: { params: Promise<{ studentId: string }> }) {
  const { studentId } = await params;
  return <BookPageView studentId={studentId} viewer="Family" back={{ href: "/portal/book", label: "Communication book" }} />;
}
