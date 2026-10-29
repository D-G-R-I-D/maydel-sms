"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { Newsletter, NewsletterInput, RecipientCount } from "./newsletters";

type Api = Parameters<Parameters<typeof load>[0]>[0];

/** For 204 No Content endpoints: any 2xx counts as done. */
function done(call: (api: Api) => Promise<{ response: Response; error?: unknown }>) {
  return load(async (api) => {
    const r = await call(api);
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}

function newsletters<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) revalidatePath("/admin/website/newsletters", "layout");
  return r;
}

/** E12.6 admin: create or update a draft. */
export async function saveNewsletter(id: string | null, body: NewsletterInput): Promise<Loaded<Newsletter>> {
  return newsletters(await load((api) => (id ? api.PUT("/api/cms/newsletters/{id}", { params: { path: { id } }, body }) : api.POST("/api/cms/newsletters", { body }))));
}

export async function deleteNewsletter(id: string): Promise<Loaded<true>> {
  return newsletters(await done((api) => api.DELETE("/api/cms/newsletters/{id}", { params: { path: { id } } })));
}

/** How many people a saved draft would reach (parents, staff, subscribers; duplicates merged). */
export async function countRecipients(id: string): Promise<Loaded<RecipientCount>> {
  return load((api) => api.GET("/api/cms/newsletters/{id}/recipients", { params: { path: { id } } }));
}

/** One "[Test]" copy, to the admin's own address unless another is given. */
export async function sendTest(id: string, email: string | null): Promise<Loaded<true>> {
  return done((api) => api.POST("/api/cms/newsletters/{id}/test", { params: { path: { id } }, body: { email } }));
}

/** Now (at null) or at a WAT time up to 90 days ahead. */
export async function sendNewsletter(id: string, at: string | null): Promise<Loaded<Newsletter>> {
  return newsletters(await load((api) => api.POST("/api/cms/newsletters/{id}/send", { params: { path: { id } }, body: { at } })));
}

/** A scheduled newsletter back to draft. */
export async function cancelNewsletter(id: string): Promise<Loaded<Newsletter>> {
  return newsletters(await load((api) => api.POST("/api/cms/newsletters/{id}/cancel", { params: { path: { id } } })));
}
