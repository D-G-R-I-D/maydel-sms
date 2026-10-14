"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import { cn } from "@/components/ui/primitives";

/**
 * The school's uploaded logo (E2.6, public via the BFF) with the bundled crest as fallback.
 * The crest shows until the logo has loaded, so a school without one never sees a flash or a broken image.
 */
export function SchoolLogo({ width, height, priority, className }: { width: number; height: number; priority?: boolean; className?: string }) {
  const img = useRef<HTMLImageElement>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    // The image may finish loading before hydration attaches onLoad.
    const el = img.current;
    if (el?.complete && el.naturalWidth > 0) setLoaded(true);
  }, []);

  return (
    <span className={cn("relative inline-flex shrink-0 items-center justify-center", className)}>
      {!loaded && <Image src="/brand/crest.svg" alt="" width={width} height={height} priority={priority} />}
      {/* Served by our BFF route; next/image would add a second proxy hop. */}
      <img
        ref={img}
        src="/api/media/school/logo"
        alt=""
        width={width}
        height={height}
        onLoad={() => setLoaded(true)}
        className={cn("object-contain", loaded ? "block" : "hidden")}
      />
    </span>
  );
}
