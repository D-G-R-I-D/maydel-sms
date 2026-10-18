"use client";

import { useState } from "react";
import { CalendarCheck, Clock, Hourglass, LogIn, PartyPopper, Undo2, XCircle } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ApplicationDetail } from "@/lib/admissions/form";
import { formatSlotRange, modeLabel, type ExamSlotOption } from "@/lib/admissions/exam";
import { acceptOffer, bookExam, declineOffer } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { PdfDownloadButton } from "@/components/reports/pdf-download";

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));

function SlotList({ applicationId, slots, current, onDone }: { applicationId: string; slots: ExamSlotOption[]; current: string | null; onDone?: () => void }) {
  const [busy, setBusy] = useState<string | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const open = slots.filter((s) => s.seatsLeft > 0 || s.id === current);
  if (open.length === 0) {
    return <p className="text-sm text-muted">No exam times are available right now. Please check back soon, or contact the school.</p>;
  }
  return (
    <div className="space-y-2">
      {problem && problem.status > 0 && problem.status !== 429 && (
        <Alert tone="danger" title={problem.code === "exam_slot.busy" ? "Someone else was booking that time" : "Couldn't book that time"}>
          {problem.code === "exam_slot.busy" ? "Please try again." : problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
        </Alert>
      )}
      <ul className="divide-y divide-border rounded-input border border-border">
        {open.map((s) => (
          <li key={s.id} className={cn("flex flex-wrap items-center gap-3 px-3 py-2.5 text-sm", s.id === current && "bg-brand/5")}>
            <span className="min-w-0 flex-1">
              <span className="block font-medium">{formatSlotRange(s.startsAt, s.endsAt)}</span>
              <span className="block text-xs text-muted">
                {s.venue} · {modeLabel[s.mode]} · {s.seatsLeft} {s.seatsLeft === 1 ? "seat" : "seats"} left
              </span>
            </span>
            {s.id === current ? (
              <Badge tone="success">Your time</Badge>
            ) : (
              <Button
                size="sm"
                loading={busy === s.id}
                loadingText="Booking…"
                disabled={busy !== null}
                onClick={async () => {
                  setProblem(null);
                  setBusy(s.id);
                  const r = await runAction(() => bookExam(applicationId, s.id), { success: current ? "Exam time changed" : "Exam booked", onProblem: setProblem });
                  setBusy(null);
                  if (r) onDone?.();
                }}
              >
                {current ? "Move here" : "Book"}
              </Button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

/** E11.6–E11.8: what the family does next, by status. Scores are never shown to families. */
export function NextSteps({ detail, slots }: { detail: ApplicationDetail; slots: ExamSlotOption[] }) {
  const a = detail.application;
  const [changing, setChanging] = useState(false);
  const [declining, setDeclining] = useState(false);
  const [accepting, setAccepting] = useState(false);

  if (a.status === "Submitted") {
    return (
      <Card className="p-5">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
          <CalendarCheck className="size-5 text-brand" aria-hidden />
          Book your entrance exam
        </h2>
        <p className="mt-1 mb-3 text-sm text-muted">{a.examWindowEndsOn ? `Choose a time on or before ${day(a.examWindowEndsOn)}.` : "Choose a time."}</p>
        <SlotList applicationId={a.id} slots={slots} current={null} />
      </Card>
    );
  }
  if (a.status === "ExamBooked" && a.exam) {
    // The API only offers sittings that haven't started; if ours is among them, it can still be moved.
    const started = !slots.some((s) => s.id === a.exam!.slotId);
    return (
      <Card className="p-5">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
          <CalendarCheck className="size-5 text-success" aria-hidden />
          Entrance exam booked
        </h2>
        <p className="mt-2 font-medium">{formatSlotRange(a.exam.startsAt, a.exam.endsAt)}</p>
        <p className="text-sm text-muted">
          {a.exam.venue} · {modeLabel[a.exam.mode]}
        </p>
        <p className="mt-3 rounded-input bg-surface-2 p-3 text-sm">Bring a pencil, pen and eraser, and arrive 15 minutes early. We&apos;ll email you a reminder the day before.</p>
        {!started && (
          <div className="mt-3">
            {changing ? (
              <SlotList applicationId={a.id} slots={slots} current={a.exam.slotId} onDone={() => setChanging(false)} />
            ) : (
              <Button variant="secondary" size="sm" onClick={() => setChanging(true)}>
                <Clock className="size-4" aria-hidden />
                Change time
              </Button>
            )}
          </div>
        )}
      </Card>
    );
  }
  if (a.status === "Missed") {
    return (
      <Alert tone="warning" title="The exam window has closed">
        The entrance exam wasn&apos;t taken in time. Please contact the school office if you&apos;d still like to apply.
      </Alert>
    );
  }
  if (a.status === "Assessed") {
    return (
      <Card className="flex items-start gap-3 p-5">
        <Hourglass className="mt-0.5 size-5 text-brand" aria-hidden />
        <div>
          <h2 className="font-display text-lg font-semibold">Exam taken</h2>
          <p className="text-sm text-muted">Thank you. The school will email you once decisions are made.</p>
        </div>
      </Card>
    );
  }
  if (a.status === "Offered") {
    return (
      <Card className="border-success/40 bg-success/5 p-5">
        <h2 className="flex items-center gap-2 font-display text-xl font-semibold text-success">
          <PartyPopper className="size-6" aria-hidden />
          Congratulations! {a.childFirstName} has been offered a place
        </h2>
        {a.admissionNumber && (
          <p className="mt-2 text-sm">
            Admission number <span className="tabular font-mono text-base font-semibold">{a.admissionNumber}</span>
          </p>
        )}
        <p className="mt-2 text-sm text-muted">Read the admission letter, then accept or decline the offer.</p>
        <div className="mt-4 flex flex-wrap gap-2">
          <PdfDownloadButton href={`/api/media/admissions/applications/${a.id}/letter`} label="Download letter" fallbackName={`Admission letter - ${a.childFirstName} ${a.childLastName}.pdf`} size="sm" />
          <Button size="sm" onClick={() => setAccepting(true)}>
            Accept the offer
          </Button>
          <Button size="sm" variant="ghost" className="text-danger" onClick={() => setDeclining(true)}>
            Decline
          </Button>
        </div>
        <ConfirmDialog
          open={accepting}
          onOpenChange={setAccepting}
          title={`Accept the place for ${a.childFirstName}?`}
          confirmLabel="Accept"
          pendingLabel="Accepting…"
          onConfirm={async () => (await runAction(() => acceptOffer(a.id), { success: "Offer accepted" })) !== null}
        >
          <p>The school will then complete enrolment and email you. Your account becomes your parent portal login.</p>
        </ConfirmDialog>
        <ConfirmDialog
          open={declining}
          onOpenChange={setDeclining}
          title={`Decline the place for ${a.childFirstName}?`}
          confirmLabel="Decline the offer"
          pendingLabel="Declining…"
          tone="destructive"
          onConfirm={async () => (await runAction(() => declineOffer(a.id), { success: "Offer declined" })) !== null}
        >
          <p>This can&apos;t be undone. The place may be offered to another child.</p>
        </ConfirmDialog>
      </Card>
    );
  }
  if (a.status === "Accepted") {
    return (
      <Card className="flex items-start gap-3 p-5">
        <Hourglass className="mt-0.5 size-5 text-brand" aria-hidden />
        <div>
          <h2 className="font-display text-lg font-semibold">Offer accepted</h2>
          <p className="text-sm text-muted">Waiting for the school to complete enrolment. We&apos;ll email you when it&apos;s done.</p>
          <div className="mt-3">
            <PdfDownloadButton href={`/api/media/admissions/applications/${a.id}/letter`} label="Admission letter" fallbackName={`Admission letter - ${a.childFirstName} ${a.childLastName}.pdf`} size="sm" />
          </div>
        </div>
      </Card>
    );
  }
  if (a.status === "Enrolled") {
    return (
      <Card className="p-5">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-success">
          <PartyPopper className="size-5" aria-hidden />
          {a.childFirstName} is enrolled
        </h2>
        <p className="mt-1 text-sm text-muted">
          Welcome to Maydel Spring Schools{a.admissionNumber ? ` (admission number ${a.admissionNumber})` : ""}. Your account is now a parent portal login: sign in again to open it.
        </p>
        <form action="/api/auth/logout" method="post" className="mt-4">
          <Button type="submit">
            <LogIn className="size-4" aria-hidden />
            Sign in to the parent portal
          </Button>
        </form>
      </Card>
    );
  }
  if (a.status === "NotOffered") {
    return (
      <Card className="flex items-start gap-3 p-5">
        <XCircle className="mt-0.5 size-5 text-muted" aria-hidden />
        <div>
          <h2 className="font-display text-lg font-semibold">We&apos;re unable to offer a place this time</h2>
          <p className="text-sm text-muted">Thank you for applying to Maydel Spring Schools. We had many strong applicants this session. You&apos;re welcome to apply again in future.</p>
        </div>
      </Card>
    );
  }
  if (a.status === "Withdrawn") {
    return (
      <Card className="flex items-start gap-3 p-5">
        <Undo2 className="mt-0.5 size-5 text-muted" aria-hidden />
        <div>
          <h2 className="font-display text-lg font-semibold">Application closed</h2>
          <p className="text-sm text-muted">This application has been withdrawn. Contact the school office if this is a mistake.</p>
        </div>
      </Card>
    );
  }
  return null;
}
