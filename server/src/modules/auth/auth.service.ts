import { pool } from "../../persistence/db.js";
import { hashPassword, verifyPassword } from "./password.js";
import { generateRefreshToken, hashRefreshToken, signAccessToken } from "./tokens.js";

export class AuthError extends Error {
  constructor(
    public readonly code: "EMAIL_TAKEN" | "INVALID_CREDENTIALS" | "INVALID_REFRESH_TOKEN",
    message: string,
  ) {
    super(message);
  }
}

export interface Session {
  accessToken: string;
  refreshToken: string;
}

function normalizeEmail(email: string): string {
  return email.trim().toLowerCase();
}

export async function registerAccount(email: string, password: string): Promise<Session> {
  const emailNormalized = normalizeEmail(email);
  const passwordHash = await hashPassword(password);
  const result = await pool.query<{ id: string }>(
    `insert into accounts (email_normalized, password_hash) values ($1, $2)
     on conflict (email_normalized) do nothing
     returning id`,
    [emailNormalized, passwordHash],
  );
  if (result.rowCount === 0) {
    throw new AuthError("EMAIL_TAKEN", "Este e-mail já está cadastrado.");
  }
  return issueSession(result.rows[0].id);
}

export async function login(email: string, password: string): Promise<Session> {
  const emailNormalized = normalizeEmail(email);
  const result = await pool.query<{ id: string; password_hash: string; status: string }>(
    `select id, password_hash, status from accounts where email_normalized = $1`,
    [emailNormalized],
  );
  const row = result.rows[0];
  if (!row || row.status !== "active" || !(await verifyPassword(password, row.password_hash))) {
    throw new AuthError("INVALID_CREDENTIALS", "E-mail ou senha inválidos.");
  }
  return issueSession(row.id);
}

export async function refreshSession(refreshToken: string): Promise<Session> {
  const tokenHash = hashRefreshToken(refreshToken);
  const result = await pool.query<{ id: string; account_id: string; expires_at: Date; revoked_at: Date | null }>(
    `select id, account_id, expires_at, revoked_at from sessions where refresh_token_hash = $1`,
    [tokenHash],
  );
  const row = result.rows[0];
  if (!row || row.revoked_at || row.expires_at.getTime() < Date.now()) {
    throw new AuthError("INVALID_REFRESH_TOKEN", "Sessão inválida ou expirada.");
  }
  await pool.query(`update sessions set revoked_at = now() where id = $1`, [row.id]);
  return issueSession(row.account_id);
}

export async function logout(refreshToken: string): Promise<void> {
  const tokenHash = hashRefreshToken(refreshToken);
  await pool.query(
    `update sessions set revoked_at = now() where refresh_token_hash = $1 and revoked_at is null`,
    [tokenHash],
  );
}

async function issueSession(accountId: string): Promise<Session> {
  const accessToken = signAccessToken({ accountId });
  const { token: refreshToken, hash, expiresAt } = generateRefreshToken();
  await pool.query(`insert into sessions (account_id, refresh_token_hash, expires_at) values ($1, $2, $3)`, [
    accountId,
    hash,
    expiresAt,
  ]);
  return { accessToken, refreshToken };
}
