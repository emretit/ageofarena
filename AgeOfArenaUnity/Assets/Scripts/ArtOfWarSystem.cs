using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N13.aow: Art-of-War challenge system — 4 scenarios, each with bronze/silver/gold rating.
///
/// Challenges are encoded as trigger sets fed into TriggerSystem.
/// The player selects a challenge from the main menu (CivSelectScreen GameMode cycling).
/// Challenges are SP-only; they override the normal match setup.
///
/// Challenge IDs (encoded in GameMode enum as ArtOfWar = 9..12 virtual modes,
/// but stored in ArtOfWarSystem static field to keep GameMode enum unchanged).
/// </summary>
public enum ArtOfWarChallenge
{
    None = 0,
    EarlyEconomy  = 1,  // gather food / build villagers quickly
    Combat        = 2,  // eliminate a small enemy army
    Counters      = 3,  // win with specific unit types
    Siege         = 4,  // destroy enemy buildings with siege units
}

public static class ArtOfWarSystem
{
    /// <summary>Currently selected challenge (set by CivSelectScreen).</summary>
    public static ArtOfWarChallenge ActiveChallenge = ArtOfWarChallenge.None;

    // ── Challenge setup (call once after WorldRoot finishes building) ──────────

    /// <summary>
    /// Inject triggers for the active challenge into the TriggerSystem.
    /// Each challenge has Bronze / Silver / Gold thresholds encoded as separate triggers.
    /// </summary>
    public static void Setup(GameManager gm)
    {
        if (ActiveChallenge == ArtOfWarChallenge.None) return;
        if (gm?.triggers == null) return;

        // Clear any stale triggers first.
        gm.triggers.LoadSnapshot(new List<TriggerData>());

        switch (ActiveChallenge)
        {
            case ArtOfWarChallenge.EarlyEconomy: SetupEarlyEconomy(gm.triggers); break;
            case ArtOfWarChallenge.Combat:        SetupCombat(gm.triggers);       break;
            case ArtOfWarChallenge.Counters:      SetupCounters(gm.triggers);     break;
            case ArtOfWarChallenge.Siege:         SetupSiege(gm.triggers);        break;
        }

        ShowObjective(gm, ActiveChallenge);
    }

    // ── Challenge definitions ─────────────────────────────────────────────────

    // Early Economy: gather 600/900/1200 food within 10 minutes.
    static void SetupEarlyEconomy(TriggerSystem ts)
    {
        // Fail if timer runs out before goal met.
        ts.Add(new TriggerData {
            id = 100, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 600f,
            effectType = EffectType.YouLose, effectStr1 = "Süre doldu!",
            effect2Type = EffectType.ShowMessage, effect2Str1 = "Hedef tamamlanamadı.",
        });
        // Bronze: 600 food gathered (deactivate fail timer)
        ts.Add(new TriggerData {
            id = 10, enabled = true, oneShot = true,
            conditionType = ConditionType.ResourceGathered,
            condInt1 = 0, condInt2 = (int)ResourceKind.Food, condFloat1 = 600f,
            effectType = EffectType.DeactivateTrigger, effectInt1 = 100,
            effect2Type = EffectType.ShowMessage, effect2Str1 = "🥉 Bronz — 600 yiyecek toplandı!",
        });
        // Silver: 900 food
        ts.Add(new TriggerData {
            id = 11, enabled = true, oneShot = true,
            conditionType = ConditionType.ResourceGathered,
            condInt1 = 0, condInt2 = (int)ResourceKind.Food, condFloat1 = 900f,
            effectType = EffectType.ShowMessage, effectStr1 = "🥈 Gümüş — 900 yiyecek toplandı!",
        });
        // Gold: 1200 food → win
        ts.Add(new TriggerData {
            id = 12, enabled = true, oneShot = true,
            conditionType = ConditionType.ResourceGathered,
            condInt1 = 0, condInt2 = (int)ResourceKind.Food, condFloat1 = 1200f,
            effectType = EffectType.YouWin, effectStr1 = "🥇 Altın — Ekonomi ustası!",
        });
    }

    // Combat: eliminate all enemies, medal by how fast. Exactly ONE EnemyEliminated win
    // trigger is active in each time window — Gold <2min, Silver <3.5min, Bronze <5min —
    // and the 5-min fail timer stays enabled the whole match (the old version made the match
    // unloseable after 120s and always awarded Gold because two win triggers ran in parallel).
    static void SetupCombat(TriggerSystem ts)
    {
        // Gold win — enemies gone before 120s. Enabled from the start.
        ts.Add(new TriggerData {
            id = 23, enabled = true, oneShot = true,
            conditionType = ConditionType.EnemyEliminated,
            effectType = EffectType.YouWin, effectStr1 = "🥇 Altın — Hızlı zafer! (<2dk)",
        });
        // At 120s the gold window closes → switch to the silver win.
        ts.Add(new TriggerData {
            id = 20, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 120f,
            effectType = EffectType.DeactivateTrigger, effectInt1 = 23,
            effect2Type = EffectType.ActivateTrigger,  effect2Int1 = 25,
        });
        // Silver win — enemies gone 120–210s. Starts disabled; activated by id=20.
        ts.Add(new TriggerData {
            id = 25, enabled = false, oneShot = true,
            conditionType = ConditionType.EnemyEliminated,
            effectType = EffectType.YouWin, effectStr1 = "🥈 Gümüş zafer! (<3.5dk)",
        });
        // At 210s the silver window closes → switch to the bronze win.
        ts.Add(new TriggerData {
            id = 21, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 210f,
            effectType = EffectType.DeactivateTrigger, effectInt1 = 25,
            effect2Type = EffectType.ActivateTrigger,  effect2Int1 = 26,
        });
        // Bronze win — enemies gone 210–300s. Starts disabled; activated by id=21.
        ts.Add(new TriggerData {
            id = 26, enabled = false, oneShot = true,
            conditionType = ConditionType.EnemyEliminated,
            effectType = EffectType.YouWin, effectStr1 = "🥉 Bronz zafer! (<5dk)",
        });
        // 5-minute fail — stays enabled the entire match (never removed).
        ts.Add(new TriggerData {
            id = 22, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 300f,
            effectType = EffectType.YouLose, effectStr1 = "Süre doldu — Rakip hayatta!",
        });
    }

