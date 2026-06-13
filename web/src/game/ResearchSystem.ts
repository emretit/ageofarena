/**
 * ResearchSystem.ts — Port of ResearchSystem.cs + TechDefs.cs (core subset).
 * Blacksmith/LumberCamp/Mill/Barracks/Stable/TC/University/Monastery/Market/Castle research queue.
 * Costs from TechDefs.cs (Unity source of truth).
 */
import { Age, ArmorClass, BuildingType, UnitType } from "../core/GameTypes";
import { Civilization } from "../core/CivilizationDefs";
import { getTeamCiv } from "../core/CivState";
import type { ResourceManager } from "../core/ResourceManager";
import type { Unit } from "./Unit";
import type { Building } from "./Building";

export const enum TechId {
  // Blacksmith — Feudal
  Fletching           = "Fletching",
  Forging             = "Forging",
  PaddedArcherArmor   = "PaddedArcherArmor",
  ScaleMail           = "ScaleMail",
  ScaleBarding        = "ScaleBarding",
  // Blacksmith — Castle
  IronCasting         = "IronCasting",
  ChainMail           = "ChainMail",
  LeatherArcherArmor  = "LeatherArcherArmor",
  Bodkin              = "Bodkin",
  ChainBarding        = "ChainBarding",
  // Blacksmith — Imperial
  BlastFurnace        = "BlastFurnace",
  PlateMail           = "PlateMail",
  RingArcherArmor     = "RingArcherArmor",
  PlateBarding        = "PlateBarding",
  Bracer              = "Bracer",
  // Gather — Feudal
  DoubleBitAxe        = "DoubleBitAxe",
  Wheelbarrow         = "Wheelbarrow",
  HorseCollar         = "HorseCollar",
  Loom                = "Loom",
  GoldMining          = "GoldMining",
  StoneMining         = "StoneMining",
  // Gather — Castle
  BowSaw              = "BowSaw",
  HandCart            = "HandCart",
  HeavyPlow           = "HeavyPlow",
  GoldShaftMining     = "GoldShaftMining",
  StoneMiningUpgrade  = "StoneMiningUpgrade",
  // Gather — Imperial
  CropRotation        = "CropRotation",
  // Military — Feudal
  ManAtArms           = "ManAtArms",
  // Military — Castle
  Longswordsman       = "Longswordsman",
  Bloodlines          = "Bloodlines",
  Crossbowman         = "Crossbowman",
  Cavalier            = "Cavalier",
  Pikeman             = "Pikeman",
  LightCavalry        = "LightCavalry",
  Husbandry           = "Husbandry",
  // Military — Imperial
  TwoHandedSwordsman  = "TwoHandedSwordsman",
  Champion            = "Champion",
  Arbalest            = "Arbalest",
  Paladin             = "Paladin",
  Halberdier          = "Halberdier",
  EliteSkirmisher     = "EliteSkirmisher",
  Hussar              = "Hussar",
  // Market
  Caravan             = "Caravan",
  Coinage             = "Coinage",
  Banking             = "Banking",
  // University — Castle
  Ballistics          = "Ballistics",
  Masonry             = "Masonry",
  Architecture        = "Architecture",
  GuardTower          = "GuardTower",
  // University — Imperial
  Chemistry           = "Chemistry",
  Keep                = "Keep",
  Fortified           = "Fortified",
  // Monastery — Castle
  Sanctity            = "Sanctity",
  BlockPrinting       = "BlockPrinting",
  Redemption          = "Redemption",
  // Monastery — Imperial
  Theocracy           = "Theocracy",
  // Castle — Civ Unique (Castle Age)
  Chivalry            = "Chivalry",       // Franks
  Ironclad            = "Ironclad",       // Teutons
  Yeomen              = "Yeomen",         // Britons
  Nomads              = "Nomads",         // Mongols
  Yasama              = "Yasama",         // Japanese
  Kamandaran          = "Kamandaran",     // Persians
  Atlatl              = "Atlatl",         // Aztecs
  GreekFire           = "GreekFire",      // Byzantines
  Chieftains          = "Chieftains",     // Vikings
  Madrasah            = "Madrasah",       // Saracens
  Stronghold          = "Stronghold",     // Celts
  GreatWall           = "GreatWall",      // Chinese
  Anarchy             = "Anarchy",        // Goths
  Sipahi              = "Sipahi",         // Turks
  // Castle — Civ Unique (Imperial Age)
  BeardedAxe          = "BeardedAxe",     // Franks
  Crenellations       = "Crenellations",  // Teutons
  Warwolf             = "Warwolf",        // Britons
  Drill               = "Drill",          // Mongols
  Kataparuto          = "Kataparuto",     // Japanese
  Mahouts             = "Mahouts",        // Persians
  GarlandWars         = "GarlandWars",    // Aztecs
  Logistica           = "Logistica",      // Byzantines
  Berserkergang       = "Berserkergang",  // Vikings
  Zealotry            = "Zealotry",       // Saracens
  FurorCeltica        = "FurorCeltica",   // Celts
  Rocketry            = "Rocketry",       // Chinese
  Perfusion           = "Perfusion",      // Goths
  Artillery           = "Artillery",      // Turks
}

export interface TechDef {
  label: string;
  host: BuildingType;
  minAge: Age;
  food: number;
  wood: number;
  gold: number;
  time: number;
  prereq?: TechId;
  /** If set, only available for this civilization. */
  civGate?: Civilization;
}

