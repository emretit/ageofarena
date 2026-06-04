/// <summary>
/// Civilization identity and per-civ bonuses. Each team picks a civ at game start
/// (or uses Default/None). Bonuses are data-only; systems read them via
/// <see cref="CivilizationDefs.Get"/> and apply multipliers in their own tick logic.
/// This keeps the civ layer additive — no civ needs bespoke system code.
///
/// CIVX: this file is the single canonical source of every civ's ID and display
/// string. HUD/selection/wiki all read <see cref="CivBonus.display"/> from here —
/// never hard-code a civ name elsewhere. docs/wiki/06-civilizations.md mirrors these.
/// </summary>
public enum Civilization
{
    None,
    Franks, Britons, Mongols, Japanese, Byzantines,   // original 5
    Aztecs, Teutons, Persians, Vikings, Saracens,      // M9/CIVC expansion (10 playable total)
}

/// <summary>
/// CIVM: a civilization's TEAM bonus — a benefit that (once alliances land in M11)
/// is shared with every allied team. Exposed through
/// <see cref="GameManager.TeamSharedBonus"/> and consumed by gameplay systems.
/// </summary>
public struct TeamBonus
{
    public float gatherFoodBonus;   // additive fraction to team food gather (0.05 = +5%)
}

public struct CivBonus
{
    public Civilization civ;
    public string display;

    // Economy
    public float gatherFoodMult;   // Franks: +20% food gather
    public float gatherWoodMult;
    public float gatherGoldMult;

    // Military
    public float cavalryHpMult;    // Franks: cavalry +20% HP
    public float archerRangeBonus; // Britons: archer +1 range
    public float cavalrySpeedMult; // Mongols: cavalry +25% speed
    public float infantryAttackMult;// Japanese: infantry +10% atk
    public float buildingHpMult;   // Byzantines: buildings +10% HP
    public float healRateMult;     // Byzantines: Medic/Monk +50% heal

    // Economy QoL
    public float farmDecayMult;    // 1.0 = normal; Franks 0.5 = half decay

    // ── M9/CIVD: new identity fields (each read by ≥1 system) ────────────────
    public float archerAttackMult;  // Vikings/Saracens: archer-class +atk (UnitEntity.AttackDamage)
    public float unitTrainTimeMult; // Mongols/Aztecs: faster training, <1 (TrainingQueue.Enqueue)

    // ── M9/CIVM: team (shared) bonus ─────────────────────────────────────────
    public TeamBonus teamBonus;
}

public static class CivilizationDefs
{
    // Helper: build a fully-populated row with sensible neutral defaults, then override.
    static CivBonus Row(Civilization c, string name,
        float food = 1f, float wood = 1f, float gold = 1f,
        float cavHp = 1f, float archRange = 0f, float cavSpd = 1f,
        float infAtk = 1f, float bldHp = 1f, float heal = 1f, float farmDecay = 1f,
        float archAtk = 1f, float trainTime = 1f, float teamFood = 0f) => new()
    {
        civ = c, display = name,
        gatherFoodMult = food, gatherWoodMult = wood, gatherGoldMult = gold,
        cavalryHpMult = cavHp, archerRangeBonus = archRange, cavalrySpeedMult = cavSpd,
        infantryAttackMult = infAtk, buildingHpMult = bldHp, healRateMult = heal,
        farmDecayMult = farmDecay, archerAttackMult = archAtk, unitTrainTimeMult = trainTime,
        teamBonus = new TeamBonus { gatherFoodBonus = teamFood },
    };

    static readonly CivBonus[] Table =
    {
        Row(Civilization.None,       "Yok"),
        Row(Civilization.Franks,     "Franklar",      food: 1.2f, cavHp: 1.2f, farmDecay: 0.5f),
        Row(Civilization.Britons,    "Britanyalılar", wood: 1.15f, archRange: 1f),
        Row(Civilization.Mongols,    "Moğollar",      gold: 1.1f, cavSpd: 1.25f, trainTime: 0.9f),
        Row(Civilization.Japanese,   "Japonlar",      wood: 1.1f, infAtk: 1.1f),
        Row(Civilization.Byzantines, "Bizanslılar",   bldHp: 1.1f, heal: 1.5f),
        // ── M9/CIVC expansion ──
        Row(Civilization.Aztecs,     "Aztekler",      food: 1.15f, heal: 1.2f, trainTime: 0.9f, teamFood: 0.05f),
        Row(Civilization.Teutons,    "Tötonlar",      infAtk: 1.05f, bldHp: 1.15f),
        Row(Civilization.Persians,   "Persler",       food: 1.1f, cavHp: 1.1f),
        Row(Civilization.Vikings,    "Vikingler",     archAtk: 1.1f, wood: 1.1f),
        Row(Civilization.Saracens,   "Saracenler",    gold: 1.15f, archAtk: 1.1f),
    };

    public static CivBonus Get(Civilization c)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].civ == c) return Table[i];
        return Table[0];
    }

    /// <summary>All civilizations a player may pick (excludes <see cref="Civilization.None"/>).</summary>
    public static System.Collections.Generic.IEnumerable<CivBonus> Playable()
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].civ != Civilization.None) yield return Table[i];
    }
}
