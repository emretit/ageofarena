using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// N12.edit: Runtime scenario/map editor.
/// Toggle with 'E' key (or HUD pause-menu button). An overlay canvas appears over the
/// arena; the player can place/delete units, buildings, and resource nodes, configure
/// per-team starting resources/civ, author triggers, then save a scenario and playtest.
///
/// Placement flow: click entity in the left palette → click on the terrain → entity spawns.
/// Delete flow: enable delete mode (🗑 button) → click any entity on the map.
/// Playtest flow: "Test Et" serialises the current scene state and restarts via PendingLoad.
/// Save/load: scenario stored in PlayerPrefs as JSON (key "AoA_Scenario_0").
/// </summary>
public class ScenarioEditor : MonoBehaviour
{
    const string ScenarioKey = "AoA_Scenario_0";

    static readonly Color PanelBg  = new Color(0.06f, 0.08f, 0.14f, 0.92f);
    static readonly Color BtnColor = new Color(0.14f, 0.20f, 0.34f, 1f);
    static readonly Color BtnSel   = new Color(0.22f, 0.42f, 0.68f, 1f);
    static readonly Color BtnDel   = new Color(0.45f, 0.10f, 0.10f, 1f);
    static readonly Color TextCol  = new Color(0.92f, 0.94f, 1.00f, 1f);
    static readonly Color LabelCol = new Color(0.70f, 0.76f, 0.60f, 1f);

    // ── State ─────────────────────────────────────────────────────────────────

    bool _open;
    bool _deleteMode;
    int  _selectedTeam;    // team to assign to newly placed entities
    int  _selectedCategory; // 0=Units 1=Buildings 2=Resources

    static readonly string[] Categories = { "Birimler", "Binalar", "Kaynaklar" };

    // palette items (display-name, action-key)
    static readonly (string label, System.Action<Vector3, Color> spawn)[] UnitItems =
    {
        ("Köylü",   (p,c) => Reg(UnitFactory.Villager(Root(), p, c))),
        ("Piyade",  (p,c) => Reg(UnitFactory.Militia(Root(), p, c))),
        ("Okçu",    (p,c) => Reg(UnitFactory.Archer(Root(), p, c))),
        ("Süvari",  (p,c) => Reg(UnitFactory.Cavalry(Root(), p, c))),
        ("Kargıcı", (p,c) => Reg(UnitFactory.Spearman(Root(), p, c))),
        ("Trebuchet",(p,c) => Reg(UnitFactory.Trebuchet(Root(), p, c))),
        ("Ram",     (p,c) => Reg(UnitFactory.Ram(Root(), p, c))),
        ("Mangonel",(p,c) => Reg(UnitFactory.Mangonel(Root(), p, c))),
    };

    static readonly (string label, System.Action<Vector3, Color, int> spawn)[] BuildingItems =
    {
        ("Şehir Merkezi", (p,c,t) => RegBGo(BuildingFactory.TownCenter(Root(), p, c), t)),
        ("Kışla",         (p,c,t) => RegBGo(BuildingFactory.Barracks(Root(), p, c), t)),
        ("Maden Kampı",   (p,c,t) => RegBGo(BuildingFactory.MiningCamp(Root(), p, c), t)),
        ("Ev",            (p,c,t) => RegBGo(BuildingFactory.House(Root(), p, c), t)),
        ("Kule",          (p,c,t) => RegBGo(BuildingFactory.WatchTower(Root(), p, c), t)),
        ("Kale",          (p,c,t) => RegBGo(BuildingFactory.Castle(Root(), p, c), t)),
    };

    static readonly (string label, System.Action<Vector3> spawn)[] ResourceItems =
    {
        ("Altın Madeni",  p => RegN(ResourceFactory.GoldMine(Root(), p))),
        ("Taş Madeni",    p => RegN(ResourceFactory.StoneMine(Root(), p))),
        ("Orman Ağacı",   p => RegN(ResourceFactory.Tree(Root(), p, ResourceFactory.TreeKind.Broadleaf))),
        ("Böğürtlen",     p => RegN(ResourceFactory.BerryBush(Root(), p))),
        ("Balık Göleti",  p => RegN(ResourceFactory.FishPond(Root(), p))),
    };

    int  _selectedItem = -1;
    Text _statusText;
    Canvas _canvas;

    // Button refs for selection highlight
    readonly List<(Button btn, int idx)> _paletteButtons = new();

    // ── Registration helpers ──────────────────────────────────────────────────

    static Transform Root()
    {
        var gm = GameManager.Instance;
        return gm != null ? gm.transform : null;
    }

