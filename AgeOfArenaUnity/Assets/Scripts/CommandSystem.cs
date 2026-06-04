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

    /// <summary>True while waiting for the player to click an attack-move destination.</summary>
    public bool AttackMovePending { get; private set; }

    void Start() => _cam = Camera.main;

    /// <summary>Enter attack-move targeting: the next ground click sends every selected
    /// unit advancing to that point, engaging enemies on the way. Called by the HUD.</summary>
    public void BeginAttackMove()
    {
        var sel = GameManager.Instance?.selection?.Selected;
        if (sel == null) return;
        for (int i = 0; i < sel.Count; i++)
            if (sel[i] != null) { AttackMovePending = true; return; }
    }

    static bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        // The rally flag tracks the selected building's rally point every frame.
        UpdateRallyFlag(gm);

        // While placing a building, the placement system owns the mouse.
        if (gm.placement != null && gm.placement.Active) return;

        // While picking an attack-move target, the next click is consumed here.
        if (AttackMovePending) { HandleAttackMovePick(gm); return; }
        if (_patrolPending) { HandlePatrolPick(gm); return; }

        HandleTrainHotkeys();
        HandleMarketHotkeys();
        HandleResearchHotkeys();
        HandleBuildHotkeys();
        HandleUnitHotkeys();
        HandleGarrisonHotkeys();
        if (!Input.GetMouseButtonDown(1)) return;
        // A right-click over the HUD command bar shouldn't issue a world order.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f)) return;

        // A selected own building takes the right-click as a rally-point order:
        // trained units will walk to this spot (or the clicked resource node).
        var selBld = gm.selectedBuilding;
        if (selBld != null && selBld.teamId == 0)
        {
            var rnode = hit.collider.GetComponentInParent<ResourceNode>();
            Vector3 rp = rnode != null && !rnode.Depleted ? rnode.transform.position : hit.point;
            selBld.hasRally = true;
            selBld.rallyPoint = rp;
            SpawnMarker(rp, MoveColor);
            return;
        }

        var selected = gm.selection != null ? gm.selection.Selected : null;
        if (selected == null || selected.Count == 0) return;

        // Any explicit right-click order overrides a running attack-move.
        for (int i = 0; i < selected.Count; i++)
            if (selected[i] != null) selected[i].attackMove = false;

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

        // Own building that is under construction or damaged → send villagers to
        // build/repair it (BuildSystem advances construction or restores hp).
        var ownB = hit.collider.GetComponentInParent<BuildingEntity>();
        if (ownB != null && ownB.teamId == 0 && (ownB.underConstruction || ownB.hp < ownB.maxHp))
        {
            bool any = false;
            for (int i = 0; i < selected.Count; i++)
            {
                var u = selected[i];
                if (u != null && u.type == UnitType.Villager) { u.BuildOrder(ownB); any = true; }
            }
            if (any) { SpawnMarker(ownB.transform.position, MoveColor); return; }
        }

        // Own intact building with garrison space → shelter the selected units inside.
        if (ownB != null && ownB.teamId == 0 && !ownB.underConstruction && ownB.GarrisonCapacity > 0)
        {
            int free = ownB.GarrisonCapacity - ownB.GarrisonCount;
            bool any = false;
            for (int i = 0; i < selected.Count && free > 0; i++)
            {
                var u = selected[i];
                if (u == null || u.isGarrisoned) continue;
                u.GarrisonOrder(ownB);
                free--;
                any = true;
            }
            if (any) { SpawnMarker(ownB.transform.position, MoveColor); return; }
        }

        var node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null && !node.Depleted)
        {
            bool any = false;
            for (int i = 0; i < selected.Count; i++)
            {
                var u = selected[i];
                // Villagers gather any node; FISH: fishing ships gather food (fish ponds) on water.
                bool canGather = u != null && (u.type == UnitType.Villager
                    || (u.type == UnitType.FishingShip && node.kind == ResourceKind.Food));
                if (canGather)
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
            // SUBT: move-order confirm sound.
            AudioManager.Play(AudioManager.SoundId.UnitMove, 0.5f);
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
        if (CtrlHeld) return; // Ctrl+digit is reserved for control-group assignment
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
        if (CtrlHeld) return; // Ctrl+digit is reserved for control-group assignment
        var gm = GameManager.Instance;
        var b = gm?.selectedBuilding;
        if (b == null || b.type != BuildingType.Market || b.underConstruction) return;

        var rm = gm.resources;
        if (Input.GetKeyDown(KeyCode.Alpha1)) MarketSystem.Sell(rm, ResourceKind.Food);
        if (Input.GetKeyDown(KeyCode.Alpha2)) MarketSystem.Sell(rm, ResourceKind.Wood);
        if (Input.GetKeyDown(KeyCode.Alpha3)) MarketSystem.Sell(rm, ResourceKind.Stone);
        if (Input.GetKeyDown(KeyCode.Alpha4)) MarketSystem.Buy(rm, ResourceKind.Food);
    }

    /// <summary>With units (and no building) selected, S stops them and A starts an
    /// attack-move. Building/train hotkeys take priority when a building is selected.</summary>
    void HandleUnitHotkeys()
    {
        var gm = GameManager.Instance;

        // Game speed: [ slow down, ] speed up, pause with Space (toggle).
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            Time.timeScale = Mathf.Max(0.5f, Time.timeScale - 0.5f);
        else if (Input.GetKeyDown(KeyCode.RightBracket))
            Time.timeScale = Mathf.Min(4f, Time.timeScale + 0.5f);
        else if (Input.GetKeyDown(KeyCode.Space))
            Time.timeScale = Time.timeScale > 0.01f ? 0f : 1f;

        if (gm.selectedBuilding != null) return;
        var sel = gm.selection != null ? gm.selection.Selected : null;
        if (sel == null || sel.Count == 0) return;

        if (Hotkeys.Down(HotkeyAction.Stop))
            for (int i = 0; i < sel.Count; i++) { var u = sel[i]; if (u != null) { u.attackMove = false; u.Stop(); } }
        else if (Hotkeys.Down(HotkeyAction.AttackMove))
            BeginAttackMove();
        else if (Input.GetKeyDown(KeyCode.P))
            BeginPatrol(gm, sel);
    }

    bool _patrolPending;

    void BeginPatrol(GameManager gm, System.Collections.Generic.List<UnitEntity> sel)
    {
        _patrolPending = true;
    }

    // Called in Update when patrol is pending; next right-click becomes the patrol endpoint.
    void HandlePatrolPick(GameManager gm)
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { _patrolPending = false; return; }
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f)) { _patrolPending = false; return; }
        var sel = gm.selection != null ? gm.selection.Selected : null;
        if (sel != null)
            for (int i = 0; i < sel.Count; i++)
            {
                var u = sel[i];
                if (u == null) continue;
                u.patrolA = u.transform.position;
                u.patrolB = hit.point;
                u.patrolActive = true;
                u.MoveTo(u.patrolB);
            }
        SpawnMarker(hit.point, MoveColor);
        _patrolPending = false;
    }

    /// <summary>With a garrison-capable building selected, U ejects everyone inside.</summary>
    void HandleGarrisonHotkeys()
    {
        var gm = GameManager.Instance;
        var b = gm?.selectedBuilding;
        if (b == null || gm.garrison == null) return;
        if (b.GarrisonCount > 0 && Input.GetKeyDown(KeyCode.U))
            gm.garrison.UngarrisonAll(b);
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

    /// <summary>Consume the attack-move targeting click: send the selection advancing
    /// to the clicked point. Right-click / Esc cancels.</summary>
    void HandleAttackMovePick(GameManager gm)
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { AttackMovePending = false; return; }
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f)) { AttackMovePending = false; return; }
        Vector3 point = hit.point;

        var sel = gm.selection != null ? gm.selection.Selected : null;
        if (sel != null)
            for (int i = 0; i < sel.Count; i++)
            {
                var u = sel[i];
                if (u == null) continue;
                u.attackMove = true;
                u.attackMoveDest = point;
                u.MoveTo(point);
            }
        SpawnMarker(point, AttackColor);
        AttackMovePending = false;
    }

    /// <summary>Public move order for the current selection to a world point. Used by
    /// the minimap right-click; clears any running attack-move and drops a marker.</summary>
    public void MoveSelectedTo(Vector3 point)
    {
        var gm = GameManager.Instance;
        var selected = gm != null && gm.selection != null ? gm.selection.Selected : null;
        if (selected == null || selected.Count == 0) return;
        for (int i = 0; i < selected.Count; i++)
            if (selected[i] != null) selected[i].attackMove = false;
        MoveOrder(selected, point);
        SpawnMarker(point, MoveColor);
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

    // ── Rally flag + line ───────────────────────────────────────────────────────

    GameObject _rallyFlag;
    LineRenderer _rallyLine;
    static readonly Color RallyColor = Prims.Hex(0x39c24a);

    /// <summary>Show a small flag at the selected building's rally point (plus a line
    /// from the building to it, N9.feedback); hide both when no rally-capable building
    /// is selected.</summary>
    void UpdateRallyFlag(GameManager gm)
    {
        var b = gm.selectedBuilding;
        bool show = b != null && b.hasRally;
        if (!show)
        {
            if (_rallyFlag != null && _rallyFlag.activeSelf) _rallyFlag.SetActive(false);
            if (_rallyLine != null && _rallyLine.enabled) _rallyLine.enabled = false;
            return;
        }
        if (_rallyFlag == null)
        {
            _rallyFlag = new GameObject("RallyFlag");
            Prims.Cylinder(_rallyFlag.transform, new Vector3(0, 0.9f, 0), 0.06f, 1.8f,
                Prims.Mat(Prims.Hex(0x4a3520)));
            Prims.Box(_rallyFlag.transform, new Vector3(0.3f, 1.5f, 0f), new Vector3(0.55f, 0.38f, 0.04f),
                Prims.Mat(RallyColor));
        }
        _rallyFlag.transform.position = b.rallyPoint;
        if (!_rallyFlag.activeSelf) _rallyFlag.SetActive(true);

        // N9.feedback: a ground line connecting the building to its rally point.
        if (_rallyLine == null)
        {
            var lgo = new GameObject("RallyLine");
            _rallyLine = lgo.AddComponent<LineRenderer>();
            _rallyLine.useWorldSpace = true;
            _rallyLine.widthMultiplier = 0.12f;
            _rallyLine.material = Prims.UnlitColorMat(RallyColor);
            _rallyLine.startColor = _rallyLine.endColor = RallyColor;
            _rallyLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rallyLine.positionCount = 2;
        }
        _rallyLine.SetPosition(0, b.transform.position + Vector3.up * 0.1f);
        _rallyLine.SetPosition(1, b.rallyPoint + Vector3.up * 0.1f);
        if (!_rallyLine.enabled) _rallyLine.enabled = true;
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
