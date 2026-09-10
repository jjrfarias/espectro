import type { WebSocket } from "ws";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { sendChatMessage } from "../../src/modules/chat/chat.service.js";
import type { ConnectedCharacter } from "../../src/modules/world/instance.js";
import { FixedWindowRateLimiter } from "../../src/modules/world/rate-limiter.js";

const mocks = vi.hoisted(() => ({ poolQuery: vi.fn(), listCharacters: vi.fn() }));

vi.mock("../../src/persistence/db.js", () => ({ pool: { query: mocks.poolQuery } }));
vi.mock("../../src/modules/world/instance.js", () => ({
  defaultInstance: { id: "default", list: mocks.listCharacters },
}));

function makeCharacter(overrides: Partial<ConnectedCharacter> = {}): ConnectedCharacter {
  return {
    characterId: "character-1",
    accountId: "account-1",
    name: "Aldric",
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
    lastMineAt: 0,
    lastCraftAt: 0,
    chatRateLimiter: new FixedWindowRateLimiter(5, 10_000),
    equipment: { mainHand: "espada_simples", tool: "picareta_simples" },
    ...overrides,
  };
}

describe("sendChatMessage", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mocks.poolQuery.mockResolvedValue({ rowCount: 1, rows: [] });
  });

  it("persists and broadcasts a normal message to everyone in the instance (GDD §2 chat local)", async () => {
    const sender = makeCharacter();
    const other = makeCharacter({ characterId: "character-2", name: "Bruna" });
    mocks.listCharacters.mockReturnValue([sender, other]);

    const result = await sendChatMessage(sender, "  Olá, Berço!  ");

    expect(result).toEqual({ ok: true });
    expect(mocks.poolQuery).toHaveBeenCalledWith(
      expect.stringContaining("insert into chat_messages"),
      ["default", "character-1", "Olá, Berço!", expect.any(Date), "visible"],
    );
    expect(sender.socket.send).toHaveBeenCalledTimes(1);
    expect(other.socket.send).toHaveBeenCalledTimes(1);
    const sentToOther = JSON.parse((other.socket.send as any).mock.calls[0][0]);
    expect(sentToOther).toMatchObject({
      type: "chat.message",
      payload: { characterId: "character-1", characterName: "Aldric", content: "Olá, Berço!" },
    });
  });

  it("persists a flagged message as hidden and does not broadcast it", async () => {
    const sender = makeCharacter();
    const other = makeCharacter({ characterId: "character-2" });
    mocks.listCharacters.mockReturnValue([sender, other]);

    const result = await sendChatMessage(sender, "seu fdp");

    expect(result).toEqual({ ok: true });
    expect(mocks.poolQuery).toHaveBeenCalledWith(expect.any(String), ["default", "character-1", "seu fdp", expect.any(Date), "hidden"]);
    expect(sender.socket.send).not.toHaveBeenCalled();
    expect(other.socket.send).not.toHaveBeenCalled();
  });

  it("rate-limits a sender who sends too many messages too fast", async () => {
    const sender = makeCharacter({ chatRateLimiter: new FixedWindowRateLimiter(2, 10_000) });
    mocks.listCharacters.mockReturnValue([sender]);

    expect(await sendChatMessage(sender, "um")).toEqual({ ok: true });
    expect(await sendChatMessage(sender, "dois")).toEqual({ ok: true });
    expect(await sendChatMessage(sender, "tres")).toEqual({ ok: false, code: "RATE_LIMITED" });
    expect(mocks.poolQuery).toHaveBeenCalledTimes(2);
  });
});
