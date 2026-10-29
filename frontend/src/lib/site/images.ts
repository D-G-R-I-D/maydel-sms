/**
 * The API's public image URL (/api/public/site/images/{kind}/{id}?v=…) → the site's same-origin proxy,
 * /api/site/images/{kind}/{id}/{v}. The browser never calls the API; the version moves into the path.
 */
export function siteImage(url: string | null | undefined): string | null {
  if (!url) return null;
  const m = /\/api\/public\/site\/images\/(news|gallery|staff|media|store)\/([0-9a-f-]{36})(?:\?v=([\w.-]{1,64}))?/i.exec(url);
  return m ? `/api/site/images/${m[1]}/${m[2]}/${m[3] ?? "0"}` : null;
}

/**
 * Formatted content (posts, events): the server keeps only images from its own store, as
 * src="/api/public/site/images/media/{id}?v=…". Point those at the same-origin proxy; leave the rest alone.
 */
export function rewriteContentImages(html: string): string {
  return html.replace(/(<img\b[^>]*?\ssrc=)(["'])([^"']*)\2/gi, (all, pre: string, q: string, src: string) => {
    const mapped = siteImage(src.replace(/&amp;/g, "&"));
    return mapped ? `${pre}${q}${mapped}${q}` : all;
  });
}

/** The reverse, for the editor: proxied srcs back to the API's public form before saving. */
export function apiImageSrc(src: string): string {
  const m = /^\/api\/site\/images\/(news|gallery|staff|media|store)\/([0-9a-f-]{36})\/([\w.-]{1,64})$/i.exec(src);
  return m ? `/api/public/site/images/${m[1]}/${m[2]}?v=${m[3]}` : src;
}
