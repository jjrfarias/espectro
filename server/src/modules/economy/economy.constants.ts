import type { EquipmentSlot, ItemCode, RecipeCode, ResourceCode } from "@espectro/contracts";

/**
 * Constantes puras da economia. Espelham docs/GDD-MVP.md §10-§12 literalmente — qualquer
 * mudança de balanceamento deve ser feita ali primeiro, e refletida aqui.
 */

export interface MiningDefinition {
  baseSeconds: number;
  minQuantity: number;
  maxQuantity: number;
  respawnSeconds: number;
  itemCode: ItemCode;
}

// GDD §10, tabela "Parâmetros iniciais".
export const miningDefinitions: Record<ResourceCode, MiningDefinition> = {
  ferro: { baseSeconds: 4, minQuantity: 1, maxQuantity: 2, respawnSeconds: 45, itemCode: "minerio_ferro" },
  cobre: { baseSeconds: 6, minQuantity: 1, maxQuantity: 1, respawnSeconds: 90, itemCode: "minerio_cobre" },
};

// GDD §7: "-2% no tempo de extração"/"-2% no tempo de fabricação" por nível da habilidade
// correspondente. Nível 1 não dá bônus; nível máximo (10) reduz 18%.
export function skillAdjustedDurationMs(baseSeconds: number, skillLevel: number): number {
  return Math.round(baseSeconds * 1000 * (1 - 0.02 * (skillLevel - 1)));
}

// GDD §10: "aproximar-se de um veio disponível" — mesma ordem de grandeza do alcance de ataque
// (PLAYER_ATTACK_RANGE_UNITS em enemy-definitions.ts), um pouco maior porque minério não se move.
export const MINING_RANGE_UNITS = 2.75;

// GDD §10: "o resultado aleatório é decidido pelo servidor" — XP de mineração não é especificado
// no GDD (só a fórmula genérica de progressão de habilidade existe); valor autoral, mesma ordem
// de grandeza do XP de espada por golpe (SWORD_SKILL_XP_PER_HIT = 5 em enemy-definitions.ts).
export const MINING_SKILL_XP_PER_EXTRACTION = 6;

export interface CraftingRecipe {
  inputItemCode: ItemCode;
  inputQuantity: number;
  outputItemCode: ItemCode;
  outputQuantity: number;
  baseSeconds: number;
}

// GDD §11, tabela de metalurgia.
export const craftingRecipes: Record<RecipeCode, CraftingRecipe> = {
  lingote_ferro: { inputItemCode: "minerio_ferro", inputQuantity: 3, outputItemCode: "lingote_ferro", outputQuantity: 1, baseSeconds: 5 },
  lingote_cobre: { inputItemCode: "minerio_cobre", inputQuantity: 3, outputItemCode: "lingote_cobre", outputQuantity: 1, baseSeconds: 7 },
};

// Autoral, mesmo raciocínio de MINING_SKILL_XP_PER_EXTRACTION: metalurgia demora mais que
// mineração, então rende um pouco mais de XP por ação.
export const METALLURGY_SKILL_XP_PER_CRAFT = 10;

// GDD §12, tabela "Preços iniciais". "—" (NPC não compra) fica de fora deste mapa.
export const sellPrices: Partial<Record<ItemCode, number>> = {
  minerio_ferro: 3,
  minerio_cobre: 5,
  lingote_ferro: 12,
  lingote_cobre: 20,
  couro: 4,
};

// GDD §9: "equipamento possui dois espaços ativos: mão principal e ferramenta" — item do MVP que
// não está aqui não é equipável em nenhum espaço (ex.: minério, lingote, poção).
export const equippableItemsBySlot: Record<EquipmentSlot, ItemCode[]> = {
  main_hand: ["espada_simples"],
  tool: ["picareta_simples"],
};

// GDD §12, tabela "Preços iniciais", coluna "NPC vende ao jogador" — hoje só a poção tem preço de
// venda ao jogador; os demais itens da tabela têm "—" nessa coluna (só compra do jogador).
export const buyPrices: Partial<Record<ItemCode, number>> = {
  pocao: 15,
};

// GDD §8 ("botão para usar poção") e §12 (poção é sumidouro de moeda) confirmam que a poção cura,
// mas o GDD não dá um valor numérico de cura. Valor autoral, mesmo raciocínio dos outros números
// de balanceamento não especificados neste módulo: metade do HP inicial (100 + Vitalidade×10, com
// Vitalidade=5) arredondado, uma cura significativa sem ser instantânea/total.
export const POTION_HEAL_AMOUNT = 75;
