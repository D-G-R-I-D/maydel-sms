/**
 * How a fetched PDF reaches the user's device, best option first (feature detection only):
 * 1. Desktop Chromium: the File System Access "Save As" dialog, so they pick the folder.
 * 2. Phones/tablets: the share sheet (iOS "Save to Files", Android Files/Drive/WhatsApp…).
 * 3. Anywhere else: a normal download (Downloads, or the browser's "ask where to save").
 * A dismissed dialog/sheet (AbortError) is a cancel: no error and no fallback download.
 */

export type SaveOutcome = "saved" | "shared" | "downloaded" | "cancelled" | "share-blocked";

type Writable = { write: (data: Blob) => Promise<void>; close: () => Promise<void> };
type PickerOptions = { suggestedName: string; types: { description: string; accept: Record<string, string[]> }[] };

export type SaveEnv = {
  showSaveFilePicker?: (options: PickerOptions) => Promise<{ createWritable: () => Promise<Writable> }>;
  canShare?: (data: { files: File[] }) => boolean;
  share?: (data: { files: File[]; title: string }) => Promise<void>;
  /** Coarse pointer (phone/tablet): where the share sheet is the natural way to save. */
  isTouch: boolean;
  download: (blob: Blob, name: string) => void;
};

const isAbort = (e: unknown) => e instanceof Error && e.name === "AbortError";
const isNotAllowed = (e: unknown) => e instanceof Error && e.name === "NotAllowedError";

export function pdfFile(blob: Blob, name: string): File {
  return new File([blob], name, { type: "application/pdf" });
}

/** Opens the share sheet. NotAllowedError means the tap "expired" while fetching: ask for another tap. */
export async function sharePdf(file: File, env: Pick<SaveEnv, "share">): Promise<SaveOutcome> {
  try {
    await env.share!({ files: [file], title: file.name });
    return "shared";
  } catch (e) {
    if (isAbort(e)) return "cancelled";
    if (isNotAllowed(e)) return "share-blocked";
    throw e;
  }
}

export type LoadedPdf = { blob: Blob; name: string };

/**
 * Desktop: the Save As dialog opens first, while the click still counts as a user gesture (building a
 * class PDF can take longer than browsers allow), then the PDF is fetched and written. Elsewhere the
 * PDF is fetched first. Errors from `load` propagate to the caller.
 */
export async function savePdf(load: () => Promise<LoadedPdf>, suggestedName: string, env: SaveEnv): Promise<SaveOutcome> {
  if (env.showSaveFilePicker && !env.isTouch) {
    let handle: Awaited<ReturnType<NonNullable<SaveEnv["showSaveFilePicker"]>>> | null = null;
    try {
      handle = await env.showSaveFilePicker({ suggestedName, types: [{ description: "PDF", accept: { "application/pdf": [".pdf"] } }] });
    } catch (e) {
      if (isAbort(e)) return "cancelled";
      // SecurityError (no gesture) or similar: fall back to a plain download below.
    }
    const pdf = await load();
    if (handle) {
      try {
        const writable = await handle.createWritable();
        await writable.write(pdf.blob);
        await writable.close();
        return "saved";
      } catch {
        // write failed (permissions, disk): fall back to a download
      }
    }
    env.download(pdf.blob, pdf.name);
    return "downloaded";
  }

  const pdf = await load();
  if (env.isTouch && env.share && env.canShare) {
    const file = pdfFile(pdf.blob, pdf.name);
    let shareable = false;
    try {
      shareable = env.canShare({ files: [file] });
    } catch {
      shareable = false;
    }
    if (shareable) {
      try {
        return await sharePdf(file, env);
      } catch {
        // share failed for another reason: fall back to a download
      }
    }
  }
  env.download(pdf.blob, pdf.name);
  return "downloaded";
}

/** The browser's real APIs. */
export function browserSaveEnv(): SaveEnv {
  const w = window as unknown as { showSaveFilePicker?: SaveEnv["showSaveFilePicker"] };
  const nav = navigator as Navigator & { canShare?: SaveEnv["canShare"] };
  return {
    showSaveFilePicker: typeof w.showSaveFilePicker === "function" ? w.showSaveFilePicker.bind(window) : undefined,
    canShare: typeof nav.canShare === "function" ? nav.canShare.bind(navigator) : undefined,
    share: typeof nav.share === "function" ? (nav.share.bind(navigator) as SaveEnv["share"]) : undefined,
    isTouch: window.matchMedia?.("(pointer: coarse)").matches ?? false,
    download: (blob, name) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = name;
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 30_000);
    },
  };
}

/** Same URL with ?inline=1, which the BFF serves with Content-Disposition: inline for viewing. */
export function inlineHref(href: string): string {
  return `${href}${href.includes("?") ? "&" : "?"}inline=1`;
}

/** "attachment; filename=…" → "inline; filename=…" (keeps the name for the viewer's own Save). */
export function toInline(disposition: string | null): string {
  if (!disposition) return "inline";
  return /^\s*attachment/i.test(disposition) ? disposition.replace(/^\s*attachment/i, "inline") : `inline; ${disposition}`;
}
