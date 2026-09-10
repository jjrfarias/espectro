import { randomUUID } from "node:crypto";
import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { signAccessToken } from "../../src/modules/auth/tokens.js";
import { buildApp } from "../../src/transport/http.js";
import { pool } from "../../src/persistence/db.js";

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: vi.fn(), query: vi.fn() },
}));

// ESPECTRO-VISAO.md §9 "Raça não é classe": POST /characters aceita race/gender puramente
// cosméticos (guardados em appearance_json), com fallback humano/masculino pra não quebrar
// clientes que ainda não os enviam.
describe("POST /characters — raça e gênero", () => {
  let app: FastifyInstance;
  const accountId = "account-1";
  const characterId = randomUUID();
  let token: string;

  beforeEach(async () => {
    vi.resetAllMocks();
    token = signAccessToken({ accountId });
    app = await buildApp();
    await app.ready();
  });

  afterEach(async () => {
    await app.close();
  });

  function mockConnection(row: Record<string, unknown>) {
    const client = { query: vi.fn(), release: vi.fn() };
    client.query.mockImplementation(async (sql: unknown) => {
      const text = sql as string;
      if (text.startsWith("insert into characters")) return { rowCount: 1, rows: [row] } as never;
      return { rowCount: 1, rows: [] } as never;
    });
    vi.mocked(pool.connect).mockResolvedValue(client as never);
    return client;
  }

  it("stores the chosen race and gender in appearance_json and returns them", async () => {
    const client = mockConnection({
      id: characterId,
      account_id: accountId,
      name: "Aldric",
      level: 1,
      xp: 0,
      hp: 150,
      position_json: { x: 0, y: 0, z: 0, facingY: 0 },
      version: 1,
      appearance_json: { race: "elfo", gender: "feminino" },
    });

    const response = await app.inject({
      method: "POST",
      url: "/characters",
      headers: { authorization: `Bearer ${token}` },
      payload: { name: "Aldric", race: "elfo", gender: "feminino" },
    });

    expect(response.statusCode).toBe(201);
    expect(response.json()).toMatchObject({ race: "elfo", gender: "feminino" });
    const insertCall = client.query.mock.calls.find(([sql]) => (sql as string).startsWith("insert into characters"));
    expect(insertCall?.[1]).toEqual([accountId, "Aldric", 150, JSON.stringify({ race: "elfo", gender: "feminino" })]);
  });

  it("defaults to humano/masculino when omitted", async () => {
    mockConnection({
      id: characterId,
      account_id: accountId,
      name: "Aldric",
      level: 1,
      xp: 0,
      hp: 150,
      position_json: { x: 0, y: 0, z: 0, facingY: 0 },
      version: 1,
      appearance_json: { race: "humano", gender: "masculino" },
    });

    const response = await app.inject({
      method: "POST",
      url: "/characters",
      headers: { authorization: `Bearer ${token}` },
      payload: { name: "Aldric" },
    });

    expect(response.statusCode).toBe(201);
    expect(response.json()).toMatchObject({ race: "humano", gender: "masculino" });
  });

  it("rejects an invalid race", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/characters",
      headers: { authorization: `Bearer ${token}` },
      payload: { name: "Aldric", race: "vampiro" },
    });

    expect(response.statusCode).toBe(400);
    expect(response.json()).toMatchObject({ error: "VALIDATION_ERROR" });
    expect(pool.connect).not.toHaveBeenCalled();
  });

  it("falls back to humano/masculino for a character created before this field existed", async () => {
    mockConnection({
      id: characterId,
      account_id: accountId,
      name: "Farias",
      level: 3,
      xp: 20,
      hp: 150,
      position_json: { x: 0, y: 0, z: 0, facingY: 0 },
      version: 4,
      appearance_json: {},
    });

    const response = await app.inject({
      method: "POST",
      url: "/characters",
      headers: { authorization: `Bearer ${token}` },
      payload: { name: "Farias" },
    });

    expect(response.json()).toMatchObject({ race: "humano", gender: "masculino" });
  });
});
