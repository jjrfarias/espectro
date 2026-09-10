import type { MigrationBuilder } from "node-pg-migrate";

// Chat local (docs/GDD-MVP.md §2 "chat local" — item incluído no escopo do MVP, não é feature
// adiada pro Corte 4 como o mural de crônicas) e a tabela de denúncias que o acompanha
// (docs/ARQUITETURA-MVP.md §10). O endpoint pra criar uma denúncia fica pra depois (precisa de
// UI no cliente pra reportar alguém) — a tabela já existe pronta pra isso não virar retrabalho.
export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createTable("chat_messages", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    instance_id: { type: "text", notNull: true },
    character_id: {
      type: "uuid",
      notNull: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    content: { type: "text", notNull: true },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
    moderation_status: { type: "text", notNull: true, default: "visible" },
  });
  pgm.createIndex("chat_messages", ["instance_id", "created_at"]);

  pgm.createTable("reports", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    reporter_account_id: {
      type: "uuid",
      notNull: true,
      references: "accounts",
      onDelete: "CASCADE",
    },
    target_account_id: {
      type: "uuid",
      notNull: true,
      references: "accounts",
      onDelete: "CASCADE",
    },
    chat_message_id: {
      type: "uuid",
      references: "chat_messages",
      onDelete: "SET NULL",
    },
    reason: { type: "text", notNull: true },
    status: { type: "text", notNull: true, default: "open" },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("reports");
  pgm.dropTable("chat_messages");
}
