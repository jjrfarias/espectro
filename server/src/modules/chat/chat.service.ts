import type { ChatMessagePayload } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import { sendEnvelope } from "../../transport/envelope.js";
import { defaultInstance, type ConnectedCharacter } from "../world/instance.js";

export type ChatSendFailureCode = "RATE_LIMITED";
export type ChatSendResult = { ok: true } | { ok: false; code: ChatSendFailureCode };

// GDD §13 fala em moderação de nome (aplicada em events.routes.ts); o GDD não detalha um
// filtro de chat, mas §9 exige reter e poder moderar mensagens. Isto é um placeholder mínimo e
// deliberadamente simples (não é moderação de verdade — sem contexto, sem revisão humana, sem
// idiomas além do óbvio): mensagens com essas palavras são gravadas com
// moderation_status='hidden' e nunca chegam a outros jogadores. Substituir antes de expor a
// jogadores reais.
const BLOCKED_SUBSTRINGS = ["porra", "caralho", "fdp"];

function moderationStatusFor(content: string): "visible" | "hidden" {
  const normalized = content.toLowerCase();
  return BLOCKED_SUBSTRINGS.some((word) => normalized.includes(word)) ? "hidden" : "visible";
}

/** GDD §2/§9: chat local — alcança só quem está na mesma instância do remetente. */
export async function sendChatMessage(character: ConnectedCharacter, content: string): Promise<ChatSendResult> {
  if (!character.chatRateLimiter.tryConsume()) {
    return { ok: false, code: "RATE_LIMITED" };
  }

  const trimmed = content.trim();
  const status = moderationStatusFor(trimmed);
  const sentAt = new Date();

  await pool.query(
    `insert into chat_messages (instance_id, character_id, content, created_at, moderation_status)
     values ($1, $2, $3, $4, $5)`,
    [defaultInstance.id, character.characterId, trimmed, sentAt, status],
  );

  if (status === "hidden") return { ok: true };

  const payload: ChatMessagePayload = {
    characterId: character.characterId,
    characterName: character.name,
    content: trimmed,
    sentAt: sentAt.toISOString(),
  };
  for (const recipient of defaultInstance.list()) {
    recipient.outboundSequence += 1;
    sendEnvelope(recipient.socket, "chat.message", payload, recipient.outboundSequence);
  }

  return { ok: true };
}
