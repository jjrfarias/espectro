import type { FastifyInstance, FastifyReply } from "fastify";
import { z } from "zod";
import { firstValidationMessage } from "../../transport/validation.js";
import { AuthError, login, logout, refreshSession, registerAccount } from "./auth.service.js";

const credentialsSchema = z.object({
  email: z.string().email("Informe um e-mail válido."),
  password: z
    .string()
    .min(8, "A senha precisa ter ao menos 8 caracteres.")
    .max(128, "A senha pode ter no máximo 128 caracteres."),
});

const refreshBodySchema = z.object({
  refreshToken: z.string().min(1, "Token de renovação ausente."),
});

export async function authRoutes(app: FastifyInstance): Promise<void> {
  app.post("/auth/register", async (request, reply) => {
    const body = credentialsSchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    try {
      const session = await registerAccount(body.data.email, body.data.password);
      return reply.code(201).send(session);
    } catch (error) {
      return handleAuthError(error, reply);
    }
  });

  app.post("/auth/login", async (request, reply) => {
    const body = credentialsSchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    try {
      const session = await login(body.data.email, body.data.password);
      return reply.send(session);
    } catch (error) {
      return handleAuthError(error, reply);
    }
  });

  app.post("/auth/refresh", async (request, reply) => {
    const body = refreshBodySchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    try {
      const session = await refreshSession(body.data.refreshToken);
      return reply.send(session);
    } catch (error) {
      return handleAuthError(error, reply);
    }
  });

  app.post("/auth/logout", async (request, reply) => {
    const body = refreshBodySchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    await logout(body.data.refreshToken);
    return reply.code(204).send();
  });
}

function handleAuthError(error: unknown, reply: FastifyReply) {
  if (error instanceof AuthError) {
    const status = error.code === "EMAIL_TAKEN" ? 409 : 401;
    return reply.code(status).send({ error: error.code, message: error.message });
  }
  throw error;
}
