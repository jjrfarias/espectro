using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Espectro.Prototype;
using UnityEngine;

namespace Espectro.Network
{
    // O teste local continua disponível; uma sessão online só começa após o primeiro snapshot.
    public sealed class NetworkSession : MonoBehaviour
    {
        private const float MovementSendIntervalSeconds = 1f / 12f;
        private NetworkUI ui;
        private NetworkCombatController combat;
        private NetworkEconomyController economy;
        private NetworkAttributesController attributesController;
        private NetworkMapController map;
        private WorldConnection connection;
        private PrototypePlayerController player;
        private InteractionController interactions;
        private ThirdPersonCamera gameplayCamera;
        private GameObject gameplayInterface;
        private string accessToken;
        private string refreshToken;
        private bool joined;
        private bool onlineRequested;
        private bool busy;
        private bool destroyed;
        private bool alive;
        private int generation;
        private float movementTimer;
        private Vector3 movementAccumulated;
        private Vector3 serverPosition;
        private float serverSnapshotAt;
        private float joinStartedAt;
        private float serverSpeed = 6f;
        private readonly Dictionary<string, RemotePlayerView> remotePlayers = new();
        private readonly HashSet<string> snapshotScratch = new();
        private readonly List<string> removedPlayers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<NetworkSession>() != null) return;
            new GameObject("Sessao de Rede").AddComponent<NetworkSession>();
        }

        private void Start()
        {
            player = FindAnyObjectByType<PrototypePlayerController>();
            gameplayCamera = FindAnyObjectByType<ThirdPersonCamera>();
            interactions = FindAnyObjectByType<InteractionController>();
            gameplayInterface = interactions != null ? interactions.transform.root.gameObject : GameObject.Find("Interface");
            combat = NetworkCombatController.Create(player);
            combat.transform.SetParent(transform, false);
            combat.OnlineRequested += BeginOnline;
            combat.LocalRequested += ReturnToLocal;
            combat.AttackRequested += RequestAttack;
            combat.SetLocalMode();
            InteractionController.OnlineNpcTalkRequested += HandleOnlineNpcTalk;
            economy = NetworkEconomyController.Create(player);
            economy.transform.SetParent(transform, false);
            economy.MineRequested += RequestMine;
            economy.CraftRequested += RequestCraft;
            economy.SellRequested += RequestSell;
            economy.EquipRequested += RequestEquip;
            attributesController = NetworkAttributesController.Create();
            attributesController.transform.SetParent(transform, false);
            attributesController.AllocateRequested += RequestAttributeAllocate;
            attributesController.RespecRequested += RequestAttributeRespec;
            map = NetworkMapController.Create(player);
            map.transform.SetParent(transform, false);
            // O jogo inicia sempre no fluxo online; o mundo não é exibido como
            // fallback offline antes da autenticação.
            BeginOnline();
        }

        private void Update()
        {
            connection?.PumpMainThread();
            if (onlineRequested && !joined && connection != null && Time.unscaledTime - joinStartedAt > 15f)
                FailConnection("O mundo não respondeu. Tente entrar novamente.");
        }

        private void LateUpdate()
        {
            if (!joined || !alive || player == null || connection == null) return;
            // Velocidade efetiva no mundo: já considera câmera, aceleração e colisão local.
            movementTimer += Time.deltaTime;
            movementAccumulated += player.LastWorldVelocity * Time.deltaTime;
            if (movementTimer >= MovementSendIntervalSeconds)
            {
                var input = Vector3.ClampMagnitude(movementAccumulated / (movementTimer * serverSpeed), 1f);
                connection.SendMovementInput(input.x, input.z, player.FacingYDegrees);
                movementTimer = 0f;
                movementAccumulated = Vector3.zero;
            }

            // Correção horizontal limitada. O terreno/gravidade continuam no cliente.
            // A tolerância acomoda o intervalo de snapshots e a previsão ainda não confirmada.
            var error = serverPosition - player.transform.position;
            error.y = 0f;
            if (Time.unscaledTime - serverSnapshotAt < 1f && error.magnitude > 0.85f)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null && controller.enabled)
                    controller.Move(error * Mathf.Min(1f, Time.deltaTime * 6f));
            }
        }

        private void BeginOnline()
        {
            if (onlineRequested) return;
            generation++;
            onlineRequested = true;
            joined = false;
            SetGameplayActive(false);
            economy.SetActive(false);
            attributesController.SetActive(false);
            map.SetActive(false);
            EnsureAuthUi();
            ShowAuth();
            ui.SetAuthStatus("");
            combat.SetConnectingMode("Entre para compartilhar o mundo.");
            var savedRefresh = PlayerPrefs.GetString("espectro.session.refresh", "");
            if (!string.IsNullOrEmpty(savedRefresh)) _ = RestoreSavedSession(savedRefresh, generation);
        }

        private void EnsureAuthUi()
        {
            if (ui != null) return;
            ui = NetworkUI.Create();
            ui.transform.SetParent(transform, false);
            ui.LoginSubmitted += HandleLogin;
            ui.RegisterSubmitted += HandleRegister;
            ui.CharacterNameSubmitted += HandleCreateCharacter;
        }

        private void ShowAuth()
        {
            ui.gameObject.SetActive(true);
            ui.ShowAuth();
        }

        private void ReturnToLocal()
        {
            generation++;
            onlineRequested = false;
            joined = false;
            alive = false;
            busy = false;
            accessToken = refreshToken = null;
            DisposeConnection();
            ClearRemotePlayers();
            ResetMovement();
            if (ui != null) ui.Hide();
            if (player != null)
            {
                player.SpeedLimit = float.PositiveInfinity;
                PlacePlayer(0f, 0f, 0f);
            }
            combat.SetLocalMode();
            economy.SetActive(false);
            attributesController.SetActive(false);
            map.SetActive(false);
            SetGameplayActive(true);
        }

        private async void HandleLogin(string email, string password) =>
            await RunAuthFlow(() => ApiClient.LoginAsync(email, password));

        private async void HandleRegister(string email, string password) =>
            await RunAuthFlow(() => ApiClient.RegisterAsync(email, password));

        private async Task RunAuthFlow(Func<Task<SessionResponse>> action)
        {
            if (busy || !onlineRequested || connection != null) return;
            var attempt = generation;
            busy = true;
            ui.SetAuthStatus("Conectando...");
            try
            {
                var session = await action();
                if (!IsCurrent(attempt)) return;
                accessToken = session.accessToken;
                refreshToken = session.refreshToken;
                SaveSession(session);
                CharacterResponse character;
                try
                {
                    character = await ApiClient.GetMyCharacterAsync(accessToken);
                }
                catch (ApiException ex) when (ex.StatusCode == 404)
                {
                    if (IsCurrent(attempt))
                    {
                        ui.SetAuthStatus("");
                        ui.ShowCharacterCreation();
                    }
                    return;
                }
                if (IsCurrent(attempt)) await EnterWorld(character, attempt);
            }
            catch (Exception ex)
            {
                if (IsCurrent(attempt))
                {
                    ui.SetAuthStatus(ex is ApiException ? ex.Message : "Falha de conexão com o servidor.");
                    Debug.LogWarning($"[NetworkSession] Autenticação falhou: {ex.Message}");
                }
            }
            finally
            {
                if (IsCurrent(attempt)) busy = false;
            }
        }

        private async Task RestoreSavedSession(string savedToken, int attempt)
        {
            if (busy || !onlineRequested || connection != null) return;
            busy = true;
            ui.SetAuthStatus("Restaurando sua sessão...");
            try
            {
                var session = await ApiClient.RefreshAsync(savedToken);
                if (!IsCurrent(attempt)) return;
                accessToken = session.accessToken;
                refreshToken = session.refreshToken;
                SaveSession(session);
                var character = await ApiClient.GetMyCharacterAsync(accessToken);
                if (IsCurrent(attempt)) await EnterWorld(character, attempt);
            }
            catch
            {
                PlayerPrefs.DeleteKey("espectro.session.refresh");
                PlayerPrefs.Save();
                if (IsCurrent(attempt)) ui.SetAuthStatus("Sessão expirada. Entre novamente.");
            }
            finally { busy = false; }
        }

        private static void SaveSession(SessionResponse session)
        {
            if (session == null || string.IsNullOrEmpty(session.refreshToken)) return;
            PlayerPrefs.SetString("espectro.session.refresh", session.refreshToken);
            PlayerPrefs.Save();
        }

        private async void HandleCreateCharacter(string name)
        {
            if (busy || !onlineRequested || connection != null) return;
            var attempt = generation;
            busy = true;
            ui.SetCharacterStatus("Criando...");
            try
            {
                var character = await ApiClient.CreateCharacterAsync(accessToken, name);
                if (IsCurrent(attempt)) await EnterWorld(character, attempt);
            }
            catch (Exception ex)
            {
                if (IsCurrent(attempt))
                    ui.SetCharacterStatus(ex is ApiException ? ex.Message : "Falha de conexão com o servidor.");
            }
            finally
            {
                if (IsCurrent(attempt)) busy = false;
            }
        }

        private async Task EnterWorld(CharacterResponse character, int attempt)
        {
            DisposeConnection();
            ClearRemotePlayers();
            ResetMovement();
            combat.PrepareCharacter(character);
            joinStartedAt = Time.unscaledTime;
            var pending = new WorldConnection();
            connection = pending;
            pending.SnapshotReceived += HandleSnapshot;
            pending.ErrorReceived += HandleWorldError;
            pending.CombatResolved += HandleCombatResolved;
            pending.PlayerDamaged += HandlePlayerDamaged;
            pending.EconomySnapshotReceived += HandleEconomySnapshot;
            pending.MineResultReceived += HandleMineResult;
            pending.CraftResultReceived += HandleCraftResult;
            pending.SellResultReceived += HandleSellResult;
            pending.EquipResultReceived += HandleEquipResult;
            pending.AttributesSnapshotReceived += HandleAttributesSnapshot;
            pending.TutorialSnapshotReceived += HandleTutorialSnapshot;
            pending.NpcTalkResultReceived += HandleNpcTalkResult;
            pending.Disconnected += HandleDisconnected;
            combat.SetConnectingMode("Entrando no mundo...");
            try
            {
                await pending.ConnectAsync(accessToken);
                if (!IsCurrent(attempt) || connection != pending) return;
                pending.SendJoin();
                // O primeiro snapshot, e não a abertura do socket, libera o jogo.
            }
            catch (Exception ex)
            {
                if (IsCurrent(attempt) && connection == pending)
                    FailConnection("Não foi possível conectar ao mundo.");
                Debug.LogWarning($"[NetworkSession] Entrada no mundo falhou: {ex.Message}");
            }
        }

        private void HandleTutorialSnapshot(TutorialSnapshotPayload snapshot)
        {
            if (snapshot == null || interactions == null) return;
            var completed = snapshot.completedSteps;
            var stage = completed == null ? (snapshot.completed ? 4 : 0) : Mathf.Clamp(completed.Length, 0, 4);
            interactions.ApplyRemoteTutorialStage(stage);
        }

        private void HandleNpcTalkResult(NpcTalkResultPayload result)
        {
            if (result?.tutorial != null) HandleTutorialSnapshot(result.tutorial);
        }

        private void HandleOnlineNpcTalk(string npcCode)
        {
            if (joined && connection != null) connection.SendNpcTalkRequest(npcCode);
        }

        private void HandleSnapshot(WorldSnapshotPayload snapshot)
        {
            if (!onlineRequested || snapshot?.self?.position == null) return;
            var firstSnapshot = !joined;
            var wasAlive = alive;
            joined = true;
            alive = snapshot.self.hp > 0;
            serverSpeed = snapshot.movementSpeed > 0 ? snapshot.movementSpeed : 6f;
            serverPosition = new Vector3(snapshot.self.position.x, snapshot.self.position.y, snapshot.self.position.z);
            serverSnapshotAt = Time.unscaledTime;
            if (player != null) player.SpeedLimit = serverSpeed;
            if (firstSnapshot || (!wasAlive && alive))
            {
                PlacePlayer(serverPosition.x, serverPosition.z, snapshot.self.facingY);
                ResetMovement();
                if (gameplayCamera != null && player != null) gameplayCamera.SetTarget(player.transform);
            }
            if (firstSnapshot)
            {
                ui.Hide();
                SetGameplayActive(true);
                economy.SetActive(true);
                map.SetActive(true);
            }
            if (player != null) player.enabled = alive;
            if (!alive) ResetMovement();
            combat.ApplySnapshot(snapshot);
            economy.ApplyResourceNodes(snapshot.resourceNodes);
            map.ApplySnapshot(snapshot);

            snapshotScratch.Clear();
            foreach (var other in snapshot.others ?? Array.Empty<SnapshotCharacterDto>())
            {
                snapshotScratch.Add(other.characterId);
                if (remotePlayers.TryGetValue(other.characterId, out var view) && view != null)
                    view.ApplySnapshot(other);
                else
                    remotePlayers[other.characterId] = RemotePlayerView.Create(other);
            }
            removedPlayers.Clear();
            foreach (var pair in remotePlayers)
                if (!snapshotScratch.Contains(pair.Key)) removedPlayers.Add(pair.Key);
            foreach (var id in removedPlayers)
            {
                if (remotePlayers[id] != null) Destroy(remotePlayers[id].gameObject);
                remotePlayers.Remove(id);
            }
        }

        private void RequestAttack(string targetId)
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendAttackRequest(targetId);
        }

        private void RequestMine(string nodeId)
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendMineRequest(nodeId);
        }

        private void RequestCraft(string recipeCode)
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendCraftRequest(recipeCode);
        }

        private void RequestSell(string itemCode, int quantity)
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendSellRequest(itemCode, quantity);
        }

        private void RequestEquip(string slot, string itemCode)
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendEquipRequest(slot, itemCode);
        }

        private void RequestAttributeAllocate(string attribute)
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendAttributeAllocateRequest(attribute);
        }

        private void RequestAttributeRespec()
        {
            if (joined && alive && connection != null && connection.IsOpen)
                connection.SendAttributeRespecRequest();
        }

        private void HandleEconomySnapshot(EconomySnapshotPayload result) => economy.ApplyEconomy(result);

        private void HandleMineResult(MineResultPayload result) => economy.ApplyMineResult(result);

        private void HandleCraftResult(CraftResultPayload result) => economy.ApplyCraftResult(result);

        private void HandleSellResult(SellResultPayload result) => economy.ApplySellResult(result);

        private void HandleEquipResult(EquipResultPayload result) => economy.ApplyEquipResult(result);

        private void HandleAttributesSnapshot(AttributesSnapshotPayload result) => attributesController.ApplyAttributes(result);

        private void HandleCombatResolved(CombatResolvedPayload result) => combat.ApplyResolved(result);

        private void HandlePlayerDamaged(CombatPlayerDamagedPayload result)
        {
            if (!joined || result == null) return;
            combat.ApplyDamaged(result);
            if (result.died || result.hp <= 0)
            {
                alive = false;
                if (player != null) player.enabled = false;
                ResetMovement();
            }
        }

        private void HandleWorldError(ErrorPayload error)
        {
            if (error == null) return;
            if (!joined || error.code == "UNAUTHORIZED" || error.code == "NOT_IN_WORLD")
            {
                FailConnection(error.message);
                return;
            }
            combat.ShowMessage(error.message);
            economy.ShowMessage(error.message);
        }

        private async void HandleDisconnected()
        {
            if (!onlineRequested || destroyed) return;
            var wasJoined = joined;
            joined = false;
            alive = false;
            DisposeConnection();
            ClearRemotePlayers();
            ResetMovement();
            SetGameplayActive(false);
            economy.SetActive(false);
            attributesController.SetActive(false);
            map.SetActive(false);
            if (!wasJoined)
            {
                FailConnection("Conexão encerrada antes de entrar no mundo.");
                return;
            }
            combat.SetConnectingMode("Conexão perdida. Reconectando...");
            var attempt = ++generation;
            try
            {
                await Task.Delay(1500);
                if (!IsCurrent(attempt)) return;
                var session = await ApiClient.RefreshAsync(refreshToken);
                if (!IsCurrent(attempt)) return;
                accessToken = session.accessToken;
                refreshToken = session.refreshToken;
                var character = await ApiClient.GetMyCharacterAsync(accessToken);
                if (IsCurrent(attempt)) await EnterWorld(character, attempt);
            }
            catch (Exception)
            {
                if (IsCurrent(attempt)) FailConnection("Conexão perdida. Entre novamente.");
            }
        }

        private void FailConnection(string message)
        {
            generation++;
            joined = alive = busy = false;
            DisposeConnection();
            ClearRemotePlayers();
            ResetMovement();
            SetGameplayActive(false);
            economy.SetActive(false);
            attributesController.SetActive(false);
            map.SetActive(false);
            EnsureAuthUi();
            ShowAuth();
            ui.SetAuthStatus(message);
            combat.SetConnectingMode(message);
        }

        private bool IsCurrent(int attempt) => !destroyed && onlineRequested && generation == attempt;

        private void DisposeConnection()
        {
            if (connection == null) return;
            connection.SnapshotReceived -= HandleSnapshot;
            connection.ErrorReceived -= HandleWorldError;
            connection.CombatResolved -= HandleCombatResolved;
            connection.PlayerDamaged -= HandlePlayerDamaged;
            connection.EconomySnapshotReceived -= HandleEconomySnapshot;
            connection.MineResultReceived -= HandleMineResult;
            connection.CraftResultReceived -= HandleCraftResult;
            connection.SellResultReceived -= HandleSellResult;
            connection.EquipResultReceived -= HandleEquipResult;
            connection.AttributesSnapshotReceived -= HandleAttributesSnapshot;
            connection.Disconnected -= HandleDisconnected;
            connection.Dispose();
            connection = null;
        }

        private void ClearRemotePlayers()
        {
            foreach (var view in remotePlayers.Values)
                if (view != null) Destroy(view.gameObject);
            remotePlayers.Clear();
        }

        private void ResetMovement()
        {
            movementTimer = 0f;
            movementAccumulated = Vector3.zero;
            if (player != null) player.MobileInput = Vector2.zero;
        }

        private void OnDestroy()
        {
            destroyed = true;
            generation++;
            InteractionController.OnlineNpcTalkRequested -= HandleOnlineNpcTalk;
            DisposeConnection();
            ClearRemotePlayers();
        }

        private void SetGameplayActive(bool active)
        {
            if (player != null) player.enabled = active;
            if (gameplayInterface != null) gameplayInterface.SetActive(active);
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = active;
                if (active && player != null) gameplayCamera.SetTarget(player.transform);
            }
        }

        private void PlacePlayer(float x, float z, float facingY)
        {
            if (player == null) return;
            var controller = player.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            player.transform.position = ResolveGroundedSpawn(x, z);
            player.transform.rotation = Quaternion.Euler(0f, facingY, 0f);
            if (controller != null) controller.enabled = wasEnabled;
        }

        private Vector3 ResolveGroundedSpawn(float x, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z)) x = z = 0f;
            if (TryFindGround(x, z, out var grounded)) return grounded;
            return TryFindGround(0f, 0f, out grounded) ? grounded : new Vector3(0f, 0.05f, 0f);
        }

        private bool TryFindGround(float x, float z, out Vector3 position)
        {
            var hits = Physics.RaycastAll(new Vector3(x, 30f, z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (player != null && hit.transform.IsChildOf(player.transform)) continue;
                if (hit.normal.y < 0.55f) continue;
                position = new Vector3(x, hit.point.y + 0.05f, z);
                return true;
            }
            position = default;
            return false;
        }
    }
}
