using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N13.camp: Campaign framework — ordered sequence of scenarios, each with
/// a briefing, objective, and win trigger. Progress is persisted in PlayerPrefs.
///
/// Flow:
///   CampaignScreen → player picks a mission → WorldRoot builds the arena →
///   CampaignSystem.Setup(gm) injects win/fail triggers → player wins/loses →
///   TriggerSystem fires YouWin → CampaignSystem.OnMissionComplete(id) saves
///   progress and unlocks the next mission.
///
/// Adding a new mission = one static Mission entry + TriggerData list.
/// </summary>
public static class CampaignSystem
{
    const string SaveKey = "AoA_Campaign_Progress";

    // ── Mission data ──────────────────────────────────────────────────────────

    public struct Mission
    {
        public int    id;
        public string name;
        public string briefing;     // shown on the campaign screen
        public string objective;    // shown in-game as HUD subtitle
        public int    startFood, startWood, startGold; // team-0 starting resources
        public bool   castleAge;    // start at Castle Age?
    }

    public static readonly Mission[] Missions =
    {
        new Mission {
            id         = 0,
            name       = "İlk Savaş",
            briefing   = "Küçük bir düşman kuvveti kapıya dayanıyor. Tüm düşman " +
                         "birimi ve binalarını yok et.",
            objective  = "Hedef: Tüm düşmanları yok et.",
            startFood  = 400,
            startWood  = 300,
            startGold  = 100,
            castleAge  = false,
        },
        new Mission {
            id         = 1,
            name       = "Kaynak Savaşı",
            briefing   = "Orta haritadaki altın madenlerini ele geçir. Toplam " +
                         "1500 altın topla ve ekonomini geliştir.",
            objective  = "Hedef: 1500 altın topla ve Castle Çağı'na ulaş.",
            startFood  = 600,
            startWood  = 500,
            startGold  = 200,
            castleAge  = false,
        },
        new Mission {
            id         = 2,
            name       = "İmparatorun Seferi",
            briefing   = "İmparatorluk Çağı'na ulaş ve düşmanın kalesini yık. " +
                         "Bu sefer ne pahasına olursa olsun kazanmalısın.",
            objective  = "Hedef: Imperial Çağ'a ulaş, ardından tüm düşmanları yok et.",
            startFood  = 800,
            startWood  = 600,
            startGold  = 400,
            castleAge  = true,
        },
    };

    // ── Progress ─────────────────────────────────────────────────────────────

    public static int ActiveMissionId = -1; // -1 = no campaign active

    public static bool IsMissionUnlocked(int id)
    {
        if (id == 0) return true; // first mission always unlocked
        return PlayerPrefs.GetInt($"{SaveKey}_{id}_unlocked", 0) == 1;
    }

    public static bool IsMissionComplete(int id) =>
        PlayerPrefs.GetInt($"{SaveKey}_{id}_done", 0) == 1;

    public static void UnlockMission(int id)
    {
        PlayerPrefs.SetInt($"{SaveKey}_{id}_unlocked", 1);
        PlayerPrefs.Save();
    }

