"use client";

import * as Menu from "@radix-ui/react-dropdown-menu";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { Camera, ImagePlus, Trash2, Upload } from "lucide-react";
import { isProblem, problemMessage, toProblem } from "@/lib/api/problem";
import { PHOTO_ACCEPT, checkPhotoFile, type PhotoKind } from "@/lib/media/photos";
import { Button } from "@/components/ui/primitives";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { Alert } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";
import { PhotoAvatar } from "./photo-avatar";

async function send(method: "PUT" | "DELETE", kind: PhotoKind, id: string, body?: FormData) {
  try {
    const res = await fetch(`/api/media/${kind}/${id}/photo`, { method, body, credentials: "same-origin" });
    if (res.ok) return null;
    const json: unknown = await res.json().catch(() => null);
    return isProblem(json) ? json : toProblem(res.status, json);
  } catch {
    return { status: 0, title: "You appear to be offline. Check your connection and try again." };
  }
}

/**
 * Profile photo with Admin controls: choose a file → preview → upload, or remove (confirmed).
 * The server re-encodes, strips metadata and resizes; we just pre-check type and size.
 */
export function PhotoControls({
  kind,
  id,
  name,
  photoVersion,
  canEdit,
}: {
  kind: PhotoKind;
  id: string;
  name: string;
  photoVersion: string | null;
  canEdit: boolean;
}) {
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

  const avatar = <PhotoAvatar kind={kind} id={id} name={name} photoVersion={photoVersion} alt={photoVersion ? name : ""} className="size-16 text-xl sm:size-20 sm:text-2xl" />;
  if (!canEdit) return avatar;

  const choose = (f: File | undefined) => {
    if (!f) return;
    setError(checkPhotoFile(f));
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
    const problem = await send("PUT", kind, id, form);
    setBusy(false);
    if (problem) {
      setError(problem.errors?.file?.join(" ") ?? problemMessage(problem));
      return;
    }
    toast.success(photoVersion ? "Photo updated" : "Photo added");
    close();
    startTransition(() => router.refresh());
  };

  return (
    <div className="relative shrink-0">
      {avatar}
      <input ref={input} type="file" accept={PHOTO_ACCEPT} className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => choose(e.target.files?.[0])} />
      <Menu.Root>
        <Menu.Trigger
          className="absolute -right-1 -bottom-1 inline-flex size-8 items-center justify-center rounded-full border-2 border-surface bg-brand text-on-brand shadow hover:bg-brand-strong"
          aria-label={photoVersion ? "Change or remove photo" : "Add photo"}
        >
          <Camera className="size-4" aria-hidden />
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Content align="start" sideOffset={6} className="z-50 w-52 rounded-card border border-border bg-surface p-1.5 text-text shadow-low">
            <Menu.Item onSelect={() => input.current?.click()} className="flex min-h-10 cursor-pointer items-center gap-2 rounded-input px-3 text-sm outline-none data-[highlighted]:bg-surface-2">
              <ImagePlus className="size-4" aria-hidden />
              {photoVersion ? "Change photo" : "Add photo"}
            </Menu.Item>
            {photoVersion && (
              <Menu.Item onSelect={() => setConfirmRemove(true)} className="flex min-h-10 cursor-pointer items-center gap-2 rounded-input px-3 text-sm text-danger outline-none data-[highlighted]:bg-surface-2">
                <Trash2 className="size-4" aria-hidden />
                Remove photo
              </Menu.Item>
            )}
          </Menu.Content>
        </Menu.Portal>
      </Menu.Root>

      <FormDialog open={file !== null} onOpenChange={(v) => !v && !busy && close()} title={photoVersion ? "Change photo" : "Add photo"} description={name}>
        {() => (
          <div className="space-y-4">
            {preview && (
              <div className="flex justify-center">
                {/* Local object-URL preview of the chosen file. */}
                <img src={preview} alt={`${name} (new, not yet saved)`} className="size-48 rounded-full border border-border object-cover" />
              </div>
            )}
            {error ? (
              <Alert tone="danger" title="Can't use this file">
                {error}
              </Alert>
            ) : (
              <p className="text-center text-sm text-muted">The photo is resized and its location data removed when you upload.</p>
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
                Upload photo
              </Button>
            </DialogActions>
          </div>
        )}
      </FormDialog>

      <ConfirmDialog
        open={confirmRemove}
        onOpenChange={setConfirmRemove}
        title={`Remove ${name}'s photo?`}
        confirmLabel="Remove photo"
        pendingLabel="Removing…"
        tone="destructive"
        onConfirm={async () => {
          const problem = await send("DELETE", kind, id);
          if (problem) {
            toast.error("Couldn't remove the photo", { description: problemMessage(problem) });
            return false;
          }
          toast.success("Photo removed");
          startTransition(() => router.refresh());
          return true;
        }}
      >
        <p>Initials will show instead. You can add a new photo at any time.</p>
      </ConfirmDialog>
    </div>
  );
}
