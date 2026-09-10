import { randomUUID } from "node:crypto";
import type { FastifyInstance } from "fastify";
import WebSocket from "ws";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { signAccessToken } from "../../src/modules/auth/tokens.js";
import { buildApp } from "../../src/transport/http.js";
import { pool } from "../../src/persistence/db.js";
import { defaultInstance } from "../../src/modules/world/instance.js";
import { tickPlayerRespawns } from "../../src/modules/combat/combat.service.js";

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: vi.fn(), query: vi.fn(async () => ({ rowCount: 1, rows: [] })) },
}));

const characterRow = {
  id: randomUUID(),
  account_id: "test-account",
  name: "Testador",
  level: 1,
  xp: 0,
  hp: 100,
  position_json: { x: 0, y: 0, z: 0, facingY: 0 },
  version: 1,
  attributes: { strength: 5, agility: 5, vitality: 5, resistance: 5 },
  unspentAttributePoints: 0,
  swordSkillLevel: 1,
  swordSkillXp: 0,
  coinBalance: 0,
  inventory: [] as Array<{ itemCode: string; quantity: number }>,
  miningSkillLevel: 1,
  miningSkillXp: 0,
  metallurgySkillLevel: 1,
  metallurgySkillXp: 0,
  equipment: { mainHand: "espada_simples", tool: "picareta_simples" },
};

// A regressão que este arquivo cobre: `ws` começa a decodificar frames assim que o socket é
// promovido, antes do handler de conexão rodar. `getCharacterCombatStateByAccountId` é atrasado
// de propósito para alargar a janela entre a promoção do socket e o registro do listener de
// mensagens — sem o buffer de mensagens pendentes em transport/ws.ts, a primeira mensagem do
// cliente (world.join) chegaria antes do listener existir e seria perdida para sempre.
vi.mock("../../src/modules/characters/characters.service.js", () => ({
  getCharacterCombatStateByAccountId: vi.fn(async (accountId: string) => {
    await new Promise((resolve) => setTimeout(resolve, 20));
    return accountId === characterRow.account_id ? characterRow : null;
  }),
  persistPosition: vi.fn(async () => {}),
  persistCombatState: vi.fn(async () => {}),
  persistSwordSkill: vi.fn(async () => {}),
}));

function envelope(type: string, payload: unknown) {
  return {
    v: 1,
    type,
    requestId: randomUUID(),
    sequence: 1,
    sentAt: new Date().toISOString(),
    payload,
  };
}

function waitForMessage(ws: WebSocket, predicate: (msg: any) => boolean, timeoutMs = 2000): Promise<any> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("timeout waiting for message")), timeoutMs);
    function onMessage(raw: Buffer) {
      const msg = JSON.parse(raw.toString());
      if (predicate(msg)) {
        clearTimeout(timer);
        ws.off("message", onMessage);
        resolve(msg);
      }
    }
    ws.on("message", onMessage);
  });
}

