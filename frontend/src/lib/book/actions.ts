"use server";

import { load, type Loaded } from "@/lib/api/server";
import type { BookEntry, BookPage } from "./book";

/** E17.2: older notes, before the first one on screen. */
export async function loadOlder(studentId: string, before: string): Promise<Loaded<BookPage>> {
  return load((api) => api.GET("/api/books/{studentId}", { params: { path: { studentId }, query: { before } } }));
}

/** Correct your own note within 15 minutes (422 book.edit_window after). */
export async function editEntry(studentId: string, entryId: string, body: string): Promise<Loaded<BookEntry>> {
  return load((api) => api.PUT("/api/books/{studentId}/entries/{entryId}", { params: { path: { studentId, entryId } }, body: { body } }));
}

/** Clears your unread mark for this book; called when it's open. */
export async function markBookRead(studentId: string): Promise<void> {
  await load(async (api) => {
    const r = await api.POST("/api/books/{studentId}/read", { params: { path: { studentId } } });
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}
