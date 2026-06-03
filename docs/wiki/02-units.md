# Birimler — AoA Wiki

> Age of Arena'daki tüm eğitilebilir birimler: rol, savaş mekanikleri, gerçek
> istatistikler (HP, hasar, menzil, hız, zırh), strateji ve counter ilişkileri.
> Tüm sayılar doğrudan koddan türetilmiştir — tek doğruluk kaynağı
> [UnitFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs) (taban HP,
> hız, zırh) ve [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs)
> (hasar, menzil, atış aralığı, çarpanlar). Birim türleri enum'u
> [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9) içinde tanımlı.

## 1. Ne olduğu

Birim, oyuncunun veya AI'nın doğrudan komuta ettiği hareketli varlıktır. AoA'da 12
birim türü vardır (`UnitType` enum, [GameTypes.cs:9](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9)):

| Tür | Rol | Sınıf |
|---|---|---|
| `Villager` | Kaynak toplama + inşaat | Ekonomi |
| `Militia` | Yakın dövüş piyade | Askeri (melee) |
| `Spearman` | Anti-süvari piyade | Askeri (melee, counter) |
| `Archer` | Menzilli okçu | Askeri (pierce) |
| `Longbowman` | Britons benzersiz okçusu | Askeri (pierce, unique) |
| `Cavalry` | Ağır süvari, şarj eden | Askeri (melee) |
| `Scout` | Hızlı hasarsız keşif | Destek |
| `Medic` | Alan iyileştirici | Destek |
| `Monk` | Dönüştürme (conversion) | Destek/dini |
| `Trebuchet` | Uzun menzilli kuşatma | Kuşatma |
| `Galley` | Deniz savaş gemisi | Deniz |
| `TradeCart` | Pazar ticaret birimi | Ekonomi |

Her birim prosedürel low-poly mesh ile üretilir (`UnitFactory`), root'a
`CapsuleCollider`, bir `UnitEntity` ve ayaklarına bir `SelectionRing` eklenir
([UnitFactory.cs:345](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L345)).

## 2. Nasıl çalışır (mekanik + formül)

### Hareket
Hareket `NavMeshAgent` ile yapılır; ajan hızı `moveSpeed`'ten alınır
([UnitEntity.cs:204](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L204)).
Birim türü ve hız fabrika tarafından `AddComponent`'ten hemen sonra atandığı için
hız ajana bir kare sonra (`Start`) itilir. Galley su NavMesh'inde ayrı bir agent
type ID ile yürür ([UnitEntity.cs:186](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L186)).

### Hasar ve zırh formülü
Etkili hasar tech ve medeniyet bonuslarıyla birleşir:

```
AttackDamage = (BaseAttackDamage + TeamTech.AttackBonus(type)) × civ.infantryAttackMult?
```

Piyade (Militia, Spearman) medeniyet `infantryAttackMult` çarpanı alır
([UnitEntity.cs:111](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L111)).
Hasar alımında zırh türe göre düşülür ve **en az 1 hasar** garanti edilir:

```
hasar = max(1, gelenHasar − zırh)        // zırh = Pierce→pierceArmor, Melee→meleeArmor, Siege→0 (bypass)
```

([UnitEntity.cs:365](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L365)). Siege
hasarı her iki zırhı da yok sayar.

### Menzil formülü
```
AttackRange = BaseAttackRange + TeamTech.RangeBonus(type) + civ.archerRangeBonus?
```
Okçu sınıfı (Archer, Longbowman) medeniyet `archerRangeBonus`'unu alır — Britons +1
menzil ([UnitEntity.cs:121](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L121)).

### Hasar çarpanları (counter sistemi)
- **Trebuchet** binalara **3×** hasar (`AntiStructureMultiplier`, [UnitEntity.cs:146](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L146)).
- **Cavalry** ilk şarj vuruşunda **2.5×** (`ChargeMultiplier`, [UnitEntity.cs:148](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148)). Şarj, 4s savaş dışı kalınca resetlenir (`ChargeReady`, [UnitEntity.cs:37](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L37)).
- **Spearman** süvariye **3×** (`AntiCavalryMultiplier`, [UnitEntity.cs:150](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L150)).

