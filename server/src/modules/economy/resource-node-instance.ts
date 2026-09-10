import type { ResourceCode } from "@espectro/contracts";
import { pool } from "../../persistence/db.js";
import type { Vector3 } from "../world/movement.js";

export interface ResourceNodeState {
  id: string;
  resourceCode: ResourceCode;
  position: Vector3;
  available: boolean;
  depletedAt: number | null;
}

interface ResourceNodeRow {
  id: string;
  resource_code: ResourceCode;
  position_json: Vector3;
  available: boolean;
}

/**
 * Veios de recurso são conteúdo orientado a dados (docs/ARQUITETURA-MVP.md §14): definidos na
 * tabela `resource_nodes` (migração 003_economy.ts), não hardcoded como os inimigos do Corte 2
 * ainda são. `available`/`depleted_at` no banco refletem só o estado no momento do carregamento —
 * a instância em memória é quem controla depleção/reaparecimento durante a execução (mesmo
 * padrão de enemy-instance.ts), então não precisamos escrever de volta no banco a cada extração.
 */
export async function loadResourceNodes(): Promise<Map<string, ResourceNodeState>> {
  const result = await pool.query<ResourceNodeRow>(
    `select id, resource_code, position_json, available from resource_nodes`,
  );
  const nodes = new Map<string, ResourceNodeState>();
  for (const row of result.rows) {
    nodes.set(row.id, {
      id: row.id,
      resourceCode: row.resource_code,
      position: row.position_json,
      available: row.available,
      depletedAt: null,
    });
  }
  return nodes;
}
