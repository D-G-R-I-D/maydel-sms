import { describe, expect, it } from "vitest";
import { isTrustedOrigin, trustedOrigins } from "./origins";

describe("trusted origins", () => {
  it("defaults to VS Code dev tunnels in development and to nothing in production", () => {
    expect(trustedOrigins({ NODE_ENV: "development" })).toEqual(["https://*.devtunnels.ms"]);
    expect(trustedOrigins({ NODE_ENV: "production" })).toEqual([]);
    expect(trustedOrigins({ NODE_ENV: "production", TRUSTED_ORIGINS: "https://Portal.Example.com, " })).toEqual(["https://portal.example.com"]);
  });

  it("matches any subdomain for a *. pattern, so a changing tunnel link keeps working", () => {
    const patterns = ["https://*.devtunnels.ms"];
    expect(isTrustedOrigin("https://abc123-3000.euw.devtunnels.ms", patterns)).toBe(true);
    expect(isTrustedOrigin("https://xyz-3000.uks1.devtunnels.ms", patterns)).toBe(true);
  });

  it("rejects look-alikes, other schemes and garbage", () => {
    const patterns = ["https://*.devtunnels.ms", "https://portal.example.com"];
    expect(isTrustedOrigin("https://evil-devtunnels.ms", patterns)).toBe(false);
    expect(isTrustedOrigin("https://devtunnels.ms.evil.com", patterns)).toBe(false);
    expect(isTrustedOrigin("http://abc.devtunnels.ms", patterns)).toBe(false);
    expect(isTrustedOrigin("https://portal.example.com.evil.com", patterns)).toBe(false);
    expect(isTrustedOrigin("not a url", patterns)).toBe(false);
    expect(isTrustedOrigin("https://portal.example.com", patterns)).toBe(true);
  });
});
