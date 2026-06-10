using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MP-3: Online lobi ekrani. Oda olustur veya koda ile katil,
/// hazir/bekliyor listesi, host "Oyunu Baslat" butonu.
/// TransportLayer ile konusur; GameBootstrap.Restart game_start alinca tetiklenir.
/// </summary>
public class LobbyScreen : MonoBehaviour
{
    // ── Renkler ───────────────────────────────────────────────────────────────
    static readonly Color BgCol      = new Color(0.04f, 0.06f, 0.12f, 0.97f);
    static readonly Color PanelCol   = new Color(0.08f, 0.12f, 0.22f, 1f);
    static readonly Color Gold       = new Color(0.95f, 0.82f, 0.42f);
    static readonly Color White      = new Color(0.95f, 0.96f, 1.00f);
    static readonly Color Dim        = new Color(0.55f, 0.60f, 0.70f);
    static readonly Color GreenCol   = new Color(0.16f, 0.40f, 0.18f, 1f);
    static readonly Color RedCol     = new Color(0.38f, 0.10f, 0.10f, 1f);
    static readonly Color ReadyGreen = new Color(0.30f, 0.85f, 0.38f);
    static readonly Color WaitYellow = new Color(0.95f, 0.80f, 0.20f);

    // ── UI referanslari ───────────────────────────────────────────────────────
    Canvas      _canvas;
    InputField  _urlField;
    InputField  _nameField;
    InputField  _codeField;
    Text        _statusText;
    Text        _roomCodeText;
    RectTransform _playerListPanel;
    Button      _readyBtn;
    Button      _startBtn;

    // ── Durum ─────────────────────────────────────────────────────────────────
    TransportLayer _transport;
    bool _isHost;
    bool _isReady;
    readonly List<ServerPlayer> _players = new();

    // ── Acik/Kapali ──────────────────────────────────────────────────────────
    public void Show()
    {
        if (_canvas == null) Build();
        _canvas.gameObject.SetActive(true);

        if (_transport == null)
        {
            _transport = GetComponent<TransportLayer>();
            if (_transport == null)
                _transport = gameObject.AddComponent<TransportLayer>();
        }

        _transport.OnConnected       += HandleConnected;
        _transport.OnDisconnected    += HandleDisconnected;
        _transport.OnRawMessage      += HandleRawMessage;
    }

    /// <summary>WorldRoot tarafindan gm.transport atanir; Show()'dan once cagirilmali.</summary>
    public void SetTransport(TransportLayer t) => _transport = t;

    public void Hide()
    {
        if (_transport != null)
        {
            _transport.OnConnected    -= HandleConnected;
            _transport.OnDisconnected -= HandleDisconnected;
            _transport.OnRawMessage   -= HandleRawMessage;
        }
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    void OnDestroy() => Hide();

    // ── UI insasi ─────────────────────────────────────────────────────────────
    void Build()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("LobbyES");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var cgo = new GameObject("LobbyCanvas");
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 8000;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        // Arka plan
        var bg = NewRect("Bg", cgo.transform);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
        bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = BgCol;

        AddLabel(bg, "Age of Arena — Cok Oyunculu Lobi", 0, 460, 38, Gold, FontStyle.Bold);
        AddLine(bg, 0, 430, 900, 2);

        // ── Sol panel: Baglanti ───────────────────────────────────────────────
        var leftPanel = NewRect("LeftPanel", bg);
        leftPanel.sizeDelta        = new Vector2(420, 500);
        leftPanel.anchoredPosition = new Vector2(-250, 20);
        leftPanel.gameObject.AddComponent<Image>().color = PanelCol;

        AddLabel(leftPanel, "Sunucu Adresi", -160, 220, 16, Dim, FontStyle.Normal);
        _urlField = AddInputField(leftPanel, "ws://localhost:2567", -5, 188, 380, 34);

        AddLabel(leftPanel, "Oyuncu Adiniz", -160, 148, 16, Dim, FontStyle.Normal);
        _nameField = AddInputField(leftPanel, "Oyuncu1", -5, 116, 380, 34);

        AddLabel(leftPanel, "Oda Kodu (katilmak icin)", -160, 76, 16, Dim, FontStyle.Normal);
        _codeField = AddInputField(leftPanel, "XXXXX", -5, 44, 380, 34);
        _codeField.characterLimit = 5;

        // Oda olustur
        var createBtn = AddBtn(leftPanel, "Oda Olustur", -5, -20, 380, 44, GreenCol, () =>
        {
            string url  = _urlField.text.Trim();
            string name = _nameField.text.Trim();
            if (string.IsNullOrEmpty(name)) name = "Oyuncu1";
            SetStatus("Baglaniliyor...");
            _transport.Connect(url);
            _transport.CreateRoom(name);
            _isHost = true;
        });

        // Oda kodla katil
        AddBtn(leftPanel, "Koda Ile Katil", -5, -80, 380, 44, new Color(0.18f, 0.22f, 0.38f, 1f), () =>
        {
            string url  = _urlField.text.Trim();
            string name = _nameField.text.Trim();
            string code = _codeField.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(name)) name = "Oyuncu1";
            if (string.IsNullOrEmpty(code)) { SetStatus("Oda kodu girin."); return; }
            SetStatus("Baglaniliyor...");
            _transport.Connect(url);
            _transport.JoinRoom(code, name);
            _isHost = false;
        });

