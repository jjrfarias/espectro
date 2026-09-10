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

    // GDD §13/§4: cinco NPCs com diálogo roteirizado (texto fica no cliente) e progresso do
    // tutorial. server/README.md, seção "NPCs e progresso do tutorial", tem o payload exato.
    [Serializable] public class TutorialSnapshotPayload { public string[] completedSteps; public bool completed; public bool rewardClaimed; }
    [Serializable] public class TutorialSnapshotEnvelope { public int v; public string type; public string requestId; public int sequence; public string sentAt; public TutorialSnapshotPayload payload; }

    [Serializable] public class NpcTalkRequestPayload { public string npcCode; }
    [Serializable] public class NpcTalkRequestEnvelope { public int v = 1; public string type = "npc.talk.request"; public string requestId; public int sequence; public string sentAt; public NpcTalkRequestPayload payload; }
    // Corrigido pra bater com o servidor: o payload real aninha o progresso em "tutorial", não
    // campos soltos "tutorialStep"/"alreadyTalked" (que nunca existiram na resposta do servidor —
    // ficavam sempre com o valor padrão do C#, silenciosamente, porque JsonUtility ignora campos
    // desconhecidos do JSON em vez de dar erro).
    [Serializable] public class NpcTalkResultPayload { public string npcCode; public TutorialSnapshotPayload tutorial; }
    [Serializable] public class NpcTalkResultEnvelope { public int v; public string type; public string requestId; public int sequence; public string sentAt; public NpcTalkResultPayload payload; }

    // GDD §6: pontos de atributo ganhos ao subir de nível (attribute.allocate já existe no
    // servidor desde 10/09/2026; sem UI/wiring no cliente até agora).
    [Serializable]
    public class AttributesDto
    {
        public int strength;
        public int agility;
        public int vitality;
        public int resistance;
    }

    [Serializable]
    public class AttributesSnapshotPayload
    {
        public AttributesDto attributes;
        public int unspentPoints;
        public int maxHp;
    }

    [Serializable]
    public class AttributesSnapshotEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public AttributesSnapshotPayload payload;
    }

    [Serializable]
    public class AttributeAllocateRequestPayload
    {
        public string attribute; // "strength" | "agility" | "vitality" | "resistance"
    }

    [Serializable]
    public class AttributeAllocateRequestEnvelope
    {
        public int v = 1;
        public string type = "attribute.allocate";
        public string requestId;
        public int sequence;
        public string sentAt;
        public AttributeAllocateRequestPayload payload;
    }

    // GDD §6/§13: "permite redistribuição gratuita durante o teste, falando com a instrutora" —
    // devolve todos os pontos já alocados e reseta os 4 atributos pro valor inicial (5). Resposta
    // reaproveita AttributesSnapshotEnvelope (mesmo formato de attribute.allocate).
    [Serializable]
    public class AttributeRespecRequestPayload
    {
    }

    [Serializable]
    public class AttributeRespecRequestEnvelope
    {
        public int v = 1;
        public string type = "attribute.respec";
        public string requestId;
        public int sequence;
        public string sentAt;
        public AttributeRespecRequestPayload payload = new();
    }

    // GDD §8/§12: comprar do comerciante (hoje só a poção) e usar um item consumível (cura HP).
    [Serializable]
    public class BuyRequestPayload
    {
        public string itemCode;
        public int quantity;
    }

    [Serializable]
    public class BuyRequestEnvelope
    {
        public int v = 1;
        public string type = "trade.buy.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public BuyRequestPayload payload;
    }

    [Serializable]
    public class BuyResultPayload
    {
        public string itemCode;
        public int quantityBought;
        public int coinsSpent;
        public EconomySnapshotPayload economy;
    }

    [Serializable]
    public class BuyResultEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public BuyResultPayload payload;
    }

    [Serializable]
    public class UseItemRequestPayload
    {
        public string itemCode;
    }

    [Serializable]
    public class UseItemRequestEnvelope
    {
        public int v = 1;
        public string type = "item.use.request";
        public string requestId;
        public int sequence;
        public string sentAt;
        public UseItemRequestPayload payload;
    }

    [Serializable]
    public class UseItemResultPayload
    {
        public string itemCode;
        public int hp;
        public int maxHp;
        public EconomySnapshotPayload economy;
    }

    [Serializable]
    public class UseItemResultEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public UseItemResultPayload payload;
    }

    // GDD §10/§11: minerar/fundir levam tempo real (não resolvem mais na hora — ver
    // server/README.md, seção "Canais de mineração e fundição"). `resource.mine.request` e
    // `craft.request` continuam existindo (acima); agora a resposta chega em duas partes: um
    // "started" com a duração, e só depois o "result" (ou um "cancelled" se for interrompido).
    [Serializable]
    public class MineStartedPayload
    {
        public string nodeId;
        public string resourceCode;
        public int durationMs;
    }

    [Serializable]
    public class MineStartedEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public MineStartedPayload payload;
    }

    [Serializable]
    public class MineCancelledPayload
    {
        public string nodeId;
        public string reason; // "moved" | "damaged"
    }

    [Serializable]
    public class MineCancelledEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public MineCancelledPayload payload;
    }

    [Serializable]
    public class CraftStartedPayload
    {
        public string recipeCode;
        public int durationMs;
    }

    [Serializable]
    public class CraftStartedEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public CraftStartedPayload payload;
    }

    [Serializable]
    public class CraftCancelRequestPayload
    {
    }

    [Serializable]
    public class CraftCancelRequestEnvelope
    {
        public int v = 1;
        public string type = "craft.cancel";
        public string requestId;
        public int sequence;
        public string sentAt;
        public CraftCancelRequestPayload payload = new();
    }

    [Serializable]
    public class CraftCancelledPayload
    {
        public string recipeCode;
        public int refundedQuantity;
    }

    [Serializable]
    public class CraftCancelledEnvelope
    {
        public int v;
        public string type;
        public string requestId;
        public int sequence;
        public string sentAt;
        public CraftCancelledPayload payload;
    }
}
