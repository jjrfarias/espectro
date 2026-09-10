import type { CombatPlayerDamagedPayload, CombatResolvedPayload } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import { persistPosition } from "../characters/characters.service.js";
import { sendEnvelope } from "../../transport/envelope.js";
import { cancelMining } from "../economy/economy.service.js";
import { defaultInstance, type ConnectedCharacter } from "../world/instance.js";
import { distanceBetween, moveToward, type Vector3 } from "../world/movement.js";
import type { EnemyState } from "./enemy-instance.js";
import { enemyDefinitions } from "./enemy-definitions.js";
import {
  PLAYER_ATTACK_RANGE_UNITS,
  PLAYER_BASE_WEAPON_DAMAGE,
  PLAYER_DEATH_INCAPACITATION_SECONDS,
  SWORD_SKILL_XP_PER_HIT,
  VILLAGE_RESPAWN_POSITION,
} from "./enemy-definitions.js";
import { applyCharacterXp, applySkillXp, attackIntervalSeconds, incomingDamage, outgoingWeaponDamage } from "./formulas.js";

export type AttackFailureCode = "TARGET_NOT_FOUND" | "OUT_OF_RANGE" | "ON_COOLDOWN" | "INCAPACITATED" | "WEAPON_NOT_EQUIPPED";

export type AttackResult = { ok: true; payload: CombatResolvedPayload } | { ok: false; code: AttackFailureCode };

// A persistência pode atravessar ticks e até o intervalo de ataque: nenhum golpe pode
// calcular outra recompensa a partir do mesmo estado enquanto o commit está pendente.
const pendingCharacters = new Set<string>();
const pendingTargets = new Set<string>();

/** GDD §8 "Regras": o servidor confirma distância, intervalo, estado do atacante e existência do alvo antes de aplicar dano. */
export async function resolveAttack(character: ConnectedCharacter, targetId: string): Promise<AttackResult> {
  const now = Date.now();
  if (character.incapacitatedUntil !== null) {
    return { ok: false, code: "INCAPACITATED" };
  }
  // GDD §9: espada é um item equipável (espaço "mão principal"); sem ela, não há como golpear.
  // Mesmo padrão de TOOL_NOT_EQUIPPED na mineração (economy.service.ts).
  if (character.equipment.mainHand !== "espada_simples") {
    return { ok: false, code: "WEAPON_NOT_EQUIPPED" };
  }

  const cooldownSeconds = attackIntervalSeconds(character.attributes.agility);
  if (pendingCharacters.has(character.characterId) || now - character.lastAttackAt < cooldownSeconds * 1000) {
    return { ok: false, code: "ON_COOLDOWN" };
  }

  const enemy = defaultInstance.getEnemy(targetId);
  if (!enemy || !enemy.alive) {
    return { ok: false, code: "TARGET_NOT_FOUND" };
  }
  if (pendingTargets.has(enemy.id)) {
    return { ok: false, code: "ON_COOLDOWN" };
  }

  if (distanceBetween(character.position, enemy.position) > PLAYER_ATTACK_RANGE_UNITS) {
    return { ok: false, code: "OUT_OF_RANGE" };
  }

  const damage = outgoingWeaponDamage(PLAYER_BASE_WEAPON_DAMAGE, character.attributes.strength, character.swordSkillLevel);
  const targetHp = Math.max(0, enemy.hp - damage);
  const targetDied = targetHp === 0;
  const definition = enemyDefinitions[enemy.definitionCode];
  const xpAwarded = targetDied ? definition.xpReward : 0;
  const progress = targetDied
    ? applyCharacterXp(character.level, character.xp, xpAwarded)
    : { level: character.level, xp: character.xp, leveledUp: false, unspentPointsGained: 0 };

  // GDD §7: a habilidade de espada ganha XP por golpe válido, morrendo o alvo ou não.
  const skillProgress = applySkillXp(character.swordSkillLevel, character.swordSkillXp, SWORD_SKILL_XP_PER_HIT);
  pendingCharacters.add(character.characterId);
  pendingTargets.add(enemy.id);
  try {
    const client = await pool.connect();
    try {
      await client.query("begin");
      await client.query(
        `update characters set xp = $2, level = $3, version = version + 1, updated_at = now() where id = $1`,
        [character.characterId, progress.xp, progress.level],
      );
      await client.query(
        `update character_skills set level = $3, xp = $4, version = version + 1
         where character_id = $1 and skill_code = $2`,
        [character.characterId, "sword", skillProgress.level, skillProgress.xp],
      );
      if (progress.unspentPointsGained > 0) {
        const attributes = await client.query(
          `update character_attributes set unspent_points = unspent_points + $2 where character_id = $1`,
          [character.characterId, progress.unspentPointsGained],
        );
        if (attributes.rowCount !== 1) {
          throw new Error("Atributos do personagem ausentes ao conceder pontos de nível.");
        }
      }
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    } finally {
      client.release();
    }

    // Só publique o novo estado após salvar toda a recompensa. Falhas deixam o
    // inimigo e a progressão intactos, permitindo tentar o golpe novamente.
    character.lastAttackAt = now;
    character.level = progress.level;
    character.xp = progress.xp;
    character.swordSkillLevel = skillProgress.level;
    character.swordSkillXp = skillProgress.xp;
    character.unspentAttributePoints += progress.unspentPointsGained;
    enemy.hp = targetHp;
    if (targetDied) {
      enemy.alive = false;
      enemy.deadAt = Date.now();
    }
  } finally {
    pendingCharacters.delete(character.characterId);
    pendingTargets.delete(enemy.id);
  }

  return {
    ok: true,
    payload: {
      targetId: enemy.id,
      damage,
      targetHp: enemy.hp,
      targetMaxHp: definition.maxHp,
      targetDied,
      xpAwarded,
      characterXp: character.xp,
      characterLevel: character.level,
      leveledUp: progress.leveledUp,
    },
  };
}

