import { z } from "zod";

const schema = z.object({
  PORT: z.coerce.number().int().positive().default(3000),
  DATABASE_URL: z.string().min(1, "DATABASE_URL é obrigatório"),
  JWT_ACCESS_SECRET: z.string().min(16, "JWT_ACCESS_SECRET deve ter ao menos 16 caracteres"),
  JWT_ACCESS_TTL_SECONDS: z.coerce.number().int().positive().default(900),
  JWT_REFRESH_TTL_DAYS: z.coerce.number().int().positive().default(30),
  WORLD_TICK_HZ: z.coerce.number().int().positive().default(10),
  MOVEMENT_MAX_SPEED_UNITS_PER_SEC: z.coerce.number().positive().default(6),
  MOVEMENT_MAX_INPUT_HZ: z.coerce.number().int().positive().default(15),
  CHAT_MAX_MESSAGES_PER_10S: z.coerce.number().int().positive().default(5),
});

const parsed = schema.safeParse(process.env);
if (!parsed.success) {
  console.error("Configuração inválida:", parsed.error.flatten().fieldErrors);
  throw new Error("Falha ao carregar variáveis de ambiente do servidor.");
}

export const env = parsed.data;
