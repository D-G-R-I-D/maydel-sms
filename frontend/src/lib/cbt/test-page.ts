import "server-only";
import { notFound } from "next/navigation";
import { load, type Loaded } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import type { TestDetail } from "./tests";

/**
 * The start of every test sub-page: staff only (null = show access denied), a valid id, and the test
 * (404 → not found; other failures come back as the problem to show).
 */
export async function loadTestForPage(id: string): Promise<null | Loaded<TestDetail>> {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) return null;
  if (!GUID.test(id)) notFound();
  const test = await load((api) => api.GET("/api/cbt/tests/{id}", { params: { path: { id } } }));
  if (!test.ok && test.problem.status === 404) notFound();
  return test;
}
