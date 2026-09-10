import type { Equipment, ItemCode, NpcCode, TutorialStepCode } from "@espectro/contracts";
import type { WebSocket } from "ws";
import type { CharacterAttributes } from "../combat/formulas.js";
import { createInitialEnemies, type EnemyState } from "../combat/enemy-instance.js";
import { persistPosition } from "../characters/characters.service.js";
import type { ActiveChannel } from "../economy/channel-state.js";
import { loadResourceNodes, type ResourceNodeState } from "../economy/resource-node-instance.js";
import { FixedWindowRateLimiter } from "./rate-limiter.js";
import type { Vector3 } from "./movement.js";

export interface ConnectedCharacter {
  characterId: string;
  accountId: string;
  name: string;
  position: Vector3;
  facingY: number;
  socket: WebSocket;
  outboundSequence: number;
  lastInputAt: number;
  movementRateLimiter: FixedWindowRateLimiter;
  dirtyPosition: boolean;

  hp: number;
  maxHp: number;
  attributes: CharacterAttributes;
  unspentAttributePoints: number;
  level: number;
  xp: number;
  swordSkillLevel: number;
  swordSkillXp: number;
  lastAttackAt: number;
  /** Timestamp (ms) até quando o personagem fica incapacitado após morrer; null quando ativo. */
  incapacitatedUntil: number | null;

  // Corte 3 (GDD §9-§12): cache em memória pra responder economy.snapshot sem round-trip ao
  // banco a cada ação; sempre atualizado junto da transação que persiste a mudança (mesmo padrão
  // de hp/xp/level acima).
  coinBalance: number;
  inventory: Map<ItemCode, number>;
  equipment: Equipment;
  miningSkillLevel: number;
  miningSkillXp: number;
  metallurgySkillLevel: number;
  metallurgySkillXp: number;
  /** GDD §10/§11: extração e fundição levam tempo; só um canal ativo por vez (ver channel-state.ts). */
  activeChannel: ActiveChannel | null;

  // GDD §13/§4: NPCs e progresso do tutorial.
  talkedNpcs: Set<NpcCode>;
  tutorialStepsCompleted: Set<TutorialStepCode>;
  tutorialRewardClaimed: boolean;

  chatRateLimiter: FixedWindowRateLimiter;
}

const INSTANCE_ID = "default";

class WorldInstance {
  readonly id = INSTANCE_ID;
  private readonly characters = new Map<string, ConnectedCharacter>();
  private readonly enemies = createInitialEnemies();
  private resourceNodes = new Map<string, ResourceNodeState>();

  /** Chamado uma vez na inicialização do servidor (src/index.ts), antes de aceitar conexões. */
  async loadResourceNodes(): Promise<void> {
    this.resourceNodes = await loadResourceNodes();
  }

  getResourceNode(nodeId: string): ResourceNodeState | undefined {
    return this.resourceNodes.get(nodeId);
  }

  listResourceNodes(): ResourceNodeState[] {
    return [...this.resourceNodes.values()];
  }

  join(character: ConnectedCharacter): void {
    this.characters.set(character.characterId, character);
  }

  leave(characterId: string): ConnectedCharacter | undefined {
    const character = this.characters.get(characterId);
    this.characters.delete(characterId);
    return character;
  }

  get(characterId: string): ConnectedCharacter | undefined {
    return this.characters.get(characterId);
  }

  list(): ConnectedCharacter[] {
    return [...this.characters.values()];
  }

  getEnemy(enemyId: string): EnemyState | undefined {
    return this.enemies.get(enemyId);
  }

  listEnemies(): EnemyState[] {
    return [...this.enemies.values()];
  }
}

export const defaultInstance = new WorldInstance();

/** Persiste apenas os personagens que se moveram desde a última chamada. */
export async function persistDirtyPositions(): Promise<void> {
  for (const character of defaultInstance.list()) {
    if (!character.dirtyPosition) continue;
    character.dirtyPosition = false;
    await persistPosition(character.characterId, { ...character.position, facingY: character.facingY });
  }
}
