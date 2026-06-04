using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SAVF / N12.savefull: Full game-state save/load. F5 = quick-save, F9 = quick-load.
///
/// Now persists: unit veteranRank + stance + isGarrisoned, building rallyPoint,
/// map seed (so reload reproduces the same terrain/resources layout).
/// Version bumped to "3" — incompatible v2 saves are rejected.
///
/// Load flow: serialise → Restart() → WorldRoot reads PendingLoad and re-applies.
/// </summary>
public class SaveSystem : MonoBehaviour
{
    const string SaveKey = "AoA_SaveSlot_0";

    [Serializable]
    public class UnitSnap
    {
        public int   type, teamId;
        public float x, z, hp;
        // N12.savefull additions
        public int   veteranRank;
        public int   stance;          // (int)AttackStance
        public bool  isGarrisoned;
    }

    [Serializable]
    public class BuildingSnap
    {
        public int   type, teamId;
        public float x, z, hp;
        public bool  underConstruction;
        // N12.savefull: rally point
        public float rallyX, rallyZ;
        public bool  hasRally;
    }

    [Serializable]
    public class TeamSnap
    {
        public int food, wood, gold, stone;
        public int ageIndex;
        public List<int> techs = new();
        public int civ;
    }

    [Serializable]
    public class SaveData
    {
        public List<UnitSnap>     units     = new();
        public List<BuildingSnap> buildings = new();
        public TeamSnap[]         teams     = new TeamSnap[GameManager.MaxTeams];
        public int  gameMode, difficulty;
        public int  mapSeed;               // N12.savefull: reproduce same map
        public List<TriggerData> triggers  = new(); // N11.trig: trigger state
        public string version = "4";       // bumped: triggers added
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) Save();
        if (Input.GetKeyDown(KeyCode.F9)) Load();
    }

    public void Save()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var wr = UnityEngine.Object.FindAnyObjectByType<WorldRoot>();

        var data = new SaveData
        {
            gameMode = (int)gm.gameMode,
            difficulty = (int)gm.difficulty,
            mapSeed  = wr != null ? wr.mapSeed : 0,
        };

        // Teams
        data.teams = new TeamSnap[gm.TeamCount];
        for (int t = 0; t < gm.TeamCount; t++)
        {
            var r  = gm.teamRes[t];
            var tt = gm.teamTech[t];
            var ts = new TeamSnap
            {
                food = r.food, wood = r.wood, gold = r.gold, stone = r.stone,
                ageIndex = (int)tt.age,
                civ = (int)gm.teamCivs[t],
            };
            foreach (TechType ty in Enum.GetValues(typeof(TechType)))
                if (tt.Has(ty)) ts.techs.Add((int)ty);
            data.teams[t] = ts;
        }

        // Units (skip garrisoned — they're saved as "isGarrisoned=true" and respawned hidden)
        foreach (var u in gm.units)
        {
            if (u == null) continue;
            data.units.Add(new UnitSnap
            {
                type        = (int)u.type,
                teamId      = u.teamId,
                x           = u.transform.position.x,
                z           = u.transform.position.z,
                hp          = u.hp,
                veteranRank = u.veteranRank,
                stance      = (int)u.stance,
                isGarrisoned = u.isGarrisoned,
            });
        }

        // Buildings (include rally point)
        foreach (var b in gm.buildings)
        {
            if (b == null) continue;
            bool hr = b.rallyPoint != Vector3.zero;
            data.buildings.Add(new BuildingSnap
            {
                type              = (int)b.type,
                teamId            = b.teamId,
                x                 = b.transform.position.x,
                z                 = b.transform.position.z,
                hp                = b.hp,
                underConstruction = b.underConstruction,
                hasRally          = hr,
                rallyX            = hr ? b.rallyPoint.x : 0f,
                rallyZ            = hr ? b.rallyPoint.z : 0f,
            });
        }

        // N11.trig: snapshot trigger state
        if (gm.triggers != null)
            data.triggers = gm.triggers.Snapshot();

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Saved {data.units.Count} units, {data.buildings.Count} buildings, seed={data.mapSeed}.");
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) { Debug.Log("[SaveSystem] No save found."); return; }
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null || (data.version != "3" && data.version != "4")) { Debug.Log("[SaveSystem] Incompatible save (need v3/v4)."); return; }

        GameBootstrap.PendingLoad    = data;
        GameBootstrap.NextGameMode   = (GameMode)data.gameMode;
        GameBootstrap.NextDifficulty = (Difficulty)data.difficulty;
        Time.timeScale = 1f;
        GameBootstrap.Restart(data.mapSeed);  // N12.savefull: replay same seed → same map
    }
}
