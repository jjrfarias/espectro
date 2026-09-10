import type { ErrorCode } from "@espectro/contracts";
import type { FastifyInstance, FastifyRequest } from "fastify";
import type { RawData, WebSocket } from "ws";
import { env } from "../config/env.js";
import { verifyAccessToken } from "../modules/auth/tokens.js";
import { getCharacterCombatStateByAccountId, persistPosition } from "../modules/characters/characters.service.js";
import { resolveAttack } from "../modules/combat/combat.service.js";
import { maxHp } from "../modules/combat/formulas.js";
import { PLAYER_DEATH_INCAPACITATION_SECONDS } from "../modules/combat/enemy-definitions.js";
import { allocateAttributePoint } from "../modules/combat/attributes.service.js";
import { abandonActiveChannel, buyItem, cancelCrafting, cancelMining, equipItem, sellItem, startCrafting, startMining, useItem } from "../modules/economy/economy.service.js";
import { sendChatMessage } from "../modules/chat/chat.service.js";
import { type ConnectedCharacter, defaultInstance } from "../modules/world/instance.js";
import { applyMovement, clampDeltaSeconds } from "../modules/world/movement.js";
import { FixedWindowRateLimiter } from "../modules/world/rate-limiter.js";
import { buildSnapshotForCharacter } from "../modules/world/snapshot.js";
import { UnsupportedTypeError, parseClientMessage, sendEnvelope, sendError } from "./envelope.js";

export async function worldGateway(app: FastifyInstance): Promise<void> {
  app.get("/world", { websocket: true }, (socket, request) => {
    handleConnection(socket, request).catch((error: unknown) => {
      app.log.error(error, "Falha ao processar conexão do mundo");
      sendError(socket, "NOT_IN_WORLD", "Não foi possível entrar no mundo agora.", true);
      socket.close(1011, "internal-error");
    });
  });
}

async function handleConnection(socket: WebSocket, request: FastifyRequest): Promise<void> {
  // `ws` começa a decodificar frames assim que o socket é promovido, antes de qualquer código
  // deste handler rodar. Se anexássemos os listeners só depois do `await` abaixo, uma mensagem
  // que chegasse durante a consulta ao personagem seria perdida (EventEmitter não enfileira
  // eventos sem listener). Por isso os listeners são síncronos e mensagens são bufferizadas até
  // o personagem estar pronto.
  let character: ConnectedCharacter | undefined;
  const pendingMessages: RawData[] = [];

  socket.on("message", (raw: RawData) => {
    if (character) {
      handleMessage(raw, character);
    } else {
      pendingMessages.push(raw);
    }
  });

  socket.on("close", () => {
    if (!character) return;
    const wasJoined = defaultInstance.leave(character.characterId);
    if (wasJoined) {
      abandonActiveChannel(character);
      persistPosition(character.characterId, { ...character.position, facingY: character.facingY }).catch(
        (error: unknown) => {
          console.error("Falha ao persistir posição na desconexão:", error);
        },
      );
    }
  });

  const token = extractToken(request);
  if (!token) {
    sendError(socket, "UNAUTHORIZED", "Token de acesso ausente.", false);
    socket.close(4001, "unauthorized");
    return;
  }

  let accountId: string;
  try {
    accountId = verifyAccessToken(token).accountId;
  } catch {
    sendError(socket, "UNAUTHORIZED", "Token de acesso inválido ou expirado.", false);
    socket.close(4001, "unauthorized");
    return;
  }

  const characterState = await getCharacterCombatStateByAccountId(accountId);
  if (!characterState) {
    sendError(socket, "NO_CHARACTER", "Esta conta ainda não tem personagem.", false);
    socket.close(4002, "no-character");
    return;
  }

  character = {
    characterId: characterState.id,
    accountId,
    name: characterState.name,
    position: {
      x: characterState.position_json.x,
      y: characterState.position_json.y,
      z: characterState.position_json.z,
    },
    facingY: characterState.position_json.facingY,
    socket,
    outboundSequence: 0,
    lastInputAt: Date.now(),
    movementRateLimiter: new FixedWindowRateLimiter(env.MOVEMENT_MAX_INPUT_HZ, 1000),
    dirtyPosition: false,
    hp: characterState.hp,
    maxHp: maxHp(characterState.attributes.vitality),
    attributes: characterState.attributes,
    unspentAttributePoints: characterState.unspentAttributePoints,
    level: characterState.level,
    xp: characterState.xp,
    swordSkillLevel: characterState.swordSkillLevel,
    swordSkillXp: characterState.swordSkillXp,
    lastAttackAt: 0,
    incapacitatedUntil: characterState.hp <= 0 ? Date.now() + PLAYER_DEATH_INCAPACITATION_SECONDS * 1000 : null,
    coinBalance: characterState.coinBalance,
    inventory: new Map(characterState.inventory.map((item) => [item.itemCode, item.quantity])),
    equipment: { ...characterState.equipment },
    miningSkillLevel: characterState.miningSkillLevel,
    miningSkillXp: characterState.miningSkillXp,
    metallurgySkillLevel: characterState.metallurgySkillLevel,
    metallurgySkillXp: characterState.metallurgySkillXp,
    activeChannel: null,
    chatRateLimiter: new FixedWindowRateLimiter(env.CHAT_MAX_MESSAGES_PER_10S, 10_000),
  };

  for (const raw of pendingMessages) {
    handleMessage(raw, character);
  }
}

