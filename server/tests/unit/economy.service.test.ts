import type { WebSocket } from "ws";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  buyItem,
  cancelCrafting,
  cancelMining,
  equipItem,
  sellItem,
  startCrafting,
  startMining,
  tickChannels,
  tickResourceNodes,
  useItem,
} from "../../src/modules/economy/economy.service.js";
import type { ResourceNodeState } from "../../src/modules/economy/resource-node-instance.js";
import type { ConnectedCharacter } from "../../src/modules/world/instance.js";
import { FixedWindowRateLimiter } from "../../src/modules/world/rate-limiter.js";

const mocks = vi.hoisted(() => ({
  connect: vi.fn(),
  query: vi.fn(),
  release: vi.fn(),
  getResourceNode: vi.fn(),
  listResourceNodes: vi.fn(() => [] as ResourceNodeState[]),
  listCharacters: vi.fn(() => [] as ConnectedCharacter[]),
}));

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: mocks.connect, query: vi.fn() },
}));
vi.mock("../../src/modules/world/instance.js", () => ({
  defaultInstance: {
    getResourceNode: mocks.getResourceNode,
    listResourceNodes: mocks.listResourceNodes,
    list: mocks.listCharacters,
  },
}));

/** Última mensagem enviada ao personagem via socket.send (sendEnvelope serializa em JSON). */
function lastSentMessage(character: ConnectedCharacter): { type: string; payload: unknown } {
  const calls = vi.mocked(character.socket.send).mock.calls;
  const raw = calls.at(-1)?.[0] as string;
  return JSON.parse(raw);
}

interface StoredState {
  inventory: Record<string, number>;
  coinBalance: number;
  hp: number;
  miningLevel: number;
  miningXp: number;
  metallurgyLevel: number;
  metallurgyXp: number;
}

function makeCharacter(overrides: Partial<ConnectedCharacter> = {}): ConnectedCharacter {
  return {
    characterId: "character-1",
    accountId: "account-1",
    name: "Testador",
    position: { x: 20, y: 0, z: -5 },
    facingY: 0,
    socket: { send: vi.fn() } as unknown as WebSocket,
    outboundSequence: 0,
    lastInputAt: 0,
    movementRateLimiter: new FixedWindowRateLimiter(10, 1000),
    dirtyPosition: false,
    hp: 150,
    maxHp: 150,
    attributes: { strength: 5, agility: 5, vitality: 5, resistance: 5 },
    unspentAttributePoints: 0,
    level: 1,
    xp: 0,
    swordSkillLevel: 1,
    swordSkillXp: 0,
    lastAttackAt: 0,
    incapacitatedUntil: null,
    coinBalance: 0,
    inventory: new Map(),
    miningSkillLevel: 1,
    miningSkillXp: 0,
    metallurgySkillLevel: 1,
    metallurgySkillXp: 0,
    activeChannel: null,
    chatRateLimiter: new FixedWindowRateLimiter(5, 10_000),
    equipment: { mainHand: "espada_simples", tool: "picareta_simples" },
    ...overrides,
  };
}

function makeNode(overrides: Partial<ResourceNodeState> = {}): ResourceNodeState {
  return {
    id: "ferro_1",
    resourceCode: "ferro",
    position: { x: 20, y: 0, z: -5 },
    available: true,
    depletedAt: null,
    ...overrides,
  };
}

