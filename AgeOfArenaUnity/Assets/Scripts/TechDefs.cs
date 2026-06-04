using System.Collections.Generic;

/// <summary>
/// Static definition of a researchable technology: where it's researched, the age
/// it needs, its cost and research time. Mirrors <see cref="BuildingDefs"/> so the
/// research UI/AI all read from one table.
/// </summary>
public struct TechDef
{
    public TechType type;
    public BuildingType building;
    public Age requiredAge;   // team must be at/after this age (advances handled specially)
    public int food, wood, gold, stone;
    public float researchTime;
    public string display;
    public TechType requires;   // prerequisite tech that must already be researched
    public bool hasRequires;    // false → no prerequisite (ignore `requires`)

    public TechDef(TechType t, BuildingType b, Age req, int f, int w, int g, int s,
        float time, string name)
    {
        type = t; building = b; requiredAge = req;
        food = f; wood = w; gold = g; stone = s;
        researchTime = time; display = name;
        requires = default; hasRequires = false;
    }

    /// <summary>Overload for a tech gated behind another tech (e.g. Longswordsman ← ManAtArms).</summary>
    public TechDef(TechType t, BuildingType b, Age req, int f, int w, int g, int s,
        float time, string name, TechType prereq)
        : this(t, b, req, f, w, g, s, time, name)
    {
        requires = prereq; hasRequires = true;
    }
}