describe("world gateway", () => {
  let app: FastifyInstance;
  let baseUrl: string;

  beforeEach(async () => {
    characterRow.hp = 150;
    app = await buildApp();
    await app.listen({ port: 0, host: "127.0.0.1" });
    const address = app.server.address();
    if (address === null || typeof address === "string") {
      throw new Error("Endereço do servidor de teste indisponível.");
    }
    baseUrl = `ws://127.0.0.1:${address.port}`;
  });

  afterEach(async () => {
    await app.close();
  });

  it("replies to world.join sent immediately after open, even while character lookup is still pending", async () => {
    const token = signAccessToken({ accountId: characterRow.account_id });
    const ws = new WebSocket(`${baseUrl}/world?token=${token}`);
    try {
      await new Promise<void>((resolve) => ws.once("open", () => resolve()));
      ws.send(JSON.stringify(envelope("world.join", {})));
      const snapshot = await waitForMessage(ws, (msg) => msg.type === "world.snapshot");
      expect(snapshot.payload.self.name).toBe(characterRow.name);
    } finally {
      ws.close();
    }
  });

  it("blocks a character loaded with zero HP until the server confirms respawn", async () => {
    characterRow.hp = 0;
    const token = signAccessToken({ accountId: characterRow.account_id });
    const ws = new WebSocket(`${baseUrl}/world?token=${token}`);
    try {
      await new Promise<void>((resolve) => ws.once("open", resolve));
      const initial = waitForMessage(ws, (msg) => msg.type === "world.snapshot");
      ws.send(JSON.stringify(envelope("world.join", {})));
      expect((await initial).payload.self.hp).toBe(0);
      const character = defaultInstance.get(characterRow.id)!;
      expect(character.incapacitatedUntil).toBeGreaterThan(Date.now());
      for (const [type, payload] of [
        ["movement.input", { moveX: 1, moveZ: 0, facingY: 0 }],
        ["combat.attack.request", { targetId: "any-enemy" }],
      ] as const) {
        const rejected = waitForMessage(ws, (msg) => msg.type === "error");
        ws.send(JSON.stringify(envelope(type, payload)));
        expect((await rejected).payload.code).toBe("INCAPACITATED");
      }
      const clock = vi.spyOn(Date, "now").mockReturnValue(character.incapacitatedUntil! + 1);
      try { tickPlayerRespawns(); } finally { clock.mockRestore(); }
      expect(character.hp).toBe(150);
      expect(character.incapacitatedUntil).toBeNull();
      expect(character.position).toEqual({ x: 0, y: 0.05, z: 0 });
      const attack = waitForMessage(ws, (msg) => msg.type === "error");
      ws.send(JSON.stringify(envelope("combat.attack.request", { targetId: "any-enemy" })));
      expect((await attack).payload.code).toBe("TARGET_NOT_FOUND");
    } finally {
      ws.close();
    }
  });

  it("reports a failed attack transaction and accepts a retry on the same connection", async () => {
    const enemy = defaultInstance.listEnemies()[0];
    const original = { ...enemy, position: { ...enemy.position } };
    const transaction = {
      query: vi.fn(async (_sql: string) => ({ rowCount: 1, rows: [] })),
      release: vi.fn(),
    };
    let fail = true;
    transaction.query.mockImplementation(async (sql: string) => {
      if (fail && sql.startsWith("update characters")) {
        fail = false;
        throw new Error("synthetic persistence failure");
      }
      return { rowCount: 1, rows: [] };
    });
    vi.mocked(pool.connect).mockResolvedValue(transaction as never);
    const errorLog = vi.spyOn(console, "error").mockImplementation(() => {});
    const token = signAccessToken({ accountId: characterRow.account_id });
    const ws = new WebSocket(`${baseUrl}/world?token=${token}`);
    try {
      await new Promise<void>((resolve) => ws.once("open", resolve));
      const initial = waitForMessage(ws, (msg) => msg.type === "world.snapshot");
      ws.send(JSON.stringify(envelope("world.join", {})));
      await initial;
      enemy.position = { x: 1, y: 0, z: 0 };
      enemy.hp = 1;
      enemy.alive = true;
      const rejected = waitForMessage(ws, (msg) => msg.type === "error");
      ws.send(JSON.stringify(envelope("combat.attack.request", { targetId: enemy.id })));
      expect((await rejected).payload).toMatchObject({ code: "PERSISTENCE_FAILED", retryable: true });
      expect(transaction.query).toHaveBeenCalledWith("rollback");
      expect(enemy.hp).toBe(1);
      expect(enemy.alive).toBe(true);
      const accepted = waitForMessage(ws, (msg) => msg.type === "combat.resolved");
      ws.send(JSON.stringify(envelope("combat.attack.request", { targetId: enemy.id })));
      expect((await accepted).payload).toMatchObject({ targetDied: true, xpAwarded: 20 });
      expect(transaction.query).toHaveBeenCalledWith("commit");
    } finally {
      ws.close();
      Object.assign(enemy, original);
      errorLog.mockRestore();
    }
  });
});
