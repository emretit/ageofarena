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

    // N6.form: formation type cycling (F key)
    public enum FormationType { Grid, Line, Staggered, Wedge }
    public static FormationType CurrentFormation = FormationType.Grid;
    static readonly string[] FormationNames = { "Izgara", "Hat", "Sıralı", "V" };

    Camera _cam;

    // Waypoint queue visualiser: one LineRenderer per selected unit showing its pending path.
    readonly List<LineRenderer> _waypointLines = new List<LineRenderer>();
    static readonly Color WaypointColor = new Color(0.2f, 0.8f, 1f, 0.7f);

    /// <summary>True while waiting for the player to click an attack-move destination.</summary>
    public bool AttackMovePending { get; private set; }

    void Start() => _cam = Camera.main;

    void LateUpdate()
    {
        var sel = GameManager.Instance?.selection?.Selected;

        // Grow pool to cover all selected units.
        while (_waypointLines.Count < (sel?.Count ?? 0))
        {
            var go = new GameObject("WaypointLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = Prims.UnlitColorMat(WaypointColor);
            lr.startWidth = lr.endWidth = 0.12f;
            lr.useWorldSpace = true;
            _waypointLines.Add(lr);
        }

        // Disable all first; re-enable only those that have queue points.
        foreach (var lr in _waypointLines) lr.enabled = false;

        if (sel == null) return;
        int lineIdx = 0;
        for (int i = 0; i < sel.Count && lineIdx < _waypointLines.Count; i++)
        {
            var u = sel[i];
            if (u == null || u.moveQueue.Count == 0) continue;
            var lr = _waypointLines[lineIdx++];
            lr.enabled = true;
            var pts = u.moveQueue.ToArray();
            lr.positionCount = pts.Length + 1;
            lr.SetPosition(0, u.transform.position + Vector3.up * 0.2f);
            for (int k = 0; k < pts.Length; k++)
                lr.SetPosition(k + 1, pts[k] + Vector3.up * 0.2f);
        }
    }

    /// <summary>Enter attack-move targeting: the next ground click sends every selected
    /// unit advancing to that point, engaging enemies on the way. Called by the HUD.</summary>
    public void BeginAttackMove()
    {
        var sel = GameManager.Instance?.selection?.Selected;
        if (sel != null && sel.Exists(u => u != null)) AttackMovePending = true;
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

        // N6.form: F = cycle formation type; H = Town Bell
        if (Input.GetKeyDown(KeyCode.F))
        {
            CurrentFormation = (FormationType)(((int)CurrentFormation + 1) % 4);
            gm.hud?.ShowSubtitle(FormationNames[(int)CurrentFormation]);
        }
        if (Input.GetKeyDown(KeyCode.H)) TownBell(gm);

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
            if (ordered)
            {
                SpawnMarker(enemy.Transform.position, AttackColor);
                // N3.cmdlog: record attack command
                int targetId = (enemy is UnitEntity ue) ? ue.unitId
                             : (enemy is BuildingEntity be) ? be.GetInstanceID() : 0;
                GameManager.Instance?.cmdRecorder?.Record(
                    CommandType.Attack, UnitIds(selected), intParam1: targetId,
                    x: enemy.Transform.position.x, z: enemy.Transform.position.z);
            }
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
            if (any)
            {
                SpawnMarker(node.transform.position, GatherColor);
                // N3.cmdlog: record gather command
                GameManager.Instance?.cmdRecorder?.Record(
                    CommandType.Gather, UnitIds(selected),
                    intParam1: node.GetInstanceID(),
                    x: node.transform.position.x, z: node.transform.position.z);
            }
            return;
        }

        if (hit.collider.gameObject.name == "Ground")
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift)
                EnqueueMove(selected, hit.point);
            else
                MoveOrder(selected, hit.point);
            SpawnMarker(hit.point, MoveColor);
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
            BeginPatrol();
    }

    bool _patrolPending;

    void BeginPatrol() => _patrolPending = true;

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
        if (b.GarrisonCount > 0 && Hotkeys.Down(HotkeyAction.Ungarrison))
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
        // N3.cmdlog: record move command
        GameManager.Instance?.cmdRecorder?.Record(
            CommandType.Move, UnitIds(selected), x: point.x, z: point.z);

        if (n == 1)
        {
            selected[0].Stop();
            selected[0].MoveTo(point);
            return;
        }

        // N6.form: apply formation-type-specific slot layout.
        var offsets = FormationOffsets(n, CurrentFormation);
        for (int i = 0; i < n; i++)
        {
            var u = selected[i];
            if (u == null) continue;
            u.Stop();
            u.MoveTo(point + offsets[i]);
        }
    }

    /// <summary>Shift+right-click: append <paramref name="point"/> to each unit's waypoint queue
    /// instead of issuing an immediate move order. The unit walks through queued waypoints in
    /// order after finishing its current move. Clears on any non-queued order (Stop, attack, etc.).</summary>
    void EnqueueMove(List<UnitEntity> selected, Vector3 point)
    {
        var offsets = selected.Count == 1 ? new[] { Vector3.zero } : FormationOffsets(selected.Count, CurrentFormation);
        for (int i = 0; i < selected.Count; i++)
        {
            var u = selected[i];
            if (u == null) continue;
            u.moveQueue.Enqueue(point + offsets[i]);
            // If already idle, start walking immediately toward the first queued point.
            if (u.state == UnitState.Idle && u.moveQueue.Count == 1)
                u.MoveTo(u.moveQueue.Dequeue());
        }
        GameManager.Instance?.cmdRecorder?.Record(
            CommandType.Move, UnitIds(selected), x: point.x, z: point.z);
    }

    static int[] UnitIds(List<UnitEntity> units)
    {
        var ids = new int[units.Count];
        for (int i = 0; i < units.Count; i++)
            ids[i] = units[i] != null ? units[i].unitId : -1;
        return ids;
    }

    /// <summary>Compute n world-space XZ offsets for the chosen formation type.</summary>
    public static Vector3[] FormationOffsets(int n, FormationType ft)
    {
        var offs = new Vector3[n];
        float s = FormationSpacing;
        switch (ft)
        {
            case FormationType.Line:
                // Single long row.
                for (int i = 0; i < n; i++)
                    offs[i] = new Vector3((i - (n - 1) / 2f) * s, 0f, 0f);
                break;
            case FormationType.Staggered:
                // Two-column stagger (AoE2 "standard" default).
                for (int i = 0; i < n; i++)
                {
                    int col = i % 2;
                    int row = i / 2;
                    offs[i] = new Vector3((col - 0.5f) * s, 0f, -row * s);
                }
                break;
            case FormationType.Wedge:
                // V-shape: front solo, then expanding rows.
                int front = 1;
                int placed = 0, rowIdx = 0;
                while (placed < n)
                {
                    int inRow = Mathf.Min(front, n - placed);
                    for (int c = 0; c < inRow; c++)
                        offs[placed++] = new Vector3((c - (inRow - 1) / 2f) * s, 0f, -rowIdx * s);
                    front += 2; rowIdx++;
                }
                break;
            default: // Grid
                int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
                for (int i = 0; i < n; i++)
                    offs[i] = new Vector3((i % cols - (cols - 1) / 2f) * s, 0f, -(i / cols) * s);
                break;
        }
        return offs;
    }

    // ── Town Bell (N6.form) ────────────────────────────────────────────────────

    static bool _townBellActive;

    /// <summary>Toggle: first press = all player villagers garrison into nearest TC/tower/castle.
    /// Second press = all garrison buildings ungarrison.</summary>
    static void TownBell(GameManager gm)
    {
        if (gm == null) return;
        _townBellActive = !_townBellActive;
        gm.hud?.ShowSubtitle(_townBellActive ? "⚑ Kule Çanı — Köylüler garnizona!" : "⚑ Kule Çanı — Geri dön!");

        if (_townBellActive)
        {
            // Find all team-0 garrison-capable buildings sorted by distance to base.
            var garrisonBuildings = new System.Collections.Generic.List<BuildingEntity>();
            for (int i = 0; i < gm.buildings.Count; i++)
            {
                var b = gm.buildings[i];
                if (b != null && b.teamId == 0 && b.GarrisonCapacity > 0 && b.hp > 0f && !b.underConstruction)
                    garrisonBuildings.Add(b);
            }
            if (garrisonBuildings.Count == 0) return;

            // Send all player villagers to garrison the nearest eligible building.
            for (int i = 0; i < gm.units.Count; i++)
            {
                var u = gm.units[i];
                if (u == null || u.teamId != 0 || u.type != UnitType.Villager || u.isGarrisoned) continue;
                // Find nearest garrison building.
                BuildingEntity best = null;
                float bestDist = float.MaxValue;
                foreach (var b in garrisonBuildings)
                {
                    if (b.GarrisonCount >= b.GarrisonCapacity) continue;
                    float d = (b.transform.position - u.transform.position).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = b; }
                }
                if (best != null) u.GarrisonOrder(best);
            }
        }
        else
        {
            // Release all garrisoned units from all team-0 buildings.
            for (int i = 0; i < gm.buildings.Count; i++)
            {
                var b = gm.buildings[i];
                if (b != null && b.teamId == 0 && b.GarrisonCount > 0)
                    gm.garrison?.UngarrisonAll(b);
            }
        }
    }

    // ── Rally flag + line ────────────────────────────────────────────────────────

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