public static class TechDefs
{
    static readonly TechDef[] Table =
    {
        //   type                 building                   reqAge       f    w    g  s  time  name
        new(TechType.FeudalAge,    BuildingType.TownCenter,   Age.Dark,   400,   0,   0, 0, 25f, "Derebeylik Çağı"),
        new(TechType.CastleAge,    BuildingType.TownCenter,   Age.Feudal, 600,   0, 200, 0, 35f, "Kale Çağı"),
        new(TechType.ImperialAge,  BuildingType.TownCenter,   Age.Castle, 1000,  0, 600, 0, 50f, "İmparatorluk Çağı"),

        // ── Blacksmith techs (moved from production buildings) ────────────────
        new(TechType.Forging,      BuildingType.Blacksmith,   Age.Feudal, 150,   0,   0, 0, 20f, "Dövme"),
        new(TechType.Fletching,    BuildingType.Blacksmith,   Age.Feudal, 100,   0,  50, 0, 20f, "Oklama"),
        new(TechType.ScaleMail,    BuildingType.Blacksmith,   Age.Castle, 150,   0, 100, 0, 25f, "Pul Zırh"),
        new(TechType.Bodkin,       BuildingType.Blacksmith,   Age.Castle, 150,   0, 100, 0, 25f, "İğne Ucu"),
        // ── Non-Blacksmith economy/stable techs ───────────────────────────────
        new(TechType.DoubleBitAxe, BuildingType.LumberCamp,   Age.Feudal, 100,   0,   0, 0, 18f, "Çift Balta"),
        new(TechType.Wheelbarrow,  BuildingType.TownCenter,   Age.Feudal, 150,  50,   0, 0, 22f, "El Arabası"),
        new(TechType.Bloodlines,   BuildingType.Stable,       Age.Castle, 150,   0, 100, 0, 25f, "Soyağacı"),
        // ── Unit upgrade lines (tier promotions) ──────────────────────────────
        new(TechType.ManAtArms,     BuildingType.Barracks,     Age.Feudal,  100,   0,  40, 0, 25f, "Piyade"),
        new(TechType.Longswordsman, BuildingType.Barracks,     Age.Castle,  150,   0, 100, 0, 30f, "Uzun Kılıç",  TechType.ManAtArms),
        new(TechType.TwoHandedSwordsman, BuildingType.Barracks, Age.Imperial,150,  0, 120, 0, 32f, "İki Elli Kılıç", TechType.Longswordsman),
        new(TechType.Champion,      BuildingType.Barracks,     Age.Imperial,200,   0, 150, 0, 35f, "Şampiyon",    TechType.TwoHandedSwordsman),
        new(TechType.Crossbowman,   BuildingType.ArcheryRange, Age.Castle,  150,   0, 100, 0, 30f, "Arbaletçi"),
        new(TechType.Arbalest,      BuildingType.ArcheryRange, Age.Imperial,200,   0, 150, 0, 35f, "Arbalet",     TechType.Crossbowman),
        new(TechType.Cavalier,      BuildingType.Stable,       Age.Castle,  150,   0, 100, 0, 30f, "Ağır Süvari"),
        new(TechType.Paladin,       BuildingType.Stable,       Age.Imperial,200,   0, 150, 0, 35f, "Paladin",     TechType.Cavalier),
        // ── Counter-unit tier lines (M2) ──────────────────────────────────────
        new(TechType.Pikeman,         BuildingType.Barracks,     Age.Castle,  100,   0,  50, 0, 28f, "Mızrakçı"),
        new(TechType.Halberdier,      BuildingType.Barracks,     Age.Imperial,150,   0, 100, 0, 32f, "Teberli",       TechType.Pikeman),
        new(TechType.EliteSkirmisher, BuildingType.ArcheryRange, Age.Imperial,150,   0, 100, 0, 30f, "Seçkin Avcı"),
        new(TechType.HeavyCamel,      BuildingType.Stable,       Age.Imperial,150,   0, 100, 0, 30f, "Ağır Deve"),
        new(TechType.LightCavalry,    BuildingType.Stable,       Age.Castle,  150,   0,  50, 0, 25f, "Hafif Süvari"),
        new(TechType.Hussar,          BuildingType.Stable,       Age.Imperial,150,   0, 100, 0, 30f, "Hüsar",          TechType.LightCavalry),
        new(TechType.HeavyCavalryArcher, BuildingType.Stable,    Age.Imperial,150,   0, 125, 0, 30f, "Ağır Atlı Okçu"),
        new(TechType.WarGalley,       BuildingType.Dock,         Age.Castle,  150,   0,  50, 0, 28f, "Savaş Kadırgası"),
        new(TechType.Galleon,         BuildingType.Dock,         Age.Imperial,150,   0, 100, 0, 32f, "Kalyon",         TechType.WarGalley),
        // ── Mill farm techs ───────────────────────────────────────────────────
        new(TechType.HorseCollar,   BuildingType.Mill,         Age.Feudal,  75,    0,   0, 0, 20f, "At Koşumu"),
        new(TechType.HeavyPlow,     BuildingType.Mill,         Age.Castle,  125,   0,   0, 0, 25f, "Ağır Saban",   TechType.HorseCollar),
        // ── University techs ──────────────────────────────────────────────────
        new(TechType.Masonry,       BuildingType.University,   Age.Castle,  150,   0,   0, 0, 22f, "Duvar Ustalığı"),
        new(TechType.Fortified,     BuildingType.University,   Age.Imperial,200,   0, 150, 0, 30f, "Takviyeli Duvar"),
        new(TechType.GuardTower,    BuildingType.University,   Age.Castle,  100,   0,  50, 0, 22f, "Muhafız Kulesi"),
        new(TechType.Keep,          BuildingType.University,   Age.Imperial,150,   0, 100, 0, 28f, "Burç",          TechType.GuardTower),

        // ── M6 (BSMT): Blacksmith melee attack line (infantry + cavalry) ──────
        new(TechType.IronCasting,   BuildingType.Blacksmith,   Age.Castle,  220,   0, 120, 0, 28f, "Demir Döküm",     TechType.Forging),
        new(TechType.BlastFurnace,  BuildingType.Blacksmith,   Age.Imperial,275,   0, 225, 0, 32f, "Yüksek Fırın",    TechType.IronCasting),
        // ── M6 (BSMT): Blacksmith infantry armor ──────────────────────────────
        new(TechType.ChainMail,     BuildingType.Blacksmith,   Age.Castle,  200,   0, 100, 0, 28f, "Zincir Zırh",     TechType.ScaleMail),
        new(TechType.PlateMail,     BuildingType.Blacksmith,   Age.Imperial,300,   0, 150, 0, 32f, "Levha Zırh",      TechType.ChainMail),
        // ── M6 (BFUR): Blacksmith cavalry armor (barding) ─────────────────────
        new(TechType.ScaleBarding,  BuildingType.Blacksmith,   Age.Feudal,  150,   0,   0, 0, 22f, "Pul Eyer Zırhı"),
        new(TechType.ChainBarding,  BuildingType.Blacksmith,   Age.Castle,  250,   0, 150, 0, 28f, "Zincir Eyer Zırhı", TechType.ScaleBarding),
        new(TechType.PlateBarding,  BuildingType.Blacksmith,   Age.Imperial,350,   0, 200, 0, 32f, "Levha Eyer Zırhı",  TechType.ChainBarding),
        // ── M6 (ARRM): Blacksmith archer armor + Bracer ───────────────────────
        new(TechType.PaddedArcherArmor,  BuildingType.Blacksmith, Age.Feudal,  100, 0,  50, 0, 22f, "Dolgulu Okçu Zırhı"),
        new(TechType.LeatherArcherArmor, BuildingType.Blacksmith, Age.Castle,  150, 0, 100, 0, 28f, "Deri Okçu Zırhı",  TechType.PaddedArcherArmor),
        new(TechType.RingArcherArmor,    BuildingType.Blacksmith, Age.Imperial,250, 0, 200, 0, 32f, "Halka Okçu Zırhı", TechType.LeatherArcherArmor),
        new(TechType.Bracer,             BuildingType.Blacksmith, Age.Imperial,200, 0, 175, 0, 30f, "Kol Koruması",     TechType.Bodkin),
        // ── M6 (ECON): economy gather techs ───────────────────────────────────
        new(TechType.Loom,          BuildingType.TownCenter,   Age.Dark,     0,    0,  50, 0, 25f, "Dokuma"),
        new(TechType.BowSaw,        BuildingType.LumberCamp,   Age.Castle,  150,   0, 100, 0, 25f, "Tezgah Testere",  TechType.DoubleBitAxe),
        new(TechType.GoldMining,    BuildingType.MiningCamp,   Age.Feudal,  100,   0,  75, 0, 22f, "Altın Madenciliği"),
        new(TechType.StoneMining,   BuildingType.MiningCamp,   Age.Feudal,  100,   0,  75, 0, 22f, "Taş Madenciliği"),
        new(TechType.CropRotation,  BuildingType.Mill,         Age.Imperial,250,   0, 100, 0, 28f, "Ekin Rotasyonu",  TechType.HeavyPlow),
        // ── M6 (CAVT): Stable husbandry ───────────────────────────────────────
        new(TechType.Husbandry,     BuildingType.Stable,       Age.Castle,  150,   0,   0, 0, 22f, "Hayvancılık"),
        // ── M6 (CARA): Market caravan ─────────────────────────────────────────
        new(TechType.Caravan,       BuildingType.Market,       Age.Castle,    0,   0, 200, 0, 28f, "Kervan"),
        // ── M6 (UNIV): University military techs ──────────────────────────────
        new(TechType.Ballistics,    BuildingType.University,   Age.Castle,  300,   0, 175, 0, 35f, "Balistik"),
        new(TechType.Chemistry,     BuildingType.University,   Age.Imperial,300,   0, 200, 0, 40f, "Kimya"),
        new(TechType.Architecture,  BuildingType.University,   Age.Castle,  300,   0,   0, 0, 35f, "Mimari"),

        // ── M7 (MONK/CONV): Monastery monk techs ──────────────────────────────
        new(TechType.Sanctity,      BuildingType.Monastery,    Age.Castle,  120,   0,   0, 0, 30f, "Kutsallık"),
        new(TechType.BlockPrinting, BuildingType.Monastery,    Age.Castle,    0,   0, 200, 0, 32f, "Matbaa"),
        new(TechType.Redemption,    BuildingType.Monastery,    Age.Castle,    0,   0, 475, 0, 35f, "Kurtarış"),
        new(TechType.Theocracy,     BuildingType.Monastery,    Age.Imperial,  0,   0, 200, 0, 40f, "Teokrasi"),
    };

