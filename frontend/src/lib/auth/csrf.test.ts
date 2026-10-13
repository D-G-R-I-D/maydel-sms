import { describe, expect, it } from "vitest";
import { checkSameSite } from "./csrf";

const ok = (headers: Record<string, string>) => checkSameSite(new Headers(headers)).ok;
const tunnel = "d24c76kv-3000.uks1.devtunnels.ms";

describe("BFF same-site check", () => {
  it("allows plain localhost", () => {
    expect(ok({ origin: "http://localhost:3000", host: "localhost:3000" })).toBe(true);
  });

  it("allows VS Code dev tunnels, which rewrite Origin/Host to localhost and keep the public host in X-Forwarded-Host", () => {
    expect(ok({ origin: "http://localhost:3000", host: "localhost:3000", "x-forwarded-host": tunnel })).toBe(true);
    expect(ok({ origin: `https://${tunnel}`, host: "localhost:3000", "x-forwarded-host": tunnel })).toBe(true);
    expect(ok({ origin: `https://${tunnel}`, host: "localhost:3000" })).toBe(true); // TRUSTED_ORIGINS dev default
  });

  it("allows a production proxy that forwards the public host (Railway/Vercel)", () => {
    expect(ok({ origin: "https://portal.example.com", host: "10.0.0.5:3000", "x-forwarded-host": "portal.example.com" })).toBe(true);
  });

  it("allows requests without an Origin (same-origin GETs, server-to-server)", () => {
    expect(ok({ host: "localhost:3000" })).toBe(true);
  });

  it("blocks another site, opaque and malformed origins", () => {
    for (const origin of ["https://evil.example.com", "null", "not a url", "http://localhost:3000.evil.com"])
      expect(ok({ origin, host: "localhost:3000", "x-forwarded-host": tunnel })).toBe(false);
  });
});
