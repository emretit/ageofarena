using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Right-click commands for the current selection: click a resource node to send
/// villagers gathering, or click the ground to issue a (formation) move order.
/// Spawns a short-lived ground marker for feedback.
/// </summary>
public class CommandSystem : MonoBehaviour
{
    const float FormationSpacing = 1.5f;
    static readonly Color MoveColor = Prims.Hex(0x00ff00);
    static readonly Color GatherColor = Prims.Hex(0xffcc44);
    static readonly Color AttackColor = Prims.Hex(0xff3322);

    Camera _cam;

    void Start() => _cam = Camera.main;

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;
        // While placing a building, the placement system owns the mouse.
        if (gm.placement != null && gm.placement.Active) return;

        HandleTrainHotkeys();
        HandleMarketHotkeys();
        HandleResearchHotkeys();
        HandleBuildHotkeys();
        if (!Input.GetMouseButtonDown(1)) return;
        // A right-click over the HUD command bar shouldn't issue a world order.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var selected = gm.selection != null ? gm.selection.Selected : null;
        if (selected == null || selected.Count == 0) return;

        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f)) return;

        // Enemy unit or building → attack order for the whole selection.
        var enemy = ResolveEnemy(hit.collider);
        if (enemy != null)
        {
            bool ordered = false;
            for (int i = 0; i < selected.Count; i++)
            {
                var u = selected[i];
                if (u != null) { u.AttackOrder(enemy); ordered = true; }
            }
            if (ordered) SpawnMarker(enemy.Transform.position, AttackColor);
            return;
        }

        var node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null && !node.Depleted)
        {
            bool any = false;
            for (int i = 0; i < selected.Count; i++)
            {
                var u = selected[i];
                if (u != null && u.type == UnitType.Villager)
                {
                    gm.gather.AssignGather(u, node);
                    any = true;
                }
            }
            if (any) SpawnMarker(node.transform.position, GatherColor);
            return;
        }

        if (hit.collider.gameObject.name == "Ground")
        {
            MoveOrder(selected, hit.point);
            SpawnMarker(hit.point, MoveColor);
        }
    }

    /// <summary>Returns the enemy (non-player) target under a collider, or null.</summary>
    static IDamageable ResolveEnemy(Collider col)
    {
        var u = col.GetComponentInParent<UnitEntity>();
        if (u != null) return u.teamId != 0 ? u : null;
        var b = col.GetComponentInParent<BuildingEntity>();
        if (b != null && b.teamId != 0) return b;
        return null;
    }

    /// <summary>When a villager is selected, a building hotkey enters placement mode.</summary>
    void HandleBuildHotkeys()
    {
        var gm = GameManager.Instance;
        if (gm.placement == null || gm.selection == null) return;

        var sel = gm.selection.Selected;
        bool hasVillager = false;
        for (int i = 0; i < sel.Count; i++)
            if (sel[i] != null && sel[i].type == UnitType.Villager) { hasVillager = true; break; }
        if (!hasVillager) return;

        foreach (var d in BuildingDefs.Buildable())
        {
            if (!BuildingDefs.UnlockedAt(d.type, gm.tech.age)) continue; // age-locked
            if (Input.GetKeyDown(char.ToLower(d.hotkey).ToString()))
            {
                gm.placement.Begin(d.type);
                break;
            }
        }
    }

    /// <summary>When a building is selected, number keys research its available techs
    /// (in the order shown in the HUD). Market keeps number keys for trading.</summary>
    void HandleResearchHotkeys()
    {
        var gm = GameManager.Instance;
        var b = gm?.selectedBuilding;
        if (b == null || gm.research == null) return;
        if (b.type == BuildingType.Market) return; // Market: number keys are trade

        var techs = b.GetResearchables();
        int max = Mathf.Min(techs.Count, 9);
        for (int i = 0; i < max; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                gm.research.Enqueue(b, techs[i]);
                break;
            }
        }
    }

    /// <summary>When a finished Market is selected, number keys trade resources.</summary>
    void HandleMarketHotkeys()
    {
        var gm = GameManager.Instance;
        var b = gm?.selectedBuilding;
        if (b == null || b.type != BuildingType.Market || b.underConstruction) return;

        var rm = gm.resources;
        if (Input.GetKeyDown(KeyCode.Alpha1)) MarketSystem.Sell(rm, ResourceKind.Food);
        if (Input.GetKeyDown(KeyCode.Alpha2)) MarketSystem.Sell(rm, ResourceKind.Wood);
        if (Input.GetKeyDown(KeyCode.Alpha3)) MarketSystem.Sell(rm, ResourceKind.Stone);
        if (Input.GetKeyDown(KeyCode.Alpha4)) MarketSystem.Buy(rm, ResourceKind.Food);
    }

    void HandleTrainHotkeys()
    {
        var gm = GameManager.Instance;
        var b = gm?.selectedBuilding;
        if (b == null || gm.trainingQueue == null) return;

        var trainables = b.GetTrainables();
        for (int i = 0; i < trainables.Length; i++)
        {
            var def = trainables[i];
            if (def.hotkey.Length > 0 && Input.GetKeyDown(def.hotkey[0].ToString().ToLower()))
                gm.trainingQueue.Enqueue(b, def);
        }
    }

    void MoveOrder(List<UnitEntity> selected, Vector3 point)
    {
        int n = selected.Count;
        if (n == 1)
        {
            selected[0].Stop();
            selected[0].MoveTo(point);
            return;
        }

        // Formation grid (Selection.ts:265-277): centered cols×rows around click.
        int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
        int rows = Mathf.CeilToInt(n / (float)cols);
        for (int i = 0; i < n; i++)
        {
            var u = selected[i];
            if (u == null) continue;
            int row = i / cols;
            int col = i % cols;
            float offX = (col - (cols - 1) / 2f) * FormationSpacing;
            float offZ = (row - (rows - 1) / 2f) * FormationSpacing;
            u.Stop();
            u.MoveTo(point + new Vector3(offX, 0f, offZ));
        }
    }

    void SpawnMarker(Vector3 pos, Color color)
    {
        var go = new GameObject("MoveMarker");
        go.transform.position = pos + new Vector3(0, 0.03f, 0);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.09f;
        lr.material = Prims.UnlitColorMat(color);
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        const int seg = 20;
        const float radius = 0.7f;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        Destroy(go, 0.5f);
    }
}
