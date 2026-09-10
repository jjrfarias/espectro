import type { WebSocket } from "ws";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { resolveAttack, tickPlayerRespawns } from "../../src/modules/combat/combat.service.js";
import { enemyDefinitions } from "../../src/modules/combat/enemy-definitions.js";
import type { EnemyState } from "../../src/modules/combat/enemy-instance.js";
import type { ConnectedCharacter } from "../../src/modules/world/instance.js";
import { FixedWindowRateLimiter } from "../../src/modules/world/rate-limiter.js";

const mocks = vi.hoisted(() => ({
  connect: vi.fn(),
  query: vi.fn(),
  poolQuery: vi.fn(),
  release: vi.fn(),
  getEnemy: vi.fn(),
  listCharacters: vi.fn(),
}));

vi.mock("../../src/persistence/db.js", () => ({
  pool: { connect: mocks.connect, query: mocks.poolQuery },
}));
vi.mock("../../src/modules/world/instance.js", () => ({
  defaultInstance: { getEnemy: mocks.getEnemy, list: mocks.listCharacters },
}));

interface StoredProgress {
  xp: number;
  level: number;
  swordXp: number;
  swordLevel: number;
  points: number;
}

describe("resolveAttack persistence", () => {
  let character: ConnectedCharacter;
  let enemy: EnemyState;
  let stored: StoredProgress;
  let transaction: StoredProgress | undefined;
  let failAttributes: boolean;
  const originalReward = enemyDefinitions.lobo.xpReward;

  beforeEach(() => {
    vi.resetAllMocks();
    vi.spyOn(Date, "now").mockReturnValue(10_000);
    character = {
      characterId: "character-1",
      accountId: "account-1",
      name: "Testador",
      position: { x: 0, y: 0, z: 0 },
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
      xp: 90,
      swordSkillLevel: 1,
      swordSkillXp: 45,
      lastAttackAt: 0,
      incapacitatedUntil: null,
      coinBalance: 0,
      inventory: new Map(),
      miningSkillLevel: 1,
      miningSkillXp: 0,
      metallurgySkillLevel: 1,
      metallurgySkillXp: 0,
      lastMineAt: 0,
      lastCraftAt: 0,
      talkedNpcs: new Set(),
      // Pré-marcado como concluído: um golpe fatal também dispara completeTutorialStep, que abriria
      // sua própria conexão de banco (via o mesmo mock de pool) se o passo ainda não estivesse
      // marcado — o que colidiria com as asserções de contagem de connect/release deste arquivo,
      // que são sobre a transação de resolveAttack, não a do tutorial.
      tutorialStepsCompleted: new Set(["derrotou_criatura"]),
      tutorialRewardClaimed: false,
      chatRateLimiter: new FixedWindowRateLimiter(5, 10_000),
      equipment: { mainHand: "espada_simples", tool: "picareta_simples" },
    };
    enemy = {
      id: "lobo-0",
      definitionCode: "lobo",
      position: { x: 1, y: 0, z: 0 },
      spawnPosition: { x: 1, y: 0, z: 0 },
      hp: 10,
      alive: true,
      lastAttackAt: 0,
      deadAt: null,
    };
    stored = { xp: 90, level: 1, swordXp: 45, swordLevel: 1, points: 3 };
    transaction = undefined;
    failAttributes = false;
    mocks.connect.mockResolvedValue({ query: mocks.query, release: mocks.release });
    mocks.getEnemy.mockImplementation((id: string) => id === enemy.id ? enemy : undefined);
    mocks.listCharacters.mockImplementation(() => [character]);
    mocks.poolQuery.mockResolvedValue({ rowCount: 1, rows: [] });
    // Pequeno banco transacional em memória: gravações só ficam duráveis no commit.
    mocks.query.mockImplementation(async (sql: string, values?: unknown[]) => {
      if (sql === "begin") transaction = { ...stored };
      else if (sql === "commit") {
        if (!transaction) throw new Error("Transaction required");
        stored = { ...transaction };
        transaction = undefined;
      } else if (sql === "rollback") transaction = undefined;
      else {
        if (!transaction || !values) throw new Error("Transaction required");
        if (sql.startsWith("update characters ")) {
          transaction.xp = values[1] as number;
          transaction.level = values[2] as number;
        } else if (sql.startsWith("update character_skills ")) {
          transaction.swordLevel = values[2] as number;
          transaction.swordXp = values[3] as number;
        } else if (sql.startsWith("update character_attributes ")) {
          if (failAttributes) throw new Error("attribute write failed");
          transaction.points += values[1] as number;
        } else throw new Error(`Unexpected query: ${sql}`);
      }
      return { rowCount: 1, rows: [] };
    });
  });

  afterEach(() => {
    enemyDefinitions.lobo.xpReward = originalReward;
    vi.restoreAllMocks();
  });

  it.each([
    { label: "one level", level: 1, xp: 90, reward: 20, nextLevel: 2, nextXp: 10, points: 1 },
    { label: "multiple levels", level: 1, xp: 0, reward: 393, nextLevel: 3, nextXp: 10, points: 2 },
    { label: "no level", level: 1, xp: 0, reward: 20, nextLevel: 1, nextXp: 20, points: 0 },
    { label: "the level cap", level: 10, xp: 0, reward: 20, nextLevel: 10, nextXp: 20, points: 0 },
  ])("persists XP, sword progress and earned attribute points for $label", async (scenario) => {
    character.level = stored.level = scenario.level;
    character.xp = stored.xp = scenario.xp;
    enemyDefinitions.lobo.xpReward = scenario.reward;

    const result = await resolveAttack(character, enemy.id);

    expect(result).toMatchObject({ ok: true, payload: {
      targetDied: true,
      xpAwarded: scenario.reward,
      characterLevel: scenario.nextLevel,
      characterXp: scenario.nextXp,
      leveledUp: scenario.points > 0,
    } });
    expect(stored).toEqual({
      xp: scenario.nextXp, level: scenario.nextLevel,
      swordLevel: 2, swordXp: 0, points: 3 + scenario.points,
    });
    expect(character).toMatchObject({
      level: stored.level, xp: stored.xp, swordSkillLevel: 2, swordSkillXp: 0,
      unspentAttributePoints: scenario.points,
    });
    expect(mocks.query.mock.calls[0]).toEqual(["begin"]);
    expect(mocks.query.mock.calls.at(-1)).toEqual(["commit"]);
    expect(mocks.poolQuery).not.toHaveBeenCalled();
    expect(mocks.release).toHaveBeenCalledOnce();
    const pointWrites = mocks.query.mock.calls.filter(([sql]) => sql.includes("unspent_points"));
    expect(pointWrites).toHaveLength(scenario.points > 0 ? 1 : 0);
    if (scenario.points > 0) {
      expect(pointWrites[0]).toEqual([
        expect.stringContaining("unspent_points = unspent_points + $2"),
        [character.characterId, scenario.points],
      ]);
    }
  });

  it("gives only sword XP for a nonlethal hit and keeps existing attribute points", async () => {
    enemy.hp = 60;
    const result = await resolveAttack(character, enemy.id);
    expect(result).toMatchObject({ ok: true, payload: { targetDied: false, xpAwarded: 0, leveledUp: false } });
    expect(stored).toEqual({ xp: 90, level: 1, swordXp: 0, swordLevel: 2, points: 3 });
    expect(enemy).toMatchObject({ hp: 47, alive: true, deadAt: null });
  });

  it("rolls back all reward writes and leaves the world unchanged when the attribute award fails", async () => {
    const previousProgress = { ...stored };
    failAttributes = true;

    await expect(resolveAttack(character, enemy.id)).rejects.toThrow("attribute write failed");

    expect(stored).toEqual(previousProgress);
    expect(character).toMatchObject({ level: 1, xp: 90, swordSkillLevel: 1, swordSkillXp: 45, lastAttackAt: 0 });
    expect(enemy).toMatchObject({ hp: 10, alive: true, deadAt: null });
    expect(mocks.query.mock.calls.at(-1)).toEqual(["rollback"]);
    expect(mocks.query).not.toHaveBeenCalledWith("commit");
    expect(mocks.release).toHaveBeenCalledOnce();

    failAttributes = false;
    await expect(resolveAttack(character, enemy.id)).resolves.toMatchObject({ ok: true });
    expect(stored).toEqual({ xp: 10, level: 2, swordXp: 0, swordLevel: 2, points: 4 });
  });

  it("releases attack reservations after failure to acquire a database connection", async () => {
    mocks.connect.mockRejectedValueOnce(new Error("database unavailable"));
    await expect(resolveAttack(character, enemy.id)).rejects.toThrow("database unavailable");
    expect(enemy).toMatchObject({ hp: 10, alive: true });
    expect(mocks.query).not.toHaveBeenCalled();
    expect(mocks.release).not.toHaveBeenCalled();
    await expect(resolveAttack(character, enemy.id)).resolves.toMatchObject({ ok: true });
    expect(stored.points).toBe(4);
  });

  it("reserves the attacker and target until commit so overlapping requests cannot duplicate rewards", async () => {
    let connect!: (client: { query: typeof mocks.query; release: typeof mocks.release }) => void;
    mocks.connect.mockReturnValueOnce(new Promise((resolve) => { connect = resolve; }));
    const firstAttack = resolveAttack(character, enemy.id);

    vi.mocked(Date.now).mockReturnValue(20_000);
    expect(enemy).toMatchObject({ hp: 10, alive: true });
    expect(character).toMatchObject({ level: 1, xp: 90 });
    await expect(resolveAttack(character, enemy.id)).resolves.toEqual({ ok: false, code: "ON_COOLDOWN" });
    const otherCharacter = { ...character, characterId: "character-2" };
    await expect(resolveAttack(otherCharacter, enemy.id)).resolves.toEqual({ ok: false, code: "ON_COOLDOWN" });

    connect({ query: mocks.query, release: mocks.release });
    await expect(firstAttack).resolves.toMatchObject({ ok: true });
    await expect(resolveAttack(otherCharacter, enemy.id)).resolves.toEqual({ ok: false, code: "TARGET_NOT_FOUND" });
    expect(stored.points).toBe(4);
    expect(mocks.connect).toHaveBeenCalledOnce();
  });

  it("rejects an attack without a sword equipped (GDD §9, mesmo padrão do TOOL_NOT_EQUIPPED)", async () => {
    character.equipment.mainHand = null;
    const result = await resolveAttack(character, enemy.id);
    expect(result).toEqual({ ok: false, code: "WEAPON_NOT_EQUIPPED" });
    expect(mocks.connect).not.toHaveBeenCalled();
  });

  it("persists only HP during respawn so it cannot overwrite concurrent XP rewards", () => {
    character.incapacitatedUntil = 9_000;
    character.hp = 0;
    tickPlayerRespawns();
    const healthWrite = mocks.poolQuery.mock.calls.find(([sql]) => sql.includes("set hp"));
    expect(healthWrite).toEqual([
      "update characters set hp = $2, version = version + 1, updated_at = now() where id = $1",
      [character.characterId, 150],
    ]);
    expect(healthWrite?.[0]).not.toMatch(/xp\s*=|level\s*=/);
  });
});
