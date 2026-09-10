import type { BuyResultPayload, CraftCancelledPayload, CraftStartedPayload, EquipmentSlot, EquipResultPayload, ItemCode, MineStartedPayload, RecipeCode, SellResultPayload, UseItemResultPayload } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import { sendEnvelope, sendError } from "../../transport/envelope.js";
import { applySkillXp } from "../combat/formulas.js";
import { defaultInstance, type ConnectedCharacter } from "../world/instance.js";
import { distanceBetween } from "../world/movement.js";
import { addItem, applyLedgerEntry, economySnapshotOf, persistSkill, removeItem, setEquipment } from "./economy.repository.js";
import { recordEvent } from "../world/events.repository.js";
import { completeTutorialStep, hasTalkedTo } from "../missions/missions.service.js";
import type { CraftingChannelState, MiningChannelState } from "./channel-state.js";
import { buyPrices, type CraftingRecipe, craftingRecipes, equippableItemsBySlot, METALLURGY_SKILL_XP_PER_CRAFT, MINING_RANGE_UNITS, MINING_SKILL_XP_PER_EXTRACTION, miningDefinitions, POTION_HEAL_AMOUNT, sellPrices, skillAdjustedDurationMs } from "./economy.constants.js";

export type MineFailureCode = "NODE_NOT_FOUND" | "NODE_DEPLETED" | "OUT_OF_RANGE" | "ON_COOLDOWN" | "INCAPACITATED" | "TOOL_NOT_EQUIPPED";
export type CraftFailureCode = "UNKNOWN_RECIPE" | "INSUFFICIENT_ITEMS" | "ON_COOLDOWN" | "INCAPACITATED" | "FORGE_LOCKED";
export type SellFailureCode = "ITEM_NOT_SELLABLE" | "INSUFFICIENT_ITEMS" | "ON_COOLDOWN" | "INCAPACITATED";
export type EquipFailureCode = "ITEM_NOT_EQUIPPABLE" | "INSUFFICIENT_ITEMS";
export type BuyFailureCode = "ITEM_NOT_BUYABLE" | "INSUFFICIENT_COINS" | "ON_COOLDOWN" | "INCAPACITATED" | "INVENTORY_FULL";
export type UseItemFailureCode = "ITEM_NOT_USABLE" | "INSUFFICIENT_ITEMS" | "ON_COOLDOWN" | "INCAPACITATED";
export type CraftCancelFailureCode = "NO_ACTIVE_CHANNEL";

export type MineStartResult = { ok: true; payload: MineStartedPayload } | { ok: false; code: MineFailureCode };
export type CraftStartResult = { ok: true; payload: CraftStartedPayload } | { ok: false; code: CraftFailureCode };
export type CraftCancelResult = { ok: true; payload: CraftCancelledPayload } | { ok: false; code: CraftCancelFailureCode };
export type SellResult = { ok: true; payload: SellResultPayload } | { ok: false; code: SellFailureCode };
export type EquipResult = { ok: true; payload: EquipResultPayload } | { ok: false; code: EquipFailureCode };
export type BuyResult = { ok: true; payload: BuyResultPayload } | { ok: false; code: BuyFailureCode };
export type UseItemResult = { ok: true; payload: UseItemResultPayload } | { ok: false; code: UseItemFailureCode };

// Trava por personagem pras ações que não são um canal (venda/compra/uso/início de fundição):
// nenhuma delas pode ser resolvida duas vezes em paralelo a partir do mesmo estado em memória
// (mesmo raciocínio de pendingCharacters em combat.service.ts). Minerar e fundir já têm exclusão
// própria via `character.activeChannel` — só um canal por vez —, mas iniciar uma fundição ainda
// precisa da trava porque a reserva dos ingredientes é assíncrona (ver startCrafting).
const pendingCharacters = new Set<string>();

