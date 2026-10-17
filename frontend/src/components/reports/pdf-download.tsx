"use client";

import { useState } from "react";
import { Download, Eye, Printer } from "lucide-react";
import { filenameFromDisposition } from "@/lib/reports/rules";
import { browserSaveEnv, inlineHref, pdfFile, savePdf, sharePdf, type LoadedPdf } from "@/lib/reports/save-pdf";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, buttonClasses } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

/** A failed PDF request; its message is already written for the user. */
class PdfError extends Error {}

/**
 * Saves a PDF from the BFF where the user chooses: the Save As dialog on desktop Chrome/Edge, the
 * share sheet on phones (iOS "Save to Files", Android Files/Drive), otherwise a normal download.
 * "View" opens it in the browser's PDF viewer instead. Errors from the BFF arrive as short plain text.
 */
export function PdfDownloadButton({
  href,
  label,
  fallbackName,
  icon = "download",
  variant = "secondary",
  size = "md",
  showView = true,
}: {
  href: string;
  label: string;
  fallbackName: string;
  icon?: "download" | "print";
  variant?: "secondary" | "primary" | "ghost";
  size?: "sm" | "md";
  /** The "View" link (opens inline in a new tab); off for compact places like table rows. */
  showView?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const Icon = icon === "print" ? Printer : Download;

  const load = async (): Promise<LoadedPdf> => {
    let res: Response;
    try {
      res = await fetch(href, { credentials: "same-origin" });
    } catch {
      throw new PdfError(unreachableProblem().title);
    }
    if (!res.ok) {
      const message = (await res.text().catch(() => "")).slice(0, 300) || "The PDF couldn't be created.";
      throw new PdfError(res.status === 401 ? "Your session has ended. Sign in again, then retry." : message);
    }
    return { blob: await res.blob(), name: filenameFromDisposition(res.headers.get("content-disposition")) ?? fallbackName };
  };

  const save = async () => {
    setBusy(true);
    const env = browserSaveEnv();
    let loaded: LoadedPdf | null = null;
    try {
      const outcome = await savePdf(
        async () => (loaded = await load()),
        fallbackName,
        env,
      );
      const name = (loaded as LoadedPdf | null)?.name ?? fallbackName;
      if (outcome === "saved") toast.success("PDF saved", { description: name });
      else if (outcome === "downloaded") toast.success("PDF downloaded", { description: name });
      else if (outcome === "share-blocked" && loaded) {
        // The tap "expired" while the PDF was being prepared; sharing needs a fresh one.
        const file = pdfFile((loaded as LoadedPdf).blob, name);
        toast("Your PDF is ready", {
          description: name,
          duration: 30_000,
          action: { label: "Save PDF", onClick: () => void sharePdf(file, env).catch(() => env.download(file, name)) },
        });
      }
    } catch (e) {
      toast.error("Couldn't get the PDF", { description: e instanceof PdfError ? e.message : unreachableProblem().title });
    } finally {
      setBusy(false);
    }
  };

  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <Button variant={variant} size={size} onClick={save} loading={busy} loadingText="Preparing PDF…">
        <Icon className="size-4" aria-hidden />
        {label}
      </Button>
      {showView && (
        <a href={inlineHref(href)} target="_blank" rel="noopener" className={buttonClasses("ghost", size)} aria-label={`View ${fallbackName.replace(/\.pdf$/i, "")} (opens in a new tab)`}>
          <Eye className="size-4" aria-hidden />
          View
        </a>
      )}
    </span>
  );
}
