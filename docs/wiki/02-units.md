# Birimler — AoA Wiki

> Age of Arena'daki tüm eğitilebilir birimler: rol, savaş mekanikleri, gerçek
> istatistikler (HP, hasar, menzil, hız, zırh), strateji ve counter ilişkileri.
> Tüm sayılar doğrudan koddan türetilmiştir — tek doğruluk kaynağı
> [UnitFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs) (taban HP,
> hız, zırh) ve [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs)
> (hasar, menzil, atış aralığı, bonuslar). Birim türleri enum'u
> [GameTypes.cs:9](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9) içinde tanımlı.

## 1. Ne olduğu

Birim, oyuncunun veya AI'nın doğrudan komuta ettiği hareketli varlıktır. AoA'da
`UnitType` enum'ında **25 tür** vardır ([GameTypes.cs:9](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9)).
Bunların `EliteEagle` bir yükseltme rütbesi, `King` ise yalnızca Regicide modunda
otomatik yerleşen kraldır.

| Tür | Rol | Sınıf | Eğitim binası |
|---|---|---|---|
| `Villager` | Kaynak toplama + inşaat | Ekonomi | Town Center |
| `Militia` | Yakın dövüş piyade | Askeri (melee) | Barracks |
| `Spearman` | Anti-süvari piyade | Askeri (melee, counter) | Barracks |
| `Scout` | Hızlı hasarsız keşif | Destek | Barracks |
| `Eagle` | Aztecs hızlı keşif-savaşçısı | Askeri (melee, unique) | Barracks (Aztecs) |
| `Archer` | Menzilli okçu | Askeri (pierce) | Archery Range |
| `Skirmisher` | Anti-okçu menzilli | Askeri (pierce, counter) | Archery Range |
| `Longbowman` | Britons benzersiz okçusu | Askeri (pierce, unique) | Archery Range (Britons) |
| `Cavalry` | Ağır süvari, şarj eden | Askeri (melee) | Stable |
| `Camel` | Anti-süvari develi süvari | Askeri (melee, counter) | Stable |
| `CavalryArcher` | Mobil süvari okçusu | Askeri (pierce) | Stable |
| `Trebuchet` | Uzun menzilli kuşatma | Kuşatma | Castle |
| `Ram` | Koçbaşı, anti-yapı | Kuşatma | Siege Workshop |
| `Mangonel` | Alan hasarlı kuşatma | Kuşatma | Siege Workshop |
| `Medic` | Alan iyileştirici | Destek | Castle |
| `Monk` | Dönüştürme + relic | Destek/dini | Monastery |
| `Galley` | Deniz savaş gemisi | Deniz | Dock |
| `FireShip` | Anti-gemi ateş gemisi | Deniz | Dock |
| `DemoShip` | Patlayıcı splash gemisi | Deniz | Dock |
| `TradeCart` | Pazar ticaret birimi | Ekonomi | Market |
| `TeutonicKnight` | Teutons ağır zırhlı piyade | Askeri (melee, unique) | Castle (Teutons) |
| `WarElephant` | Persians dev savaş fili | Askeri (melee, unique) | Castle (Persians) |
| `Mangudai` | Mongols süvari okçusu (anti-siege) | Askeri (pierce, unique) | Castle (Mongols) |
| `Samurai` | Japanese hızlı piyade | Askeri (melee, unique) | Castle (Japanese) |
| `King` | Regicide kralı (ölümü = elenme) | Özel mod | otomatik (Regicide) |

> `EliteEagle` Eagle'ın Imperial yükseltmesidir (`EliteEagle` tech,
> [GameTypes.cs:163](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L163)); ayrı
> bir eğitim girişi yoktur.

Prosedürel low-poly birimler `UnitFactory` ile üretilir; root'a `CapsuleCollider`,
bir `UnitEntity` ve ayaklarına bir `SelectionRing` eklenir
([UnitFactory.cs:733](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L733)).
Bazı birimler (Villager, Militia, Archer, Scout, Spearman, Skirmisher, Medic, Monk,
Longbowman, King) varsa KayKit animasyonlu görsel kullanır, yoksa primitive mesh'e düşer.

