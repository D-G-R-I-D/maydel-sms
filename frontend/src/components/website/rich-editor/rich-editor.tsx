"use client";

import { EditorContent, useEditor, useEditorState, type Editor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Link from "@tiptap/extension-link";
import { useRef, useState, type ReactNode } from "react";
import {
  Bold, Heading2, Heading3, ImagePlus, Italic, Link2, List, ListOrdered, Minus, Quote, Redo2, RectangleHorizontal, Strikethrough, Underline, Undo2, type LucideIcon,
} from "lucide-react";
import { checkPhotoFile, PHOTO_ACCEPT } from "@/lib/media/photos";
import { problemMessage } from "@/lib/api/problem";
import { toProblem } from "@/lib/api/problem";
import { Figure, type FigureAlign, type FigureAttrs } from "./figure";
import { FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

/** Links carry an optional class so one can be shown as a button (the server keeps class="button"). */
const ButtonableLink = Link.extend({
  addAttributes() {
    return { ...this.parent?.(), class: { default: null } };
  },
});

const linkOk = (href: string) => /^(https?:\/\/|mailto:|\/(?!\/))/i.test(href.trim());
/** The server keeps http(s) and mailto links only, so a site path becomes a full address (also right in emails). */
const absolute = (href: string) => (href.startsWith("/") ? `${window.location.origin}${href}` : href);

/**
 * The formatted-text editor for news, notices, events and newsletters. Outputs HTML in the shape the
 * server's allow-list keeps: h2–h4, bold/italic/underline/strike, lists, quotes, links (optionally as a
 * button), dividers, and pictures with alt text and a caption. Pasting from Word or Google Docs works;
 * the server strips styles on save.
 */
export function RichEditor({ id, initialHtml, onChange, label, invalid }: { id: string; initialHtml: string; onChange: (html: string) => void; label: string; invalid?: boolean }) {
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({ heading: { levels: [2, 3, 4] }, link: false, code: false, codeBlock: false }),
      ButtonableLink.configure({ openOnClick: false, autolink: true, protocols: ["http", "https", "mailto"], HTMLAttributes: { rel: "noopener noreferrer", target: null } }),
      Figure,
    ],
    content: initialHtml || "<p></p>",
    editorProps: {
      attributes: {
        id,
        role: "textbox",
        "aria-multiline": "true",
        "aria-label": label,
        ...(invalid ? { "aria-invalid": "true" } : {}),
        class: "rich-content min-h-64 px-4 py-3 outline-none",
      },
    },
    onUpdate: ({ editor }) => onChange(editor.isEmpty ? "" : editor.getHTML()),
  });

  return (
    <div className={cn("overflow-hidden rounded-input border bg-surface focus-within:outline-2 focus-within:outline-brand", invalid ? "border-danger" : "border-border")}>
      {editor ? <Toolbar editor={editor} /> : <div className="h-11 border-b border-border bg-surface-2" />}
      <EditorContent editor={editor} />
    </div>
  );
}

function Toolbar({ editor }: { editor: Editor }) {
  const s = useEditorState({
    editor,
    selector: ({ editor: e }) => ({
      h2: e.isActive("heading", { level: 2 }),
      h3: e.isActive("heading", { level: 3 }),
      bold: e.isActive("bold"),
      italic: e.isActive("italic"),
      underline: e.isActive("underline"),
      strike: e.isActive("strike"),
      bullet: e.isActive("bulletList"),
      ordered: e.isActive("orderedList"),
      quote: e.isActive("blockquote"),
      link: e.isActive("link"),
      figure: e.isActive("figure"),
      canUndo: e.can().undo(),
      canRedo: e.can().redo(),
    }),
  });
  const chain = () => editor.chain().focus();

  return (
    <div role="toolbar" aria-label="Formatting" className="flex flex-wrap items-center gap-0.5 border-b border-border bg-surface-2 p-1">
      <Tool icon={Heading2} label="Heading" active={s.h2} onClick={() => chain().toggleHeading({ level: 2 }).run()} />
      <Tool icon={Heading3} label="Subheading" active={s.h3} onClick={() => chain().toggleHeading({ level: 3 }).run()} />
      <Sep />
      <Tool icon={Bold} label="Bold" active={s.bold} onClick={() => chain().toggleBold().run()} />
      <Tool icon={Italic} label="Italic" active={s.italic} onClick={() => chain().toggleItalic().run()} />
      <Tool icon={Underline} label="Underline" active={s.underline} onClick={() => chain().toggleUnderline().run()} />
      <Tool icon={Strikethrough} label="Strikethrough" active={s.strike} onClick={() => chain().toggleStrike().run()} />
      <Sep />
      <Tool icon={List} label="Bulleted list" active={s.bullet} onClick={() => chain().toggleBulletList().run()} />
      <Tool icon={ListOrdered} label="Numbered list" active={s.ordered} onClick={() => chain().toggleOrderedList().run()} />
      <Tool icon={Quote} label="Quote" active={s.quote} onClick={() => chain().toggleBlockquote().run()} />
      <Tool icon={Minus} label="Divider" onClick={() => chain().setHorizontalRule().run()} />
      <Sep />
      <LinkDialog editor={editor} active={s.link} />
      <PictureDialog editor={editor} editing={s.figure} />
      <span className="ml-auto flex">
        <Tool icon={Undo2} label="Undo" disabled={!s.canUndo} onClick={() => chain().undo().run()} />
        <Tool icon={Redo2} label="Redo" disabled={!s.canRedo} onClick={() => chain().redo().run()} />
      </span>
    </div>
  );
}

