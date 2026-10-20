/**
 * The API's public image URL (/api/public/site/images/{kind}/{id}?v=…) → the site's same-origin proxy,
 * /api/site/images/{kind}/{id}/{v}. The browser never calls the API; the version moves into the path.
 */
export function siteImage(url: string | null | undefined): string | null {
  if (!url) return null;
  const m = /\/api\/public\/site\/images\/(news|gallery|staff)\/([0-9a-f-]{36})(?:\?v=([\w.-]{1,64}))?/i.exec(url);
  return m ? `/api/site/images/${m[1]}/${m[2]}/${m[3] ?? "0"}` : null;
}
