/// <summary>
/// Civilization identity and per-civ bonuses. Each team picks a civ at game start
/// (or uses Default/None). Bonuses are data-only; systems read them via
/// <see cref="CivilizationDefs.Get"/> and apply multipliers in their own tick logic.
/// This keeps the civ layer additive — no civ needs bespoke system code.
/// </summary>
public enum Civilization { None, Franks, Britons, Mongols, Japanese, Byzantines }

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
}

public static class CivilizationDefs
{
    static readonly CivBonus[] Table =
    {
        new() { civ = Civilization.None,       display = "Yok",         // balanced (no bonus)
            gatherFoodMult=1f, gatherWoodMult=1f, gatherGoldMult=1f,
            cavalryHpMult=1f, archerRangeBonus=0f, cavalrySpeedMult=1f,
            infantryAttackMult=1f, buildingHpMult=1f, healRateMult=1f, farmDecayMult=1f },

        new() { civ = Civilization.Franks,     display = "Franklar",
            gatherFoodMult=1.2f, cavalryHpMult=1.2f, farmDecayMult=0.5f,
            gatherWoodMult=1f, gatherGoldMult=1f, cavalrySpeedMult=1f,
            infantryAttackMult=1f, buildingHpMult=1f, healRateMult=1f },

        new() { civ = Civilization.Britons,    display = "Britanyalılar",
            archerRangeBonus=1f, gatherWoodMult=1.15f,
            gatherFoodMult=1f, gatherGoldMult=1f, cavalryHpMult=1f,
            cavalrySpeedMult=1f, infantryAttackMult=1f, buildingHpMult=1f,
            healRateMult=1f, farmDecayMult=1f },

        new() { civ = Civilization.Mongols,    display = "Moğollar",
            cavalrySpeedMult=1.25f, gatherGoldMult=1.1f,
            gatherFoodMult=1f, gatherWoodMult=1f, cavalryHpMult=1f,
            archerRangeBonus=0f, infantryAttackMult=1f, buildingHpMult=1f,
            healRateMult=1f, farmDecayMult=1f },

        new() { civ = Civilization.Japanese,   display = "Japonlar",
            infantryAttackMult=1.1f, gatherWoodMult=1.1f,
            gatherFoodMult=1f, gatherGoldMult=1f, cavalryHpMult=1f,
            cavalrySpeedMult=1f, archerRangeBonus=0f, buildingHpMult=1f,
            healRateMult=1f, farmDecayMult=1f },

        new() { civ = Civilization.Byzantines, display = "Bizanslılar",
            buildingHpMult=1.1f, healRateMult=1.5f,
            gatherFoodMult=1f, gatherWoodMult=1f, gatherGoldMult=1f,
            cavalryHpMult=1f, cavalrySpeedMult=1f, infantryAttackMult=1f,
            archerRangeBonus=0f, farmDecayMult=1f },
    };

    public static CivBonus Get(Civilization c)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].civ == c) return Table[i];
        return Table[0];
    }
}
