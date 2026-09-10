import { tutorialStepCodes } from "@espectro/contracts";
import type { WebSocket } from "ws";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { completeTutorialStep, hasTalkedTo, talkToNpc, tutorialSnapshotOf } from "../../src/modules/missions/missions.service.js";
import { TUTORIAL_REWARD_COINS } from "../../src/modules/missions/missions.constants.js";
import type { ConnectedCharacter } from "../../src/modules/world/instance.js";
import { FixedWindowRateLimiter } from "../../src/modules/world/rate-limiter.js";

const mocks = vi.hoisted(() => ({
  connect: vi.fn(),
  query: vi.fn(),
  release: vi.fn(),
  poolQuery: vi.fn(),
}));

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: mocks.connect, query: mocks.poolQuery },
}));

function makeCharacter(overrides: Partial<ConnectedCharacter> = {}): ConnectedCharacter {
  return {
    characterId: "character-1",
    accountId: "account-1",
    name: "Testador",
    position: { x: 0, y: 0, z: 0 },
    facingY: 0,
    socket: { send: vi.fn(), readyState: 1, OPEN: 1 } as unknown as WebSocket,
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
    talkedNpcs: new Set(),
    tutorialStepsCompleted: new Set(),
    tutorialRewardClaimed: false,
    chatRateLimiter: new FixedWindowRateLimiter(5, 10_000),
    equipment: { mainHand: "espada_simples", tool: "picareta_simples" },
    ...overrides,
  };
}

