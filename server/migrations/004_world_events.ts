import type { MigrationBuilder } from "node-pg-migrate";

// Histórico persistente do mundo (docs/ARQUITETURA-MVP.md §10, tabela `world_events`) — base de
// dados do "mural de crônicas" (docs/GDD-MVP.md §13, Corte 4). Modelado agora, na cauda do
// Corte 3, porque os primeiros feitos que ele registra (ex.: primeiro lingote fundido) acontecem
// justamente na economia que acabou de ser implementada — sem a tabela, esses eventos já
// aconteceriam sem deixar rastro, e teriam que ser reconstruídos depois. A UI do mural em si
// (moderação, frases geradas, chat) continua fora de escopo até o Corte 4.
export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createTable("world_events", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    event_type: { type: "text", notNull: true },
    // GDD §12 "toda criação e destruição de moeda gera registro auditável" e o princípio de
    // "primeiros feitos" (§13) precisam de idempotência: unique_key garante que o mesmo marco
    // (ex.: "primeiro lingote de ferro") só é registrado uma vez, mesmo sob corrida.
    unique_key: { type: "text", unique: true },
    character_id: {
      type: "uuid",
      references: "characters",
      onDelete: "SET NULL",
    },
    occurred_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
    location_code: { type: "text" },
    data_json: { type: "jsonb", notNull: true, default: "{}" },
    schema_version: { type: "integer", notNull: true, default: 1 },
  });
  pgm.createIndex("world_events", "occurred_at");
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("world_events");
}
