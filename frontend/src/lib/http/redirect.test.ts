import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import { publicUrl, redirectTo } from "./redirect";

const req = (headers: Record<string, string> = {}) => new NextRequest("http://localhost:3000/admin", { headers });

describe("redirects behind tunnels and proxies", () => {
  it("route-handler redirects use a relative Location", () => {
    expect(redirectTo("/login?next=%2Fadmin", 303).headers.get("location")).toBe("/login?next=%2Fadmin");
    expect(() => redirectTo("//evil.com")).toThrow();
  });

  it("proxy redirects stay on the trusted public host the browser used", () => {
    const tunnel = req({ "x-forwarded-host": "abc-3000.euw.devtunnels.ms", "x-forwarded-proto": "https" });
    expect(publicUrl(tunnel, "/login").href).toBe("https://abc-3000.euw.devtunnels.ms/login");
  });

  it("ignores an untrusted forwarded host (no open redirect)", () => {
    const spoofed = req({ "x-forwarded-host": "evil.example.com", "x-forwarded-proto": "https" });
    expect(publicUrl(spoofed, "/login").href).toBe("http://localhost:3000/login");
    expect(publicUrl(req(), "/login").href).toBe("http://localhost:3000/login");
  });
});
