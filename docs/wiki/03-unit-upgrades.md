# Birim Yükseltme Zincirleri — AoA Wiki

> AoA'da birim yükseltmeleri (Man-at-Arms, Crossbowman, Paladin, Pikeman, Hussar vb.)
> ayrı bir birim tipi yaratmaz. Bunun yerine **takım çapında stat bonusu** veren
> araştırılabilir teknolojilerdir: bir kez araştırılınca o takımın ilgili
> `UnitType`'ına ait *tüm* birimleri (geçmişte üretilmiş + ileride üretilecek)
> güçlenir. Birimin görünen adı ve `UnitType` enum'u değişmez; yalnızca
> saldırı/HP/zırh/menzil değerleri artar.
>
> **Kod kaynağı (tek doğruluk):**
> [TechDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs) (maliyet/çağ/önkoşul),
> [TechState.cs](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs) (bonus formülleri),
> [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs) (`TechType` enum),
> [ResearchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs) (retroaktif uygulama),
> [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs) (canlı okuma).

---

## 1. Ne olduğu

AoE2'de Militia → Man-at-Arms → Long Swordsman → Champion zinciri birimi *başka bir
birime dönüştürür*. AoA'da bu model **tier-terfi teknolojisi** olarak modellenmiştir:
her tier ayrı bir `TechType`'tır ([GameTypes.cs:83](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L83)) ve araştırıldığında
ilgili birim hattına eklenen stat bonusu **diğer tier'lerle birikir (stack)**.

AoA'da kodda tanımlı **birim yükseltme hatları** (her biri bir `UnitType`'ı besler):

| Hat | `UnitType` | Tier 2 | Tier 3 | Tier 4 (Imperial) | Üretildiği bina |
|---|---|---|---|---|---|
| Piyade (Militia line) | `Militia` | `ManAtArms` (Feudal) | `Longswordsman` (Castle) | `TwoHandedSwordsman` → `Champion` (Imperial) | Barracks |
| Mızrakçı (Spearman line) | `Spearman` | `Pikeman` (Castle) | `Halberdier` (Imperial) | — | Barracks |
| Okçu (Archer line) | `Archer` | `Crossbowman` (Castle) | `Arbalest` (Imperial) | — | ArcheryRange |
| Avcı (Skirmisher line) | `Skirmisher` | `EliteSkirmisher` (Imperial) | — | — | ArcheryRange |
| Süvari (Cavalry line) | `Cavalry` | `Cavalier` (Castle) | `Paladin` (Imperial) | — | Stable |
| Deve (Camel line) | `Camel` | `HeavyCamel` (Imperial) | — | — | Stable |
| Keşif (Scout line) | `Scout` | `LightCavalry` (Castle) | `Hussar` (Imperial) | — | Stable |
| Atlı Okçu (Cavalry Archer line) | `CavalryArcher` | `HeavyCavalryArcher` (Imperial) | — | — | Stable |
| Gemi (Galley line) | `Galley` | `WarGalley` (Castle) | `Galleon` (Imperial) | — | Dock |
| Kartal (Eagle line, Aztek) | `Eagle` | `EliteEagle` (Imperial) | — | — | Barracks |

Piyade hattı en uzun olanıdır (5 kademe): Militia → ManAtArms → Longswordsman →
TwoHandedSwordsman → Champion. `ManAtArms` zaten Feudal'da gelir.

Bunlara ek olarak **flat "blacksmith" yükseltmeleri** (Forging/IronCasting/BlastFurnace,
Fletching/Bodkin/Bracer, ScaleMail, Bloodlines, zırh hatları) tier değildir ama aynı
birim statlarını besler — efektif statı belirledikleri için aşağıda formüllere dahildirler.

---

## 2. Nasıl çalışır (mekanik + formül)

**Araştırma akışı.** Bir tech `ResearchSystem.Apply(type, teamId)` ile uygulanır:

1. Uygulama öncesi her `UnitType` için mevcut HP bonusu kaydedilir.
2. `tech.Mark(type)` ile araştırılan set'e eklenir; `Version` artar
   ([TechState.cs:20](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L20)).
3. HP delta'sı (yeni − eski) hesaplanır ve o takımın **zaten sahada olan** tüm
   birimlerine `maxHp += d; hp += d` olarak uygulanır.

Yani HP yükseltmesi **retroaktiftir** — eski birimler de anında güçlenir.

**Canlı okunan statlar.** Saldırı, menzil ve zırh bonusları depolanmaz; her çağrıda
`TechState`'ten *canlı* okunur:

- Saldırı: `AttackDamage = BaseAttackDamage + TeamTech.AttackBonus(type)`
  ([UnitEntity.cs:152](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L152)).
- Menzil: `AttackRange = BaseAttackRange + TeamTech.RangeBonus(type)`.
- Zırh: `ArmorBonus(type, dmg)` `TakeDamage` içinde canlı okunur
  ([TechState.cs:178](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L178)).
- HP: yeni doğan birim `Start()`'ta o anki `HpBonus`'u alır.

**Birikim (stack) formülleri** ([TechState.cs:37](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L37)):

```
# Saldırı hat bonusları (additive)
MilitiaLineAtk    = ManAtArms+1, Longswordsman+2, TwoHandedSwordsman+2, Champion+2 (+ BeardedAxe+2 — Frank civ)
SpearmanLineAtk   = Pikeman+2, Halberdier+3
ArcherLineAtk     = Crossbowman+2, Arbalest+2
SkirmisherLineAtk = EliteSkirmisher+1
CavalryLineAtk    = Cavalier+2, Paladin+3
CamelLineAtk      = HeavyCamel+3
ScoutLineAtk      = LightCavalry+5, Hussar+2      (Scout taban atk 0 → tek savaş kaynağı)
CavArcherLineAtk  = HeavyCavalryArcher+2
GalleyLineAtk     = WarGalley+2, Galleon+2 (+ Chemistry+1)
EagleAtk          = EliteEagle+3

# Flat blacksmith katkıları (hatla toplanır)
MeleeAttackBonus  = Forging+2, IronCasting+1, BlastFurnace+2   (Militia/Spearman/Cavalry/Camel)
ArcherAttackBonus = Fletching+1, Bodkin+1, Bracer+1, Chemistry+1 (Archer/Skirmisher/CavArcher)

# Menzil — yalnız archer sınıfı
RangeBonus(Archer) = Bracer+0.5 + Fletching+0.5 + Crossbowman+0.5 + Arbalest+0.5
RangeBonus(Skirmisher/CavalryArcher/Longbowman) = Bracer+0.5

# Zırh tier katkısı (ARMR) — blacksmith zırh + tier başına +1
Militia melee armor += Longswordsman+1, Champion+1
Spearman melee armor += Pikeman+1, Halberdier+1
Cavalry melee armor += Cavalier+1, Paladin+1
Archer pierce armor += Crossbowman+1, Arbalest+1
```

**Önkoşul zinciri.** Imperial/Castle tier'leri bir önceki tier'i ister
([TechDefs.cs:67](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L67)). `ForBuilding`
filtresi hem çağı, hem önkoşulu, hem de civ kapısını doğrular
([TechDefs.cs:158](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L158)): örn. `Champion`
ancak `TwoHandedSwordsman`, o da ancak `Longswordsman`, o da ancak `ManAtArms`
araştırılmışsa listelenir. Önkoşulsuz tier 2'ler (`Crossbowman`, `Cavalier`,
`Pikeman`, `LightCavalry`, `WarGalley`, `HeavyCamel` vb.) yalnız çağ şartına tabidir.
`EliteEagle` ise `Civilization.Aztecs`'e kapalıdır
([TechDefs.cs:135](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L135)).

---

## 3. Gerçek statlar (koddan)

### 3.1 Tier-terfi teknolojileri — maliyet / çağ / önkoşul

