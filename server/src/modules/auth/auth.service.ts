import { pool } from "../../persistence/db.js";
import { hashPassword, verifyPassword } from "./password.js";
import { generatePasswordResetToken, generateRefreshToken, hashRefreshToken, signAccessToken } from "./tokens.js";

export class AuthError extends Error {
  constructor(
    public readonly code: "EMAIL_TAKEN" | "INVALID_CREDENTIALS" | "INVALID_REFRESH_TOKEN" | "INVALID_RESET_TOKEN",
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

/**
 * docs/REFERENCIAS-INTERFACE-JOGABILIDADE.md, plano de evolução §2 "Esqueci a senha": a resposta
 * é sempre a mesma pro chamador, exista ou não a conta — evita que alguém descubra e-mails
 * cadastrados testando esse endpoint (enumeração de contas). Só devolve o token de verdade pra
 * quem chamou decidir o que fazer com ele (rota HTTP); nunca é a resposta HTTP em produção.
 */
export async function requestPasswordReset(email: string): Promise<{ token: string | null }> {
  const emailNormalized = normalizeEmail(email);
  const result = await pool.query<{ id: string }>(
    `select id from accounts where email_normalized = $1 and status = 'active'`,
    [emailNormalized],
  );
  const row = result.rows[0];
  if (!row) return { token: null };

  const { token, hash, expiresAt } = generatePasswordResetToken();
  await pool.query(`insert into password_reset_tokens (account_id, token_hash, expires_at) values ($1, $2, $3)`, [
    row.id,
    hash,
    expiresAt,
  ]);
  return { token };
}

/**
 * Token de uso único: `for update` trava a linha contra duas confirmações simultâneas com o
 * mesmo token. Trocar a senha revoga todas as sessões da conta — GDD/plano de evolução exige que
 * uma sessão comprometida não sobreviva à troca de senha.
 */
export async function resetPassword(token: string, newPassword: string): Promise<void> {
  const tokenHash = hashRefreshToken(token);
  const client = await pool.connect();
  try {
    await client.query("begin");
    const result = await client.query<{ id: string; account_id: string; expires_at: Date; used_at: Date | null }>(
      `select id, account_id, expires_at, used_at from password_reset_tokens where token_hash = $1 for update`,
      [tokenHash],
    );
    const row = result.rows[0];
    if (!row || row.used_at || row.expires_at.getTime() < Date.now()) {
      await client.query("rollback");
      throw new AuthError("INVALID_RESET_TOKEN", "Link de redefinição inválido, expirado ou já usado.");
    }
    const passwordHash = await hashPassword(newPassword);
    await client.query(`update accounts set password_hash = $2 where id = $1`, [row.account_id, passwordHash]);
    await client.query(`update password_reset_tokens set used_at = now() where id = $1`, [row.id]);
    await client.query(`update sessions set revoked_at = now() where account_id = $1 and revoked_at is null`, [
      row.account_id,
    ]);
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }
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
