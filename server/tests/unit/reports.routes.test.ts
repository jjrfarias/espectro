import { randomUUID } from "node:crypto";
import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { signAccessToken } from "../../src/modules/auth/tokens.js";
import { buildApp } from "../../src/transport/http.js";
import { pool } from "../../src/persistence/db.js";

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: vi.fn(), query: vi.fn() },
}));

// docs/ARQUITETURA-MVP.md §10: a tabela `reports` já existia (005_chat.ts); este teste cobre só
// o endpoint HTTP `/reports` que passou a gravar nela.
describe("POST /reports", () => {
  let app: FastifyInstance;
  const reporterAccountId = "reporter-account";
  const targetCharacterId = randomUUID();
  const targetAccountId = "target-account";
  let token: string;

  beforeEach(async () => {
    vi.resetAllMocks();
    token = signAccessToken({ accountId: reporterAccountId });
    app = await buildApp();
    await app.ready();
  });

  afterEach(async () => {
    await app.close();
  });

  it("records a report against another character and returns 201", async () => {
    vi.mocked(pool.query).mockImplementation(async (sql: unknown) => {
      const text = sql as string;
      if (text.startsWith("select account_id from characters")) {
        return { rowCount: 1, rows: [{ account_id: targetAccountId }] } as never;
      }
      if (text.startsWith("insert into reports")) {
        return { rowCount: 1, rows: [] } as never;
      }
      throw new Error(`Unexpected query: ${text}`);
    });

    const response = await app.inject({
      method: "POST",
      url: "/reports",
      headers: { authorization: `Bearer ${token}` },
      payload: { targetCharacterId, reason: "Linguagem ofensiva no chat global." },
    });

    expect(response.statusCode).toBe(201);
    expect(response.json()).toEqual({ status: "open" });
    const insertCall = vi.mocked(pool.query).mock.calls.find(([sql]) => (sql as string).startsWith("insert into reports"));
    expect(insertCall?.[1]).toEqual([reporterAccountId, targetAccountId, null, "Linguagem ofensiva no chat global."]);
  });

  it("rejects a report without a valid access token", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/reports",
      payload: { targetCharacterId, reason: "teste" },
    });
    expect(response.statusCode).toBe(401);
    expect(pool.query).not.toHaveBeenCalled();
  });

  it("rejects an unknown target character", async () => {
    vi.mocked(pool.query).mockResolvedValue({ rowCount: 0, rows: [] } as never);

    const response = await app.inject({
      method: "POST",
      url: "/reports",
      headers: { authorization: `Bearer ${token}` },
      payload: { targetCharacterId, reason: "teste" },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json()).toMatchObject({ error: "TARGET_NOT_FOUND" });
  });

  it("rejects self-reports", async () => {
    vi.mocked(pool.query).mockResolvedValue({ rowCount: 1, rows: [{ account_id: reporterAccountId }] } as never);

    const response = await app.inject({
      method: "POST",
      url: "/reports",
      headers: { authorization: `Bearer ${token}` },
      payload: { targetCharacterId, reason: "teste" },
    });

    expect(response.statusCode).toBe(400);
    expect(response.json()).toMatchObject({ error: "CANNOT_REPORT_SELF" });
  });

  it("rejects a missing reason", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/reports",
      headers: { authorization: `Bearer ${token}` },
      payload: { targetCharacterId, reason: "" },
    });

    expect(response.statusCode).toBe(400);
    expect(response.json()).toMatchObject({ error: "VALIDATION_ERROR" });
    expect(pool.query).not.toHaveBeenCalled();
  });
});