describe("economy.service", () => {
  let character: ConnectedCharacter;
  let stored: StoredState;
  let transaction: StoredState | undefined;

  beforeEach(() => {
    vi.resetAllMocks();
    vi.spyOn(Date, "now").mockReturnValue(100_000);
    character = makeCharacter();
    stored = { inventory: {}, coinBalance: 0, hp: character.hp, miningLevel: 1, miningXp: 0, metallurgyLevel: 1, metallurgyXp: 0 };
    transaction = undefined;

    mocks.listCharacters.mockImplementation(() => [character]);
    mocks.connect.mockResolvedValue({ query: mocks.query, release: mocks.release });
    mocks.getResourceNode.mockReturnValue(undefined);
    mocks.listResourceNodes.mockReturnValue([]);

    // Pequeno banco transacional em memória, mesmo padrão de combat.service.test.ts: gravações
    // só ficam duráveis no commit, e cada "tabela" é simulada pelo prefixo do SQL.
    mocks.query.mockImplementation(async (sql: string, values?: unknown[]) => {
      if (sql === "begin") {
        transaction = structuredClone(stored);
        return { rowCount: 1, rows: [] };
      }
      if (sql === "commit") {
        if (!transaction) throw new Error("Transaction required");
        stored = transaction;
        transaction = undefined;
        return { rowCount: 1, rows: [] };
      }
      if (sql === "rollback") {
        transaction = undefined;
        return { rowCount: 1, rows: [] };
      }
      if (!transaction || !values) throw new Error("Transaction required outside begin/commit");

      if (sql.startsWith("insert into inventory_items")) {
        const [, itemCode, quantity, maxSlots] = values as [string, string, number, number];
        const isNewItem = !(itemCode in transaction.inventory) || transaction.inventory[itemCode] === 0;
        const distinctSlots = Object.values(transaction.inventory).filter((qty) => qty > 0).length;
        if (isNewItem && distinctSlots >= maxSlots) {
          return { rowCount: 0, rows: [] };
        }
        transaction.inventory[itemCode] = (transaction.inventory[itemCode] ?? 0) + quantity;
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("update inventory_items")) {
        const [, itemCode, quantity] = values as [string, string, number];
        const current = transaction.inventory[itemCode] ?? 0;
        if (current < quantity) return { rowCount: 0, rows: [] };
        transaction.inventory[itemCode] = current - quantity;
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("update characters set coin_balance")) {
        const [, delta] = values as [string, number];
        if (transaction.coinBalance + delta < 0) return { rowCount: 0, rows: [] };
        transaction.coinBalance += delta;
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("insert into ledger_entries")) {
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("update characters set hp")) {
        const [, hp] = values as [string, number];
        transaction.hp = hp;
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("insert into world_events")) {
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("insert into character_equipment")) {
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("update character_skills")) {
        const [, skillCode, level, xp] = values as [string, string, number, number];
        if (skillCode === "mineracao") {
          transaction.miningLevel = level;
          transaction.miningXp = xp;
        } else if (skillCode === "metalurgia") {
          transaction.metallurgyLevel = level;
          transaction.metallurgyXp = xp;
        }
        return { rowCount: 1, rows: [] };
      }
      throw new Error(`Unexpected query: ${sql}`);
    });
  });

  describe("mining channel (GDD §10)", () => {
    it("starts a channel, marking the node unavailable, without touching the database yet", () => {
      const node = makeNode();
      mocks.getResourceNode.mockReturnValue(node);

      const result = startMining(character, node.id);

      expect(result).toEqual({ ok: true, payload: { nodeId: node.id, resourceCode: "ferro", durationMs: 4000 } });
      expect(character.activeChannel).toEqual({ type: "mine", nodeId: node.id, resourceCode: "ferro", startedAt: 100_000, durationMs: 4000 });
      expect(node.available).toBe(false);
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("shortens the duration with mining skill level (GDD §7: -2%/nível)", () => {
      character.miningSkillLevel = 10;
      mocks.getResourceNode.mockReturnValue(makeNode());
      const result = startMining(character, "ferro_1");
      expect(result).toEqual({ ok: true, payload: { nodeId: "ferro_1", resourceCode: "ferro", durationMs: 3280 } });
    });

    it("rejects an unknown node", () => {
      mocks.getResourceNode.mockReturnValue(undefined);
      const result = startMining(character, "nao-existe");
      expect(result).toEqual({ ok: false, code: "NODE_NOT_FOUND" });
      expect(character.activeChannel).toBeNull();
    });

    it("rejects mining without a pickaxe equipped (GDD §10 fluxo passo 1)", () => {
      character.equipment.tool = null;
      mocks.getResourceNode.mockReturnValue(makeNode());
      const result = startMining(character, "ferro_1");
      expect(result).toEqual({ ok: false, code: "TOOL_NOT_EQUIPPED" });
    });

    it("rejects a depleted node", () => {
      mocks.getResourceNode.mockReturnValue(makeNode({ available: false, depletedAt: 90_000 }));
      const result = startMining(character, "ferro_1");
      expect(result).toEqual({ ok: false, code: "NODE_DEPLETED" });
    });

    it("rejects extraction out of range", () => {
      mocks.getResourceNode.mockReturnValue(makeNode({ position: { x: 100, y: 0, z: 100 } }));
      const result = startMining(character, "ferro_1");
      expect(result).toEqual({ ok: false, code: "OUT_OF_RANGE" });
    });

    it("rejects starting a second channel while one is already active", () => {
      mocks.getResourceNode.mockReturnValue(makeNode());
      startMining(character, "ferro_1");
      const result = startMining(character, "ferro_1");
      expect(result).toEqual({ ok: false, code: "ON_COOLDOWN" });
    });

    it("delivers ore and awards skill XP once tickChannels sees the duration elapsed", async () => {
      const node = makeNode();
      mocks.getResourceNode.mockReturnValue(node);
      startMining(character, node.id);

      vi.mocked(Date.now).mockReturnValue(100_000 + 4000);
      tickChannels();
      await vi.waitFor(() => expect(mocks.release).toHaveBeenCalledOnce());

      expect(character.activeChannel).toBeNull();
      expect(stored.miningXp).toBeGreaterThan(0);
      const message = lastSentMessage(character);
      expect(message).toMatchObject({ type: "resource.mine.result", payload: { nodeId: node.id, resourceCode: "ferro" } });
      const payload = message.payload as { quantityGained: number };
      expect(stored.inventory.minerio_ferro).toBe(payload.quantityGained);
      expect(character.inventory.get("minerio_ferro")).toBe(payload.quantityGained);
      expect(node.available).toBe(false); // esgotado de verdade agora; tickResourceNodes cuida do reaparecimento
    });

    it("does nothing before the channel's duration has elapsed", () => {
      mocks.getResourceNode.mockReturnValue(makeNode());
      startMining(character, "ferro_1");
      vi.mocked(Date.now).mockReturnValue(100_000 + 3999);
      tickChannels();
      expect(character.activeChannel).not.toBeNull();
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("cancels without reward and reopens the node when the character moves", () => {
      const node = makeNode();
      mocks.getResourceNode.mockReturnValue(node);
      startMining(character, node.id);

      cancelMining(character, "moved");

      expect(character.activeChannel).toBeNull();
      expect(node.available).toBe(true);
      expect(node.depletedAt).toBeNull();
      expect(lastSentMessage(character)).toMatchObject({
        type: "resource.mine.cancelled",
        payload: { nodeId: node.id, reason: "moved" },
      });
    });

    it("cancels without reward when the character takes damage", () => {
      const node = makeNode();
      mocks.getResourceNode.mockReturnValue(node);
      startMining(character, node.id);

      cancelMining(character, "damaged");

      expect(character.activeChannel).toBeNull();
      expect(node.available).toBe(true);
    });

    it("cancelMining is a no-op when the active channel is a craft, not a mine", () => {
      character.activeChannel = { type: "craft", recipeCode: "lingote_ferro", startedAt: 100_000, durationMs: 5000 };
      cancelMining(character, "moved");
      expect(character.activeChannel).not.toBeNull();
    });

    it("reopens the node and reports INVENTORY_FULL if the inventory has no room on completion", async () => {
      const node = makeNode();
      mocks.getResourceNode.mockReturnValue(node);
      for (let i = 0; i < 20; i += 1) {
        const code = `item_${i}`;
        character.inventory.set(code as never, 1);
        stored.inventory[code] = 1;
      }
      startMining(character, node.id);

      vi.mocked(Date.now).mockReturnValue(100_000 + 4000);
      tickChannels();
      await vi.waitFor(() => expect(mocks.release).toHaveBeenCalledOnce());

      expect(node.available).toBe(true);
      expect(node.depletedAt).toBeNull();
      expect(stored.inventory.minerio_ferro).toBeUndefined();
      expect(lastSentMessage(character)).toMatchObject({ type: "error", payload: { code: "INVENTORY_FULL" } });
    });

    it("reopens the node if persistence fails mid-completion", async () => {
      const node = makeNode();
      mocks.getResourceNode.mockReturnValue(node);
      startMining(character, node.id);
      mocks.query
        .mockImplementationOnce(async () => ({ rowCount: 1, rows: [] })) // begin
        .mockImplementationOnce(async () => { throw new Error("boom"); }); // addItem

      vi.mocked(Date.now).mockReturnValue(100_000 + 4000);
      tickChannels();
      await vi.waitFor(() => expect(mocks.release).toHaveBeenCalledOnce());

      expect(node.available).toBe(true);
      expect(node.depletedAt).toBeNull();
    });
  });

  describe("crafting channel (GDD §11)", () => {
    it("reserves the ore immediately and starts a channel", async () => {
      character.inventory.set("minerio_ferro", 5);
      stored.inventory.minerio_ferro = 5;

      const result = await startCrafting(character, "lingote_ferro");

      expect(result).toEqual({ ok: true, payload: { recipeCode: "lingote_ferro", durationMs: 5000 } });
      expect(character.activeChannel).toEqual({ type: "craft", recipeCode: "lingote_ferro", startedAt: 100_000, durationMs: 5000 });
      expect(stored.inventory.minerio_ferro).toBe(2);
      expect(character.inventory.get("minerio_ferro")).toBe(2);
    });

    it("rejects an unknown recipe", async () => {
      const result = await startCrafting(character, "receita_invalida" as never);
      expect(result).toEqual({ ok: false, code: "UNKNOWN_RECIPE" });
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("rejects crafting without enough ore, and reserves nothing", async () => {
      character.inventory.set("minerio_ferro", 2);
      stored.inventory.minerio_ferro = 2;

      const result = await startCrafting(character, "lingote_ferro");

      expect(result).toEqual({ ok: false, code: "INSUFFICIENT_ITEMS" });
      expect(character.activeChannel).toBeNull();
      expect(stored.inventory.minerio_ferro).toBe(2);
    });

    it("rejects starting a second channel while one is already active", async () => {
      character.inventory.set("minerio_ferro", 10);
      stored.inventory.minerio_ferro = 10;
      await startCrafting(character, "lingote_ferro");

      const result = await startCrafting(character, "lingote_ferro");

      expect(result).toEqual({ ok: false, code: "ON_COOLDOWN" });
    });

    it("delivers the ingot and awards metallurgy XP once tickChannels sees the duration elapsed", async () => {
      character.inventory.set("minerio_ferro", 5);
      stored.inventory.minerio_ferro = 5;
      await startCrafting(character, "lingote_ferro");

      vi.mocked(Date.now).mockReturnValue(100_000 + 5000);
      tickChannels();
      await vi.waitFor(() => expect(mocks.release).toHaveBeenCalledTimes(2)); // reserva + conclusão

      expect(character.activeChannel).toBeNull();
      expect(stored.inventory.lingote_ferro).toBe(1);
      expect(character.inventory.get("lingote_ferro")).toBe(1);
      expect(stored.metallurgyXp).toBeGreaterThan(0);
      expect(lastSentMessage(character)).toMatchObject({
        type: "craft.result",
        payload: { recipeCode: "lingote_ferro", producedItemCode: "lingote_ferro", producedQuantity: 1 },
      });
    });

    it("refunds the reserved ore if the inventory has no room for the ingot on completion", async () => {
      character.inventory.set("minerio_ferro", 5);
      stored.inventory.minerio_ferro = 5;
      for (let i = 0; i < 19; i += 1) {
        const code = `item_${i}`;
        character.inventory.set(code as never, 1);
        stored.inventory[code] = 1;
      }
      await startCrafting(character, "lingote_ferro");

      vi.mocked(Date.now).mockReturnValue(100_000 + 5000);
      tickChannels();
      await vi.waitFor(() => expect(mocks.release).toHaveBeenCalledTimes(2));

      expect(stored.inventory.minerio_ferro).toBe(5); // devolvido por completo
      expect(stored.inventory.lingote_ferro).toBeUndefined();
      expect(character.inventory.get("minerio_ferro")).toBe(5);
      expect(lastSentMessage(character)).toMatchObject({ type: "error", payload: { code: "INVENTORY_FULL" } });
    });

    it("refunds the full input when cancelled before half the duration", async () => {
      character.inventory.set("minerio_ferro", 5);
      stored.inventory.minerio_ferro = 5;
      await startCrafting(character, "lingote_ferro");

      vi.mocked(Date.now).mockReturnValue(100_000 + 2000); // < 2500 (metade de 5000)
      const result = await cancelCrafting(character);

      expect(result).toEqual({ ok: true, payload: { recipeCode: "lingote_ferro", refundedQuantity: 3 } });
      expect(character.activeChannel).toBeNull();
      expect(stored.inventory.minerio_ferro).toBe(5);
      expect(character.inventory.get("minerio_ferro")).toBe(5);
    });

    it("refunds all but one unit when cancelled after half the duration (GDD §11: 'dois dos três minérios')", async () => {
      character.inventory.set("minerio_ferro", 5);
      stored.inventory.minerio_ferro = 5;
      await startCrafting(character, "lingote_ferro");

      vi.mocked(Date.now).mockReturnValue(100_000 + 3000); // > 2500
      const result = await cancelCrafting(character);

      expect(result).toEqual({ ok: true, payload: { recipeCode: "lingote_ferro", refundedQuantity: 2 } });
      expect(stored.inventory.minerio_ferro).toBe(4); // 2 restantes + 2 devolvidos
      expect(character.inventory.get("minerio_ferro")).toBe(4);
    });

    it("rejects cancelling when there is no active craft channel", async () => {
      const result = await cancelCrafting(character);
      expect(result).toEqual({ ok: false, code: "NO_ACTIVE_CHANNEL" });
    });

    it("rejects cancelling a mining channel through cancelCrafting", async () => {
      character.activeChannel = { type: "mine", nodeId: "ferro_1", resourceCode: "ferro", startedAt: 100_000, durationMs: 4000 };
      const result = await cancelCrafting(character);
      expect(result).toEqual({ ok: false, code: "NO_ACTIVE_CHANNEL" });
      expect(character.activeChannel).not.toBeNull();
    });
  });

  describe("sellItem", () => {
    it("pays the fixed price and records a ledger entry (GDD §12)", async () => {
      character.inventory.set("lingote_ferro", 4);
      stored.inventory.lingote_ferro = 4;

      const result = await sellItem(character, "lingote_ferro", 2);

      expect(result.ok).toBe(true);
      if (!result.ok) return;
      expect(result.payload).toMatchObject({ quantitySold: 2, coinsEarned: 24 });
      expect(stored.coinBalance).toBe(24);
      expect(stored.inventory.lingote_ferro).toBe(2);
      expect(character.coinBalance).toBe(24);
      const ledgerWrites = mocks.query.mock.calls.filter(([sql]) => (sql as string).startsWith("insert into ledger_entries"));
      expect(ledgerWrites).toHaveLength(1);
    });

    it("refuses to sell an item the merchant doesn't buy", async () => {
      character.inventory.set("espada_simples", 1);
      const result = await sellItem(character, "espada_simples", 1);
      expect(result).toEqual({ ok: false, code: "ITEM_NOT_SELLABLE" });
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("refuses to sell more than the character owns, leaving inventory untouched", async () => {
      character.inventory.set("minerio_ferro", 1);
      stored.inventory.minerio_ferro = 1;
      const result = await sellItem(character, "minerio_ferro", 3);
      expect(result).toEqual({ ok: false, code: "INSUFFICIENT_ITEMS" });
      expect(stored.inventory.minerio_ferro).toBe(1);
      expect(character.coinBalance).toBe(0);
    });

    it("never double-spends the same inventory under a concurrent duplicate request", async () => {
      character.inventory.set("minerio_ferro", 3);
      stored.inventory.minerio_ferro = 3;

      const [first, second] = await Promise.all([
        sellItem(character, "minerio_ferro", 3),
        sellItem(character, "minerio_ferro", 3),
      ]);

      const results = [first, second];
      const succeeded = results.filter((result) => result.ok);
      const rejected = results.filter((result) => !result.ok);
      expect(succeeded).toHaveLength(1);
      expect(rejected).toHaveLength(1);
      expect(rejected[0]).toMatchObject({ ok: false, code: "ON_COOLDOWN" });
      expect(stored.inventory.minerio_ferro).toBe(0);
      expect(character.coinBalance).toBe(9);
    });
  });

  describe("buyItem", () => {
    it("charges coins and delivers the item (GDD §12, poção 15 moedas)", async () => {
      character.coinBalance = 30;
      stored.coinBalance = 30;

      const result = await buyItem(character, "pocao", 2);

      expect(result.ok).toBe(true);
      if (!result.ok) return;
      expect(result.payload).toMatchObject({ itemCode: "pocao", quantityBought: 2, coinsSpent: 30 });
      expect(stored.coinBalance).toBe(0);
      expect(stored.inventory.pocao).toBe(2);
      expect(character.coinBalance).toBe(0);
      expect(character.inventory.get("pocao")).toBe(2);
    });

    it("refuses to buy an item the merchant doesn't sell", async () => {
      character.coinBalance = 100;
      const result = await buyItem(character, "minerio_ferro", 1);
      expect(result).toEqual({ ok: false, code: "ITEM_NOT_BUYABLE" });
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("refuses to buy without enough coins, leaving the balance untouched", async () => {
      character.coinBalance = 10;
      stored.coinBalance = 10;
      const result = await buyItem(character, "pocao", 1);
      expect(result).toEqual({ ok: false, code: "INSUFFICIENT_COINS" });
      expect(stored.coinBalance).toBe(10);
      expect(character.inventory.get("pocao")).toBeUndefined();
    });
  });

  describe("useItem", () => {
    it("consumes a potion and heals HP, capped at maxHp (GDD §8)", async () => {
      character.hp = 100;
      stored.hp = 100;
      character.inventory.set("pocao", 2);
      stored.inventory.pocao = 2;

      const result = await useItem(character, "pocao");

      expect(result).toEqual({ ok: true, payload: { itemCode: "pocao", hp: 150, maxHp: 150, economy: expect.anything() } });
      expect(stored.hp).toBe(150);
      expect(stored.inventory.pocao).toBe(1);
      expect(character.hp).toBe(150);
      expect(character.inventory.get("pocao")).toBe(1);
    });

    it("never heals above maxHp", async () => {
      character.hp = 140;
      stored.hp = 140;
      character.inventory.set("pocao", 1);
      stored.inventory.pocao = 1;

      const result = await useItem(character, "pocao");

      expect(result).toMatchObject({ ok: true, payload: { hp: 150 } });
      expect(character.hp).toBe(150);
    });

    it("refuses to use a potion the character doesn't have", async () => {
      const result = await useItem(character, "pocao");
      expect(result).toEqual({ ok: false, code: "INSUFFICIENT_ITEMS" });
    });

    it("refuses to use a non-usable item", async () => {
      character.inventory.set("lingote_ferro", 1);
      const result = await useItem(character, "lingote_ferro");
      expect(result).toEqual({ ok: false, code: "ITEM_NOT_USABLE" });
      expect(mocks.connect).not.toHaveBeenCalled();
    });
  });

  describe("equipItem", () => {
    it("equips an owned item into the matching slot (GDD §9)", async () => {
      character.inventory.set("picareta_simples", 1);

      const result = await equipItem(character, "tool", "picareta_simples");

      expect(result).toEqual({
        ok: true,
        payload: { slot: "tool", itemCode: "picareta_simples", economy: expect.objectContaining({ equipment: { mainHand: "espada_simples", tool: "picareta_simples" } }) },
      });
      expect(character.equipment.tool).toBe("picareta_simples");
    });

    it("unequips a slot when itemCode is null", async () => {
      const result = await equipItem(character, "tool", null);
      expect(result).toEqual({ ok: true, payload: expect.objectContaining({ slot: "tool", itemCode: null }) });
      expect(character.equipment.tool).toBeNull();
    });

    it("refuses to equip an item the character doesn't own", async () => {
      const result = await equipItem(character, "tool", "picareta_simples");
      expect(result).toEqual({ ok: false, code: "INSUFFICIENT_ITEMS" });
      expect(character.equipment.tool).toBe("picareta_simples"); // inalterado
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("refuses to equip an item into the wrong slot", async () => {
      character.inventory.set("lingote_ferro", 1);
      const result = await equipItem(character, "tool", "lingote_ferro");
      expect(result).toEqual({ ok: false, code: "ITEM_NOT_EQUIPPABLE" });
      expect(mocks.connect).not.toHaveBeenCalled();
    });
  });

  describe("tickResourceNodes", () => {
    it("respawns a node only after its respawn delay elapses (GDD §10)", () => {
      const node = makeNode({ available: false, depletedAt: 100_000 - 44_000 });
      mocks.listResourceNodes.mockReturnValue([node]);

      tickResourceNodes();
      expect(node.available).toBe(false);

      vi.spyOn(Date, "now").mockReturnValue(100_000 + 2_000); // 46s desde a depleção > 45s do ferro.
      tickResourceNodes();
      expect(node.available).toBe(true);
      expect(node.depletedAt).toBeNull();
    });
  });
});
