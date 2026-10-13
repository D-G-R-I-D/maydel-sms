import { LogOut } from "lucide-react";
import { buttonClasses, cn } from "@/components/ui/primitives";

/** A plain form POST: works before hydration and without JS. The route clears cookies and redirects to /login. */
export function SignOutButton({ variant = "ghost", className }: { variant?: "ghost" | "link"; className?: string }) {
  return (
    <form action="/api/auth/logout" method="post" className={className}>
      <button type="submit" className={cn(buttonClasses(variant, "md"), variant === "ghost" && "w-full justify-start")}>
        <LogOut className="size-4" aria-hidden />
        Sign out
      </button>
    </form>
  );
}
