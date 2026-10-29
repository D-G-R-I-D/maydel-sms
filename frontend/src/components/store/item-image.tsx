"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import { ImagePlus, Shirt, Trash2 } from "lucide-react";
import { isProblem, problemMessage, toProblem } from "@/lib/api/problem";
import { PHOTO_ACCEPT, checkPhotoFile } from "@/lib/media/photos";
import { siteImage } from "@/lib/site/images";
import { Button, Card } from "@/components/ui/primitives";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Alert } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";

async function send(method: "PUT" | "DELETE", id: string, body?: FormData) {
  try {
    const res = await fetch(`/api/media/store/items/${id}/image`, { method, body, credentials: "same-origin" });
    if (res.ok) return null;
    const json: unknown = await res.json().catch(() => null);
    return isProblem(json) ? json : toProblem(res.status, json);
  } catch {
    return { status: 0, title: "You appear to be offline. Check your connection and try again." };
  }
}

/** E19: the item's photo for the shop. The server re-encodes and resizes it. */
export function ItemImage({ id, name, imageUrl }: { id: string; name: string; imageUrl: string | null }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const src = siteImage(imageUrl);

  const upload = async (f: File | undefined) => {
    if (input.current) input.current.value = "";
    if (!f) return;
    const bad = checkPhotoFile(f);
    setError(bad);
    if (bad) return;
    setBusy(true);
    const form = new FormData();
    form.append("file", f);
    const problem = await send("PUT", id, form);
    setBusy(false);
    if (problem) return setError(problem.errors?.file?.join(" ") ?? problemMessage(problem));
    toast.success(src ? "Photo changed" : "Photo added");
    startTransition(() => router.refresh());
  };

  return (
    <Card className="space-y-3 p-5">
      <h2 className="font-display text-lg font-semibold">Photo</h2>
      <div className="flex aspect-square items-center justify-center overflow-hidden rounded-card border border-border bg-surface-2">
        {src ? <img src={src} alt={name} className="size-full object-cover" /> : <Shirt className="size-12 text-muted" aria-hidden />}
      </div>
      {error && <Alert tone="danger">{error}</Alert>}
      <input ref={input} type="file" accept={PHOTO_ACCEPT} className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => upload(e.target.files?.[0])} />
      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" size="sm" loading={busy} loadingText="Uploading…" onClick={() => input.current?.click()}>
          <ImagePlus className="size-4" aria-hidden />
          {src ? "Change photo" : "Add photo"}
        </Button>
        {src && (
          <ConfirmDialog
            title="Remove the photo?"
            confirmLabel="Remove"
            pendingLabel="Removing…"
            tone="destructive"
            onConfirm={async () => {
              const problem = await send("DELETE", id);
              if (problem) {
                setError(problemMessage(problem));
                return false;
              }
              toast.success("Photo removed");
              startTransition(() => router.refresh());
              return true;
            }}
            trigger={
              <Button variant="ghost" size="sm">
                <Trash2 className="size-4" aria-hidden />
                Remove
              </Button>
            }
          >
            The shop shows a plain placeholder instead.
          </ConfirmDialog>
        )}
      </div>
    </Card>
  );
}
