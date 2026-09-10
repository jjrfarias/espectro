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
  // navegador aplica CORS (diferente do cliente nativo, que não passa por isso). Origem
  // restrita à lista abaixo agora que há teste público real (server/README.md, link WebGL) —
  // sem cookies/sessão de navegador envolvidos (auth é Bearer token), então isto é reforço,
  // não a única defesa, mas evita que qualquer site arbitrário chame a API em nome do usuário.
  const allowedOrigins = [
    "https://webgl-production-cae7.up.railway.app",
    /^http:\/\/(127\.0\.0\.1|localhost):\d+$/,
  ];
  await app.register(corsPlugin, { origin: allowedOrigins });
  await app.register(websocketPlugin);
  await app.register(authRoutes);
  await app.register(characterRoutes);
  await app.register(worldEventRoutes);
  await app.register(reportRoutes);
  await app.register(worldGateway);
  app.get("/health", async () => ({ status: "ok" }));
  return app;
}
