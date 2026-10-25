import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied } from "@/components/ui/states";
import { PostEditor } from "@/components/website/post-editor";

export const metadata: Metadata = { title: "New post" };

export default async function NewPostPage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="New post" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  return (
    <>
      <Link href="/admin/website/news" className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        News & notices
      </Link>
      <PageHeader title="New post" />
      <PostEditor initial={null} />
    </>
  );
}
