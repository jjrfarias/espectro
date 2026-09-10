import { z } from "zod";

/**
 * Contratos do protocolo em tempo real (WebSocket), versão 1.
 * Cobre as mensagens implementadas nos Cortes 1 (presença compartilhada) e 2 (combate).
 * Ver docs/ARQUITETURA-MVP.md, seção 8, para o desenho completo do envelope.
 */

export const PROTOCOL_VERSION = 1 as const;

export const envelopeSchema = z.object({
  v: z.literal(PROTOCOL_VERSION),
  type: z.string().min(1),
  requestId: z.string().uuid(),
  sequence: z.number().int().nonnegative(),
  sentAt: z.string().datetime(),
  payload: z.unknown(),
});
export type Envelope = z.infer<typeof envelopeSchema>;

export const errorCodes = [
  "UNAUTHORIZED",
  "INVALID_ENVELOPE",
  "UNSUPPORTED_TYPE",
  "VALIDATION_ERROR",
  "RATE_LIMITED",
  "NOT_IN_WORLD",
  "NO_CHARACTER",
  "TARGET_NOT_FOUND",
  "OUT_OF_RANGE",
  "ON_COOLDOWN",
  "INCAPACITATED",
  "PERSISTENCE_FAILED",
  "NODE_NOT_FOUND",
  "NODE_DEPLETED",
  "INSUFFICIENT_ITEMS",
  "UNKNOWN_RECIPE",
  "ITEM_NOT_SELLABLE",
  "ITEM_NOT_EQUIPPABLE",
  "TOOL_NOT_EQUIPPED",
  "WEAPON_NOT_EQUIPPED",
  "INVENTORY_FULL",
  "NO_UNSPENT_POINTS",
  "ITEM_NOT_BUYABLE",
  "INSUFFICIENT_COINS",
  "ITEM_NOT_USABLE",
  "NO_ACTIVE_CHANNEL",
] as const;
export type ErrorCode = (typeof errorCodes)[number];

export const errorPayloadSchema = z.object({
  code: z.enum(errorCodes),
  message: z.string(),
  retryable: z.boolean(),
});
export type ErrorPayload = z.infer<typeof errorPayloadSchema>;

// cliente -> servidor: entrar na instância padrão do mundo.
export const worldJoinPayloadSchema = z.object({});
export type WorldJoinPayload = z.infer<typeof worldJoinPayloadSchema>;

// cliente -> servidor: intenção de movimento do quadro atual.
export const movementInputPayloadSchema = z.object({
  moveX: z.number().min(-1).max(1),
  moveZ: z.number().min(-1).max(1),
  facingY: z.number(),
});
export type MovementInputPayload = z.infer<typeof movementInputPayloadSchema>;

const vector3Schema = z.object({
  x: z.number(),
  y: z.number(),
  z: z.number(),
});

const snapshotCharacterSchema = z.object({
  characterId: z.string().uuid(),
  name: z.string(),
  position: vector3Schema,
  facingY: z.number(),
  hp: z.number(),
  maxHp: z.number(),
});

// GDD-MVP.md §8: só dois tipos de inimigo no MVP, sem conteúdo orientado a dados ainda.
export const enemyDefinitionCodes = ["lobo", "javali"] as const;
export type EnemyDefinitionCode = (typeof enemyDefinitionCodes)[number];

// GDD-MVP.md §10: só ferro e cobre no MVP. Declarado aqui (não perto do resto da economia, mais
// abaixo) porque snapshotResourceNodeSchema, usado no world.snapshot, precisa dele antes.
export const resourceCodes = ["ferro", "cobre"] as const;
export type ResourceCode = (typeof resourceCodes)[number];

const snapshotEnemySchema = z.object({
  enemyId: z.string(),
  definitionCode: z.enum(enemyDefinitionCodes),
  position: vector3Schema,
  hp: z.number(),
  maxHp: z.number(),
  alive: z.boolean(),
});

const snapshotResourceNodeSchema = z.object({
  nodeId: z.string(),
  resourceCode: z.enum(resourceCodes),
  position: vector3Schema,
  available: z.boolean(),
});

// servidor -> cliente: estado visível confirmado da instância.
export const worldSnapshotPayloadSchema = z.object({
  instanceId: z.string(),
  serverTime: z.string().datetime(),
  movementSpeed: z.number().positive().optional(),
  self: snapshotCharacterSchema,
  others: z.array(snapshotCharacterSchema),
  enemies: z.array(snapshotEnemySchema),
  resourceNodes: z.array(snapshotResourceNodeSchema),
});
export type WorldSnapshotPayload = z.infer<typeof worldSnapshotPayloadSchema>;

