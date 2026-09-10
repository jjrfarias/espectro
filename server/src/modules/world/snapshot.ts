import type { WorldSnapshotPayload } from "@espectro/contracts";
import { env } from "../../config/env.js";
import { enemyDefinitions } from "../combat/enemy-definitions.js";
import { defaultInstance, type ConnectedCharacter } from "./instance.js";

export function buildSnapshotForCharacter(self: ConnectedCharacter, all: ConnectedCharacter[]): WorldSnapshotPayload {
  return {
    instanceId: "default",
    serverTime: new Date().toISOString(),
    movementSpeed: env.MOVEMENT_MAX_SPEED_UNITS_PER_SEC,
    self: toSnapshotCharacter(self),
    others: all.filter((character) => character.characterId !== self.characterId).map(toSnapshotCharacter),
    enemies: defaultInstance.listEnemies().map((enemy) => ({
      enemyId: enemy.id,
      definitionCode: enemy.definitionCode,
      position: enemy.position,
      hp: enemy.hp,
      maxHp: enemyDefinitions[enemy.definitionCode].maxHp,
      alive: enemy.alive,
    })),
    resourceNodes: defaultInstance.listResourceNodes().map((node) => ({
      nodeId: node.id,
      resourceCode: node.resourceCode,
      position: node.position,
      available: node.available,
    })),
  };
}

function toSnapshotCharacter(character: ConnectedCharacter) {
  return {
    characterId: character.characterId,
    name: character.name,
    position: character.position,
    facingY: character.facingY,
    hp: character.hp,
    maxHp: character.maxHp,
  };
}
