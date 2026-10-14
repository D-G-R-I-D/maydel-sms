"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { ImageOff, ImagePlus, Trash2, Upload } from "lucide-react";
import { isProblem, problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import { PHOTO_ACCEPT, checkPhotoFile } from "@/lib/media/photos";
import { Button, Card } from "@/components/ui/primitives";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { Alert } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";

export type SchoolAsset = "logo" | "signature";

const copy: Record<SchoolAsset, { title: string; noun: string; hint: string; removeNote: string; empty: string }> = {
  logo: {
    title: "Logo",
    noun: "logo",
    hint: "A transparent PNG that reads well on a light background works best.",
    removeNote: "Removing uses the built-in crest on report cards.",
    empty: "No logo uploaded. Report cards use the built-in crest.",
  },
  signature: {
    title: "Principal's signature",
    noun: "signature",
    hint: "Sign in black ink on white paper, photograph it, crop tightly; a transparent PNG prints best.",
    removeNote: "Report cards will print without a signature.",
    empty: "No signature uploaded. The principal's comment box prints without one.",
  },
};

async function send(method: "PUT" | "DELETE", asset: SchoolAsset, body?: FormData): Promise<Problem | null> {
  try {
    const res = await fetch(`/api/media/school/${asset}`, { method, body, credentials: "same-origin" });
    if (res.ok) return null;
    const json: unknown = await res.json().catch(() => null);
    return isProblem(json) ? json : toProblem(res.status, json);
  } catch {
    return { status: 0, title: "You appear to be offline. Check your connection and try again." };
  }
}

const fileError = (f: File) => checkPhotoFile(f)?.replaceAll("photo", "image") ?? null;

/**
 * Logo or signature: preview (through the BFF; `?v=` busts the cache on change), upload/replace
 * via a confirm-with-preview dialog, and remove. The server re-encodes to a transparent PNG.
 */
export function SchoolImageCard({ asset, version }: { asset: SchoolAsset; version: string | null }) {
  const c = copy[asset];
  const router = useRouter();
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const [, startTransition] = useTransition();

  useEffect(() => () => {
    if (preview) URL.revokeObjectURL(preview);
  }, [preview]);

  const choose = (f: File | undefined) => {
    if (!f) return;
    setError(fileError(f));
    setFile(f);
    setPreview(URL.createObjectURL(f));
  };

  const close = () => {
    setFile(null);
    setPreview(null);
    setError(null);
    if (input.current) input.current.value = "";
  };

  const upload = async () => {
    if (!file || error) return;
    setBusy(true);
    const form = new FormData();
    form.append("file", file);
    const problem = await send("PUT", asset, form);
    setBusy(false);
    if (problem) {
      setError(problem.errors?.file?.join(" ") ?? problemMessage(problem));
      return;
    }
    toast.success(version ? `${c.title} replaced` : `${c.title} uploaded`);
    close();
    startTransition(() => router.refresh());
  };

  const frame = "flex h-40 items-center justify-center rounded-input border border-border bg-white p-4";

  return (
    <Card className="flex flex-col p-5">
      <h2 className="font-display text-lg font-semibold">{c.title}</h2>
      <p className="mt-1 text-sm text-muted">{c.hint}</p>

      <div className={`mt-4 ${frame}`}>
        {version ? (
          // Served by our BFF with the API's caching headers; next/image would re-proxy it.
          <img src={`/api/media/school/${asset}?v=${encodeURIComponent(version)}`} alt={`Current ${c.noun}`} className="max-h-full max-w-full object-contain" />
        ) : (
          <p className="flex flex-col items-center gap-2 text-center text-sm text-slate-500">
            <ImageOff className="size-6" aria-hidden />
            {c.empty}
          </p>
        )}
      </div>

      <input ref={input} type="file" accept={PHOTO_ACCEPT} className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => choose(e.target.files?.[0])} />
      <div className="mt-4 flex flex-wrap gap-2">
        <Button variant="secondary" onClick={() => input.current?.click()}>
          <ImagePlus className="size-4" aria-hidden />
          {version ? `Replace ${c.noun}` : `Upload ${c.noun}`}
        </Button>
        {version && (
          <Button variant="ghost" className="text-danger" onClick={() => setConfirmRemove(true)}>
            <Trash2 className="size-4" aria-hidden />
            Remove
          </Button>
        )}
      </div>
      <p className="mt-2 text-xs text-muted">JPEG, PNG or WebP, up to 12 MB.</p>

      <FormDialog open={file !== null} onOpenChange={(v) => !v && !busy && close()} title={version ? `Replace ${c.noun}` : `Upload ${c.noun}`}>
        {() => (
          <div className="space-y-4">
            {preview && (
              <div className={frame}>
                {/* Local object-URL preview of the chosen file. */}
                <img src={preview} alt={`New ${c.noun} (not yet saved)`} className="max-h-full max-w-full object-contain" />
              </div>
            )}
            {error ? (
              <Alert tone="danger" title="Can't use this file">
                {error}
              </Alert>
            ) : (
              <p className="text-center text-sm text-muted">It&apos;s resized and saved as a PNG; transparency is kept.</p>
            )}
            <DialogActions>
              {error ? (
                <Button variant="secondary" onClick={() => input.current?.click()}>
                  Choose another
                </Button>
              ) : (
                <CancelButton />
              )}
              <Button onClick={upload} loading={busy} loadingText="Uploading…" disabled={Boolean(error)}>
                <Upload className="size-4" aria-hidden />
                {version ? `Replace ${c.noun}` : `Upload ${c.noun}`}
              </Button>
            </DialogActions>
          </div>
        )}
      </FormDialog>

      <ConfirmDialog
        open={confirmRemove}
        onOpenChange={setConfirmRemove}
        title={`Remove the ${c.noun}?`}
        confirmLabel={`Remove ${c.noun}`}
        pendingLabel="Removing…"
        tone="destructive"
        onConfirm={async () => {
          const problem = await send("DELETE", asset);
          if (problem) {
            toast.error(`Couldn't remove the ${c.noun}`, { description: problemMessage(problem) });
            return false;
          }
          toast.success(`${c.title} removed`);
          startTransition(() => router.refresh());
          return true;
        }}
      >
        <p>{c.removeNote} You can upload a new one at any time.</p>
      </ConfirmDialog>
    </Card>
  );
}
