using System.Collections.Generic;
using UnityEngine;

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
    public Civilization requiredCiv; // M9: civ-gated tech (None = available to every civ)

    public TechDef(TechType t, BuildingType b, Age req, int f, int w, int g, int s,
        float time, string name)
    {
        type = t; building = b; requiredAge = req;
        food = f; wood = w; gold = g; stone = s;
        researchTime = time; display = name;
        requires = default; hasRequires = false;
        requiredCiv = Civilization.None;
    }

    /// <summary>Overload for a tech gated behind another tech (e.g. Longswordsman ← ManAtArms).</summary>
    public TechDef(TechType t, BuildingType b, Age req, int f, int w, int g, int s,
        float time, string name, TechType prereq)
        : this(t, b, req, f, w, g, s, time, name)
    {
        requires = prereq; hasRequires = true;
    }

    /// <summary>Overload for a civ-gated tech (M9: unique/Elite techs available to one civ only).</summary>
    public TechDef(TechType t, BuildingType b, Age req, int f, int w, int g, int s,
        float time, string name, Civilization reqCiv)
        : this(t, b, req, f, w, g, s, time, name)
    {
        requiredCiv = reqCiv;
    }
}

public static class TechDefs
{
    public readonly struct TechAvailability
    {
        public readonly bool canResearch;
        public readonly string reason;

        public TechAvailability(bool canResearch, string reason)
        {
            this.canResearch = canResearch;
            this.reason = reason;
        }
    }

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

        // ── M8 (MKTT): Market economy techs (Caravan zaten M6'da tanımlı) ─────
        new(TechType.Coinage,       BuildingType.Market,       Age.Castle,    0,   0, 200, 0, 30f, "Sikke Basımı"),
        new(TechType.Banking,       BuildingType.Market,       Age.Imperial,  0,   0, 300, 0, 35f, "Bankacılık",   TechType.Coinage),
        new(TechType.Guilds,        BuildingType.Market,       Age.Imperial,300,   0,   0, 0, 35f, "Loncalar"),

        // ── M9 (EAGLE): Aztec-only Eagle upgrade ──────────────────────────────
        new(TechType.EliteEagle,    BuildingType.Barracks,     Age.Imperial,200,   0, 100, 0, 35f, "Seçkin Kartal", Civilization.Aztecs),

        // ── M9 (CIVT): civ-özel unique tech'ler (Castle'da araştırılır) ───────
        new(TechType.Chivalry,      BuildingType.Castle,       Age.Castle,    0,   0, 400, 0, 40f, "Şövalyelik",    Civilization.Franks),
        new(TechType.BeardedAxe,    BuildingType.Castle,       Age.Imperial,  0,   0, 400, 0, 40f, "Sakallı Balta", Civilization.Franks),
        new(TechType.Ironclad,      BuildingType.Castle,       Age.Castle,    0,   0, 400, 0, 40f, "Zırhlı",        Civilization.Teutons),
        new(TechType.Crenellations, BuildingType.Castle,       Age.Imperial,  0,   0, 400, 0, 40f, "Mazgallar",     Civilization.Teutons),
        // ── N4 (CIVT): unique tech pairs for the remaining UU civilizations ───
        new(TechType.Yeomen,        BuildingType.Castle,       Age.Castle,    0,   0, 350, 0, 40f, "Yeomen",         Civilization.Britons),
        new(TechType.Warwolf,       BuildingType.Castle,       Age.Imperial,  0,   0, 800, 0, 40f, "Warwolf",        Civilization.Britons),
        new(TechType.Nomads,        BuildingType.Castle,       Age.Castle,    0,   0, 300, 0, 40f, "Göçebeler",      Civilization.Mongols),
        new(TechType.Drill,         BuildingType.Castle,       Age.Imperial,  0,   0, 500, 0, 40f, "Talim",          Civilization.Mongols),
        new(TechType.Yasama,        BuildingType.Castle,       Age.Castle,  100,   0, 100, 0, 40f, "Yasama",         Civilization.Japanese),
        new(TechType.Kataparuto,    BuildingType.Castle,       Age.Imperial,  0,   0, 750, 0, 40f, "Kataparuto",     Civilization.Japanese),
        new(TechType.Kamandaran,    BuildingType.Castle,       Age.Castle,    0,   0, 300, 0, 40f, "Kamandaran",     Civilization.Persians),
        new(TechType.Mahouts,       BuildingType.Castle,       Age.Imperial,  0,   0, 300, 0, 40f, "Mahut",          Civilization.Persians),
        new(TechType.Atlatl,        BuildingType.Castle,       Age.Castle,  400,   0, 350, 0, 40f, "Atlatl",         Civilization.Aztecs),
        new(TechType.GarlandWars,   BuildingType.Castle,       Age.Imperial,450,   0, 750, 0, 40f, "Çiçek Savaşları",Civilization.Aztecs),
        new(TechType.GreekFire,     BuildingType.Castle,       Age.Castle,  300,   0, 100, 0, 40f, "Rum Ateşi",      Civilization.Byzantines),
        new(TechType.Logistica,     BuildingType.Castle,       Age.Imperial,500,   0, 600, 0, 40f, "Lojistika",      Civilization.Byzantines),
        new(TechType.Chieftains,    BuildingType.Castle,       Age.Castle,  400,   0, 200, 0, 40f, "Reisler",        Civilization.Vikings),
        new(TechType.Berserkergang, BuildingType.Castle,       Age.Imperial,300,   0, 350, 0, 40f, "Berserkergang",  Civilization.Vikings),
        new(TechType.Madrasah,      BuildingType.Castle,       Age.Castle,    0,   0, 200, 0, 40f, "Medrese",        Civilization.Saracens),
        new(TechType.Zealotry,      BuildingType.Castle,       Age.Imperial,750,   0, 800, 0, 40f, "Bağnazlık",      Civilization.Saracens),
        // ── N4/CIVC13: AoK-13 civ unique techs ──
        new(TechType.Stronghold,    BuildingType.Castle,       Age.Castle,    0,   0, 300, 0, 40f, "Müstahkem Mevki",Civilization.Celts),
        new(TechType.FurorCeltica,  BuildingType.Castle,       Age.Imperial,750,   0, 450, 0, 40f, "Furor Celtica",  Civilization.Celts),
        new(TechType.GreatWall,     BuildingType.Castle,       Age.Castle,  400,   0, 200, 0, 40f, "Çin Seddi",      Civilization.Chinese),
        new(TechType.Rocketry,      BuildingType.Castle,       Age.Imperial,600,   0, 600, 0, 40f, "Roket",          Civilization.Chinese),
        new(TechType.Anarchy,       BuildingType.Castle,       Age.Castle,    0,   0, 350, 0, 40f, "Anarşi",         Civilization.Goths),
        new(TechType.Perfusion,     BuildingType.Castle,       Age.Imperial,  0,   0, 450, 0, 40f, "Perfüzyon",      Civilization.Goths),
        new(TechType.Sipahi,        BuildingType.Castle,       Age.Castle,  350,   0, 150, 0, 40f, "Sipahi",         Civilization.Turks),
        new(TechType.Artillery,     BuildingType.Castle,       Age.Imperial,500,   0, 450, 0, 40f, "Topçuluk",       Civilization.Turks),