// cliente -> servidor: solicita ataque a um inimigo (sem PvP no MVP, alvo é sempre um inimigo).
export const combatAttackRequestPayloadSchema = z.object({
  targetId: z.string().min(1),
});
export type CombatAttackRequestPayload = z.infer<typeof combatAttackRequestPayloadSchema>;

// servidor -> cliente: resultado confirmado de um ataque solicitado pelo próprio jogador.
export const combatResolvedPayloadSchema = z.object({
  targetId: z.string(),
  damage: z.number(),
  targetHp: z.number(),
  targetMaxHp: z.number(),
  targetDied: z.boolean(),
  xpAwarded: z.number(),
  characterXp: z.number(),
  characterLevel: z.number(),
  leveledUp: z.boolean(),
});
export type CombatResolvedPayload = z.infer<typeof combatResolvedPayloadSchema>;

// servidor -> cliente: o próprio personagem sofreu dano (iniciativa de um inimigo, não é resposta a um request).
export const combatPlayerDamagedPayloadSchema = z.object({
  sourceEnemyId: z.string(),
  damage: z.number(),
  hp: z.number(),
  maxHp: z.number(),
  died: z.boolean(),
  respawnPosition: vector3Schema.nullable(),
});
export type CombatPlayerDamagedPayload = z.infer<typeof combatPlayerDamagedPayloadSchema>;

// GDD §6: "cada nível concede um ponto de atributo", entre os quatro atributos usados no MVP.
export const attributeCodes = ["strength", "agility", "vitality", "resistance"] as const;
export type AttributeCode = (typeof attributeCodes)[number];

const attributesSchema = z.object({
  strength: z.number().int(),
  agility: z.number().int(),
  vitality: z.number().int(),
  resistance: z.number().int(),
});

// servidor -> cliente: atributos e pontos não gastos do próprio personagem. Enviado ao entrar e
// após cada alocação — não faz parte do world.snapshot pelo mesmo motivo do economy.snapshot
// (estado privado, não visível a outros jogadores).
export const attributesSnapshotPayloadSchema = z.object({
  attributes: attributesSchema,
  unspentPoints: z.number().int().nonnegative(),
  maxHp: z.number().int().positive(),
});
export type AttributesSnapshotPayload = z.infer<typeof attributesSnapshotPayloadSchema>;

// cliente -> servidor: gasta um ponto não alocado num dos quatro atributos (GDD §6).
export const attributeAllocateRequestPayloadSchema = z.object({
  attribute: z.enum(attributeCodes),
});
export type AttributeAllocateRequestPayload = z.infer<typeof attributeAllocateRequestPayloadSchema>;

// Corte 3 (GDD-MVP.md §9-§12): inventário, mineração, metalurgia e venda ao comerciante.
export const itemCodes = [
  "espada_simples",
  "picareta_simples",
  "pocao",
  "minerio_ferro",
  "minerio_cobre",
  "lingote_ferro",
  "lingote_cobre",
  "couro",
] as const;
export type ItemCode = (typeof itemCodes)[number];

export const recipeCodes = ["lingote_ferro", "lingote_cobre"] as const;
export type RecipeCode = (typeof recipeCodes)[number];

const inventoryItemSchema = z.object({
  itemCode: z.enum(itemCodes),
  quantity: z.number().int().nonnegative(),
});
export type InventoryItem = z.infer<typeof inventoryItemSchema>;

// GDD §9: "equipamento possui dois espaços ativos: mão principal e ferramenta".
export const equipmentSlots = ["main_hand", "tool"] as const;
export type EquipmentSlot = (typeof equipmentSlots)[number];

const equipmentSchema = z.object({
  mainHand: z.enum(itemCodes).nullable(),
  tool: z.enum(itemCodes).nullable(),
});
export type Equipment = z.infer<typeof equipmentSchema>;

// servidor -> cliente: estado privado de economia do próprio personagem (não é visível a outros
// jogadores, por isso não faz parte do world.snapshot). Enviado ao entrar e após cada ação.
export const economySnapshotPayloadSchema = z.object({
  coinBalance: z.number().int().nonnegative(),
  inventory: z.array(inventoryItemSchema),
  equipment: equipmentSchema,
});
export type EconomySnapshotPayload = z.infer<typeof economySnapshotPayloadSchema>;

// cliente -> servidor: equipa (ou desequipa, com itemCode null) um item num dos dois espaços.
export const equipRequestPayloadSchema = z.object({
  slot: z.enum(equipmentSlots),
  itemCode: z.enum(itemCodes).nullable(),
});
export type EquipRequestPayload = z.infer<typeof equipRequestPayloadSchema>;

