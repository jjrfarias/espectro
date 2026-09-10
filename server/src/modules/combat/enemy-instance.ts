import type { EnemyDefinitionCode } from "@espectro/contracts";
import type { Vector3 } from "../world/movement.js";
import { enemyDefinitions } from "./enemy-definitions.js";

export interface EnemyState {
  id: string;
  definitionCode: EnemyDefinitionCode;
  position: Vector3;
  spawnPosition: Vector3;
  hp: number;
  alive: boolean;
  lastAttackAt: number;
  deadAt: number | null;
}

/**
 * Pontos de spawn fixos para o Corte 2. Posições escolhidas para caírem perto da floresta e da
 * área da mina descritas em client/Assets/Editor/Corte0ProjectSetup.cs, mas o servidor não lê a
 * cena do Unity — quem ligar o cliente a este corte deve conferir se batem visualmente com o
 * terreno e ajustar aqui se não baterem.
 */
const SPAWN_POINTS: Array<{ definitionCode: EnemyDefinitionCode; position: Vector3 }> = [
  { definitionCode: "lobo", position: { x: -14, y: 0.3, z: 5 } },
  { definitionCode: "lobo", position: { x: -16, y: 0.3, z: -4 } },
  { definitionCode: "lobo", position: { x: -10, y: 0.3, z: -10 } },
  { definitionCode: "javali", position: { x: 16, y: 0.3, z: -12 } },
  { definitionCode: "javali", position: { x: 20, y: 0.3, z: 8 } },
];

export function createInitialEnemies(): Map<string, EnemyState> {
  const enemies = new Map<string, EnemyState>();
  SPAWN_POINTS.forEach((spawn, index) => {
    const id = `${spawn.definitionCode}-${index}`;
    const definition = enemyDefinitions[spawn.definitionCode];
    enemies.set(id, {
      id,
      definitionCode: spawn.definitionCode,
      position: { ...spawn.position },
      spawnPosition: { ...spawn.position },
      hp: definition.maxHp,
      alive: true,
      lastAttackAt: 0,
      deadAt: null,
    });
  });
  return enemies;
}
