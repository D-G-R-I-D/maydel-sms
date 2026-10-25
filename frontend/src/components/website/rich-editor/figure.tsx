"use client";

import { Node, NodeViewWrapper, ReactNodeViewRenderer, mergeAttributes, type ReactNodeViewProps } from "@tiptap/react";
import { siteImage } from "@/lib/site/images";
import { cn } from "@/components/ui/primitives";

export type FigureAlign = "center" | "left" | "right" | "wide";
export type FigureAttrs = { src: string; alt: string; caption: string; align: FigureAlign };

const alignClass: Record<FigureAlign, string> = { center: "align-center", left: "align-left", right: "align-right", wide: "wide" };
const fromClass = (c: string | null): FigureAlign => (c?.includes("wide") ? "wide" : c?.includes("align-left") ? "left" : c?.includes("align-right") ? "right" : "center");

// Augments the Commands interface where Tiptap declares it (types only; @tiptap/react re-exports core).
declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    figure: { setFigure: (attrs: FigureAttrs) => ReturnType };
  }
}

/**
 * A picture with alt text and a caption, saved as <figure class="align-…|wide"><img><figcaption>, the
 * shape the server's allow-list keeps. `src` stays the API's public address; the editor shows it through
 * the site's image proxy.
 */
export const Figure = Node.create({
  name: "figure",
  group: "block",
  atom: true,
  draggable: true,

  addAttributes() {
    return {
      src: { default: "" },
      alt: { default: "" },
      caption: { default: "" },
      align: { default: "center" as FigureAlign },
    };
  },

  parseHTML() {
    return [
      {
        tag: "figure",
        getAttrs: (el) => {
          const img = (el as HTMLElement).querySelector("img");
          if (!img) return false;
          return {
            src: img.getAttribute("src") ?? "",
            alt: img.getAttribute("alt") ?? "",
            caption: (el as HTMLElement).querySelector("figcaption")?.textContent ?? "",
            align: fromClass((el as HTMLElement).getAttribute("class")),
          };
        },
      },
      // A bare <img> (e.g. pasted) becomes a figure without a caption.
      { tag: "img[src]", getAttrs: (el) => ({ src: (el as HTMLElement).getAttribute("src") ?? "", alt: (el as HTMLElement).getAttribute("alt") ?? "", caption: "", align: "center" }) },
    ];
  },

  renderHTML({ node }) {
    const { src, alt, caption, align } = node.attrs as FigureAttrs;
    const img = ["img", mergeAttributes({ src, alt })];
    return caption ? ["figure", { class: alignClass[align] }, img, ["figcaption", {}, caption]] : ["figure", { class: alignClass[align] }, img];
  },

  addCommands() {
    return {
      setFigure:
        (attrs) =>
        ({ commands }) =>
          // A paragraph after it, with the cursor there, so typing carries on below the picture
          // instead of replacing it.
          commands.insertContent([{ type: this.name, attrs }, { type: "paragraph" }]),
    };
  },

  addNodeView() {
    return ReactNodeViewRenderer(FigureView);
  },
});

function FigureView({ node, selected }: ReactNodeViewProps) {
  const { src, alt, caption, align } = node.attrs as FigureAttrs;
  return (
    <NodeViewWrapper as="figure" className={cn("my-4", align === "left" && "mr-auto max-w-[60%]", align === "right" && "ml-auto max-w-[60%]", align === "center" && "mx-auto max-w-[80%]")} data-drag-handle>
      <img src={siteImage(src) ?? src} alt={alt} className={cn("w-full rounded-input", selected && "ring-2 ring-brand ring-offset-2")} />
      {caption && <figcaption className="mt-1 text-center text-sm text-muted">{caption}</figcaption>}
      {!alt && <p className="mt-1 text-xs text-danger">Add alt text: select the picture and choose “Picture details”.</p>}
    </NodeViewWrapper>
  );
}
