import type { NpcCode, TutorialStepCode } from "@espectro/contracts";

/**
 * GDD §4 "Jornada da primeira sessão": conversar com a Instrutora entrega a espada (passo 5) e
 * com o Minerador entrega a picareta (passo 7). Como os dois itens já nascem equipados com o
 * personagem (simplificação documentada em economy.repository.ts/006_equipment.ts, sem sistema
 * de entrega de item por NPC ainda), essas conversas não entregam nada de novo — só marcam o
 * passo do tutorial como concluído.
 */
export const NPC_TUTORIAL_STEP: Partial<Record<NpcCode, TutorialStepCode>> = {
  instrutora: "falou_instrutora",
  minerador: "falou_minerador",
};

// GDD §12: "recompensa única de missões do tutorial" é citada como fonte de moeda, mas o GDD não
// dá um valor. Autoral, mesmo raciocínio dos outros números de balanceamento não especificados
// neste projeto: perto do preço de um lingote de cobre (20, o preço mais alto da tabela §12), uma
// recompensa que vale a pena sem trivializar a economia inicial.
export const TUTORIAL_REWARD_COINS = 50;