### Aggro ve stance
Her birimin boşta otomatik düşman tarama yarıçapı vardır (`AggroRadius`,
[UnitEntity.cs:138](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L138)); destek
birimleri (Scout, Medic, Villager, Monk) `0` döner ve kendiliğinden savaşa girmez.
`AttackStance` (Aggressive/Defensive/StandGround/NoAttack) auto-aggro ve takibi
kontrol eder ([GameTypes.cs:61](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L61)).

### Veteranlık (Veterancy)
Öldürme sayısı rütbe verir: 1 öldürme = Veteran, 3 = Elite. Her rütbe atlamada
**+10 max HP** uygulanır (`AddKill`, [UnitEntity.cs:424](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L424)).
> Not: Sınıf yorumu "+%10 attack ve +%10 max HP" der, ancak kodda yalnızca düz +10
> HP eklenir; attack artışı **kodda tanımlı değil**.

### Medic iyileştirme
Medic 6u yarıçapındaki dostlara saniyede 3 HP yeniler (`HealRadius`/`HealPower`,
[UnitEntity.cs:166](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L166)),
`CombatSystem.StepHeal` ile sürülür.

### Monk dönüştürme
Monk hedefe 4 saniye kanalize ederek düşmanı dönüştürür (`ConvertTime`,
[UnitEntity.cs:53](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L53)).

### Eğitim (training)
Birimler binadan kuyruğa alınır; kaynak enqueue'da düşülür, zamanlayıcı dolunca
kapıda spawn olur (`TrainingQueue`, [TrainingQueue.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L29)).
14u içinde Blacksmith varsa eğitim **%20 hızlanır**
([TrainingQueue.cs:40](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L40)).
Maks kuyruk 5, pop cap dolunca enqueue reddedilir.

## 3. Gerçek statlar (koddan)

> Taban HP/hız/zırh `UnitFactory`'den; hasar/menzil/aralık/aggro `UnitEntity` switch
> tablolarından. Hız belirtilmeyen birimler `moveSpeed` varsayılanı **3.5**'i kullanır
> ([UnitEntity.cs:17](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L17)). Zırh
> belirtilmeyen birimlerde melee/pierce zırh **0**'dır.

### Savaş birimleri — taban statlar

| Birim | HP | Hız | Melee zırh | Pierce zırh | Kaynak |
|---|---|---|---|---|---|
| Militia | 40 | 3.5 (vars.) | 0 | 1 | [UnitFactory.cs:49](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L49) |
| Spearman | 25 | 3.3 | 0 | 3 | [UnitFactory.cs:210](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L210) |
| Archer | 30 | 3.2 | 0 | 0 | [UnitFactory.cs:72](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L72) |
| Longbowman | 35 | 3.0 | 0 | 0 | [UnitFactory.cs:257](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L257) |
| Cavalry | 75 | 5.5 | 2 | 2 | [UnitFactory.cs:102](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L102) |
| Trebuchet | 150 | 1.8 | 0 | 0 | [UnitFactory.cs:139](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L139) |
| Galley | 120 | 4.5 | 0 | 1 | [UnitFactory.cs:308](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L308) |
| Scout | 40 | 6.5 | 0 | 2 | [UnitFactory.cs:161](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L161) |
| Medic | 35 | 3.2 | 0 | 0 | [UnitFactory.cs:187](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L187) |
| Monk | 30 | 2.8 | 0 | 0 | [UnitFactory.cs:233](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L233) |

### Ekonomi birimleri

| Birim | HP | Hız | Kaynak |
|---|---|---|---|
| Villager | 25 | 3.5 (vars.) | [UnitFactory.cs:28](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L28) |
| TradeCart | 25 | 4.5 | [UnitFactory.cs:332](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L332) |

