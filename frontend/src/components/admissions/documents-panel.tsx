"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import { Camera, Download, FileUp, ImageOff, Trash2 } from "lucide-react";
import { isProblem, problemMessage, toProblem } from "@/lib/api/problem";
import { DOC_ACCEPT, MAX_DOCUMENTS, documentFileError, documentKindLabel, type ApplicationDetail, type DocumentKind } from "@/lib/admissions/form";
import { removeApplicationDocument } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Card, Select } from "@/components/ui/primitives";
import { ConfirmDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

/**
 * E11.5: the application's documents. A passport photograph slot (photo or camera, previewed) and up
 * to six documents in all. Files are served back only as downloads; photos preview as images.
 * Read-only once submitted (`canEdit` false) or for staff (`staff`).
 */
export function DocumentsPanel({ detail, staff = false }: { detail: ApplicationDetail; staff?: boolean }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const photoInput = useRef<HTMLInputElement>(null);
  const cameraInput = useRef<HTMLInputElement>(null);
  const otherInput = useRef<HTMLInputElement>(null);
  const [kind, setKind] = useState<Exclude<DocumentKind, "PassportPhoto">>("BirthCertificate");
  const [busy, setBusy] = useState<DocumentKind | null>(null);
  const [removing, setRemoving] = useState<string | null>(null);
  const id = detail.application.id;
  const editable = detail.canEdit && !staff;
  const photo = detail.documents.find((d) => d.kind === "PassportPhoto");
  const others = detail.documents.filter((d) => d.kind !== "PassportPhoto");
  const full = detail.documents.length >= MAX_DOCUMENTS;
  const fileHref = (docId: string) => (staff ? `/api/media/admissions/staff/applications/${id}/documents/${docId}` : `/api/media/admissions/applications/${id}/documents/${docId}`);

  const upload = async (file: File | undefined, k: DocumentKind) => {
    if (!file) return;
    const err = documentFileError(file, k);
    if (err) {
      toast.error("Can't use this file", { description: err });
      return;
    }
    const form = new FormData();
    form.append("kind", k);
    form.append("file", file);
    setBusy(k);
    try {
      const res = await fetch(`/api/media/admissions/applications/${id}/documents`, { method: "POST", body: form, credentials: "same-origin" });
      if (!res.ok) {
        const json: unknown = await res.json().catch(() => null);
        const p = isProblem(json) ? json : toProblem(res.status, json);
        toast.error("Couldn't upload the document", { description: p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p) });
        return;
      }
      toast.success(`${documentKindLabel[k]} uploaded`);
      startTransition(() => router.refresh());
    } catch {
      toast.error(unreachableProblem().title);
    } finally {
      setBusy(null);
      for (const input of [photoInput, cameraInput, otherInput]) if (input.current) input.current.value = "";
    }
  };

  return (
    <Card className="p-5">
      <h2 className="font-display text-lg font-semibold">Documents</h2>
      <p className="mt-0.5 text-sm text-muted">A passport photograph is required. You can add the birth certificate, the last report card or other documents (up to {MAX_DOCUMENTS} in all).</p>

      <div className="mt-4 flex flex-wrap items-start gap-4">
        <div className="flex size-32 shrink-0 items-center justify-center overflow-hidden rounded-input border border-border bg-surface-2">
          {photo ? (
            // The family's own re-encoded photo, served by the BFF (as a download for navigation; fine for <img>).
            <img src={fileHref(photo.id)} alt="Passport photograph" className="size-full object-cover" />
          ) : (
            <ImageOff className="size-8 text-muted" aria-hidden />
          )}
        </div>
        <div className="space-y-2">
          <p className="text-sm font-medium">Passport photograph {photo ? "" : <span className="text-danger">(required)</span>}</p>
          {editable && (
            <>
              <input ref={photoInput} type="file" accept="image/jpeg,image/png,image/webp" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => upload(e.target.files?.[0], "PassportPhoto")} />
              <input ref={cameraInput} type="file" accept="image/*" capture="user" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => upload(e.target.files?.[0], "PassportPhoto")} />
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="secondary" onClick={() => photoInput.current?.click()} loading={busy === "PassportPhoto"} loadingText="Uploading…">
                  <FileUp className="size-4" aria-hidden />
                  {photo ? "Replace photo" : "Choose photo"}
                </Button>
                <Button size="sm" variant="secondary" onClick={() => cameraInput.current?.click()} disabled={busy !== null}>
                  <Camera className="size-4" aria-hidden />
                  Take a photo
                </Button>
              </div>
              <p className="text-xs text-muted">A clear head-and-shoulders photo on a plain background.</p>
            </>
          )}
          {photo && !editable && (
            <a href={fileHref(photo.id)} download className="inline-flex items-center gap-1.5 text-sm text-brand hover:underline">
              <Download className="size-4" aria-hidden />
              Download
            </a>
          )}
        </div>
      </div>

      <ul className="mt-5 divide-y divide-border border-y border-border">
        {others.length === 0 && <li className="py-3 text-sm text-muted">No other documents.</li>}
        {others.map((d) => (
          <li key={d.id} className="flex flex-wrap items-center gap-3 py-2.5 text-sm">
            <span className="min-w-0 flex-1">
              <span className="font-medium">{documentKindLabel[d.kind]}</span>
              <span className="block text-xs text-muted">
                {d.contentType === "application/pdf" ? "PDF" : "Photo"} · {timeFormat.format(new Date(d.uploadedAt))}
              </span>
            </span>
            <a href={fileHref(d.id)} download className="inline-flex min-h-9 items-center gap-1.5 rounded-input px-2 text-brand hover:bg-surface-2">
              <Download className="size-4" aria-hidden />
              Download
            </a>
            {editable && (
              <Button size="sm" variant="ghost" className="text-danger" onClick={() => setRemoving(d.id)} aria-label={`Remove ${documentKindLabel[d.kind]}`}>
                <Trash2 className="size-4" aria-hidden />
              </Button>
            )}
          </li>
        ))}
      </ul>

      {editable && (
        <div className="mt-4 flex flex-wrap items-center gap-2">
          <label htmlFor="doc-kind" className="sr-only">
            Document type
          </label>
          <Select id="doc-kind" value={kind} onChange={(e) => setKind(e.target.value as typeof kind)} className="w-auto min-w-48" disabled={full}>
            <option value="BirthCertificate">Birth certificate</option>
            <option value="LastReportCard">Last report card</option>
            <option value="Other">Other document</option>
          </Select>
          <input ref={otherInput} type="file" accept={DOC_ACCEPT} className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => upload(e.target.files?.[0], kind)} />
          <Button size="sm" variant="secondary" onClick={() => otherInput.current?.click()} disabled={full || busy !== null} loading={busy !== null && busy !== "PassportPhoto"} loadingText="Uploading…">
            <FileUp className="size-4" aria-hidden />
            Add document
          </Button>
          {full && <span className="text-xs text-muted">That&apos;s the maximum of {MAX_DOCUMENTS}. Remove one to add another.</span>}
        </div>
      )}

      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(v) => !v && setRemoving(null)}
        title="Remove this document?"
        confirmLabel="Remove"
        pendingLabel="Removing…"
        tone="destructive"
        onConfirm={async () => {
          if (!removing) return true;
          const r = await runAction(() => removeApplicationDocument(id, removing), { success: "Document removed" });
          return r !== null;
        }}
      >
        <p>You can upload it again before you submit.</p>
      </ConfirmDialog>
    </Card>
  );
}
