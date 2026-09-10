import { describe, expect, it } from "vitest";
import { generateRefreshToken, hashRefreshToken, signAccessToken, verifyAccessToken } from "../../src/modules/auth/tokens.js";

describe("access tokens", () => {
  it("round-trips the account id", () => {
    const token = signAccessToken({ accountId: "11111111-1111-1111-1111-111111111111" });
    expect(verifyAccessToken(token)).toEqual({ accountId: "11111111-1111-1111-1111-111111111111" });
  });

  it("rejects a tampered token", () => {
    const token = signAccessToken({ accountId: "11111111-1111-1111-1111-111111111111" });
    expect(() => verifyAccessToken(`${token}tampered`)).toThrow();
  });
});

describe("refresh tokens", () => {
  it("generates a token whose hash matches hashRefreshToken", () => {
    const { token, hash } = generateRefreshToken();
    expect(hashRefreshToken(token)).toBe(hash);
  });

  it("generates unique tokens", () => {
    const a = generateRefreshToken();
    const b = generateRefreshToken();
    expect(a.token).not.toBe(b.token);
  });
});
