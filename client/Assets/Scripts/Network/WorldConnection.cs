using System;
using System.Collections.Concurrent;
using System.IO;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
#endif
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Espectro.Network
{
    // Cliente WebSocket para /world (server/README.md, docs/ARQUITETURA-MVP.md seção 8).
    //
    // Em builds WebGL reais, System.Net.WebSockets.ClientWebSocket não funciona — o navegador
    // sandboxa o binário e não dá acesso a sockets nativos — então essa plataforma usa a
    // WebSocket nativa do navegador via WebGlWebSocketBridge.cs (P/Invoke para
    // Assets/Plugins/WebGL/EspectroWebSocket.jslib). Todas as outras plataformas, incluindo o
    // Editor mesmo com WebGL selecionado como alvo, usam ClientWebSocket normalmente.
    //
    // Os dois caminhos convergem na mesma fila (`incoming`) e no mesmo PumpMainThread(): nenhuma
    // API do Unity pode ser tocada fora da thread principal, e no caminho nativo a leitura roda
    // em segundo plano, então PumpMainThread() precisa ser chamado a cada quadro para disparar
    // os eventos com segurança.
    public sealed class WorldConnection : IDisposable
    {
        public event Action<WorldSnapshotPayload> SnapshotReceived;
        public event Action<ErrorPayload> ErrorReceived;
        public event Action<CombatResolvedPayload> CombatResolved;
        public event Action<CombatPlayerDamagedPayload> PlayerDamaged;
        public event Action<EconomySnapshotPayload> EconomySnapshotReceived;
        public event Action<MineResultPayload> MineResultReceived;
        public event Action<CraftResultPayload> CraftResultReceived;
        public event Action<SellResultPayload> SellResultReceived;
        public event Action<EquipResultPayload> EquipResultReceived;
        public event Action<TutorialSnapshotPayload> TutorialSnapshotReceived;
        public event Action<NpcTalkResultPayload> NpcTalkResultReceived;
        public event Action<AttributesSnapshotPayload> AttributesSnapshotReceived;
        public event Action<BuyResultPayload> BuyResultReceived;
        public event Action<UseItemResultPayload> UseItemResultReceived;
        // GDD §10/§11: minerar/fundir agora levam tempo real — Started chega antes do Result (ou
        // de um Cancelled, se movimento/dano/cancelamento voluntário interromper no meio).
        public event Action<MineStartedPayload> MineStartedReceived;
        public event Action<MineCancelledPayload> MineCancelledReceived;
        public event Action<CraftStartedPayload> CraftStartedReceived;
        public event Action<CraftCancelledPayload> CraftCancelledReceived;
        public event Action Disconnected;

        private readonly ConcurrentQueue<string> incoming = new();
        private int outboundSequence;
        private bool disconnectedFired;
        private volatile bool isClosed = true;
        private bool connecting;
        private bool connectionStarted;
        private bool disposed;

#if UNITY_WEBGL && !UNITY_EDITOR
        private int webGlSocketId = -1;
        private TaskCompletionSource<bool> connectCompletion;
#else
        private ClientWebSocket socket;
        private CancellationTokenSource cts;
        private readonly SemaphoreSlim sendLock = new(1, 1);
#endif

        public bool IsOpen => !isClosed;

        public async Task ConnectAsync(string accessToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WorldConnection));
            var uri = $"{NetworkSettings.WebSocketBaseUrl}/world?token={Uri.EscapeDataString(accessToken)}";
            disconnectedFired = false;
            outboundSequence = 0;
            connectionStarted = true;
            connecting = true;
            try
            {

#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            connectCompletion = tcs;
            webGlSocketId = WebGlWebSocketBridge.Connect(
                uri,
                onOpen: () =>
                {
                    if (disposed) return;
                    isClosed = false;
                    tcs.TrySetResult(true);
                },
                onMessage: json => incoming.Enqueue(json),
                onClose: () =>
                {
                    isClosed = true;
                    tcs.TrySetException(new IOException("Conexão encerrada antes de entrar no mundo."));
                },
                onError: message =>
                {
                    Debug.LogWarning($"[WorldConnection] Erro WebSocket (WebGL): {message}");
                    isClosed = true;
                    tcs.TrySetException(new IOException(message));
                });
            await tcs.Task;
#else
            await ConnectNativeAsync(uri);
#endif
            }
            finally
            {
                connecting = false;
            }
        }

        public void PumpMainThread()
        {
            if (disposed) return;
            while (incoming.TryDequeue(out var json))
            {
                Dispatch(json);
                if (disposed) return;
            }

            if (connectionStarted && !connecting && !disconnectedFired && isClosed)
            {
                disconnectedFired = true;
                Disconnected?.Invoke();
            }
        }

        public void SendJoin()
        {
            var envelope = new WorldJoinEnvelope
            {
                sequence = ++outboundSequence,
                requestId = Guid.NewGuid().ToString(),
                sentAt = NowIso(),
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendMovementInput(float moveX, float moveZ, float facingY)
        {
            var envelope = new MovementInputEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new MovementInputPayload { moveX = moveX, moveZ = moveZ, facingY = facingY },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendAttackRequest(string targetId)
        {
            var envelope = new CombatAttackRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new CombatAttackRequestPayload { targetId = targetId },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendMineRequest(string nodeId)
        {
            var envelope = new MineRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new MineRequestPayload { nodeId = nodeId },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendCraftRequest(string recipeCode)
        {
            var envelope = new CraftRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new CraftRequestPayload { recipeCode = recipeCode },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendSellRequest(string itemCode, int quantity)
        {
            var envelope = new SellRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new SellRequestPayload { itemCode = itemCode, quantity = quantity },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendEquipRequest(string slot, string itemCode)
        {
            var envelope = new EquipRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new EquipRequestPayload { slot = slot, itemCode = itemCode },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendNpcTalkRequest(string npcCode)
        {
            var envelope = new NpcTalkRequestEnvelope { requestId = Guid.NewGuid().ToString(), sequence = ++outboundSequence, sentAt = NowIso(), payload = new NpcTalkRequestPayload { npcCode = npcCode } };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendAttributeAllocateRequest(string attribute)
        {
            var envelope = new AttributeAllocateRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new AttributeAllocateRequestPayload { attribute = attribute },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        // GDD §6/§13: redistribuição gratuita — resposta chega em AttributesSnapshotReceived,
        // mesmo evento de SendAttributeAllocateRequest (o servidor reaproveita attributes.snapshot).
        public void SendAttributeRespecRequest()
        {
            var envelope = new AttributeRespecRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendBuyRequest(string itemCode, int quantity)
        {
            var envelope = new BuyRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new BuyRequestPayload { itemCode = itemCode, quantity = quantity },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendUseItemRequest(string itemCode)
        {
            var envelope = new UseItemRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
                payload = new UseItemRequestPayload { itemCode = itemCode },
            };
            Send(JsonUtility.ToJson(envelope));
        }

        public void SendCraftCancelRequest()
        {
            var envelope = new CraftCancelRequestEnvelope
            {
                requestId = Guid.NewGuid().ToString(),
                sequence = ++outboundSequence,
                sentAt = NowIso(),
            };
            Send(JsonUtility.ToJson(envelope));
        }

        private void Send(string json)
        {
            if (isClosed) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (webGlSocketId >= 0) WebGlWebSocketBridge.Send(webGlSocketId, json);
#else
            _ = SendNativeAsync(json);
#endif
        }

        private void Dispatch(string json)
        {
            EnvelopeTypePeek peek;
            try
            {
                peek = JsonUtility.FromJson<EnvelopeTypePeek>(json);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("[WorldConnection] Mensagem recebida não era JSON válido.");
                return;
            }

            switch (peek?.type)
            {
                case "world.snapshot":
                    SnapshotReceived?.Invoke(JsonUtility.FromJson<WorldSnapshotEnvelope>(json).payload);
                    break;
                case "error":
                    ErrorReceived?.Invoke(JsonUtility.FromJson<ErrorEnvelope>(json).payload);
                    break;
                case "combat.resolved":
                    CombatResolved?.Invoke(JsonUtility.FromJson<CombatResolvedEnvelope>(json).payload);
                    break;
                case "combat.player_damaged":
                    PlayerDamaged?.Invoke(JsonUtility.FromJson<CombatPlayerDamagedEnvelope>(json).payload);
                    break;
                case "economy.snapshot":
                    EconomySnapshotReceived?.Invoke(JsonUtility.FromJson<EconomySnapshotEnvelope>(json).payload);
                    break;
                case "resource.mine.result":
                    MineResultReceived?.Invoke(JsonUtility.FromJson<MineResultEnvelope>(json).payload);
                    break;
                case "craft.result":
                    CraftResultReceived?.Invoke(JsonUtility.FromJson<CraftResultEnvelope>(json).payload);
                    break;
                case "trade.sell.result":
                    SellResultReceived?.Invoke(JsonUtility.FromJson<SellResultEnvelope>(json).payload);
                    break;
                case "equip.result":
                    EquipResultReceived?.Invoke(JsonUtility.FromJson<EquipResultEnvelope>(json).payload);
                    break;
                case "npc.talk.result":
                    NpcTalkResultReceived?.Invoke(JsonUtility.FromJson<NpcTalkResultEnvelope>(json).payload);
                    break;
                case "tutorial.snapshot":
                    TutorialSnapshotReceived?.Invoke(JsonUtility.FromJson<TutorialSnapshotEnvelope>(json).payload);
                    break;
                case "attributes.snapshot":
                    AttributesSnapshotReceived?.Invoke(JsonUtility.FromJson<AttributesSnapshotEnvelope>(json).payload);
                    break;
                case "trade.buy.result":
                    BuyResultReceived?.Invoke(JsonUtility.FromJson<BuyResultEnvelope>(json).payload);
                    break;
                case "item.use.result":
                    UseItemResultReceived?.Invoke(JsonUtility.FromJson<UseItemResultEnvelope>(json).payload);
                    break;
                case "resource.mine.started":
                    MineStartedReceived?.Invoke(JsonUtility.FromJson<MineStartedEnvelope>(json).payload);
                    break;
                case "resource.mine.cancelled":
                    MineCancelledReceived?.Invoke(JsonUtility.FromJson<MineCancelledEnvelope>(json).payload);
                    break;
                case "craft.started":
                    CraftStartedReceived?.Invoke(JsonUtility.FromJson<CraftStartedEnvelope>(json).payload);
                    break;
                case "craft.cancelled":
                    CraftCancelledReceived?.Invoke(JsonUtility.FromJson<CraftCancelledEnvelope>(json).payload);
                    break;
                default:
                    Debug.LogWarning($"[WorldConnection] Tipo de mensagem desconhecido: {peek?.type}");
                    break;
            }
        }

        public async Task CloseAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (webGlSocketId >= 0) WebGlWebSocketBridge.Close(webGlSocketId);
            isClosed = true;
            await Task.CompletedTask;
#else
            await CloseNativeAsync();
#endif
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            isClosed = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            connectCompletion?.TrySetCanceled();
            if (webGlSocketId >= 0) WebGlWebSocketBridge.Close(webGlSocketId);
#else
            cts?.Cancel();
            socket?.Dispose();
            cts?.Dispose();
            // Envios em andamento ainda podem estar no finally/Release. O semáforo é
            // gerenciado e será coletado com esta conexão depois que esses envios acabarem.
#endif
        }

        private static string NowIso() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task ConnectNativeAsync(string uri)
        {
            socket = new ClientWebSocket();
            cts = new CancellationTokenSource();
            await socket.ConnectAsync(new Uri(uri), cts.Token);
            isClosed = false;
            _ = ReceiveLoopAsync(socket, cts.Token);
        }

        private async Task SendNativeAsync(string json)
        {
            if (disposed || socket is not { State: WebSocketState.Open }) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            var acquired = false;
            try
            {
                await sendLock.WaitAsync(cts.Token);
                acquired = true;
                if (socket is { State: WebSocketState.Open })
                {
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Encerramento durante envio.
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WorldConnection] Falha ao enviar mensagem: {ex.Message}");
            }
            finally
            {
                if (acquired) sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket activeSocket, CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (activeSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    using var messageStream = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await activeSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, token);
                            isClosed = true;
                            return;
                        }

                        messageStream.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    incoming.Enqueue(Encoding.UTF8.GetString(messageStream.ToArray()));
                }
            }
            catch (OperationCanceledException)
            {
                // Encerramento esperado via CloseAsync/Dispose.
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WorldConnection] Conexão encerrada: {ex.Message}");
            }
            finally
            {
                isClosed = true;
            }
        }

        private async Task CloseNativeAsync()
        {
            cts?.Cancel();
            if (socket is { State: WebSocketState.Open })
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client-closing", CancellationToken.None);
                }
                catch (Exception)
                {
                    // Já em processo de encerramento; ignora erro de fechamento duplo.
                }
            }
            isClosed = true;
        }
#endif
    }
}
