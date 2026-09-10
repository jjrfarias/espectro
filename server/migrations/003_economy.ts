import type { MigrationBuilder } from "node-pg-migrate";

// Corte 3 (docs/ARQUITETURA-MVP.md §19, docs/GDD-MVP.md §9-§12): inventário, mineração,
// metalurgia e venda ao comerciante com livro-razão auditável.
export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.addColumn("characters", {
    coin_balance: { type: "integer", notNull: true, default: 0 },
  });
  pgm.addConstraint("characters", "characters_coin_balance_nonnegative", {
    check: "coin_balance >= 0",
  });

  pgm.createTable("inventory_items", {
    character_id: {
      type: "uuid",
      notNull: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    item_code: { type: "text", notNull: true },
    quantity: { type: "integer", notNull: true, default: 0 },
  });
  pgm.addConstraint("inventory_items", "inventory_items_pkey", {
    primaryKey: ["character_id", "item_code"],
  });
  pgm.addConstraint("inventory_items", "inventory_items_quantity_nonnegative", {
    check: "quantity >= 0",
  });

  // GDD §12: "toda criação e destruição de moeda gera registro auditável com motivo e
  // referência da transação" — livro-razão append-only, nunca editado nem apagado por código de
  // aplicação.
  pgm.createTable("ledger_entries", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    character_id: {
      type: "uuid",
      notNull: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    delta: { type: "integer", notNull: true },
    reason: { type: "text", notNull: true },
    reference: { type: "text" },
    created_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
  pgm.createIndex("ledger_entries", "character_id");

  // Veios de recurso são globais à instância (GDD §10: "sem PvP"), não pertencem a um
  // personagem. position_json usa o mesmo formato de characters.position_json (sem facingY).
  pgm.createTable("resource_nodes", {
    id: { type: "text", primaryKey: true },
    resource_code: { type: "text", notNull: true },
    position_json: { type: "jsonb", notNull: true },
    available: { type: "boolean", notNull: true, default: true },
    depleted_at: { type: "timestamptz" },
  });

  // Skills genéricas (character_skills já existe desde o Corte 2) ganham dois códigos novos.
  pgm.sql(`insert into character_skills (character_id, skill_code) select id, 'mineracao' from characters`);
  pgm.sql(`insert into character_skills (character_id, skill_code) select id, 'metalurgia' from characters`);

  // Veios iniciais de O Berço — posições próximas da mina (docs/GDD-MVP.md §10, fluxo de mineração).
  pgm.sql(`
    insert into resource_nodes (id, resource_code, position_json) values
      ('ferro_1', 'ferro', '{"x": 18, "y": 0, "z": -3}'),
      ('ferro_2', 'ferro', '{"x": 20, "y": 0, "z": -6}'),
      ('ferro_3', 'ferro', '{"x": 23, "y": 0, "z": -4}'),
      ('cobre_1', 'cobre', '{"x": 21, "y": 0, "z": -8}'),
      ('cobre_2', 'cobre', '{"x": 24, "y": 0, "z": -7}')
  `);
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("resource_nodes");
  pgm.dropTable("ledger_entries");
  pgm.dropTable("inventory_items");
  pgm.dropConstraint("characters", "characters_coin_balance_nonnegative");
  pgm.dropColumn("characters", "coin_balance");
}