## 2. Nasıl çalışır (mekanik + formül)

### Hareket
Hareket `NavMeshAgent` ile yapılır; ajan hızı `moveSpeed`'ten alınır
([UnitEntity.cs:295](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L295)).
Hız fabrika tarafından `AddComponent`'ten hemen sonra atandığı için ajana bir kare
sonra (`Start`) itilir. Naval birimler (Galley, FireShip, DemoShip) su NavMesh'inde
ayrı bir agent type ID ile yürür ([UnitEntity.cs:303](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L303)).
Efektif hız `RecomputeSpeed` ile taban × civ süvari çarpanı (Mongols) × tech
(Husbandry/Wheelbarrow) olarak yeniden hesaplanır
([UnitEntity.cs:338](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L338)).

### Hasar ve zırh formülü
Etkili hasar tech, medeniyet ve veteranlık bonuslarıyla birleşir
([UnitEntity.cs:148](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148)):

```
AttackDamage = (BaseAttackDamage + TeamTech.AttackBonus(type)) × civMult × VeteranMult
```

Piyade (Militia, Spearman) medeniyet `infantryAttackMult`, okçu sınıfı (Archer,
Longbowman, Skirmisher, CavalryArcher) `archerAttackMult` çarpanı alır
([UnitEntity.cs:153](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L153)).
Hasar alımında zırh türe göre düşülür ve **en az 1 hasar** garanti edilir:

```
hasar = max(1, gelenHasar − zırh)   // zırh = Pierce→pierceArmor, Melee→meleeArmor, Siege→0 (bypass)
```

([UnitEntity.cs:513](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L513)). Siege
hasarı her iki zırhı da yok sayar. Canlı Blacksmith zırh tech'leri (ChainMail/PlateMail,
barding, archer armor, Loom) her vuruşta `TechState`'ten okunur.

### Menzil formülü
```
AttackRange = BaseAttackRange + TeamTech.RangeBonus(type) + civ.archerRangeBonus?
```
Yalnızca Archer ve Longbowman medeniyet `archerRangeBonus`'unu alır — Britons +1
menzil ([UnitEntity.cs:163](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L163)).
Kuşatma birimlerinin bir de **minimum menzili** vardır (yakın hedefe ateş edemez):
Trebuchet 3, Mangonel 2, Galley 1.5 ([UnitEntity.cs:241](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L241)).

### Yeni combat modeli: ArmorClass + additive BonusDamageVs
**Eski çarpan sistemi (×2/×3) kaldırıldı.** Artık her birim/bina bir veya birden
fazla `ArmorClass` taşır (Infantry / Cavalry / Archer / Siege / Building / Ship /
Camel — [GameTypes.cs:35](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L35)),
saldıran ise hedefin sınıfına **düz (additive) bonus hasar** ekler
([UnitEntity.cs:223](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L223)):

| Saldıran | Hedef sınıfı | Bonus hasar | Kaynak |
|---|---|---|---|
| Spearman | Cavalry | +8 | [UnitEntity.cs:230](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L230) |
| Camel | Cavalry | +7 | [UnitEntity.cs:231](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L231) |
| Skirmisher | Archer | +3 | [UnitEntity.cs:232](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L232) |
| Trebuchet | Building | +70 | [UnitEntity.cs:233](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L233) |
| Ram | Building | +16 | [UnitEntity.cs:234](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L234) |
| WarElephant | Building | +30 | [UnitEntity.cs:235](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L235) |
| Mangudai | Siege | +10 | [UnitEntity.cs:236](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L236) |

Sınıf eşlemesi: Cavalry sınıfını Cavalry/Scout/Camel/WarElephant/CavalryArcher/Mangudai
paylaşır, böylece Spearman hattı hepsini sayar; CavalryArcher ve Mangudai aynı anda
Archer + Cavalry sınıfındadır ([UnitEntity.cs:200](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L200)).

