import { describe, expect, it, vi } from "vitest";
import { inlineHref, savePdf, sharePdf, toInline, type SaveEnv } from "./save-pdf";

const blob = new Blob(["%PDF-1.7"], { type: "application/pdf" });
const load = (name: string) => async () => ({ blob, name });
const named = (name: string) => Object.assign(new Error(name), { name });

type TestEnv = SaveEnv & { download: ReturnType<typeof vi.fn> };
function env(over: Partial<SaveEnv> = {}): TestEnv {
  return { isTouch: false, download: vi.fn(), ...over } as TestEnv;
}

function picker(fail?: Error) {
  const write = vi.fn(async () => {});
  const close = vi.fn(async () => {});
  const show = vi.fn(async () => {
    if (fail) throw fail;
    return { createWritable: async () => ({ write, close }) };
  });
  return { show, write, close };
}

describe("savePdf", () => {
  it("uses the Save As dialog on desktop and writes the file", async () => {
    const p = picker();
    const e = env({ showSaveFilePicker: p.show });
    expect(await savePdf(load("Report.pdf"), "Report.pdf", e)).toBe("saved");
    expect(p.show).toHaveBeenCalledWith(expect.objectContaining({ suggestedName: "Report.pdf" }));
    expect(p.write).toHaveBeenCalledWith(blob);
    expect(p.close).toHaveBeenCalled();
    expect(e.download).not.toHaveBeenCalled();
  });

  it("treats a cancelled Save As as a cancel, with no fallback download", async () => {
    const e = env({ showSaveFilePicker: picker(named("AbortError")).show });
    expect(await savePdf(load("Report.pdf"), "Report.pdf", e)).toBe("cancelled");
    expect(e.download).not.toHaveBeenCalled();
  });

  it("falls back to a download when the picker fails otherwise", async () => {
    const e = env({ showSaveFilePicker: picker(named("SecurityError")).show });
    expect(await savePdf(load("Report.pdf"), "Report.pdf", e)).toBe("downloaded");
    expect(e.download).toHaveBeenCalledWith(blob, "Report.pdf");
  });

  it("opens the share sheet on touch devices with a PDF file", async () => {
    const share = vi.fn(async (_data: { files: File[]; title: string }) => {});
    const e = env({ isTouch: true, canShare: () => true, share, showSaveFilePicker: picker().show });
    expect(await savePdf(load("Report.pdf"), "Report.pdf", e)).toBe("shared");
    const arg = share.mock.calls[0][0];
    expect(arg.files[0].name).toBe("Report.pdf");
    expect(arg.files[0].type).toBe("application/pdf");
    expect(e.download).not.toHaveBeenCalled();
  });

  it("a dismissed share sheet is a cancel; an expired tap asks for another", async () => {
    const reject = (n: string) => async () => Promise.reject(named(n));
    expect(await savePdf(load("R.pdf"), "R.pdf", env({ isTouch: true, canShare: () => true, share: reject("AbortError") }))).toBe("cancelled");
    expect(await savePdf(load("R.pdf"), "R.pdf", env({ isTouch: true, canShare: () => true, share: reject("NotAllowedError") }))).toBe("share-blocked");
  });

  it("downloads when files can't be shared, or nothing better exists", async () => {
    const touch = env({ isTouch: true, canShare: () => false, share: vi.fn() });
    expect(await savePdf(load("R.pdf"), "R.pdf", touch)).toBe("downloaded");
    const plain = env();
    expect(await savePdf(load("R.pdf"), "R.pdf", plain)).toBe("downloaded");
    expect(plain.download).toHaveBeenCalledWith(blob, "R.pdf");
  });

  it("downloads when sharing fails for another reason", async () => {
    const e = env({ isTouch: true, canShare: () => true, share: async () => Promise.reject(named("DataError")) });
    expect(await savePdf(load("R.pdf"), "R.pdf", e)).toBe("downloaded");
  });
});

describe("savePdf ordering and errors", () => {
  it("opens the Save As dialog before fetching, and doesn't fetch when it's cancelled", async () => {
    const order: string[] = [];
    const show = vi.fn(async () => {
      order.push("picker");
      throw named("AbortError");
    });
    const loader = vi.fn(async () => {
      order.push("load");
      return { blob, name: "R.pdf" };
    });
    expect(await savePdf(loader, "R.pdf", env({ showSaveFilePicker: show }))).toBe("cancelled");
    expect(order).toEqual(["picker"]);
  });

  it("propagates fetch errors to the caller", async () => {
    const failing = async () => Promise.reject(new Error("offline"));
    await expect(savePdf(failing, "R.pdf", env())).rejects.toThrow("offline");
    await expect(savePdf(failing, "R.pdf", env({ showSaveFilePicker: picker().show }))).rejects.toThrow("offline");
  });

  it("uses the server's file name for downloads", async () => {
    const e = env();
    await savePdf(load("Server name.pdf"), "Fallback.pdf", e);
    expect(e.download).toHaveBeenCalledWith(blob, "Server name.pdf");
  });
});

describe("sharePdf", () => {
  it("rethrows unexpected errors", async () => {
    await expect(sharePdf(new File([blob], "R.pdf"), { share: async () => Promise.reject(named("DataError")) })).rejects.toThrow();
  });
});

describe("inline helpers", () => {
  it("adds inline=1 to the URL", () => {
    expect(inlineHref("/api/media/reports/a/b/pdf")).toBe("/api/media/reports/a/b/pdf?inline=1");
    expect(inlineHref("/api/media/reports/class/pdf?classRoomId=c&termId=t")).toBe("/api/media/reports/class/pdf?classRoomId=c&termId=t&inline=1");
  });
  it("turns an attachment disposition inline, keeping the file name", () => {
    expect(toInline(`attachment; filename="R.pdf"; filename*=UTF-8''R.pdf`)).toBe(`inline; filename="R.pdf"; filename*=UTF-8''R.pdf`);
    expect(toInline(null)).toBe("inline");
  });
});