function handleMessage(raw: RawData, character: ConnectedCharacter): void {
  let message;
  try {
    message = parseClientMessage(JSON.parse(raw.toString()));
  } catch (error) {
    const code = error instanceof UnsupportedTypeError ? "UNSUPPORTED_TYPE" : "INVALID_ENVELOPE";
    replyError(character, code, error instanceof Error ? error.message : "Mensagem inválida.", false);
    return;
  }

  const isJoined = defaultInstance.get(character.characterId) !== undefined;

  if (message.type === "world.join") {
    if (!isJoined) {
      defaultInstance.join(character);
      replySnapshot(character);
      replyEconomySnapshot(character);
      replyAttributesSnapshot(character);
    }
    return;
  }

  if (message.type === "movement.input") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de se mover.", true);
      return;
    }
    if (character.incapacitatedUntil !== null) {
      replyError(character, "INCAPACITATED", "Você está incapacitado.", true);
      return;
    }
    if (!character.movementRateLimiter.tryConsume()) {
      replyError(character, "RATE_LIMITED", "Muitas mensagens de movimento.", true);
      return;
    }
    const now = Date.now();
    const dtSeconds = clampDeltaSeconds((now - character.lastInputAt) / 1000);
    character.lastInputAt = now;
    const payload = message.payload as { moveX: number; moveZ: number; facingY: number };
    if (payload.moveX !== 0 || payload.moveZ !== 0) {
      // GDD §10: "mover-se ... cancela a extração sem recompensa" — só um deslocamento real
      // cancela, não os quadros de input parado (moveX/moveZ = 0) enviados enquanto o canal corre.
      cancelMining(character, "moved");
    }
    character.position = applyMovement(character.position, payload, dtSeconds, env.MOVEMENT_MAX_SPEED_UNITS_PER_SEC);
    character.facingY = payload.facingY;
    character.dirtyPosition = true;
    return;
  }

  if (message.type === "combat.attack.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de atacar.", true);
      return;
    }
    const payload = message.payload as { targetId: string };
    void resolveAttack(character, payload.targetId).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "combat.resolved", result.payload, character.outboundSequence);
        return;
      }
      const retryable = result.code === "ON_COOLDOWN" || result.code === "OUT_OF_RANGE";
      replyError(character, result.code, describeAttackFailure(result.code), retryable);
    }).catch((error: unknown) => {
      console.error("Falha ao persistir ataque:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível confirmar o ataque. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "resource.mine.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de minerar.", true);
      return;
    }
    const payload = message.payload as { nodeId: string };
    const result = startMining(character, payload.nodeId);
    if (result.ok) {
      character.outboundSequence += 1;
      sendEnvelope(character.socket, "resource.mine.started", result.payload, character.outboundSequence);
      return;
    }
    const retryable = result.code === "ON_COOLDOWN" || result.code === "OUT_OF_RANGE" || result.code === "NODE_DEPLETED";
    replyError(character, result.code, describeMineFailure(result.code), retryable);
    return;
  }

  if (message.type === "craft.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de fundir.", true);
      return;
    }
    const payload = message.payload as { recipeCode: "lingote_ferro" | "lingote_cobre" };
    void startCrafting(character, payload.recipeCode).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "craft.started", result.payload, character.outboundSequence);
        return;
      }
      replyError(character, result.code, describeCraftFailure(result.code), result.code === "ON_COOLDOWN");
    }).catch((error: unknown) => {
      console.error("Falha ao persistir início da fundição:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível iniciar a fundição. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "craft.cancel") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de cancelar a fundição.", true);
      return;
    }
    void cancelCrafting(character).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "craft.cancelled", result.payload, character.outboundSequence);
        return;
      }
      replyError(character, result.code, "Não há fundição em andamento para cancelar.", false);
    }).catch((error: unknown) => {
      console.error("Falha ao cancelar fundição:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível cancelar a fundição. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "trade.sell.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de vender.", true);
      return;
    }
    const payload = message.payload as { itemCode: Parameters<typeof sellItem>[1]; quantity: number };
    void sellItem(character, payload.itemCode, payload.quantity).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "trade.sell.result", result.payload, character.outboundSequence);
        return;
      }
      replyError(character, result.code, describeSellFailure(result.code), result.code === "ON_COOLDOWN");
    }).catch((error: unknown) => {
      console.error("Falha ao persistir venda:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível confirmar a venda. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "trade.buy.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de comprar.", true);
      return;
    }
    const payload = message.payload as { itemCode: Parameters<typeof buyItem>[1]; quantity: number };
    void buyItem(character, payload.itemCode, payload.quantity).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "trade.buy.result", result.payload, character.outboundSequence);
        return;
      }
      replyError(character, result.code, describeBuyFailure(result.code), result.code === "ON_COOLDOWN");
    }).catch((error: unknown) => {
      console.error("Falha ao persistir compra:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível confirmar a compra. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "item.use.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de usar um item.", true);
      return;
    }
    const payload = message.payload as { itemCode: Parameters<typeof useItem>[1] };
    void useItem(character, payload.itemCode).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "item.use.result", result.payload, character.outboundSequence);
        return;
      }
      replyError(character, result.code, describeUseItemFailure(result.code), result.code === "ON_COOLDOWN");
    }).catch((error: unknown) => {
      console.error("Falha ao persistir uso de item:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível usar o item. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "chat.send") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de conversar.", true);
      return;
    }
    const payload = message.payload as { content: string };
    void sendChatMessage(character, payload.content).then((result) => {
      if (!result.ok) {
        replyError(character, result.code, "Aguarde antes de enviar outra mensagem.", true);
      }
    }).catch((error: unknown) => {
      console.error("Falha ao persistir mensagem de chat:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível enviar a mensagem. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "equip.request") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de equipar.", true);
      return;
    }
    const payload = message.payload as { slot: Parameters<typeof equipItem>[1]; itemCode: Parameters<typeof equipItem>[2] };
    void equipItem(character, payload.slot, payload.itemCode).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "equip.result", result.payload, character.outboundSequence);
        return;
      }
      const message = result.code === "INSUFFICIENT_ITEMS" ? "Você não tem esse item." : "Esse item não pode ser equipado nesse espaço.";
      replyError(character, result.code, message, false);
    }).catch((error: unknown) => {
      console.error("Falha ao persistir equipamento:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível equipar. Tente novamente.", true);
    });
    return;
  }

  if (message.type === "attribute.allocate") {
    if (!isJoined) {
      replyError(character, "NOT_IN_WORLD", "Envie world.join antes de alocar atributos.", true);
      return;
    }
    const payload = message.payload as { attribute: Parameters<typeof allocateAttributePoint>[1] };
    void allocateAttributePoint(character, payload.attribute).then((result) => {
      if (result.ok) {
        character.outboundSequence += 1;
        sendEnvelope(character.socket, "attributes.snapshot", result.payload, character.outboundSequence);
        return;
      }
      replyError(character, result.code, "Você não tem pontos de atributo disponíveis.", false);
    }).catch((error: unknown) => {
      console.error("Falha ao persistir alocação de atributo:", error);
      replyError(character, "PERSISTENCE_FAILED", "Não foi possível alocar o ponto de atributo. Tente novamente.", true);
    });
  }
}

