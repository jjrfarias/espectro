import type { AttributeCode } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import { maxHp } from "./formulas.js";
import type { ConnectedCharacter } from "../world/instance.js";

export type AllocateAttributeFailureCode = "NO_UNSPENT_POINTS";

export interface AllocateAttributeSuccess {
  attributes: ConnectedCharacter["attributes"];
  unspentPoints: number;
  maxHp: number;
}

export type AllocateAttributeResult =
  | { ok: true; payload: AllocateAttributeSuccess }
  | { ok: false; code: AllocateAttributeFailureCode };

const ATTRIBUTE_COLUMNS: Record<AttributeCode, string> = {
  strength: "strength",
  agility: "agility",
  vitality: "vitality",
  resistance: "resistance",
};

/**
 * GDD §6: "cada nível concede um ponto de atributo". Gasta um ponto não alocado em um dos quatro
 * atributos do MVP. A checagem `unspent_points > 0` no WHERE torna a operação atômica sem
 * precisar de uma transação explícita — sob concorrência, só uma das chamadas simultâneas afeta uma linha.
 */
export async function allocateAttributePoint(
  character: ConnectedCharacter,
  attribute: AttributeCode,
): Promise<AllocateAttributeResult> {
  const column = ATTRIBUTE_COLUMNS[attribute];
  const result = await pool.query<{ strength: number; agility: number; vitality: number; resistance: number; unspent_points: number }>(
    `update character_attributes
     set ${column} = ${column} + 1, unspent_points = unspent_points - 1
     where character_id = $1 and unspent_points > 0
     returning strength, agility, vitality, resistance, unspent_points`,
    [character.characterId],
  );
  const row = result.rows[0];
  if (!row) return { ok: false, code: "NO_UNSPENT_POINTS" };

  character.attributes = {
    strength: row.strength,
    agility: row.agility,
    vitality: row.vitality,
    resistance: row.resistance,
  };
  character.unspentAttributePoints = row.unspent_points;
  character.maxHp = maxHp(row.vitality);

  return {
    ok: true,
    payload: { attributes: character.attributes, unspentPoints: character.unspentAttributePoints, maxHp: character.maxHp },
  };
}

// GDD §6: "todos os atributos começam em 5".
const BASE_ATTRIBUTE_VALUE = 5;

/**
 * GDD §6: "permite redistribuição gratuita durante o teste, falando com a instrutora". Devolve
 * todos os pontos já alocados (a diferença de cada atributo pro valor inicial) pra unspentPoints
 * e reseta os quatro pro valor inicial — sempre bem-sucedido, mesmo sem nada pra redistribuir
 * (não há "erro" possível aqui, diferente de allocateAttributePoint).
 */
export async function respecAttributes(character: ConnectedCharacter): Promise<AllocateAttributeSuccess> {
  const result = await pool.query<{ strength: number; agility: number; vitality: number; resistance: number; unspent_points: number }>(
    `update character_attributes
     set unspent_points = unspent_points + (strength - $2) + (agility - $2) + (vitality - $2) + (resistance - $2),
         strength = $2, agility = $2, vitality = $2, resistance = $2
     where character_id = $1
     returning strength, agility, vitality, resistance, unspent_points`,
    [character.characterId, BASE_ATTRIBUTE_VALUE],
  );
  const row = result.rows[0];
  if (!row) throw new Error(`Personagem ${character.characterId} sem linha em character_attributes ao redistribuir.`);

  character.attributes = {
    strength: row.strength,
    agility: row.agility,
    vitality: row.vitality,
    resistance: row.resistance,
  };
  character.unspentAttributePoints = row.unspent_points;
  character.maxHp = maxHp(row.vitality);

  return { attributes: character.attributes, unspentPoints: character.unspentAttributePoints, maxHp: character.maxHp };
}
