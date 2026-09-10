import { describe, expect, it } from "vitest";
import { applyMovement, clampDeltaSeconds } from "../../src/modules/world/movement.js";

describe("applyMovement", () => {
  it("moves in the input direction at the configured max speed", () => {
    const result = applyMovement({ x: 0, y: 0, z: 0 }, { moveX: 1, moveZ: 0 }, 1, 6);
    expect(result).toEqual({ x: 6, y: 0, z: 0 });
  });

  it("normalizes diagonal input instead of moving faster", () => {
    const result = applyMovement({ x: 0, y: 0, z: 0 }, { moveX: 1, moveZ: 1 }, 1, 6);
    expect(result.x).toBeCloseTo(6 / Math.sqrt(2));
    expect(result.z).toBeCloseTo(6 / Math.sqrt(2));
  });

  it("never lets input above 1 exceed the max speed", () => {
    const result = applyMovement({ x: 0, y: 0, z: 0 }, { moveX: 5, moveZ: 0 }, 1, 6);
    expect(result.x).toBeCloseTo(6);
  });

  it("keeps position unchanged when there is no input", () => {
    const position = { x: 3, y: 0, z: 4 };
    expect(applyMovement(position, { moveX: 0, moveZ: 0 }, 1, 6)).toEqual(position);
  });

  it("keeps position unchanged when elapsed time is zero", () => {
    const position = { x: 3, y: 0, z: 4 };
    expect(applyMovement(position, { moveX: 1, moveZ: 0 }, 0, 6)).toEqual(position);
  });
});

describe("clampDeltaSeconds", () => {
  it("clamps large gaps caused by lag", () => {
    expect(clampDeltaSeconds(5)).toBe(0.25);
  });

  it("clamps negative values to zero", () => {
    expect(clampDeltaSeconds(-1)).toBe(0);
  });

  it("keeps small deltas unchanged", () => {
    expect(clampDeltaSeconds(0.1)).toBe(0.1);
  });
});