export const TECH_DEFS: Record<TechId, TechDef> = {
  // ── Blacksmith Feudal ────────────────────────────────────────────────────
  [TechId.Fletching]:          { label: "Fletching",           host: BuildingType.Blacksmith,  minAge: Age.Feudal,   food: 100, wood:  0, gold:  50, time: 20 },
  [TechId.Forging]:            { label: "Forging",             host: BuildingType.Blacksmith,  minAge: Age.Feudal,   food: 150, wood:  0, gold:   0, time: 20 },
  [TechId.PaddedArcherArmor]:  { label: "Padded Arch. Armor",  host: BuildingType.Blacksmith,  minAge: Age.Feudal,   food: 100, wood:  0, gold:  50, time: 22 },
  [TechId.ScaleMail]:          { label: "Scale Mail Armor",    host: BuildingType.Blacksmith,  minAge: Age.Feudal,   food: 100, wood:  0, gold:  50, time: 22 },
  [TechId.ScaleBarding]:       { label: "Scale Barding",       host: BuildingType.Blacksmith,  minAge: Age.Feudal,   food: 150, wood:  0, gold:   0, time: 22 },
  // ── Blacksmith Castle ────────────────────────────────────────────────────
  [TechId.IronCasting]:        { label: "Iron Casting",        host: BuildingType.Blacksmith,  minAge: Age.Castle,   food: 220, wood:  0, gold: 120, time: 28, prereq: TechId.Forging },
  [TechId.ChainMail]:          { label: "Chain Mail Armor",    host: BuildingType.Blacksmith,  minAge: Age.Castle,   food: 200, wood:  0, gold: 100, time: 28, prereq: TechId.ScaleMail },
  [TechId.LeatherArcherArmor]: { label: "Leather Arch. Armor", host: BuildingType.Blacksmith,  minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 28, prereq: TechId.PaddedArcherArmor },
  [TechId.Bodkin]:             { label: "Bodkin Arrow",        host: BuildingType.Blacksmith,  minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 25, prereq: TechId.Fletching },
  [TechId.ChainBarding]:       { label: "Chain Barding",       host: BuildingType.Blacksmith,  minAge: Age.Castle,   food: 250, wood:  0, gold: 150, time: 28, prereq: TechId.ScaleBarding },
  // ── Blacksmith Imperial ──────────────────────────────────────────────────
  [TechId.BlastFurnace]:       { label: "Blast Furnace",       host: BuildingType.Blacksmith,  minAge: Age.Imperial, food: 275, wood:  0, gold: 225, time: 32, prereq: TechId.IronCasting },
  [TechId.PlateMail]:          { label: "Plate Mail Armor",    host: BuildingType.Blacksmith,  minAge: Age.Imperial, food: 300, wood:  0, gold: 150, time: 32, prereq: TechId.ChainMail },
  [TechId.RingArcherArmor]:    { label: "Ring Archer Armor",   host: BuildingType.Blacksmith,  minAge: Age.Imperial, food: 250, wood:  0, gold: 200, time: 32, prereq: TechId.LeatherArcherArmor },
  [TechId.PlateBarding]:       { label: "Plate Barding",       host: BuildingType.Blacksmith,  minAge: Age.Imperial, food: 350, wood:  0, gold: 200, time: 32, prereq: TechId.ChainBarding },
  [TechId.Bracer]:             { label: "Bracer",              host: BuildingType.Blacksmith,  minAge: Age.Imperial, food: 200, wood:  0, gold: 175, time: 30, prereq: TechId.Bodkin },
  // ── Gather Feudal ────────────────────────────────────────────────────────
  [TechId.DoubleBitAxe]:       { label: "Double-Bit Axe",      host: BuildingType.LumberCamp,  minAge: Age.Feudal,   food: 100, wood:  0, gold:   0, time: 18 },
  [TechId.Wheelbarrow]:        { label: "Wheelbarrow",          host: BuildingType.TownCenter,  minAge: Age.Feudal,   food: 150, wood: 50, gold:   0, time: 22 },
  [TechId.HorseCollar]:        { label: "Horse Collar",         host: BuildingType.Mill,        minAge: Age.Feudal,   food:  75, wood:  0, gold:   0, time: 20 },
  [TechId.Loom]:               { label: "Loom",                 host: BuildingType.TownCenter,  minAge: Age.Dark,     food:   0, wood:  0, gold:  50, time: 25 },
  [TechId.GoldMining]:         { label: "Gold Mining",          host: BuildingType.MiningCamp,  minAge: Age.Feudal,   food: 100, wood:  0, gold:  75, time: 22 },
  [TechId.StoneMining]:        { label: "Stone Mining",         host: BuildingType.MiningCamp,  minAge: Age.Feudal,   food: 100, wood:  0, gold:  75, time: 22 },
  // ── Gather Castle ────────────────────────────────────────────────────────
  [TechId.BowSaw]:             { label: "Bow Saw",              host: BuildingType.LumberCamp,  minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 25, prereq: TechId.DoubleBitAxe },
  [TechId.HandCart]:           { label: "Hand Cart",            host: BuildingType.TownCenter,  minAge: Age.Castle,   food: 300, wood:200, gold:   0, time: 35, prereq: TechId.Wheelbarrow },
  [TechId.HeavyPlow]:          { label: "Heavy Plow",           host: BuildingType.Mill,        minAge: Age.Castle,   food: 125, wood:  0, gold:   0, time: 25, prereq: TechId.HorseCollar },
  [TechId.GoldShaftMining]:    { label: "Gold Shaft Mining",    host: BuildingType.MiningCamp,  minAge: Age.Castle,   food: 200, wood:  0, gold: 100, time: 28, prereq: TechId.GoldMining },
  [TechId.StoneMiningUpgrade]: { label: "Stone Shaft Mining",   host: BuildingType.MiningCamp,  minAge: Age.Castle,   food: 200, wood:  0, gold: 100, time: 28, prereq: TechId.StoneMining },
  // ── Gather Imperial ──────────────────────────────────────────────────────
  [TechId.CropRotation]:       { label: "Crop Rotation",        host: BuildingType.Mill,        minAge: Age.Imperial, food: 250, wood:  0, gold: 100, time: 28, prereq: TechId.HeavyPlow },
  // ── Military Feudal ──────────────────────────────────────────────────────
  [TechId.ManAtArms]:          { label: "Man-at-Arms",          host: BuildingType.Barracks,    minAge: Age.Feudal,   food: 100, wood:  0, gold:  40, time: 25 },
  // ── Military Castle ──────────────────────────────────────────────────────
  [TechId.Longswordsman]:      { label: "Long Swordsman",       host: BuildingType.Barracks,    minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 30, prereq: TechId.ManAtArms },
  [TechId.Bloodlines]:         { label: "Bloodlines",           host: BuildingType.Stable,      minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 25 },
  [TechId.Crossbowman]:        { label: "Crossbowman",          host: BuildingType.ArcheryRange, minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 30 },
  [TechId.Cavalier]:           { label: "Cavalier",             host: BuildingType.Stable,      minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 30 },
  [TechId.Pikeman]:            { label: "Pikeman",              host: BuildingType.Barracks,    minAge: Age.Castle,   food: 100, wood:  0, gold:  50, time: 28 },
  [TechId.LightCavalry]:       { label: "Light Cavalry",        host: BuildingType.Stable,      minAge: Age.Castle,   food: 150, wood:  0, gold:  50, time: 25 },
  [TechId.Husbandry]:          { label: "Husbandry",            host: BuildingType.Stable,      minAge: Age.Castle,   food: 150, wood:  0, gold:   0, time: 22 },
  // ── Military Imperial ────────────────────────────────────────────────────
  [TechId.TwoHandedSwordsman]: { label: "Two-Handed Swordsman", host: BuildingType.Barracks,    minAge: Age.Imperial, food: 150, wood:  0, gold: 120, time: 32, prereq: TechId.Longswordsman },
  [TechId.Champion]:           { label: "Champion",             host: BuildingType.Barracks,    minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 35, prereq: TechId.TwoHandedSwordsman },
  [TechId.Arbalest]:           { label: "Arbalest",             host: BuildingType.ArcheryRange, minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 35, prereq: TechId.Crossbowman },
  [TechId.Paladin]:            { label: "Paladin",              host: BuildingType.Stable,      minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 35, prereq: TechId.Cavalier },
  [TechId.Halberdier]:         { label: "Halberdier",           host: BuildingType.Barracks,    minAge: Age.Imperial, food: 150, wood:  0, gold: 100, time: 32, prereq: TechId.Pikeman },
  [TechId.EliteSkirmisher]:    { label: "Elite Skirmisher",     host: BuildingType.ArcheryRange, minAge: Age.Imperial, food: 150, wood:  0, gold: 100, time: 30 },
  [TechId.Hussar]:             { label: "Hussar",               host: BuildingType.Stable,      minAge: Age.Imperial, food: 150, wood:  0, gold: 100, time: 30, prereq: TechId.LightCavalry },
  // ── Market ───────────────────────────────────────────────────────────────
  [TechId.Caravan]:            { label: "Caravan",              host: BuildingType.Market,      minAge: Age.Castle,   food:   0, wood:  0, gold: 200, time: 28 },
  [TechId.Coinage]:            { label: "Coinage",              host: BuildingType.Market,      minAge: Age.Castle,   food:   0, wood:  0, gold: 200, time: 30 },
  [TechId.Banking]:            { label: "Banking",              host: BuildingType.Market,      minAge: Age.Imperial, food:   0, wood:  0, gold: 300, time: 35, prereq: TechId.Coinage },
  // ── University Castle ────────────────────────────────────────────────────
  [TechId.Ballistics]:         { label: "Ballistics",           host: BuildingType.University,  minAge: Age.Castle,   food: 300, wood:  0, gold: 175, time: 35 },
  [TechId.Masonry]:            { label: "Masonry",              host: BuildingType.University,  minAge: Age.Castle,   food: 150, wood:  0, gold:   0, time: 22 },
  [TechId.Architecture]:       { label: "Architecture",         host: BuildingType.University,  minAge: Age.Castle,   food: 300, wood:  0, gold:   0, time: 35, prereq: TechId.Masonry },
  [TechId.GuardTower]:         { label: "Guard Tower",          host: BuildingType.University,  minAge: Age.Castle,   food: 100, wood:  0, gold:  50, time: 22 },
  // ── University Imperial ──────────────────────────────────────────────────
  [TechId.Chemistry]:          { label: "Chemistry",            host: BuildingType.University,  minAge: Age.Imperial, food: 300, wood:  0, gold: 200, time: 40 },
  [TechId.Keep]:               { label: "Keep",                 host: BuildingType.University,  minAge: Age.Imperial, food: 150, wood:  0, gold: 100, time: 28, prereq: TechId.GuardTower },
  [TechId.Fortified]:          { label: "Fortified Wall",       host: BuildingType.University,  minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 30 },
  // ── Monastery Castle ─────────────────────────────────────────────────────
  [TechId.Sanctity]:           { label: "Sanctity",             host: BuildingType.Monastery,   minAge: Age.Castle,   food: 120, wood:  0, gold:   0, time: 30 },
  [TechId.BlockPrinting]:      { label: "Block Printing",       host: BuildingType.Monastery,   minAge: Age.Castle,   food:   0, wood:  0, gold: 200, time: 32 },
  [TechId.Redemption]:         { label: "Redemption",           host: BuildingType.Monastery,   minAge: Age.Castle,   food:   0, wood:  0, gold: 475, time: 35 },
  // ── Monastery Imperial ───────────────────────────────────────────────────
  [TechId.Theocracy]:          { label: "Theocracy",            host: BuildingType.Monastery,   minAge: Age.Imperial, food:   0, wood:  0, gold: 200, time: 40 },
  // ── Castle Unique — Feudal Castle Age ────────────────────────────────────
  [TechId.Chivalry]:      { label: "Şövalyelik",     host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 400, time: 40, civGate: Civilization.Franks },
  [TechId.Ironclad]:      { label: "Zırhlı",          host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 400, time: 40, civGate: Civilization.Teutons },
  [TechId.Yeomen]:        { label: "Yeomen",           host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 350, time: 40, civGate: Civilization.Britons },
  [TechId.Nomads]:        { label: "Göçebeler",        host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 300, time: 40, civGate: Civilization.Mongols },
  [TechId.Yasama]:        { label: "Yasama",           host: BuildingType.Castle, minAge: Age.Castle,   food: 100, wood: 0, gold: 100, time: 40, civGate: Civilization.Japanese },
  [TechId.Kamandaran]:    { label: "Kamandaran",       host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 300, time: 40, civGate: Civilization.Persians },
  [TechId.Atlatl]:        { label: "Atlatl",           host: BuildingType.Castle, minAge: Age.Castle,   food: 400, wood: 0, gold: 350, time: 40, civGate: Civilization.Aztecs },
  [TechId.GreekFire]:     { label: "Rum Ateşi",        host: BuildingType.Castle, minAge: Age.Castle,   food: 300, wood: 0, gold: 100, time: 40, civGate: Civilization.Byzantines },
  [TechId.Chieftains]:    { label: "Reisler",          host: BuildingType.Castle, minAge: Age.Castle,   food: 400, wood: 0, gold: 200, time: 40, civGate: Civilization.Vikings },
  [TechId.Madrasah]:      { label: "Medrese",          host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 200, time: 40, civGate: Civilization.Saracens },
  [TechId.Stronghold]:    { label: "Müstahkem Mevki",  host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 300, time: 40, civGate: Civilization.Celts },
  [TechId.GreatWall]:     { label: "Çin Seddi",        host: BuildingType.Castle, minAge: Age.Castle,   food: 400, wood: 0, gold: 200, time: 40, civGate: Civilization.Chinese },
  [TechId.Anarchy]:       { label: "Anarşi",           host: BuildingType.Castle, minAge: Age.Castle,   food:   0, wood: 0, gold: 350, time: 40, civGate: Civilization.Goths },
  [TechId.Sipahi]:        { label: "Sipahi",           host: BuildingType.Castle, minAge: Age.Castle,   food: 350, wood: 0, gold: 150, time: 40, civGate: Civilization.Turks },
  // ── Castle Unique — Imperial Age ─────────────────────────────────────────
  [TechId.BeardedAxe]:    { label: "Sakallı Balta",    host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 400, time: 40, civGate: Civilization.Franks },
  [TechId.Crenellations]: { label: "Mazgallar",        host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 400, time: 40, civGate: Civilization.Teutons },
  [TechId.Warwolf]:       { label: "Warwolf",          host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 800, time: 40, civGate: Civilization.Britons },
  [TechId.Drill]:         { label: "Talim",            host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 500, time: 40, civGate: Civilization.Mongols },
  [TechId.Kataparuto]:    { label: "Kataparuto",       host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 750, time: 40, civGate: Civilization.Japanese },
  [TechId.Mahouts]:       { label: "Mahut",            host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 300, time: 40, civGate: Civilization.Persians },
  [TechId.GarlandWars]:   { label: "Çiçek Savaşları",  host: BuildingType.Castle, minAge: Age.Imperial, food: 450, wood: 0, gold: 750, time: 40, civGate: Civilization.Aztecs },
  [TechId.Logistica]:     { label: "Lojistika",        host: BuildingType.Castle, minAge: Age.Imperial, food: 500, wood: 0, gold: 600, time: 40, civGate: Civilization.Byzantines },
  [TechId.Berserkergang]: { label: "Berserkergang",    host: BuildingType.Castle, minAge: Age.Imperial, food: 300, wood: 0, gold: 350, time: 40, civGate: Civilization.Vikings },
  [TechId.Zealotry]:      { label: "Bağnazlık",        host: BuildingType.Castle, minAge: Age.Imperial, food: 750, wood: 0, gold: 800, time: 40, civGate: Civilization.Saracens },
  [TechId.FurorCeltica]:  { label: "Furor Celtica",    host: BuildingType.Castle, minAge: Age.Imperial, food: 750, wood: 0, gold: 450, time: 40, civGate: Civilization.Celts },
  [TechId.Rocketry]:      { label: "Roket",            host: BuildingType.Castle, minAge: Age.Imperial, food: 600, wood: 0, gold: 600, time: 40, civGate: Civilization.Chinese },
  [TechId.Perfusion]:     { label: "Perfüzyon",        host: BuildingType.Castle, minAge: Age.Imperial, food:   0, wood: 0, gold: 450, time: 40, civGate: Civilization.Goths },
  [TechId.Artillery]:     { label: "Topçuluk",         host: BuildingType.Castle, minAge: Age.Imperial, food: 500, wood: 0, gold: 450, time: 40, civGate: Civilization.Turks },
};

