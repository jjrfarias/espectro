import type { FastifyReply, FastifyRequest } from "fastify";
import { verifyAccessToken } from "./tokens.js";

/** Extrai e valida o token de acesso do cabeçalho Authorization. Responde 401 e retorna null se inválido. */
export function requireAccountId(request: FastifyRequest, reply: FastifyReply): string | null {
  const header = request.headers.authorization;
  const token = header?.startsWith("Bearer ") ? header.slice("Bearer ".length) : null;
  if (!token) {
    reply.code(401).send({ error: "UNAUTHORIZED", message: "Token de acesso ausente." });
    return null;
  }
  try {
    return verifyAccessToken(token).accountId;
  } catch {
    reply.code(401).send({ error: "UNAUTHORIZED", message: "Token de acesso inválido ou expirado." });
    return null;
  }
}