| Tech | Hat | Çağ | Food | Gold | Süre (s) | Bina | Önkoşul | Kaynak |
|---|---|---|---|---|---|---|---|---|
| `ManAtArms` | Militia | Feudal | 100 | 40 | 25 | Barracks | — | [TechDefs.cs:66](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L66) |
| `Longswordsman` | Militia | Castle | 150 | 100 | 30 | Barracks | `ManAtArms` | [TechDefs.cs:67](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L67) |
| `TwoHandedSwordsman` | Militia | Imperial | 150 | 120 | 32 | Barracks | `Longswordsman` | [TechDefs.cs:68](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L68) |
| `Champion` | Militia | Imperial | 200 | 150 | 35 | Barracks | `TwoHandedSwordsman` | [TechDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L69) |
| `Pikeman` | Spearman | Castle | 100 | 50 | 28 | Barracks | — | [TechDefs.cs:75](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L75) |
| `Halberdier` | Spearman | Imperial | 150 | 100 | 32 | Barracks | `Pikeman` | [TechDefs.cs:76](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L76) |
| `Crossbowman` | Archer | Castle | 150 | 100 | 30 | ArcheryRange | — | [TechDefs.cs:70](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L70) |
| `Arbalest` | Archer | Imperial | 200 | 150 | 35 | ArcheryRange | `Crossbowman` | [TechDefs.cs:71](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L71) |
| `EliteSkirmisher` | Skirmisher | Imperial | 150 | 100 | 30 | ArcheryRange | — | [TechDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L77) |
| `Cavalier` | Cavalry | Castle | 150 | 100 | 30 | Stable | — | [TechDefs.cs:72](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L72) |
| `Paladin` | Cavalry | Imperial | 200 | 150 | 35 | Stable | `Cavalier` | [TechDefs.cs:73](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L73) |
| `HeavyCamel` | Camel | Imperial | 150 | 100 | 30 | Stable | — | [TechDefs.cs:78](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L78) |
| `LightCavalry` | Scout | Castle | 150 | 50 | 25 | Stable | — | [TechDefs.cs:79](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L79) |
| `Hussar` | Scout | Imperial | 150 | 100 | 30 | Stable | `LightCavalry` | [TechDefs.cs:80](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L80) |
| `HeavyCavalryArcher` | CavalryArcher | Imperial | 150 | 125 | 30 | Stable | — | [TechDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L81) |
| `WarGalley` | Galley | Castle | 150 | 50 | 28 | Dock | — | [TechDefs.cs:82](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L82) |
| `Galleon` | Galley | Imperial | 150 | 100 | 32 | Dock | `WarGalley` | [TechDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L83) |
| `EliteEagle` | Eagle | Imperial | 200 | 100 | 35 | Barracks | *civ: Aztecs* | [TechDefs.cs:135](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L135) |

> Tüm bu tech'lerin wood/stone maliyeti 0'dır.

### 3.2 Tier başına stat artışı (additive bonus)