### Alan hasarı (splash)
Mangonel 1.8u, DemoShip 2.5u splash yarıçapına sahiptir (`SplashRadius`,
[UnitEntity.cs:249](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L249));
diğer tüm birimler tek hedeflidir (0).

### Cavalry şarjı
Cavalry'nin ilk yakın dövüş vuruşu **2.5×** hasar verir (`ChargeMultiplier`,
[UnitEntity.cs:256](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L256)). Şarj,
4s savaş dışı kalınca resetlenir (`ChargeReady`, [UnitEntity.cs:45](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L45)).
> Not: Şarj çarpanı yalnızca `UnitType.Cavalry`'ye uygulanır; Camel/CavalryArcher
> gibi diğer atlılar şarj almaz.

### Aggro ve stance
Her birimin boşta otomatik düşman tarama yarıçapı vardır (`AggroRadius`,
[UnitEntity.cs:185](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L185)); destek
birimleri (Villager, Monk, Medic, King) `0` döner ve kendiliğinden savaşa girmez.
Scout, Light Cavalry tech'i araştırılana kadar pasif keşiftir (0), sonra savaşçı olur (8).
`AttackStance` (Aggressive/Defensive/StandGround/NoAttack) auto-aggro ve takibi
kontrol eder ([GameTypes.cs:172](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L172)).

### Veteranlık (Veterancy)
Öldürme sayısı rütbe verir: 1 öldürme = Veteran, 3 = Elite. Her rütbe **+10 max HP**
(`VetHpPerRank`) ve **+%10 attack** (`VeteranMult`) sağlar
([UnitEntity.cs:575](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L575)).
> Eski wiki notunun aksine attack bonusu artık kodda mevcut: `VeteranMult => 1 + 0.10 × veteranRank`
> ([UnitEntity.cs:61](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L61)).

### Medic iyileştirme
Medic 6u yarıçapındaki dostlara saniyede 3 HP yeniler (`HealRadius`/`HealPower`,
[UnitEntity.cs:282](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L282)),
`CombatSystem.StepHeal` ile sürülür.

### Monk dönüştürme + faith + relic
Dönüştürme artık olasılıksal: her yeni dönüştürme `[ConvertMinTime 3s, ConvertMaxTime 7s]`
aralığında rastgele bir eşik atar (`convertThreshold`, [UnitEntity.cs:69](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L69));
Theocracy bunu kısaltır. `ConvertTime = 4f` yalnızca eski sabit fallback'tir
([UnitEntity.cs:65](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L65)). Monk
dönüştürmeye başlamak için tam faith'te olmalı; başarılı dönüştürmeden sonra faith
0'a düşer ve ~8s'de dolar (`FaithFull = 100`, `FaithRegenPerSec = 12.5`,
[UnitEntity.cs:75](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L75)). Monk
relic taşıyabilir (`isCarryingRelic`, [UnitEntity.cs:79](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L79)).

### Eğitim (training)
Birimler binadan kuyruğa alınır; kaynak enqueue'da düşülür, zamanlayıcı dolunca
kapıda spawn olur (`TrainingQueue`, [TrainingQueue.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L29)).
14u içinde Blacksmith varsa eğitim **%20 hızlanır**
([TrainingQueue.cs:40](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L40)).
Maks kuyruk 5, pop cap dolunca enqueue reddedilir.

## 3. Gerçek statlar (koddan)

> Taban HP/hız/zırh `UnitFactory`'den; hasar/menzil/aralık/aggro `UnitEntity` switch
> tablolarından. Hız belirtilmeyen birimler `moveSpeed` varsayılanı **3.5**'i kullanır
> ([UnitEntity.cs:21](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L21)). Zırh
> belirtilmeyen birimlerde melee/pierce zırh **0**'dır. "(vars.)" = switch'te açık dal
> yok, `_ => …` varsayılanı kullanılır.

### Savaş & ekonomi birimleri — taban statlar