function describeMineFailure(code: string): string {
  switch (code) {
    case "NODE_NOT_FOUND":
      return "Veio de recurso inválido.";
    case "NODE_DEPLETED":
      return "Este veio está esgotado. Aguarde ele reaparecer.";
    case "OUT_OF_RANGE":
      return "Aproxime-se do veio para minerar.";
    case "ON_COOLDOWN":
      return "Você já está ocupado com outra ação (minerando ou fundindo).";
    case "INCAPACITATED":
      return "Você está incapacitado.";
    case "TOOL_NOT_EQUIPPED":
      return "Equipe uma picareta antes de minerar.";
    default:
      return "Não foi possível minerar.";
  }
}

function describeCraftFailure(code: string): string {
  switch (code) {
    case "UNKNOWN_RECIPE":
      return "Receita desconhecida.";
    case "INSUFFICIENT_ITEMS":
      return "Você não tem minério suficiente.";
    case "ON_COOLDOWN":
      return "Você já está ocupado com outra ação (minerando ou fundindo).";
    case "INCAPACITATED":
      return "Você está incapacitado.";
    default:
      return "Não foi possível fundir.";
  }
}

function describeSellFailure(code: string): string {
  switch (code) {
    case "ITEM_NOT_SELLABLE":
      return "O comerciante não compra este item.";
    case "INSUFFICIENT_ITEMS":
      return "Você não tem esse item em quantidade suficiente.";
    case "ON_COOLDOWN":
      return "Aguarde antes de vender de novo.";
    case "INCAPACITATED":
      return "Você está incapacitado.";
    default:
      return "Não foi possível vender.";
  }
}