| Tech | Hat | +Saldırı | +HP | +Menzil | +Zırh | Kaynak (atk / hp / armor) |
|---|---|---|---|---|---|---|
| `ManAtArms` | Militia | +1 | +10 | — | — | [TechState.cs:37](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L37) / [TechState.cs:94](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L94) / — |
| `Longswordsman` | Militia | +2 | +15 | — | +1 melee | [TechState.cs:38](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L38) / [TechState.cs:95](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L95) / [TechState.cs:161](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L161) |
| `TwoHandedSwordsman` | Militia | +2 | +15 | — | — | [TechState.cs:39](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L39) / [TechState.cs:96](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L96) / — |
| `Champion` | Militia | +2 | +20 | — | +1 melee | [TechState.cs:40](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L40) / [TechState.cs:97](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L97) / [TechState.cs:161](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L161) |
| `Pikeman` | Spearman | +2 | +15 | — | +1 melee | [TechState.cs:47](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L47) / [TechState.cs:106](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L106) / [TechState.cs:162](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L162) |
| `Halberdier` | Spearman | +3 | +20 | — | +1 melee | [TechState.cs:48](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L48) / [TechState.cs:107](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L107) / [TechState.cs:162](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L162) |
| `Crossbowman` | Archer | +2 | +10 | +0.5 | +1 pierce | [TechState.cs:44](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L44) / [TechState.cs:103](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L103) / [TechState.cs:173](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L173) |
| `Arbalest` | Archer | +2 | +15 | +0.5 | +1 pierce | [TechState.cs:45](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L45) / [TechState.cs:104](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L104) / [TechState.cs:173](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L173) |
| `EliteSkirmisher` | Skirmisher | +1 | +10 | — | — | [TechState.cs:49](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L49) / [TechState.cs:108](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L108) / — |
| `Cavalier` | Cavalry | +2 | +20 | — | +1 melee | [TechState.cs:42](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L42) / [TechState.cs:100](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L100) / [TechState.cs:163](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L163) |
| `Paladin` | Cavalry | +3 | +25 | — | +1 melee | [TechState.cs:43](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L43) / [TechState.cs:101](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L101) / [TechState.cs:163](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L163) |
| `HeavyCamel` | Camel | +3 | +20 | — | — | [TechState.cs:50](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L50) / [TechState.cs:110](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L110) / — |
| `LightCavalry` | Scout | +5 | +15 | — | — | [TechState.cs:52](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L52) / [TechState.cs:111](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L111) / — |
| `Hussar` | Scout | +2 | +15 | — | — | [TechState.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L53) / [TechState.cs:112](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L112) / — |
| `HeavyCavalryArcher` | CavalryArcher | +2 | +20 | — | — | [TechState.cs:54](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L54) / [TechState.cs:115](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L115) / — |
| `WarGalley` | Galley | +2 | +20 | — | — | [TechState.cs:55](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L55) / [TechState.cs:116](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L116) / — |
| `Galleon` | Galley | +2 | +30 | — | — | [TechState.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L56) / [TechState.cs:117](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L117) / — |
| `EliteEagle` | Eagle | +3 | +20 | — | — | [TechState.cs:71](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L71) / [TechState.cs:120](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L120) / — |

> Not: `Scout` taban saldırısı 0'dır ([UnitEntity.cs:133](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133));
> savaş gücü yalnız `LightCavalry`/`Hussar` tech'lerinden gelir. `Scout`/`Camel`/
> `CavalryArcher` HP'sine ayrıca `Bloodlines` (+20) de katkı verir
> ([TechState.cs:109](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L109)).

### 3.3 Destekleyici flat yükseltmeler (tier değil, ama statı besler)

| Tech | Etkilediği | +Saldırı | +HP | +Menzil | Kaynak |
|---|---|---|---|---|---|
| `Forging` / `IronCasting` / `BlastFurnace` | Militia, Spearman, Cavalry, Camel | +2 / +1 / +2 | — | — | [TechState.cs:25](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L25) |
| `Fletching` / `Bodkin` / `Bracer` | Archer, Skirmisher, CavArcher | +1 / +1 / +1 | — | (Bracer +0.5) | [TechState.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L29) |
| `Chemistry` | tüm missile (Archer/CavArcher/Galley) | +1 | — | — | [TechState.cs:34](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L34) |
| `ScaleMail` | Militia, Spearman, Cavalry | — | +20 | — | [TechState.cs:93](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L93) |
| `Bloodlines` | Cavalry, Camel, Scout, CavArcher | — | +20 | — | [TechState.cs:99](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L99) |
| `BeardedAxe` (Frank civ) | Militia | +2 | — | — | [TechState.cs:41](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L41) |
| `Chivalry` (Frank civ) | Cavalry | — | +20 | — | [TechState.cs:102](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L102) |

