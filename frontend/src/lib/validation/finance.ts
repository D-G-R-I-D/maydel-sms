import { z } from "zod";
import { parseAmount } from "@/lib/finance/rules";

/** Client mirror of the E10 fee item rules (FeeRules.CheckFeeItem + validator). The API re-checks. */
export const MAX_FEE_AMOUNT = 100_000_000;

export const feeItemSchema = z.object({
  name: z.string().trim().min(1, "Give the fee a name.").max(80, "Use 80 characters or fewer."),
  amount: z
    .string()
    .transform((v, ctx) => {
      const n = parseAmount(v);
      if (n === null || n <= 0 || n > MAX_FEE_AMOUNT) {
        ctx.addIssue({ code: "custom", message: "Enter an amount above ₦0 with at most 2 decimal places." });
        return z.NEVER;
      }
      return n;
    }),
  classLevelId: z.string().transform((v) => (v === "" ? null : v)),
  isMandatory: z.boolean(),
  siblingDiscount: z.boolean(),
});
export type FeeItemFormValues = z.input<typeof feeItemSchema>;
export type FeeItemPayload = z.output<typeof feeItemSchema>;