/**
 * GDD §10: equipar a picareta, aproximar-se do veio, servidor valida alcance e estado antes de
 * iniciar a extração. Só valida e reserva o veio — a entrega do recurso e do XP acontece em
 * completeMining, quando o canal (GDD §10 passo 3: "manter o comando de interação") terminar sem
 * ser cancelado por movimento, dano ou desconexão.
 */
export function startMining(character: ConnectedCharacter, nodeId: string): MineStartResult {
  if (character.incapacitatedUntil !== null) return { ok: false, code: "INCAPACITATED" };
  if (character.equipment.tool !== "picareta_simples") return { ok: false, code: "TOOL_NOT_EQUIPPED" };
  if (character.activeChannel !== null) return { ok: false, code: "ON_COOLDOWN" };

  const node = defaultInstance.getResourceNode(nodeId);
  if (!node) return { ok: false, code: "NODE_NOT_FOUND" };
  if (!node.available) return { ok: false, code: "NODE_DEPLETED" };
  if (distanceBetween(character.position, node.position) > MINING_RANGE_UNITS) {
    return { ok: false, code: "OUT_OF_RANGE" };
  }

  const definition = miningDefinitions[node.resourceCode];
  const durationMs = skillAdjustedDurationMs(definition.baseSeconds, character.miningSkillLevel);
  node.available = false;
  node.depletedAt = Date.now();
  character.activeChannel = { type: "mine", nodeId, resourceCode: node.resourceCode, startedAt: Date.now(), durationMs };

  return { ok: true, payload: { nodeId, resourceCode: node.resourceCode, durationMs } };
}

/** Libera o veio de um canal de mineração sem enviar nenhuma mensagem (usado na desconexão). */
function abandonMiningChannel(character: ConnectedCharacter): void {
  const channel = character.activeChannel;
  if (!channel || channel.type !== "mine") return;
  const node = defaultInstance.getResourceNode(channel.nodeId);
  if (node) {
    node.available = true;
    node.depletedAt = null;
  }
  character.activeChannel = null;
}

/** GDD §10: "mover-se, sofrer dano ou perder conexão cancela a extração sem recompensa." */
export function cancelMining(character: ConnectedCharacter, reason: "moved" | "damaged"): void {
  const channel = character.activeChannel;
  if (!channel || channel.type !== "mine") return;
  const nodeId = channel.nodeId;
  abandonMiningChannel(character);
  if (character.socket.readyState === character.socket.OPEN) {
    character.outboundSequence += 1;
    sendEnvelope(character.socket, "resource.mine.cancelled", { nodeId, reason }, character.outboundSequence);
  }
}

/** Chamado por tickChannels quando o canal de mineração termina sem ser cancelado. */
async function completeMining(character: ConnectedCharacter, channel: MiningChannelState): Promise<void> {
  const node = defaultInstance.getResourceNode(channel.nodeId);
  const definition = miningDefinitions[channel.resourceCode];
  const quantity = randomInt(definition.minQuantity, definition.maxQuantity);
  const skillProgress = applySkillXp(character.miningSkillLevel, character.miningSkillXp, MINING_SKILL_XP_PER_EXTRACTION);

  let added = false;
  const client = await pool.connect();
  try {
    await client.query("begin");
    added = await addItem(client, character.characterId, definition.itemCode, quantity);
    if (added) {
      await persistSkill(client, character.characterId, "mineracao", skillProgress.level, skillProgress.xp);
    }
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    if (node) {
      node.available = true;
      node.depletedAt = null;
    }
    throw error;
  } finally {
    client.release();
  }

  if (!added) {
    // GDD §9: 20 espaços. Um tipo de item novo que não cabe não esgota o veio — o jogador só
    // precisa abrir espaço (vender/usar) e tentar minerar de novo.
    if (node) {
      node.available = true;
      node.depletedAt = null;
    }
    sendError(character.socket, "INVENTORY_FULL", "Inventário cheio (20 espaços). Venda ou use algo antes de minerar mais.", true);
    return;
  }

  character.miningSkillLevel = skillProgress.level;
  character.miningSkillXp = skillProgress.xp;
  character.inventory.set(definition.itemCode, (character.inventory.get(definition.itemCode) ?? 0) + quantity);
  character.outboundSequence += 1;
  sendEnvelope(
    character.socket,
    "resource.mine.result",
    {
      nodeId: channel.nodeId,
      resourceCode: channel.resourceCode,
      quantityGained: quantity,
      xpAwarded: MINING_SKILL_XP_PER_EXTRACTION,
      economy: snapshotOf(character),
    },
    character.outboundSequence,
  );
  void completeTutorialStep(character, "extraiu_minerio").catch((error: unknown) => {
    console.error("Falha ao registrar passo do tutorial (extraiu_minerio):", error);
  });
}

