import type { FastifyInstance } from "fastify";
import { z } from "zod";
import { requireAccountId } from "../auth/authenticate.js";
import { firstValidationMessage } from "../../transport/validation.js";
import { pool } from "../../persistence/db.js";

// docs/ARQUITETURA-MVP.md §10, tabela `reports`. A tabela existe desde a migração do chat
// (005_chat.ts); só o endpoint pra criar uma denúncia estava faltando.
const createReportSchema = z.object({
  targetCharacterId: z.string().uuid(),
  chatMessageId: z.string().uuid().optional(),
  reason: z.string().trim().min(1).max(500),
});

export async function reportRoutes(app: FastifyInstance): Promise<void> {
  app.post("/reports", async (request, reply) => {
    const accountId = requireAccountId(request, reply);
    if (!accountId) return;

    const body = createReportSchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }

    const target = await pool.query<{ account_id: string }>(
      `select account_id from characters where id = $1`,
      [body.data.targetCharacterId],
    );
    const targetAccountId = target.rows[0]?.account_id;
    if (!targetAccountId) {
      return reply.code(404).send({ error: "TARGET_NOT_FOUND", message: "Personagem denunciado não encontrado." });
    }
    if (targetAccountId === accountId) {
      return reply.code(400).send({ error: "CANNOT_REPORT_SELF", message: "Você não pode denunciar a si mesmo." });
    }

    await pool.query(
      `insert into reports (reporter_account_id, target_account_id, chat_message_id, reason)
       values ($1, $2, $3, $4)`,
      [accountId, targetAccountId, body.data.chatMessageId ?? null, body.data.reason],
    );

    // GDD §9/§13: moderação de verdade (revisão humana de denúncias, punição etc.) é escopo do
    // Corte 4. Este endpoint só registra a denúncia (status "open") — nada acontece com ela ainda.
    return reply.code(201).send({ status: "open" });
  });
}