/** Techs available per building type (player-facing order). */
export const BUILDING_TECHS: Partial<Record<BuildingType, TechId[]>> = {
  [BuildingType.Blacksmith]:  [
    TechId.Fletching, TechId.Forging, TechId.PaddedArcherArmor, TechId.ScaleMail, TechId.ScaleBarding,
    TechId.IronCasting, TechId.ChainMail, TechId.LeatherArcherArmor, TechId.Bodkin, TechId.ChainBarding,
    TechId.BlastFurnace, TechId.PlateMail, TechId.RingArcherArmor, TechId.PlateBarding, TechId.Bracer,
  ],
  [BuildingType.LumberCamp]:  [TechId.DoubleBitAxe, TechId.BowSaw],
  [BuildingType.MiningCamp]:  [TechId.GoldMining, TechId.StoneMining, TechId.GoldShaftMining, TechId.StoneMiningUpgrade],
  [BuildingType.Mill]:        [TechId.HorseCollar, TechId.HeavyPlow, TechId.CropRotation],
  [BuildingType.TownCenter]:  [TechId.Loom, TechId.Wheelbarrow, TechId.HandCart],
  [BuildingType.Barracks]:    [TechId.ManAtArms, TechId.Longswordsman, TechId.Pikeman, TechId.TwoHandedSwordsman, TechId.Champion, TechId.Halberdier],
  [BuildingType.Stable]:      [TechId.Bloodlines, TechId.Husbandry, TechId.LightCavalry, TechId.Cavalier, TechId.Paladin, TechId.Hussar],
  [BuildingType.ArcheryRange]:[TechId.Crossbowman, TechId.Arbalest, TechId.EliteSkirmisher],
  [BuildingType.Market]:      [TechId.Caravan, TechId.Coinage, TechId.Banking],
  [BuildingType.University]:  [TechId.Ballistics, TechId.Masonry, TechId.Architecture, TechId.GuardTower, TechId.Chemistry, TechId.Keep, TechId.Fortified],
  [BuildingType.Monastery]:   [TechId.Sanctity, TechId.BlockPrinting, TechId.Redemption, TechId.Theocracy],
  // Castle techs — civ-gated; available() filters by civGate
  [BuildingType.Castle]: [
    // Castle Age unique techs
    TechId.Chivalry, TechId.Ironclad, TechId.Yeomen, TechId.Nomads, TechId.Yasama,
    TechId.Kamandaran, TechId.Atlatl, TechId.GreekFire, TechId.Chieftains, TechId.Madrasah,
    TechId.Stronghold, TechId.GreatWall, TechId.Anarchy, TechId.Sipahi,
    // Imperial Age unique techs
    TechId.BeardedAxe, TechId.Crenellations, TechId.Warwolf, TechId.Drill, TechId.Kataparuto,
    TechId.Mahouts, TechId.GarlandWars, TechId.Logistica, TechId.Berserkergang, TechId.Zealotry,
    TechId.FurorCeltica, TechId.Rocketry, TechId.Perfusion, TechId.Artillery,
  ],
};