### Savaş statları — hasar / menzil / atış aralığı / aggro

| Birim | Hasar | Menzil | Aralık (s) | Aggro | Hasar türü | Çarpan | Kaynak |
|---|---|---|---|---|---|---|---|
| Militia | 5 | 1.3 | 1.0 | 7 | Melee | — | [UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96) |
| Spearman | 4 | 1.5 | 1.3 | 7 | Melee | 3× vs süvari | [UnitEntity.cs:97](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L97) |
| Archer | 4 | 6.5 | 1.4 | 9 | Pierce | — | [UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96) |
| Longbowman | 5 | 8.5 | 1.6 | 11 | Pierce | — | [UnitEntity.cs:97](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L97) |
| Cavalry | 8 | 1.4 | 1.1 | 8 | Melee | 2.5× şarj | [UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96) |
| Trebuchet | 35 | 15 | 5.5 | 15 | Siege | 3× vs bina | [UnitEntity.cs:97](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L97) |
| Galley | 8 | 5.5 | 2.0 | 8 | Pierce | — | [UnitEntity.cs:98](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L98) |
| Scout | 0 | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | — | [UnitEntity.cs:99](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L99) |
| Medic | 0 | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | iyileştirir | [UnitEntity.cs:99](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L99) |
| Monk | 2 (vars.) | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | dönüştürür | [UnitEntity.cs:101](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L101) |
| Villager | 2 (vars.) | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | — | [UnitEntity.cs:101](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L101) |

> "(vars.)" = switch'te açık dal yok, `_ => …` varsayılanı kullanılır. Scout ve Medic
> hasarı açıkça 0'dır ([UnitEntity.cs:99](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L99)).

### Destek değerleri

| Mekanik | Değer | Kaynak |
|---|---|---|
| Medic iyileştirme yarıçapı | 6u | [UnitEntity.cs:166](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L166) |
| Medic iyileştirme gücü | 3 HP/s | [UnitEntity.cs:168](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L168) |
| Monk dönüştürme süresi | 4s | [UnitEntity.cs:53](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L53) |
| Cavalry şarj reset süresi | 4s savaş dışı | [UnitEntity.cs:37](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L37) |
| Veteran rütbe HP bonusu | +10 HP / rütbe | [UnitEntity.cs:431](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L431) |

### Eğitim maliyetleri (food / wood / gold / süre)

| Birim | Bina | Food | Wood | Gold | Süre (s) | Min çağ | Kaynak |
|---|---|---|---|---|---|---|---|
| Villager | Town Center | 50 | 0 | 0 | 25 | Dark | [BuildingEntity.cs:102](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L102) |
| Militia | Barracks | 0 | 60 | 20 | 21 | Dark | [BuildingEntity.cs:107](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L107) |
| Spearman | Barracks | 35 | 25 | 0 | 18 | Feudal | [BuildingEntity.cs:108](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L108) |
| Scout | Barracks | 30 | 0 | 0 | 14 | Dark | [BuildingEntity.cs:109](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L109) |
| Archer | Archery Range | 0 | 25 | 45 | 22 | Feudal | [BuildingEntity.cs:114](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L114) |
| Longbowman | Archery Range (Britons) | 0 | 35 | 65 | 26 | Castle | [BuildingEntity.cs:121](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L121) |
| Cavalry | Stable | 80 | 0 | 0 | 24 | Castle | [BuildingEntity.cs:126](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L126) |
| Trebuchet | Castle | 0 | 200 | 100 | 40 | Castle | [BuildingEntity.cs:131](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L131) |
| Medic | Castle | 60 | 0 | 0 | 26 | Castle | [BuildingEntity.cs:132](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L132) |
| Monk | Monastery | 0 | 0 | 100 | 30 | Castle | [BuildingEntity.cs:137](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L137) |
| TradeCart | Market | 0 | 80 | 50 | 35 | Dark | [BuildingEntity.cs:142](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L142) |
| Galley | Dock | 0 | 120 | 60 | 35 | Feudal | [BuildingEntity.cs:147](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L147) |