/**
 * GDD §11: fundição só na forja. "O servidor reserva os ingredientes ao iniciar a receita" — por
 * isso, diferente de startMining, esta função já remove o insumo do inventário de imediato.
 */
export async function startCrafting(character: ConnectedCharacter, recipeCode: RecipeCode): Promise<CraftStartResult> {
  if (character.incapacitatedUntil !== null) return { ok: false, code: "INCAPACITATED" };
  // GDD §13: "Ferreiro: libera a forja e explica metalurgia" — sem conversar com ele, a forja
  // fica travada. Personagens criados antes deste corte já têm essa conversa retroativa (migração 007).
  if (!hasTalkedTo(character, "ferreiro")) return { ok: false, code: "FORGE_LOCKED" };

  const recipe = craftingRecipes[recipeCode];
  if (!recipe) return { ok: false, code: "UNKNOWN_RECIPE" };
  if (character.activeChannel !== null || pendingCharacters.has(character.characterId)) {
    return { ok: false, code: "ON_COOLDOWN" };
  }

  pendingCharacters.add(character.characterId);
  try {
    const client = await pool.connect();
    try {
      await client.query("begin");
      const removed = await removeItem(client, character.characterId, recipe.inputItemCode, recipe.inputQuantity);
      if (!removed) {
        await client.query("rollback");
        return { ok: false, code: "INSUFFICIENT_ITEMS" };
      }
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    } finally {
      client.release();
    }

    character.inventory.set(
      recipe.inputItemCode,
      Math.max(0, (character.inventory.get(recipe.inputItemCode) ?? 0) - recipe.inputQuantity),
    );
    const durationMs = skillAdjustedDurationMs(recipe.baseSeconds, character.metallurgySkillLevel);
    character.activeChannel = { type: "craft", recipeCode, startedAt: Date.now(), durationMs };
    return { ok: true, payload: { recipeCode, durationMs } };
  } finally {
    pendingCharacters.delete(character.characterId);
  }
}

/** Quanto do insumo devolver ao cancelar (GDD §11): metade do tempo ou mais, perde uma unidade. */
function craftRefundQuantity(recipe: CraftingRecipe, elapsedMs: number, durationMs: number): number {
  return elapsedMs < durationMs / 2 ? recipe.inputQuantity : Math.max(0, recipe.inputQuantity - 1);
}

/**
 * GDD §11: "cancelamento voluntário antes da metade do tempo devolve os ingredientes; depois da
 * metade, devolve dois dos três minérios." Reaproveitada também na desconexão (ver ws.ts) — o GDD
 * pede uma pausa de 60s antes de aplicar essa regra numa queda de conexão; simplificação
 * deliberada (mesmo espírito do resto do módulo): aplica a mesma regra na hora, sem a pausa.
 */
