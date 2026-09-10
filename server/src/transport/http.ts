import corsPlugin from "@fastify/cors";
import websocketPlugin from "@fastify/websocket";
import Fastify, { type FastifyInstance } from "fastify";
import { authRoutes } from "../modules/auth/auth.routes.js";
import { characterRoutes } from "../modules/characters/characters.routes.js";
import { worldEventRoutes } from "../modules/world/events.routes.js";
import { reportRoutes } from "../modules/chat/reports.routes.js";
import { worldGateway } from "./ws.js";

export async function buildApp(): Promise<FastifyInstance> {
  const app = Fastify({ logger: true });
  // Builds WebGL rodam sandboxadas no navegador: chamadas HTTP passam por fetch/XHR e o
  // navegador aplica CORS (diferente do cliente nativo, que não passa por isso). Sem
  // produção pública ainda (docs/ARQUITETURA-MVP.md §16), origem liberada geral por ora —
  // revisar antes de qualquer teste fechado com jogadores reais.
  await app.register(corsPlugin, { origin: true });
  await app.register(websocketPlugin);
  await app.register(authRoutes);
  await app.register(characterRoutes);
  await app.register(worldEventRoutes);
  await app.register(reportRoutes);
  await app.register(worldGateway);
  app.get("/health", async () => ({ status: "ok" }));
  return app;
}
