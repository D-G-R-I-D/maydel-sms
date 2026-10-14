import { RouteLoader } from "@/components/ui/school-loader";

/** Whole-page wait in the console; sections with tables keep their own skeletons. */
export default function Loading() {
  return <RouteLoader />;
}