export async function cancelCrafting(character: ConnectedCharacter): Promise<CraftCancelResult> {
  const channel = character.activeChannel;
  if (!channel || channel.type !== "craft" || pendingCharacters.has(character.characterId)) {
    return { ok: false, code: "NO_ACTIVE_CHANNEL" };
  }

  const recipe = craftingRecipes[channel.recipeCode];
  const refundedQuantity = craftRefundQuantity(recipe, Date.now() - channel.startedAt, channel.durationMs);
  character.activeChannel = null;

  if (refundedQuantity > 0) {
    const client = await pool.connect();
    try {
      await client.query("begin");
      await addItem(client, character.characterId, recipe.inputItemCode, refundedQuantity);
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    } finally {
      client.release();
    }
    character.inventory.set(recipe.inputItemCode, (character.inventory.get(recipe.inputItemCode) ?? 0) + refundedQuantity);
  }

  return { ok: true, payload: { recipeCode: channel.recipeCode, refundedQuantity } };
}

/** Chamado por tickChannels quando o canal de fundição termina sem ser cancelado. */
async function completeCrafting(character: ConnectedCharacter, channel: CraftingChannelState): Promise<void> {
  const recipe = craftingRecipes[channel.recipeCode];
  const skillProgress = applySkillXp(character.metallurgySkillLevel, character.metallurgySkillXp, METALLURGY_SKILL_XP_PER_CRAFT);

  let added = false;
  const client = await pool.connect();
  try {
    await client.query("begin");
    added = await addItem(client, character.characterId, recipe.outputItemCode, recipe.outputQuantity);
    if (!added) {
      // O insumo já foi reservado ao iniciar o canal (GDD §11); sem espaço pro produto, devolve o
      // material em vez de simplesmente perdê-lo — decisão pró-jogador não coberta pelo GDD.
      await addItem(client, character.characterId, recipe.inputItemCode, recipe.inputQuantity);
    } else {
      await persistSkill(client, character.characterId, "metalurgia", skillProgress.level, skillProgress.xp);
      // GDD §13: "{personagem} fundiu o primeiro lingote de ferro em {data}" — registrado na
      // mesma transação da fundição; unique_key garante que só o primeiro fundido no mundo
      // inteiro vira crônica, mesmo sob corrida entre dois jogadores fundindo ao mesmo tempo.
      await recordEvent(
        {
          eventType: "primeiro_lingote",
          uniqueKey: `primeiro_lingote:${recipe.outputItemCode}`,
          characterId: character.characterId,
          data: { itemCode: recipe.outputItemCode },
        },
        client,
      );
    }
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }

  if (!added) {
    character.inventory.set(recipe.inputItemCode, (character.inventory.get(recipe.inputItemCode) ?? 0) + recipe.inputQuantity);
    sendError(character.socket, "INVENTORY_FULL", "Inventário cheio (20 espaços) — o material foi devolvido. Abra espaço e funda de novo.", true);
    return;
  }

  character.metallurgySkillLevel = skillProgress.level;
  character.metallurgySkillXp = skillProgress.xp;
  character.inventory.set(recipe.outputItemCode, (character.inventory.get(recipe.outputItemCode) ?? 0) + recipe.outputQuantity);
  character.outboundSequence += 1;
  sendEnvelope(
    character.socket,
    "craft.result",
    {
      recipeCode: channel.recipeCode,
      producedItemCode: recipe.outputItemCode,
      producedQuantity: recipe.outputQuantity,
      xpAwarded: METALLURGY_SKILL_XP_PER_CRAFT,
      economy: snapshotOf(character),
    },
    character.outboundSequence,
  );
  void completeTutorialStep(character, "fundiu_lingote").catch((error: unknown) => {
    console.error("Falha ao registrar passo do tutorial (fundiu_lingote):", error);
  });
}

/** Libera um canal de mineração ou fundição na desconexão, sem enviar nenhuma mensagem. */
export function abandonActiveChannel(character: ConnectedCharacter): void {
  if (!character.activeChannel) return;
  if (character.activeChannel.type === "mine") {
    abandonMiningChannel(character);
    return;
  }
  // GDD §11: queda de conexão aplica a mesma regra de cancelamento (ver nota em cancelCrafting).
  void cancelCrafting(character).catch((error: unknown) => {
    console.error("Falha ao cancelar fundição na desconexão:", error);
  });
}

