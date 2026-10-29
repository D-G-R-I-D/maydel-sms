import type { Metadata } from "next";
import Link from "next/link";
import { ExternalLink, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { PhotoControls } from "@/components/media/photo-controls";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { StaffWebsiteDialog, StaffWebsiteSwitch } from "@/components/website/staff-website";

export const metadata: Metadata = { title: "Staff on the website" };

/** E7 console: who appears on the website's Staff page, in what order, with their title and bio. */
export default async function StaffOnWebsitePage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Staff on the website" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const list = await load((api) => api.GET("/api/cms/staff"));
  const rows = list.ok ? list.data : [];
  const shown = rows.filter((r) => r.showOnWebsite);
  const nextOrder = Math.min(999, Math.max(0, ...shown.map((r) => r.websiteOrder)) + 1);

  return (
    <>
      <PageHeader
        title="Staff on the website"
        description="Choose who appears on the website's Staff page, with the name, photo and short write-up visitors see."
        actions={
          <a href="/staff" target="_blank" rel="noopener noreferrer" className={buttonClasses("secondary")}>
            <ExternalLink className="size-4" aria-hidden />
            View the page
          </a>
        }
      />
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : rows.length === 0 ? (
        <Card>
          <EmptyState icon={Users} title="No active staff yet" />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <p className="border-b border-border px-5 py-3 text-sm text-muted">
            <span className="tabular">{shown.length}</span> of <span className="tabular">{rows.length}</span> on the website
          </p>
          <ul className="divide-y divide-border">
            {rows.map((r) => {
              return (
                <li key={r.staffId} className="flex items-center gap-3 px-5 py-3">
                  <PhotoControls kind="staff" id={r.staffId} name={r.name} photoVersion={r.hasPhoto ? r.photoVersion : null} canEdit />
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <Link href={`/admin/staff/${r.staffId}`} className="truncate font-medium hover:text-brand hover:underline">
                        {r.displayName || r.name}
                      </Link>
                      {r.displayName && r.displayName !== r.name && <span className="text-xs text-muted">({r.name})</span>}
                      {r.showOnWebsite && <Badge tone="success">#{r.websiteOrder}</Badge>}
                      {r.showOnWebsite && !r.hasPhoto && <Badge tone="warning">No photo</Badge>}
                    </span>
                    <span className="block truncate text-xs text-muted">{r.jobTitle ?? "No title"}{r.bio ? ` · ${r.bio}` : ""}</span>
                  </span>
                  <StaffWebsiteDialog row={r} />
                  <StaffWebsiteSwitch row={r} nextOrder={nextOrder} />
                </li>
              );
            })}
          </ul>
        </Card>
      )}
    </>
  );
}
