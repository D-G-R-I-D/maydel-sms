"use client";

import { useState } from "react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { saveStoreSettings } from "@/lib/store/actions";
import type { StoreSettings } from "@/lib/store/store";
import { Button, Card, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

/** E19: whether parents may put shop orders on the term's fees, and how long unpaid orders hold stock. */
export function StoreSettingsForm({ initial }: { initial: StoreSettings }) {
  const [fees, setFees] = useState(initial.parentsCanChargeToFees);
  const [hours, setHours] = useState(String(initial.holdHours));
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const hoursErr = /^\d{1,3}$/.test(hours) && Number(hours) >= 1 && Number(hours) <= 336 ? undefined : "Between 1 and 336 hours (two weeks).";

  return (
    <Card className="max-w-2xl space-y-5 p-5">
      {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
      <div className="flex items-start gap-3">
        <Switch id="ss-fees" label="Parents can add orders to fees" checked={fees} onCheckedChange={setFees} className="mt-0.5" />
        <div className="text-sm">
          <p className="font-medium">Parents can add orders to fees</p>
          <p className="text-muted">The order goes on the child&apos;s current-term invoice. Off: parents pay at the office when they collect.</p>
        </div>
      </div>
      <Field id="ss-hold" label="Hold unpaid orders for (hours)" error={hoursErr} hint="After this, an unpaid parent order is cancelled and the stock goes back on the shelf.">
        <Input value={hours} onChange={(e) => setHours(e.target.value)} inputMode="numeric" className="tabular w-28" {...describedBy("ss-hold", hoursErr, true)} />
      </Field>
      <div className="flex justify-end">
        <Button
          loading={busy}
          loadingText="Saving…"
          disabled={Boolean(hoursErr)}
          onClick={async () => {
            setBusy(true);
            setProblem(null);
            await runAction(() => saveStoreSettings({ parentsCanChargeToFees: fees, holdHours: Number(hours) }), { success: "Store settings saved", onProblem: setProblem });
            setBusy(false);
          }}
        >
          Save settings
        </Button>
      </div>
    </Card>
  );
}
