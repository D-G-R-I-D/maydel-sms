import "server-only";
import { load } from "@/lib/api/server";
import { loadTerms } from "@/lib/assessment/terms";
import type { ComponentOption } from "./tests";
import type { ArmOption } from "@/components/cbt/test-header-form";

/** What the test header form needs: authoring scopes, terms, active components and class arms. */
export async function loadTestFormData(termId: string | undefined) {
  const [scopes, { terms, selected }, settings, rooms] = await Promise.all([
    load((api) => api.GET("/api/cbt/scopes")),
    loadTerms(termId),
    load((api) => api.GET("/api/assessment/settings")),
    load((api) => api.GET("/api/academics/classrooms")),
  ]);
  const components: ComponentOption[] = settings.ok ? settings.data.components.filter((c) => c.isActive).sort((a, b) => a.order - b.order).map((c) => ({ id: c.id, name: c.name, maxScore: c.maxScore })) : [];
  const arms: ArmOption[] = rooms.ok ? rooms.data.map((r) => ({ id: r.id, name: r.name, classLevelId: r.classLevelId })) : [];
  return { scopes, terms, selected, components, arms };
}
