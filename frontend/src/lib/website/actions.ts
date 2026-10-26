"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { ContactMessage, MessageStatus, Testimonial, TestimonialInput } from "./cms";
import type { Post, PostInput, SiteEvent, SiteEventInput } from "./posts";
import type { components } from "@/lib/api/schema";

type GalleryPhoto = components["schemas"]["GalleryPhotoDto"];
type PhotoInput = components["schemas"]["PhotoInput"];

/** Gallery photo details (title, caption, album, order, featured on the homepage carousel). */
export async function saveGalleryPhoto(id: string, body: PhotoInput): Promise<Loaded<GalleryPhoto>> {
  const r = await load((api) => api.PUT("/api/cms/gallery/{id}", { params: { path: { id } }, body }));
  if (r.ok) revalidatePath("/admin/website/gallery");
  return r;
}

export async function deleteGalleryPhoto(id: string): Promise<Loaded<true>> {
  const r = await done((api) => api.DELETE("/api/cms/gallery/{id}", { params: { path: { id } } }));
  if (r.ok) revalidatePath("/admin/website/gallery");
  return r;
}


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

function posts<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) revalidatePath("/admin/website/news", "layout");
  return r;
}

export async function savePost(id: string | null, body: PostInput): Promise<Loaded<Post>> {
  return posts(await load((api) => (id ? api.PUT("/api/cms/posts/{id}", { params: { path: { id } }, body }) : api.POST("/api/cms/posts", { body }))));
}

export async function publishPost(id: string): Promise<Loaded<Post>> {
  return posts(await load((api) => api.POST("/api/cms/posts/{id}/publish", { params: { path: { id } } })));
}

export async function unpublishPost(id: string): Promise<Loaded<Post>> {
  return posts(await load((api) => api.POST("/api/cms/posts/{id}/unpublish", { params: { path: { id } } })));
}

export async function deletePost(id: string): Promise<Loaded<true>> {
  return posts(await done((api) => api.DELETE("/api/cms/posts/{id}", { params: { path: { id } } })));
}

function events<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) revalidatePath("/admin/website/events");
  return r;
}

export async function saveEvent(id: string | null, body: SiteEventInput): Promise<Loaded<SiteEvent>> {
  return events(await load((api) => (id ? api.PUT("/api/cms/events/{id}", { params: { path: { id } }, body }) : api.POST("/api/cms/events", { body }))));
}

export async function deleteEvent(id: string): Promise<Loaded<true>> {
  return events(await done((api) => api.DELETE("/api/cms/events/{id}", { params: { path: { id } } })));
}
