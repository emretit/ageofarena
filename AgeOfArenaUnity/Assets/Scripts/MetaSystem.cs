using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// N13.meta: Tech-tree viewer, local achievements, and seeded daily challenge.
///
/// Tech-tree viewer: pause menu → "Teknoloji" button opens a civ-filtered scrollable
/// list of all researched/available/locked techs with cost + status.
///
/// Achievements (PlayerPrefs): first castle built, first Imperial age, 50 kills, etc.
///
/// Daily challenge: date-seeded deterministic start conditions shown in CivSelectScreen
/// (e.g. start with extra gold, or under attack immediately).
/// </summary>
public static class MetaSystem
{
    // ── Achievements ──────────────────────────────────────────────────────────

    public enum Achievement
    {
        FirstBlood,      // first enemy unit kill
        Builder,         // built a Castle
        Emperor,         // reached Imperial Age
        Veteran,         // a unit reached Elite rank
        KillStreak50,    // 50 total kills in one game
        EcoMaster,       // accumulated 5000 gold in one game
        Defender,        // repelled an AI attack (TC survived with <20% HP)
        Diplomat,        // researched a Diplomacy tech
        AllCivs,         // played as every civ at least once
    }

    const string Prefix = "Ach_";

    public static bool IsUnlocked(Achievement a)
        => PlayerPrefs.GetInt(Prefix + a, 0) == 1;

    public static void Unlock(Achievement a)
    {
        if (IsUnlocked(a)) return;
        PlayerPrefs.SetInt(Prefix + a, 1);
        PlayerPrefs.Save();
        // Queue a HUD notification (checked each frame by HUD).
        _pendingAchievement = a;
    }

    static Achievement? _pendingAchievement;

    /// <summary>Consume the pending achievement toast (called by HUD.Update).</summary>
    public static bool TryTakeAchievement(out Achievement a)
    {
        if (_pendingAchievement.HasValue)
        {
            a = _pendingAchievement.Value;
            _pendingAchievement = null;
            return true;
        }
        a = default;
        return false;
    }

    public static string DisplayName(Achievement a) => a switch
    {
        Achievement.FirstBlood  => "İlk Kan",
        Achievement.Builder     => "Kale İnşaatçısı",
        Achievement.Emperor     => "İmparator",
        Achievement.Veteran     => "Efsane Savaşçı",
        Achievement.KillStreak50=> "Katliamcı (50 kill)",
        Achievement.EcoMaster   => "Ekonomi Ustası",
        Achievement.Defender    => "Kale Koruyucusu",
        Achievement.Diplomat    => "Diplomat",
        Achievement.AllCivs     => "Dünya Fatihi",
        _                       => a.ToString(),
    };

    public static int UnlockedCount()
    {
        int n = 0;
        foreach (Achievement a in System.Enum.GetValues(typeof(Achievement)))
            if (IsUnlocked(a)) n++;
        return n;
    }

    // ── Achievement tracking (called from game systems) ───────────────────────

    static int _killCount;
    static float _peakGold;
    static bool _defenderEligible; // TC dropped below 20%, now repelled

    public static void OnKill(int teamId)
    {
        if (teamId != 0) return;
        _killCount++;
        if (_killCount == 1)  Unlock(Achievement.FirstBlood);
        if (_killCount >= 50) Unlock(Achievement.KillStreak50);
    }

    public static void OnBuildingComplete(BuildingType type, int teamId)
    {
        if (teamId != 0) return;
        if (type == BuildingType.Castle) Unlock(Achievement.Builder);
    }

    public static void OnAgeAdvanced(int teamId, Age age)
    {
        if (teamId != 0) return;
        if (age == Age.Imperial) Unlock(Achievement.Emperor);
    }

    public static void OnVeteranRankUp(UnitEntity u)
    {
        if (u.teamId != 0) return;
        if (u.veteranRank >= 2) Unlock(Achievement.Veteran);
    }

    public static void OnGoldGain(float total)
    {
        _peakGold = Mathf.Max(_peakGold, total);
        if (_peakGold >= 5000f) Unlock(Achievement.EcoMaster);
    }