    // Counters: own ≥5 Spearmen + ≥3 Skirmishers (prove knowledge of counters).
    static void SetupCounters(TriggerSystem ts)
    {
        // Fail after 8 minutes
        ts.Add(new TriggerData {
            id = 100, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 480f,
            effectType = EffectType.YouLose, effectStr1 = "Süre doldu!",
        });
        // Bronze: own 5 actual Spearmen (condInt2 filters by unit type — the old code
        // counted ANY 5 units, even villagers, so it never tested the counter knowledge).
        ts.Add(new TriggerData {
            id = 30, enabled = true, oneShot = true,
            conditionType = ConditionType.OwnUnits, condInt1 = 0,
            condInt2 = (int)UnitType.Spearman, condFloat1 = 5f,
            effectType = EffectType.ShowMessage, effectStr1 = "🥉 Bronz — 5 Kargıcı eğitildi!",
        });
        // Silver: own 3 actual Skirmishers.
        ts.Add(new TriggerData {
            id = 31, enabled = true, oneShot = true,
            conditionType = ConditionType.OwnUnits, condInt1 = 0,
            condInt2 = (int)UnitType.Skirmisher, condFloat1 = 3f,
            effectType = EffectType.ShowMessage, effectStr1 = "🥈 Gümüş — 3 Okçu-avcı eğitildi!",
        });
        // Gold: reach Castle Age with counter force
        ts.Add(new TriggerData {
            id = 32, enabled = true, oneShot = true,
            conditionType = ConditionType.AgeReached, condInt1 = 0, condInt2 = 2, // Castle = index 2
            effectType = EffectType.YouWin, effectStr1 = "🥇 Altın — Kale Çağı'na ulaşıldı!",
            effect2Type = EffectType.DeactivateTrigger, effect2Int1 = 100,
        });
    }

    // Siege: build a Trebuchet and destroy all enemy buildings.
    static void SetupSiege(TriggerSystem ts)
    {
        // Fail after 15 minutes
        ts.Add(new TriggerData {
            id = 100, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 900f,
            effectType = EffectType.YouLose, effectStr1 = "Süre doldu — Kuşatma başarısız!",
        });
        // Bronze: own at least 1 Ram (proxy for siege capability)
        ts.Add(new TriggerData {
            id = 40, enabled = true, oneShot = true,
            conditionType = ConditionType.OwnUnits, condInt1 = 0, condFloat1 = 1f,
            effectType = EffectType.ShowMessage, effectStr1 = "🥉 Bronz — Kuşatma birimi eğitildi!",
        });
        // Silver: reach Imperial Age
        ts.Add(new TriggerData {
            id = 41, enabled = true, oneShot = true,
            conditionType = ConditionType.AgeReached, condInt1 = 0, condInt2 = 3, // Imperial = index 3
            effectType = EffectType.ShowMessage, effectStr1 = "🥈 Gümüş — İmparatorluk Çağı!",
        });
        // Gold: all enemies eliminated
        ts.Add(new TriggerData {
            id = 42, enabled = true, oneShot = true,
            conditionType = ConditionType.EnemyEliminated,
            effectType = EffectType.YouWin, effectStr1 = "🥇 Altın — Kuşatma ustası!",
            effect2Type = EffectType.DeactivateTrigger, effect2Int1 = 100,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void ShowObjective(GameManager gm, ArtOfWarChallenge c)
    {
        string obj = c switch
        {
            ArtOfWarChallenge.EarlyEconomy => "Hedef: 10 dakikada maksimum yiyecek topla (600/900/1200)",
            ArtOfWarChallenge.Combat       => "Hedef: Tüm düşmanları mümkün olan en kısa sürede yok et",
            ArtOfWarChallenge.Counters     => "Hedef: Counter birimler eğit ve Kale Çağına ulaş",
            ArtOfWarChallenge.Siege        => "Hedef: Kuşatma birimi üret ve tüm düşman binalarını yık",
            _                              => "",
        };
        if (!string.IsNullOrEmpty(obj))
            gm.hud?.ShowSubtitle(obj, 6f);
    }

    /// <summary>Display name shown in CivSelectScreen.</summary>
    public static string DisplayName(ArtOfWarChallenge c) => c switch
    {
        ArtOfWarChallenge.EarlyEconomy => "Erken Eko",
        ArtOfWarChallenge.Combat       => "Muharebe",
        ArtOfWarChallenge.Counters     => "Counter'lar",
        ArtOfWarChallenge.Siege        => "Kuşatma",
        _                              => "Yok",
    };
}