describe("missions.service", () => {
  let stored: { completedSteps: Set<string>; coinBalance: number; rewardClaimed: boolean };
  let transaction: typeof stored | undefined;

  beforeEach(() => {
    vi.resetAllMocks();
    stored = { completedSteps: new Set(), coinBalance: 0, rewardClaimed: false };
    transaction = undefined;

    mocks.poolQuery.mockResolvedValue({ rowCount: 1, rows: [] });
    mocks.connect.mockResolvedValue({ query: mocks.query, release: mocks.release });
    mocks.query.mockImplementation(async (sql: string, values?: unknown[]) => {
      if (sql === "begin") {
        transaction = { completedSteps: new Set(stored.completedSteps), coinBalance: stored.coinBalance, rewardClaimed: stored.rewardClaimed };
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

      if (sql.startsWith("insert into character_tutorial_steps")) {
        const [, stepCode] = values as [string, string];
        if (transaction.completedSteps.has(stepCode)) return { rowCount: 0, rows: [] };
        transaction.completedSteps.add(stepCode);
        return { rowCount: 1, rows: [] };
      }
      if (sql.startsWith("update characters set coin_balance")) {
        if (transaction.rewardClaimed || transaction.completedSteps.size < tutorialStepCodes.length) {
          return { rowCount: 0, rows: [] };
        }
        transaction.rewardClaimed = true;
        transaction.coinBalance += TUTORIAL_REWARD_COINS;
        return { rowCount: 1, rows: [{ coin_balance: transaction.coinBalance }] };
      }
      if (sql.startsWith("insert into ledger_entries")) {
        return { rowCount: 1, rows: [] };
      }
      throw new Error(`Unexpected query: ${sql}`);
    });
  });

  describe("talkToNpc", () => {
    it("records the conversation and completes the mapped tutorial step (GDD §4/§13)", async () => {
      const character = makeCharacter();

      const result = await talkToNpc(character, "instrutora");

      expect(character.talkedNpcs.has("instrutora")).toBe(true);
      expect(mocks.poolQuery).toHaveBeenCalledWith(
        expect.stringContaining("insert into character_npc_talks"),
        ["character-1", "instrutora"],
      );
      expect(result).toEqual({
        npcCode: "instrutora",
        tutorial: { completedSteps: ["falou_instrutora"], completed: false, rewardClaimed: false },
      });
      expect(character.tutorialStepsCompleted.has("falou_instrutora")).toBe(true);
    });

    it("is idempotent: talking to the same NPC again does not re-insert or re-complete the step", async () => {
      const character = makeCharacter({ talkedNpcs: new Set(["instrutora"]), tutorialStepsCompleted: new Set(["falou_instrutora"]) });

      await talkToNpc(character, "instrutora");

      expect(mocks.poolQuery).not.toHaveBeenCalled();
      expect(mocks.connect).not.toHaveBeenCalled();
    });

    it("records the conversation without touching tutorial steps for an NPC with no mapped step", async () => {
      const character = makeCharacter();

      const result = await talkToNpc(character, "comerciante");

      expect(character.talkedNpcs.has("comerciante")).toBe(true);
      expect(mocks.connect).not.toHaveBeenCalled();
      expect(result.tutorial.completedSteps).toEqual([]);
    });
  });

  describe("hasTalkedTo", () => {
    it("reflects the in-memory set without touching the database", () => {
      const character = makeCharacter({ talkedNpcs: new Set(["ferreiro"]) });
      expect(hasTalkedTo(character, "ferreiro")).toBe(true);
      expect(hasTalkedTo(character, "cronista")).toBe(false);
      expect(mocks.poolQuery).not.toHaveBeenCalled();
    });
  });

  describe("completeTutorialStep", () => {
    it("marks a step complete and pushes a tutorial.snapshot, without claiming a reward yet", async () => {
      const character = makeCharacter();

      await completeTutorialStep(character, "derrotou_criatura");

      expect(character.tutorialStepsCompleted.has("derrotou_criatura")).toBe(true);
      expect(character.tutorialRewardClaimed).toBe(false);
      expect(character.coinBalance).toBe(0);
      expect(character.socket.send).toHaveBeenCalledOnce();
      const [sentRaw] = vi.mocked(character.socket.send).mock.calls[0];
      const sent = JSON.parse(sentRaw as string);
      expect(sent).toMatchObject({ type: "tutorial.snapshot", payload: { completed: false, rewardClaimed: false } });
    });

    it("is a no-op (no DB call) when the step was already completed in memory", async () => {
      const character = makeCharacter({ tutorialStepsCompleted: new Set(["derrotou_criatura"]) });

      await completeTutorialStep(character, "derrotou_criatura");

      expect(mocks.connect).not.toHaveBeenCalled();
      expect(character.socket.send).not.toHaveBeenCalled();
    });

    it("claims the one-time reward when the last tutorial step completes (GDD §12)", async () => {
      const allButLast = tutorialStepCodes.slice(0, -1);
      const lastStep = tutorialStepCodes.at(-1)!;
      const character = makeCharacter({ tutorialStepsCompleted: new Set(allButLast) });
      stored.completedSteps = new Set(allButLast);

      await completeTutorialStep(character, lastStep);

      expect(character.tutorialStepsCompleted.size).toBe(tutorialStepCodes.length);
      expect(character.tutorialRewardClaimed).toBe(true);
      expect(character.coinBalance).toBe(TUTORIAL_REWARD_COINS);
      expect(stored.rewardClaimed).toBe(true);
      const ledgerWrite = mocks.query.mock.calls.find(([sql]) => (sql as string).startsWith("insert into ledger_entries"));
      expect(ledgerWrite).toBeDefined();
      const [sentRaw] = vi.mocked(character.socket.send).mock.calls[0];
      const sent = JSON.parse(sentRaw as string);
      expect(sent).toMatchObject({ type: "tutorial.snapshot", payload: { completed: true, rewardClaimed: true } });
    });

    it("never claims the reward twice even if called again after completion", async () => {
      const character = makeCharacter({ tutorialStepsCompleted: new Set(tutorialStepCodes), tutorialRewardClaimed: true });

      await completeTutorialStep(character, tutorialStepCodes[0]);

      expect(mocks.connect).not.toHaveBeenCalled();
      expect(character.coinBalance).toBe(0);
    });
  });

  describe("tutorialSnapshotOf", () => {
    it("reports completed:true only when every step is done", () => {
      const partial = makeCharacter({ tutorialStepsCompleted: new Set(tutorialStepCodes.slice(0, -1)) });
      expect(tutorialSnapshotOf(partial).completed).toBe(false);

      const full = makeCharacter({ tutorialStepsCompleted: new Set(tutorialStepCodes), tutorialRewardClaimed: true });
      expect(tutorialSnapshotOf(full)).toEqual({
        completedSteps: [...tutorialStepCodes],
        completed: true,
        rewardClaimed: true,
      });
    });
  });
});