| Birim | HP | Hız | Melee zırh | Pierce zırh | Kaynak (HP/hız/zırh) |
|---|---|---|---|---|---|
| Villager | 25 | 3.5 (vars.) | 0 | 0 | [UnitFactory.cs:48](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L48) |
| Militia | 40 | 3.5 (vars.) | 0 | 1 | [UnitFactory.cs:73](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L73) |
| Archer | 30 | 3.2 | 0 | 0 | [UnitFactory.cs:95](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L95) |
| Cavalry | 75 | 5.5 | 2 | 2 | [UnitFactory.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L125) |
| Trebuchet | 150 | 1.8 | 0 | 0 | [UnitFactory.cs:162](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L162) |
| Scout | 40 | 6.5 | 0 | 2 | [UnitFactory.cs:184](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L184) |
| Medic | 35 | 3.2 | 0 | 0 | [UnitFactory.cs:209](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L209) |
| Spearman | 25 | 3.3 | 0 | 3 | [UnitFactory.cs:232](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L232) |
| Skirmisher | 30 | 3.2 | 0 | 3 | [UnitFactory.cs:259](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L259) |
| Camel | 80 | 5.8 | 1 | 1 | [UnitFactory.cs:291](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L291) |
| Ram | 200 | 2.2 | 3 | 180 | [UnitFactory.cs:321](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L321) |
| Mangonel | 50 | 2.4 | 0 | 4 | [UnitFactory.cs:351](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L351) |
| CavalryArcher | 50 | 5.2 | 0 | 1 | [UnitFactory.cs:383](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L383) |
| TeutonicKnight | 100 | 2.5 | 5 | 2 | [UnitFactory.cs:405](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L405) |
| WarElephant | 250 | 2.2 | 3 | 3 | [UnitFactory.cs:430](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L430) |
| Mangudai | 60 | 5.5 | 0 | 1 | [UnitFactory.cs:455](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L455) |
| Samurai | 80 | 4.0 | 2 | 2 | [UnitFactory.cs:474](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L474) |
| Eagle | 55 | 4.5 | 0 | 2 | [UnitFactory.cs:495](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L495) |
| King | 75 | 3.2 | 1 | 1 | [UnitFactory.cs:519](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L519) |
| Monk | 30 | 2.8 | 0 | 0 | [UnitFactory.cs:543](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L543) |
| Longbowman | 35 | 3.0 | 0 | 0 | [UnitFactory.cs:566](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L566) |
| Galley | 120 | 4.5 | 0 | 1 | [UnitFactory.cs:617](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L617) |
| FireShip | 100 | 5.5 | 0 | 2 | [UnitFactory.cs:671](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L671) |
| DemoShip | 50 | 4.5 | 0 | 1 | [UnitFactory.cs:696](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L696) |
| TradeCart | 25 | 4.5 | 0 | 0 | [UnitFactory.cs:720](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L720) |

