import type { WebSocket } from "ws";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { allocateAttributePoint } from "../../src/modules/combat/attributes.service.js";
import type { ConnectedCharacter } from "../../src/modules/world/instance.js";
import { FixedWindowRateLimiter } from "../../src/modules/world/rate-limiter.js";

const mocks = vi.hoisted(() => ({ query: vi.fn() }));

vi.mock("../../src/persistence/db.js", () => ({
  pool: { query: mocks.query },
}));

function makeCharacter(overrides: Partial<ConnectedCharacter> = {}): ConnectedCharacter {
  return {
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
    unspentAttributePoints: 1,
    level: 2,
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

describe("allocateAttributePoint", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("spends a point on the chosen attribute and updates the in-memory character (GDD §6)", async () => {
    const character = makeCharacter();
    mocks.query.mockResolvedValue({
      rows: [{ strength: 6, agility: 5, vitality: 5, resistance: 5, unspent_points: 0 }],
    });

    const result = await allocateAttributePoint(character, "strength");

    expect(result).toEqual({
      ok: true,
      payload: { attributes: { strength: 6, agility: 5, vitality: 5, resistance: 5 }, unspentPoints: 0, maxHp: 150 },
    });
    expect(character.attributes.strength).toBe(6);
    expect(character.unspentAttributePoints).toBe(0);
    const [sql, values] = mocks.query.mock.calls[0];
    expect(sql).toContain("strength = strength + 1");
    expect(values).toEqual(["character-1"]);
  });

  it("raises maxHp when a point is spent on vitality", async () => {
    const character = makeCharacter();
    mocks.query.mockResolvedValue({
      rows: [{ strength: 5, agility: 5, vitality: 6, resistance: 5, unspent_points: 0 }],
    });

    const result = await allocateAttributePoint(character, "vitality");

    expect(result).toMatchObject({ ok: true, payload: { maxHp: 160 } });
    expect(character.maxHp).toBe(160);
  });

  it("rejects allocation when there are no unspent points, leaving the character untouched", async () => {
    const character = makeCharacter({ unspentAttributePoints: 0 });
    mocks.query.mockResolvedValue({ rows: [] });

    const result = await allocateAttributePoint(character, "strength");

    expect(result).toEqual({ ok: false, code: "NO_UNSPENT_POINTS" });
    expect(character.attributes.strength).toBe(5);
    expect(character.unspentAttributePoints).toBe(0);
  });
});
