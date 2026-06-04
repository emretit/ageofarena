using System;
using System.Collections.Generic;
using UnityEngine;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>N11.trig: when does a trigger fire?</summary>
public enum ConditionType
{
    None,
    /// condFloat1 = elapsed seconds required.
    Timer,
    /// condInt1 = teamId, condFloat1 = min unit count.
    OwnUnits,
    /// condInt1 = teamId, condFloat1 = min building count.
    OwnBuildings,
    /// condInt1 = teamId, condInt2 = (int)ResourceKind, condFloat1 = total gathered.
    ResourceGathered,
    /// All enemy teams have no units or buildings left.
    EnemyEliminated,
    /// condInt1 = teamId, condInt2 = (int)TechType.
    TechResearched,
    /// condInt1 = teamId, condInt2 = (int)Age.
    AgeReached,
}

/// <summary>N11.trig: what happens when a trigger fires?</summary>
public enum EffectType
{
    None,
    YouWin,
    YouLose,
    ShowMessage,       // effectStr1 = HUD subtitle text
    ShowObjective,     // effectStr1 = objective text (larger display)
    AddResource,       // effectInt1 = teamId, effectInt2 = (int)ResourceKind, effectFloat1 = amount
    ActivateTrigger,   // effectInt1 = trigger id to enable
    DeactivateTrigger, // effectInt1 = trigger id to disable
    PlaySound,         // effectStr1 = AudioManager.SoundId name
    SetGameOver,       // effectInt1 = winning teamId (-1 = no winner / draw)
}

// ── Serialisable data ─────────────────────────────────────────────────────────

[Serializable]
public class TriggerData
{
    public int  id;
    public bool enabled  = true;
    public bool fired    = false;  // set true after effect fires (oneShot)
    public bool oneShot  = true;   // false = repeating trigger

    // Condition
    public ConditionType conditionType = ConditionType.None;
    public float condFloat1;
    public int   condInt1;
    public int   condInt2;

    // Primary effect
    public EffectType effectType = EffectType.None;
    public string effectStr1 = "";
    public float  effectFloat1;
    public int    effectInt1;
    public int    effectInt2;

    // Optional second effect (chained; fires at the same time)
    public EffectType effect2Type = EffectType.None;
    public string effect2Str1  = "";
    public float  effect2Float1;
    public int    effect2Int1;
}

// ── Runtime ───────────────────────────────────────────────────────────────────

/// <summary>
/// N11.trig: Generic condition→effect trigger runtime.
/// Evaluates each enabled trigger every frame against live GameManager state.
///
/// SP version: effects applied directly (command-log + fixed-step deferred to N3/N15).
/// </summary>
public class TriggerSystem : MonoBehaviour
{
    readonly List<TriggerData> _triggers = new();

    // Accumulated resource tracking (sum of all deposits, not current balance).
    readonly float[] _foodGathered  = new float[GameManager.MaxTeams];
    readonly float[] _woodGathered  = new float[GameManager.MaxTeams];
    readonly float[] _goldGathered  = new float[GameManager.MaxTeams];
    readonly float[] _stoneGathered = new float[GameManager.MaxTeams];

    float _matchTime; // seconds since Build()

    public float MatchTime => _matchTime;

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Add(TriggerData t) => _triggers.Add(t);

    public void LoadSnapshot(List<TriggerData> saved)
    {
        _triggers.Clear();
        if (saved != null) _triggers.AddRange(saved);
    }

    public List<TriggerData> Snapshot() => new(_triggers);

