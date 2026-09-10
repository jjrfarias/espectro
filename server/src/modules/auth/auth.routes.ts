import type { FastifyInstance, FastifyReply } from "fastify";
import { z } from "zod";
import { firstValidationMessage } from "../../transport/validation.js";
import { AuthError, login, logout, refreshSession, registerAccount, requestPasswordReset, resetPassword } from "./auth.service.js";

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

const passwordResetRequestSchema = z.object({
  email: z.string().email("Informe um e-mail válido."),
});

const passwordResetConfirmSchema = z.object({
  token: z.string().min(1, "Token de redefinição ausente."),
  password: z
    .string()
    .min(8, "A senha precisa ter ao menos 8 caracteres.")
    .max(128, "A senha pode ter no máximo 128 caracteres."),
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

  // docs/REFERENCIAS-INTERFACE-JOGABILIDADE.md, plano de evolução §2 "Esqueci a senha": resposta
  // sempre neutra (nunca revela se o e-mail existe). Sem provedor de e-mail configurado ainda —
  // `devToken` só aparece fora de produção, exclusivamente pra dar pra testar o fluxo de ponta a
  // ponta sem depender de um envio real; em produção a resposta nunca inclui o token, então o
  // link efetivamente não chega em lugar nenhum até existir um provedor real de e-mail.
  app.post("/auth/password-reset/request", async (request, reply) => {
    const body = passwordResetRequestSchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    const { token } = await requestPasswordReset(body.data.email);
    const devToken = process.env.NODE_ENV === "production" ? undefined : (token ?? undefined);
    return reply.send({ status: "ok", ...(devToken ? { devToken } : {}) });
  });

  app.post("/auth/password-reset/confirm", async (request, reply) => {
    const body = passwordResetConfirmSchema.safeParse(request.body);
    if (!body.success) {
      return reply
        .code(400)
        .send({ error: "VALIDATION_ERROR", message: firstValidationMessage(body.error), details: body.error.flatten() });
    }
    try {
      await resetPassword(body.data.token, body.data.password);
      return reply.code(204).send();
    } catch (error) {
      return handleAuthError(error, reply);
    }
  });
}

function handleAuthError(error: unknown, reply: FastifyReply) {
  if (error instanceof AuthError) {
    const status = error.code === "EMAIL_TAKEN" ? 409 : 401;
    return reply.code(status).send({ error: error.code, message: error.message });
  }
  throw error;
}