> Min çağ `MinAgeFor` ile uygulanır ([BuildingEntity.cs:175](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L175));
> Longbowman yalnızca Britons'ta ve Castle Age'de görünür.

## 4. Strateji & counter

- **Spearman → Cavalry**: 3× hasar çarpanı ile düşük tabanlı (4) Spearman bile süvariyi söker. Ucuz (35F/25W), Feudal'dan açık. Süvari spam'ine cevap.
- **Cavalry → Archer/menzilli**: Yüksek HP (75) + hız (5.5) + şarj (2.5×) ile okçu hattını ezer. Ama Spearman'a karşı dikkatli kullanılmalı.
- **Archer/Longbowman → Piyade**: Pierce hasar ve menzil (6.5 / 8.5) ile yavaş Militia/Spearman'ı kite eder. Longbowman 8.5 menzille (Britons +1 = 9.5) kale duvarından bile vurur.
- **Trebuchet → Binalar**: 15 menzil + 3× bina hasarı. Birimlere karşı çok yavaş (5.5s aralık); önden korunmalı.
- **Militia → Genel**: dengeli ucuz piyade; pierce zırh 1 ile okçuya kısmi direnç.
- **Scout**: hasarsız, en hızlı (6.5) keşif; düşman ekonomisini erken görür.
- **Medic**: orduyu sahada ayakta tutar; savaş birimlerinin arkasında 6u içinde kalmalı.
- **Monk**: pahalı (100 altın) ama düşman birimini 4s'de dönüştürerek sayısal avantaj kazanır; trebuchet/cavalry gibi yüksek değerli hedefleri çalmak için ideal.
- **Galley**: deniz kontrolü; pierce hasar, sahil binalarına ve karşı gemilere.

## 5. Çapraz bağlantılar

- Birim yükseltme zincirleri (Man-at-Arms, Crossbowman, Cavalier vb.): [./03-unit-upgrades.md](./03-unit-upgrades.md)
- Eğitim binaları ve garnizon mekaniği: [./04-buildings.md](./04-buildings.md)
- Hangi tech hangi birimi etkiler (Forging, Fletching, Bloodlines …): [./05-tech-tree.md](./05-tech-tree.md)
- Medeniyet bonusları (Britons menzil, Franks HP, Mongols hız): [./06-civilizations.md](./06-civilizations.md)
- Hasar türü / zırh matrisi ve counter çarpanları: [./07-combat-counters.md](./07-combat-counters.md)
- Villager toplama, TradeCart ticaret geliri: [./08-economy-trade.md](./08-economy-trade.md)
- Çağ kilitleri ve birim açılış sırası: [./01-game-flow-ages.md](./01-game-flow-ages.md)
- Seçim, komut, stance UI'si: [./11-controls-ui-feedback.md](./11-controls-ui-feedback.md)

## 6. Kod referansları (file:line, derivation)

- `UnitType` enum (12 tür): [GameTypes.cs:9](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9)
- Fabrika taban statları (HP, hız, zırh atamaları): [UnitFactory.cs:13](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L13)–[312](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L312)
- `Finish()` ortak kurulum (collider, ring, scale): [UnitFactory.cs:345](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L345)
- Varsayılan moveSpeed 3.5: [UnitEntity.cs:17](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L17)
- `BaseAttackDamage` switch: [UnitEntity.cs:94](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L94)
- `BaseAttackRange` switch: [UnitEntity.cs:103](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L103)
- `AttackDamage` (tech + civ infantry mult): [UnitEntity.cs:111](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L111)
- `AttackRange` (tech + civ archer bonus): [UnitEntity.cs:121](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L121)
- `AttackInterval` switch: [UnitEntity.cs:130](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L130)
- `AggroRadius` switch: [UnitEntity.cs:138](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L138)
- Çarpanlar (anti-structure / charge / anti-cavalry): [UnitEntity.cs:146](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L146)–[150](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L150)
- `DamageKind` / `IsRanged`: [UnitEntity.cs:152](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L152)
- Medic heal: [UnitEntity.cs:166](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L166)
- `TakeDamage` zırh/min-1 formülü: [UnitEntity.cs:365](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L365)
- Veterancy `AddKill`: [UnitEntity.cs:424](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L424)
- Eğitim kuyruğu + Blacksmith aurası: [TrainingQueue.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L29)
- Trainable tanımları + `MinAgeFor`: [BuildingEntity.cs:100](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L100)–[186](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L186)

