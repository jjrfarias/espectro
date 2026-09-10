import type { MigrationBuilder } from "node-pg-migrate";

// docs/REFERENCIAS-INTERFACE-JOGABILIDADE.md, plano de evolução §2 "Esqueci a senha": token
// temporário de uso único, com expiração — nunca a senha em texto claro, nunca o token em claro
// (guardamos o hash, mesmo padrão de `sessions.refresh_token_hash`).
export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createTable("password_reset_tokens", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    account_id: {
      type: "uuid",
      notNull: true,
      references: "accounts",
      onDelete: "CASCADE",
    },
    token_hash: { type: "text", notNull: true, unique: true },
    expires_at: { type: "timestamptz", notNull: true },
    used_at: { type: "timestamptz" },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
  pgm.createIndex("password_reset_tokens", "account_id");
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("password_reset_tokens");
}
