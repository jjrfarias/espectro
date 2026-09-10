import type { Equipment, ItemCode, NpcCode, TutorialStepCode } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import { maxHp, type CharacterAttributes } from "../combat/formulas.js";

/** GDD-MVP.md §6: todos os atributos começam em 5, então o HP inicial é sempre este valor. */
const STARTING_HP = maxHp(5);

export interface CharacterPosition {
  x: number;
  y: number;
  z: number;
  facingY: number;
}

export interface CharacterRow {
  id: string;
  account_id: string;
  name: string;
  level: number;
  xp: number;
  hp: number;
  position_json: CharacterPosition;
  version: number;
}

export interface CharacterCombatState extends CharacterRow {
  attributes: CharacterAttributes;
  unspentAttributePoints: number;
  swordSkillLevel: number;
  swordSkillXp: number;
  // Corte 3 (GDD §9-§12).
  coinBalance: number;
  inventory: Array<{ itemCode: ItemCode; quantity: number }>;
  miningSkillLevel: number;
  miningSkillXp: number;
  metallurgySkillLevel: number;
  metallurgySkillXp: number;
  equipment: Equipment;
  // GDD §13/§4: NPCs conversados e passos do tutorial já concluídos.
  talkedNpcs: NpcCode[];
  tutorialStepsCompleted: TutorialStepCode[];
  tutorialRewardClaimed: boolean;
}

export class CharacterError extends Error {
  constructor(
    public readonly code: "CHARACTER_EXISTS" | "NAME_TAKEN",
    message: string,
  ) {
    super(message);
  }
}

const SELECT_FIELDS = "id, account_id, name, level, xp, hp, position_json, version";
const SWORD_SKILL_CODE = "sword";
const MINING_SKILL_CODE = "mineracao";
const METALLURGY_SKILL_CODE = "metalurgia";

export async function createCharacter(accountId: string, name: string): Promise<CharacterRow> {
  const client = await pool.connect();
  try {
    await client.query("begin");
    const result = await client.query<CharacterRow>(
      `insert into characters (account_id, name, hp) values ($1, $2, $3)
       on conflict (account_id) do nothing
       returning ${SELECT_FIELDS}`,
      [accountId, name, STARTING_HP],
    );
    if (result.rowCount === 0) {
      throw new CharacterError("CHARACTER_EXISTS", "Esta conta já possui um personagem.");
    }
    const character = result.rows[0];
    await client.query(`insert into character_attributes (character_id) values ($1)`, [character.id]);
    await client.query(
      `insert into character_skills (character_id, skill_code) values ($1, $2), ($1, $3), ($1, $4)`,
      [character.id, SWORD_SKILL_CODE, MINING_SKILL_CODE, METALLURGY_SKILL_CODE],
    );
    // GDD §9 flui a picareta vindo do NPC Minerador; simplificação deliberada (sem sistema de
    // entrega de item por NPC ainda, ver migração 006_equipment.ts): todo personagem já nasce
    // com espada e picareta simples, equipadas.
    await client.query(
      `insert into inventory_items (character_id, item_code, quantity) values ($1, 'espada_simples', 1), ($1, 'picareta_simples', 1)`,
      [character.id],
    );
    await client.query(
      `insert into character_equipment (character_id, main_hand_item_code, tool_item_code) values ($1, 'espada_simples', 'picareta_simples')`,
      [character.id],
    );
    await client.query("commit");
    return character;
  } catch (error) {
    await client.query("rollback");
    if (error instanceof CharacterError) throw error;
    if (isUniqueViolation(error)) {
      throw new CharacterError("NAME_TAKEN", "Este nome já está em uso.");
    }
    throw error;
  } finally {
    client.release();
  }
}

export async function getCharacterByAccountId(accountId: string): Promise<CharacterRow | null> {
  const result = await pool.query<CharacterRow>(
    `select ${SELECT_FIELDS} from characters where account_id = $1`,
    [accountId],
  );
  return result.rows[0] ?? null;
}

interface CombatStateRow extends CharacterRow {
  strength: number;
  agility: number;
  vitality: number;
  resistance: number;
  unspent_points: number;
  sword_level: number;
  sword_xp: number;
  coin_balance: number;
  mining_level: number;
  mining_xp: number;
  metallurgy_level: number;
  metallurgy_xp: number;
  inventory: Array<{ itemCode: ItemCode; quantity: number }> | null;
  main_hand_item_code: ItemCode | null;
  tool_item_code: ItemCode | null;
  talked_npcs: NpcCode[] | null;
  tutorial_steps_completed: TutorialStepCode[] | null;
  tutorial_reward_claimed_at: string | null;
}

