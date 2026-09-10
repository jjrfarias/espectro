import type { EnemyDefinitionCode } from "@espectro/contracts";

/** GDD-MVP.md §8, tabela "Inimigos". Valores são parâmetros de balanceamento do servidor. */
export interface EnemyDefinition {
  code: EnemyDefinitionCode;
  displayName: string;
  maxHp: number;
  baseDamage: number;
  xpReward: number;
  attackRangeUnits: number;
  aggroRangeUnits: number;
  attackCooldownSeconds: number;
  moveSpeedUnitsPerSecond: number;
  respawnDelaySeconds: number;
}

export const enemyDefinitions: Record<EnemyDefinitionCode, EnemyDefinition> = {
  lobo: {
    code: "lobo",
    displayName: "Lobo",
    maxHp: 60,
    baseDamage: 8,
    xpReward: 20,
    attackRangeUnits: 1.6,
    aggroRangeUnits: 8,
    attackCooldownSeconds: 1.4,
    moveSpeedUnitsPerSecond: 2.4,
    respawnDelaySeconds: 25,
  },
  javali: {
    code: "javali",
    displayName: "Javali",
    maxHp: 110,
    baseDamage: 13,
    xpReward: 35,
    attackRangeUnits: 1.8,
    aggroRangeUnits: 7,
    attackCooldownSeconds: 1.7,
    moveSpeedUnitsPerSecond: 2.0,
    respawnDelaySeconds: 35,
  },
};

/** Alcance de ataque do personagem (GDD §8: "uma arma" no MVP, sem sistema de itens ainda). */
export const PLAYER_ATTACK_RANGE_UNITS = 2.25;
export const PLAYER_BASE_WEAPON_DAMAGE = 5;

/** GDD §7: a habilidade de espada ganha XP por golpe válido; o GDD não fixa a quantia por golpe. */
export const SWORD_SKILL_XP_PER_HIT = 5;

/** GDD §8 "Morte": incapacitado por 5s, renasce em O Berço com HP completo. */
export const PLAYER_DEATH_INCAPACITATION_SECONDS = 5;
export const VILLAGE_RESPAWN_POSITION = { x: 0, y: 0.05, z: 0 };
