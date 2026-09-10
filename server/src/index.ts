import { env } from "./config/env.js";
import { defaultInstance } from "./modules/world/instance.js";
import { startWorldSimulation, stopWorldSimulation } from "./modules/world/simulation.js";
import { closePool } from "./persistence/db.js";
import { buildApp } from "./transport/http.js";

async function main(): Promise<void> {
  const app = await buildApp();
  await defaultInstance.loadResourceNodes();
  startWorldSimulation();

  const shutdown = async (signal: string): Promise<void> => {
    app.log.info(`Recebido ${signal}, encerrando...`);
    stopWorldSimulation();
    await app.close();
    await closePool();
    process.exit(0);
  };

  process.on("SIGINT", () => void shutdown("SIGINT"));
  process.on("SIGTERM", () => void shutdown("SIGTERM"));

  await app.listen({ port: env.PORT, host: "0.0.0.0" });
}

main().catch((error: unknown) => {
  console.error("Falha ao iniciar o servidor:", error);
  process.exit(1);
});
