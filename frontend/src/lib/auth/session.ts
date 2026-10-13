import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { USER_COOKIE, decodeUser, type SessionUser } from "./cookies";

/** The signed-in user as cached at sign-in/refresh. For display and UX only; the API decides access. */
export async function getSessionUser(): Promise<SessionUser | null> {
  return decodeUser((await cookies()).get(USER_COOKIE)?.value);
}

export async function requireSessionUser(): Promise<SessionUser> {
  const user = await getSessionUser();
  if (!user) redirect("/login");
  return user;
}
