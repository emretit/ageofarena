using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Left-click selection: single click picks one own-team unit (shift toggles to
/// add/remove), or a drag-box selects every own-team unit inside the rectangle.
/// Uses legacy Input + Physics.Raycast (no Input System package). Selected units
/// show a green ring via <see cref="UnitEntity.SetSelected"/>.
/// </summary>
public class SelectionSystem : MonoBehaviour
{
    const float DragThreshold = 5f;     // px before a click becomes a drag
    static readonly Color OwnColor = Prims.Hex(0x00ff00);

    public readonly List<UnitEntity> Selected = new();

    // Control groups: Ctrl+1..9 assigns the current selection, 1..9 re-selects it,
    // a second tap within DoubleTapWindow jumps the camera to the group.
    const float DoubleTapWindow = 0.4f;
    readonly Dictionary<int, List<UnitEntity>> _groups = new();
    int _lastGroupKey = -1;
    float _lastGroupTime = -10f;

    Camera _cam;
    Vector2 _dragStart;
    bool _pointerDown;
    bool _dragging;
    Texture2D _boxTex;

    void Start()
    {
        _cam = Camera.main;
        _boxTex = new Texture2D(1, 1);
        _boxTex.SetPixel(0, 0, new Color(0.3f, 1f, 0.4f, 0.25f));
        _boxTex.Apply();
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        var gm = GameManager.Instance;
        if (gm != null && gm.placement != null && gm.placement.Active) return; // build mode owns the mouse
        if (gm != null && gm.command != null && gm.command.AttackMovePending) return; // attack-move picking owns the mouse

        HandleControlGroups(gm);
        if (Input.GetKeyDown(KeyCode.Period)) SelectNextIdleWorker();

        if (Input.GetMouseButtonDown(0))
        {
            // A click that starts over the HUD (command bar buttons) belongs to uGUI,
            // not world selection — ignore it so the panel doesn't get dismissed.
            if (IsPointerOverUI()) return;
            _pointerDown = true;
            _dragging = false;
            _dragStart = Input.mousePosition;
        }

        if (_pointerDown && Input.GetMouseButton(0))
        {
            if (!_dragging && ((Vector2)Input.mousePosition - _dragStart).magnitude > DragThreshold)
                _dragging = true;
        }

        if (Input.GetMouseButtonUp(0) && _pointerDown)
        {
            _pointerDown = false;
            if (_dragging) BoxSelect(_dragStart, Input.mousePosition);
            else SingleClick();
            _dragging = false;
        }
    }

    void SingleClick()
    {
        bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var ray = _cam.ScreenPointToRay(Input.mousePosition);

        UnitEntity hitUnit = null;
        BuildingEntity hitBuilding = null;
        if (Physics.Raycast(ray, out var hit, 500f))
        {
            hitUnit    = hit.collider.GetComponentInParent<UnitEntity>();
            if (hitUnit == null)
                hitBuilding = hit.collider.GetComponentInParent<BuildingEntity>();
        }

        var gm = GameManager.Instance;

        if (hitUnit != null && hitUnit.teamId == 0)
        {
            gm.selectedBuilding = null;
            if (additive)
            {
                if (Selected.Contains(hitUnit)) Deselect(hitUnit);
                else Select(hitUnit);
            }
            else
            {
                ClearSelection();
                Select(hitUnit);
            }
        }
        else if (hitBuilding != null && hitBuilding.teamId == 0 && !additive)
        {
            ClearSelection();
            gm.selectedBuilding = hitBuilding;
        }
        else if (!additive)
        {
            ClearSelection();
            gm.selectedBuilding = null;
        }
    }

    void BoxSelect(Vector2 a, Vector2 b)
    {
        if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            ClearSelection();

        float xMin = Mathf.Min(a.x, b.x), xMax = Mathf.Max(a.x, b.x);
        float yMin = Mathf.Min(a.y, b.y), yMax = Mathf.Max(a.y, b.y);

        var units = GameManager.Instance.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.teamId != 0 || u.isGarrisoned) continue;
            Vector3 sp = _cam.WorldToScreenPoint(u.transform.position);
            if (sp.z < 0f) continue; // behind camera
            if (sp.x >= xMin && sp.x <= xMax && sp.y >= yMin && sp.y <= yMax && !Selected.Contains(u))
                Select(u);
        }
    }

    void Select(UnitEntity u)
    {
        Selected.Add(u);
        u.SetSelected(true, OwnColor);
    }

    void Deselect(UnitEntity u)
    {
        Selected.Remove(u);
        u.SetSelected(false, OwnColor);
    }

    public void ClearSelection()
    {
        for (int i = 0; i < Selected.Count; i++)
            if (Selected[i] != null) Selected[i].SetSelected(false, OwnColor);
        Selected.Clear();
    }

    // ── Control groups ─────────────────────────────────────────────────────────
    void HandleControlGroups(GameManager gm)
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        for (int n = 1; n <= 9; n++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha0 + n)) continue;
            if (ctrl) AssignGroup(n);
            // A bare digit recalls the group only when no building owns it (the command
            // bar uses 1..9 for research/trade while a building is selected).
            else if (gm == null || gm.selectedBuilding == null) SelectGroup(n);
            return; // at most one digit per frame
        }
    }

    /// <summary>Snapshot the current selection into control group <paramref name="n"/>.
    /// An empty selection is a no-op (does not wipe an existing group).</summary>
    void AssignGroup(int n)
    {
        var list = new List<UnitEntity>();
        for (int i = 0; i < Selected.Count; i++)
            if (Selected[i] != null && Selected[i].teamId == 0) list.Add(Selected[i]);
        if (list.Count > 0) _groups[n] = list;
    }

    /// <summary>Re-select control group <paramref name="n"/>, pruning dead members. A
    /// second tap within <see cref="DoubleTapWindow"/> re-centres the camera on it.</summary>
    void SelectGroup(int n)
    {
        if (!_groups.TryGetValue(n, out var list)) return;
        list.RemoveAll(u => u == null || u.teamId != 0);
        if (list.Count == 0) { _groups.Remove(n); return; }

        ClearSelection();
        if (GameManager.Instance != null) GameManager.Instance.selectedBuilding = null;
        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            if (u.isGarrisoned) continue; // hidden inside a building — skip but keep in group
            if (!Selected.Contains(u)) Select(u);
        }

        float t = Time.unscaledTime;
        if (_lastGroupKey == n && t - _lastGroupTime <= DoubleTapWindow) FocusCameraOnSelection();
        _lastGroupKey = n;
        _lastGroupTime = t;
    }

    // ── Idle workers ───────────────────────────────────────────────────────────
    int _idleCycle;

    static bool IsIdleWorker(UnitEntity u)
        => u != null && u.teamId == 0 && u.type == UnitType.Villager
           && !u.isGarrisoned && u.state == UnitState.Idle;

    /// <summary>Count of the player's idle villagers (drives the HUD indicator).</summary>
    public int IdleVillagerCount()
    {
        var units = GameManager.Instance != null ? GameManager.Instance.units : null;
        if (units == null) return 0;
        int c = 0;
        for (int i = 0; i < units.Count; i++) if (IsIdleWorker(units[i])) c++;
        return c;
    }

    /// <summary>Select the next idle villager in a stable cycle and centre the camera
    /// on it. No-op when none are idle.</summary>
    public void SelectNextIdleWorker()
    {
        var units = GameManager.Instance != null ? GameManager.Instance.units : null;
        if (units == null) return;
        var idle = new List<UnitEntity>();
        for (int i = 0; i < units.Count; i++) if (IsIdleWorker(units[i])) idle.Add(units[i]);
        if (idle.Count == 0) return;

        if (_idleCycle >= idle.Count) _idleCycle = 0;
        var pick = idle[_idleCycle];
        _idleCycle = (_idleCycle + 1) % idle.Count;

        ClearSelection();
        if (GameManager.Instance != null) GameManager.Instance.selectedBuilding = null;
        Select(pick);
        var rig = _cam != null ? _cam.GetComponent<IsometricCameraRig>() : null;
        if (rig != null) rig.FocusOn(pick.transform.position);
    }

    void FocusCameraOnSelection()
    {
        Vector3 sum = Vector3.zero; int c = 0;
        for (int i = 0; i < Selected.Count; i++)
            if (Selected[i] != null) { sum += Selected[i].transform.position; c++; }
        if (c == 0) return;
        var rig = _cam != null ? _cam.GetComponent<IsometricCameraRig>() : null;
        if (rig != null) rig.FocusOn(sum / c);
    }

    /// <summary>True when the cursor is over an interactive uGUI element (the HUD).</summary>
    static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void OnGUI()
    {
        if (!_dragging) return;
        Vector2 cur = Input.mousePosition;
        // Input is bottom-left origin; GUI is top-left. Flip Y for drawing.
        float x = Mathf.Min(_dragStart.x, cur.x);
        float y = Screen.height - Mathf.Max(_dragStart.y, cur.y);
        float w = Mathf.Abs(_dragStart.x - cur.x);
        float h = Mathf.Abs(_dragStart.y - cur.y);
        GUI.DrawTexture(new Rect(x, y, w, h), _boxTex);
    }
}