/** Roda a cada tick da simulação: movimenta e ataca inimigos vivos, e faz respawn dos mortos. */
export function tickEnemies(deltaSeconds: number): void {
  const now = Date.now();
  const activeCharacters = defaultInstance.list().filter((character) => character.incapacitatedUntil === null);

  for (const enemy of defaultInstance.listEnemies()) {
    if (!enemy.alive) {
      maybeRespawnEnemy(enemy, now);
      continue;
    }

    const definition = enemyDefinitions[enemy.definitionCode];
    const target = nearestCharacterWithin(enemy.position, activeCharacters, definition.aggroRangeUnits);
    if (!target) continue;

    if (distanceBetween(enemy.position, target.position) > definition.attackRangeUnits) {
      enemy.position = moveToward(enemy.position, target.position, definition.moveSpeedUnitsPerSecond, deltaSeconds);
      continue;
    }

    if (now - enemy.lastAttackAt < definition.attackCooldownSeconds * 1000) continue;
    enemy.lastAttackAt = now;
    void applyEnemyAttack(enemy, target, definition.baseDamage);
  }
}

/** Roda a cada tick: restaura personagens cuja incapacitação (GDD §8 "Morte") já terminou. */
export function tickPlayerRespawns(): void {
  const now = Date.now();
  for (const character of defaultInstance.list()) {
    if (character.incapacitatedUntil === null || now < character.incapacitatedUntil) continue;
    character.incapacitatedUntil = null;
    character.hp = character.maxHp;
    character.position = { ...VILLAGE_RESPAWN_POSITION };
    character.facingY = 0;
    character.dirtyPosition = false;
    void persistPosition(character.characterId, { ...character.position, facingY: character.facingY });
    void persistHp(character);
  }
}

async function applyEnemyAttack(enemy: EnemyState, character: ConnectedCharacter, baseDamage: number): Promise<void> {
  const damage = incomingDamage(baseDamage, character.attributes.resistance);
  character.hp = Math.max(0, character.hp - damage);
  // GDD §10: "sofrer dano ... cancela a extração sem recompensa" — só mineração, fundição
  // acontece parado na forja e não é interrompida por combate.
  cancelMining(character, "damaged");
  const died = character.hp === 0;
  let respawnPosition: Vector3 | null = null;

  if (died) {
    character.incapacitatedUntil = Date.now() + PLAYER_DEATH_INCAPACITATION_SECONDS * 1000;
    respawnPosition = VILLAGE_RESPAWN_POSITION;
  }

  character.outboundSequence += 1;
  const payload: CombatPlayerDamagedPayload = {
    sourceEnemyId: enemy.id,
    damage,
    hp: character.hp,
    maxHp: character.maxHp,
    died,
    respawnPosition,
  };
  sendEnvelope(character.socket, "combat.player_damaged", payload, character.outboundSequence);

  await persistHp(character);
}

async function persistHp(character: ConnectedCharacter): Promise<void> {
  // Dano e respawn podem acontecer durante uma recompensa pendente. Gravar somente
  // HP impede que esses eventos restaurem um XP/nível anterior ao commit do ataque.
  await pool.query(
    `update characters set hp = $2, version = version + 1, updated_at = now() where id = $1`,
    [character.characterId, character.hp],
  );
}

function maybeRespawnEnemy(enemy: EnemyState, now: number): void {
  if (enemy.deadAt === null) return;
  const definition = enemyDefinitions[enemy.definitionCode];
  if (now - enemy.deadAt < definition.respawnDelaySeconds * 1000) return;
  enemy.hp = definition.maxHp;
  enemy.position = { ...enemy.spawnPosition };
  enemy.alive = true;
  enemy.deadAt = null;
}

function nearestCharacterWithin(
  position: Vector3,
  characters: ConnectedCharacter[],
  rangeUnits: number,
): ConnectedCharacter | undefined {
  let nearest: ConnectedCharacter | undefined;
  let nearestDistance = rangeUnits;
  for (const character of characters) {
    const distance = distanceBetween(position, character.position);
    if (distance <= nearestDistance) {
      nearest = character;
      nearestDistance = distance;
    }
  }
  return nearest;
}
