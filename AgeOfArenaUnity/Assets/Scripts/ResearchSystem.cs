using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-building research queue for the player (team 0). One technology researches
/// at a time per building. Deducts resources on enqueue, advances a timer each
/// frame, and applies the tech to <c>GameManager.teamTech[0]</c> when done.
/// Mirrors <see cref="TrainingQueue"/>. AI teams research instantly via the static
/// <see cref="Apply"/> helper (they deduct their own resources first).
/// </summary>
public class ResearchSystem : MonoBehaviour
{
    class ResearchItem
    {
        public TechType type;
        public float totalTime;
        public float elapsed;
    }

    readonly Dictionary<BuildingEntity, ResearchItem> _active = new();

    GameManager GM => GameManager.Instance;

    public bool IsResearching(BuildingEntity b) => b != null && _active.ContainsKey(b);

    /// <summary>Start researching a tech at a building. Returns false if busy,
    /// already researched, or unaffordable.</summary>
    public bool Enqueue(BuildingEntity b, TechDef def)
    {
        if (b == null || _active.ContainsKey(b)) return false;
        var tech = GM.tech;                 // player = team 0
        if (tech.Has(def.type)) return false;
        var rm = GM.resources;
        if (!rm.CanAfford(def.food, def.wood, def.gold, def.stone)) return false;

        rm.Deduct(def.food, def.wood, def.gold, def.stone);
        _active[b] = new ResearchItem { type = def.type, totalTime = def.researchTime };
        return true;
    }

    /// <returns>0–1 progress of the active research, or -1 if none.</returns>
    public float GetProgress(BuildingEntity b)
        => _active.TryGetValue(b, out var it) ? it.elapsed / it.totalTime : -1f;

    public TechType GetActiveTech(BuildingEntity b)
        => _active.TryGetValue(b, out var it) ? it.type : default;

    /// <summary>Cancel the building's active research and refund its cost.</summary>
    public void CancelActive(BuildingEntity b)
    {
        if (b == null || !_active.TryGetValue(b, out var it)) return;
        var def = TechDefs.Get(it.type);
        GM.resources.Gain(ResourceKind.Food, def.food);
        GM.resources.Gain(ResourceKind.Wood, def.wood);
        GM.resources.Gain(ResourceKind.Gold, def.gold);
        GM.resources.Gain(ResourceKind.Stone, def.stone);
        _active.Remove(b);
    }

    public void Tick(float dt)
    {
        if (_active.Count == 0) return;

        List<BuildingEntity> done = null;
        foreach (var kvp in _active)
        {
            if (kvp.Key == null) { (done ??= new()).Add(kvp.Key); continue; }
            var it = kvp.Value;
            it.elapsed += dt;
            if (it.elapsed >= it.totalTime)
            {
                Apply(it.type, 0);
                (done ??= new()).Add(kvp.Key);
            }
        }
        if (done != null)
            for (int i = 0; i < done.Count; i++) _active.Remove(done[i]);
    }

    /// <summary>
    /// Apply a completed tech to a team's <see cref="TechState"/>. Age advances bump
    /// the age; hp upgrades raise max-hp (and current hp) of that team's live units
    /// of the affected type by the delta so existing armies benefit immediately.
    /// Attack/range/gather bonuses need no action — they're read live from TechState.
    /// </summary>
    public static void Apply(TechType type, int teamId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var tech = gm.teamTech[teamId];
        if (tech.Has(type)) return;

        // Capture each unit type's hp bonus before the tech lands so we can bump the
        // max-hp (and current hp) of already-spawned units by exactly the delta.
        var types = (UnitType[])System.Enum.GetValues(typeof(UnitType));
        var before = new float[types.Length];
        for (int i = 0; i < types.Length; i++) before[i] = tech.HpBonus(types[i]);

        tech.Mark(type);
        if (type == TechType.FeudalAge)
        {
            tech.age = Age.Feudal;
            GameEvents.FireAgeAdvanced(teamId, Age.Feudal);
        }
        else if (type == TechType.CastleAge)
        {
            tech.age = Age.Castle;
            GameEvents.FireAgeAdvanced(teamId, Age.Castle);
        }
        else if (type == TechType.ImperialAge)
        {
            tech.age = Age.Imperial;
            GameEvents.FireAgeAdvanced(teamId, Age.Imperial);
        }
        else
        {
            GameEvents.FireResearchCompleted(teamId, type);
        }

        // Per-type hp delta (indexable since UnitType values are contiguous 0..N).
        var delta = new float[types.Length];
        bool any = false;
        for (int i = 0; i < types.Length; i++)
        {
            delta[i] = tech.HpBonus(types[i]) - before[i];
            if (delta[i] > 0f) any = true;
        }
        if (!any) return;

        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId != teamId) continue;
            float d = delta[(int)u.type];
            if (d > 0f) { u.maxHp += d; u.hp += d; }
        }
    }
}
