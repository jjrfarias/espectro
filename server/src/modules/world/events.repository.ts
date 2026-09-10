import type { PoolClient } from "pg";
import { pool } from "../../persistence/db.js";

/**
 * Histórico persistente do mundo (docs/ARQUITETURA-MVP.md §10). Cada linha é um fato que
 * aconteceu — nunca é atualizada nem apagada por código de aplicação, só inserida.
 */
export interface WorldEventRow {
  id: string;
  event_type: string;
  unique_key: string | null;
  character_id: string | null;
  occurred_at: string;
  location_code: string | null;
  data_json: Record<string, unknown>;
  character_name: string | null;
}

export interface RecordEventInput {
  eventType: string;
  /** Quando definido, o evento só é gravado uma vez no mundo inteiro (ex.: "primeiro lingote de ferro"). */
  uniqueKey?: string;
  characterId?: string;
  locationCode?: string;
  data?: Record<string, unknown>;
}

/**
 * Registra um evento. Se `uniqueKey` já existir, não faz nada e retorna false — quem chamou sabe
 * então que este NÃO foi o primeiro a alcançar aquele marco. Recebe um client opcional pra poder
 * participar da mesma transação de quem está gravando o resto da ação (ex.: economy.service.ts
 * fundindo o item e registrando o marco atomicamente).
 */
export async function recordEvent(
  input: RecordEventInput,
  client: PoolClient | typeof pool = pool,
): Promise<boolean> {
  const result = await client.query(
    `insert into world_events (event_type, unique_key, character_id, location_code, data_json)
     values ($1, $2, $3, $4, $5)
     on conflict (unique_key) do nothing`,
    [input.eventType, input.uniqueKey ?? null, input.characterId ?? null, input.locationCode ?? null, JSON.stringify(input.data ?? {})],
  );
  return (result.rowCount ?? 0) === 1;
}

export async function listRecentEvents(limit: number): Promise<WorldEventRow[]> {
  const result = await pool.query<WorldEventRow>(
    `select we.id, we.event_type, we.unique_key, we.character_id, we.occurred_at, we.location_code, we.data_json,
            c.name as character_name
     from world_events we
     left join characters c on c.id = we.character_id
     order by we.occurred_at desc
     limit $1`,
    [limit],
  );
  return result.rows;
}
