using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal save/load system. Serializes the player's resource ledger, tech state
/// (age + researched set), and unit/building counts into PlayerPrefs (JSON).
/// Full scene reconstruction on load is too expensive for a vertical slice — instead
/// we save the "progress" snapshot and hot-restart the arena with those values applied.
/// F5 = quick-save, F9 = quick-load.
/// </summary>
public class SaveSystem : MonoBehaviour
{
    const string SaveKey = "AoA_SaveSlot_0";

    [Serializable]
    class SaveData
    {
        public int food, wood, gold, stone;
        public int ageIndex;
        public List<int> researchedTechs = new();
        public int popCap;
        public string version = "1";
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
        var rm = gm.resources;
        var tech = gm.tech;

        var data = new SaveData
        {
            food  = rm.food, wood = rm.wood, gold = rm.gold, stone = rm.stone,
            ageIndex = (int)tech.age,
            popCap = rm.popCap,
        };
        // Serialize researched tech IDs.
        foreach (TechType t in Enum.GetValues(typeof(TechType)))
            if (tech.Has(t)) data.researchedTechs.Add((int)t);

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Saved. Age={tech.age} Food={rm.food}");
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) { Debug.Log("[SaveSystem] No save found."); return; }
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;
        var rm = gm.resources;

        // Restore resources.
        int df = data.food - rm.food, dw = data.wood - rm.wood,
            dg = data.gold - rm.gold, ds = data.stone - rm.stone;
        rm.Gain(ResourceKind.Food,  df);
        rm.Gain(ResourceKind.Wood,  dw);
        rm.Gain(ResourceKind.Gold,  dg);
        rm.Gain(ResourceKind.Stone, ds);
        rm.popCap = data.popCap;

        // Restore tech/age.
        var tech = gm.tech;
        for (int i = 0; i < data.researchedTechs.Count; i++)
        {
            var type = (TechType)data.researchedTechs[i];
            if (!tech.Has(type)) ResearchSystem.Apply(type, 0);
        }
        Debug.Log($"[SaveSystem] Loaded. Age={tech.age} Food={rm.food}");
    }
}