/** Roda a cada tick da simulação: completa canais de mineração/fundição cujo tempo já passou. */
export function tickChannels(): void {
  const now = Date.now();
  for (const character of defaultInstance.list()) {
    const channel = character.activeChannel;
    if (!channel || now - channel.startedAt < channel.durationMs) continue;
    character.activeChannel = null;
    if (channel.type === "mine") {
      void completeMining(character, channel).catch((error: unknown) => {
        console.error("Falha ao completar mineração:", error);
      });
    } else {
      void completeCrafting(character, channel).catch((error: unknown) => {
        console.error("Falha ao completar fundição:", error);
      });
    }
  }
}

/** GDD §12: preços fixos, toda venda gera registro no livro-razão. */
export async function sellItem(
  character: ConnectedCharacter,
  itemCode: keyof typeof sellPrices,
  quantity: number,
): Promise<SellResult> {
  if (character.incapacitatedUntil !== null) return { ok: false, code: "INCAPACITATED" };

  const unitPrice = sellPrices[itemCode];
  if (unitPrice === undefined) return { ok: false, code: "ITEM_NOT_SELLABLE" };

  if (pendingCharacters.has(character.characterId)) return { ok: false, code: "ON_COOLDOWN" };

  const coinsEarned = unitPrice * quantity;
  pendingCharacters.add(character.characterId);
  try {
    const client = await pool.connect();
    try {
      await client.query("begin");
      const removed = await removeItem(client, character.characterId, itemCode, quantity);
      if (!removed) {
        await client.query("rollback");
        return { ok: false, code: "INSUFFICIENT_ITEMS" };
      }
      const applied = await applyLedgerEntry(client, character.characterId, coinsEarned, "venda_comerciante", itemCode);
      if (!applied) {
        // Não deveria acontecer (saldo só cresce numa venda), mas por segurança não deixa a
        // transação seguir se o livro-razão não puder registrar o crédito.
        await client.query("rollback");
        throw new Error("Falha ao registrar crédito de venda no livro-razão.");
      }
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    } finally {
      client.release();
    }

    character.coinBalance += coinsEarned;
    character.inventory.set(itemCode, Math.max(0, (character.inventory.get(itemCode) ?? 0) - quantity));
  } finally {
    pendingCharacters.delete(character.characterId);
  }

  // GDD §4 passo 10: "vender o lingote ao comerciante" — só lingotes contam pro tutorial, não
  // minério/couro (mesmo raciocínio de "uma mesma ação não pode conceder XP mais de uma vez").
  if (itemCode === "lingote_ferro" || itemCode === "lingote_cobre") {
    void completeTutorialStep(character, "vendeu_lingote").catch((error: unknown) => {
      console.error("Falha ao registrar passo do tutorial (vendeu_lingote):", error);
    });
  }

  return {
    ok: true,
    payload: {
      itemCode,
      quantitySold: quantity,
      coinsEarned,
      economy: snapshotOf(character),
    },
  };
}

/** GDD §12: compra ao comerciante (hoje só a poção tem preço de venda ao jogador). */
export async function buyItem(
  character: ConnectedCharacter,
  itemCode: keyof typeof buyPrices,
  quantity: number,
): Promise<BuyResult> {
  if (character.incapacitatedUntil !== null) return { ok: false, code: "INCAPACITATED" };

  const unitPrice = buyPrices[itemCode];
  if (unitPrice === undefined) return { ok: false, code: "ITEM_NOT_BUYABLE" };

  if (pendingCharacters.has(character.characterId)) return { ok: false, code: "ON_COOLDOWN" };

  const coinsSpent = unitPrice * quantity;
  pendingCharacters.add(character.characterId);
  try {
    const client = await pool.connect();
    try {
      await client.query("begin");
      const charged = await applyLedgerEntry(client, character.characterId, -coinsSpent, "compra_comerciante", itemCode);
      if (!charged) {
        await client.query("rollback");
        return { ok: false, code: "INSUFFICIENT_COINS" };
      }
      const added = await addItem(client, character.characterId, itemCode, quantity);
      if (!added) {
        await client.query("rollback");
        return { ok: false, code: "INVENTORY_FULL" };
      }
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    } finally {
      client.release();
    }

    character.coinBalance -= coinsSpent;
    character.inventory.set(itemCode, (character.inventory.get(itemCode) ?? 0) + quantity);
  } finally {
    pendingCharacters.delete(character.characterId);
  }

  return {
    ok: true,
    payload: {
      itemCode,
      quantityBought: quantity,
      coinsSpent,
      economy: snapshotOf(character),
    },
  };
}

