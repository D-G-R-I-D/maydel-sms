"use client";

import { useState } from "react";
import { Download, Printer } from "lucide-react";
import { filenameFromDisposition } from "@/lib/reports/rules";
import { Button } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

/**
 * Downloads a PDF through the BFF with a visible pending state (the class PDF can take a few
 * seconds), then saves it under the server's file name. Errors arrive as short plain text.
 */
export function PdfDownloadButton({
  href,
  label,
  fallbackName,
  icon = "download",
  variant = "secondary",
  size = "md",
}: {
  href: string;
  label: string;
  fallbackName: string;
  icon?: "download" | "print";
  variant?: "secondary" | "primary";
  size?: "sm" | "md";
}) {
  const [busy, setBusy] = useState(false);
  const Icon = icon === "print" ? Printer : Download;

  const download = async () => {
    setBusy(true);
    try {
      const res = await fetch(href, { credentials: "same-origin" });
      if (!res.ok) {
        const message = (await res.text().catch(() => "")).slice(0, 300) || "The PDF couldn't be created.";
        toast.error("Couldn't download the PDF", { description: res.status === 401 ? "Your session has ended. Sign in again, then retry." : message });
        return;
      }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filenameFromDisposition(res.headers.get("content-disposition")) ?? fallbackName;
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 30_000);
      toast.success("PDF downloaded", { description: a.download });
    } catch {
      toast.error("Couldn't download the PDF", { description: "You appear to be offline. Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Button variant={variant} size={size} onClick={download} loading={busy} loadingText="Preparing PDF…">
      <Icon className="size-4" aria-hidden />
      {label}
    </Button>
  );
}
