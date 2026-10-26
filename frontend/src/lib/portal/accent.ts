/**
 * One colour per child in the parent portal, so brothers and sisters are easy to tell apart at a glance.
 * By the child's position in the family list (stable across pages); full class names for Tailwind. Text
 * stays in the theme's text colour so it reads in light and dark.
 */
const accents = [
  { border: "border-l-brand", ring: "ring-brand", chip: "bg-brand/10", dot: "bg-brand" },
  { border: "border-l-gold", ring: "ring-gold", chip: "bg-gold/20", dot: "bg-gold" },
  { border: "border-l-success", ring: "ring-success", chip: "bg-success/15", dot: "bg-success" },
  { border: "border-l-[#8b5cf6]", ring: "ring-[#8b5cf6]", chip: "bg-[#8b5cf6]/15", dot: "bg-[#8b5cf6]" },
] as const;

export type ChildAccent = (typeof accents)[number];

export function childAccent(index: number): ChildAccent {
  return accents[((index % accents.length) + accents.length) % accents.length];
}
