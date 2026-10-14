import { LoaderStatus } from "@/components/ui/school-loader";

/** Sign-in / password pages: the crest loader in place of the form, shown after ~200 ms. */
export default function Loading() {
  return (
    <div aria-busy="true" className="loader-delayed flex justify-center py-16">
      <LoaderStatus size="lg" />
    </div>
  );
}