/** Per-civ denied techs — N0.7 port. Only includes techs present in this web port. */
const DENIED_TECHS: Partial<Record<Civilization, ReadonlySet<TechId>>> = {
  [Civilization.Franks]:   new Set([TechId.Halberdier, TechId.Arbalest]),
  [Civilization.Britons]:  new Set([TechId.Paladin]),
  [Civilization.Mongols]:  new Set([TechId.Halberdier, TechId.Paladin]),
  [Civilization.Japanese]: new Set([TechId.Paladin]),
  [Civilization.Aztecs]:   new Set([TechId.Cavalier, TechId.Paladin, TechId.Bloodlines, TechId.Husbandry]),
  [Civilization.Vikings]:  new Set([TechId.Paladin]),
  [Civilization.Celts]:    new Set([TechId.Paladin, TechId.Arbalest]),
  [Civilization.Chinese]:  new Set([TechId.Halberdier]),
  [Civilization.Goths]:    new Set([TechId.Paladin, TechId.Arbalest]),
  [Civilization.Turks]:    new Set([TechId.Halberdier, TechId.EliteSkirmisher]),
};

interface QueueEntry { tech: TechId; timer: number; total: number; }

export class ResearchSystem {
  /** Per-team set of completed techs. */
  private readonly done = new Map<number, Set<TechId>>();
  /** Per-building active research queue (max 1 at a time). */
  private readonly queues = new Map<Building, QueueEntry>();
  /** Called when a tech completes — cosmetic seam for SFX/UI. */
  onComplete: ((teamId: number, tech: TechId) => void) | null = null;