> Ram'in pierce zırhı 180'dir — oklar yalnızca min-1 hasar yapar (pierce'e bağışık),
> [UnitFactory.cs:324](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L324).

### Savaş statları — hasar / menzil / atış aralığı / aggro / hasar türü

| Birim | Hasar | Menzil | Aralık (s) | Aggro | Hasar türü | Bonus | Kaynak (hasar) |
|---|---|---|---|---|---|---|---|
| Militia | 5 | 1.3 | 1.0 | 7 | Melee | — | [UnitEntity.cs:122](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122) |
| Archer | 4 | 6.5 | 1.4 | 9 | Pierce | — | [UnitEntity.cs:122](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122) |
| Cavalry | 8 | 1.4 | 1.1 | 8 | Melee | 2.5× şarj | [UnitEntity.cs:122](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122) |
| Trebuchet | 35 | 15 | 5.5 | 15 | Siege | +70 vs bina | [UnitEntity.cs:123](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L123) |
| Spearman | 4 | 1.5 | 1.3 | 7 | Melee | +8 vs süvari | [UnitEntity.cs:123](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L123) |
| Longbowman | 5 | 8.5 | 1.6 | 11 | Pierce | — | [UnitEntity.cs:123](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L123) |
| Galley | 8 | 5.5 | 2.0 | 8 | Pierce | — | [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124) |
| Skirmisher | 3 | 5 | 2.0 | 9 | Pierce | +3 vs okçu | [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124) |
| Camel | 7 | 1.4 | 1.1 | 8 | Melee | +7 vs süvari | [UnitEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124) |
| Ram | 4 | 1.3 | 3.0 | 4 | Siege | +16 vs bina | [UnitEntity.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) |
| Mangonel | 25 | 9 | 4.0 | 11 | Siege | splash 1.8u | [UnitEntity.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) |
| CavalryArcher | 5 | 4 | 2.0 | 10 | Pierce | — | [UnitEntity.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) |
| FireShip | 6 | 3 | 0.8 | 8 | Pierce | — | [UnitEntity.cs:126](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L126) |
| DemoShip | 40 | 1.5 | 2.0 | 6 | Siege | splash 2.5u | [UnitEntity.cs:126](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L126) |
| TeutonicKnight | 12 | 1.4 | 2.0 | 7 | Melee | — | [UnitEntity.cs:128](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L128) |
| WarElephant | 20 | 1.4 | 2.5 | 8 | Melee | +30 vs bina | [UnitEntity.cs:128](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L128) |
| Mangudai | 6 | 5 | 2.0 | 10 | Pierce | +10 vs siege | [UnitEntity.cs:128](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L128) |
| Samurai | 9 | 1.2 | 1.3 | 8 | Melee | — | [UnitEntity.cs:129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129) |
| Eagle | 7 | 1.3 | 1.5 | 8 | Melee | — | [UnitEntity.cs:129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129) |
| EliteEagle | 9 | 1.3 | 1.4 | 8 | Melee | — | [UnitEntity.cs:129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129) |
| King | 6 | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | — | [UnitEntity.cs:130](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L130) |
| Scout | 0 | 1.1 (vars.) | 1.6 (vars.) | 0/8* | Melee | — | [UnitEntity.cs:133](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) |
| Medic | 0 | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | iyileştirir | [UnitEntity.cs:133](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) |
| Monk | 2 (vars.) | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | dönüştürür | [UnitEntity.cs:134](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L134) |
| Villager | 2 (vars.) | 1.1 (vars.) | 1.6 (vars.) | 0 | Melee | — | [UnitEntity.cs:134](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L134) |

> \* Scout aggro: Light Cavalry tech yoksa **0**, varsa **8** ([UnitEntity.cs:194](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L194)).
> Scout/Medic hasarı açıkça 0'dır; Monk/Villager hasarı (2) `_ => 2f` varsayılanından gelir.

### Minimum menzil & splash

| Birim | Min menzil | Splash | Kaynak |
|---|---|---|---|
| Trebuchet | 3u | — | [UnitEntity.cs:243](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L243) |
| Mangonel | 2u | 1.8u | [UnitEntity.cs:244](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L244) |
| Galley | 1.5u | — | [UnitEntity.cs:245](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L245) |
| DemoShip | — | 2.5u | [UnitEntity.cs:252](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L252) |

### Destek değerleri

| Mekanik | Değer | Kaynak |
|---|---|---|
| Medic iyileştirme yarıçapı | 6u | [UnitEntity.cs:282](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L282) |
| Medic iyileştirme gücü | 3 HP/s | [UnitEntity.cs:284](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L284) |
| Monk dönüştürme süresi | 3–7s (rastgele eşik) | [UnitEntity.cs:69](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L69) |
| Monk faith dolu / regen | 100 / 12.5 HP/s (~8s) | [UnitEntity.cs:75](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L75) |
| Cavalry şarj reset süresi | 4s savaş dışı | [UnitEntity.cs:45](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L45) |
| Veteran rütbe HP bonusu | +10 HP / rütbe | [UnitEntity.cs:59](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L59) |
| Veteran rütbe attack bonusu | +%10 / rütbe | [UnitEntity.cs:61](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L61) |

### Eğitim maliyetleri (food / wood / gold / süre)

| Birim | Bina | Food | Wood | Gold | Süre (s) | Min çağ | Kaynak |
|---|---|---|---|---|---|---|---|
| Villager | Town Center | 50 | 0 | 0 | 25 | Dark | [BuildingEntity.cs:111](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L111) |
| Militia | Barracks | 0 | 60 | 20 | 21 | Dark | [BuildingEntity.cs:116](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L116) |
| Spearman | Barracks | 35 | 25 | 0 | 18 | Feudal | [BuildingEntity.cs:117](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L117) |
| Scout | Barracks | 30 | 0 | 0 | 14 | Dark | [BuildingEntity.cs:118](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L118) |
| Eagle | Barracks (Aztecs) | 20 | 0 | 50 | 20 | Castle | [BuildingEntity.cs:164](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L164) |
| Archer | Archery Range | 0 | 25 | 45 | 22 | Feudal | [BuildingEntity.cs:123](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L123) |
| Skirmisher | Archery Range | 0 | 25 | 35 | 22 | Feudal | [BuildingEntity.cs:124](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L124) |
| Longbowman | Archery Range (Britons) | 0 | 35 | 65 | 26 | Castle | [BuildingEntity.cs:132](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L132) |
| Cavalry | Stable | 80 | 0 | 0 | 24 | Castle | [BuildingEntity.cs:137](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L137) |
| Camel | Stable | 55 | 0 | 60 | 22 | Castle | [BuildingEntity.cs:138](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L138) |
| CavalryArcher | Stable | 0 | 40 | 70 | 26 | Castle | [BuildingEntity.cs:139](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L139) |
| Trebuchet | Castle | 0 | 200 | 100 | 40 | Castle | [BuildingEntity.cs:144](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L144) |
| Medic | Castle | 60 | 0 | 0 | 26 | Castle | [BuildingEntity.cs:145](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L145) |
| TeutonicKnight | Castle (Teutons) | 0 | 0 | 85 | 30 | Castle | [BuildingEntity.cs:151](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L151) |
| WarElephant | Castle (Persians) | 100 | 0 | 70 | 36 | Castle | [BuildingEntity.cs:152](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L152) |
| Mangudai | Castle (Mongols) | 0 | 55 | 65 | 28 | Castle | [BuildingEntity.cs:153](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L153) |
| Samurai | Castle (Japanese) | 60 | 0 | 30 | 26 | Castle | [BuildingEntity.cs:154](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L154) |
| Ram | Siege Workshop | 0 | 160 | 75 | 40 | Castle | [BuildingEntity.cs:186](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L186) |
| Mangonel | Siege Workshop | 0 | 160 | 135 | 45 | Castle | [BuildingEntity.cs:187](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L187) |
| Monk | Monastery | 0 | 0 | 100 | 30 | Castle | [BuildingEntity.cs:169](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L169) |
| TradeCart | Market | 0 | 80 | 50 | 35 | Dark | [BuildingEntity.cs:174](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L174) |
| Galley | Dock | 0 | 120 | 60 | 35 | Feudal | [BuildingEntity.cs:179](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L179) |
| FireShip | Dock | 0 | 100 | 45 | 32 | Feudal | [BuildingEntity.cs:180](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L180) |
| DemoShip | Dock | 0 | 70 | 50 | 30 | Castle | [BuildingEntity.cs:181](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L181) |

> Min çağ `MinAgeFor` ile uygulanır ([BuildingEntity.cs:227](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L227)).
> Unique birimler yalnızca ilgili medeniyette ve Castle Age'de görünür (Eagle Aztecs
> Barracks'ında, Longbowman Britons Archery Range'inde, Castle unique'leri ilgili
> medeniyetin Castle'ında). King'in eğitim girişi yoktur; Regicide modunda otomatik
> yerleşir.

## 4. Strateji & counter

- **Spearman → Süvari sınıfı**: +8 bonus ile düşük tabanlı (4) Spearman bile her atlıyı
  (Cavalry/Camel/Scout/CavalryArcher/WarElephant/Mangudai) söker. Ucuz (35F/25W), Feudal'dan açık.
- **Camel → Süvari**: +7 bonus + yüksek HP (80) + hız (5.8). Spearman'a alternatif
  anti-süvari; pahalı (55F/60G, Castle).
- **Skirmisher → Okçu**: +3 vs Archer sınıfı, pierce zırh 3 ile okçu oklarına dirençli.
  Archer/Longbowman/CavalryArcher hattının doğal counter'ı.
- **Cavalry → Okçu/menzilli**: Yüksek HP (75) + hız (5.5) + şarj (2.5×) ile okçu hattını ezer.
- **Archer/Longbowman → Piyade**: Pierce hasar + menzil (6.5 / 8.5) ile yavaş piyadeyi kite eder.
  Longbowman Britons'ta +1 menzille (9.5) kale duvarından bile vurur.
- **CavalryArcher / Mangudai**: Mobil okçu; vur-kaç. Mangudai siege'e +10 bonusla anti-kuşatma.
- **Ram → Binalar**: +16 bina bonusu, pierce'e bağışık (zırh 180) — okçu ateşi etkisiz.
  Diğer Siege ile birlikte itme kolonunu oluşturur.
- **Mangonel → Yığın birim**: 1.8u splash ile sıkışık piyade/okçu kümelerini biçer; min menzil 2.
- **Trebuchet → Kale/duvar**: 15 menzil + 70 bina bonusu, en güçlü kuşatma; birimlere karşı çok yavaş.
- **WarElephant**: 250 HP dev; +30 bina bonusu, ama Spearman/Camel'e (Cavalry sınıfı) açık.
- **TeutonicKnight**: 5 melee zırh + 12 hasar, ama çok yavaş (2.5) — okçu kite eder.
- **Samurai**: hızlı (4.0) piyade; unique birimlere karşı güçlü dövüşçü.
- **Eagle**: hızlı (4.5) keşif-savaşçısı; Monk/kuşatma avlamada ve baskında ideal.
- **Medic**: orduyu sahada ayakta tutar; savaş birimlerinin arkasında 6u içinde kalmalı.
- **Monk**: pahalı (100 altın); olasılıksal 3–7s dönüştürmeyle yüksek değerli hedef
  (Trebuchet/WarElephant) çalar, relic toplar.
- **Galley / FireShip / DemoShip**: deniz kontrolü; FireShip gemilere kapanır,
  DemoShip 2.5u splash ile gemi kümesini patlatır.

## 5. Çapraz bağlantılar

- Birim yükseltme zincirleri (Man-at-Arms, Crossbowman, Cavalier, Pikeman, Hussar vb.): [./03-unit-upgrades.md](./03-unit-upgrades.md)
- Eğitim binaları ve garnizon mekaniği: [./04-buildings.md](./04-buildings.md)
- Hangi tech hangi birimi etkiler (Forging, Fletching, Bloodlines …): [./05-tech-tree.md](./05-tech-tree.md)
- Medeniyet bonusları (Britons menzil, Franks HP, Mongols hız) ve unique birimler: [./06-civilizations.md](./06-civilizations.md)
- Hasar türü / ArmorClass matrisi ve bonus hasar: [./07-combat-counters.md](./07-combat-counters.md)
- Villager toplama, TradeCart ticaret geliri: [./08-economy-trade.md](./08-economy-trade.md)
- Çağ kilitleri ve birim açılış sırası: [./01-game-flow-ages.md](./01-game-flow-ages.md)
- Seçim, komut, stance UI'si: [./11-controls-ui-feedback.md](./11-controls-ui-feedback.md)

## 6. Kod referansları (file:line, derivation)

- `UnitType` enum (25 tür): [GameTypes.cs:9](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9)
- `ArmorClass` flags enum: [GameTypes.cs:35](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L35)
- Fabrika taban statları (HP, hız, zırh): [UnitFactory.cs:32](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L32)–[723](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L723)
- `Finish()` ortak kurulum (collider, ring, scale): [UnitFactory.cs:733](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L733)
- Varsayılan moveSpeed 3.5: [UnitEntity.cs:21](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L21)
- `BaseAttackDamage` switch: [UnitEntity.cs:120](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L120)
- `BaseAttackRange` switch: [UnitEntity.cs:136](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L136)
- `AttackDamage` (tech + civ + veteran): [UnitEntity.cs:148](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148)
- `AttackRange` (tech + civ archer bonus): [UnitEntity.cs:163](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L163)
- `AttackInterval` switch: [UnitEntity.cs:172](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L172)
- `AggroRadius` switch: [UnitEntity.cs:185](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L185)
- `ArmorClasses` switch: [UnitEntity.cs:200](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L200)
- `BonusDamageVs` (additive bonus model): [UnitEntity.cs:223](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L223)
- `MinAttackRange` / `SplashRadius`: [UnitEntity.cs:241](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L241)
- `ChargeMultiplier` (Cavalry 2.5×): [UnitEntity.cs:256](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L256)
- `DamageKind` / `IsRanged`: [UnitEntity.cs:258](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L258)
- Medic heal: [UnitEntity.cs:282](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L282)
- `TakeDamage` zırh/min-1 formülü: [UnitEntity.cs:513](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L513)
- Veterancy `AddKill` + `VeteranMult`: [UnitEntity.cs:575](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L575)
- Eğitim kuyruğu + Blacksmith aurası: [TrainingQueue.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L29)
- Trainable tanımları + `MinAgeFor` + civ unique: [BuildingEntity.cs:109](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L109)–[251](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L251)

## 7. AoE2 farkı (reference köprü)

Tam AoE2 birim hatları ve istatistik karşılaştırması için:
[../reference/02-units-upgrade-chains.md](../reference/02-units-upgrade-chains.md).

Öne çıkan farklar (M2–M10 sonrası güncel durum):

- **Counter birimleri eklendi**: Artık Skirmisher (anti-archer) ve Camel (anti-cavalry
  alternatifi) mevcut; combat modeli AoE2-tarzı ArmorClass + additive bonus hasarına geçti.
- **Kuşatma çeşitliliği eklendi**: Ram (anti-yapı, pierce-immune) ve Mangonel (1.8u splash)
  Siege Workshop'tan eğitiliyor. Hâlâ eksik: Scorpion / Petard / Onager / Bombard Cannon.
- **Süvari okçusu eklendi**: CavalryArcher (genel) + Mangudai (Mongols unique, anti-siege).
- **Naval genişledi**: Galley + FireShip (anti-ship) + DemoShip (splash). Hâlâ eksik:
  Fishing/Transport/Cannon Galleon ve War Galley/Galleon upgrade chain'i kısmen (tech enum'da var).
