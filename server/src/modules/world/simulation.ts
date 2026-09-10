import { env } from "../../config/env.js";
import { sendEnvelope } from "../../transport/envelope.js";
import { tickEnemies, tickPlayerRespawns } from "../combat/combat.service.js";
import { tickChannels, tickResourceNodes } from "../economy/economy.service.js";
import { defaultInstance, persistDirtyPositions } from "./instance.js";
import { buildSnapshotForCharacter } from "./snapshot.js";

const PERSIST_INTERVAL_MS = 10_000;

let tickTimer: NodeJS.Timeout | undefined;
let persistTimer: NodeJS.Timeout | undefined;
let lastTickAt = Date.now();

export function startWorldSimulation(): void {
  const tickIntervalMs = 1000 / env.WORLD_TICK_HZ;
  lastTickAt = Date.now();
  tickTimer = setInterval(tick, tickIntervalMs);
  persistTimer = setInterval(() => {
    persistDirtyPositions().catch((error: unknown) => {
      console.error("Falha ao persistir posições do mundo:", error);
    });
  }, PERSIST_INTERVAL_MS);
}

export function stopWorldSimulation(): void {
  clearInterval(tickTimer);
  clearInterval(persistTimer);
}

function tick(): void {
  const now = Date.now();
  const deltaSeconds = (now - lastTickAt) / 1000;
  lastTickAt = now;

  tickEnemies(deltaSeconds);
  tickPlayerRespawns();
  tickResourceNodes();
  tickChannels();
  broadcastSnapshots();
}

function broadcastSnapshots(): void {
  const characters = defaultInstance.list();
  for (const character of characters) {
    if (character.socket.readyState !== character.socket.OPEN) continue;
    const snapshot = buildSnapshotForCharacter(character, characters);
    character.outboundSequence += 1;
    sendEnvelope(character.socket, "world.snapshot", snapshot, character.outboundSequence);
  }
}
