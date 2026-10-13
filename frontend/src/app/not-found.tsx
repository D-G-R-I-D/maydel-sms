import Link from "next/link";
import { Compass } from "lucide-react";
import { buttonClasses } from "@/components/ui/primitives";

export default function NotFound() {
  return (
    <main id="main" className="flex min-h-dvh flex-col items-center justify-center gap-3 px-6 text-center">
      <span className="inline-flex size-12 items-center justify-center rounded-full bg-surface-2 text-muted">
        <Compass className="size-6" aria-hidden />
      </span>
      <h1 className="font-display text-2xl font-semibold">Page not found</h1>
      <p className="max-w-md text-sm text-muted">The page you&apos;re looking for doesn&apos;t exist or has moved.</p>
      <Link href="/" className={buttonClasses("secondary", "md", "mt-2")}>
        Go to the start
      </Link>
    </main>
  );
}
