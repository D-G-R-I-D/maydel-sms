import { rewriteContentImages } from "@/lib/site/images";
import { cn } from "@/components/ui/primitives";

/**
 * Formatted content from the editor. `html` was cleaned by the server against its allow-list on save
 * (no scripts, styles, event handlers or outside images); here only image addresses are pointed at the
 * site's proxy. Without HTML, the plain text keeps its line breaks.
 */
export function RichContent({ html, text, className }: { html: string | null | undefined; text?: string | null; className?: string }) {
  if (html) return <div className={cn("rich-content", className)} dangerouslySetInnerHTML={{ __html: rewriteContentImages(html) }} />;
  return text ? <p className={cn("whitespace-pre-line", className)}>{text}</p> : null;
}
