import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { load } from "@/lib/api/server";
import { BookList } from "@/components/book/book-list";
import { Card, PageHeader } from "@/components/ui/primitives";
import { ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Communication book" };

/** E17.2 portal: your children's books (or your own); with just one, open it straight away. */
export default async function PortalBooksPage() {
  const books = await load((api) => api.GET("/api/portal/books"));
  if (books.ok && books.data.length === 1) redirect(`/portal/book/${books.data[0].studentId}`);
  return (
    <>
      <PageHeader title="Communication book" description="Notes between you and the class teacher." />
      {books.ok ? (
        <BookList books={books.data} hrefFor={(b) => `/portal/book/${b.studentId}`} empty="No communication books yet" />
      ) : (
        <Card>
          <ProblemState problem={books.problem} />
        </Card>
      )}
    </>
  );
}