    /// <summary>Called by GatherSystem when a villager deposits resources.</summary>
    public void OnResourceDeposited(int teamId, ResourceKind kind, float amount)
    {
        if (teamId < 0 || teamId >= GameManager.MaxTeams) return;
        switch (kind)
        {
            case ResourceKind.Food:  _foodGathered [teamId] += amount; break;
            case ResourceKind.Wood:  _woodGathered [teamId] += amount; break;
            case ResourceKind.Gold:  _goldGathered [teamId] += amount; break;
            case ResourceKind.Stone: _stoneGathered[teamId] += amount; break;
        }
    }

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.match == null || gm.match.IsOver) return;

        _matchTime += Time.deltaTime;

        foreach (var t in _triggers)
        {
            if (!t.enabled || t.fired) continue;
            if (!EvaluateCondition(t, gm)) continue;

            FireEffect(t.effectType, t.effectStr1, t.effectFloat1, t.effectInt1, t.effectInt2, gm);
            if (t.effect2Type != EffectType.None)
                FireEffect(t.effect2Type, t.effect2Str1, t.effect2Float1, t.effect2Int1, 0, gm);

            if (t.oneShot)
            {
                t.fired   = true;
                t.enabled = false;
            }
        }
    }

    // ── Condition evaluation ───────────────────────────────────────────────────

    bool EvaluateCondition(TriggerData t, GameManager gm) => t.conditionType switch
    {
        ConditionType.Timer           => _matchTime >= t.condFloat1,
        ConditionType.OwnUnits        => CountUnits(t.condInt1, gm) >= (int)t.condFloat1,
        ConditionType.OwnBuildings    => CountBuildings(t.condInt1, gm) >= (int)t.condFloat1,
        ConditionType.ResourceGathered=> GatheredAmount(t.condInt1, (ResourceKind)t.condInt2) >= t.condFloat1,
        ConditionType.EnemyEliminated => AllEnemiesGone(gm),
        ConditionType.TechResearched  => gm.teamTech[t.condInt1]?.Has((TechType)t.condInt2) ?? false,
        ConditionType.AgeReached      => gm.teamTech.Length > t.condInt1 &&
                                         (int)gm.teamTech[t.condInt1].age >= t.condInt2,
        _                             => false,
    };

    int CountUnits(int teamId, GameManager gm)
    {
        int c = 0;
        foreach (var u in gm.units) if (u != null && u.teamId == teamId) c++;
        return c;
    }

    int CountBuildings(int teamId, GameManager gm)
    {
        int c = 0;
        foreach (var b in gm.buildings) if (b != null && b.teamId == teamId) c++;
        return c;
    }

    float GatheredAmount(int teamId, ResourceKind kind) => kind switch
    {
        ResourceKind.Food  => _foodGathered [teamId],
        ResourceKind.Wood  => _woodGathered [teamId],
        ResourceKind.Gold  => _goldGathered [teamId],
        ResourceKind.Stone => _stoneGathered[teamId],
        _                  => 0f,
    };

    bool AllEnemiesGone(GameManager gm)
    {
        for (int t = 1; t < gm.TeamCount; t++)
        {
            foreach (var u in gm.units)    if (u != null && u.teamId == t) return false;
            foreach (var b in gm.buildings) if (b != null && b.teamId == t && b.type != BuildingType.Wall) return false;
        }
        return true;
    }

    // ── Effect dispatch ───────────────────────────────────────────────────────

    void FireEffect(EffectType et, string str1, float f1, int i1, int i2, GameManager gm)
    {
        switch (et)
        {
            case EffectType.YouWin:
                gm.hud?.ShowGameOver(true, str1);
                break;

            case EffectType.YouLose:
                gm.hud?.ShowGameOver(false, str1);
                break;

            case EffectType.ShowMessage:
                gm.hud?.ShowSubtitle(str1, 3.5f);
                break;

            case EffectType.ShowObjective:
                gm.hud?.ShowSubtitle(str1, 5f);
                break;

            case EffectType.AddResource:
                if (i1 >= 0 && i1 < gm.TeamCount)
                {
                    var res = gm.teamRes[i1];
                    switch ((ResourceKind)i2)
                    {
                        case ResourceKind.Food:  res.food  += (int)f1; break;
                        case ResourceKind.Wood:  res.wood  += (int)f1; break;
                        case ResourceKind.Gold:  res.gold  += (int)f1; break;
                        case ResourceKind.Stone: res.stone += (int)f1; break;
                    }
                }
                break;

            case EffectType.ActivateTrigger:
                SetEnabled(i1, true);
                break;

            case EffectType.DeactivateTrigger:
                SetEnabled(i1, false);
                break;

            case EffectType.SetGameOver:
                bool playerWon = i1 == 0;
                gm.hud?.ShowGameOver(playerWon, str1);
                break;

            case EffectType.PlaySound:
                if (System.Enum.TryParse<AudioManager.SoundId>(str1, out var sid))
                    AudioManager.Play(sid);
                break;
        }
    }

    void SetEnabled(int id, bool value)
    {
        foreach (var t in _triggers)
            if (t.id == id) { t.enabled = value; break; }
    }
}
