"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { ContactMessage, MessageStatus, Testimonial, TestimonialInput } from "./cms";

type Api = Parameters<Parameters<typeof load>[0]>[0];

/** For 204 No Content endpoints: any 2xx counts as done. */
function done(call: (api: Api) => Promise<{ response: Response; error?: unknown }>) {
  return load(async (api) => {
    const r = await call(api);
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}

export async function setMessageStatus(id: string, status: MessageStatus): Promise<Loaded<ContactMessage>> {
  const r = await load((api) => api.PUT("/api/cms/messages/{id}/status", { params: { path: { id } }, body: { status } }));
  if (r.ok) revalidatePath("/admin/website/messages");
  return r;
}

export async function saveTestimonial(id: string | null, body: TestimonialInput): Promise<Loaded<Testimonial>> {
  const r = await load((api) =>
    id ? api.PUT("/api/cms/testimonials/{id}", { params: { path: { id } }, body }) : api.POST("/api/cms/testimonials", { body }),
  );
  if (r.ok) revalidatePath("/admin/website/testimonials");
  return r;
}

export async function deleteTestimonial(id: string): Promise<Loaded<true>> {
  const r = await done((api) => api.DELETE("/api/cms/testimonials/{id}", { params: { path: { id } } }));
  if (r.ok) revalidatePath("/admin/website/testimonials");
  return r;
}

export async function unsubscribe(id: string): Promise<Loaded<true>> {
  const r = await done((api) => api.DELETE("/api/cms/subscribers/{id}", { params: { path: { id } } }));
  if (r.ok) revalidatePath("/admin/website/subscribers");
  return r;
}
