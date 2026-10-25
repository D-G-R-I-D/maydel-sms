"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import Image from "next/image";
import { CalendarClock, Eye, ImagePlus, Megaphone, Pin, Save, Send, Trash2, Undo2 } from "lucide-react";
import { problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { useDraft } from "@/lib/drafts/use-draft";
import { checkPhotoFile, PHOTO_ACCEPT } from "@/lib/media/photos";
import { siteImage } from "@/lib/site/images";
import { formatLagos } from "@/lib/time/lagos";
import { deletePost, publishPost, savePost, unpublishPost } from "@/lib/website/actions";
import { audiences, formToInput, postErrors, postToForm, type Post, type PostForm } from "@/lib/website/posts";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Badge, Button, Card, Field, Input, buttonClasses, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";
import { RichEditor } from "./rich-editor/rich-editor";

const SERVER_FIELDS: Record<string, keyof PostForm> = { title: "title", body: "bodyHtml", bodyHtml: "bodyHtml", audience: "audience", showUntil: "showUntil", linkUrl: "linkUrl", summary: "summary" };

/**
 * News & notices editor (E7): formatted content, cover picture, who sees it, the notice window, the
 * pop-up / ticker / carousel switches, and publish. New posts are created on first save (a draft); the
 * picture can be added once the post exists.
 */
export function PostEditor({ initial }: { initial: Post | null }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [post, setPost] = useState(initial);
  const [saved, setSaved] = useState<PostForm>(() => postToForm(initial));
  const [form, setForm] = useState<PostForm>(saved);
  const [editorKey, setEditorKey] = useState(0);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState<"save" | "publish" | "picture" | null>(null);
  const picture = useRef<HTMLInputElement>(null);

  const hasPicture = Boolean(post?.imageUrl);
  const local = postErrors(form, hasPicture);
  const server: Partial<Record<keyof PostForm, string>> = {};
  for (const [k, v] of Object.entries(problem?.errors ?? {})) if (SERVER_FIELDS[k] && v[0]) server[SERVER_FIELDS[k]] = v[0];
  const errors = { ...server, ...local };
  const dirty = JSON.stringify(form) !== JSON.stringify(saved);
  const published = Boolean(post?.publishedAt);

  const draft = useDraft({
    scope: ["post", post?.id ?? "new"],
    value: form,
    dirty,
    server: saved,
    onRestore: (v) => {
      setForm(v);
      setEditorKey((k) => k + 1);
    },
  });

  const set = <K extends keyof PostForm>(k: K, v: PostForm[K]) => setForm((f) => ({ ...f, [k]: v }));

  const save = async (): Promise<Post | null> => {
    setProblem(null);
    setBusy("save");
    const r = await runAction(() => savePost(post?.id ?? null, formToInput(form)), { success: post ? "Saved" : "Draft created", onProblem: setProblem });
    setBusy(null);
    if (!r) return null;
    const next = postToForm(r);
    setPost(r);
    setSaved(next);
    setForm(next);
    draft.clear();
    if (!post) startTransition(() => router.replace(`/admin/website/news/${r.id}`));
    return r;
  };

  const togglePublish = async () => {
    let current = post;
    if (dirty || !current) current = await save();
    if (!current) return;
    setBusy("publish");
    const id = current.id;
    const r = await runAction(() => (published ? unpublishPost(id) : publishPost(id)), {
      success: published ? "Unpublished: it's a draft again" : "Published",
      onProblem: setProblem,
    });
    setBusy(null);
    if (r) {
      setPost(r);
      startTransition(() => router.refresh());
    }
  };

  const uploadPicture = async (file: File | null) => {
    if (!post) return;
    if (file) {
      const bad = checkPhotoFile(file);
      if (bad) return toast.error(bad);
    }
    setBusy("picture");
    try {
      const body = file ? new FormData() : undefined;
      if (file) body!.append("file", file);
      const res = await fetch(`/api/media/website/posts/${post.id}/image`, { method: file ? "PUT" : "DELETE", body, credentials: "same-origin" });
      const json: unknown = res.status === 204 ? null : await res.json().catch(() => null);
      if (!res.ok) toast.error(problemMessage(toProblem(res.status, json)));
      else {
        if (json) setPost(json as Post);
        else setPost({ ...post, imageUrl: null });
        toast.success(file ? "Picture added" : "Picture removed");
      }
    } catch {
      toast.error("Couldn't upload. Check your connection and try again.");
    } finally {
      setBusy(null);
    }
  };

  const img = siteImage(post?.imageUrl);
  const invalid = Object.keys(local).length > 0;

  return (
    <div className="space-y-5">
      {draft.restoredAt && (
        <DraftBanner
          draft={draft}
          onDiscard={() => {
            draft.clear();
            setForm(saved);
            setEditorKey((k) => k + 1);
          }}
        />
      )}
      {problem && !problem.errors && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
        <Card className="space-y-4 p-5">
          <Field id="post-title" label="Title" error={errors.title}>
            <Input value={form.title} onChange={(e) => set("title", e.target.value)} className="font-display text-lg" {...describedBy("post-title", errors.title)} />
          </Field>
          <Field id="post-summary" label="Summary (optional)" error={errors.summary} hint="One or two sentences for lists, the pop-up and search engines.">
            <Input value={form.summary} onChange={(e) => set("summary", e.target.value)} {...describedBy("post-summary", errors.summary, true)} />
          </Field>
          <div className="space-y-1.5">
            <span className="block text-sm font-medium" id="post-body-label">
              Content
            </span>
            <RichEditor key={editorKey} id="post-body" label="Content" initialHtml={form.bodyHtml} onChange={(html) => set("bodyHtml", html)} invalid={Boolean(errors.bodyHtml)} />
            {errors.bodyHtml && (
              <p className="text-sm text-danger" role="alert">
                {errors.bodyHtml}
              </p>
            )}
          </div>
        </Card>

        <div className="space-y-5">
          <Card className="space-y-3 p-5">
            <div className="flex items-center gap-2">
              <Badge tone={published ? "success" : "neutral"}>{published ? "Published" : "Draft"}</Badge>
              {dirty && <span className="text-xs text-warning">Unsaved changes</span>}
            </div>
            {post?.publishedAt && <p className="text-xs text-muted">Published {formatLagos(post.publishedAt)}</p>}
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" loading={busy === "save"} loadingText="Saving…" disabled={invalid || !dirty} onClick={() => void save()}>
                <Save className="size-4" aria-hidden />
                Save
              </Button>
              <Button variant={published ? "secondary" : "primary"} loading={busy === "publish"} loadingText={published ? "Unpublishing…" : "Publishing…"} disabled={invalid} onClick={() => void togglePublish()}>
                {published ? <Undo2 className="size-4" aria-hidden /> : <Send className="size-4" aria-hidden />}
                {published ? "Unpublish" : "Publish"}
              </Button>
            </div>
            {published && post?.slug && form.audience.includes("Public") && (
              <a href={`/news/${post.slug}`} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline">
                <Eye className="size-4" aria-hidden />
                View on the website
              </a>
            )}
          </Card>

          <Card className="space-y-3 p-5">
            <fieldset>
              <legend className="mb-2 text-sm font-medium">Who sees it</legend>
              <div className="space-y-1.5">
                {audiences.map((a) => (
                  <label key={a.value} className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      className="size-4 accent-brand"
                      checked={form.audience.includes(a.value)}
                      onChange={(e) => set("audience", e.target.checked ? [...form.audience, a.value] : form.audience.filter((v) => v !== a.value))}
                    />
                    {a.label}
                  </label>
                ))}
              </div>
              {errors.audience && (
                <p className="mt-1 text-sm text-danger" role="alert">
                  {errors.audience}
                </p>
              )}
            </fieldset>
            <Field id="post-category" label="Category (optional)" error={errors.category} hint="e.g. Academics, Events, Admissions">
              <Input value={form.category} onChange={(e) => set("category", e.target.value)} {...describedBy("post-category", errors.category, true)} />
            </Field>
          </Card>

          <Card className="space-y-3 p-5">
            <p className="text-sm font-medium">Cover picture</p>
            {img ? (
              <div className="relative aspect-[16/9] overflow-hidden rounded-input bg-surface-2">
                <Image src={img} alt="" fill sizes="320px" className="object-cover" />
              </div>
            ) : (
              <p className="text-sm text-muted">{post ? "No picture yet." : "Save the post first, then add a picture."}</p>
            )}
            <input ref={picture} type="file" accept={PHOTO_ACCEPT} className="sr-only" aria-label="Choose a cover picture" onChange={(e) => void uploadPicture(e.target.files?.[0] ?? null)} />
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant="secondary" disabled={!post} loading={busy === "picture"} loadingText="Uploading…" onClick={() => picture.current?.click()}>
                <ImagePlus className="size-4" aria-hidden />
                {img ? "Change" : "Add picture"}
              </Button>
              {img && (
                <Button size="sm" variant="ghost" disabled={busy === "picture"} onClick={() => void uploadPicture(null)}>
                  Remove
                </Button>
              )}
            </div>
          </Card>

          <Card className="space-y-4 p-5">
            <p className="flex items-center gap-2 text-sm font-medium">
              <Megaphone className="size-4 text-brand" aria-hidden />
              On the website
            </p>
            <Toggle checked={form.isPinned} onChange={(v) => set("isPinned", v)} label="Pin to the top of the news" icon={Pin} />
            <Toggle checked={form.inCarousel} onChange={(v) => set("inCarousel", v)} label="Homepage carousel (needs a picture)" error={errors.inCarousel} />
            <Toggle checked={form.inTicker} onChange={(v) => set("inTicker", v)} label="News ticker" />
            <Toggle checked={form.isFeatured} onChange={(v) => set("isFeatured", v)} label="Pop-up notice for visitors" />
            <div className="space-y-2 border-t border-border pt-3">
              <p className="flex items-center gap-2 text-sm font-medium">
                <CalendarClock className="size-4 text-brand" aria-hidden />
                Notice window (optional)
              </p>
              <p className="text-xs text-muted">Shows it in the website&apos;s notices envelope between these times (Abuja time).</p>
              <Field id="post-from" label="From">
                <Input type="datetime-local" value={form.showFrom} onChange={(e) => set("showFrom", e.target.value)} />
              </Field>
              <Field id="post-until" label="Until" error={errors.showUntil}>
                <Input type="datetime-local" value={form.showUntil} onChange={(e) => set("showUntil", e.target.value)} {...describedBy("post-until", errors.showUntil)} />
              </Field>
            </div>
            <div className="space-y-2 border-t border-border pt-3">
              <Field id="post-link" label="Button link (optional)" error={errors.linkUrl} hint="e.g. /admissions/apply or https://…">
                <Input value={form.linkUrl} onChange={(e) => set("linkUrl", e.target.value)} {...describedBy("post-link", errors.linkUrl, true)} />
              </Field>
              {form.linkUrl.trim() && (
                <Field id="post-link-text" label="Button text" error={errors.linkText}>
                  <Input value={form.linkText} onChange={(e) => set("linkText", e.target.value)} placeholder="Learn more" {...describedBy("post-link-text", errors.linkText)} />
                </Field>
              )}
            </div>
          </Card>

          {post && (
            <ConfirmDialog
              title="Delete this post?"
              confirmLabel="Delete"
              pendingLabel="Deleting…"
              tone="destructive"
              trigger={
                <button type="button" className={cn(buttonClasses("ghost", "sm"), "text-danger")}>
                  <Trash2 className="size-4" aria-hidden />
                  Delete post
                </button>
              }
              onConfirm={async () => {
                const r = await runAction(() => deletePost(post.id), { success: "Post deleted" });
                if (r) {
                  draft.clear();
                  startTransition(() => router.replace("/admin/website/news"));
                }
                return Boolean(r);
              }}
            >
              &ldquo;{post.title}&rdquo; will be removed{published ? " from the website and the portal" : ""}. This can&apos;t be undone.
            </ConfirmDialog>
          )}
        </div>
      </div>
    </div>
  );
}

function Toggle({ checked, onChange, label, icon: Icon, error }: { checked: boolean; onChange: (v: boolean) => void; label: string; icon?: typeof Pin; error?: string }) {
  return (
    <div>
      <span className="flex items-center gap-3 text-sm">
        <Switch checked={checked} onCheckedChange={onChange} label={label} />
        <span aria-hidden className="flex items-center gap-1.5">
          {Icon && <Icon className="size-3.5 text-muted" />}
          {label}
        </span>
      </span>
      {error && (
        <p className="mt-1 text-sm text-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
