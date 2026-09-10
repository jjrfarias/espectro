import { randomUUID } from "node:crypto";
import { describe, expect, it } from "vitest";
import { UnsupportedTypeError, parseClientMessage } from "../../src/transport/envelope.js";

function baseEnvelope(overrides: Record<string, unknown> = {}) {
  return {
    v: 1,
    type: "world.join",
    requestId: randomUUID(),
    sequence: 1,
    sentAt: new Date().toISOString(),
    payload: {},
    ...overrides,
  };
}

describe("parseClientMessage", () => {
  it("accepts a valid world.join message", () => {
    const message = parseClientMessage(baseEnvelope());
    expect(message.type).toBe("world.join");
  });

  it("accepts a valid movement.input message", () => {
    const message = parseClientMessage(
      baseEnvelope({ type: "movement.input", payload: { moveX: 0.5, moveZ: -1, facingY: 90 } }),
    );
    expect(message.type).toBe("movement.input");
  });

  it("accepts a valid combat.attack.request message", () => {
    const message = parseClientMessage(baseEnvelope({ type: "combat.attack.request", payload: { targetId: "lobo-0" } }));
    expect(message.type).toBe("combat.attack.request");
  });

  it("rejects an unknown message type", () => {
    expect(() => parseClientMessage(baseEnvelope({ type: "chronicle.rewrite.reality" }))).toThrow(UnsupportedTypeError);
  });

  it("rejects a payload outside the valid range", () => {
    expect(() =>
      parseClientMessage(baseEnvelope({ type: "movement.input", payload: { moveX: 5, moveZ: 0, facingY: 0 } })),
    ).toThrow();
  });

  it("rejects an envelope with the wrong protocol version", () => {
    expect(() => parseClientMessage(baseEnvelope({ v: 2 }))).toThrow();
  });
});
