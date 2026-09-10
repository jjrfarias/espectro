using System;

namespace Espectro.Network
{
    // Espelha contracts/src/index.ts (Cortes 1 e 2). Unity não pode importar o pacote
    // TypeScript diretamente, então estes tipos precisam ser mantidos em sincronia manualmente
    // com o servidor sempre que o protocolo mudar.

    [Serializable]
    public class CredentialsRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class SessionResponse
    {
        public string accessToken;
        public string refreshToken;
    }

    [Serializable]
    public class RefreshRequest
    {
        public string refreshToken;
    }

    [Serializable]
    public class CreateCharacterRequest
    {
        public string name;
    }

    [Serializable]
    public class PositionDto
    {
        public float x;
        public float y;
        public float z;
        public float facingY;
    }

    [Serializable]
    public class CharacterResponse
    {
        public string id;
        public string name;
        public int level;
        public int xp;
        public int hp;
        public PositionDto position;
        public int version;
    }

    [Serializable]
    public class ApiErrorResponse
    {
        public string error;
        public string message;
    }

    [Serializable]
    public class EnvelopeTypePeek
    {
        public string type;
    }

    [Serializable]
    public class WorldJoinPayload
    {
    }

    [Serializable]
    public class WorldJoinEnvelope
    {
        public int v = 1;
        public string type = "world.join";
        public string requestId;
        public int sequence = 1;
        public string sentAt;
        public WorldJoinPayload payload = new();
    }

    [Serializable]
    public class MovementInputPayload
    {
        public float moveX;
        public float moveZ;
        public float facingY;
    }

    [Serializable]
    public class MovementInputEnvelope
    {
        public int v = 1;
        public string type = "movement.input";
        public string requestId;
        public int sequence;
        public string sentAt;
        public MovementInputPayload payload;
    }

    [Serializable]
    public class Vector3Dto
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class SnapshotCharacterDto
    {
        public string characterId;
        public string name;
        public Vector3Dto position;
        public float facingY;
        public float hp;
        public float maxHp;
    }

    [Serializable]
    public class SnapshotEnemyDto
    {
        public string enemyId;
        public string definitionCode;
        public Vector3Dto position;
        public float hp;
        public float maxHp;
        public bool alive;
    }

    // Corte 3 — economia (server/README.md). Veio de recurso visível a todo mundo (sem PvP: não
    // pertence a ninguém, só fica indisponível enquanto esgotado).
    [Serializable]
    public class SnapshotResourceNodeDto
    {
        public string nodeId;
        public string resourceCode;
        public Vector3Dto position;
        public bool available;
    }

    [Serializable]
    public class WorldSnapshotPayload
    {
        public string instanceId;
        public string serverTime;
        public float movementSpeed;
        public SnapshotCharacterDto self;
        public SnapshotCharacterDto[] others;
        public SnapshotEnemyDto[] enemies;
        public SnapshotResourceNodeDto[] resourceNodes;
    }

    [Serializable]
    public class WorldSnapshotEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public WorldSnapshotPayload payload;
    }

    [Serializable]
    public class ErrorPayload
    {
        public string code;
        public string message;
        public bool retryable;
    }

    [Serializable]
    public class ErrorEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public ErrorPayload payload;
    }

    // Corte 2 — combate. Sem PvP no MVP: o alvo é sempre um inimigo (enemyId do world.snapshot).
    [Serializable]
    public class CombatAttackRequestPayload
    {
        public string targetId;
    }

    [Serializable]
    public class CombatAttackRequestEnvelope
    {
        public int v = 1;
        public string type = "combat.attack.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public CombatAttackRequestPayload payload;
    }

    [Serializable]
    public class CombatResolvedPayload
    {
        public string targetId;
        public float damage;
        public float targetHp;
        public float targetMaxHp;
        public bool targetDied;
        public int xpAwarded;
        public int characterXp;
        public int characterLevel;
        public bool leveledUp;
    }

    [Serializable]
    public class CombatResolvedEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public CombatResolvedPayload payload;
    }

    [Serializable]
    public class CombatPlayerDamagedPayload
    {
        public string sourceEnemyId;
        public float damage;
        public float hp;
        public float maxHp;
        public bool died;
        public Vector3Dto respawnPosition;
    }

    [Serializable]
    public class CombatPlayerDamagedEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public CombatPlayerDamagedPayload payload;
    }

    // Corte 3 — economia (server/README.md, GDD-MVP.md §9-§12): inventário, mineração,
    // metalurgia e venda ao comerciante.
    [Serializable]
    public class InventoryItemDto
    {
        public string itemCode;
        public int quantity;
    }

    // GDD §9: "equipamento possui dois espaços ativos: mão principal e ferramenta". Campo null
    // (ambos os campos são referência, JsonUtility lida bem com JSON null aqui) = espaço vazio.
    [Serializable]
    public class EquipmentDto
    {
        public string mainHand;
        public string tool;
    }

    [Serializable]
    public class EconomySnapshotPayload
    {
        public int coinBalance;
        public InventoryItemDto[] inventory;
        public EquipmentDto equipment;
    }

    [Serializable]
    public class EconomySnapshotEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public EconomySnapshotPayload payload;
    }

    [Serializable]
    public class MineRequestPayload
    {
        public string nodeId;
    }

    [Serializable]
    public class MineRequestEnvelope
    {
        public int v = 1;
        public string type = "resource.mine.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public MineRequestPayload payload;
    }

    [Serializable]
    public class MineResultPayload
    {
        public string nodeId;
        public string resourceCode;
        public int quantityGained;
        public int xpAwarded;
        public EconomySnapshotPayload economy;
    }

    [Serializable]
    public class MineResultEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public MineResultPayload payload;
    }

    [Serializable]
    public class CraftRequestPayload
    {
        public string recipeCode;
    }

    [Serializable]
    public class CraftRequestEnvelope
    {
        public int v = 1;
        public string type = "craft.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public CraftRequestPayload payload;
    }

    [Serializable]
    public class CraftResultPayload
    {
        public string recipeCode;
        public string producedItemCode;
        public int producedQuantity;
        public int xpAwarded;
        public EconomySnapshotPayload economy;
    }

    [Serializable]
    public class CraftResultEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public CraftResultPayload payload;
    }

    [Serializable]
    public class SellRequestPayload
    {
        public string itemCode;
        public int quantity;
    }

    [Serializable]
    public class SellRequestEnvelope
    {
        public int v = 1;
        public string type = "trade.sell.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public SellRequestPayload payload;
    }

    [Serializable]
    public class SellResultPayload
    {
        public string itemCode;
        public int quantitySold;
        public int coinsEarned;
        public EconomySnapshotPayload economy;
    }

    [Serializable]
    public class SellResultEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public SellResultPayload payload;
    }

    [Serializable]
    public class EquipRequestPayload
    {
        public string slot; // "main_hand" ou "tool".
        public string itemCode; // null desequipa o espaço.
    }

    [Serializable]
    public class EquipRequestEnvelope
    {
        public int v = 1;
        public string type = "equip.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public EquipRequestPayload payload;
    }

    [Serializable]
    public class EquipResultPayload
    {
        public string slot;
        public string itemCode;
        public EconomySnapshotPayload economy;
    }

    [Serializable]
    public class EquipResultEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public EquipResultPayload payload;
    }
}
