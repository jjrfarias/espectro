import type { FastifyInstance } from "fastify";
import { genderCodes, raceCodes } from "@espectro/contracts";
import { z } from "zod";
import { requireAccountId } from "../auth/authenticate.js";
import { firstValidationMessage } from "../../transport/validation.js";
import { CharacterError, type CharacterRow, createCharacter, getCharacterByAccountId } from "./characters.service.js";

// ESPECTRO-VISAO.md §9 "Raça não é classe": padrão "humano"/"masculino" cobre clientes antigos
// que ainda não enviam esses campos, sem quebrar a criação de personagem.
const createCharacterSchema = z.object({
  name: z
    .string()
    .trim()
    .min(3, "O nome precisa ter ao menos 3 caracteres.")
    .max(20, "O nome pode ter no máximo 20 caracteres.")
    .regex(/^[\p{L}0-9 _-]+$/u, "Use apenas letras, números, espaço, hífen ou sublinhado."),
  race: z.enum(raceCodes).default("humano"),
  gender: z.enum(genderCodes).default("masculino"),
});

export async function characterRoutes(app: FastifyInstance): Promise<void> {
  app.post("/characters", async (request, reply) => {
    const accountId = requireAccountId(request, reply);
    if (!accountId) return;
    const body = createCharacterSchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    try {
      const character = await createCharacter(accountId, body.data.name, {
        race: body.data.race,
        gender: body.data.gender,
      });
      return reply.code(201).send(toResponse(character));
    } catch (error) {
      if (error instanceof CharacterError) {
        return reply.code(409).send({ error: error.code, message: error.message });
      }
      throw error;
    }
  });

  app.get("/characters/me", async (request, reply) => {
    const accountId = requireAccountId(request, reply);
    if (!accountId) return;
    const character = await getCharacterByAccountId(accountId);
    if (!character) {
      return reply
        .code(404)
        .send({ error: "CHARACTER_NOT_FOUND", message: "Nenhum personagem encontrado para esta conta." });
    }
    return reply.send(toResponse(character));
  });
}

function toResponse(character: CharacterRow) {
  return {
    id: character.id,
    name: character.name,
    level: character.level,
    xp: character.xp,
    hp: character.hp,
    position: character.position_json,
    version: character.version,
    // Personagens criados antes desta mudança têm appearance_json = {} (sem essas chaves) — os
    // valores padrão evitam mandar `undefined` pro cliente nesse caso.
    race: character.appearance_json.race ?? "humano",
    gender: character.appearance_json.gender ?? "masculino",
  };
}