function describeBuyFailure(code: string): string {
  switch (code) {
    case "ITEM_NOT_BUYABLE":
      return "O comerciante não vende este item.";
    case "INSUFFICIENT_COINS":
      return "Você não tem moedas suficientes.";
    case "ON_COOLDOWN":
      return "Aguarde antes de comprar de novo.";
    case "INCAPACITATED":
      return "Você está incapacitado.";
    case "INVENTORY_FULL":
      return "Inventário cheio (20 espaços). Venda ou use algo antes de comprar mais.";
    default:
      return "Não foi possível comprar.";
  }
}

function describeUseItemFailure(code: string): string {
  switch (code) {
    case "ITEM_NOT_USABLE":
      return "Este item não pode ser usado.";
    case "INSUFFICIENT_ITEMS":
      return "Você não tem esse item.";
    case "ON_COOLDOWN":
      return "Aguarde antes de usar outro item.";
    case "INCAPACITATED":
      return "Você está incapacitado.";
    default:
      return "Não foi possível usar o item.";
  }
}

function describeAttackFailure(code: string): string {
  switch (code) {
    case "TARGET_NOT_FOUND":
      return "Alvo inválido ou já derrotado.";
    case "OUT_OF_RANGE":
      return "Alvo fora de alcance.";
    case "ON_COOLDOWN":
      return "Aguarde o intervalo entre ataques.";
    case "INCAPACITATED":
      return "Você está incapacitado.";
    case "WEAPON_NOT_EQUIPPED":
      return "Equipe uma espada antes de atacar.";
    default:
      return "Não foi possível atacar.";
  }
}

function replyError(character: ConnectedCharacter, code: ErrorCode, message: string, retryable: boolean): void {
  character.outboundSequence += 1;
  sendError(character.socket, code, message, retryable, character.outboundSequence);
}

function replySnapshot(character: ConnectedCharacter): void {
  character.outboundSequence += 1;
  sendEnvelope(
    character.socket,
    "world.snapshot",
    buildSnapshotForCharacter(character, defaultInstance.list()),
    character.outboundSequence,
  );
}

function replyEconomySnapshot(character: ConnectedCharacter): void {
  character.outboundSequence += 1;
  sendEnvelope(
    character.socket,
    "economy.snapshot",
    {
      coinBalance: character.coinBalance,
      inventory: [...character.inventory.entries()]
        .filter(([, quantity]) => quantity > 0)
        .map(([itemCode, quantity]) => ({ itemCode, quantity })),
      equipment: { mainHand: character.equipment.mainHand, tool: character.equipment.tool },
    },
    character.outboundSequence,
  );
}

function replyAttributesSnapshot(character: ConnectedCharacter): void {
  character.outboundSequence += 1;
  sendEnvelope(
    character.socket,
    "attributes.snapshot",
    { attributes: character.attributes, unspentPoints: character.unspentAttributePoints, maxHp: character.maxHp },
    character.outboundSequence,
  );
}

function extractToken(request: FastifyRequest): string | null {
  const token = (request.query as Record<string, unknown> | undefined)?.token;
  return typeof token === "string" && token.length > 0 ? token : null;
}
