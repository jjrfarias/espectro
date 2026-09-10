import { createHash, randomBytes } from "node:crypto";
import jwt from "jsonwebtoken";
import { env } from "../../config/env.js";

export interface AccessTokenPayload {
  accountId: string;
}

export function signAccessToken(payload: AccessTokenPayload): string {
  return jwt.sign(payload, env.JWT_ACCESS_SECRET, { expiresIn: env.JWT_ACCESS_TTL_SECONDS });
}

export function verifyAccessToken(token: string): AccessTokenPayload {
  const decoded = jwt.verify(token, env.JWT_ACCESS_SECRET);
  if (typeof decoded !== "object" || decoded === null || typeof (decoded as { accountId?: unknown }).accountId !== "string") {
    throw new Error("Token de acesso inválido.");
  }
  return { accountId: (decoded as AccessTokenPayload).accountId };
}

export function hashRefreshToken(token: string): string {
  return createHash("sha256").update(token).digest("hex");
}

export function generateRefreshToken(): { token: string; hash: string; expiresAt: Date } {
  const token = randomBytes(48).toString("base64url");
  const expiresAt = new Date(Date.now() + env.JWT_REFRESH_TTL_DAYS * 24 * 60 * 60 * 1000);
  return { token, hash: hashRefreshToken(token), expiresAt };
}

export const PASSWORD_RESET_TTL_MINUTES = 30;

// Mesmo formato/hash do refresh token (sha256 do valor opaco) — só o nome muda pra deixar claro
// que é um token de uso único e vida curta, não uma sessão.
export function generatePasswordResetToken(): { token: string; hash: string; expiresAt: Date } {
  const token = randomBytes(32).toString("base64url");
  const expiresAt = new Date(Date.now() + PASSWORD_RESET_TTL_MINUTES * 60 * 1000);
  return { token, hash: hashRefreshToken(token), expiresAt };
}
