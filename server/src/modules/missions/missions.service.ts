import { tutorialStepCodes, type NpcCode, type NpcTalkResultPayload, type TutorialSnapshotPayload, type TutorialStepCode } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import { sendEnvelope } from "../../transport/envelope.js";
import type { ConnectedCharacter } from "../world/instance.js";
import { economySnapshotOf } from "../economy/economy.repository.js";
import { NPC_TUTORIAL_STEP, TUTORIAL_REWARD_COINS } from "./missions.constants.js";

/**
 * GDD §13: registra que o personagem conversou com um NPC (idempotente — falar de novo não faz
 * nada além de reconfirmar) e, se essa conversa corresponde a um passo do tutorial (§4), completa
 * o passo. O conteúdo do diálogo em si é responsabilidade do cliente.
 */
export async function talkToNpc(character: ConnectedCharacter, npcCode: NpcCode): Promise<NpcTalkResultPayload> {
  if (!character.talkedNpcs.has(npcCode)) {
    await pool.query(
      `insert into character_npc_talks (character_id, npc_code) values ($1, $2) on conflict (character_id, npc_code) do nothing`,
      [character.characterId, npcCode],
    );
    character.talkedNpcs.add(npcCode);
  }

  const step = NPC_TUTORIAL_STEP[npcCode];
  if (step) {
    await completeTutorialStep(character, step);
  }

  return { npcCode, tutorial: tutorialSnapshotOf(character) };
}

/** GDD §13: "Ferreiro: libera a forja" — usado por economy.service.ts pra travar a fundição. */
export function hasTalkedTo(character: ConnectedCharacter, npcCode: NpcCode): boolean {
  return character.talkedNpcs.has(npcCode);
}

/**
 * Chamado pelos módulos de combate/economia quando uma ação corresponde a um passo do tutorial
 * (GDD §4): derrotar uma criatura, extrair minério, fundir um lingote, vender um lingote.
 * Idempotente (`on conflict do nothing`) — uma ação repetida não conta o passo de novo, mesmo
 * quando o personagem já tinha esse passo marcado antes de reconectar.
 */
export async function completeTutorialStep(character: ConnectedCharacter, stepCode: TutorialStepCode): Promise<void> {
  if (character.tutorialStepsCompleted.has(stepCode)) return;

  let newlyCompleted = false;
  let rewardClaimed = false;
  const client = await pool.connect();
  try {
    await client.query("begin");
    const inserted = await client.query(
      `insert into character_tutorial_steps (character_id, step_code) values ($1, $2) on conflict (character_id, step_code) do nothing`,
      [character.characterId, stepCode],
    );
    newlyCompleted = (inserted.rowCount ?? 0) === 1;

    if (newlyCompleted) {
      // Consulta o total de passos concluídos e paga a recompensa numa única instrução condicional
      // (mesmo raciocínio de applyLedgerEntry/allocateAttributePoint) — atômico mesmo sob duas
      // ações completando o último passo do tutorial ao mesmo tempo.
      const claim = await client.query(
        `update characters set coin_balance = coin_balance + $2, tutorial_reward_claimed_at = now(), version = version + 1, updated_at = now()
         where id = $1
           and tutorial_reward_claimed_at is null
           and (select count(*) from character_tutorial_steps where character_id = $1) >= $3
         returning coin_balance`,
        [character.characterId, TUTORIAL_REWARD_COINS, tutorialStepCodes.length],
      );
      rewardClaimed = (claim.rowCount ?? 0) === 1;
      if (rewardClaimed) {
        await client.query(
          `insert into ledger_entries (character_id, delta, reason, reference) values ($1, $2, $3, $4)`,
          [character.characterId, TUTORIAL_REWARD_COINS, "recompensa_tutorial", "tutorial"],
        );
      }
    }
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }

  if (!newlyCompleted) return;

  character.tutorialStepsCompleted.add(stepCode);
  if (rewardClaimed) {
    character.coinBalance += TUTORIAL_REWARD_COINS;
    character.tutorialRewardClaimed = true;
  }

  if (character.socket.readyState === character.socket.OPEN) {
    character.outboundSequence += 1;
    sendEnvelope(character.socket, "tutorial.snapshot", tutorialSnapshotOf(character), character.outboundSequence);
    // A recompensa (GDD §12) muda coinBalance, mas isso não é visível em nenhuma outra mensagem —
    // sem isto, o saldo só apareceria certo na próxima ação de economia (comprar/vender/minerar).
    if (rewardClaimed) {
      character.outboundSequence += 1;
      sendEnvelope(character.socket, "economy.snapshot", economySnapshotOf(character), character.outboundSequence);
    }
  }
}

export function tutorialSnapshotOf(character: ConnectedCharacter): TutorialSnapshotPayload {
  return {
    completedSteps: [...character.tutorialStepsCompleted],
    completed: character.tutorialStepsCompleted.size >= tutorialStepCodes.length,
    rewardClaimed: character.tutorialRewardClaimed,
  };
}
