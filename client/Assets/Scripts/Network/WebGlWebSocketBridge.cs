#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AOT;

namespace Espectro.Network
{
    // Ponte P/Invoke para Assets/Plugins/WebGL/EspectroWebSocket.jslib. Só compila em builds
    // WebGL reais (nunca no Editor, mesmo com WebGL selecionado como plataforma ativa) — o
    // Editor sempre usa System.Net.WebSockets.ClientWebSocket normalmente.
    internal static class WebGlWebSocketBridge
    {
        [DllImport("__Internal")]
        private static extern void EspectroWsRegisterCallback(IntPtr callback);

        [DllImport("__Internal")]
        private static extern int EspectroWsConnect(string url);

        [DllImport("__Internal")]
        private static extern void EspectroWsSend(int id, string message);

        [DllImport("__Internal")]
        private static extern void EspectroWsClose(int id);

        public delegate void OpenHandler();
        public delegate void MessageHandler(string json);
        public delegate void CloseHandler();
        public delegate void ErrorHandler(string message);

        private sealed class Handlers
        {
            public OpenHandler OnOpen;
            public MessageHandler OnMessage;
            public CloseHandler OnClose;
            public ErrorHandler OnError;
        }

        private delegate void DispatchDelegate(int eventType, int socketId, IntPtr messagePtr);

        private static readonly ConcurrentDictionary<int, Handlers> ActiveSockets = new();
        // Mantém uma referência viva ao delegate: se ele for coletado pelo GC, o ponteiro de
        // função registrado no JS vira inválido e a próxima chamada derruba o runtime.
        private static readonly DispatchDelegate DispatchRef = Dispatch;
        private static bool callbackRegistered;

        public static int Connect(string url, OpenHandler onOpen, MessageHandler onMessage, CloseHandler onClose, ErrorHandler onError)
        {
            EnsureCallbackRegistered();
            var id = EspectroWsConnect(url);
            ActiveSockets[id] = new Handlers { OnOpen = onOpen, OnMessage = onMessage, OnClose = onClose, OnError = onError };
            return id;
        }

        public static void Send(int id, string json) => EspectroWsSend(id, json);

        public static void Close(int id) => EspectroWsClose(id);

        private static void EnsureCallbackRegistered()
        {
            if (callbackRegistered) return;
            callbackRegistered = true;
            EspectroWsRegisterCallback(Marshal.GetFunctionPointerForDelegate(DispatchRef));
        }

        [MonoPInvokeCallback(typeof(DispatchDelegate))]
        private static void Dispatch(int eventType, int socketId, IntPtr messagePtr)
        {
            if (!ActiveSockets.TryGetValue(socketId, out var handlers)) return;
            switch (eventType)
            {
                case 0:
                    handlers.OnOpen?.Invoke();
                    break;
                case 1:
                    handlers.OnMessage?.Invoke(Marshal.PtrToStringUTF8(messagePtr));
                    break;
                case 2:
                    ActiveSockets.TryRemove(socketId, out _);
                    handlers.OnClose?.Invoke();
                    break;
                case 3:
                    handlers.OnError?.Invoke(Marshal.PtrToStringUTF8(messagePtr));
                    break;
            }
        }
    }
}
#endif