        // ── Sag panel: Oda bilgisi + oyuncular ───────────────────────────────
        var rightPanel = NewRect("RightPanel", bg);
        rightPanel.sizeDelta        = new Vector2(420, 500);
        rightPanel.anchoredPosition = new Vector2(250, 20);
        rightPanel.gameObject.AddComponent<Image>().color = PanelCol;

        AddLabel(rightPanel, "Oda Kodu:", -160, 220, 16, Dim, FontStyle.Normal);
        _roomCodeText = AddLabel(rightPanel, "---", 60, 220, 28, Gold, FontStyle.Bold);

        AddLine(rightPanel, 0, 196, 380, 1);
        AddLabel(rightPanel, "Oyuncular", -155, 174, 18, White, FontStyle.Bold);

        _playerListPanel = NewRect("PlayerList", rightPanel);
        _playerListPanel.sizeDelta        = new Vector2(380, 200);
        _playerListPanel.anchoredPosition = new Vector2(0, 50);

        // Hazir + Baslat butonlari
        _readyBtn = AddBtn(rightPanel, "Hazir Degil", 0, -170, 370, 46, RedCol, OnReadyClicked);
        _startBtn = AddBtn(rightPanel, "Oyunu Baslat", 0, -228, 370, 46, GreenCol, OnStartClicked);
        _startBtn.interactable = false;

        // Alt durum satiri
        _statusText = AddLabel(bg, "Sunucu adresi girin ve oda olusturun.", 0, -440, 18, Dim, FontStyle.Normal);
        _statusText.alignment = TextAnchor.MiddleCenter;