    static void Reg(UnitEntity e)
    {
        if (e == null) return;
        GameManager.Instance?.RegisterUnit(e);
    }

    static void RegB(BuildingEntity b)
    {
        if (b == null) return;
        GameManager.Instance?.RegisterBuilding(b);
    }

    // BuildingFactory returns GameObject; extract BuildingEntity and set team.
    static void RegBGo(GameObject go, int teamId)
    {
        if (go == null) return;
        var b = go.GetComponent<BuildingEntity>();
        if (b == null) return;
        b.teamId = teamId;
        GameManager.Instance?.RegisterBuilding(b);
    }

    static void RegN(ResourceNode n)
    {
        if (n == null) return;
        GameManager.Instance?.RegisterNode(n);
    }

    // ── Toggle ────────────────────────────────────────────────────────────────

    public bool IsOpen => _open;

    public void Open()
    {
        _open = true;
        BuildUI();
        _canvas.gameObject.SetActive(true);
        Time.timeScale = 0f; // freeze while editing
    }

    public void Close()
    {
        _open = false;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && !_open) Open();
        if (!_open) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

        // Mouse placement on Left-Click
        if (Input.GetMouseButtonDown(0) && _selectedItem >= 0)
        {
            if (TryGetGroundPoint(out var pos))
            {
                PlaceSelected(pos);
            }
        }
        // Delete on Left-Click in delete mode
        else if (Input.GetMouseButtonDown(0) && _deleteMode)
        {
            TryDeleteAt();
        }
    }

    // ── Placement & delete ────────────────────────────────────────────────────

    void PlaceSelected(Vector3 pos)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var col = TeamPalette.For(_selectedTeam);
        pos.y = 0f;

        if (_selectedCategory == 0 && _selectedItem < UnitItems.Length)
        {
            UnitItems[_selectedItem].spawn(pos, col);
            // Most factories set teamId=0 by default; fix to selected team.
            if (gm.units.Count > 0)
            {
                var u = gm.units[gm.units.Count - 1];
                if (u != null) { u.teamId = _selectedTeam; }
            }
        }
        else if (_selectedCategory == 1 && _selectedItem < BuildingItems.Length)
        {
            BuildingItems[_selectedItem].spawn(pos, col, _selectedTeam);
        }
        else if (_selectedCategory == 2 && _selectedItem < ResourceItems.Length)
        {
            ResourceItems[_selectedItem].spawn(pos);
        }
        SetStatus($"Yerleştirildi: {GetItemName(_selectedCategory, _selectedItem)} (T{_selectedTeam})");
    }

    void TryDeleteAt()
    {
        if (!TryGetWorldHit(out var hit)) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        var go = hit.collider.gameObject;

        var unit = go.GetComponentInParent<UnitEntity>();
        if (unit != null) { gm.units.Remove(unit); Object.Destroy(unit.gameObject); SetStatus("Birim silindi."); return; }

        var bldg = go.GetComponentInParent<BuildingEntity>();
        if (bldg != null) { gm.buildings.Remove(bldg); Object.Destroy(bldg.gameObject); SetStatus("Bina silindi."); return; }

        var node = go.GetComponentInParent<ResourceNode>();
        if (node != null) { gm.nodes.Remove(node); Object.Destroy(node.gameObject); SetStatus("Kaynak silindi."); return; }
    }

    static bool TryGetGroundPoint(out Vector3 pos)
    {
        pos = Vector3.zero;
        var cam = Camera.main;
        if (cam == null) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float enter)) return false;
        pos = ray.GetPoint(enter);
        return true;
    }

    static bool TryGetWorldHit(out RaycastHit hit)
    {
        hit = default;
        var cam = Camera.main;
        if (cam == null) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit, 200f);
    }

    // ── Save / Load / Playtest ────────────────────────────────────────────────

    void SaveScenario()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var wr = Object.FindAnyObjectByType<WorldRoot>();
        var data = new SaveSystem.SaveData { mapSeed = wr != null ? wr.mapSeed : 0 };

        data.teams = new SaveSystem.TeamSnap[gm.TeamCount];
        for (int t = 0; t < gm.TeamCount; t++)
        {
            var r = gm.teamRes[t];
            data.teams[t] = new SaveSystem.TeamSnap
            {
                food = r.food, wood = r.wood, gold = r.gold, stone = r.stone,
                ageIndex = (int)gm.teamTech[t].age,
                civ = (int)gm.teamCivs[t],
            };
            foreach (TechType ty in System.Enum.GetValues(typeof(TechType)))
                if (gm.teamTech[t].Has(ty)) data.teams[t].techs.Add((int)ty);
        }
        foreach (var u in gm.units)
        {
            if (u == null) continue;
            data.units.Add(new SaveSystem.UnitSnap {
                type = (int)u.type, teamId = u.teamId,
                x = u.transform.position.x, z = u.transform.position.z, hp = u.maxHp,
            });
        }
        foreach (var b in gm.buildings)
        {
            if (b == null) continue;
            data.buildings.Add(new SaveSystem.BuildingSnap {
                type = (int)b.type, teamId = b.teamId,
                x = b.transform.position.x, z = b.transform.position.z, hp = b.maxHp,
            });
        }
        if (gm.triggers != null) data.triggers = gm.triggers.Snapshot();

        PlayerPrefs.SetString(ScenarioKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        SetStatus($"Senaryo kaydedildi ({data.units.Count}b+{data.buildings.Count}bi).");
    }

    void LoadScenario()
    {
        string json = PlayerPrefs.GetString(ScenarioKey, "");
        if (string.IsNullOrEmpty(json)) { SetStatus("Kayıtlı senaryo bulunamadı."); return; }
        var data = JsonUtility.FromJson<SaveSystem.SaveData>(json);
        if (data == null) { SetStatus("Senaryo okunamadı."); return; }
        GameBootstrap.PendingLoad = data;
        Close();
        GameBootstrap.Restart(data.mapSeed);
    }

    void PlaytestScenario()
    {
        SaveScenario(); // snapshot current state
        string json = PlayerPrefs.GetString(ScenarioKey, "");
        var data = JsonUtility.FromJson<SaveSystem.SaveData>(json);
        if (data == null) return;
        GameBootstrap.PendingLoad = data;
        Close();
        GameBootstrap.Restart(data.mapSeed);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        if (_canvas != null) return; // already built

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EditorEventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var cgo = new GameObject("ScenarioEditorCanvas");
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 6000;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        // ── Top bar ────────────────────────────────────────────────────────────
        var bar = MakeRect("TopBar", cgo.transform);
        bar.anchorMin = new Vector2(0,1); bar.anchorMax = new Vector2(1,1);
        bar.pivot     = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0, 44);
        bar.anchoredPosition = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = PanelBg;

        MakeLabel(bar, "📝 Senaryo Editörü", -700, 0, 22, TextCol, FontStyle.Bold);

        _statusText = MakeLabel(bar, "", 0, 0, 16, LabelCol, FontStyle.Normal);

        MakeBtn(bar, "🗑️ Sil", 550, 0, 90, 36, () =>
        {
            _deleteMode = !_deleteMode;
            _selectedItem = -1;
            SetStatus(_deleteMode ? "Silme modu — haritada tıkla." : "Silme modu kapatıldı.");
        });
        MakeBtn(bar, "💾 Kaydet", 660, 0, 100, 36, SaveScenario);
        MakeBtn(bar, "📂 Yükle",  770, 0, 100, 36, LoadScenario);
        MakeBtn(bar, "▶ Test Et", 880, 0, 110, 36, PlaytestScenario);
        MakeBtn(bar, "✕ Kapat",   900, 0,  90, 36, Close);

        // ── Left palette panel ─────────────────────────────────────────────────
        var pal = MakeRect("Palette", cgo.transform);
        pal.anchorMin = new Vector2(0, 0); pal.anchorMax = new Vector2(0, 1);
        pal.pivot = new Vector2(0, 0.5f);
        pal.sizeDelta = new Vector2(180, -44);
        pal.anchoredPosition = new Vector2(0, -22);
        pal.gameObject.AddComponent<Image>().color = PanelBg;

        // Category tabs
        for (int i = 0; i < Categories.Length; i++)
        {
            int captured = i;
            MakeBtn(pal, Categories[i], 0, 440 - i * 36, 170, 32, () =>
            {
                _selectedCategory = captured;
                _selectedItem     = -1;
                RefreshPalette(pal);
            });
        }

        // Team selector
        MakeLabel(pal, "Takım:", 0, 310, 16, LabelCol, FontStyle.Normal);
        for (int t = 0; t < 4; t++)
        {
            int captured = t;
            var col = TeamPalette.For(t);
            var tb = MakeBtn(pal, $"T{t}", -55 + t * 40, 280, 34, 28, () => { _selectedTeam = captured; });
            tb.GetComponent<Image>().color = new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f, 0.9f);
        }

        // Entity list placeholder (filled in RefreshPalette)
        RefreshPalette(pal);

        // ── Right panel: player setup ──────────────────────────────────────────
        var rp = MakeRect("PlayerSetup", cgo.transform);
        rp.anchorMin = new Vector2(1, 0); rp.anchorMax = new Vector2(1, 1);
        rp.pivot = new Vector2(1, 0.5f);
        rp.sizeDelta = new Vector2(200, -44);
        rp.anchoredPosition = new Vector2(0, -22);
        rp.gameObject.AddComponent<Image>().color = PanelBg;
        BuildPlayerPanel(rp);
    }

    void RefreshPalette(RectTransform pal)
    {
        _paletteButtons.Clear();
        // Remove old item buttons (keep first 3 category + 5 team buttons = first 8 children)
        for (int i = pal.childCount - 1; i >= 8; i--)
            Object.Destroy(pal.GetChild(i).gameObject);

        string[] names = _selectedCategory == 0 ? GetNames(UnitItems.Length, 0)
                       : _selectedCategory == 1 ? GetNames(BuildingItems.Length, 1)
                       :                          GetNames(ResourceItems.Length, 2);

        for (int i = 0; i < names.Length; i++)
        {
            int captured = i;
            var btn = MakeBtn(pal, names[i], 0, 240 - i * 34, 170, 30, () =>
            {
                _selectedItem = captured;
                _deleteMode   = false;
                SetStatus($"Seçildi: {GetItemName(_selectedCategory, _selectedItem)} — haritaya tıkla.");
            });
            _paletteButtons.Add((btn.GetComponent<Button>(), i));
        }
    }

    string[] GetNames(int count, int cat)
    {
        var n = new string[count];
        for (int i = 0; i < count; i++)
            n[i] = GetItemName(cat, i);
        return n;
    }

    string GetItemName(int cat, int idx) => cat switch
    {
        0 when idx < UnitItems.Length     => UnitItems[idx].label,
        1 when idx < BuildingItems.Length => BuildingItems[idx].label,
        2 when idx < ResourceItems.Length => ResourceItems[idx].label,
        _ => "?"
    };

    void BuildPlayerPanel(RectTransform rp)
    {
        MakeLabel(rp, "Oyuncu Ayarları", 0, 480, 18, TextCol, FontStyle.Bold);
        var gm = GameManager.Instance;
        if (gm == null) return;

        for (int t = 0; t < 4; t++)
        {
            int ti = t;
            float y = 420 - t * 90;
            var col = TeamPalette.For(t);
            MakeLabel(rp, $"Takım {t}", -60, y + 35, 16,
                new Color(col.r, col.g, col.b), FontStyle.Bold);

            // Food +/-
            MakeLabel(rp, $"Yiyecek:{gm.teamRes[t].food}", 0, y + 14, 14, LabelCol, FontStyle.Normal);
            MakeBtn(rp, "+500", 60, y, 60, 24, () =>
            {
                gm.teamRes[ti].food += 500;
                Object.Destroy(rp.gameObject); // rebuild panel
                var parent = rp.parent;
                var newPanel = MakeRect("PlayerSetup", parent);
                newPanel.anchorMin = new Vector2(1,0); newPanel.anchorMax = new Vector2(1,1);
                newPanel.pivot = new Vector2(1, 0.5f);
                newPanel.sizeDelta = new Vector2(200, -44);
                newPanel.anchoredPosition = new Vector2(0, -22);
                newPanel.gameObject.AddComponent<Image>().color = PanelBg;
                BuildPlayerPanel(newPanel);
            });

            // Age up
            MakeBtn(rp, "Çağ+", -60, y - 20, 80, 24, () =>
            {
                var tech = gm.teamTech[ti];
                if (tech.age < Age.Imperial)
                {
                    var next = (Age)((int)tech.age + 1);
                    ResearchSystem.Apply(next == Age.Feudal   ? TechType.FeudalAge
                                     :  next == Age.Castle   ? TechType.CastleAge
                                     :                         TechType.ImperialAge, ti);
                }
            });
        }
    }

    void SetStatus(string msg) { if (_statusText != null) _statusText.text = msg; }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Text MakeLabel(RectTransform parent, string text, float x, float y,
        int fontSize, Color col, FontStyle style)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 26);
        rt.anchoredPosition = new Vector2(x, y);
        var t = go.AddComponent<Text>();
        t.text      = text;
        t.fontSize  = fontSize;
        t.color     = col;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.font      = null;
        return t;
    }

    static GameObject MakeBtn(RectTransform parent, string label, float x, float y,
        float w, float h, System.Action onClick)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = BtnColor;
        var btn = go.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        cols.pressedColor     = new Color(1.6f, 1.6f, 1.6f);
        btn.colors = cols;
        btn.onClick.AddListener(() => onClick());
        MakeLabel(rt, label, 0, 0, 13, TextCol, FontStyle.Normal);
        return go;
    }
}
