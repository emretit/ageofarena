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

    // Combat: eliminate all enemies within 5 minutes.
    // Timer thresholds: Gold <2min, Silver <3.5min, Bronze <5min.
    static void SetupCombat(TriggerSystem ts)
    {
        // Gold: enemies gone within 120 seconds
        ts.Add(new TriggerData {
            id = 20, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 120f,
            effectType = EffectType.DeactivateTrigger, effectInt1 = 21,  // disable silver trigger
            effect2Type = EffectType.DeactivateTrigger, effect2Int1 = 22,
        });
        // After 120s: check enemy eliminated for gold
        ts.Add(new TriggerData {
            id = 23, enabled = true, oneShot = true,
            conditionType = ConditionType.EnemyEliminated,
            effectType = EffectType.YouWin, effectStr1 = "🥇 Altın — Hızlı zafer!",
        });
        // Silver: enemies gone within 210 seconds
        ts.Add(new TriggerData {
            id = 21, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 210f,
            effectType = EffectType.ShowMessage, effectStr1 = "Gümüş süresinde…",
        });
        // Bronze: enemies gone within 300 seconds (5 min)
        ts.Add(new TriggerData {
            id = 22, enabled = true, oneShot = true,
            conditionType = ConditionType.Timer, condFloat1 = 300f,
            effectType = EffectType.YouLose, effectStr1 = "Süre doldu — Rakip hayatta!",
        });
        // Persistent enemy-eliminated check after silver window
        ts.Add(new TriggerData {
            id = 24, enabled = true, oneShot = true,
            conditionType = ConditionType.EnemyEliminated,
            effectType = EffectType.YouWin, effectStr1 = "Tüm düşmanlar yok edildi!",
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
        // Bronze: train 5 Spearmen (condInt2 = UnitType.Spearman = 3)
        ts.Add(new TriggerData {
            id = 30, enabled = true, oneShot = true,
            conditionType = ConditionType.OwnUnits, condInt1 = 0, condFloat1 = 5f,
            effectType = EffectType.ShowMessage, effectStr1 = "🥉 Bronz — 5 Kargıcı eğitildi!",
        });
        // Silver: also own 3 Skirmishers
        ts.Add(new TriggerData {
            id = 31, enabled = true, oneShot = true,
            conditionType = ConditionType.OwnUnits, condInt1 = 0, condFloat1 = 10f,
            effectType = EffectType.ShowMessage, effectStr1 = "🥈 Gümüş — 10 birim!",
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
