// WebGL WebSocket bridge — Unity calls these JS functions from C#.
// Mirrors the API expected by NativeWebSocket.cs.
var WebSocketBridge = {
  $sockets: {},
  $nextId: 1,

  WS_Create: function(urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = nextId++;
    sockets[id] = {
      ws: null,
      sendQueue: [],
      state: 3 // CLOSED
    };
    try {
      var ws = new WebSocket(url);
      ws.binaryType = "arraybuffer";
      sockets[id].ws = ws;
      sockets[id].state = 0; // CONNECTING

      ws.onopen = function() {
        sockets[id].state = 1; // OPEN
        Module.dynCall_vi(Module._ws_onopen_cb, id);
      };
      ws.onmessage = function(ev) {
        var data = ev.data;
        if (typeof data === "string") {
          var bytes = new TextEncoder().encode(data);
          var buf = _malloc(bytes.length);
          HEAPU8.set(bytes, buf);
          Module.dynCall_viii(Module._ws_onmessage_cb, id, buf, bytes.length);
          _free(buf);
        }
      };
      ws.onerror = function() {
        sockets[id].state = 3;
        Module.dynCall_vi(Module._ws_onerror_cb, id);
      };
      ws.onclose = function(ev) {
        sockets[id].state = 3;
        Module.dynCall_vii(Module._ws_onclose_cb, id, ev.code);
      };
    } catch(e) {
      console.error("WebSocket create error:", e);
    }
    return id;
  },

  WS_Send: function(id, msgPtr, len) {
    var s = sockets[id];
    if (!s || !s.ws || s.ws.readyState !== 1) return;
    var bytes = HEAPU8.subarray(msgPtr, msgPtr + len);
    s.ws.send(new TextDecoder().decode(bytes));
  },

  WS_Close: function(id, code) {
    var s = sockets[id];
    if (s && s.ws) { try { s.ws.close(code); } catch(e) {} }
  },

  WS_State: function(id) {
    var s = sockets[id];
    return s ? s.state : 3;
  },
};

autoAddDeps(WebSocketBridge, "$sockets");
autoAddDeps(WebSocketBridge, "$nextId");
mergeInto(LibraryManager.library, WebSocketBridge);
