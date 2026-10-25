import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { PostEditor } from "@/components/website/post-editor";

export const metadata: Metadata = { title: "Edit post" };

export default async function EditPostPage({ params }: { params: Promise<{ id: string }> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Edit post" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  const post = await load((api) => api.GET("/api/cms/posts/{id}", { params: { path: { id } } }));
  if (!post.ok && post.problem.status === 404) notFound();
  return (
    <>
      <Link href="/admin/website/news" className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        News & notices
      </Link>
      <PageHeader title={post.ok ? post.data.title : "Edit post"} />
      {post.ok ? (
        <PostEditor key={post.data.id} initial={post.data} />
      ) : (
        <Card>
          <ProblemState problem={post.problem} />
        </Card>
      )}
    </>
  );
}
