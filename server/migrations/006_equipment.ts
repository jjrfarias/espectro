import type { MigrationBuilder } from "node-pg-migrate";

// Equipamento (docs/GDD-MVP.md §9): "equipamento possui dois espaços ativos: mão principal e
// ferramenta". Uma linha por personagem (não uma tabela de itens) porque só existem esses dois
// espaços fixos no MVP — sem inventário de equipamento nem múltiplos sets.
export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createTable("character_equipment", {
    character_id: {
      type: "uuid",
      primaryKey: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    main_hand_item_code: { type: "text" },
    tool_item_code: { type: "text" },
  });

  // GDD §9 flui a picareta vindo do NPC Minerador; simplificação deliberada (sem sistema de
  // entrega de item por NPC ainda): todo personagem já nasce com espada e picareta simples no
  // inventário, equipadas. Backfill pros personagens existentes.
  pgm.sql(`insert into inventory_items (character_id, item_code, quantity)
    select id, 'espada_simples', 1 from characters
    on conflict (character_id, item_code) do nothing`);
  pgm.sql(`insert into inventory_items (character_id, item_code, quantity)
    select id, 'picareta_simples', 1 from characters
    on conflict (character_id, item_code) do nothing`);
  pgm.sql(`insert into character_equipment (character_id, main_hand_item_code, tool_item_code)
    select id, 'espada_simples', 'picareta_simples' from characters`);
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("character_equipment");
}
