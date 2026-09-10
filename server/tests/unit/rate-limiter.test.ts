import { describe, expect, it } from "vitest";
import { FixedWindowRateLimiter } from "../../src/modules/world/rate-limiter.js";

describe("FixedWindowRateLimiter", () => {
  it("allows up to the limit within the window", () => {
    const limiter = new FixedWindowRateLimiter(3, 1000, 0);
    expect(limiter.tryConsume(0)).toBe(true);
    expect(limiter.tryConsume(0)).toBe(true);
    expect(limiter.tryConsume(0)).toBe(true);
    expect(limiter.tryConsume(0)).toBe(false);
  });

  it("resets the count after the window elapses", () => {
    const limiter = new FixedWindowRateLimiter(1, 1000, 0);
    expect(limiter.tryConsume(0)).toBe(true);
    expect(limiter.tryConsume(500)).toBe(false);
    expect(limiter.tryConsume(1000)).toBe(true);
  });
});
