/**
 * Page-wide "unsaved changes" registry. Editors with unsaved edits register here; the DraftScope warns
 * before the tab unloads or an in-app link is followed while any are registered.
 */
const dirty = new Set<string>();

export function setUnsaved(id: string, unsaved: boolean) {
  if (unsaved) dirty.add(id);
  else dirty.delete(id);
}

export function hasUnsaved(): boolean {
  return dirty.size > 0;
}
