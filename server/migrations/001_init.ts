import type { MigrationBuilder } from "node-pg-migrate";

export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createExtension("pgcrypto", { ifNotExists: true });

  pgm.createTable("accounts", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    email_normalized: { type: "text", notNull: true, unique: true },
    password_hash: { type: "text", notNull: true },
    status: { type: "text", notNull: true, default: "active" },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });

  pgm.createTable("sessions", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    account_id: {
      type: "uuid",
      notNull: true,
      references: "accounts",
      onDelete: "CASCADE",
    },
    refresh_token_hash: { type: "text", notNull: true },
    expires_at: { type: "timestamptz", notNull: true },
    revoked_at: { type: "timestamptz" },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
  pgm.createIndex("sessions", "account_id");

  pgm.createTable("characters", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    account_id: {
      type: "uuid",
      notNull: true,
      unique: true,
      references: "accounts",
      onDelete: "CASCADE",
    },
    name: { type: "text", notNull: true },
    appearance_json: { type: "jsonb", notNull: true, default: "{}" },
    level: { type: "integer", notNull: true, default: 1 },
    xp: { type: "integer", notNull: true, default: 0 },
    hp: { type: "integer", notNull: true, default: 100 },
    position_json: {
      type: "jsonb",
      notNull: true,
      default: pgm.func(`'{"x":0,"y":0,"z":0,"facingY":0}'::jsonb`),
    },
    version: { type: "integer", notNull: true, default: 1 },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
    updated_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
  pgm.sql(`create unique index characters_name_normalized_idx on characters (lower(name))`);
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("characters");
  pgm.dropTable("sessions");
  pgm.dropTable("accounts");
}
