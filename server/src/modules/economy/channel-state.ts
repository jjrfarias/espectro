import type { RecipeCode, ResourceCode } from "@espectro/contracts";

/**
 * GDD §10/§11: minerar e fundir levam tempo ("manter o comando de interação durante a extração";
 * "o servidor reserva os ingredientes ao iniciar a receita"). Um personagem só pode ter um canal
 * ativo por vez — a exclusividade em si substitui o cooldown pós-ação que existia quando essas
 * ações resolviam na hora.
 */
export interface MiningChannelState {
  type: "mine";
  nodeId: string;
  resourceCode: ResourceCode;
  startedAt: number;
  durationMs: number;
}

export interface CraftingChannelState {
  type: "craft";
  recipeCode: RecipeCode;
  startedAt: number;
  durationMs: number;
}

export type ActiveChannel = MiningChannelState | CraftingChannelState;
