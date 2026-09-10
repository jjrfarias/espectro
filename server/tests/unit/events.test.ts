import { describe, expect, it, vi } from "vitest";
import { toChronicleLine } from "../../src/modules/world/events.routes.js";
import { recordEvent } from "../../src/modules/world/events.repository.js";

describe("toChronicleLine", () => {
  it("formats a known event type with the character's name (GDD §13)", () => {
    const line = toChronicleLine({
      event_type: "primeiro_lingote",
      character_name: "Aldric",
      occurred_at: "2026-09-10T12:00:00.000Z",
      data_json: { itemCode: "lingote_ferro" },
    });
    expect(line).toBe("Aldric fundiu o primeiro lingote de ferro em 10/09/2026.");
  });

  it("falls back to 'um aventureiro' when the character no longer exists (GDD §13 moderação)", () => {
    const line = toChronicleLine({
      event_type: "primeiro_lingote",
      character_name: null,
      occurred_at: "2026-09-10T12:00:00.000Z",
      data_json: { itemCode: "lingote_cobre" },
    });
    expect(line).toBe("um aventureiro fundiu o primeiro lingote de cobre em 10/09/2026.");
  });

  it("returns null for an event type without a chronicle template", () => {
    const line = toChronicleLine({
      event_type: "algum_evento_desconhecido",
      character_name: "Aldric",
      occurred_at: "2026-09-10T12:00:00.000Z",
      data_json: {},
    });
    expect(line).toBeNull();
  });
});

describe("recordEvent", () => {
  it("returns false (and inserts nothing new) when the unique_key already exists", async () => {
    const query = vi.fn().mockResolvedValue({ rowCount: 0, rows: [] });
    const client = { query } as unknown as Parameters<typeof recordEvent>[1];

    const recorded = await recordEvent({ eventType: "primeiro_lingote", uniqueKey: "primeiro_lingote:lingote_ferro" }, client);

    expect(recorded).toBe(false);
    expect(query).toHaveBeenCalledWith(
      expect.stringContaining("on conflict (unique_key) do nothing"),
      ["primeiro_lingote", "primeiro_lingote:lingote_ferro", null, null, "{}"],
    );
  });

  it("returns true when the event is newly inserted", async () => {
    const query = vi.fn().mockResolvedValue({ rowCount: 1, rows: [] });
    const client = { query } as unknown as Parameters<typeof recordEvent>[1];

    const recorded = await recordEvent(
      { eventType: "primeiro_lingote", uniqueKey: "primeiro_lingote:lingote_ferro", characterId: "char-1", data: { itemCode: "lingote_ferro" } },
      client,
    );

    expect(recorded).toBe(true);
  });
});
