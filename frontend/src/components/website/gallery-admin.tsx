"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import { ImagePlus, Pencil, Save, Star, Trash2, Upload } from "lucide-react";
import { problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import type { components } from "@/lib/api/schema";
import { runAction } from "@/lib/client/action";
import { checkPhotoFile, PHOTO_ACCEPT } from "@/lib/media/photos";
import { deleteGalleryPhoto, saveGalleryPhoto } from "@/lib/website/actions";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { Button, Card, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";

type Photo = components["schemas"]["GalleryPhotoDto"];

/** "IMG_1234 sports-day.jpg" → "Sports day": a starting title the admin can change later. */
export function titleFromFile(name: string): string {
  const base = name.replace(/\.[^.]+$/, "").replace(/^(img|dsc|pxl|photo)[_-]?\d+[_\s-]*/i, "").replace(/[_-]+/g, " ").trim();
  return base ? base.charAt(0).toUpperCase() + base.slice(1, 200) : "Photo";
}

/** Several photos at once into one album; each uploads in turn with its own progress. */
export function GalleryUploader({ albums, nextOrder }: { albums: string[]; nextOrder: number }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [album, setAlbum] = useState(albums[0] ?? "");
  const [files, setFiles] = useState<File[]>([]);
  const [status, setStatus] = useState<Record<number, "waiting" | "uploading" | "done" | string>>({});
  const [busy, setBusy] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const albumError = !album.trim() ? "Choose or type an album, e.g. Sports day." : album.length > 60 ? "60 characters at most." : undefined;

  const pick = (list: FileList | null) => {
    const chosen = Array.from(list ?? []).slice(0, 30);
    const bad = chosen.map((f) => checkPhotoFile(f)).find(Boolean);
    if (bad) toast.error(bad);
    const ok = chosen.filter((f) => !checkPhotoFile(f));
    setFiles(ok);
    setStatus(Object.fromEntries(ok.map((_, i) => [i, "waiting"])));
  };

  const upload = async () => {
    setBusy(true);
    let done = 0;
    for (let i = 0; i < files.length; i++) {
      setStatus((s) => ({ ...s, [i]: "uploading" }));
      try {
        const body = new FormData();
        body.append("file", files[i]);
        body.append("title", titleFromFile(files[i].name));
        body.append("category", album.trim());
        body.append("order", String(nextOrder + i));
        const res = await fetch("/api/media/website/gallery", { method: "POST", body, credentials: "same-origin" });
        if (res.ok) {
          done++;
          setStatus((s) => ({ ...s, [i]: "done" }));
        } else {
          const json: unknown = await res.json().catch(() => null);
          setStatus((s) => ({ ...s, [i]: problemMessage(toProblem(res.status, json)) }));
        }
      } catch {
        setStatus((s) => ({ ...s, [i]: "Couldn't upload. Check your connection." }));
      }
    }
    setBusy(false);
    if (done) {
      toast.success(`${done} photo${done === 1 ? "" : "s"} added to ${album.trim()}`);
      startTransition(() => router.refresh());
    }
    if (done === files.length) {
      setFiles([]);
      setStatus({});
      if (input.current) input.current.value = "";
    }
  };

  return (
    <Card className="space-y-4 p-5">
      <h2 className="font-display text-lg font-semibold">Add photos</h2>
      <div className="grid gap-4 sm:grid-cols-[1fr_auto] sm:items-end">
        <Field id="gal-album" label="Album" error={files.length ? albumError : undefined} hint="Pick an existing album or type a new one.">
          <Input list="gal-albums" value={album} onChange={(e) => setAlbum(e.target.value)} {...describedBy("gal-album", files.length ? albumError : undefined, true)} />
        </Field>
        <datalist id="gal-albums">
          {albums.map((a) => (
            <option key={a} value={a} />
          ))}
        </datalist>
        <div>
          <input ref={input} type="file" accept={PHOTO_ACCEPT} multiple className="sr-only" aria-label="Choose photos" onChange={(e) => pick(e.target.files)} />
          <Button variant="secondary" onClick={() => input.current?.click()} disabled={busy}>
            <ImagePlus className="size-4" aria-hidden />
            Choose photos
          </Button>
        </div>
      </div>
      <p className="text-xs text-muted">JPEG, PNG or WebP, up to 12 MB each, 30 at a time. Only add photos of children whose parents agreed to the website.</p>
      {files.length > 0 && (
        <>
          <ul className="max-h-60 space-y-1 overflow-y-auto text-sm">
            {files.map((f, i) => {
              const s = status[i];
              return (
                <li key={`${f.name}-${i}`} className="flex items-center gap-2">
                  <span className="min-w-0 flex-1 truncate">{f.name}</span>
                  <span className={cn("shrink-0 text-xs", s === "done" ? "text-success" : s === "uploading" ? "text-brand" : s === "waiting" ? "text-muted" : "text-danger")}>
                    {s === "done" ? "Added" : s === "uploading" ? "Uploading…" : s === "waiting" ? "Ready" : s}
                  </span>
                </li>
              );
            })}
          </ul>
          <div className="flex justify-end">
            <Button loading={busy} loadingText="Uploading…" disabled={Boolean(albumError)} onClick={() => void upload()}>
              <Upload className="size-4" aria-hidden />
              Upload {files.length} photo{files.length === 1 ? "" : "s"}
            </Button>
          </div>
        </>
      )}
    </Card>
  );
}

/** One photo's controls: feature on the homepage carousel, edit details, delete. */
export function PhotoActions({ photo, albums }: { photo: Photo; albums: string[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState(photo.title);
  const [caption, setCaption] = useState(photo.caption ?? "");
  const [category, setCategory] = useState(photo.category);
  const [order, setOrder] = useState(String(photo.order));
  const [featured, setFeatured] = useState(photo.isFeatured);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const local = {
    title: !title.trim() ? "Give the photo a title." : title.length > 200 ? "200 characters at most." : undefined,
    category: !category.trim() ? "Choose an album." : category.length > 60 ? "60 characters at most." : undefined,
    caption: caption.length > 500 ? "500 characters at most." : undefined,
    order: /^\d{1,4}$/.test(order) ? undefined : "A whole number.",
  };
  const server = problem?.errors ?? {};
  const errors = { title: local.title ?? server.title?.[0], category: local.category ?? server.category?.[0], caption: local.caption ?? server.caption?.[0], order: local.order };
  const id = `ph-${photo.id}`;

  const save = (input: { title: string; caption: string | null; category: string; order: number; isFeatured: boolean }, success: string) =>
    runAction(() => saveGalleryPhoto(photo.id, input), { success, onProblem: setProblem });

  const toggleFeatured = async () => {
    const r = await save({ title: photo.title, caption: photo.caption, category: photo.category, order: photo.order, isFeatured: !photo.isFeatured }, photo.isFeatured ? "Removed from the homepage carousel" : "On the homepage carousel");
    if (r) startTransition(() => router.refresh());
  };

  const onOpenChange = (v: boolean) => {
    if (v) {
      setTitle(photo.title);
      setCaption(photo.caption ?? "");
      setCategory(photo.category);
      setOrder(String(photo.order));
      setFeatured(photo.isFeatured);
      setProblem(null);
    }
    setOpen(v);
  };

  return (
    <div className="flex items-center gap-0.5">
      <Button variant="ghost" size="sm" aria-label={photo.isFeatured ? `Remove ${photo.title} from the carousel` : `Feature ${photo.title} on the homepage carousel`} aria-pressed={photo.isFeatured} onClick={() => void toggleFeatured()}>
        <Star className={cn("size-4", photo.isFeatured && "fill-gold text-gold")} aria-hidden />
      </Button>
      <FormDialog
        title="Photo details"
        open={open}
        onOpenChange={onOpenChange}
        trigger={
          <Button variant="ghost" size="sm" aria-label={`Edit ${photo.title}`}>
            <Pencil className="size-4" aria-hidden />
          </Button>
        }
      >
        {(close) => (
          <div className="space-y-4 p-5">
            {problem && !problem.errors && (
              <Alert tone="danger" title="Couldn't save">
                {problemMessage(problem)}
              </Alert>
            )}
            <Field id={`${id}-title`} label="Title" error={errors.title}>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} {...describedBy(`${id}-title`, errors.title)} />
            </Field>
            <Field id={`${id}-cap`} label="Caption (optional)" error={errors.caption}>
              <Input value={caption} onChange={(e) => setCaption(e.target.value)} {...describedBy(`${id}-cap`, errors.caption)} />
            </Field>
            <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
              <Field id={`${id}-album`} label="Album" error={errors.category}>
                <Input list={`${id}-albums`} value={category} onChange={(e) => setCategory(e.target.value)} {...describedBy(`${id}-album`, errors.category)} />
              </Field>
              <datalist id={`${id}-albums`}>
                {albums.map((a) => (
                  <option key={a} value={a} />
                ))}
              </datalist>
              <Field id={`${id}-order`} label="Order" error={errors.order} hint="Lower first">
                <Input value={order} onChange={(e) => setOrder(e.target.value)} inputMode="numeric" className="tabular" {...describedBy(`${id}-order`, errors.order, true)} />
              </Field>
            </div>
            <span className="flex items-center gap-3 text-sm">
              <Switch checked={featured} onCheckedChange={setFeatured} label="Feature on the homepage carousel" />
              <span aria-hidden>Feature on the homepage carousel</span>
            </span>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={close}>
                Cancel
              </Button>
              <Button
                loading={busy}
                loadingText="Saving…"
                disabled={Boolean(local.title || local.category || local.caption || local.order)}
                onClick={async () => {
                  setProblem(null);
                  setBusy(true);
                  const r = await save({ title: title.trim(), caption: caption.trim() || null, category: category.trim(), order: Number(order), isFeatured: featured }, "Photo saved");
                  setBusy(false);
                  if (r) {
                    close();
                    startTransition(() => router.refresh());
                  }
                }}
              >
                <Save className="size-4" aria-hidden />
                Save
              </Button>
            </div>
          </div>
        )}
      </FormDialog>
      <ConfirmDialog
        title="Delete this photo?"
        confirmLabel="Delete"
        pendingLabel="Deleting…"
        tone="destructive"
        trigger={
          <Button variant="ghost" size="sm" aria-label={`Delete ${photo.title}`}>
            <Trash2 className="size-4" aria-hidden />
          </Button>
        }
        onConfirm={async () => {
          const r = await runAction(() => deleteGalleryPhoto(photo.id), { success: "Photo deleted" });
          if (r) startTransition(() => router.refresh());
          return Boolean(r);
        }}
      >
        &ldquo;{photo.title}&rdquo; will be removed from the website gallery.
      </ConfirmDialog>
    </div>
  );
}
