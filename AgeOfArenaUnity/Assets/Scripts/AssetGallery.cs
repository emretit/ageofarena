using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// In-game "admin" asset gallery. Press <b>F8</b> during play to lay out every
/// building type and every unit type on a flat plane in labelled grids, far from
/// the live island, and snap the camera there. Press F8 again to return to the
/// game. Purely a dev/inspection tool — it spawns display dummies (units have
/// their AI + NavMeshAgent disabled) and never touches the running match.
/// </summary>
public class AssetGallery : MonoBehaviour
{
    const float GZ = 260f;   // gallery sits far north of the island (radius ~92)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("AssetGallery");
        go.AddComponent<AssetGallery>();
    }

    GameObject _root;
    readonly List<Transform> _labels = new();
    bool _on;
    Vector2 _savedBounds;
    float _savedMax;
    NavMeshDataInstance _nav;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (_on) Close();
            else Build();
        }
    }

    void Build()
    {
        if (_root != null) Destroy(_root);
        _labels.Clear();
        _root = new GameObject("__AssetGallery");
        _root.transform.position = new Vector3(0, 0, GZ);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GalleryGround";
        ground.transform.SetParent(_root.transform);
        ground.transform.localPosition = new Vector3(0, 0, 18f);
        ground.transform.localScale = new Vector3(13, 1, 13);
        ground.GetComponent<Renderer>().material.color = new Color(0.80f, 0.77f, 0.56f);

        BakeNavPatch();   // before unit spawn so each agent attaches without warnings

        var team = TeamPalette.For(0);

        // ── Buildings (back rows) ──
        var btypes = (BuildingType[])Enum.GetValues(typeof(BuildingType));
        Grid(btypes.Length, 6, 7f, new Vector3(0, 0, GZ + 42f), (i, pos) =>
        {
            BuildingFactory.Create(btypes[i], _root.transform, pos, team);
            Label(btypes[i].ToString(), pos, 4.8f);
        });

        // ── Units (front rows) — display dummies ──
        // UnitEntity.Awake adds an (enabled) NavMeshAgent, which warns if there's no
        // NavMesh underneath. We baked a flat patch under the gallery above, and pass
        // navalAgentTypeId=0 so ships use the default agent type too — so every agent
        // attaches cleanly here. UnitEntity itself is then disabled to freeze logic.
        var utypes = (UnitType[])Enum.GetValues(typeof(UnitType));
        Grid(utypes.Length, 10, 3f, new Vector3(0, 0, GZ + 6f), (i, pos) =>
        {
            UnitEntity e = null;
            try { e = UnitFactory.Spawn(utypes[i], _root.transform, pos, 0, 0); }
            catch { /* unusual factory paths — label still shown */ }
            if (e != null) e.enabled = false;   // model + animator keep rendering
            Label(utypes[i].ToString(), pos, 2.2f);
        });

        // ── Camera: widen the rig's pannable area and snap to the gallery ──
        var rig = GameManager.Instance != null ? GameManager.Instance.cameraRig : null;
        if (rig != null)
        {
            _savedBounds = rig.bounds;
            _savedMax = rig.maxSize;
            rig.bounds = new Vector2(80f, GZ + 90f);
            rig.maxSize = 95f;
            if (Camera.main != null) Camera.main.orthographicSize = 70f;
            rig.FocusOn(new Vector3(0, 0, GZ + 22f));
        }

        _on = true;
    }

    // Flat default-agent NavMesh patch under the gallery so spawned units' agents
    // attach here instead of logging "not close enough to the NavMesh".
    void BakeNavPatch()
    {
        if (_nav.valid) NavMesh.RemoveNavMeshData(_nav);
        var center = new Vector3(0, 0, GZ + 15f);
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape     = NavMeshBuildSourceShape.Box,
                size      = new Vector3(130f, 0.2f, 130f),
                transform = Matrix4x4.Translate(center),
                area      = 0,
            }
        };
        var bounds = new Bounds(center, new Vector3(140f, 8f, 140f));
        var data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByIndex(0), sources, bounds, Vector3.zero, Quaternion.identity);
        if (data != null) _nav = NavMesh.AddNavMeshData(data);
    }

    void Close()
    {
        if (_root != null) Destroy(_root);
        if (_nav.valid) { NavMesh.RemoveNavMeshData(_nav); _nav = default; }
        _labels.Clear();
        var rig = GameManager.Instance != null ? GameManager.Instance.cameraRig : null;
        if (rig != null)
        {
            rig.bounds = _savedBounds;
            rig.maxSize = _savedMax;
            if (Camera.main != null) Camera.main.orthographicSize = Mathf.Min(Camera.main.orthographicSize, _savedMax);
            rig.FocusOn(Vector3.zero);
        }
        _on = false;
    }

    void LateUpdate()
    {
        if (!_on || Camera.main == null) return;
        var rot = Camera.main.transform.rotation;
        for (int i = 0; i < _labels.Count; i++)
            if (_labels[i] != null) _labels[i].rotation = rot;
    }

    void Grid(int count, int cols, float step, Vector3 origin, Action<int, Vector3> place)
    {
        for (int i = 0; i < count; i++)
        {
            int cx = i % cols, cz = i / cols;
            // rows march toward -z so successive rows stack in front of the camera
            var pos = origin + new Vector3((cx - (cols - 1) * 0.5f) * step, 0, -cz * step);
            place(i, pos);
        }
    }

    void Label(string text, Vector3 worldPos, float y)
    {
        var go = new GameObject("lbl");
        go.transform.SetParent(_root.transform);
        go.transform.position = new Vector3(worldPos.x, y, worldPos.z);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = UiFonts.Default;
        tm.fontSize = 48;
        tm.characterSize = 0.18f;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.12f, 0.1f, 0.08f);
        var mr = go.GetComponent<MeshRenderer>();
        if (tm.font != null) mr.sharedMaterial = tm.font.material;
        _labels.Add(go.transform);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, Screen.height - 26, 600, 22),
            _on ? "Asset Galerisi açık — F8: oyuna dön" : "F8: Asset Galerisi (tüm bina + birim)", style);
    }
}