    public static TechDef Get(TechType t)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].type == t) return Table[i];
        return Table[0];
    }

    /// <summary>
    /// Techs researchable right now at a building of <paramref name="building"/>:
    /// defined for that building, age requirement met, and not already researched.
    /// Age advances are gated to the immediately-next age (Feudal needs Dark, Castle
    /// needs Feudal, Imperial needs Castle); upgrades require being at or beyond their
    /// <c>requiredAge</c>.
    /// </summary>
    public static List<TechDef> ForBuilding(BuildingType building, Age age, TechState tech)
    {
        var list = new List<TechDef>();
        for (int i = 0; i < Table.Length; i++)
        {
            var d = Table[i];
            if (d.building != building) continue;
            if (tech != null && tech.Has(d.type)) continue;
            if (d.hasRequires && (tech == null || !tech.Has(d.requires))) continue; // prerequisite tech
            if (!IsAvailable(d, age)) continue;
            list.Add(d);
        }
        return list;
    }

    static bool IsAvailable(TechDef d, Age age)
    {
        if (d.type == TechType.FeudalAge) return age == Age.Dark;
        if (d.type == TechType.CastleAge) return age == Age.Feudal;
        if (d.type == TechType.ImperialAge) return age == Age.Castle;
        return age >= d.requiredAge;
    }
}
