import type { FastifyInstance } from "fastify";
import { listRecentEvents } from "./events.repository.js";

const MAX_CHRONICLE_ENTRIES = 50;

const itemDisplayNames: Record<string, string> = {
  lingote_ferro: "lingote de ferro",
  lingote_cobre: "lingote de cobre",
};

/**
 * GDD §13: "o mural de crônicas exibe frases criadas por modelos de texto fixos" — cada
 * event_type conhecido vira uma frase fixa preenchida com os dados do evento. Um event_type sem
 * template aqui é ignorado no mural (mas continua gravado em world_events).
 */
export function toChronicleLine(event: {
  event_type: string;
  character_name: string | null;
  occurred_at: string;
  data_json: Record<string, unknown>;
}): string | null {
  // GDD §13: "nomes inadequados passam por moderação e podem ser substituídos por 'um
  // aventureiro'". Moderação de conteúdo de verdade é escopo do Corte 4 (fora daqui); o único
  // caso coberto agora é o personagem já não existir mais (conta excluída).
  const actor = event.character_name ?? "um aventureiro";
  const date = new Date(event.occurred_at).toLocaleDateString("pt-BR");

  switch (event.event_type) {
    case "primeiro_lingote": {
      const itemCode = typeof event.data_json.itemCode === "string" ? event.data_json.itemCode : undefined;
      const itemName = (itemCode && itemDisplayNames[itemCode]) ?? "um lingote";
      return `${actor} fundiu o primeiro ${itemName} em ${date}.`;
    }
    default:
      return null;
  }
}

export async function worldEventRoutes(app: FastifyInstance): Promise<void> {
  // Mural público (GDD §2 "uma cidade... mural de crônicas"): não exige conta, qualquer um pode ler.
  app.get("/world/chronicles", async (_request, reply) => {
    const events = await listRecentEvents(MAX_CHRONICLE_ENTRIES);
    const lines = events.map(toChronicleLine).filter((line): line is string => line !== null);
    return reply.send({ lines });
  });
}