// servidor -> cliente: confirmação de equipar/desequipar.
export const equipResultPayloadSchema = z.object({
  slot: z.enum(equipmentSlots),
  itemCode: z.enum(itemCodes).nullable(),
  economy: economySnapshotPayloadSchema,
});
export type EquipResultPayload = z.infer<typeof equipResultPayloadSchema>;

const resourceNodeSchema = z.object({
  nodeId: z.string(),
  resourceCode: z.enum(resourceCodes),
  position: vector3Schema,
  available: z.boolean(),
});
export type ResourceNodeSnapshot = z.infer<typeof resourceNodeSchema>;

// cliente -> servidor: solicita extrair um veio de recurso (GDD §10).
export const mineRequestPayloadSchema = z.object({
  nodeId: z.string().min(1),
});
export type MineRequestPayload = z.infer<typeof mineRequestPayloadSchema>;

// servidor -> cliente: resultado confirmado de uma extração.
export const mineResultPayloadSchema = z.object({
  nodeId: z.string(),
  resourceCode: z.enum(resourceCodes),
  quantityGained: z.number().int().positive(),
  xpAwarded: z.number(),
  economy: economySnapshotPayloadSchema,
});
export type MineResultPayload = z.infer<typeof mineResultPayloadSchema>;

// servidor -> cliente: extração iniciada (GDD §10, passo 3: "manter o comando de interação
// durante a extração") — `resource.mine.result` só chega ao fim de `durationMs`, se nada cancelar antes.
export const mineStartedPayloadSchema = z.object({
  nodeId: z.string(),
  resourceCode: z.enum(resourceCodes),
  durationMs: z.number().int().positive(),
});
export type MineStartedPayload = z.infer<typeof mineStartedPayloadSchema>;

// servidor -> cliente: extração interrompida antes de completar (GDD §10: "mover-se, sofrer dano
// ou perder conexão cancela a extração sem recompensa"). Sem mensagem para o caso de desconexão
// (o próprio socket já caiu).
export const mineCancelReasons = ["moved", "damaged"] as const;
export const mineCancelledPayloadSchema = z.object({
  nodeId: z.string(),
  reason: z.enum(mineCancelReasons),
});
export type MineCancelledPayload = z.infer<typeof mineCancelledPayloadSchema>;

// cliente -> servidor: solicita fundir uma receita na forja (GDD §11).
export const craftRequestPayloadSchema = z.object({
  recipeCode: z.enum(recipeCodes),
});
export type CraftRequestPayload = z.infer<typeof craftRequestPayloadSchema>;

// servidor -> cliente: resultado confirmado de uma fundição.
export const craftResultPayloadSchema = z.object({
  recipeCode: z.enum(recipeCodes),
  producedItemCode: z.enum(itemCodes),
  producedQuantity: z.number().int().positive(),
  xpAwarded: z.number(),
  economy: economySnapshotPayloadSchema,
});
export type CraftResultPayload = z.infer<typeof craftResultPayloadSchema>;

// servidor -> cliente: fundição iniciada — ingredientes já reservados (GDD §11: "o servidor
// reserva os ingredientes ao iniciar a receita"). `craft.result` só chega ao fim de `durationMs`.
export const craftStartedPayloadSchema = z.object({
  recipeCode: z.enum(recipeCodes),
  durationMs: z.number().int().positive(),
});
export type CraftStartedPayload = z.infer<typeof craftStartedPayloadSchema>;

// cliente -> servidor: cancela voluntariamente a fundição em andamento (GDD §11).
export const craftCancelRequestPayloadSchema = z.object({});
export type CraftCancelRequestPayload = z.infer<typeof craftCancelRequestPayloadSchema>;

// servidor -> cliente: fundição cancelada (voluntariamente ou por queda de conexão detectada na
// reconexão). GDD §11: "cancelamento voluntário antes da metade do tempo devolve os ingredientes;
// depois da metade, devolve dois dos três minérios" — `refundedQuantity` generaliza essa regra
// pra qualquer receita (quantidade total do insumo, se antes da metade, ou quantidade menos 1).
export const craftCancelledPayloadSchema = z.object({
  recipeCode: z.enum(recipeCodes),
  refundedQuantity: z.number().int().nonnegative(),
});
export type CraftCancelledPayload = z.infer<typeof craftCancelledPayloadSchema>;

// cliente -> servidor: solicita vender itens ao comerciante (GDD §12).
export const sellRequestPayloadSchema = z.object({
  itemCode: z.enum(itemCodes),
  quantity: z.number().int().positive(),
});
export type SellRequestPayload = z.infer<typeof sellRequestPayloadSchema>;

