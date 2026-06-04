# Age of Arena — AoE2-Parite SP Yol Haritası (PARITY-PLAN)

> Bu doküman 85 backlog kalemini bağımlılık-sıralı milestone'lara dönüştürür.
> Hedef: tek-oyuncu (SP) AoE2-parite. Tüm geliştirme **Unity (C#)** tarafında.
> Goal modu için makine-okunur "Definition of Done" en sonda.

---

## 0. Özet

| Metrik | Değer |
|---|---|
| Toplam madde | **85** |
| Milestone sayısı | **14** (M1–M14) |
| Tahmini toplam oturum | **~118 oturum** |
| Efor dağılımı | S: 28 · M: 39 · L: 18 |
| Döngü (cycle) | **Yok** — graf DAG, topolojik sıralama mümkün |
| Birleşen kök (VTAT≡VETATK) | 1 çift (aynı kök neden, tek fix) |

### Kritik Yol (en uzun bağımlılık zinciri)

En uzun zincir **5 düğüm** (medeniyet ekseni):

```
CIVM → CIVD → CIVC → CIVX        (4 düğüm)
CIVS → CIVT → CIVU               (3 düğüm)
ARMC → BNUS                      (2 düğüm)
SWRK → {SPLASH, RAMS}            (2 düğüm)
AREA → (SPLASH bağımsız tüketir) (2 düğüm)
```

**En uzun tekil zincir:** `CIVM → CIVD → CIVC → CIVX` = **4 derinlik**.
Toplam efor olarak en ağır zincir: `CIVS(M) → CIVT(L) → CIVU(L)` = ~3 L-bloku, civ ekseni kritik yoldur.

### Bağımlılık analizi notları

1. **Döngü yok.** Tüm `deps` ilişkileri ileri yönlü; topolojik sıralama tek seferde çözüldü.
2. **Yumuşak bağımlılıklar** (sketch içinde "ön koşul" denip `deps` alanında olmayanlar):
   - `MFAITH` ↔ `RELC`/`MONK` (kapsam örtüşmesi; deps boş ama çift-iş riski).
   - `CONV` → `MONK` (deps tanımlı). `CONV` ayrıca `MFAITH` faith-modeliyle örtüşür.
   - `TRIB` → `MKTT` (deps tanımlı; MKTT yoksa vergi sabit %30, kabul edilebilir).
3. **Eş-kök çift:** `VTAT` ve `VETATK` aynı dosya/satır (UnitEntity AddKill). `VETATK` deps=`[VTAT]`. Tek fix ikisini kapatır; `VETATK` yalnız denge-doğrulamadır.
4. **SPLASH/AREA tekilleştirme:** `AREA` = Projectile altyapısı, `SPLASH` = onu tüketen Mangonel birimi (`deps=[SWRK]`). Aynı oturumda yapılırsa tek değişiklik kümesi; ayrı PR'da AREA önce gelir.
5. **Eksik dep yok** — tüm referans verilen ID'ler backlog'da mevcut.

### Topolojik katmanlar (Kahn — in-degree 0'dan başlayarak)

| Katman | Maddeler (deps'i çözülmüş olanlar) |
|---|---|
| L0 (deps yok) | SKIR, SPEAR, SCT2, CAVAR, CAMEL, EAGLE, NAVX, THSW, MFAITH, AREA, RAMS*, VTAT, MINR, ARMC, CSTL, BPOP, OUTP, TWUP, WLUP, FARM, RELC, FISH, SWRK, BSMT, ECON, GRATE, CAVT, CARA, UNIV, MONK, STONE, CIVB, CIVF, CIVV, CIVS, CIVM, GMODE-ENUM, VHOLD, VTIME, VDIPL, AIWN, AIDF, AIRD, AISC, AGEB, AGFX*, DARK, ARES, STIC, HKEY, HPWB, CMDP, MPNG, SUBT, FOWD, RETR |
| L1 | SKI(SKIR), SPN2(SPEAR), CAVA(CAVAR), CAML(CAMEL), VETATK(VTAT), SPLASH(SWRK), RAMS(SWRK), BFUR(BSMT), ARRM(BSMT), RPCT(ECON), MKTT(CARA), BNUS(ARMC), CONV(MONK), CIVD(CIVM), CIVT(CIVS), VDEATH(GMODE-ENUM), VNOMAD(GMODE-ENUM), VREGI(GMODE-ENUM), DIPL(VDIPL), AIDP(VDIPL), AICH(AIDF), MMTR(FOWD), AGFX(SUBT), SAVF(ARES), STRT(ARES), ARMR(RETR), TRIB(MKTT) |
| L2 | BMBT(TWUP), CIVC(CIVD), CIVU(CIVT), CIVX(CIVC) |

> *RAMS/SPLASH L0'da görünse de SWRK'ye bağlı → gerçek katman L1. AGFX SUBT'a bağlı → L1.

---

## Milestone Genel Bakış

| MS | Tema | Madde sayısı | Efor | ~Oturum |
|---|---|---|---|---|
| M1 | Civ bug düzeltmeleri + retroaktif altyapı (gerçek bug'lar) | 6 | 5S+1M | 6 |
| M2 | Counter birimleri: Skirmisher + Camel + Pikeman hattı | 6 | 6M | 9 |
| M3 | Siege Workshop + kuşatma (Ram/Mangonel/splash) | 5 | 1L+4M | 9 |
| M4 | Mobil & deniz birimleri (Scout/CavArcher/Naval/Eagle/Monk) | 7 | 4M+3L | 13 |
| M5 | Savunma binaları & kule zinciri | 7 | 5M+2S | 9 |
| M6 | Blacksmith + ekonomi tech ağacı | 9 | 1L+8M | 16 |
| M7 | Combat model derinleştirme (armor class + bonus damage + monk) | 5 | 3L+1M+1S | 10 |
| M8 | Pazar/ticaret/haraç + stone ekonomisi | 4 | 3M+1S | 7 |
| M9 | Medeniyet kimliği & seçim & genişletme | 9 | 4S+3M+... | 14 |
| M10 | Oyun modları altyapısı (GameMode/Deathmatch/Regicide/Nomad) | 5 | 1S+1M+3L | 11 |
| M11 | Zafer & diplomasi & AI farkındalığı | 6 | 1S+5M | 9 |
| M12 | AI derinleştirme (difficulty/handicap/strateji) | 4 | 1S+3M+... | 9 |
| M13 | Çağ akışı & UI/QoL cila | 11 | çoğu S/M | 14 |
| M14 | Fog of War + Save + minimap + dünya HP barı (ağır cila) | 6 | 3M+2L+... | 11 |

---

## M1 — Civ Bug Düzeltmeleri + Retroaktif Altyapı

**Tema:** Düşük-bağımlılık, yüksek-değer, gerçek bug'lar. Inert (ölü) civ bonuslarını canlandır + veterancy attack bug'ı + retroaktif HP terfisi. İlk teslim, hızlı kazanım.

| ID | Başlık | Efor |
|---|---|---|
| VTAT | Veterancy +%10 attack uygulanmıyor — bug | S |
| VETATK | Veteranlık attack bonusu (VTAT ile aynı kök; balance) | S |
| CIVB | Byzantines buildingHpMult & healRateMult tüket (inert) | S |
| CIVF | Franks farmDecayMult uygula (inert) | S |
| CIVV | Suvari HP/hız civ bonusunu live hesapla | S |
| RETR | Araştırılan HP terfisi canlı birimlere geriye dönük | M |

**Birleşik kabul kriterleri:**
- VTAT/VETATK: Veteran birim DamagePopup'ı recruit'tan ~%10 yüksek; yorum-kod tutarlı; tek fix iki maddeyi kapatır.
- CIVB: Byzantines heal hızı 1.5×, bina maxHp 1.1× (None'a göre); grep `healRateMult`/`buildingHpMult` eşleşir.
- CIVF: Franks atıl farm yarı hızda decay; None regresyonsuz.
- CIVV: Cavalry hp/speed computed/tick-güncel; Start()'ta tek-seferlik çarpan kalmaz; çift-çarpma yok.
- RETR: Tamamlanan tech HpBonus delta'sı canlı birimlere uygulanır; Start + retroaktif çift-sayım yok.
- Tümü: Unity Roslyn 0 error / 0 warning.

**Tahmini oturum:** 6

---

## M2 — Counter Birimleri: Skirmisher + Camel + Pikeman Hattı

**Tema:** Temel AoE2 counter üçgenini tamamla (anti-archer / anti-cavalry). Bağımsız teslim edilebilir birim grubu.

| ID | Başlık | Efor |
|---|---|---|
| SKIR | Skirmisher birimi (anti-archer) | M |
| SKI | Skirmisher tier: Elite Skirmisher tech | M |
| SPEAR | Spearman hat kimliği: anti-cavalry doğrulama + bonus kanalı | S |
| SPN2 | Spearman tier: Pikeman (Castle) + Halberdier (Imperial) | M |
| CAMEL | Camel Rider birimi (anti-cavalry uzmanı) | M |
| CAML | Camel tier: Heavy Camel Rider tech | M |

**İç bağımlılık sırası:** `SKIR → SKI`, `SPEAR → SPN2`, `CAMEL → CAML`.

**Birleşik kabul kriterleri:**
- SKIR: GameTypes enum + UnitEntity switch (DamageKind=Pierce, IsRanged); CombatSystem anti-archer çarpanı Archer/Longbowman'e uygulanır; Barracks'tan eğitilir (Feudal).
- SKI: EliteSkirmisher TechDefs (ArcheryRange/Imperial); TechState Skirmisher HP/atk retroaktif artar.
- SPEAR: TechState AttackBonus/HpBonus switch'inde Spearman dalı açılır; mevcut 3× anti-cavalry korunur.
- SPN2: Pikeman(Castle)+Halberdier(Imperial, requires Pikeman); zincir filtrelenir; retroaktif HP artışı.
- CAMEL: Camel UnitType; anti-cavalry çarpanı (Spearman'dan düşük, örn 2×); Stable/Castle.
- CAML: HeavyCamel tech (Stable/Imperial); Camel HP/atk retroaktif artar.
- Tümü: Stable/Barracks menüsünde doğru çağda görünür; Unity 0 error/0 warning.

**Tahmini oturum:** 9

---

## M3 — Siege Workshop + Kuşatma (Ram / Mangonel / Splash)

**Tema:** Kuşatma ekosistemi. Önce üretim binası (SWRK), sonra splash altyapısı (AREA), sonra birimler. Kuşatma counter'ı (MINR) burada.

| ID | Başlık | Efor |
|---|---|---|
| SWRK | Siege Workshop üretim binası (Ram/Mangonel/Scorpion) | L |
| AREA | Projectile alan-hasarı altyapısı | M |
| SPLASH | Mangonel/Onager alan hasarı (AoE splash) | L |
| RAMS | Battering Ram — pierce-immune anti-bina | M |
| MINR | Kuşatma minimum menzili (yakın infantry counter) | S |

**İç bağımlılık sırası:** `SWRK → {SPLASH, RAMS}`, `AREA → SPLASH` (AREA önce). MINR bağımsız.

**Birleşik kabul kriterleri:**
- SWRK: BuildingType.SiegeWorkshop (Castle); SiegeWorkshopTrainables + GetTrainables switch; en az 1 kuşatma birimi üretilir.
- AREA: Projectile.Spawn'a splashRadius (varsayılan 0) eklenir; 0 iken davranış değişmez; >0 iken radius-içi düşmana çoklu TakeDamage, friendly skip.
- SPLASH: Mangonel (DamageKind=Siege, IsRanged); kümelenmiş 3+ düşmana çoklu DamagePopup.
- RAMS: Ram pierceArmor≥100 (pierce-immune, Archer hasarı min-1); AntiStructureMultiplier≥3; SWRK'den üretilir.
- MINR: MinAttackRange property; FlatDist < MinAttackRange iken ranged ateş etmez; yakın Militia Treb'i döver.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 9

---

## M4 — Mobil & Deniz Birimleri (Scout/CavArcher/Naval/Eagle/Monk)

**Tema:** Birim çeşitliliğini tamamla. Çoğu L0; civ-bağımlı Eagle ve riskli naval/relic burada.

| ID | Başlık | Efor |
|---|---|---|
| SCT2 | Scout tier: Light Cavalry + Hussar | M |
| CAVAR | Cavalry Archer birimi (Castle) | M |
| CAVA | Cavalry Archer tier: Heavy Cavalry Archer | M |
| THSW | Two-Handed Swordsman ara tier'i | S |
| EAGLE | Eagle Warrior hattı + Elite tier | L |
| NAVX | Naval çeşitlilik: Galley tier + Fire/Demo Ship | L |
| MFAITH | Monk faith + relic taşıma; değişken dönüştürme süresi | M |

**İç bağımlılık sırası:** `CAVAR → CAVA`. SCT2/THSW/EAGLE/NAVX/MFAITH bağımsız.
**Yumuşak uyarı:** MFAITH ↔ RELC (M5) kapsam örtüşmesi; relic-taşıma sınırını net çiz, çift-iş yapma.

**Birleşik kabul kriterleri:**
- SCT2: LightCavalry(Stable/Castle)+Hussar(Stable/Imperial); Scout HP retroaktif; saldırı tasarım kararı dokümante.
- CAVAR: DamageKind=Pierce, IsRanged, yüksek hız; Stable/Castle; hareket+atış (veya dur-at) çalışır.
- CAVA: HeavyCavalryArcher tech (Imperial); retroaktif HP artışı.
- THSW: Longsword→2H→Champion zinciri; Champion.requires=TwoHandedSwordsman; Militia HP retroaktif.
- EAGLE: Eagle UnitType + Elite tier; civ-koşullu trainable (IsBritons deseni); başka civ'de görünmez.
- NAVX: WarGalley+Galleon tier (retroaktif) + FireShip/DemoShip Dock'tan; navalAgentTypeId paylaşımı.
- MFAITH: faith alanı + dönüşüm sonrası regen; ConvertTime faith'e bağlı; Monk relic alıp Monastery'ye deposit → gold.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 13

---

## M5 — Savunma Binaları & Kule Zinciri

**Tema:** Savunma derinliği. Kule yükseltme zinciri (TWUP→BMBT), duvar/farm/outpost.

| ID | Başlık | Efor |
|---|---|---|
| CSTL | Castle'a Castle Age çağ kilidi (minAge) | S |
| BPOP | Nüfus tavanı toplama doğrula + genelleştir | S |
| OUTP | Outpost binası (ateş etmeyen görüş kulesi) | S |
| TWUP | Watch Tower → Guard Tower → Keep zinciri | M |
| BMBT | Bombard Tower (Imperial) | M |
| WLUP | Wall katman yükseltme (Palisade/Stone/Fortified) | M |
| RELC | Monastery relic tutma + relic-gold geliri | L |
| FARM | Farm kapasite tech zinciri | M |
| FISH | Dock deniz ekonomisi: balıkçı + Fish Trap | L |

**İç bağımlılık sırası:** `TWUP → BMBT`. Diğerleri bağımsız.
**Yumuşak uyarı:** RELC ↔ MFAITH (M4) — taşıma/deposit mekaniğini tek yerde tut.

**Birleşik kabul kriterleri:**
- CSTL: Castle def'inde minAge:Age.Castle; Feudal'da kilitli, Castle'da aktif.
- BPOP: RecomputePop TC5+House5+Castle10 doğru toplar, 200'e clamp; pop==cap iken üretim reddedilir.
- OUTP: Outpost attackRange==0 (ateş etmez); wood+stone maliyet; BuildingFactory.Make dalı.
- TWUP: GuardTower(Castle)+Keep(Imperial) tech; WatchTower atk/hp/range tech bonusu; BuildingCombatSystem teamTech okur.
- BMBT: BombardTower (Imperial); attackDamageType=Siege alanı; Watch Tower'ın ≥4× hasarı.
- WLUP: BuildingEntity Wall/Gate teamTech.BuildingMeleeArmor+PierceArmor uygular; Masonry/Fortified canlanır; Stone-tier HP bonusu.
- RELC: carriedByMonk/heldInMonastery durumu; Monk pickup→deposit→gold/s; MatchSystem relic-zafer kırılmaz.
- FARM: HorseCollar(Feudal)+HeavyPlow(Castle); FarmCapacityBonus; reseed/yeni farm >300 food.
- FISH: FishingShip + su Fish node/Fish Trap; gatherer akışı → Dock deposit; food artar.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 9

---

## M6 — Blacksmith + Ekonomi Tech Ağacı

**Tema:** Tech ağacının ekonomi/zırh/saldırı omurgası. BSMT armor altyapısı diğerlerini besler.

| ID | Başlık | Efor |
|---|---|---|
| BSMT | Blacksmith saldırı & piyade zırh 3 kademe | M |
| BFUR | Suvari saldırı & zırh (Barding) | M |
| ARRM | Okçu zırh hattı (Archer Armor) + Bracer | M |
| ECON | Ekonomi tech zinciri (Loom/Mill/LumberCamp/MiningCamp) | L |
| GRATE | Kaynak-türü-bazlı taban hasat hızı | M |
| RPCT | Toplama bileşik modeli (taşıma + hız) | M |
| CAVT | Stable: Husbandry + Light Cav/Hussar yan hattı | M |
| CARA | Caravan hız tech'i + Trade Cog | M |
| UNIV | University tech'leri (Ballistics/Chemistry/Architecture) + kule | L |

**İç bağımlılık sırası:** `BSMT → {BFUR, ARRM}`, `ECON → RPCT`. GRATE/CAVT/CARA/UNIV bağımsız.

**Birleşik kabul kriterleri:**
- BSMT: IronCasting/BlastFurnace/ChainMail/PlateMail; requires zincirleri; TechState.ArmorBonus(UnitType,DamageType) + UnitEntity.TakeDamage live okur; +3 atk doğrulanır.
- BFUR: ScaleBarding/ChainBarding/PlateBarding; IronCasting/BlastFurnace cavalry'ye bağlanır; çift-sayım yok.
- ARRM: Padded/Leather/Ring Archer Armor + Bracer (+1 atk, +0.5 range); pierce armor artar.
- ECON: Loom/HorseCollar/HeavyPlow/CropRotation/BowSaw/GoldMining/StoneMining; GatherMult kind-bazlı; deposit artar.
- GRATE: GatherRateFor(kind); Wood<Gold/Stone<Food; villager hasat hızı kind'e göre farklı.
- RPCT: CarryCapacityMult/CarryBonus; Wheelbarrow taşıma+hız; deposit frekansı ölçülür.
- CAVT: Husbandry MoveSpeedMult(Cavalry)=1.1 live; NavMeshAgent.speed artar.
- CARA: Caravan trade kazanç/hız çarpanı; Trade Cog tanımı.
- UNIV: Ballistics/Chemistry/Architecture/GuardTower/Keep; Architecture bina maxHp↑; Chemistry +1 atk.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 16

---

## M7 — Combat Model Derinleştirme (Armor Class + Bonus Damage + Monk)

**Tema:** En riskli combat refaktoru. AoE2 additive-bonus + armor-class modeline geçiş. Yüksek regresyon riski; her counter tek tek doğrulanır.

| ID | Başlık | Efor |
|---|---|---|
| ARMC | Genişletilmiş armor class'ları | L |
| BNUS | Toplamalı bonus-damage modeli | L |
| MONK | Monastery/Monk tech'leri | L |
| CONV | Olasılıksal Monk conversion + Redemption/Theocracy | L |
| ARMR | Tier-terfilerde zırh artışı | M |

**İç bağımlılık sırası:** `ARMC → BNUS`, `MONK → CONV`. ARMR(deps=RETR, M1'de) bağımsız burada.

**Birleşik kabul kriterleri:**
- ARMC: ArmorClass enum/flag; UnitFactory her birime atar; IDamageable/UnitEntity sorgusu; mevcut melee/pierce regresyonsuz.
- BNUS: dmg = base+bonus, sonra max(1,amount-armor); tüm counter Play testleri geçer (Spearman/Treb korunur); multiplier'lar deprecate/taşınmış.
- MONK: Sanctity/BlockPrinting/Redemption (Monastery); HpBonus(Monk)+15; ConvertRange BlockPrinting ile artar.
- CONV: ≥2 monk tech; StepConvert olasılıksal/değişken süre; Has(Theocracy) tüketilir.
- ARMR: MeleeArmorBonus/PierceArmorBonus(UnitType); etkili armor=base+bonus; ScaleMail sonrası az hasar; çift-sayım yok.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 10

---

## M8 — Pazar / Ticaret / Haraç + Stone Ekonomisi

**Tema:** Ekonomik etkileşim katmanı. MKTT → TRIB zinciri; stone ekonomik döngüye girer.

| ID | Başlık | Efor |
|---|---|---|
| MKTT | Market tech'leri (Coinage/Banking/Caravan/Guilds) | M |
| TRIB | Takımlar arası kaynak haracı (tribute) + vergi | M |
| STONE | Stone başlangıç & ekonomik döngü dengesi | S |

**İç bağımlılık sırası:** `CARA(M6) → MKTT → TRIB`. STONE bağımsız.
**Not:** MKTT, M6'daki CARA'ya bağlı — M8 M6'dan sonra gelmeli.
**Uyarı:** STONE, CLAUDE.md'deki "stone=0 (oyuncu kararı)" kuralını iptal eder; kullanıcı onayı gerekebilir.

**Birleşik kabul kriterleri:**
- MKTT: Coinage/Banking/Caravan/Guilds; Market TechDefs satırları; Guilds sonrası spread daralır.
- TRIB: Tribute(from,to,kind,amount); Coinage yoksa %30 vergi; her iki teamRes doğru güncellenir.
- STONE: stone başlangıç >0 (örn 200); ≥1 bina stone kullanır; yetersizken inşa engellenir.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 7

---

## M9 — Medeniyet Kimliği & Seçim & Genişletme (Kritik Yol)

**Tema:** Civ ekseni — projenin en uzun bağımlılık zinciri. Seçim ekranı, team bonus, unique tech/unit, ek civ'ler.

| ID | Başlık | Efor |
|---|---|---|
| CIVS | Oyuncu medeniyet seçim ekranı | M |
| CIVM | Takım bonusu (team bonus) kavramı | M |
| CIVD | Civ kimliklerini genişlet (ek bonus alanları) | M |
| CIVC | Medeniyet sayısını 5 → daha fazla | L |
| CIVX | Civ ID çakışma önleme / isim senkron | S |
| CIVT | Unique tech sistemi (civ-özel) | L |
| CIVU | Civ başına unique birim | L |

**İç bağımlılık sırası (kritik yol):**
```
CIVM → CIVD → CIVC → CIVX
CIVS → CIVT → CIVU
```
İki paralel zincir. CIVM/CIVS L0 kökleri.

**Birleşik kabul kriterleri:**
- CIVS: Player civ kullanıcı seçimi; AI rastgele; seçim UI 5+None; bonus etkin.
- CIVM: TeamBonus tablosu/alanları; GameManager TeamSharedBonus API; ≥1 team-bonus tüketilir.
- CIVD: ≥2 yeni CivBonus alanı 5 civ için doldurulur; her alan ≥1 sistemde okunur; sayılar referanstan.
- CIVC: Civilization enum ≥10 total; her yeni civ tam doldurulmuş satır; rastgele atama yeni civ seçer.
- CIVX: 5 civ display string wiki ile birebir; tek isim/ID kaynağı netleşir.
- CIVT: Civ-özel TechType; ResearchSystem civ+çağ gating; ≥2 civ için Castle+Imperial unique tech.
- CIVU: ≥4 unique birim UnitType; tüm switch'lerde tanımlı; civ-koşullu trainable; isimler referansa uyar.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 14

---

## M10 — Oyun Modları Altyapısı

**Tema:** GameMode enum kökünden 4 mod. GMODE-ENUM tüm modların ön koşulu.

| ID | Başlık | Efor |
|---|---|---|
| GMODE-ENUM | GameMode enum + GameManager.gameMode alanı | S |
| VHOLD | Wonder/Relic tutma süresi ayarlanabilir | S |
| VTIME | Time Limit / Score zaferi | M |
| VDEATH | Deathmatch modu — yüksek başlangıç kaynağı | M |
| VREGI | Regicide modu — King birimi + eleme | L |
| VNOMAD | Nomad modu — TC'siz dağılık başlangıç | L |

**İç bağımlılık sırası:** `GMODE-ENUM → {VDEATH, VREGI, VNOMAD}`. VHOLD/VTIME bağımsız.

**Birleşik kabul kriterleri:**
- GMODE-ENUM: enum GameMode 4 değer; GameManager.gameMode alanı; davranış değişmez.
- VHOLD: WonderHoldTime/RelicHoldTime const değil set edilebilir; WorldRoot atar; varsayılan 60sn.
- VTIME: matchTimeLimit + sayaç; süre dolunca 4 takım Score() max; limit 0 regresyonsuz.
- VDEATH: Deathmatch'te 4 takım yüksek kaynak (20000F vb.); RandomMap regresyonsuz.
- VREGI: King UnitType; her takıma 1 King; King ölünce eleme; Wonder/Relic countdown atlanır.
- VNOMAD: BuildBase çağrılmaz, Villager spawn; ilk-TC-yok grace; AI TC inşa eder.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 11

---

## M11 — Zafer & Diplomasi & AI Farkındalığı

**Tema:** Diplomasi matrisi (VDIPL kökü) ve çok-yollu zafer farkındalığı.

| ID | Başlık | Efor |
|---|---|---|
| VDIPL | Diplomasi durumları (Allied/Neutral/Enemy) | L |
| DIPL | Diplomasi / ittifak UI paneli | M |
| AIDP | AI diplomasi davranışı — müttefike saldırmaz + tribute | M |
| AIWN | AI çok-yollu zafer farkındalığı (Wonder/Relic/Score) | M |

**İç bağımlılık sırası:** `VDIPL → {DIPL, AIDP}`. AIWN bağımsız.
**Not:** L sayımı VDIPL tek; M11'de 1L+3M = ~9 oturum.

**Birleşik kabul kriterleri:**
- VDIPL: DiplomacyState 4×4 matrisi; MatchSystem zaferi matris okur (hardcoded kalkar); CombatSystem Allied/Neutral'a saldırmaz; default tüm-düşman regresyonsuz.
- DIPL: HUD diplomasi paneli; tıklayınca matris güncellenir; text.font=null.
- AIDP: EnemyAI Allied'ı hedeflemez; opsiyonel tribute; default-düşman regresyonsuz.
- AIWN: MatchSystem countdown public read; EnemyAI Wonder'a countdown'da öncelik; AI ordusu Wonder'a yönelir.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 9

---

## M12 — AI Derinleştirme

**Tema:** Difficulty genişletme + handicap + strateji katmanı + rounding bug.

| ID | Başlık | Efor |
|---|---|---|
| AIRD | RoundToInt round-half-to-even tutarsızlığı (bug) | S |
| AIDF | Difficulty 4 → 6 seviye | M |
| AICH | Difficulty handicap modeli — eko/üretim ölçekleme | M |
| AISC | AI strateji/script katmanı — personality genişletme | L |

**İç bağımlılık sırası:** `AIDF → AICH`. AIRD/AISC bağımsız.

**Birleşik kabul kriterleri:**
- AIRD: Kod ile doküman tutarlı (FloorToInt(x+0.5f) veya wiki 6'ya güncel); diğer türevler doğru.
- AIDF: Difficulty 6 değer; ApplyDifficulty 6 case kapsar; HUD 6 seviye döner; monoton.
- AICH: Eko/üretim hızında difficulty çarpanı; bedava kaynak YOK (yalnız hız); Easy az / Insane çok birim.
- AISC: Genişletilmiş AIProfile; birim karışım profilden; mevcut 3 personality regresyonsuz.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 9

---

## M13 — Çağ Akışı & UI/QoL Cila

**Tema:** Çağ önkoşulları, UI cila, hotkey, stance, ses. Çoğu bağımsız S/M.

> 🔧 **Tamamlanan dış katkı — HUD/UI Rework** (2026-06-03, bkz. [docs/HUD-AOE2-REWORK.md](HUD-AOE2-REWORK.md)):
> AoE2-sadık dokulu üst bar + kalıcı 4-bölge alt bar + **kamera-RTT diamond minimap** (tıkla-navigasyon).
> Yeni `UiSkin.cs` (9-slice, fallback'li) + `Editor/UiSpriteImporter.cs` + `Resources/UI/*.png` (Kenney CC0).
> Unity 0 error/warning, Play doğrulandı. **Plana etkisi:**
> - **MMTR** kapsamı daraldı → minimap artık gerçek terrain RTT render ediyor; kalan iş yalnız
>   **FogOfWar overlay** (keşfedilmemiş siyah). DoD satırı buna göre güncellendi.
> - **MPNG** (ping) yeni `MinimapClick` (IPointerClick+IDrag) handler'ı üzerine eklenecek.
> - **STRT** (kurulum ekranı), **CMDP** (komut sayfalama), **DIPL** (diplomasi paneli) artık
>   `UiSkin` 9-slice skinning'i yeniden kullanmalı (tutarlı görünüm + sıfır ek asset).

| ID | Başlık | Efor |
|---|---|---|
| AGEB | Çağ atlama 2-bina önkoşulu | M |
| AGFX | Çağ atlama görsel/ses kutlama | S |
| DARK | Dark çağına özgü kısıtlama/avantaj | S |
| ARES | Restart sonrası civ/zorluk koru | S |
| STRT | Oyun başı kurulum ekranı (harita/civ/zorluk) | L |
| STIC | Attack-stance ikonları + gösterge | M |
| HKEY | Özelleştirilebilir hotkey haritası | L |
| HPWB | Dünya-uzayında birim HP barları | M |
| CMDP | Komut kartı sayfalama | M |
| MPNG | Minimap ping / işaret sistemi | S |
| SUBT | Birim-spesifik seçim/onay sesleri | M |

**İç bağımlılık sırası:** `SUBT → AGFX`, `ARES → STRT`. Diğerleri bağımsız.

**Birleşik kabul kriterleri:**
- AGEB: TechDef prereqBuildings+minPrereqCount; IsAvailable tamamlanmış bina sayar; Dark'ta Feudal butonu kilitli.
- AGFX: AudioManager.SoundId.AgeUp; OnAgeAdvanced Play(AgeUp); çağ popup'ı; yalnız team 0.
- DARK: Dark-dışı binalar minAge≥Feudal; Dark'ta yalnız temel açık.
- ARES: GameBootstrap static lastCivs/lastDifficulty; restart aynı civ/zorluk.
- STRT: SetupScreen Canvas; civ+difficulty+seed; Start→Build; restart setup'ı atlar.
- STIC: 4 stance ikonu; HUD seçili stance gösterir; Q + butonla değişir.
- HKEY: Hotkeys sınıfı (enum→KeyCode+PlayerPrefs); çağrı noktaları Hotkeys.Get; rebind kalıcı.
- HPWB: Billboard HP bar fill=hp/maxHp; hasarlı/seçili/hover'da görünür; perf düşüşü yok.
- CMDP: 15+ slot sayfalama; aktif sayfa render; seçimde page=0.
- MPNG: Modifiyer+tık ping; görsel marker + ses; sol-pan/sağ-move bozulmaz.
- SUBT: Birim-sınıfı seçim/onay sesleri; villager vs asker farklı; eksik clip null-guard.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 14

---

## M14 — Fog of War + Save + Minimap + Dünya HP (Ağır Cila)

**Tema:** En ağır cila katmanı. FOWD → MMTR zinciri; tam save (SAVF).

| ID | Başlık | Efor |
|---|---|---|
| FOWD | Fog of War varsayılan açık + dengeleme | M |
| MMTR | Minimap terrain/explored render | M |
| SAVF | Tam oyun durumu kaydı (birim/bina pozisyonları) | L |

**İç bağımlılık sırası:** `FOWD → MMTR`, `ARES(M13) → SAVF`.
**Not:** SAVF, M13'teki ARES'e bağlı — M14 M13'ten sonra.

**Birleşik kabul kriterleri:**
- FOWD: fogEnabled varsayılan true; UI toggle; harita başta karanlık, görüş açıldıkça lit; Custom/FogOfWar shader bulunur.
- MMTR: fog açıkken FogOfWarSystem dokusu minimap overlay; keşfedilmemiş siyah; fog kapalı regresyonsuz.
- SAVF: SaveData birim+bina+teamCivs serileştirir; Load arenayı temizleyip yeniden kurar; NavMesh geçerli.
- Tümü: Unity 0 error/0 warning.

**Tahmini oturum:** 11

---

## Milestone Bağımlılık Akışı (Özet Graf)

```
M1 (bug/inert/retro) ─────────────────────────┐
M2 (counter units)                              │
M3 (siege)  ◄── SWRK,AREA                        │
M4 (mobil/naval/monk)                            ├─► birbirinden büyük ölçüde bağımsız
M5 (defense/tower)                               │
M6 (blacksmith/econ tech) ──► M8 (CARA→MKTT→TRIB)│
M7 (combat model: ARMC→BNUS, MONK→CONV)          │
M9 (civ: CIVM→CIVD→CIVC→CIVX, CIVS→CIVT→CIVU)    │  ◄ KRİTİK YOL
M10 (game modes: GMODE-ENUM→...)                 │
M11 (diplomacy: VDIPL→DIPL/AIDP)                 │
M12 (AI: AIDF→AICH)                              │
M13 (UI/QoL: SUBT→AGFX, ARES→STRT) ──► M14 (ARES→SAVF, FOWD→MMTR)
```

> Önerilen yürütme sırası: M1 → M2 → M3 → M5 → M6 → M4 → M7 → M8 → M9 → M10 → M11 → M12 → M13 → M14.
> M1 her zaman önce (gerçek bug'lar + retro altyapı sonraki milestone'ları besler).
> Cross-milestone dep: MKTT⊃CARA(M6), SAVF⊃ARES(M13), M8⊃M6, M14⊃M13.

---

# Definition of Done (makine-okunur)

> Her madde için tek satır ölçülebilir kriter. Goal modu her iterasyonda yeniden ölçebilir.
> Tümü ortak: Unity Roslyn 0 error / 0 warning (Unity_GetConsoleLogs boş).

- [x] VTAT: UnitEntity.AttackDamage getter veteranRank çarpanı içerir (VeteranMult); Veteran ×1.1 / Elite ×1.2.
- [x] VETATK: VTAT fix sonrası Veteran(+%10)/Elite(+%20) attack; rank eğrisi VeteranMult ile UnitEntity'de görünür.
- [x] CIVB: `healRateMult` CombatSystem.StepHeal'de + `buildingHpMult` BuildingEntity.Start'ta tüketiliyor; Byzantines heal 1.5×, bina maxHp 1.1×.
- [x] CIVF: `farmDecayMult` ResourceNode decay'de tüketiliyor; Franks farm yarı hızda decay, None ×1.0 regresyonsuz.
- [x] CIVV: Start()'ta tek-seferlik cavalryHpMult YOK; HP RecomputeMaxHp ile baseMaxHp'den türetilir (çift-çarpma yok); speed base'den bir kez.
- [x] RETR: ResearchSystem.Apply canlı birimlerde RecomputeMaxHp çağırır (baseMaxHp+tech+vet+civ); idempotent, çift-sayım yok.
- [x] SKIR: UnitType.Skirmisher (DamageKind=Pierce, IsRanged, AntiArcherMultiplier 2×); ArcheryRange/Feudal trainable. (Runtime: dmg3/range5/Pierce/antiArcher2 doğrulandı.)
- [x] SKI: TechType.EliteSkirmisher (ArcheryRange/Imperial); TechState Skirmisher HP/atk RecomputeMaxHp ile retroaktif.
- [x] SPEAR: TechState AttackBonus/HpBonus'ta UnitType.Spearman dalı (SpearmanLineAtk); CombatSystem 3× anti-cavalry korundu.
- [x] SPN2: TechType.Pikeman (Castle) + Halberdier (Imperial, requires=Pikeman); Spearman HP retroaktif; HUD tier adı (Kargıcı/Teberli).
- [x] CAMEL: UnitType.Camel; CombatSystem anti-cavalry Camel'i kapsar (2×); Stable/Castle trainable. (Runtime: hp80/antiCav2 doğrulandı.)
- [x] CAML: TechType.HeavyCamel (Stable/Imperial); Camel HP/atk RecomputeMaxHp ile retroaktif.
- [x] SWRK: BuildingType.SiegeWorkshop (Castle, buildable); SiegeWorkshopTrainables (Ram+Mangonel) + GetTrainables + BuildingFactory mesh.
- [x] AREA: Projectile.Spawn(splashRadius=0 default); 0→tek hedef; >0→radius-içi target-takımı çoklu TakeDamage, friendly (diğer takımlar) skip.
- [x] SPLASH: UnitType.Mangonel (DamageKind=Siege, SplashRadius 1.8); Projectile alan hasarı + çoklu DamagePopup. (Runtime: splash1.8 doğrulandı.)
- [x] RAMS: UnitFactory.Ram pierceArmor=180 (Archer→min-1 doğrulandı: 30 Pierce→delta 1); AntiStructure 5×; SiegeWorkshop'tan üretilir.
- [x] MINR: UnitEntity.MinAttackRange (Trebuchet 3/Mangonel 2/Galley 1.5); CombatSystem FlatDist<MinAttackRange iken ateş etmez.
- [x] SCT2: LightCavalry(Stable/Castle)+Hussar(Stable/Imperial); Scout recon→combat (tasarım kararı: taban 0dmg/0aggro, tech ile aktif). Runtime: 40→55→70 HP, dmg 5→7, aggro 8.
- [x] CAVAR: UnitType.CavalryArcher (Pierce, IsRanged, hız 5.2); Stable/Castle. Runtime: hp50/dmg5/range4 doğrulandı.
- [x] CAVA: TechType.HeavyCavalryArcher (Stable/Imperial); CavalryArcher HP/atk RecomputeMaxHp ile retroaktif.
- [x] THSW: TechType.TwoHandedSwordsman (Imperial, req Longswordsman); Champion.requires=TwoHandedSwordsman; Militia HP retroaktif; HUD tier adı.
- [x] EAGLE: (M9b'de açıldı) UnitType.Eagle + UnitFactory.Eagle() (hp55/hız4.5); Aztecs Barracks-koşullu trainable (BarracksTrainablesAztec); TechType.EliteEagle (Aztec-gated, +20hp/+3atk retroaktif). Runtime: Aztek Barracks Eagle içerir, Frank içermez; EliteEagle yalnız Aztek menüsünde.
- [x] NAVX: WarGalley+Galleon tier (Dock Castle/Imperial, retroaktif: Galley 120→170 HP, dmg 12); UnitType.FireShip+DemoShip Dock'tan SpawnNaval ile. (Runtime doğrulandı; DemoShip splash 2.5.)
- [x] MFAITH (faith parçası): UnitEntity.faith + FaithReady; conversion full faith ister, sonra faith=0 + regen (CombatSystem.StepConvert). **Relic taşıma/deposit → RELC/M5'e** (plan uyarısı gereği sınır çizildi, çift-iş yok).
- [x] CSTL: Castle def `minAge: Age.Castle`; UnlockedAt(Castle,Feudal)=false, (Castle,Castle)=true (runtime doğrulandı).
- [x] BPOP: RecomputePop TC5+House5+Castle10 (GameManager.cs:132), Clamp(cap,0,200); TrainingQueue pop>=cap reddeder. (Mevcut+doğrulandı.)
- [x] OUTP: BuildingType.Outpost (attackRange=0, ateş etmez, 25O+5T, buildable); BuildingFactory mesh + Create.
- [x] TWUP: TechType.GuardTower+Keep (University); BuildingCombatSystem teamTech.TowerAttackBonus okur → WatchTower 7+7=14. (Runtime: bonus 7.)
- [x] BMBT: BuildingType.BombardTower (Imperial); BuildingDef.attackDamageType=Siege; dmg 30 ≥ 4× WatchTower(7). (Runtime doğrulandı.)
- [x] WLUP: BuildingEntity.TakeDamage teamTech.BuildingMelee/PierceArmor uygular (tüm binalar); Masonry→2/2 (runtime). Wall/Gate dahil.
- [x] RELC: RelicEntity.carrier + heldInMonastery + Available; RelicSystem Monk pickup→takip→Monastery deposit→ForceControl+GrantGold. Proximity ile uyumlu (relic-zafer iterasyonu değişmedi; değişiklik liste-okuma). Runtime: deposit→controllingTeam=0 doğrulandı.
- [x] FARM: TechType.HorseCollar(Mill/Feudal)+HeavyPlow(Mill/Castle); TechState.FarmCapacityBonus=150; ResourceNode reseed maxAmount+bonus → >300. (Runtime: 150.)
- [ ] FISH: (→ ERTELENDİ) Su-üstü Fish node + naval gather gerektiriyor (NAV-full su haritasına bağlı, L-effort). Naval/su ekonomisi grubuyla yapılacak: FishingShip + Fish Trap + gather→Dock akışı.
- [x] BSMT: IronCasting/BlastFurnace (MeleeAttackBonus, infantry+cavalry) + ChainMail/PlateMail (TechState.ArmorBonus infantry); UnitEntity.TakeDamage live okur. Runtime: Forging+IronCasting Militia atk=+3, infantry armor 3/3 doğrulandı.
- [x] BFUR: ScaleBarding/ChainBarding/PlateBarding (ArmorBonus cavalry); Cavalry attack MeleeAttackBonus üzerinden IronCasting/BlastFurnace alır (tek kaynak → çift-sayım yok). Runtime: Cavalry atk=+5, barding melee=+4.
- [x] ARRM: PaddedArcherArmor/LeatherArcherArmor/RingArcherArmor (ArmorBonus archer pierce +3) + Bracer (ArcherAttackBonus +1, RangeBonus +0.5). Runtime: archer pierce armor 3, Bracer atk 1/range 0.5.
- [x] ECON: Loom/BowSaw/GoldMining/StoneMining/CropRotation (HorseCollar/HeavyPlow mevcut); GatherMult kind-bazlı. Runtime: Wood 1.2 (BowSaw +0.2), Gold 1.15 (GoldMining +0.15), Stone 1.15; Loom Villager +15hp/+1armor; Farm 225.
- [x] GRATE: GatherSystem.GatherRateFor(kind)+GatherIntervalFor(kind); Food 0.5s < Gold/Stone 0.6s < Wood 0.7s → rate Wood<Gold/Stone<Food.
- [x] RPCT: TechState.CarryCapacityMult (Wheelbarrow 1.25) + CarryBonus; GatherSystem.CarryCapacityFor tech okur; Wheelbarrow ayrıca villager MoveSpeedMult 1.1 (GatherMult'tan çıkarıldı → çift-buff yok). Runtime: 1.25/1.1.
- [x] CAVT: TechType.Husbandry; TechState.MoveSpeedMult(Cavalry)=1.1; UnitEntity.RecomputeSpeed (baseMoveSpeed) + ResearchSystem.Apply canlı birimlere uygular → NavMeshAgent.speed. Runtime: 1.1.
- [x] CARA: TechType.Caravan; TradingSystem StepCart TradeGoldMult (×1.5); Dock/Trade Cog rotası aynı StepCart mantığını paylaşır (su haritası gelince). Runtime: 1.5.
- [x] UNIV: Ballistics/Chemistry/Architecture; BuildingHpMult (Architecture 1.10, BuildingEntity.Start) + Building armor +1; Chemistry +1 missile atk (archer/galley/tower). Ballistics = homing mermi zaten %100 isabet (Projectile not). Runtime: Architecture 1.1/armor1, Chemistry archer1/tower1.
- [x] ARMC: ArmorClass flags enum (GameTypes); UnitEntity.ArmorClasses (type-driven, factory'den daha güvenli — desync olmaz) + BuildingEntity.ArmorClasses=Building; IDamageable.ArmorClasses uniform. Runtime: Cavalry/[Cavalry,Camel]/[Cavalry,Archer]/Archer/Siege doğrulandı.
- [x] BNUS: CombatSystem dmg = AttackDamage + BonusDamageVs(target), sonra TakeDamage max(1,amount-armor). Eski `*= AntiCavalry/AntiArcher/AntiStructure` kaldırıldı → UnitEntity.BonusDamageVs (additive, ArmorClass-keyed). Base-stat counter'lar birebir korundu (Spear+8/Camel+7 vs Cavalry, Skirm+3 vs Archer, Treb+70/Ram+16 vs Building). Runtime doğrulandı.
- [x] MONK: Sanctity/BlockPrinting/Redemption (Monastery, Castle); HpBonus(Monk)=Sanctity +15; StepConvert ConvertRange = 2.5 + BlockPrinting 1.5 (TechState.MonkConvertRange). Runtime: hp+15, range 4.0.
- [x] CONV: 4 monk tech (Sanctity/BlockPrinting/Redemption/Theocracy ≥2); StepConvert olasılıksal/değişken süre (convertThreshold = Random[3..7]s, Theocracy ×0.6); Has(Theocracy) tüketilir (faith yarıda kalır). Runtime: Theocracy/Redemption True.
- [x] ARMR: TechState.MeleeArmorBonus/PierceArmorBonus(UnitType) = Blacksmith armor + ScaleMail + tier-terfi zırhı (infantry/cav melee, archer pierce); UnitEntity.TakeDamage live okur. Runtime: ScaleMail 1/1, Militia tier melee 3, archer tier pierce 2.
- [x] MKTT: Coinage/Banking/Guilds (Caravan M6'da) Market TechDefs satırları; Guilds → MarketSystem spread daralır (sell 70→80, buy 130→120, spread 60→40); Banking TradeGoldMult ×1.2 (Caravan ile 1.8). Runtime doğrulandı.
- [x] TRIB: TributeSystem.Tribute(from,to,kind,amount); Coinage yoksa %30 vergi (100→alıcı 70), Coinage ile vergisiz (100→100); yetersiz kaynak reddedilir; iki teamRes doğru. Runtime doğrulandı.
- [x] STONE: ResourceManager.stone=200 başlangıç (team0+AI); BuildingDefs stone>0 (Castle 650/University 150/BombardTower 100/Outpost 5/Wonder 600); BuildingPlacement.CanAfford(...,stone) yetersizken engeller (University@200 ✓, Castle@200 ✗, Castle@700 ✓). CLAUDE.md kuralı güncellendi (kullanıcı onayı). Runtime doğrulandı.
- [x] CIVS: WorldRoot team0 = GameBootstrap.PlayerCiv, yalnız AI(1..3) random; CivSelectScreen.cs seçim UI (None + 10 oynanabilir, kendi Canvas'ı); Choose → playerCiv + canlı RecomputeMaxHp/Speed + GameBootstrap.PlayerCiv kalıcı; HUD CivilizationDefs.display gösterir. Runtime: team0=PlayerCiv, AI random, ekran sahnede, seçim→Franklar.
- [x] CIVM: struct TeamBonus + CivBonus.teamBonus; GameManager.TeamSharedBonus(teamId) API (alliances M11'de toplanır); GatherSystem food deposit'te tüketilir (Aztecs +5% team food). Runtime: 0.05/0.
- [x] CIVD: CivBonus'a archerAttackMult + unitTrainTimeMult (2 yeni alan, 11 civ için doldurulu); archerAttackMult UnitEntity.AttackDamage'de (Vikings 4.4 vs None 4.0), unitTrainTimeMult TrainingQueue.Enqueue'de (Mongols 0.9). Runtime doğrulandı.
- [x] CIVC: Civilization enum 11 total/10 oynanabilir (+Aztecs/Teutons/Persians/Vikings/Saracens); her civ Row() ile tam doldurulu; WorldRoot Random.Range(1,length) yeni civ'leri seçer. Runtime doğrulandı.
- [x] CIVX: CivilizationDefs tek kanonik ID/isim kaynağı (dosya başında belgeli; HUD/seçim oradan okur); 10 display string dolu+tekil (Franklar..Saracenler). _Not: wiki 06 (5 civ) batch tazelemede 10'a senkronlanacak._
- [x] CIVT: TechType Chivalry/BeardedAxe (Franks) + Ironclad/Crenellations (Teutons); TechDef.requiredCiv + ForBuilding civ+çağ gating; Castle'da araştırılır. Efektler: Chivalry cav +20hp, BeardedAxe militia +2atk, Ironclad siege +4 armor, Crenellations +1 tower range. Runtime: Franks→[Chivalry,BeardedAxe], Teutons→[Ironclad,Crenellations], çağ-gating + efektler doğrulandı.
- [x] CIVU: 5 yeni unique UnitType (TeutonicKnight/WarElephant/Mangudai/Samurai + Eagle); UnitEntity tüm switch'lerde tanımlı (atk/range/interval/aggro/DamageKind/IsRanged/ArmorClass/BonusDamageVs) + UnitFactory mesh + TrainingQueue spawn + CommandIconFactory/HUD ad. Castle GetTrainables civ-koşullu (CastleUniqueFor: Teutons→TK, Persians→WarEle, Mongols→Mangudai, Japanese→Samurai). Runtime doğrulandı.
- [ ] GMODE-ENUM: grep `enum GameMode` (4 değer); GameManager `GameMode gameMode` alanı; davranış değişmez.
- [ ] VHOLD: grep `WonderHoldTime` const değil; WorldRoot `HoldTime` atar; countdown yeni süreden sayar; default 60sn.
- [ ] VTIME: grep `TimeLimit` MatchSystem + sayaç; süre dolunca 4 takım Score() max; limit 0 regresyonsuz.
- [ ] VDEATH: grep `Deathmatch` WorldRoot; 4 takım food≥20000; RandomMap 200F/200W/100G/0S regresyonsuz.
- [ ] VREGI: grep `King` GameTypes+UnitFactory+WorldRoot; King ölünce eleme; Wonder/Relic countdown atlanır.
- [ ] VNOMAD: grep `Nomad` WorldRoot (BuildBase yok, Villager spawn); ilk-TC-yok grace; AI ≥1 TC inşa eder.
- [ ] VDIPL: grep `Diplomacy` GameManager (4×4); MatchSystem matris okur (hardcoded kalkar); CombatSystem Allied/Neutral'a saldırmaz.
- [ ] DIPL: grep `Diplom` HUD; panel tıkla→GameManager matris güncellenir; text.font=null render.
- [ ] AIDP: grep `Diplomacy` EnemyAI; Allied takım hedeflenmez; default-düşman regresyonsuz.
- [ ] AIWN: MatchSystem countdown public getter; EnemyAI Wonder'a countdown'da hedef değeri yükseltir; AI ordusu Wonder'a yönelir.
- [ ] AIRD: EnemyAI ApplyDifficulty türevleri doküman ile tutarlı (FloorToInt(x+0.5f) veya wiki güncel).
- [ ] AIDF: grep `enum Difficulty` 6 değer; ApplyDifficulty 6 case (default'a düşmez); HUD 6 seviye döner; monoton.
- [ ] AICH: EnemyAI eko/üretim hızında difficulty çarpanı; bedava kaynak YOK; Easy az / Insane çok birim.
- [ ] AISC: grep `AIProfile`/yeni struct; birim karışım profilden; Rusher≠Boomer dağılımı; 3 personality regresyonsuz.
- [ ] AGEB: TechDefs prereqBuildings+minPrereqCount; IsAvailable tamamlanmış bina sayar; Dark'ta FeudalAge butonu kilitli/2-bina ile açılır.
- [ ] AGFX: grep AudioManager.SoundId.AgeUp; HUD.OnAgeAdvanced Play(AgeUp); çağ popup'ı görünür; yalnız team 0.
- [ ] DARK: BuildingDefs Dark-dışı binalar requiredAge/minAge≥Feudal; Dark'ta yalnız temel aktif.
- [ ] ARES: grep GameBootstrap static lastCivs/lastDifficulty; WorldRoot önceki seçimi tercih eder; restart aynı civ/zorluk.
- [ ] STRT: grep SetupScreen/GameBootstrap; civ+difficulty+seed seçilir; Start→WorldRoot.Build; restart setup'ı atlar.
- [ ] STIC: grep CommandIconFactory 4 stance ikonu; HUD seçili stance gösterir; Q+butonla değişir.
- [ ] HKEY: grep `Hotkeys` (HotkeyAction+KeyCode+PlayerPrefs); çağrı noktaları Hotkeys.Get; rebind restart sonrası korunur.
- [ ] HPWB: grep HealthBar/hpBar UnitEntity; billboard fill=hp/maxHp; hasarlıda görünür, full'da gizli; perf düşüşü yok.
- [ ] CMDP: grep `page` HUD; 15+ slot sayfalama; aktif sayfa render; seçimde page=0.
- [ ] MPNG: grep `ping` MinimapSystem; modifiyer+tık ping marker+ses; sol-pan/sağ-move bozulmaz.
- [ ] SUBT: AudioManager birim-sınıfı seçim/onay SoundId; SelectionSystem/CommandSystem tipine göre çalar; villager≠asker.
- [ ] FOWD: grep FogOfWarSystem.fogEnabled varsayılan true; UI toggle; harita başta karanlık, görüş açıldıkça lit; shader bulunur.
- [ ] MMTR: (terrain RTT render HUD-rework'te yapıldı — diamond kamera-RTT minimap) kalan: grep `fog` MinimapSystem; fog açıkken FogOfWarSystem dokusu overlay; keşfedilmemiş siyah; fog kapalı regresyonsuz.
- [ ] SAVF: grep SaveSystem birim+bina+teamCivs serialize; Load arenayı temizleyip yeniden kurar; F5/F9 birim sayısı/pozisyon geri gelir; NavMesh geçerli.