        // ── N content: core AoE2 techs added for parity ──────────────────────
        new(TechType.HandCart,      BuildingType.TownCenter,   Age.Castle,  300, 200, 0, 0, 35f, "Gelişmiş El Arabası", TechType.Wheelbarrow),
        new(TechType.TownWatch,     BuildingType.TownCenter,   Age.Feudal,   75,   0,  0, 0, 20f, "Kasaba Gözcülüğü"),
        new(TechType.TownPatrol,    BuildingType.TownCenter,   Age.Castle,  300,   0,  0, 0, 30f, "Kasaba Devriyesi", TechType.TownWatch),
        new(TechType.TwoManSaw,     BuildingType.LumberCamp,   Age.Castle,   300, 200, 0, 0, 35f, "İki Kişilik Testere", TechType.BowSaw),
        new(TechType.GoldShaftMining, BuildingType.MiningCamp, Age.Castle,   200, 100, 0, 0, 28f, "Altın Şaft Madenciliği", TechType.GoldMining),
        new(TechType.StoneShaftMining, BuildingType.MiningCamp, Age.Castle,  200, 100, 0, 0, 28f, "Taş Şaft Madenciliği", TechType.StoneMining),
        new(TechType.Squires,       BuildingType.Barracks,     Age.Castle,   200,   0, 0, 0, 28f, "Yaverler"),
        new(TechType.Arson,         BuildingType.Barracks,     Age.Castle,   150,  50, 0, 0, 28f, "Ateşe Verme", TechType.ManAtArms),
        new(TechType.Supplies,      BuildingType.Barracks,     Age.Feudal,   150,   0, 0, 0, 25f, "Erzak"),
        new(TechType.Gambesons,     BuildingType.Barracks,     Age.Castle,   100, 100, 0, 0, 28f, "Göğüslük", TechType.Supplies),
        new(TechType.ThumbRing,     BuildingType.ArcheryRange, Age.Castle,   300, 250, 0, 0, 35f, "Başparmak Halkası"),
        new(TechType.ParthianTactics, BuildingType.ArcheryRange, Age.Imperial,200, 300, 0, 0, 40f, "Part Taktikleri"),
        new(TechType.CappedRam,     BuildingType.SiegeWorkshop, Age.Castle,  300,   0, 225, 0, 35f, "Gelişmiş Koçbaşı"),
        new(TechType.SiegeRam,      BuildingType.SiegeWorkshop, Age.Imperial, 500,   0, 400, 0, 40f, "Kuşatma Koçbaşı", TechType.CappedRam),
        new(TechType.Onager,        BuildingType.SiegeWorkshop, Age.Imperial, 750,   0, 400, 0, 40f, "Onager"),
        new(TechType.SiegeOnager,   BuildingType.SiegeWorkshop, Age.Imperial,1500,   0,1000, 0, 50f, "Kuşatma Onager", TechType.Onager),
        new(TechType.HeavyScorpion, BuildingType.SiegeWorkshop, Age.Imperial, 750,   0, 400, 0, 40f, "Ağır Akrep"),
    };

    public static TechDef Get(TechType t)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].type == t) return Table[i];
        return Table[0];
    }

    /// <summary>N13.meta: all tech definitions for tech-tree viewer.</summary>
    public static TechDef[] All() => Table;

    /// <summary>
    /// Techs researchable right now at a building of <paramref name="building"/>:
    /// defined for that building, age requirement met, and not already researched.
    /// Age advances are gated to the immediately-next age (Feudal needs Dark, Castle
    /// needs Feudal, Imperial needs Castle); upgrades require being at or beyond their
    /// <c>requiredAge</c>.
    /// </summary>
    public static List<TechDef> ForBuilding(BuildingType building, Age age, TechState tech,
        Civilization civ = Civilization.None)
    {
        var list = new List<TechDef>();
        for (int i = 0; i < Table.Length; i++)
        {
            var d = Table[i];
            if (d.building != building) continue;
            if (tech != null && tech.Has(d.type)) continue;
            if (d.hasRequires && (tech == null || !tech.Has(d.requires))) continue; // prerequisite tech
            if (d.requiredCiv != Civilization.None && d.requiredCiv != civ) continue; // M9: civ-gated tech
            if (CivilizationDefs.IsTechDenied(civ, d.type)) continue; // N0.7: tech-tree subtraction
            if (!IsAvailable(d, age)) continue;
            list.Add(d);
        }
        return list;
    }

    /// <summary>All researchables for a specific building, applying the full shared
    /// availability gate (age, prerequisites, civ denials and age-up building count).</summary>
    public static List<TechDef> ForBuilding(BuildingEntity building)
    {
        var list = new List<TechDef>();
        if (building == null || building.underConstruction) return list;

        var gm = GameManager.Instance;
        if (gm == null || building.teamId < 0 || building.teamId >= gm.teamTech.Length)
            return list;

        var tech = gm.teamTech[building.teamId] ??= new TechState();
        Civilization civ = gm.teamCivs[building.teamId];
        for (int i = 0; i < Table.Length; i++)
        {
            var d = Table[i];
            if (d.building != building.type) continue;
            var avail = Check(building, d, tech, civ, gm);
            if (avail.canResearch) list.Add(d);
        }
        return list;
    }

    /// <summary>Shared availability gate used by HUD, ResearchSystem and AI.</summary>
    public static TechAvailability Check(BuildingEntity building, TechDef def,
        TechState tech = null, Civilization civ = Civilization.None, GameManager gm = null)
    {
        if (building == null) return new TechAvailability(false, "no building");
        if (building.underConstruction) return new TechAvailability(false, "building incomplete");
        if (def.building != building.type) return new TechAvailability(false, "wrong building");

        gm ??= GameManager.Instance;
        if (gm == null) return new TechAvailability(false, "no game manager");
        if (building.teamId < 0 || building.teamId >= gm.teamTech.Length)
            return new TechAvailability(false, "invalid team");

        tech ??= gm.teamTech[building.teamId] ??= new TechState();
        civ = civ != Civilization.None ? civ : gm.teamCivs[building.teamId];

        if (tech.Has(def.type)) return new TechAvailability(false, "already researched");
        if (def.hasRequires && !tech.Has(def.requires))
            return new TechAvailability(false, "missing prerequisite");
        if (def.requiredCiv != Civilization.None && def.requiredCiv != civ)
            return new TechAvailability(false, "civilization locked");
        if (CivilizationDefs.IsTechDenied(civ, def.type))
            return new TechAvailability(false, "civilization denied");
        if (!IsAvailable(def, tech.age))
            return new TechAvailability(false, "age locked");
        if (IsAgeTech(def.type) && !MeetsBuildingPrereq(gm, building.teamId, 2))
            return new TechAvailability(false, "needs 2 substantial buildings");

        return new TechAvailability(true, "");
    }

    public static bool CanResearch(BuildingEntity building, TechDef def)
        => Check(building, def).canResearch;

    static bool IsAvailable(TechDef d, Age age)
    {
        if (d.type == TechType.FeudalAge) return age == Age.Dark;
        if (d.type == TechType.CastleAge) return age == Age.Feudal;
        if (d.type == TechType.ImperialAge) return age == Age.Castle;
        return age >= d.requiredAge;
    }

    static bool IsAgeTech(TechType t) =>
        t == TechType.FeudalAge || t == TechType.CastleAge || t == TechType.ImperialAge;

    static bool CountsTowardAge(BuildingType t) => t switch
    {
        BuildingType.TownCenter or BuildingType.House or BuildingType.Farm
            or BuildingType.Wall or BuildingType.Gate or BuildingType.Outpost
            or BuildingType.FishTrap => false,
        _ => true,
    };

    static bool MeetsBuildingPrereq(GameManager gm, int teamId, int required)
    {
        if (gm == null) return true;
        int count = 0;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || b.teamId != teamId || b.underConstruction) continue;
            if (!CountsTowardAge(b.type)) continue;
            if (++count >= required) return true;
        }
        return false;
    }
}
