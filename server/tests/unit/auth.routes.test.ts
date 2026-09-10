import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { buildApp } from "../../src/transport/http.js";
import { pool } from "../../src/persistence/db.js";

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: vi.fn(), query: vi.fn() },
}));

// docs/REFERENCIAS-INTERFACE-JOGABILIDADE.md, plano de evolução §2 "Esqueci a senha": resposta
// sempre neutra (nunca revela se a conta existe); devToken só aparece fora de produção, pra dar
// pra testar o fluxo de ponta a ponta sem um provedor de e-mail real ainda configurado.
describe("POST /auth/password-reset", () => {
  let app: FastifyInstance;
  const originalNodeEnv = process.env.NODE_ENV;

  beforeEach(async () => {
    vi.resetAllMocks();
    app = await buildApp();
    await app.ready();
  });

  afterEach(async () => {
    await app.close();
    process.env.NODE_ENV = originalNodeEnv;
  });

  describe("/request", () => {
    it("returns the same neutral response for an existing account, with devToken outside production", async () => {
      process.env.NODE_ENV = "development";
      vi.mocked(pool.query).mockImplementation(async (sql: unknown) => {
        const text = sql as string;
        if (text.startsWith("select id from accounts")) return { rowCount: 1, rows: [{ id: "account-1" }] } as never;
        if (text.startsWith("insert into password_reset_tokens")) return { rowCount: 1, rows: [] } as never;
        throw new Error(`Unexpected query: ${text}`);
      });

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/request",
        payload: { email: "existe@example.com" },
      });

      expect(response.statusCode).toBe(200);
      const body = response.json();
      expect(body.status).toBe("ok");
      expect(typeof body.devToken).toBe("string");
      expect(body.devToken.length).toBeGreaterThan(0);
    });

    it("returns the identical response for a non-existent account (no account enumeration)", async () => {
      process.env.NODE_ENV = "development";
      vi.mocked(pool.query).mockResolvedValue({ rowCount: 0, rows: [] } as never);

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/request",
        payload: { email: "nao-existe@example.com" },
      });

      expect(response.statusCode).toBe(200);
      expect(response.json()).toEqual({ status: "ok" });
      const insertCall = vi.mocked(pool.query).mock.calls.find(([sql]) => (sql as string).startsWith("insert into password_reset_tokens"));
      expect(insertCall).toBeUndefined();
    });

    it("never includes devToken in production, even for an existing account", async () => {
      process.env.NODE_ENV = "production";
      vi.mocked(pool.query).mockImplementation(async (sql: unknown) => {
        const text = sql as string;
        if (text.startsWith("select id from accounts")) return { rowCount: 1, rows: [{ id: "account-1" }] } as never;
        return { rowCount: 1, rows: [] } as never;
      });

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/request",
        payload: { email: "existe@example.com" },
      });

      expect(response.json()).toEqual({ status: "ok" });
    });

    it("rejects an invalid email with VALIDATION_ERROR", async () => {
      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/request",
        payload: { email: "not-an-email" },
      });
      expect(response.statusCode).toBe(400);
      expect(response.json()).toMatchObject({ error: "VALIDATION_ERROR" });
      expect(pool.query).not.toHaveBeenCalled();
    });
  });

  describe("/confirm", () => {
    function mockClient(row: Record<string, unknown> | undefined) {
      const client = { query: vi.fn(), release: vi.fn() };
      client.query.mockImplementation(async (sql: unknown) => {
        const text = sql as string;
        if (text.startsWith("select id, account_id, expires_at, used_at from password_reset_tokens")) {
          return { rowCount: row ? 1 : 0, rows: row ? [row] : [] } as never;
        }
        return { rowCount: 1, rows: [] } as never;
      });
      vi.mocked(pool.connect).mockResolvedValue(client as never);
      return client;
    }

    it("resets the password, marks the token used, and revokes active sessions", async () => {
      const client = mockClient({
        id: "token-1",
        account_id: "account-1",
        expires_at: new Date(Date.now() + 60_000),
        used_at: null,
      });

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/confirm",
        payload: { token: "a-valid-token", password: "novaSenhaForte123" },
      });

      expect(response.statusCode).toBe(204);
      const queries = client.query.mock.calls.map(([sql]) => sql as string);
      expect(queries.some((sql) => sql.startsWith("update accounts set password_hash"))).toBe(true);
      expect(queries.some((sql) => sql.startsWith("update password_reset_tokens set used_at"))).toBe(true);
      expect(queries.some((sql) => sql.includes("update sessions set revoked_at"))).toBe(true);
      expect(queries[0]).toBe("begin");
      expect(queries.at(-1)).toBe("commit");
    });

    it("rejects an expired token", async () => {
      mockClient({
        id: "token-1",
        account_id: "account-1",
        expires_at: new Date(Date.now() - 60_000),
        used_at: null,
      });

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/confirm",
        payload: { token: "expired-token", password: "novaSenhaForte123" },
      });

      expect(response.statusCode).toBe(401);
      expect(response.json()).toMatchObject({ error: "INVALID_RESET_TOKEN" });
    });

    it("rejects a token that was already used", async () => {
      mockClient({
        id: "token-1",
        account_id: "account-1",
        expires_at: new Date(Date.now() + 60_000),
        used_at: new Date(),
      });

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/confirm",
        payload: { token: "used-token", password: "novaSenhaForte123" },
      });

      expect(response.statusCode).toBe(401);
      expect(response.json()).toMatchObject({ error: "INVALID_RESET_TOKEN" });
    });

    it("rejects an unknown token", async () => {
      mockClient(undefined);

      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/confirm",
        payload: { token: "unknown-token", password: "novaSenhaForte123" },
      });

      expect(response.statusCode).toBe(401);
      expect(response.json()).toMatchObject({ error: "INVALID_RESET_TOKEN" });
    });

    it("rejects a password shorter than 8 characters", async () => {
      const response = await app.inject({
        method: "POST",
        url: "/auth/password-reset/confirm",
        payload: { token: "a-valid-token", password: "curta" },
      });
      expect(response.statusCode).toBe(400);
      expect(response.json()).toMatchObject({ error: "VALIDATION_ERROR" });
      expect(pool.connect).not.toHaveBeenCalled();
    });
  });
});