// servidor -> cliente: resultado confirmado de uma venda.
export const sellResultPayloadSchema = z.object({
  itemCode: z.enum(itemCodes),
  quantitySold: z.number().int().positive(),
  coinsEarned: z.number().int().positive(),
  economy: economySnapshotPayloadSchema,
});
export type SellResultPayload = z.infer<typeof sellResultPayloadSchema>;

// cliente -> servidor: solicita comprar itens do comerciante (GDD §12, hoje só a poção é vendida ao jogador).
export const buyRequestPayloadSchema = z.object({
  itemCode: z.enum(itemCodes),
  quantity: z.number().int().positive(),
});
export type BuyRequestPayload = z.infer<typeof buyRequestPayloadSchema>;

// servidor -> cliente: resultado confirmado de uma compra.
export const buyResultPayloadSchema = z.object({
  itemCode: z.enum(itemCodes),
  quantityBought: z.number().int().positive(),
  coinsSpent: z.number().int().positive(),
  economy: economySnapshotPayloadSchema,
});
export type BuyResultPayload = z.infer<typeof buyResultPayloadSchema>;

// cliente -> servidor: consome um item usável do inventário (GDD §8: "botão para usar poção").
export const useItemRequestPayloadSchema = z.object({
  itemCode: z.enum(itemCodes),
});
export type UseItemRequestPayload = z.infer<typeof useItemRequestPayloadSchema>;

// servidor -> cliente: resultado confirmado de usar um item (hoje só a poção cura HP).
export const useItemResultPayloadSchema = z.object({
  itemCode: z.enum(itemCodes),
  hp: z.number().int().nonnegative(),
  maxHp: z.number().int().positive(),
  economy: economySnapshotPayloadSchema,
});
export type UseItemResultPayload = z.infer<typeof useItemResultPayloadSchema>;

// Chat local (GDD-MVP.md §2/§9): mensagens ficam retidas (chat_messages) e alcançam só a
// instância do remetente — sem canais globais nem sussurro no MVP.
export const CHAT_MESSAGE_MAX_LENGTH = 240;

export const chatSendPayloadSchema = z.object({
  content: z.string().trim().min(1).max(CHAT_MESSAGE_MAX_LENGTH),
});
export type ChatSendPayload = z.infer<typeof chatSendPayloadSchema>;

// servidor -> cliente: mensagem já moderada, entregue a todos na mesma instância (inclusive quem enviou).
export const chatMessagePayloadSchema = z.object({
  characterId: z.string().uuid(),
  characterName: z.string(),
  content: z.string(),
  sentAt: z.string().datetime(),
});
export type ChatMessagePayload = z.infer<typeof chatMessagePayloadSchema>;

export const clientMessageSchemas = {
  "world.join": worldJoinPayloadSchema,
  "movement.input": movementInputPayloadSchema,
  "combat.attack.request": combatAttackRequestPayloadSchema,
  "resource.mine.request": mineRequestPayloadSchema,
  "craft.request": craftRequestPayloadSchema,
  "craft.cancel": craftCancelRequestPayloadSchema,
  "trade.sell.request": sellRequestPayloadSchema,
  "chat.send": chatSendPayloadSchema,
  "equip.request": equipRequestPayloadSchema,
  "attribute.allocate": attributeAllocateRequestPayloadSchema,
  "trade.buy.request": buyRequestPayloadSchema,
  "item.use.request": useItemRequestPayloadSchema,
} as const;
export type ClientMessageType = keyof typeof clientMessageSchemas;

export const serverMessageSchemas = {
  "world.snapshot": worldSnapshotPayloadSchema,
  "combat.resolved": combatResolvedPayloadSchema,
  "combat.player_damaged": combatPlayerDamagedPayloadSchema,
  "economy.snapshot": economySnapshotPayloadSchema,
  "resource.mine.started": mineStartedPayloadSchema,
  "resource.mine.result": mineResultPayloadSchema,
  "resource.mine.cancelled": mineCancelledPayloadSchema,
  "craft.started": craftStartedPayloadSchema,
  "craft.result": craftResultPayloadSchema,
  "craft.cancelled": craftCancelledPayloadSchema,
  "trade.sell.result": sellResultPayloadSchema,
  "chat.message": chatMessagePayloadSchema,
  "equip.result": equipResultPayloadSchema,
  "attributes.snapshot": attributesSnapshotPayloadSchema,
  "trade.buy.result": buyResultPayloadSchema,
  "item.use.result": useItemResultPayloadSchema,
  error: errorPayloadSchema,
} as const;
export type ServerMessageType = keyof typeof serverMessageSchemas;

export function isClientMessageType(type: string): type is ClientMessageType {
  return Object.hasOwn(clientMessageSchemas, type);
}
