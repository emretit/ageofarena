using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built uGUI HUD. Shows a resource bar at the top, unit selection info
/// at the bottom-left, and a building panel at the bottom-center when a building
/// is selected (with training progress bar and hotkey hints).
/// </summary>
public class HUD : MonoBehaviour
{
    Font _font;
    ResourceManager _res;
    Transform _canvasRoot;
    bool _gameOverShown;

    // Top bar
    Text _foodText, _woodText, _goldText, _stoneText, _popText;
    // Bottom-left
    Text _selText;
    // Building panel
    RectTransform _bldPanel;
    Text _bldNameText, _bldTrainHints, _bldQueueText;
    RectTransform _progressFill;
    Image _progressFillImg;

    public void Init(ResourceManager res)
    {
        _res = res;
        _font = ResolveFont();
        BuildCanvas();
        Refresh();
        _res.OnChanged += Refresh;
    }

    void OnDestroy()
    {
        if (_res != null) _res.OnChanged -= Refresh;
    }

    static Font ResolveFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }

    void BuildCanvas()
    {
        var canvasGo = new GameObject("HUDCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvasRoot = canvasGo.transform;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Top bar
        var bar = NewRect("TopBar", canvasGo.transform);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot     = new Vector2(0.5f, 1);
        bar.sizeDelta = new Vector2(0, 56);
        bar.anchoredPosition = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        float x = 24f;
        _foodText  = AddEntry(bar, ref x, Prims.Hex(0xd64545), "Food");
        _woodText  = AddEntry(bar, ref x, Prims.Hex(0x8a5a2b), "Wood");
        _goldText  = AddEntry(bar, ref x, Prims.Hex(0xf2c14e), "Gold");
        _stoneText = AddEntry(bar, ref x, Prims.Hex(0xb9b9b9), "Stone");
        _popText   = AddEntry(bar, ref x, Prims.Hex(0x6fa8dc), "Pop");

        // Selection count, bottom-left
        var sel = NewRect("SelInfo", canvasGo.transform);
        sel.anchorMin = sel.anchorMax = sel.pivot = new Vector2(0, 0);
        sel.sizeDelta = new Vector2(300, 30);
        sel.anchoredPosition = new Vector2(16, 16);
        _selText = AddText(sel, "", TextAnchor.LowerLeft);

        // Building panel, bottom-center
        _bldPanel = NewRect("BuildingPanel", canvasGo.transform);
        _bldPanel.anchorMin = new Vector2(0.5f, 0);
        _bldPanel.anchorMax = new Vector2(0.5f, 0);
        _bldPanel.pivot     = new Vector2(0.5f, 0);
        _bldPanel.sizeDelta = new Vector2(340, 120);
        _bldPanel.anchoredPosition = new Vector2(0, 16);
        var panelImg = _bldPanel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.55f);

        // Building name
        var nameRect = NewRect("BldName", _bldPanel);
        nameRect.anchorMin = new Vector2(0, 1); nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot     = new Vector2(0.5f, 1);
        nameRect.sizeDelta = new Vector2(0, 28);
        nameRect.anchoredPosition = new Vector2(0, -4);
        _bldNameText = AddText(nameRect, "", TextAnchor.MiddleCenter);
        _bldNameText.fontSize = 18;
        _bldNameText.fontStyle = FontStyle.Bold;

        // Train hotkey hints
        var hintsRect = NewRect("BldHints", _bldPanel);
        hintsRect.anchorMin = new Vector2(0, 1); hintsRect.anchorMax = new Vector2(1, 1);
        hintsRect.pivot     = new Vector2(0.5f, 1);
        hintsRect.sizeDelta = new Vector2(0, 24);
        hintsRect.anchoredPosition = new Vector2(0, -34);
        _bldTrainHints = AddText(hintsRect, "", TextAnchor.MiddleCenter);
        _bldTrainHints.fontSize = 14;
        _bldTrainHints.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Progress bar background
        var barBg = NewRect("ProgressBg", _bldPanel);
        barBg.anchorMin = new Vector2(0.05f, 0); barBg.anchorMax = new Vector2(0.95f, 0);
        barBg.pivot     = new Vector2(0.5f, 0);
        barBg.sizeDelta = new Vector2(0, 18);
        barBg.anchoredPosition = new Vector2(0, 14);
        var bgImg = barBg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Progress fill
        _progressFill = NewRect("ProgressFill", barBg);
        _progressFill.anchorMin = new Vector2(0, 0); _progressFill.anchorMax = new Vector2(0, 1);
        _progressFill.pivot     = new Vector2(0, 0.5f);
        _progressFill.sizeDelta = new Vector2(0, 0);
        _progressFill.anchoredPosition = Vector2.zero;
        _progressFillImg = _progressFill.gameObject.AddComponent<Image>();
        _progressFillImg.color = Prims.Hex(0x4caf50);

        // Queue count text
        var queueRect = NewRect("QueueCount", _bldPanel);
        queueRect.anchorMin = new Vector2(0, 0); queueRect.anchorMax = new Vector2(1, 0);
        queueRect.pivot     = new Vector2(0.5f, 0);
        queueRect.sizeDelta = new Vector2(0, 16);
        queueRect.anchoredPosition = new Vector2(0, 36);
        _bldQueueText = AddText(queueRect, "", TextAnchor.MiddleCenter);
        _bldQueueText.fontSize = 13;
        _bldQueueText.color = new Color(0.7f, 0.9f, 1f, 1f);

        _bldPanel.gameObject.SetActive(false);
    }

    Text AddEntry(RectTransform bar, ref float x, Color swatch, string label)
    {
        var s = NewRect(label + "Swatch", bar);
        s.anchorMin = s.anchorMax = s.pivot = new Vector2(0, 0.5f);
        s.sizeDelta = new Vector2(22, 22);
        s.anchoredPosition = new Vector2(x, 0);
        s.gameObject.AddComponent<Image>().color = swatch;
        x += 30f;

        var v = NewRect(label + "Text", bar);
        v.anchorMin = v.anchorMax = v.pivot = new Vector2(0, 0.5f);
        v.sizeDelta = new Vector2(70, 30);
        v.anchoredPosition = new Vector2(x, 0);
        var t = AddText(v, "0", TextAnchor.MiddleLeft);
        x += 88f;
        return t;
    }

    Text AddText(RectTransform parent, string content, TextAnchor anchor)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = 20;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = content;
        return t;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    void Refresh()
    {
        if (_res == null) return;
        if (_foodText)  _foodText.text  = _res.food.ToString();
        if (_woodText)  _woodText.text  = _res.wood.ToString();
        if (_goldText)  _goldText.text  = _res.gold.ToString();
        if (_stoneText) _stoneText.text = _res.stone.ToString();
        if (_popText)
        {
            bool full = _res.popCap > 0 && _res.pop >= _res.popCap;
            _popText.text  = full ? _res.pop + "/" + _res.popCap + " DOLU" : _res.pop + "/" + _res.popCap;
            _popText.color = full ? Prims.Hex(0xff5555) : Color.white;
        }
    }

    /// <summary>Full-screen victory/defeat overlay shown by <see cref="MatchSystem"/>.</summary>
    public void ShowGameOver(bool playerWon)
    {
        if (_gameOverShown || _canvasRoot == null) return;
        _gameOverShown = true;

        var overlay = NewRect("GameOverOverlay", _canvasRoot);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        var titleRect = NewRect("GameOverTitle", overlay);
        titleRect.anchorMin = new Vector2(0, 0.5f); titleRect.anchorMax = new Vector2(1, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(0, 120);
        titleRect.anchoredPosition = new Vector2(0, 30);
        var title = AddText(titleRect, playerWon ? "ZAFER!" : "YENILDIN", TextAnchor.MiddleCenter);
        title.fontSize = 80;
        title.fontStyle = FontStyle.Bold;
        title.color = playerWon ? Prims.Hex(0x4caf50) : Prims.Hex(0xff5555);

        var hintRect = NewRect("GameOverHint", overlay);
        hintRect.anchorMin = new Vector2(0, 0.5f); hintRect.anchorMax = new Vector2(1, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(0, 40);
        hintRect.anchoredPosition = new Vector2(0, -60);
        var hint = AddText(hintRect, "Yeniden baslatmak icin R", TextAnchor.MiddleCenter);
        hint.fontSize = 28;
        hint.color = new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Selection text
        if (_selText != null && gm.selection != null)
        {
            int n = gm.selection.Selected.Count;
            _selText.text = n > 0 ? n + " birim seçili" : "";
        }

        // Building panel
        var b = gm.selectedBuilding;
        if (_bldPanel == null) return;

        if (b == null)
        {
            _bldPanel.gameObject.SetActive(false);
            return;
        }

        _bldPanel.gameObject.SetActive(true);
        _bldNameText.text = b.type.ToString();

        // Hotkey hints
        var trainables = b.GetTrainables();
        if (trainables.Length > 0)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var tr in trainables)
                sb.Append($"[{tr.hotkey}] {tr.unitType}  {CostStr(tr)}   ");
            _bldTrainHints.text = sb.ToString().TrimEnd();
        }
        else
        {
            _bldTrainHints.text = "";
        }

        // Progress bar
        if (gm.trainingQueue != null)
        {
            float prog  = gm.trainingQueue.GetProgress(b);
            int qCount  = gm.trainingQueue.GetQueueCount(b);

            if (prog >= 0f)
            {
                // Stretch the fill rect by modifying sizeDelta.x using parent width.
                float parentW = (_progressFill.parent as RectTransform)?.rect.width ?? 300f;
                _progressFill.sizeDelta = new Vector2(parentW * prog, 0);
                _bldQueueText.text = qCount > 1 ? $"Kuyruk: {qCount}" : "";
            }
            else
            {
                _progressFill.sizeDelta = Vector2.zero;
                _bldQueueText.text = "";
            }
        }
    }

    static string CostStr(UnitTrainable tr)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (tr.food > 0) parts.Add($"{tr.food}Y"); // Yiyecek
        if (tr.wood > 0) parts.Add($"{tr.wood}O"); // Odun
        if (tr.gold > 0) parts.Add($"{tr.gold}A"); // Altın
        return parts.Count > 0 ? "(" + string.Join(" ", parts) + ")" : "";
    }
}
