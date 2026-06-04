using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SAVF: Full game-state save/load. F5 = quick-save, F9 = quick-load.
///
/// Save captures: player resources + tech, all unit snapshots (type/team/pos/hp),
/// all building snapshots (type/team/pos/hp/underConstruction), and team civs.
///
/// Load flow: serialise → Restart() → WorldRoot reads <see cref="GameBootstrap.PendingLoad"/>
/// and re-applies the snapshot instead of the default spawn (NavMesh re-baked fresh).
/// </summary>
public class SaveSystem : MonoBehaviour
{
    const string SaveKey = "AoA_SaveSlot_0";

    [Serializable]
    public class UnitSnap
    {
        public int type, teamId;
        public float x, z, hp;
    }

    [Serializable]
    public class BuildingSnap
    {
        public int type, teamId;
        public float x, z, hp;
        public bool underConstruction;
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
        public TeamSnap[]         teams     = new TeamSnap[4];
        public int gameMode, difficulty;
        public string version = "2";
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

        var data = new SaveData
        {
            gameMode   = (int)gm.gameMode,
            difficulty = (int)gm.difficulty,
        };

        // Teams
        data.teams = new TeamSnap[4];
        for (int t = 0; t < 4; t++)
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

        // Units
        foreach (var u in gm.units)
        {
            if (u == null) continue;
            data.units.Add(new UnitSnap
            {
                type = (int)u.type, teamId = u.teamId,
                x = u.transform.position.x, z = u.transform.position.z,
                hp = u.hp,
            });
        }

        // Buildings
        foreach (var b in gm.buildings)
        {
            if (b == null) continue;
            data.buildings.Add(new BuildingSnap
            {
                type = (int)b.type, teamId = b.teamId,
                x = b.transform.position.x, z = b.transform.position.z,
                hp = b.hp, underConstruction = b.underConstruction,
            });
        }

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Saved {data.units.Count} units, {data.buildings.Count} buildings.");
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) { Debug.Log("[SaveSystem] No save found."); return; }
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null || data.version != "2") { Debug.Log("[SaveSystem] Incompatible save."); return; }

        GameBootstrap.PendingLoad = data;
        GameBootstrap.NextGameMode   = (GameMode)data.gameMode;
        GameBootstrap.NextDifficulty = (Difficulty)data.difficulty;
        Time.timeScale = 1f;
        GameBootstrap.Restart();
    }
}
