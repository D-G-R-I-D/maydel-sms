import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ChevronLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { ItemEditor } from "@/components/store/item-editor";
import { ItemImage } from "@/components/store/item-image";

export const metadata: Metadata = { title: "Store item" };

/** E19: add an item (/items/new) or edit one, with its options, stock and photo. */
export default async function StoreItemPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Store item" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const back = (
    <Link href="/admin/store" className="mb-3 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
      <ChevronLeft className="size-4" aria-hidden />
      School store
    </Link>
  );
  if (id === "new")
    return (
      <>
        {back}
        <PageHeader title="Add an item" description="Name it, list its options, then save to add stock and a photo." />
        <ItemEditor item={null} />
      </>
    );
  if (!GUID.test(id)) notFound();

  const r = await load((api) => api.GET("/api/store/items/{id}", { params: { path: { id } } }));
  if (!r.ok && r.problem.status === 404) notFound();
  if (!r.ok)
    return (
      <>
        {back}
        <PageHeader title="Store item" />
        <Card>
          <ProblemState problem={r.problem} />
        </Card>
      </>
    );
  const item = r.data;

  return (
    <>
      {back}
      <PageHeader title={item.name} />
      <div className="grid gap-5 lg:grid-cols-[1fr_18rem] lg:items-start">
        {/* Remount when options are added so new rows pick up their ids; stock changes flow in as props. */}
        <ItemEditor key={item.variants.map((v) => v.id).join(",")} item={item} />
        <ItemImage id={item.id} name={item.name} imageUrl={item.imageUrl} />
      </div>
    </>
  );
}
