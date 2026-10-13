"use client";

import { useState } from "react";
import { photoUrl, type PhotoKind } from "@/lib/media/photos";
import { Avatar, cn } from "@/components/ui/primitives";

/**
 * Round photo via the BFF media route, falling back to initials when there's no photo
 * (photoVersion null) or it fails to load. Decorative by default (the name is shown beside it).
 */
export function PhotoAvatar({
  kind,
  id,
  name,
  photoVersion,
  className,
  alt = "",
}: {
  kind: PhotoKind;
  id: string;
  name: string;
  photoVersion: string | null | undefined;
  className?: string;
  alt?: string;
}) {
  const src = photoUrl(kind, id, photoVersion);
  const [failed, setFailed] = useState<string | null>(null);
  if (!src || failed === src) return <Avatar name={name} className={className} />;
  return (
    // Plain <img>: private, already resized server-side; next/image optimisation adds nothing here.
    <img
      src={src}
      alt={alt}
      loading="lazy"
      decoding="async"
      onError={() => setFailed(src)}
      className={cn("inline-block size-9 shrink-0 rounded-full bg-surface-2 object-cover", className)}
    />
  );
}