        // Kapat
        AddBtn(bg, "X Cik", 420, -440, 100, 36, RedCol, () =>
        {
            Hide();
            Time.timeScale = 1f;
        });
    }

    // ── Olay isleyicileri ─────────────────────────────────────────────────────
    void HandleConnected()
    {
        SetStatus("Baglandi. Oda bekleniyor...");
    }

    void HandleDisconnected(string reason)
    {
        SetStatus($"Baglanti kesildi: {reason}");
    }

    void HandleRawMessage(ServerMsg msg)
    {
        switch (msg.type)
        {
            case "room_created":
                _roomCodeText.text = msg.roomCode;
                SetStatus($"Oda olusturuldu: {msg.roomCode} — Oyuncular katilacak bekleniyor.");
                break;

            case "room_joined":
                _roomCodeText.text = msg.roomCode;
                SetStatus($"Odaya katilindi: {msg.roomCode}");
                RefreshPlayerList(msg.playerList ?? msg.players);
                break;

            case "player_joined":
                SetStatus($"{msg.name} odaya katildi.");
                RefreshPlayerList(msg.playerList ?? msg.players);
                break;

            case "player_ready":
                RefreshPlayerList(msg.playerList ?? msg.players);
                break;

            case "player_left":
                SetStatus($"Oyuncu ayriliyor.");
                RefreshPlayerList(msg.playerList ?? msg.players);
                break;

            case "error":
                SetStatus($"Hata: {msg.message}");
                break;

            case "game_start":
                SetStatus("Oyun basliyor...");
                // TransportLayer zaten GameBootstrap.Restart(seed) cagiracak
                Hide();
                break;
        }
    }

    void OnReadyClicked()
    {
        _isReady = !_isReady;
        if (_isReady)
        {
            _transport.SendReady();
            SetBtnLabel(_readyBtn, "Hazir");
            SetBtnColor(_readyBtn, GreenCol);
        }
        else
        {
            SetBtnLabel(_readyBtn, "Hazir Degil");
            SetBtnColor(_readyBtn, RedCol);
        }
    }

    void OnStartClicked()
    {
        if (!_isHost) return;
        // Host "Hazir" butonuna basarak game_start tetikler (sunucu tarafli)
        if (!_isReady) OnReadyClicked();
    }

    // ── Oyuncu listesi ────────────────────────────────────────────────────────
    void RefreshPlayerList(List<ServerPlayer> list)
    {
        if (list == null) return;
        _players.Clear();
        _players.AddRange(list);

        foreach (Transform child in _playerListPanel)
            Destroy(child.gameObject);

        bool allReady = _players.Count >= 2;
        for (int i = 0; i < _players.Count; i++)
        {
            var p = _players[i];
            var row = NewRect($"Row{i}", _playerListPanel);
            row.sizeDelta        = new Vector2(360, 36);
            row.anchoredPosition = new Vector2(0, 90 - i * 42f);

            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.color = new Color(0.06f, 0.09f, 0.18f, 1f);

            var nameLbl = AddLabel(row, $"Takim {p.team + 1}  {p.name}", -135, 0, 16, White, FontStyle.Normal);
            nameLbl.alignment = TextAnchor.MiddleLeft;

            bool ready = p.ready;
            if (!ready) allReady = false;
            AddLabel(row, ready ? "Hazir" : "Bekliyor", 140, 0, 15, ready ? ReadyGreen : WaitYellow, FontStyle.Bold);
        }

        if (_isHost)
            _startBtn.interactable = allReady;
    }

    // ── UI yardimcilari ───────────────────────────────────────────────────────
    void SetStatus(string msg)
    {
        if (_statusText != null) _statusText.text = msg;
    }

    static void SetBtnLabel(Button btn, string label)
    {
        var t = btn.GetComponentInChildren<Text>();
        if (t != null) t.text = label;
    }

    static void SetBtnColor(Button btn, Color col)
    {
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = col;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Text AddLabel(RectTransform parent, string text, float x, float y,
        int size, Color col, FontStyle style)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(360, 34);
        rt.anchoredPosition = new Vector2(x, y);
        var t = go.AddComponent<Text>();
        t.text      = text;
        t.fontSize  = size;
        t.color     = col;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        t.font      = UiFonts.Default;
        return t;
    }

    static void AddLine(RectTransform parent, float x, float y, float w, float h)
    {
        var go = NewRect("Line", parent);
        go.sizeDelta        = new Vector2(w, h);
        go.anchoredPosition = new Vector2(x, y);
        go.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.82f, 0.42f, 0.3f);
    }

    static Button AddBtn(RectTransform parent, string label, float x, float y,
        float w, float h, Color col, System.Action onClick)
    {
        var go = NewRect("Btn_" + label, parent);
        go.sizeDelta        = new Vector2(w, h);
        go.anchoredPosition = new Vector2(x, y);
        var img = go.gameObject.AddComponent<Image>();
        img.color = col;
        var btn = go.gameObject.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
        cols.pressedColor     = new Color(1.5f, 1.5f, 1.5f);
        cols.fadeDuration     = 0.07f;
        btn.colors = cols;
        var lbl = AddLabel(go, label, 0, 0, 18, White, FontStyle.Bold);
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.GetComponent<RectTransform>().sizeDelta = new Vector2(w - 8, h);
        btn.onClick.AddListener(() => onClick());
        return btn;
    }

    static InputField AddInputField(RectTransform parent, string placeholder, float x, float y,
        float w, float h)
    {
        var go = NewRect("Input", parent);
        go.sizeDelta        = new Vector2(w, h);
        go.anchoredPosition = new Vector2(x, y);
        go.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.14f, 1f);

        var field = go.gameObject.AddComponent<InputField>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6, 2); textRt.offsetMax = new Vector2(-6, -2);
        var t = textGo.AddComponent<Text>();
        t.font      = UiFonts.Default;
        t.fontSize  = 16;
        t.color     = new Color(0.9f, 0.92f, 1f);
        t.alignment = TextAnchor.MiddleLeft;
        field.textComponent = t;

        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(go, false);
        var phRt = phGo.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(6, 2); phRt.offsetMax = new Vector2(-6, -2);
        var ph = phGo.AddComponent<Text>();
        ph.font      = UiFonts.Default;
        ph.fontSize  = 16;
        ph.color     = new Color(0.45f, 0.50f, 0.60f);
        ph.fontStyle = FontStyle.Italic;
        ph.alignment = TextAnchor.MiddleLeft;
        ph.text      = placeholder;
        field.placeholder = ph;

        return field;
    }
}