  isResearched(teamId: number, tech: TechId): boolean {
    return this.done.get(teamId)?.has(tech) ?? false;
  }

  /** Returns the in-progress entry for a building (for HUD progress bar). */
  active(b: Building): QueueEntry | undefined {
    return this.queues.get(b);
  }

  /** Returns all available (not yet researched, prereqs met, age ok, civ allowed, not denied) techs for a building. */
  available(b: Building, rm: ResourceManager): TechId[] {
    const list = BUILDING_TECHS[b.buildingType] ?? [];
    const teamCiv = getTeamCiv(b.teamId);
    const denied = DENIED_TECHS[teamCiv];
    return list.filter(t => {
      if (this.isResearched(b.teamId, t)) return false;
      if (denied?.has(t)) return false;
      const def = TECH_DEFS[t];
      if (rm.age < def.minAge) return false;
      if (def.prereq && !this.isResearched(b.teamId, def.prereq)) return false;
      if (def.civGate !== undefined && def.civGate !== teamCiv) return false;
      return true;
    });
  }

  start(b: Building, tech: TechId, rm: ResourceManager): boolean {
    if (this.queues.has(b)) return false; // already busy
    if (this.isResearched(b.teamId, tech)) return false;
    const def = TECH_DEFS[tech];
    if (rm.age < def.minAge) return false;
    if (def.prereq && !this.isResearched(b.teamId, def.prereq)) return false;
    const teamCiv = getTeamCiv(b.teamId);
    if (DENIED_TECHS[teamCiv]?.has(tech)) return false;
    if (def.civGate !== undefined && def.civGate !== teamCiv) return false;
    if (!rm.canAfford(def.food, def.wood, def.gold)) return false;
    rm.deduct(def.food, def.wood, def.gold);
    this.queues.set(b, { tech, timer: def.time, total: def.time });
    return true;
  }

