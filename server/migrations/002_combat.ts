import type { MigrationBuilder } from "node-pg-migrate";

export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createTable("character_attributes", {
    character_id: {
      type: "uuid",
      primaryKey: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    strength: { type: "integer", notNull: true, default: 5 },
    agility: { type: "integer", notNull: true, default: 5 },
    vitality: { type: "integer", notNull: true, default: 5 },
    resistance: { type: "integer", notNull: true, default: 5 },
    unspent_points: { type: "integer", notNull: true, default: 0 },
  });

  pgm.createTable("character_skills", {
    id: { type: "uuid", primaryKey: true, default: pgm.func("gen_random_uuid()") },
    character_id: {
      type: "uuid",
      notNull: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    skill_code: { type: "text", notNull: true },
    level: { type: "integer", notNull: true, default: 1 },
    xp: { type: "integer", notNull: true, default: 0 },
    version: { type: "integer", notNull: true, default: 1 },
  });
  pgm.addConstraint("character_skills", "character_skills_character_id_skill_code_key", {
    unique: ["character_id", "skill_code"],
  });

  // Backfill: personagens criados antes deste corte não têm essas linhas. getCharacterCombatStateByAccountId
  // usa inner join com character_attributes, então sem isso eles ficariam sem conseguir entrar no mundo.
  pgm.sql(`insert into character_attributes (character_id) select id from characters`);
  pgm.sql(`insert into character_skills (character_id, skill_code) select id, 'sword' from characters`);
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropTable("character_skills");
  pgm.dropTable("character_attributes");
}
