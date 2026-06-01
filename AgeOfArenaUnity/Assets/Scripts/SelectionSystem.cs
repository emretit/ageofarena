using System.Collections.Generic;
using UnityEngine;

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

        if (Input.GetMouseButtonDown(0))
        {
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
            if (u == null || u.teamId != 0) continue;
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
