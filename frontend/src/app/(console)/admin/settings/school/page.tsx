import type { Metadata } from "next";
import Link from "next/link";
import { FileText } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { SchoolProfileForm } from "@/components/school/school-profile-form";
import { SchoolImageCard } from "@/components/school/school-image-card";

export const metadata: Metadata = { title: "School profile" };

/** E2.6: the school's details, logo and principal's signature, as printed on report cards. Admin only. */
export default async function SchoolProfilePage() {
  const user = await requireSessionUser();
  const header = (
    <PageHeader
      title="School profile"
      description="The name, contacts, logo and principal's signature printed on report cards."
      actions={
        <Link href="/admin/reports" className={buttonClasses("secondary")}>
          <FileText className="size-4" aria-hidden />
          Preview a report card
        </Link>
      }
    />
  );
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="School profile" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const profile = await load((api) => api.GET("/api/school/profile"));
  if (!profile.ok) {
    return (
      <>
        {header}
        <Card>
          <ProblemState problem={profile.problem} />
        </Card>
      </>
    );
  }
  return (
    <>
      {header}
      <div className="grid gap-5 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <Card className="animate-rise p-5">
          <h2 className="mb-4 font-display text-lg font-semibold">Details</h2>
          <SchoolProfileForm profile={profile.data} />
        </Card>
        <div className="grid content-start gap-5 sm:grid-cols-2 xl:grid-cols-1">
          <SchoolImageCard asset="logo" version={profile.data.logoVersion} />
          <SchoolImageCard asset="signature" version={profile.data.signatureVersion} />
        </div>
      </div>
    </>
  );
}