function Tool({ icon: Icon, label, active, disabled, onClick }: { icon: LucideIcon; label: string; active?: boolean; disabled?: boolean; onClick?: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      aria-pressed={active}
      title={label}
      className={cn("inline-flex size-9 items-center justify-center rounded-input text-muted hover:bg-surface hover:text-text disabled:opacity-40", active && "bg-surface text-brand shadow-low")}
    >
      <Icon className="size-4" aria-hidden />
    </button>
  );
}

const Sep = () => <span className="mx-1 h-6 w-px bg-border" aria-hidden />;

/** Add or change a link; "Show as a button" makes it the school's call-to-action button. */
function LinkDialog({ editor, active }: { editor: Editor; active: boolean }) {
  const [open, setOpen] = useState(false);
  const [href, setHref] = useState("");
  const [button, setButton] = useState(false);
  const error = href.trim() && !linkOk(href) ? "Use https://…, mailto:… or a site path like /admissions-info." : undefined;

  const onOpenChange = (v: boolean) => {
    if (v) {
      const attrs = editor.getAttributes("link") as { href?: string; class?: string | null };
      setHref(attrs.href ?? "");
      setButton(Boolean(attrs.class?.includes("button")));
    }
    setOpen(v);
  };

  return (
    <FormDialog title={active ? "Edit link" : "Add a link"} description="Select the words to link first." open={open} onOpenChange={onOpenChange} onCloseAutoFocus={(e) => (e.preventDefault(), editor.commands.focus())} trigger={<ToolTrigger icon={Link2} label="Link" active={active} />}>
      {(close) => (
        <div className="space-y-4 p-5">
          <Field id="rte-href" label="Web address" error={error} hint="e.g. https://… or /admissions-info">
            <Input value={href} onChange={(e) => setHref(e.target.value)} placeholder="https://" {...describedBy("rte-href", error, true)} />
          </Field>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={button} onChange={(e) => setButton(e.target.checked)} className="size-4 accent-brand" />
            <RectangleHorizontal className="size-4 text-muted" aria-hidden />
            Show as a button
          </label>
          <div className="flex justify-between gap-2">
            {active ? (
              <Button variant="ghost" onClick={() => (editor.chain().focus().extendMarkRange("link").unsetLink().run(), close())}>
                Remove link
              </Button>
            ) : (
              <span />
            )}
            <Button
              disabled={!href.trim() || Boolean(error)}
              onClick={() => {
                const attrs = { href: absolute(href.trim()), class: button ? "button" : null };
                const c = editor.chain().focus().extendMarkRange("link");
                // With no words selected, insert the address itself as the link text.
                if (editor.state.selection.empty && !active) c.insertContent({ type: "text", text: attrs.href, marks: [{ type: "link", attrs }] }).run();
                else c.setLink(attrs).run();
                close();
              }}
            >
              Apply
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

/** Upload a picture (or edit the selected one's details): alt text and caption are required. */
function PictureDialog({ editor, editing }: { editor: Editor; editing: boolean }) {
  const [open, setOpen] = useState(false);
  const [src, setSrc] = useState("");
  const [alt, setAlt] = useState("");
  const [caption, setCaption] = useState("");
  const [align, setAlign] = useState<FigureAlign>("center");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const file = useRef<HTMLInputElement>(null);

  const onOpenChange = (v: boolean) => {
    if (v) {
      const a = (editing ? editor.getAttributes("figure") : {}) as Partial<FigureAttrs>;
      setSrc(a.src ?? "");
      setAlt(a.alt ?? "");
      setCaption(a.caption ?? "");
      setAlign(a.align ?? "center");
      setError(null);
    }
    setOpen(v);
  };

  const upload = async (f: File) => {
    const bad = checkPhotoFile(f);
    if (bad) return setError(bad);
    setBusy(true);
    setError(null);
    try {
      const body = new FormData();
      body.append("file", f);
      const res = await fetch("/api/media/website/media", { method: "POST", body, credentials: "same-origin" });
      const json: unknown = await res.json().catch(() => null);
      if (!res.ok) setError(problemMessage(toProblem(res.status, json)));
      else setSrc((json as { url: string }).url);
    } catch {
      setError("Couldn't upload. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  };

  const altError = src && !alt.trim() ? "Describe the picture for people who can't see it." : undefined;
  const capError = src && !caption.trim() ? "Add a short caption." : undefined;

  return (
    <FormDialog title={editing ? "Picture details" : "Add a picture"} open={open} onOpenChange={onOpenChange} onCloseAutoFocus={(e) => (e.preventDefault(), editor.commands.focus())} trigger={<ToolTrigger icon={ImagePlus} label={editing ? "Picture details" : "Add a picture"} active={editing} />}>
      {(close) => (
        <div className="space-y-4 p-5">
          {error && <Alert tone="danger">{error}</Alert>}
          {!editing && (
            <div>
              <input ref={file} type="file" accept={PHOTO_ACCEPT} className="sr-only" aria-label="Choose a picture" onChange={(e) => e.target.files?.[0] && void upload(e.target.files[0])} />
              <Button variant="secondary" loading={busy} loadingText="Uploading…" onClick={() => file.current?.click()}>
                <ImagePlus className="size-4" aria-hidden />
                {src ? "Choose a different picture" : "Choose a picture"}
              </Button>
              <p className="mt-1 text-xs text-muted">JPEG, PNG or WebP, up to 12 MB.</p>
            </div>
          )}
          {src && (
            <>
              <Field id="rte-alt" label="Alt text" error={altError} hint="What's in the picture, for screen readers and if it doesn't load.">
                <Input value={alt} onChange={(e) => setAlt(e.target.value)} {...describedBy("rte-alt", altError, true)} />
              </Field>
              <Field id="rte-cap" label="Caption" error={capError}>
                <Input value={caption} onChange={(e) => setCaption(e.target.value)} {...describedBy("rte-cap", capError)} />
              </Field>
              <fieldset>
                <legend className="mb-1.5 text-sm font-medium">Position</legend>
                <div className="flex flex-wrap gap-2">
                  {(["center", "left", "right", "wide"] as const).map((a) => (
                    <label key={a} className={cn("inline-flex min-h-9 cursor-pointer items-center rounded-input border px-3 text-sm", align === a ? "border-brand bg-brand/10 text-brand" : "border-border")}>
                      <input type="radio" name="rte-align" value={a} checked={align === a} onChange={() => setAlign(a)} className="sr-only" />
                      {a === "center" ? "Centre" : a === "left" ? "Left" : a === "right" ? "Right" : "Full width"}
                    </label>
                  ))}
                </div>
              </fieldset>
            </>
          )}
          <div className="flex justify-between gap-2">
            {editing ? (
              <Button variant="ghost" onClick={() => (editor.chain().focus().deleteSelection().run(), close())}>
                Remove picture
              </Button>
            ) : (
              <span />
            )}
            <Button
              disabled={!src || Boolean(altError || capError) || busy}
              onClick={() => {
                const attrs: FigureAttrs = { src, alt: alt.trim(), caption: caption.trim(), align };
                if (editing) editor.chain().focus().updateAttributes("figure", attrs).run();
                else editor.chain().focus().setFigure(attrs).run();
                close();
              }}
            >
              {editing ? "Update" : "Insert"}
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

/** A toolbar button that opens a dialog (Radix needs a forwardRef-able child; a plain button is). */
function ToolTrigger({ icon: Icon, label, active, ...rest }: { icon: LucideIcon; label: string; active?: boolean } & Omit<React.ComponentProps<"button">, "children">): ReactNode {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      aria-pressed={active}
      className={cn("inline-flex size-9 items-center justify-center rounded-input text-muted hover:bg-surface hover:text-text", active && "bg-surface text-brand shadow-low")}
      {...rest}
    >
      <Icon className="size-4" aria-hidden />
    </button>
  );
}