    public static void OnCivPlayed(Civilization civ)
    {
        if (civ == Civilization.None) return;
        PlayerPrefs.SetInt("PlayedCiv_" + (int)civ, 1);
        // Check all civs
        bool allPlayed = true;
        foreach (var row in CivilizationDefs.Playable())
            if (PlayerPrefs.GetInt("PlayedCiv_" + (int)row.civ, 0) == 0) { allPlayed = false; break; }
        if (allPlayed) Unlock(Achievement.AllCivs);
    }

    public static void OnTCLowHP()     { _defenderEligible = true; }
    public static void OnAttackRepelled()
    {
        if (_defenderEligible) { Unlock(Achievement.Defender); _defenderEligible = false; }
    }

    public static void OnResearchComplete(TechType tech, int teamId)
    {
        if (teamId != 0) return;
        if (tech == TechType.FeudalAge || tech == TechType.CastleAge || tech == TechType.ImperialAge) return;
        Unlock(Achievement.Diplomat); // any non-age tech counts as "diplomacy" (simplified)
    }

    public static void Reset()
    {
        _killCount = 0; _peakGold = 0f; _defenderEligible = false;
    }

    // ── Daily challenge ────────────────────────────────────────────────────────

    public enum ChallengeType
    {
        Abundant,    // start with 3000 food/wood/gold
        Poverty,     // start with 50 of each resource
        Assault,     // AI attacks from minute 1 (no aggro timer)
        Pacifist,    // achieve 1000 score without military (check at game-over)
        Marathon,    // 60-minute time limit
    }

    /// <summary>Returns today's challenge (date-seeded deterministic).</summary>
    public static ChallengeType TodayChallenge()
    {
        // Date string YYYYMMDD as seed
        string dateStr = System.DateTime.UtcNow.ToString("yyyyMMdd");
        int seed = 0;
        foreach (char c in dateStr) seed = seed * 31 + c;
        int idx = Mathf.Abs(seed) % System.Enum.GetValues(typeof(ChallengeType)).Length;
        return (ChallengeType)idx;
    }

    public static string ChallengeName(ChallengeType c) => c switch
    {
        ChallengeType.Abundant  => "Bolluk — Kaynaklarla başla",
        ChallengeType.Poverty   => "Yoksulluk — 50 kaynak ile başla",
        ChallengeType.Assault   => "Baskın — AI hemen saldırır",
        ChallengeType.Pacifist  => "Pasifist — Asker olmadan 1000 puan",
        ChallengeType.Marathon  => "Maraton — 60 dakika hayatta kal",
        _                       => c.ToString(),
    };

    public static void ApplyDailyChallenge(GameManager gm, ChallengeType c)
    {
        if (gm == null) return;
        var res = gm.resources;
        switch (c)
        {
            case ChallengeType.Abundant:
                res.food  = Mathf.Max(res.food,  3000);
                res.wood  = Mathf.Max(res.wood,  3000);
                res.gold  = Mathf.Max(res.gold,  3000);
                break;
            case ChallengeType.Poverty:
                res.food = 50; res.wood = 50; res.gold = 50; res.stone = 50;
                break;
            case ChallengeType.Marathon:
                var match = gm.gameObject.GetComponent<MatchSystem>();
                if (match != null) match.MatchTimeLimit = 3600f; // 60 min
                break;
        }
    }

    // ── Tech-tree viewer (built into HUD as a scrollable panel) ──────────────

    public static List<(TechDef def, bool researched, bool locked)> GetTechList(
        Civilization civ, TechState tech)
    {
        var result = new List<(TechDef, bool, bool)>();
        var all    = TechDefs.All();
        for (int i = 0; i < all.Length; i++)
        {
            var d = all[i];
            // Skip age-advance techs (they're shown in HUD directly)
            if (d.type == TechType.FeudalAge || d.type == TechType.CastleAge
                || d.type == TechType.ImperialAge) continue;
            // Skip civs that this civ can't access
            if (d.requiredCiv != Civilization.None && d.requiredCiv != civ) continue;
            bool res    = tech != null && tech.Has(d.type);
            bool locked = civ != Civilization.None && CivilizationDefs.IsTechDenied(civ, d.type);
            result.Add((d, res, locked));
        }
        return result;
    }
}
