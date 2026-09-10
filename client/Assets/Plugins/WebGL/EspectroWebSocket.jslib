// Ponte de WebSocket para builds WebGL. System.Net.WebSockets.ClientWebSocket (usado no resto das
// plataformas, ver WorldConnection.cs) não funciona em WebGL — builds rodam sandboxados no
// navegador e não têm acesso a sockets nativos. Este plugin expõe a WebSocket nativa do
// navegador para o C# via P/Invoke, seguindo o mesmo padrão usado por bibliotecas WebGL
// WebSocket estabelecidas para Unity (um único callback despachante, eventos por código).
//
// Eventos despachados para o C# (ver WebGlWebSocketBridge.cs): 0 = open, 1 = message, 2 = close, 3 = error.
//
// Toda função/estado compartilhado entre os símbolos exportados precisa ser declarada como
// dependência `$nome` do próprio Emscripten (via autoAddDeps) — uma função solta fora do objeto
// passado a mergeInto não entra no grafo de dependências e some do bundle final no build de
// produção (confirmado: causava "ReferenceError: espectroWsDispatch is not defined" em runtime,
// mesmo com o build compilando sem erro).

var EspectroWebSocketLib = {
  $espectroWsState: {
    sockets: {},
    nextId: 1,
    callback: null,
  },

  $espectroWsDispatch: function (eventType, id, message) {
    if (!espectroWsState.callback) return;
    var messagePtr = 0;
    if (message !== null) {
      var length = lengthBytesUTF8(message) + 1;
      messagePtr = _malloc(length);
      stringToUTF8(message, messagePtr, length);
    }
    {{{ makeDynCall("viii", "espectroWsState.callback") }}}(eventType, id, messagePtr);
    if (messagePtr !== 0) _free(messagePtr);
  },

  EspectroWsRegisterCallback: function (callbackPtr) {
    espectroWsState.callback = callbackPtr;
  },

  EspectroWsConnect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = espectroWsState.nextId++;

    try {
      var socket = new WebSocket(url);
      socket.binaryType = "arraybuffer";
      espectroWsState.sockets[id] = socket;

      socket.onopen = function () {
        espectroWsDispatch(0, id, null);
      };
      socket.onmessage = function (event) {
        if (typeof event.data === "string") {
          espectroWsDispatch(1, id, event.data);
        }
      };
      socket.onclose = function () {
        espectroWsDispatch(2, id, null);
        delete espectroWsState.sockets[id];
      };
      socket.onerror = function () {
        espectroWsDispatch(3, id, "websocket error");
      };
    } catch (error) {
      espectroWsDispatch(3, id, String(error));
    }

    return id;
  },

  EspectroWsSend: function (id, messagePtr) {
    var socket = espectroWsState.sockets[id];
    if (!socket || socket.readyState !== 1) return;
    socket.send(UTF8ToString(messagePtr));
  },

  EspectroWsClose: function (id) {
    var socket = espectroWsState.sockets[id];
    if (!socket) return;
    socket.close();
  },

  EspectroWsGetState: function (id) {
    var socket = espectroWsState.sockets[id];
    if (!socket) return 3; // CLOSED
    return socket.readyState; // 0 CONNECTING, 1 OPEN, 2 CLOSING, 3 CLOSED
  },
};

autoAddDeps(EspectroWebSocketLib, "$espectroWsState");
autoAddDeps(EspectroWebSocketLib, "$espectroWsDispatch");
mergeInto(LibraryManager.library, EspectroWebSocketLib);
