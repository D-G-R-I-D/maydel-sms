import { describe, expect, it } from "vitest";
import { unreachableProblem } from "./unreachable";

describe("unreachableProblem", () => {
  it("only says offline when the browser is offline", () => {
    expect(unreachableProblem(false).title).toMatch(/offline/);
    expect(unreachableProblem(true).title).toBe("Couldn't reach the server. Reload the page and try again.");
    expect(unreachableProblem(true).status).toBe(0);
  });
});