/**
 * Usado ao entrar no mundo: junta o personagem com atributos, habilidades e economia (Corte 3)
 * em uma única consulta. `inventory` vem agregado via `json_agg` — subconsulta separada (não um
 * join direto) pra não multiplicar as outras linhas por item do inventário.
 */
export async function getCharacterCombatStateByAccountId(accountId: string): Promise<CharacterCombatState | null> {
  const result = await pool.query<CombatStateRow>(
    `select
       c.id, c.account_id, c.name, c.level, c.xp, c.hp, c.position_json, c.version, c.coin_balance,
       a.strength, a.agility, a.vitality, a.resistance, a.unspent_points,
       coalesce(sw.level, 1) as sword_level, coalesce(sw.xp, 0) as sword_xp,
       coalesce(mi.level, 1) as mining_level, coalesce(mi.xp, 0) as mining_xp,
       coalesce(me.level, 1) as metallurgy_level, coalesce(me.xp, 0) as metallurgy_xp,
       (select json_agg(json_build_object('itemCode', item_code, 'quantity', quantity))
          from inventory_items where character_id = c.id and quantity > 0) as inventory,
       eq.main_hand_item_code, eq.tool_item_code,
       (select json_agg(npc_code) from character_npc_talks where character_id = c.id) as talked_npcs,
       (select json_agg(step_code) from character_tutorial_steps where character_id = c.id) as tutorial_steps_completed,
       c.tutorial_reward_claimed_at
     from characters c
     join character_attributes a on a.character_id = c.id
     left join character_skills sw on sw.character_id = c.id and sw.skill_code = $2
     left join character_skills mi on mi.character_id = c.id and mi.skill_code = $3
     left join character_skills me on me.character_id = c.id and me.skill_code = $4
     left join character_equipment eq on eq.character_id = c.id
     where c.account_id = $1`,
    [accountId, SWORD_SKILL_CODE, MINING_SKILL_CODE, METALLURGY_SKILL_CODE],
  );
  const row = result.rows[0];
  if (!row) return null;
  return {
    id: row.id,
    account_id: row.account_id,
    name: row.name,
    level: row.level,
    xp: row.xp,
    hp: row.hp,
    position_json: row.position_json,
    version: row.version,
    attributes: {
      strength: row.strength,
      agility: row.agility,
      vitality: row.vitality,
      resistance: row.resistance,
    },
    unspentAttributePoints: row.unspent_points,
    swordSkillLevel: row.sword_level,
    swordSkillXp: row.sword_xp,
    coinBalance: row.coin_balance,
    inventory: row.inventory ?? [],
    miningSkillLevel: row.mining_level,
    miningSkillXp: row.mining_xp,
    metallurgySkillLevel: row.metallurgy_level,
    metallurgySkillXp: row.metallurgy_xp,
    equipment: { mainHand: row.main_hand_item_code, tool: row.tool_item_code },
    talkedNpcs: row.talked_npcs ?? [],
    tutorialStepsCompleted: row.tutorial_steps_completed ?? [],
    tutorialRewardClaimed: row.tutorial_reward_claimed_at !== null,
  };
}

export async function persistPosition(characterId: string, position: CharacterPosition): Promise<void> {
  await pool.query(
    `update characters set position_json = $2, version = version + 1, updated_at = now() where id = $1`,
    [characterId, JSON.stringify(position)],
  );
}

export async function persistCombatState(
  characterId: string,
  state: { hp: number; xp: number; level: number },
): Promise<void> {
  await pool.query(
    `update characters set hp = $2, xp = $3, level = $4, version = version + 1, updated_at = now() where id = $1`,
    [characterId, state.hp, state.xp, state.level],
  );
}

export async function persistSwordSkill(characterId: string, level: number, xp: number): Promise<void> {
  await pool.query(
    `update character_skills set level = $3, xp = $4, version = version + 1
     where character_id = $1 and skill_code = $2`,
    [characterId, SWORD_SKILL_CODE, level, xp],
  );
}

function isUniqueViolation(error: unknown): boolean {
  return typeof error === "object" && error !== null && (error as { code?: string }).code === "23505";
}
