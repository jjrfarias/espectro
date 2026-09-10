import type { Equipment, EquipmentSlot, ItemCode, InventoryItem } from "@espectro/contracts";
import type { PoolClient } from "pg";
import { pool } from "../../persistence/db.js";

/**
 * Helpers de persistência da economia (Corte 3). GDD §9: "toda alteração de inventário é uma
 * transação atômica no servidor" — cada função aqui assume que já está rodando dentro de uma
 * transação (`client` vem de `pool.connect()` + `begin`, nunca do `pool` direto), pra poder
 * compor várias mudanças (ex.: remover minério + adicionar lingote) em um único commit/rollback.
 */

export async function getInventory(client: PoolClient, characterId: string): Promise<InventoryItem[]> {
  const result = await client.query<{ item_code: ItemCode; quantity: number }>(
    `select item_code, quantity from inventory_items where character_id = $1 and quantity > 0`,
    [characterId],
  );
  return result.rows.map((row) => ({ itemCode: row.item_code, quantity: row.quantity }));
}

export async function getCoinBalance(client: PoolClient, characterId: string): Promise<number> {
  const result = await client.query<{ coin_balance: number }>(
    `select coin_balance from characters where id = $1`,
    [characterId],
  );
  return result.rows[0]?.coin_balance ?? 0;
}

// GDD §9: "inventário inicial com 20 espaços". Um espaço = uma pilha de um tipo de item (os
// mesmos itens já empilham sem limite numa única linha de inventory_items — o GDD fala em
// "limite definido por item" pra pilha, mas não dá números; sem eles pra cada um dos 8 itens do
// MVP, o limite por pilha fica de fora por enquanto, documentado como tal, em vez de inventar
// valores de balanceamento). Adicionar a um item que já existe nunca falha (só empilha mais);
// só ganhar um tipo de item NOVO conta pra esse limite.
export const MAX_INVENTORY_SLOTS = 20;

/** Retorna false (e não altera nada) se ganhar um item NOVO estourasse o limite de espaços. */
export async function addItem(
  client: PoolClient,
  characterId: string,
  itemCode: ItemCode,
  quantity: number,
): Promise<boolean> {
  if (quantity <= 0) return true;
  const result = await client.query(
    `insert into inventory_items (character_id, item_code, quantity)
     select $1, $2, $3
     where exists (select 1 from inventory_items where character_id = $1 and item_code = $2)
        or (select count(*) from inventory_items where character_id = $1 and quantity > 0) < $4
     on conflict (character_id, item_code) do update set quantity = inventory_items.quantity + $3`,
    [characterId, itemCode, quantity, MAX_INVENTORY_SLOTS],
  );
  return (result.rowCount ?? 0) === 1;
}

/** Retorna false (e não altera nada) se o personagem não tiver quantidade suficiente. */
export async function removeItem(
  client: PoolClient,
  characterId: string,
  itemCode: ItemCode,
  quantity: number,
): Promise<boolean> {
  if (quantity <= 0) return true;
  const result = await client.query(
    `update inventory_items set quantity = quantity - $3
     where character_id = $1 and item_code = $2 and quantity >= $3`,
    [characterId, itemCode, quantity],
  );
  return (result.rowCount ?? 0) === 1;
}

/**
 * Ganho/gasto de moeda com registro no livro-razão (GDD §12), atômico com o resto da transação.
 * `delta` negativo (gasto) falha e retorna false se deixaria o saldo negativo — a constraint
 * `characters_coin_balance_nonnegative` também protege isso no banco, mas checar aqui evita
 * derrubar a transação inteira com uma exceção de constraint.
 */
export async function applyLedgerEntry(
  client: PoolClient,
  characterId: string,
  delta: number,
  reason: string,
  reference: string,
): Promise<boolean> {
  const result = await client.query(
    `update characters set coin_balance = coin_balance + $2 where id = $1 and coin_balance + $2 >= 0`,
    [characterId, delta],
  );
  if ((result.rowCount ?? 0) !== 1) return false;

  await client.query(
    `insert into ledger_entries (character_id, delta, reason, reference) values ($1, $2, $3, $4)`,
    [characterId, delta, reason, reference],
  );
  return true;
}

/** Grava o item equipado num espaço (ou libera o espaço, com itemCode null). */
export async function setEquipment(
  client: PoolClient,
  characterId: string,
  slot: EquipmentSlot,
  itemCode: ItemCode | null,
): Promise<void> {
  const column = slot === "main_hand" ? "main_hand_item_code" : "tool_item_code";
  await client.query(
    `insert into character_equipment (character_id, ${column}) values ($1, $2)
     on conflict (character_id) do update set ${column} = $2`,
    [characterId, itemCode],
  );
}

export async function persistSkill(
  client: PoolClient,
  characterId: string,
  skillCode: "mineracao" | "metalurgia",
  level: number,
  xp: number,
): Promise<void> {
  await client.query(
    `update character_skills set level = $3, xp = $4, version = version + 1
     where character_id = $1 and skill_code = $2`,
    [characterId, skillCode, level, xp],
  );
}
