import { describe, expect, it } from "vitest";
import {
  applyCharacterXp,
  applySkillXp,
  attackIntervalSeconds,
  incomingDamage,
  maxHp,
  outgoingWeaponDamage,
  skillXpForNextLevel,
  xpForNextLevel,
} from "../../src/modules/combat/formulas.js";

describe("maxHp", () => {
  it("matches GDD §6: 100 + vitalidade x 10", () => {
    expect(maxHp(5)).toBe(150);
    expect(maxHp(0)).toBe(100);
    expect(maxHp(10)).toBe(200);
  });
});

describe("attackIntervalSeconds", () => {
  it("matches GDD §6: max(0.7s, 1.5s - agilidade x 0.03s)", () => {
    expect(attackIntervalSeconds(5)).toBeCloseTo(1.35);
    expect(attackIntervalSeconds(0)).toBeCloseTo(1.5);
  });

  it("never drops below the 0.7s floor even at very high agility", () => {
    expect(attackIntervalSeconds(100)).toBe(0.7);
  });
});

describe("outgoingWeaponDamage", () => {
  it("matches GDD §6/§7: (dano da arma + força x 1.5) x (1 + nível de espada x 0.02)", () => {
    // (5 + 5*1.5) * (1 + 1*0.02) = 12.5 * 1.02 = 12.75 -> arredonda para 13
    expect(outgoingWeaponDamage(5, 5, 1)).toBe(13);
  });

  it("never returns less than 1 even with zero stats", () => {
    expect(outgoingWeaponDamage(0, 0, 0)).toBeGreaterThanOrEqual(1);
  });

  it("increases with the sword skill level", () => {
    const atLevelOne = outgoingWeaponDamage(5, 5, 1);
    const atLevelFive = outgoingWeaponDamage(5, 5, 5);
    expect(atLevelFive).toBeGreaterThan(atLevelOne);
  });
});

describe("incomingDamage", () => {
  it("matches GDD §6: max(1, dano bruto - resistência x 0.5)", () => {
    expect(incomingDamage(8, 5)).toBe(6); // 8 - 2.5 = 5.5 -> arredonda para 6
  });

  it("never drops below 1 even against very high resistance", () => {
    expect(incomingDamage(8, 1000)).toBe(1);
  });
});

describe("xpForNextLevel / skillXpForNextLevel", () => {
  it("matches GDD §6: 100 x nível^1.5, arredondado para cima", () => {
    expect(xpForNextLevel(1)).toBe(100);
    expect(xpForNextLevel(2)).toBe(Math.ceil(100 * 2 ** 1.5));
  });

  it("matches GDD §7: 50 x nível^1.6", () => {
    expect(skillXpForNextLevel(1)).toBe(50);
  });
});

describe("applyCharacterXp", () => {
  it("does not level up when xp stays below the threshold", () => {
    const result = applyCharacterXp(1, 0, 50);
    expect(result).toEqual({ level: 1, xp: 50, leveledUp: false, unspentPointsGained: 0 });
  });

  it("levels up exactly at the threshold and carries over the remainder", () => {
    const result = applyCharacterXp(1, 0, 100);
    expect(result.level).toBe(2);
    expect(result.xp).toBe(0);
    expect(result.leveledUp).toBe(true);
    expect(result.unspentPointsGained).toBe(1);
  });

  it("resolves multiple level-ups from a single large xp gain", () => {
    const result = applyCharacterXp(1, 0, xpForNextLevel(1) + xpForNextLevel(2) + 10);
    expect(result.level).toBe(3);
    expect(result.xp).toBe(10);
    expect(result.unspentPointsGained).toBe(2);
  });

  it("never exceeds the MVP level cap of 10", () => {
    const result = applyCharacterXp(9, 0, 1_000_000);
    expect(result.level).toBe(10);
  });
});

describe("applySkillXp", () => {
  it("levels up the skill independently of character level", () => {
    const result = applySkillXp(1, 45, 10);
    expect(result.level).toBe(2);
    expect(result.leveledUp).toBe(true);
  });
});