  tick(units: Unit[], buildings: Building[], teamRes: ResourceManager[], dt: number) {
    for (const [b, entry] of this.queues) {
      entry.timer -= dt;
      if (entry.timer <= 0) {
        this.queues.delete(b);
        this._complete(b.teamId, entry.tech, units, buildings, teamRes);
      }
    }
  }

  /** Apply all completed techs for a team to a single newly-spawned unit. */
  applyCompletedResearchTo(u: Unit, teamId: number) {
    const techs = this.done.get(teamId);
    if (!techs) return;
    for (const tech of techs) applyTechBonus(u, tech);
  }

  private _complete(teamId: number, tech: TechId, units: Unit[], buildings: Building[], teamRes: ResourceManager[]) {
    let set = this.done.get(teamId);
    if (!set) { set = new Set(); this.done.set(teamId, set); }
    set.add(tech);
    for (const u of units) {
      if (u.teamId !== teamId || !u.alive) continue;
      applyTechBonus(u, tech);
    }
    applyGatherBonus(tech, teamRes[teamId]);
    applyBuildingBonus(tech, buildings.filter(b => b.teamId === teamId && b.alive));
    this.onComplete?.(teamId, tech);
  }
}

/** Apply a single tech bonus to an existing unit (retroactive on research). */
export function applyTechBonus(u: Unit, tech: TechId) {
  switch (tech) {
    // ── Blacksmith archer attack ───────────────────────────────────────────
    case TechId.Fletching:
      if (u.armorClass & ArmorClass.Archer) (u as { baseAtk: number }).baseAtk += 1;
      break;
    case TechId.Bodkin:
      if (u.armorClass & ArmorClass.Archer) (u as { baseAtk: number }).baseAtk += 1;
      break;
    // ── Blacksmith melee attack ────────────────────────────────────────────
    case TechId.Forging:
      if (u.armorClass & (ArmorClass.Infantry | ArmorClass.Cavalry)) (u as { baseAtk: number }).baseAtk += 1;
      break;
    case TechId.IronCasting:
      if (u.armorClass & (ArmorClass.Infantry | ArmorClass.Cavalry)) (u as { baseAtk: number }).baseAtk += 1;
      break;
    case TechId.BlastFurnace:
      if (u.armorClass & (ArmorClass.Infantry | ArmorClass.Cavalry)) (u as { baseAtk: number }).baseAtk += 2;
      break;
    // ── Blacksmith infantry armor ──────────────────────────────────────────
    case TechId.ScaleMail:
      if (u.armorClass & ArmorClass.Infantry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.ChainMail:
      if (u.armorClass & ArmorClass.Infantry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.PlateMail:
      if (u.armorClass & ArmorClass.Infantry) { (u as { armorMelee: number }).armorMelee += 2; (u as { armorPierce: number }).armorPierce += 2; }
      break;
    // ── Blacksmith cavalry barding ─────────────────────────────────────────
    case TechId.ScaleBarding:
      if (u.armorClass & ArmorClass.Cavalry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.ChainBarding:
      if (u.armorClass & ArmorClass.Cavalry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.PlateBarding:
      if (u.armorClass & ArmorClass.Cavalry) { (u as { armorMelee: number }).armorMelee += 2; (u as { armorPierce: number }).armorPierce += 2; }
      break;
    // ── Blacksmith archer armor ────────────────────────────────────────────
    case TechId.PaddedArcherArmor:
      if (u.armorClass & ArmorClass.Archer) (u as { armorPierce: number }).armorPierce += 1;
      break;
    case TechId.LeatherArcherArmor:
      if (u.armorClass & ArmorClass.Archer) (u as { armorPierce: number }).armorPierce += 1;
      break;
    case TechId.RingArcherArmor:
      if (u.armorClass & ArmorClass.Archer) (u as { armorPierce: number }).armorPierce += 1;
      break;
    // ── Loom (Villager defense) ────────────────────────────────────────────
    case TechId.Loom:
      if (u.unitType === UnitType.Villager) {
        (u as { maxHp: number }).maxHp += 15;
        u.hp = Math.min(u.hp + 15, u.maxHp);
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { armorPierce: number }).armorPierce += 1;
      }
      break;
    // ── Wheelbarrow / Hand Cart (Villager speed) ───────────────────────────
    case TechId.Wheelbarrow:
      if (u.unitType === UnitType.Villager) (u as { moveSpeed: number }).moveSpeed += 0.1;
      break;
    case TechId.HandCart:
      if (u.unitType === UnitType.Villager) (u as { moveSpeed: number }).moveSpeed += 0.1;
      break;
    // ── Cavalry upgrades ───────────────────────────────────────────────────
    case TechId.Bloodlines:
      if (u.armorClass & ArmorClass.Cavalry) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    case TechId.Cavalier:
      if (u.unitType === UnitType.Cavalry) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
        (u as { baseAtk: number }).baseAtk += 2;
      }
      break;
    case TechId.Paladin:
      if (u.unitType === UnitType.Cavalry) {
        (u as { maxHp: number }).maxHp += 25;
        u.hp = Math.min(u.hp + 25, u.maxHp);
        (u as { baseAtk: number }).baseAtk += 3;
      }
      break;
    // ── Militia line ───────────────────────────────────────────────────────
    case TechId.ManAtArms:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
      }
      break;
    case TechId.Longswordsman:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { maxHp: number }).maxHp += 15;
        (u as { hp: number }).hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    case TechId.TwoHandedSwordsman:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { maxHp: number }).maxHp += 15;
        (u as { hp: number }).hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    case TechId.Champion:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { maxHp: number }).maxHp += 20;
        (u as { hp: number }).hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    // ── Spearman line ──────────────────────────────────────────────────────
    case TechId.Pikeman:
      if (u.unitType === UnitType.Spearman) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { maxHp: number }).maxHp += 15;
        (u as { hp: number }).hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    case TechId.Halberdier:
      if (u.unitType === UnitType.Spearman) {
        (u as { baseAtk: number }).baseAtk += 3;
        (u as { maxHp: number }).maxHp += 20;
        (u as { hp: number }).hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    // ── Archer line ────────────────────────────────────────────────────────
    case TechId.Crossbowman:
      if (u.unitType === UnitType.Archer) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { maxHp: number }).maxHp += 10;
        u.hp = Math.min(u.hp + 10, u.maxHp);
      }
      break;
    case TechId.Arbalest:
      if (u.unitType === UnitType.Archer) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { maxHp: number }).maxHp += 15;
        u.hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    // ── Elite Skirmisher ───────────────────────────────────────────────────
    case TechId.EliteSkirmisher:
      if (u.unitType === UnitType.Skirmisher) {
        (u as { baseAtk: number }).baseAtk += 1;
        (u as { maxHp: number }).maxHp += 10;
        u.hp = Math.min(u.hp + 10, u.maxHp);
      }
      break;
    // ── Bracer (archer attack + range) ────────────────────────────────────
    case TechId.Bracer:
      if (u.armorClass & ArmorClass.Archer) {
        (u as { baseAtk: number }).baseAtk += 1;
        (u as { attackRange: number }).attackRange += 1;
      }
      break;
    // ── Light Cavalry / Hussar (Scout line) ───────────────────────────────
    case TechId.LightCavalry:
      if (u.unitType === UnitType.Scout) {
        (u as { maxHp: number }).maxHp += 10;
        u.hp = Math.min(u.hp + 10, u.maxHp);
        (u as { baseAtk: number }).baseAtk += 2;
      }
      break;
    case TechId.Hussar:
      if (u.unitType === UnitType.Scout) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
        (u as { baseAtk: number }).baseAtk += 2;
      }
      break;
    // ── Husbandry (cavalry speed) ──────────────────────────────────────────
    case TechId.Husbandry:
      if (u.armorClass & ArmorClass.Cavalry) {
        (u as { moveSpeed: number }).moveSpeed *= 1.1;
      }
      break;
    // ── Ballistics (lead targeting vs moving units + minor accuracy buff) ──
    case TechId.Ballistics:
      if (u.isRanged) {
        (u as { baseAtk: number }).baseAtk += 1;
        u.hasBallistics = true;
      }
      break;
    // ── Chemistry (ranged +1 attack) ──────────────────────────────────────
    case TechId.Chemistry:
      if (u.isRanged) (u as { baseAtk: number }).baseAtk += 1;
      break;
    // ── Sanctity (Monk +50 HP) ────────────────────────────────────────────
    case TechId.Sanctity:
      if (u.unitType === UnitType.Monk) {
        (u as { maxHp: number }).maxHp += 50;
        u.hp = Math.min(u.hp + 50, u.maxHp);
      }
      break;
    // ── BlockPrinting (Monk +1 range) ────────────────────────────────────
    case TechId.BlockPrinting:
      if (u.unitType === UnitType.Monk) {
        (u as { attackRange: number }).attackRange += 1;
      }
      break;
    // ── Theocracy (Monk +10% speed) ───────────────────────────────────────
    case TechId.Theocracy:
      if (u.unitType === UnitType.Monk) {
        (u as { moveSpeed: number }).moveSpeed *= 1.1;
      }
      break;
    // ── Civ unique techs (Castle) ─────────────────────────────────────────
    case TechId.Chivalry:       // Franks: cavalry +1 melee armor
      if (u.armorClass & ArmorClass.Cavalry) (u as { armorMelee: number }).armorMelee += 1;
      break;
    case TechId.Yeomen:         // Britons: archers +1 attack, +2 range
      if (u.armorClass & ArmorClass.Archer) {
        (u as { baseAtk: number }).baseAtk += 1;
        (u as { attackRange: number }).attackRange += 2;
      }
      break;
    case TechId.Atlatl:         // Aztecs: archers/skirmishers +1 attack +1 range
      if (u.armorClass & ArmorClass.Archer) {
        (u as { baseAtk: number }).baseAtk += 1;
        (u as { attackRange: number }).attackRange += 1;
      }
      break;
    case TechId.Chieftains:     // Vikings: infantry +6 bonus vs cavalry
      if (u.armorClass & ArmorClass.Infantry) {
        const bvEntry = u.bonusVs.find(e => e.cls === ArmorClass.Cavalry);
        if (bvEntry) bvEntry.bonus += 6;
        else u.bonusVs.push({ cls: ArmorClass.Cavalry, bonus: 6 });
      }
      break;
    case TechId.Ironclad:       // Teutons: siege +4 melee armor
      if (u.armorClass & ArmorClass.Siege) (u as { armorMelee: number }).armorMelee += 4;
      break;
    case TechId.Sipahi:         // Turks: cavalry +20 HP
      if (u.armorClass & ArmorClass.Cavalry) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    // ── Civ unique techs (Imperial) ───────────────────────────────────────
    case TechId.BeardedAxe:     // Franks: infantry +2 attack
      if (u.armorClass & ArmorClass.Infantry) (u as { baseAtk: number }).baseAtk += 2;
      break;
    case TechId.Crenellations:  // Teutons: infantry +1 melee armor
      if (u.armorClass & ArmorClass.Infantry) (u as { armorMelee: number }).armorMelee += 1;
      break;
    case TechId.Warwolf:        // Britons: Trebuchet +2 attack
      if (u.unitType === UnitType.Trebuchet) (u as { baseAtk: number }).baseAtk += 2;
      break;
    case TechId.Drill:          // Mongols: siege +50% speed
      if (u.armorClass & ArmorClass.Siege) (u as { moveSpeed: number }).moveSpeed *= 1.5;
      break;
    case TechId.Kataparuto:     // Japanese: Trebuchet fires faster
      if (u.unitType === UnitType.Trebuchet) {
        (u as { attackInterval: number }).attackInterval *= 0.8;
      }
      break;
    case TechId.GarlandWars:    // Aztecs: infantry +4 attack
      if (u.armorClass & ArmorClass.Infantry) (u as { baseAtk: number }).baseAtk += 4;
      break;
    case TechId.Zealotry:       // Saracens: cavalry +20 HP
      if (u.armorClass & ArmorClass.Cavalry) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    case TechId.FurorCeltica:   // Celts: siege +50% HP
      if (u.armorClass & ArmorClass.Siege) {
        const bonus = Math.round(u.maxHp * 0.5);
        (u as { maxHp: number }).maxHp += bonus;
        u.hp = Math.min(u.hp + bonus, u.maxHp);
      }
      break;
    case TechId.Rocketry:       // Chinese: siege +2 range
      if (u.armorClass & ArmorClass.Siege) (u as { attackRange: number }).attackRange += 2;
      break;
    case TechId.Artillery:      // Turks: siege +2 range
      if (u.armorClass & ArmorClass.Siege) (u as { attackRange: number }).attackRange += 2;
      break;
    // No-op techs (complex mechanics not applicable to web unit model)
    case TechId.Nomads: case TechId.Yasama: case TechId.Kamandaran:
    case TechId.Mahouts: case TechId.GreekFire: case TechId.Logistica:
    case TechId.Berserkergang: case TechId.Madrasah: case TechId.Stronghold:
    case TechId.GreatWall: case TechId.Anarchy: case TechId.Perfusion:
      break;
  }
}

