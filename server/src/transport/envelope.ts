import { randomUUID } from "node:crypto";
import type { WebSocket } from "ws";
import {
  PROTOCOL_VERSION,
  clientMessageSchemas,
  envelopeSchema,
  isClientMessageType,
  type ClientMessageType,
  type ErrorCode,
  type ServerMessageType,
} from "@espectro/contracts";

export class UnsupportedTypeError extends Error {
  constructor(public readonly messageType: string) {
    super(`Tipo de mensagem não suportado: ${messageType}`);
  }
}

export interface ParsedClientMessage<T extends ClientMessageType = ClientMessageType> {
  type: T;
  requestId: string;
  sequence: number;
  payload: Record<string, unknown>;
}

/** Valida o envelope e o payload de uma mensagem recebida do cliente. Lança em caso de erro. */
export function parseClientMessage(raw: unknown): ParsedClientMessage {
  const envelope = envelopeSchema.parse(raw);
  if (!isClientMessageType(envelope.type)) {
    throw new UnsupportedTypeError(envelope.type);
  }
  const payload = clientMessageSchemas[envelope.type].parse(envelope.payload) as Record<string, unknown>;
  return { type: envelope.type, requestId: envelope.requestId, sequence: envelope.sequence, payload };
}

/** `sequence` é responsabilidade de quem chama: cada conexão deve manter seu próprio contador. */
export function sendEnvelope(socket: WebSocket, type: ServerMessageType, payload: unknown, sequence = 0): void {
  socket.send(
    JSON.stringify({
      v: PROTOCOL_VERSION,
      type,
      requestId: randomUUID(),
      sequence,
      sentAt: new Date().toISOString(),
      payload,
    }),
  );
}

export function sendError(socket: WebSocket, code: ErrorCode, message: string, retryable: boolean, sequence = 0): void {
  sendEnvelope(socket, "error", { code, message, retryable }, sequence);
}