    public static void CompleteMission(int id)
    {
        PlayerPrefs.SetInt($"{SaveKey}_{id}_done", 1);
        // unlock next
        if (id + 1 < Missions.Length) UnlockMission(id + 1);
        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        foreach (var m in Missions)
        {
            PlayerPrefs.DeleteKey($"{SaveKey}_{m.id}_unlocked");
            PlayerPrefs.DeleteKey($"{SaveKey}_{m.id}_done");
        }
        PlayerPrefs.Save();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by WorldRoot.SetupGameplay when ActiveMissionId >= 0.
    /// Applies starting resources, researches Castle Age if needed,
    /// and injects win/fail triggers.
    /// </summary>
    public static void Setup(GameManager gm)
    {
        if (ActiveMissionId < 0 || ActiveMissionId >= Missions.Length) return;
        if (gm?.triggers == null) return;

        var m = Missions[ActiveMissionId];

        // Apply starting resources for player team.
        gm.teamRes[0].food  = Mathf.Max(gm.teamRes[0].food,  m.startFood);
        gm.teamRes[0].wood  = Mathf.Max(gm.teamRes[0].wood,  m.startWood);
        gm.teamRes[0].gold  = Mathf.Max(gm.teamRes[0].gold,  m.startGold);
        if (m.castleAge)
        {
            ResearchSystem.Apply(TechType.FeudalAge,  0);
            ResearchSystem.Apply(TechType.CastleAge,  0);
        }

        // Clear stale triggers and inject mission triggers.
        gm.triggers.LoadSnapshot(new List<TriggerData>());
        BuildTriggers(m, gm.triggers);

        // Show briefing subtitle.
        gm.hud?.ShowSubtitle(m.objective, 6f);
    }

    static void BuildTriggers(Mission m, TriggerSystem ts)
    {
        // Universal fail: player loses all buildings + units
        // (MatchSystem.Conquest handles this via existing logic — no extra trigger needed)

        switch (m.id)
        {
            case 0: // First Battle: eliminate all enemies
                ts.Add(new TriggerData {
                    id = 0, enabled = true, oneShot = true,
                    conditionType = ConditionType.EnemyEliminated,
                    effectType    = EffectType.YouWin,
                    effectStr1    = "Görev tamamlandı! Tüm düşmanlar yok edildi.",
                    effect2Type   = EffectType.None,
                });
                // Fail: 10-minute time limit
                ts.Add(new TriggerData {
                    id = 1, enabled = true, oneShot = true,
                    conditionType = ConditionType.Timer, condFloat1 = 600f,
                    effectType    = EffectType.YouLose,
                    effectStr1    = "Süre doldu — takviye yetişemedi!",
                });
                break;

            case 1: // Resource War: gather 1500 gold AND reach Castle Age
                ts.Add(new TriggerData {
                    id = 10, enabled = true, oneShot = true,
                    conditionType = ConditionType.ResourceGathered,
                    condInt1 = 0, condInt2 = (int)ResourceKind.Gold, condFloat1 = 1500f,
                    effectType = EffectType.ActivateTrigger, effectInt1 = 12,
                    effect2Type = EffectType.ShowMessage, effect2Str1 = "1500 altın toplandı! Castle Çağı'na geç.",
                });
                ts.Add(new TriggerData {
                    id = 11, enabled = true, oneShot = true,
                    conditionType = ConditionType.AgeReached,
                    condInt1 = 0, condInt2 = 2, // Castle = 2
                    effectType = EffectType.ActivateTrigger, effectInt1 = 12,
                    effect2Type = EffectType.ShowMessage, effect2Str1 = "Castle Çağı! 1500 altın tamamla.",
                });
                // Win only when BOTH conditions met (id=12 starts disabled; activated when first is met)
                ts.Add(new TriggerData {
                    id = 12, enabled = false, oneShot = true,
                    conditionType = ConditionType.AgeReached,
                    condInt1 = 0, condInt2 = 2,
                    effectType = EffectType.YouWin,
                    effectStr1 = "Görev tamamlandı! Ekonomi gücü kanıtlandı.",
                });
                // Fail: 15 min
                ts.Add(new TriggerData {
                    id = 13, enabled = true, oneShot = true,
                    conditionType = ConditionType.Timer, condFloat1 = 900f,
                    effectType = EffectType.YouLose, effectStr1 = "Süre doldu!",
                });
                break;

            case 2: // Imperial Conquest: reach Imperial Age THEN eliminate all enemies
                ts.Add(new TriggerData {
                    id = 20, enabled = true, oneShot = true,
                    conditionType = ConditionType.AgeReached, condInt1 = 0, condInt2 = 3,
                    effectType = EffectType.ActivateTrigger, effectInt1 = 21,
                    effect2Type = EffectType.ShowMessage, effect2Str1 = "İmparatorluk Çağı! Şimdi düşmanı ez.",
                });
                ts.Add(new TriggerData {
                    id = 21, enabled = false, oneShot = true,
                    conditionType = ConditionType.EnemyEliminated,
                    effectType    = EffectType.YouWin,
                    effectStr1    = "Sefer başarıyla tamamlandı! İmparatorluk hüküm sürdü.",
                });
                break;
        }
    }

    // ── Win hook ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from HUD.ShowGameOver when playerWon == true.
    /// Marks the active mission complete and unlocks the next.
    /// </summary>
    public static void OnCampaignWin()
    {
        if (ActiveMissionId < 0) return;
        CompleteMission(ActiveMissionId);
        // Reset so a normal restart doesn't re-run campaign mode.
        ActiveMissionId = -1;
    }

    /// <summary>Abandon the active mission WITHOUT completing/unlocking it (called on
    /// defeat or quit-to-menu). Without this, losing a mission leaves ActiveMissionId set
    /// and the next normal "New Game" silently re-injects the mission's triggers + economy.</summary>
    public static void Abort() => ActiveMissionId = -1;
}
