export interface Vector3 {
  x: number;
  y: number;
  z: number;
}

export interface MovementInput {
  moveX: number;
  moveZ: number;
}

/** Aplica uma intenção de movimento a uma posição, limitada pela velocidade máxima do servidor. */
export function applyMovement(
  position: Vector3,
  input: MovementInput,
  dtSeconds: number,
  maxSpeedUnitsPerSecond: number,
): Vector3 {
  const magnitude = Math.hypot(input.moveX, input.moveZ);
  if (magnitude === 0 || dtSeconds <= 0) return position;
  const clampedMagnitude = Math.min(magnitude, 1);
  const dirX = input.moveX / magnitude;
  const dirZ = input.moveZ / magnitude;
  const distance = maxSpeedUnitsPerSecond * clampedMagnitude * dtSeconds;
  return {
    x: position.x + dirX * distance,
    y: position.y,
    z: position.z + dirZ * distance,
  };
}

/** O cliente nunca dita o tempo decorrido: o servidor mede e limita para conter picos de latência. */
export function clampDeltaSeconds(dtSeconds: number, maxDtSeconds = 0.25): number {
  return Math.max(0, Math.min(dtSeconds, maxDtSeconds));
}

/** Distância euclidiana no plano horizontal (ignora Y, como o resto da validação de movimento/combate). */
export function distanceBetween(a: Vector3, b: Vector3): number {
  return Math.hypot(a.x - b.x, a.z - b.z);
}

/** Move uma posição em direção a um alvo, sem ultrapassá-lo, a uma velocidade fixa. */
export function moveToward(position: Vector3, target: Vector3, speedUnitsPerSecond: number, dtSeconds: number): Vector3 {
  const dx = target.x - position.x;
  const dz = target.z - position.z;
  const distance = Math.hypot(dx, dz);
  const step = speedUnitsPerSecond * dtSeconds;
  if (distance <= step || distance === 0) {
    return { x: target.x, y: position.y, z: target.z };
  }
  return {
    x: position.x + (dx / distance) * step,
    y: position.y,
    z: position.z + (dz / distance) * step,
  };
}