- **Eagle Warrior eklendi**: Aztecs unique, Barracks'tan; Elite Eagle Imperial yükseltmesi var.
- **Monk derinleşti**: Olasılıksal 3–7s dönüştürme + faith recharge (100/12.5) + relic taşıma.
- **Civ unique birimler**: TeutonicKnight / WarElephant / Mangudai / Samurai medeniyete bağlı
  Castle'dan eğitiliyor.
- **Regicide King**: tek kral birimi, ölümü takımı eler.
- **Stat ölçeği farklı**: AoA hızları Unity birimi (3.5 u/s) cinsinden; AoE2 tile/s
  ölçeğinde — doğrudan karşılaştırılamaz.
- **Medic AoA'ya özgü**: AoE2'de gezici alan iyileştirici birim yok (yalnız Monk iyileştirir).

## 8. Eksikler/Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| SIEGE2 | Kuşatma | Scorpion / Petard / Onager / Bombard Cannon | reference §Siege | M |
| NAVX2 | Deniz | Fishing Ship / Transport / Cannon Galleon | reference §Naval | M |
| MONKR | Destek/dini | Relic gelir mekaniği + iyileştirme menzili tech'leri tam entegrasyon | reference §Monk | M |
| UNQ2 | Askeri (unique) | Kalan medeniyet unique birimleri (Britons hariç hepsinde değil) | [06-civilizations.md](./06-civilizations.md) | M |
| HEROES | Özel | Senaryo/kahraman birimleri | reference §Heroes | L |
