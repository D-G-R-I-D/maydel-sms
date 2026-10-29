import type { Metadata } from "next";
import { MessageSquareQuote, Pencil, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Badge, Button, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { DeleteTestimonial, TestimonialDialog } from "@/components/website/testimonial-dialog";

export const metadata: Metadata = { title: "Testimonials" };

/** E7 console: families' words for the homepage, in order; hidden ones stay here for later. */
export default async function TestimonialsPage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Testimonials" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const list = await load((api) => api.GET("/api/cms/testimonials"));
  const nextOrder = list.ok ? Math.min(999, Math.max(0, ...list.data.map((t) => t.order)) + 1) : 0;
  const add = (
    <TestimonialDialog
      item={null}
      nextOrder={nextOrder}
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          Add testimonial
        </Button>
      }
    />
  );

  return (
    <>
      <PageHeader title="Testimonials" description="What families say, shown on the homepage in this order." actions={list.ok && list.data.length > 0 ? add : undefined} />
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : list.data.length === 0 ? (
        <Card>
          <EmptyState icon={MessageSquareQuote} title="No testimonials yet" action={add}>
            Until you add some, the homepage shows the quotes from the school&apos;s previous website.
          </EmptyState>
        </Card>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {list.data.map((t) => (
            <li key={t.id}>
              <Card className="flex h-full flex-col gap-3 p-5">
                <div className="flex items-center gap-2">
                  <span className="tabular text-xs text-muted">#{t.order}</span>
                  <Badge tone={t.isPublished ? "success" : "neutral"}>{t.isPublished ? "On the website" : "Hidden"}</Badge>
                  <span className="ml-auto flex">
                    <TestimonialDialog
                      item={t}
                      nextOrder={nextOrder}
                      trigger={
                        <Button variant="ghost" size="sm" aria-label={`Edit testimonial from ${t.attribution}`}>
                          <Pencil className="size-4" aria-hidden />
                        </Button>
                      }
                    />
                    <DeleteTestimonial item={t} />
                  </span>
                </div>
                <blockquote className="flex-1 font-display text-lg italic">&ldquo;{t.quote}&rdquo;</blockquote>
                <p className="text-sm text-muted">— {t.attribution}</p>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