/**
 * GDD §8 ("botão para usar poção") e §12. Hoje só a poção é usável — consome 1 unidade e cura
 * POTION_HEAL_AMOUNT de HP, sem passar de maxHp.
 */
export async function useItem(character: ConnectedCharacter, itemCode: ItemCode): Promise<UseItemResult> {
  if (character.incapacitatedUntil !== null) return { ok: false, code: "INCAPACITATED" };
  if (itemCode !== "pocao") return { ok: false, code: "ITEM_NOT_USABLE" };

  if (pendingCharacters.has(character.characterId)) return { ok: false, code: "ON_COOLDOWN" };

  const newHp = Math.min(character.maxHp, character.hp + POTION_HEAL_AMOUNT);
  pendingCharacters.add(character.characterId);
  try {
    const client = await pool.connect();
    try {
      await client.query("begin");
      const removed = await removeItem(client, character.characterId, itemCode, 1);
      if (!removed) {
        await client.query("rollback");
        return { ok: false, code: "INSUFFICIENT_ITEMS" };
      }
      await client.query(
        `update characters set hp = $2, version = version + 1, updated_at = now() where id = $1`,
        [character.characterId, newHp],
      );
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    } finally {
      client.release();
    }

    character.hp = newHp;
    character.inventory.set(itemCode, Math.max(0, (character.inventory.get(itemCode) ?? 0) - 1));
  } finally {
    pendingCharacters.delete(character.characterId);
  }

  return {
    ok: true,
    payload: {
      itemCode,
      hp: character.hp,
      maxHp: character.maxHp,
      economy: snapshotOf(character),
    },
  };
}

/**
 * GDD §9: "equipamento possui dois espaços ativos: mão principal e ferramenta". Equipar exige
 * possuir o item; `itemCode: null` desequipa o espaço. Sem trava de pendingCharacters — equipar
 * não muda inventário nem moeda, só qual item de um espaço já possuído está ativo.
 */
export async function equipItem(
  character: ConnectedCharacter,
  slot: EquipmentSlot,
  itemCode: ItemCode | null,
): Promise<EquipResult> {
  if (itemCode !== null) {
    if (!equippableItemsBySlot[slot].includes(itemCode)) return { ok: false, code: "ITEM_NOT_EQUIPPABLE" };
    if ((character.inventory.get(itemCode) ?? 0) <= 0) return { ok: false, code: "INSUFFICIENT_ITEMS" };
  }

  const client = await pool.connect();
  try {
    await client.query("begin");
    await setEquipment(client, character.characterId, slot, itemCode);
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }

  if (slot === "main_hand") character.equipment.mainHand = itemCode;
  else character.equipment.tool = itemCode;

  return { ok: true, payload: { slot, itemCode, economy: snapshotOf(character) } };
}

/** Roda a cada tick da simulação: reaparece veios esgotados após o tempo de respawn (GDD §10). */
export function tickResourceNodes(): void {
  const now = Date.now();
  for (const node of defaultInstance.listResourceNodes()) {
    if (node.available || node.depletedAt === null) continue;
    if (now - node.depletedAt < miningDefinitions[node.resourceCode].respawnSeconds * 1000) continue;
    node.available = true;
    node.depletedAt = null;
  }
}

const snapshotOf = economySnapshotOf;

function randomInt(min: number, max: number): number {
  return min + Math.floor(Math.random() * (max - min + 1));
}
