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
    const float DragThreshold  = 5f;    // px before a click becomes a drag
    const float DblClickWindow = 0.35f; // seconds for double-click detection
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

    float _lastClickTime = -10f;
    UnitType? _lastClickType;

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
        if (gm != null && gm.command != null && (gm.command.AttackMovePending || gm.command.PatrolPending)) return;

        HandleControlGroups(gm);
        if (Hotkeys.Down(HotkeyAction.SelectIdle)) SelectNextIdleWorker();

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
        ResourceNode hitNode = null;
        if (Physics.Raycast(ray, out var hit, 500f))
        {
            hitUnit    = hit.collider.GetComponentInParent<UnitEntity>();
            if (hitUnit == null)
                hitBuilding = hit.collider.GetComponentInParent<BuildingEntity>();
            if (hitUnit == null && hitBuilding == null)
                hitNode = hit.collider.GetComponentInParent<ResourceNode>();
        }

        var gm = GameManager.Instance;

        if (hitUnit != null && hitUnit.teamId == GameBootstrap.LocalTeam)
        {
            gm.selectedBuilding = null;
            gm.selectedNode = null;
            float t = Time.unscaledTime;
            bool isDbl = !additive && _lastClickType == hitUnit.type && t - _lastClickTime <= DblClickWindow;
            _lastClickTime = t;
            _lastClickType = hitUnit.type;

            if (isDbl)
            {
                // Double-click: select all visible units of the same type on screen.
                SelectSameTypeOnScreen(hitUnit.type, gm);
            }
            else if (additive)
            {
                if (Selected.Contains(hitUnit)) Deselect(hitUnit);
                else Select(hitUnit);
            }
            else
            {
                ClearSelection();
                Select(hitUnit);
            }
            PlaySelectSound();
        }
        else if (hitBuilding != null && !additive)
        {
            ClearSelection();
            gm.selectedBuilding = hitBuilding;
            gm.selectedNode = null;
        }
        else if (hitNode != null && !additive)
        {
            ClearSelection();
            gm.selectedBuilding = null;
            gm.selectedNode = hitNode;
        }
        else if (!additive)
        {
            ClearSelection();
            gm.selectedBuilding = null;
            gm.selectedNode = null;
        }
    }

    void BoxSelect(Vector2 a, Vector2 b)
    {
        if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            ClearSelection();

        // Compare in viewport space (0..1) so the box matches the units under it on ANY
        // DPI / render-scale / letterboxed display. The old code mixed Input.mousePosition
        // (screen px) with WorldToScreenPoint (camera px) — correct only at 1.0 scale.
        float sw = Screen.width, sh = Screen.height;
        Vector2 av = new Vector2(a.x / sw, a.y / sh);
        Vector2 bv = new Vector2(b.x / sw, b.y / sh);
        float xMin = Mathf.Min(av.x, bv.x), xMax = Mathf.Max(av.x, bv.x);
        float yMin = Mathf.Min(av.y, bv.y), yMax = Mathf.Max(av.y, bv.y);

        var units = GameManager.Instance.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.teamId != GameBootstrap.LocalTeam || u.isGarrisoned) continue;
            Vector3 sp = _cam.WorldToViewportPoint(u.transform.position);
            if (sp.z < 0f) continue; // behind camera
            if (sp.x >= xMin && sp.x <= xMax && sp.y >= yMin && sp.y <= yMax && !Selected.Contains(u))
                Select(u);
        }
        if (Selected.Count > 0) PlaySelectSound();
    }

    void Select(UnitEntity u)
    {
        Selected.Add(u);
        u.SetSelected(true, OwnColor);
    }

    /// <summary>SUBT: One selection blip per user action. Plays villager sound if all
    /// selected units are villagers, otherwise generic unit select.</summary>
    void PlaySelectSound()
    {
        bool allVillagers = Selected.Count > 0 && Selected.TrueForAll(u => u.type == UnitType.Villager);
        AudioManager.Play(allVillagers ? AudioManager.SoundId.UnitVillager : AudioManager.SoundId.UnitSelect, 0.5f);
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
            if (Selected[i] != null && Selected[i].teamId == GameBootstrap.LocalTeam) list.Add(Selected[i]);
        if (list.Count > 0) _groups[n] = list;
    }

    /// <summary>Re-select control group <paramref name="n"/>, pruning dead members. A
    /// second tap within <see cref="DoubleTapWindow"/> re-centres the camera on it.</summary>
    void SelectGroup(int n)
    {
        if (!_groups.TryGetValue(n, out var list)) return;
        list.RemoveAll(u => u == null || u.teamId != GameBootstrap.LocalTeam);
        if (list.Count == 0) { _groups.Remove(n); return; }

        ClearSelection();
        if (GameManager.Instance != null) GameManager.Instance.selectedBuilding = null;
        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            if (u.isGarrisoned) continue; // hidden inside a building — skip but keep in group
            if (!Selected.Contains(u)) Select(u);
        }

        if (Selected.Count > 0) PlaySelectSound();
        float t = Time.unscaledTime;
        if (_lastGroupKey == n && t - _lastGroupTime <= DoubleTapWindow) FocusCameraOnSelection();
        _lastGroupKey = n;
        _lastGroupTime = t;
    }

    // ── Idle workers ───────────────────────────────────────────────────────────
    int _idleCycle;

    static bool IsIdleWorker(UnitEntity u)
        => u != null && u.teamId == GameBootstrap.LocalTeam && u.type == UnitType.Villager
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

    void SelectSameTypeOnScreen(UnitType targetType, GameManager gm)
    {
        ClearSelection();
        gm.selectedBuilding = null;
        var units = gm.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.teamId != GameBootstrap.LocalTeam || u.type != targetType || u.isGarrisoned) continue;
            Vector3 sp = _cam.WorldToViewportPoint(u.transform.position);
            if (sp.z > 0f && sp.x >= 0f && sp.x <= 1f && sp.y >= 0f && sp.y <= 1f)
                Select(u);
        }
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
