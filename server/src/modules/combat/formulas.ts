/**
 * Fórmulas puras de combate e progressão. Espelham docs/GDD-MVP.md §6-§8 literalmente —
 * qualquer mudança de balanceamento deve ser feita ali primeiro, e refletida aqui.
 */

export interface CharacterAttributes {
  strength: number;
  agility: number;
  vitality: number;
  resistance: number;
}

/** GDD §6: HP máximo = 100 + Vitalidade × 10 */
export function maxHp(vitality: number): number {
  return 100 + vitality * 10;
}

/** GDD §6: intervalo de ataque = máximo(0,7 s, 1,5 s - Agilidade × 0,03 s) */
export function attackIntervalSeconds(agility: number): number {
  return Math.max(0.7, 1.5 - agility * 0.03);
}

/**
 * GDD §6/§7: dano bruto = dano da arma + Força × 1,5, ajustado pelo bônus da habilidade de
 * espada (+2% por nível). Usado para o dano do personagem contra um inimigo — inimigos não
 * possuem Resistência no MVP, então nada reduz esse valor no lado do alvo.
 */
export function outgoingWeaponDamage(weaponDamage: number, strength: number, swordSkillLevel: number): number {
  const rawDamage = weaponDamage + strength * 1.5;
  const skillMultiplier = 1 + swordSkillLevel * 0.02;
  return Math.max(1, Math.round(rawDamage * skillMultiplier));
}

/** GDD §6: dano recebido = máximo(1, dano bruto - Resistência × 0,5). Usado quando um inimigo ataca o personagem. */
export function incomingDamage(rawDamage: number, resistance: number): number {
  return Math.max(1, Math.round(rawDamage - resistance * 0.5));
}

/** GDD §6: XP para o próximo nível = 100 × nível atual^1,5, arredondado para cima. Nível máximo 10. */
export function xpForNextLevel(currentLevel: number): number {
  return Math.ceil(100 * currentLevel ** 1.5);
}

/** GDD §7: XP de habilidade para o próximo nível = 50 × nível atual^1,6. Nível máximo 10. */
export function skillXpForNextLevel(currentLevel: number): number {
  return Math.ceil(50 * currentLevel ** 1.6);
}

export const MAX_CHARACTER_LEVEL = 10;
export const MAX_SKILL_LEVEL = 10;

export interface LevelProgressResult {
  level: number;
  xp: number;
  leveledUp: boolean;
  unspentPointsGained: number;
}

/** Aplica XP ganho e resolve quantos níveis o personagem sobe (GDD §6: 1 ponto de atributo por nível), até o nível máximo. */
export function applyCharacterXp(currentLevel: number, currentXp: number, xpGained: number): LevelProgressResult {
  let level = currentLevel;
  let xp = currentXp + xpGained;
  let unspentPointsGained = 0;

  while (level < MAX_CHARACTER_LEVEL && xp >= xpForNextLevel(level)) {
    xp -= xpForNextLevel(level);
    level += 1;
    unspentPointsGained += 1;
  }
  if (level >= MAX_CHARACTER_LEVEL) {
    xp = Math.min(xp, xpForNextLevel(MAX_CHARACTER_LEVEL - 1));
  }

  return { level, xp, leveledUp: unspentPointsGained > 0, unspentPointsGained };
}

export interface SkillProgressResult {
  level: number;
  xp: number;
  leveledUp: boolean;
}

/** Mesma mecânica de progressão, aplicada a uma habilidade (GDD §7). */
export function applySkillXp(currentLevel: number, currentXp: number, xpGained: number): SkillProgressResult {
  let level = currentLevel;
  let xp = currentXp + xpGained;
  let leveledUp = false;

  while (level < MAX_SKILL_LEVEL && xp >= skillXpForNextLevel(level)) {
    xp -= skillXpForNextLevel(level);
    level += 1;
    leveledUp = true;
  }
  if (level >= MAX_SKILL_LEVEL) {
    xp = Math.min(xp, skillXpForNextLevel(MAX_SKILL_LEVEL - 1));
  }

  return { level, xp, leveledUp };
}
