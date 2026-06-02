using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Villager build mode: a translucent ghost of the chosen building follows the
/// cursor on the ground, tinted green when the spot is valid (affordable + not
/// overlapping anything) and red otherwise. Left-click places a construction
/// site, deducts resources and sends the selected villagers to build it;
/// right-click / Esc cancels. Hotkey-driven (AoE style) — see
/// <see cref="CommandSystem.HandleBuildHotkeys"/>. While <see cref="Active"/>,
/// selection and command input is suppressed so clicks don't leak through.
/// </summary>
public class BuildingPlacement : MonoBehaviour
{
    static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
    static readonly Color TeamColor = Prims.Hex(0x2a5db0);
    static readonly Vector3 CheckHalf = new Vector3(1.4f, 1.0f, 1.4f);

    public bool Active { get; private set; }

    Camera _cam;
    BuildingType _type;
    BuildingDef _def;
    GameObject _ghost;
    Material _ghostMat;
    readonly List<UnitEntity> _builders = new();
    bool _valid;

    GameManager GM => GameManager.Instance;

    /// <summary>Enter placement mode for <paramref name="type"/> using the current villager selection.</summary>
    public void Begin(BuildingType type)
    {
        if (_cam == null) _cam = Camera.main;

        // Capture selected villagers as builders; without one we can't build.
        _builders.Clear();
        var sel = GM.selection != null ? GM.selection.Selected : null;
        if (sel != null)
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null && sel[i].type == UnitType.Villager) _builders.Add(sel[i]);
        if (_builders.Count == 0) return;

        Cancel();              // clear any in-progress ghost
        _type = type;
        _def = BuildingDefs.Get(type);

        _ghost = BuildingFactory.Create(type, null, Vector3.zero, TeamColor);
        var be = _ghost.GetComponent<BuildingEntity>();
        if (be != null) Destroy(be);                                  // ghost isn't a real building
        foreach (var c in _ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        // A Wall ghost carries a carving NavMeshObstacle; strip it so the preview
        // doesn't punch holes in the NavMesh while it's still just a ghost.
        foreach (var o in _ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) Destroy(o);
        _ghostMat = GhostMat(Color.green);
        foreach (var r in _ghost.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = _ghostMat;

        Active = true;
    }

    public void Cancel()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        Active = false;
    }

    void Update()
    {
        if (!Active) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Ground.Raycast(ray, out float enter)) return;
        Vector3 pos = ray.GetPoint(enter);
        _ghost.transform.position = pos;

        _valid = IsValid(pos);
        _ghostMat.color = _valid ? new Color(0.3f, 1f, 0.35f, 0.45f) : new Color(1f, 0.3f, 0.25f, 0.45f);

        if (_valid && Input.GetMouseButtonDown(0)) Place(pos);
    }

    bool IsValid(Vector3 pos)
    {
        if (!GM.resources.CanAfford(_def.food, _def.wood, _def.gold, _def.stone)) return false;
        // Castle needs a bigger clearance; Wall/Gate use a small box so segments can
        // sit next to each other in a line; everything else uses the default footprint.
        Vector3 half =
            _type == BuildingType.Castle             ? new Vector3(2.4f, 1.0f, 2.4f) :
            _type is BuildingType.Wall or BuildingType.Gate ? new Vector3(0.7f, 1.0f, 0.7f) :
            CheckHalf;
        // Overlap test raised above the ground plane so the flat ground isn't a hit.
        var hits = Physics.OverlapBox(pos + Vector3.up * 1.2f, half, Quaternion.identity);
        return hits.Length == 0;
    }

    void Place(Vector3 pos)
    {
        GM.resources.Deduct(_def.food, _def.wood, _def.gold, _def.stone);

        var go = BuildingFactory.Create(_type, null, pos, TeamColor);
        var be = go.GetComponent<BuildingEntity>();
        be.teamId = 0;
        be.maxHp = _def.maxHp;
        be.buildTime = _def.buildTime;
        be.underConstruction = true;
        be.buildProgress = 0f;
        be.hp = 1f;
        GM.RegisterBuilding(be);                 // a half-built building can already be attacked

        for (int i = 0; i < _builders.Count; i++)
            if (_builders[i] != null) _builders[i].BuildOrder(be);

        Cancel();
    }

    /// <summary>Transparent Standard material for the placement ghost.</summary>
    static Material GhostMat(Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3000;
        m.color = new Color(c.r, c.g, c.b, 0.45f);
        return m;
    }

    GUIStyle _style;

    void OnGUI()
    {
        if (GM == null) return;
        _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };

        // Only the active-placement hint is drawn here; the villager build menu now
        // lives in the HUD command bar (clickable buttons), so no hotkey-list text.
        if (Active)
        {
            _style.normal.textColor = _valid ? Color.green : new Color(1f, 0.5f, 0.4f);
            GUI.Label(new Rect(Screen.width / 2f - 250, 8, 500, 22),
                $"Yerleştiriliyor: {_def.display}  —  Sol tık: koy, Sağ tık/Esc: iptal", _style);
        }
    }
}
