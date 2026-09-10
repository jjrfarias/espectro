import type { MigrationBuilder } from "node-pg-migrate";

// GDD §13 ("NPCs e missões") + §4 ("Jornada da primeira sessão"): cinco NPCs com diálogo
// roteirizado; o tutorial é a sequência linear dos passos 4-11 dessa jornada. Este corte cobre só
// o estado de progresso — o conteúdo do diálogo em si (as falas) é responsabilidade do cliente.
export async function up(pgm: MigrationBuilder): Promise<void> {
  pgm.createTable("character_npc_talks", {
    character_id: {
      type: "uuid",
      notNull: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    npc_code: { type: "text", notNull: true },
    talked_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
  pgm.addConstraint("character_npc_talks", "character_npc_talks_character_id_npc_code_key", {
    unique: ["character_id", "npc_code"],
  });

  pgm.createTable("character_tutorial_steps", {
    character_id: {
      type: "uuid",
      notNull: true,
      references: "characters",
      onDelete: "CASCADE",
    },
    step_code: { type: "text", notNull: true },
    completed_at: { type: "timestamptz", notNull: true, default: pgm.func("now()") },
  });
  pgm.addConstraint("character_tutorial_steps", "character_tutorial_steps_character_id_step_code_key", {
    unique: ["character_id", "step_code"],
  });

  // GDD §12: "recompensa única de missões do tutorial" é uma fonte de moeda — precisa de um jeito
  // de garantir que só é paga uma vez por personagem, mesmo sob corrida entre duas ações
  // completando o último passo do tutorial ao mesmo tempo.
  pgm.addColumn("characters", {
    tutorial_reward_claimed_at: { type: "timestamptz" },
  });

  // GDD §13: "Ferreiro: libera a forja". Sem sistema de entrega de item por NPC ainda (mesma
  // simplificação do Corte 3 pra espada/picareta), personagens existentes já devem poder fundir —
  // do contrário, todo personagem criado antes desta migração ficaria trancado da forja pra sempre
  // sem nenhuma forma de "conversar com o ferreiro" retroativamente.
  pgm.sql(`insert into character_npc_talks (character_id, npc_code)
    select id, 'ferreiro' from characters
    on conflict (character_id, npc_code) do nothing`);
}

export async function down(pgm: MigrationBuilder): Promise<void> {
  pgm.dropColumn("characters", "tutorial_reward_claimed_at");
  pgm.dropTable("character_tutorial_steps");
  pgm.dropTable("character_npc_talks");
}