> Ayrıca blacksmith zırh hatları (`ChainMail`/`PlateMail`, `Scale/Chain/PlateBarding`,
> `Padded/Leather/RingArcherArmor`) ve `Loom` (Villager) zırha katkı verir
> ([TechState.cs:140](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L140)) — bunlar
> tier hattı değildir.

### 3.4 Türetilen efektif statlar (taban + tüm tier'ler)

Aşağıdaki tablolar **sadece tier teknolojileri** araştırılmış varsayar (flat/civ
bonusları hariç) ve birikimli (kümülatif) toplamı gösterir. Taban statlar
[UnitFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs) ve
[UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs)'ten.

**Piyade hattı** (taban: Atk 5, HP 40 — [UnitFactory.cs:73](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L73), [UnitEntity.cs:122](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Militia (taban) | 5 | 40 | — |
| + ManAtArms | 6 | 50 | +1, +10 |
| + Longswordsman | 8 | 65 | +2, +15 |
| + TwoHandedSwordsman | 10 | 80 | +2, +15 |
| + Champion | 12 | 100 | +2, +20 |

**Mızrakçı hattı** (taban: Atk 4, HP 25 — [UnitFactory.cs:232](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L232), [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Spearman (taban) | 4 | 25 | — |
| + Pikeman | 6 | 40 | +2, +15 |
| + Halberdier | 9 | 60 | +3, +20 |

**Okçu hattı** (taban: Atk 4, HP 30, Menzil 6.5 — [UnitFactory.cs:95](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L95), [UnitEntity.cs:138](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L138)):

| Kademe | Atk | HP | Menzil | Türetme |
|---|---|---|---|---|
| Archer (taban) | 4 | 30 | 6.5 | — |
| + Crossbowman | 6 | 40 | 7.0 | +2, +10, +0.5 |
| + Arbalest | 8 | 55 | 7.5 | +2, +15, +0.5 |

**Avcı hattı** (taban: Atk 3, HP 30, Menzil 5 — [UnitFactory.cs:259](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L259), [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124), [UnitEntity.cs:140](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Skirmisher (taban) | 3 | 30 | — |
| + EliteSkirmisher | 4 | 40 | +1, +10 |

**Süvari hattı** (taban: Atk 8, HP 75 — [UnitFactory.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L125), [UnitEntity.cs:122](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Cavalry (taban) | 8 | 75 | — |
| + Cavalier | 10 | 95 | +2, +20 |
| + Paladin | 13 | 120 | +3, +25 |

**Deve hattı** (taban: Atk 7, HP 80 — [UnitFactory.cs:291](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L291), [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Camel (taban) | 7 | 80 | — |
| + HeavyCamel | 10 | 100 | +3, +20 |

**Keşif hattı** (taban: Atk 0, HP 40 — [UnitFactory.cs:184](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L184), [UnitEntity.cs:133](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Scout (taban) | 0 | 40 | savaşçı değil |
| + LightCavalry | 5 | 55 | +5, +15 |
| + Hussar | 7 | 70 | +2, +15 |

**Atlı okçu hattı** (taban: Atk 5, HP 50 — [UnitFactory.cs:383](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L383), [UnitEntity.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| CavalryArcher (taban) | 5 | 50 | — |
| + HeavyCavalryArcher | 7 | 70 | +2, +20 |

**Gemi hattı** (taban: Atk 8, HP 120 — [UnitFactory.cs:617](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L617), [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Galley (taban) | 8 | 120 | — |
| + WarGalley | 10 | 140 | +2, +20 |
| + Galleon | 12 | 170 | +2, +30 |

**Kartal hattı** (Aztek, taban: Atk 7, HP 55 — [UnitFactory.cs:495](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L495), [UnitEntity.cs:129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129)):

| Kademe | Atk | HP | Türetme |
|---|---|---|---|
| Eagle (taban) | 7 | 55 | — |
| + EliteEagle | 10 | 75 | +3, +20 |

> Not: Süvari ek olarak ilk vuruşta `ChargeReady` 2.5× hasar alır
> ([UnitEntity.cs:45](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L45)); bu yükseltmeden
> bağımsızdır. **Tier-terfi hattı tanımlı OLMAYAN** birimler: Trebuchet, Medic, Monk,
> Longbowman (Britons unique — tek seviye), Ram, Mangonel, FireShip, DemoShip,
> TradeCart, King ve M9 unique birimler (TeutonicKnight/WarElephant/Mangudai/Samurai).
> Bunlar blacksmith flat tech'lerinden faydalanabilir ama ayrı bir terfi kademeleri yoktur.

---

## 4. Strateji & counter

- **Imperial atılımı pahalıdır.** Bir hattı sonuna kadar yükseltmek (örn. Militia →
  Champion: 4 tech, toplam 650 food + 410 gold) yüksek maliyetlidir; age-advance
  maliyetleri eklenince tek hatta kilitlenmek riskli olur. Bkz. [05-tech-tree.md](./05-tech-tree.md).
- **HP retroaktif, saldırı her zaman canlı.** Tek bir Imperial tech araştırmak
  sahadaki *tüm* ordunun HP'sini anında bump eder — savaş ortasında bile araştırmayı
  bitirmek bir "comeback" hamlesidir.
- **Scout hattı saf savaş kazanımıdır.** Scout taban atk 0; `LightCavalry` +5 ile
  birden savaşçıya döner, `Hussar` ile 7'ye çıkar — Camel/anti-cav karşısında zayıf
  ama okçu avında ucuz ve hızlıdır.
- **Halberdier süvari panzehiri.** Spearman hattı `Halberdier`'de 9 atk + Camel sınıfı
  bonusuyla Paladin yumruğunu eritir. Süvari counter matrisi için bkz. [07-combat-counters.md](./07-combat-counters.md).
- **Arbalest menzili 7.5'e çıkar** (Fletching/Bracer eklenirse 8.5); bu okçuların
  piyadeyi kite etmesini kolaylaştırır ama Britons'un `archerRangeBonus`'u bunun da
  üstüne biner. Bkz. [06-civilizations.md](./06-civilizations.md).
- **Önkoşul kilidi.** Piyade hattında `Champion` için sırasıyla `ManAtArms` →
  `Longswordsman` → `TwoHandedSwordsman` zinciri zorunludur; tier atlanamaz
  ([TechDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L69)).

---

## 5. Çapraz bağlantılar

- [02-units.md](./02-units.md) — taban birim statları (HP/atk/menzil), `UnitType` listesi.
- [05-tech-tree.md](./05-tech-tree.md) — tüm `TechType`'ların ağaç görünümü, çağ kapıları.
- [07-combat-counters.md](./07-combat-counters.md) — DamageType / armor / counter çarpanları.
- [06-civilizations.md](./06-civilizations.md) — civ bonusları + civ-gated tech'ler (BeardedAxe, Chivalry, EliteEagle).
- [01-game-flow-ages.md](./01-game-flow-ages.md) — çağ ilerlemesi (Feudal/Castle/Imperial gate'leri).
- [04-buildings.md](./04-buildings.md) — Barracks / ArcheryRange / Stable / Dock üretim binaları.

---

## 6. Kod referansları (file:line, türetme)

| Konu | Dosya:satır | Açıklama |
|---|---|---|
| `TechType` enum (tier listesi) | [GameTypes.cs:83](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L83) | ManAtArms…EliteEagle tier terfileri |
| Tier maliyet/çağ/önkoşul tablosu | [TechDefs.cs:65](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L65) | `TechDef` kayıtları |
| Önkoşul + civ filtresi | [TechDefs.cs:158](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L158) | `ForBuilding` çağ + `requires` + civ kontrolü |
| Saldırı bonusu birikimi | [TechState.cs:37](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L37) | tüm hat *LineAtk getter'ları |
| `AttackBonus(UnitType)` switch | [TechState.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L60) | flat + hat bonusu toplamı |
| `RangeBonus` (archer sınıfı) | [TechState.cs:77](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L77) | Bracer + Fletching/Crossbow/Arbalest |
| `HpBonus(UnitType)` switch | [TechState.cs:91](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L91) | tier + flat HP birikimi |
| `MeleeArmorBonus` / `PierceArmorBonus` | [TechState.cs:156](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L156) | tier başına +1 melee/pierce armor |
| Canlı saldırı okuma | [UnitEntity.cs:152](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L152) | base + TeamTech.AttackBonus |

**Türetme örneği (Champion efektif HP):** taban 40 ([UnitFactory.cs:73](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L73))
+ ManAtArms 10 + Longswordsman 15 + TwoHandedSwordsman 15 + Champion 20
([TechState.cs:94](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L94)) = **100 HP** (ScaleMail/civ hariç).

---

## 7. AoE2 farkı (referans köprü)

Tam AoE2 birim zincirleri ve baz statlar için: [../reference/02-units-upgrade-chains.md](../reference/02-units-upgrade-chains.md).

Öne çıkan farklar:

- **Birim dönüşümü yok.** AoE2'de upgrade birimi yeni bir birime *çevirir* (Militia →
  Man-at-Arms ayrı bir entity). AoA'da birim hep aynı `UnitType` kalır; yalnızca
  takım çapında stat artar. Bu yüzden AoA'da "kısmi yükseltilmiş" karma ordu olmaz —
  bir hat yükseltilince o hattın tamamı güçlenir.
- **Hatların çoğu artık mevcut.** Eski sürümde eksik olan Spearman→Pikeman→Halberdier,
  Skirmisher→Elite, Scout→Light Cavalry→Hussar, Camel→Heavy Camel, Cavalry Archer→Heavy,
  Galley→War Galley→Galleon ve Aztek Eagle→Elite Eagle zincirleri **kodda tanımlıdır**.
  Hâlâ eksik olanlar: siege (Ram/Mangonel/Scorpion/Onager) yükseltmeleri, Fire/Demo
  Ship tier'leri, Longbowman için Elite kademesi.
- **Stat ölçeği farklı.** AoA Paladin'i 120 efektif HP (kod), AoE2 Paladin'i 160 HP.
  AoA tüm değerleri kendi dengesiyle daha düşük tutar; doğrudan AoE2 sayısı taşınmamıştır.
- **Çağ eşleşmesi yakın ama tam değil.** AoA'da TwoHandedSwordsman ile Champion ayrı
  iki Imperial kademesidir (AoE2 ile aynı), ancak değerler dengeye göre yeniden ölçeklenmiştir.
- **Önkoşul modeli aynı.** Hem AoE2 hem AoA bir üst tier için bir önceki tier'i
  zorunlu kılar (AoA: [TechDefs.cs:67](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L67) `requires`).

---

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| `SIUP` | yeni sınıf | Siege yükseltme zincirleri (Ram/Mangonel→Onager/Scorpion); Trebuchet dışında siege tier yok | [ref §Kuşatma](../reference/02-units-upgrade-chains.md) | L |
| `FSUP` | yeni hat | Fire Ship / Demo Ship tier'leri (Galley hattı tamam, diğer gemiler tek seviye) | [ref §Deniz Birimleri](../reference/02-units-upgrade-chains.md) | M |
| `LBUP` | tier hattı | Longbowman için Elite Longbowman yükseltmesi (unique unit, tek seviye) | [ref §AoA Karşılaştırması](../reference/02-units-upgrade-chains.md) | S |
| `UQUP` | tier hattı | M9 unique birimler (TeutonicKnight/WarElephant/Mangudai/Samurai) için Elite kademeleri yok | [ref §Unique Units](../reference/02-units-upgrade-chains.md) | M |
| `RENM` | UI/feedback | Yükseltme sonrası birim *adının* değişmesi (Militia → "Şampiyon" gösterimi); kodda display rename yok | [ref §Militia Hattı](../reference/02-units-upgrade-chains.md) | S |
</content>
</invoke>