/** Apply research bonuses that affect gather rates (not unit stats). */
function applyGatherBonus(tech: TechId, rm: ResourceManager | undefined) {
  if (!rm) return;
  switch (tech) {
    case TechId.HorseCollar:        rm.techGatherFoodMult  += 0.15; break;
    case TechId.HeavyPlow:          rm.techGatherFoodMult  += 0.15; break;
    case TechId.CropRotation:       rm.techGatherFoodMult  += 0.15; break;
    case TechId.DoubleBitAxe:       rm.techGatherWoodMult  += 0.20; break;
    case TechId.BowSaw:             rm.techGatherWoodMult  += 0.20; break;
    case TechId.GoldMining:         rm.techGatherGoldMult  += 0.15; break;
    case TechId.GoldShaftMining:    rm.techGatherGoldMult  += 0.15; break;
    case TechId.StoneMining:        rm.techGatherStoneMult += 0.15; break;
    case TechId.StoneMiningUpgrade: rm.techGatherStoneMult += 0.15; break;
    case TechId.Caravan:            rm.techTradeCartSpeedMult = 1.5; break;
  }
}

/** Apply research bonuses that affect buildings retroactively. */
function applyBuildingBonus(tech: TechId, buildings: Building[]) {
  for (const b of buildings) {
    switch (tech) {
      case TechId.Masonry:
        (b as { maxHp: number }).maxHp = Math.round(b.maxHp * 1.1);
        b.hp = Math.min(b.hp, b.maxHp);
        break;
      case TechId.Architecture:
        (b as { maxHp: number }).maxHp = Math.round(b.maxHp * 1.1);
        b.hp = Math.min(b.hp, b.maxHp);
        break;
    }
  }
}
