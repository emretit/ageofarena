using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-team enemy brain. Manages an economy (villagers gather resources) and
/// a military loop (trains units when it can afford them, rushes when army is large
/// enough). Each AI team starts with 3 villagers (spawned by WorldRoot) and uses
/// its own <see cref="ResourceManager"/> slot in <c>GameManager.teamRes</c>.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    const float SpawnInterval      = 15f;
    const float AssessInterval     = 3f;
    const float GatherCheckInterval = 6f;
    const int   ArmyCap            = 12;
    const int   RushThreshold      = 8;
    const int   VillagerTarget     = 3;

    // Unit costs
    const int MilitiaCostFood = 60;
    const int ArcherCostWood  = 35;
    const int ArcherCostGold  = 25;
    const int VillagerCostFood = 50;

    int _teamId;
    Color _teamColor;
    Vector3 _home;
    Transform _unitsRoot;

    float _spawnTimer  = 15f;  // delay first spawn so player can establish economy
    float _assessTimer = 2f;
    float _gatherTimer = 3f;
    bool  _spawnArcher;

    ResourceManager _res;

    public void Init(int teamId, Color teamColor, Vector3 home, Transform unitsRoot)
    {
        _teamId     = teamId;
        _teamColor  = teamColor;
        _home       = home;
        _unitsRoot  = unitsRoot;
    }

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null) _res = gm.teamRes[_teamId];
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || _res == null) return;
        float dt = Time.deltaTime;

        if ((_spawnTimer  -= dt) <= 0f) { _spawnTimer  = SpawnInterval;       TrySpawn(gm); }
        if ((_assessTimer -= dt) <= 0f) { _assessTimer = AssessInterval;       Assess(gm); }
        if ((_gatherTimer -= dt) <= 0f) { _gatherTimer = GatherCheckInterval;  EconomyTick(gm); }
    }

    // ── Military ─────────────────────────────────────────────────────────────

    void TrySpawn(GameManager gm)
    {
        if (CountArmy(gm) >= ArmyCap) return;

        bool canArcher  = _spawnArcher && _res.CanAfford(0, ArcherCostWood, ArcherCostGold, 0);
        bool canMilitia = _res.CanAfford(MilitiaCostFood, 0, 0, 0);
        if (!canArcher && !canMilitia) return;

        Vector3 fwd   = _home.sqrMagnitude > 0.01f ? (-_home).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 pos   = _home + fwd * 4f + right * Random.Range(-2.5f, 2.5f);

        UnitEntity u;
        if (canArcher)
        {
            _res.Deduct(0, ArcherCostWood, ArcherCostGold, 0);
            u = UnitFactory.Archer(_unitsRoot, pos, _teamColor);
            _spawnArcher = false;
        }
        else
        {
            _res.Deduct(MilitiaCostFood, 0, 0, 0);
            u = UnitFactory.Militia(_unitsRoot, pos, _teamColor);
            _spawnArcher = true;
        }
        u.teamId = _teamId;
        gm.RegisterUnit(u);
    }

    void Assess(GameManager gm)
    {
        var army = new List<UnitEntity>();
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && IsMilitary(u)) army.Add(u);
        }
        if (army.Count < RushThreshold) return;

        var target = FindNearestEnemy(gm);
        if (target == null) return;

        for (int i = 0; i < army.Count; i++)
        {
            var u = army[i];
            if (u.attackTarget == null || !u.attackTarget.IsAlive)
                u.AttackOrder(target);
        }
    }

    // ── Economy ───────────────────────────────────────────────────────────────

    void EconomyTick(GameManager gm)
    {
        AssignVillagersToGather(gm);
        TryTrainVillager(gm);
    }

    void AssignVillagersToGather(GameManager gm)
    {
        var gather = gm.gather;
        if (gather == null) return;

        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId != _teamId || u.type != UnitType.Villager) continue;
            if (u.state != UnitState.Idle) continue;

            // Prefer wood for military production, then food, then gold
            var node = FindNearestNode(gm, u.transform.position, ResourceKind.Wood)
                    ?? FindNearestNode(gm, u.transform.position, ResourceKind.Food)
                    ?? FindNearestNode(gm, u.transform.position, ResourceKind.Gold);
            if (node != null) gather.AssignGather(u, node);
        }
    }

    void TryTrainVillager(GameManager gm)
    {
        if (!_res.CanAfford(VillagerCostFood, 0, 0, 0)) return;

        int count = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && u.type == UnitType.Villager) count++;
        }
        if (count >= VillagerTarget) return;

        _res.Deduct(VillagerCostFood, 0, 0, 0);
        Vector3 fwd = _home.sqrMagnitude > 0.01f ? (-_home).normalized : Vector3.forward;
        Vector3 pos = _home - fwd * 2f + Vector3.right * Random.Range(-1.5f, 1.5f);
        var v = UnitFactory.Villager(_unitsRoot, pos, _teamColor);
        v.teamId = _teamId;
        gm.RegisterUnit(v);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    ResourceNode FindNearestNode(GameManager gm, Vector3 pos, ResourceKind kind)
    {
        ResourceNode best   = null;
        float        bestSq = float.MaxValue;
        var          nodes  = gm.nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || n.Depleted || n.kind != kind || !n.HasRoom) continue;
            float dx = n.transform.position.x - pos.x;
            float dz = n.transform.position.z - pos.z;
            float sq = dx * dx + dz * dz;
            if (sq < bestSq) { bestSq = sq; best = n; }
        }
        return best;
    }

    int CountArmy(GameManager gm)
    {
        int n = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && IsMilitary(u)) n++;
        }
        return n;
    }

    IDamageable FindNearestEnemy(GameManager gm)
    {
        IDamageable best   = null;
        float       bestSq = float.MaxValue;

        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId == _teamId) continue;
            float sq = (u.transform.position - _home).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = u; }
        }
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || b.teamId == _teamId) continue;
            float sq = (b.transform.position - _home).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        return best;
    }

    static bool IsMilitary(UnitEntity u) =>
        u.type == UnitType.Militia || u.type == UnitType.Archer || u.type == UnitType.Cavalry;
}