## 7. AoE2 farkı (reference köprü)

Tam AoE2 birim hatları ve istatistik karşılaştırması için:
[../reference/02-units-upgrade-chains.md](../reference/02-units-upgrade-chains.md).

Öne çıkan farklar:

- **Stat ölçeği farklı**: AoA Militia HP 40/atk 5 ≈ AoE2 (HP 40/atk 4) ama AoA hızları
  Unity birimi (3.5 u/s) cinsinden; AoE2 1.0 civarı tile/s ölçeğinde — doğrudan
  karşılaştırılamaz.
- **Eksik counter birimleri**: AoE2'deki **Skirmisher** (anti-archer) ve **Camel**
  (anti-cavalry alternatifi) AoA'da yok; tek anti-cavalry Spearman hattı.
- **Eksik kuşatma çeşitliliği**: AoE2'de Ram / Mangonel (AoE) / Scorpion / Petard var;
  AoA'da yalnızca Trebuchet. Alan hasarı (splash) **kodda tanımlı değil**.
- **Eksik süvari okçusu**: Cavalry Archer / Mangudai sınıfı AoA'da yok.
- **Naval sadeleştirilmiş**: AoE2'de Fishing/Transport/Fire/Demo/Cannon Galleon var;
  AoA'da yalnızca tek `Galley` (Fire Ship, Demo Ship, Cannon Galleon eksik).
- **Eagle Warrior hattı** (Aztek/Maya/İnka) AoA'da yok.
- **Monk basitleştirilmiş**: AoE2 dönüştürme ~10s + faith mekaniği; AoA sabit 4s, faith
  yok, relic taşıma **kodda tanımlı değil**.
- **Medic AoA'ya özgü**: AoE2'de gezici alan iyileştirici birim yok (yalnız Monk iyileştirir).

## 8. Eksikler/Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| SKIR | Askeri (pierce-counter) | Skirmisher hattı (anti-archer, bonus vs archers) | [02-units-upgrade-chains.md](../reference/02-units-upgrade-chains.md) §Skirmisher | M |
| SIEGE | Kuşatma | Ram / Mangonel(splash) / Scorpion / Petard birimleri | reference §Siege | L |
| SPLASH | Savaş mekaniği | Alan hasarı (AoE splash) sistemi — Mangonel/Onager için | reference §Mangonel | M |
| CAVAR | Askeri (pierce) | Cavalry Archer hattı (mobil okçu) | reference §Cavalry Archer | M |
| NAVX | Deniz | Fire Ship / Demo Ship / Cannon Galleon + War Galley upgrade | reference §Naval | L |
| EAGLE | Askeri (piyade-süvari) | Eagle Warrior hattı (medeniyete özgü) | reference §Eagle Warrior | M |
| MFAITH | Destek/dini | Monk faith + relic taşıma + iyileştirme | reference §Monk | M |
| CAMEL | Askeri (anti-cavalry) | Camel Rider hattı (Spearman alternatifi) | reference §Camel | S |
| VETATK | Savaş mekaniği | Veteranlık attack bonusu (yorumda var, kodda yalnız +HP) | — (iç tutarsızlık) | S |
