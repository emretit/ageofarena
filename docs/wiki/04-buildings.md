# Binalar & Garnizon — AoA Wiki

> Age of Arena'daki tüm binalar: maliyet, HP, zırh, çağ kilidi, drop-off, üretim,
> savunma ateşi ve garnizon mekanikleri. Tüm sayılar koddaki merkezi
> `BuildingDefs.Table` tablosundan gelir (tek doğruluk kaynağı). Savunma ateşi
> `BuildingCombatSystem`, garnizon yaşam döngüsü `GarrisonSystem`, hasar/zırh
> `BuildingEntity.TakeDamage` içinde tanımlıdır.
>
> Kod kaynağı:
> [BuildingDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs),
> [BuildingEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs),
> [BuildingCombatSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs),
> [GarrisonSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/GarrisonSystem.cs)

## 1. Ne olduğu

Binalar oyuncunun ekonomisini, askeri üretimini ve savunmasını kuran sabit
yapılardır. AoA'da binalar tek bir veri tablosundan (`BuildingDefs.Table`) beslenir;
maliyet, inşa süresi, sağladığı nüfus, HP, zırh, çağ kilidi, drop-off kabulü,
savunma ateşi ve garnizon kapasitesi hep aynı satırda tutulur. Bu sayede inşa menüsü
(maliyet/hotkey), `BuildingEntity` (HP/zırh) ve yerleştirme/AI sistemleri aynı
sayıları okur.

Binalar fonksiyonel olarak şu gruplara ayrılır:

- **Merkez & nüfus:** Town Center, House
- **Askeri üretim:** Barracks, Archery Range, Stable, Castle, Monastery, Dock
- **Ekonomi & drop-off:** Town Center, Mill, Lumber Camp, Mining Camp, Farm, Market
- **Teknoloji:** Blacksmith, Monastery, University
- **Savunma:** Castle, Watch Tower, Wall, Gate
- **Zafer:** Wonder (Anıt)

Garnizon, dost bir binanın içine birim sığınması mekaniğidir: birimler gizlenir,
iyileşir ve binanın savunma ateşine ek ok katar. Yalnızca `garrisonCapacity > 0`
olan binalar (Town Center, Castle) garnizon kabul eder.

## 2. Nasıl çalışır (mekanik + formül)

### Maliyet, inşa, nüfus, HP
Her bina `BuildingDef` struct'ı ile tanımlanır: `food/wood/gold/stone`, `buildTime`
(saniye), `popProvided` (nüfus kapasitesi katkısı), `maxHp`, `hotkey`, `buildable`,
`minAge`. HP `BuildingEntity.Start()` içinde `MaxHpFor(type)` ile tablodan çekilir.

### Zırh ve hasar
`BuildingEntity.TakeDamage(amount, damageType)` her bina için ayrı `meleeArmor` ve
`pierceArmor` uygular. Formül:

```
hasar = max(1, amount - armor)
```

`armor` saldırının `DamageType`'ına göre seçilir:
- `DamageType.Melee` → `meleeArmor`
- `DamageType.Pierce` → `pierceArmor`
- `DamageType.Siege` → `0` (zırhı tamamen baypas eder)

Yani **kuşatma hasarı bina zırhını yok sayar** — duvar/kale taş yığını kuşatma
silahlarına karşı zayıftır. HP %50'nin altına düşünce bina kararır (`TintDamage`),
0'a inince `Die()` ile yıkılır.

### Drop-off (kaynak teslim)
`isDropoff` ve `dropoffMask` (bit maskesi) belirler. `AcceptsDropoff(type, kind)`
ilgili bit set ise true döner. Maskeler:
- `MaskAll` — Food + Wood + Gold + Stone (Town Center)
- `MaskWood` — Wood (Lumber Camp)
- `MaskMine` — Gold + Stone (Mining Camp)
- `MaskFood` — Food (Mill, Dock)

### Çağ kilidi
`UnlockedAt(type, age)` → `age >= def.minAge`. Bina menüde görünse de takım gerekli
çağa ulaşmadan inşa edilemez.

### Savunma ateşi (BuildingCombatSystem)
`def.attackRange > 0` olan binalar (Castle, Watch Tower) menzildeki en yakın düşman
birime cooldown ile ok atar. Pasif bina (Town Center) yalnızca **içinde garnizon
varken** `GarrisonRange = 8` menzille ateş eder.

- Castle/Watch Tower oku: `def.attackDamage`, `DamageType.Pierce`
- Her garnizon birimi ek ok ekler: `GarrisonArrowDamage = 6`, en fazla
  `MaxGarrisonArrows = 5` ek ok
- Cooldown: `def.attackInterval` (yoksa `GarrisonInterval = 1.2`)
- Namlu yüksekliği: `MuzzleHeight = 5`

### Garnizon yaşam döngüsü (GarrisonSystem)
1. **Giriş:** `garrisonTarget` belirlenen birim binaya `Radius + EnterPad (1)`
   mesafesine ulaşınca `garrison` listesine eklenir, `EnterGarrison()` ile gizlenir
   (nüfus korunur — birim hâlâ `gm.units` içindedir).
2. **İyileşme:** Sığınan her birim saniyede `HealRate = 5` HP iyileşir.
3. **Çıkış:** `UngarrisonAll(b)` tüm birimleri kapının önüne (`GateBack = 3.5`) veya
   rally noktasına yönlendirir; 4'erli gruplar hâlinde yelpaze yapar.
4. **Yıkım:** Bina yıkılırsa `OnBuildingDestroyed` ile **içindeki tüm birimler ölür**
   (kaçış yok).

## 3. Gerçek statlar (koddan)

> Stat JSON girdisi boş (`[]`) geldiği için tüm sayılar doğrudan
> `BuildingDefs.Table` (BuildingDefs.cs) satırlarından alınmıştır — kod tek doğruluk
> kaynağıdır. `f/w/g/s` = food/wood/gold/stone.

### 3.1 Maliyet, inşa, nüfus, HP, çağ

| Bina | f | w | g | s | Süre (s) | Nüfus | HP | Çağ | İnşa? | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|
| Town Center | 0 | 0 | 0 | 0 | 60 | +5 | 600 | Dark | hayır | [BuildingDefs.cs:65](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L65) |
| House | 0 | 30 | 0 | 0 | 12 | +5 | 300 | Dark | evet | [BuildingDefs.cs:66](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L66) |
| Barracks | 0 | 120 | 0 | 0 | 25 | 0 | 400 | Dark | evet | [BuildingDefs.cs:67](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L67) |
| Archery Range | 0 | 120 | 0 | 0 | 25 | 0 | 400 | Feudal | evet | [BuildingDefs.cs:68](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L68) |
| Stable | 0 | 120 | 0 | 0 | 25 | 0 | 400 | Castle | evet | [BuildingDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L69) |
| Farm | 0 | 60 | 0 | 0 | 12 | 0 | 200 | Dark | evet | [BuildingDefs.cs:70](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L70) |
| Lumber Camp | 0 | 50 | 0 | 0 | 10 | 0 | 150 | Dark | evet | [BuildingDefs.cs:71](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L71) |
| Mining Camp | 0 | 50 | 0 | 0 | 10 | 0 | 150 | Dark | evet | [BuildingDefs.cs:72](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L72) |
| Mill | 0 | 60 | 0 | 0 | 12 | 0 | 150 | Dark | evet | [BuildingDefs.cs:73](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L73) |
| Market | 0 | 175 | 0 | 0 | 25 | 0 | 350 | Dark | evet | [BuildingDefs.cs:74](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L74) |
| Castle | 0 | 0 | 0 | 650 | 80 | +10 | 2000 | Dark | evet | [BuildingDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L77) |
| Wall | 0 | 10 | 0 | 0 | 4 | 0 | 200 | Dark | evet | [BuildingDefs.cs:80](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L80) |
| Gate | 0 | 30 | 0 | 0 | 8 | 0 | 450 | Dark | evet | [BuildingDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L81) |
| Wonder (Anıt) | 0 | 500 | 800 | 600 | 150 | 0 | 3000 | Imperial | evet | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Watch Tower (Gözetleme Kulesi) | 0 | 125 | 0 | 0 | 18 | 0 | 500 | Feudal | evet | [BuildingDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L85) |
| Blacksmith (Demirci) | 0 | 150 | 0 | 0 | 20 | 0 | 350 | Feudal | evet | [BuildingDefs.cs:87](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L87) |
| Monastery (Manastır) | 0 | 175 | 0 | 0 | 22 | 0 | 350 | Castle | evet | [BuildingDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L89) |
| University (Üniversite) | 0 | 200 | 0 | 150 | 28 | 0 | 400 | Castle | evet | [BuildingDefs.cs:91](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L91) |
| Dock (Liman) | 0 | 150 | 0 | 0 | 25 | 0 | 300 | Dark | evet | [BuildingDefs.cs:93](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L93) |

> Not: Castle `minAge` parametresi tabloda verilmemiş, varsayılan `Age.Dark`'a düşer
> ([BuildingDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L77)).
> Yani kodda Castle teknik olarak Dark Age'den inşa edilebilir (taş ekonomisi gerekir);
> AoE2'de Castle Age kilidi vardır — bkz. §7.

### 3.2 Zırh (melee / pierce)

> Varsayılan zırh `BuildingDef` ctor'unda `meleeArm: 1`, `pierceArm: 3`
> ([BuildingDefs.cs:40](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L40)).
> Aşağıdaki tabloda yalnızca varsayılandan farklı tanımlananlar açık değerle, geri
> kalanlar varsayılanla gösterilir.

| Bina | Melee zırh | Pierce zırh | Kaynak |
|---|---|---|---|
| Town Center | 3 | 5 | [BuildingDefs.cs:65](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L65) |
| Castle | 8 | 8 | [BuildingDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L77) |
| Wall | 10 | 10 | [BuildingDefs.cs:80](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L80) |
| Wonder | 5 | 8 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Watch Tower | 2 | 4 | [BuildingDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L85) |
| Diğer tüm binalar | 1 (varsayılan) | 3 (varsayılan) | [BuildingDefs.cs:40](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L40) |

> Kuşatma (`DamageType.Siege`) hasarı zırhı tamamen baypas eder
> ([BuildingEntity.cs:67](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L67)).

### 3.3 Savunma ateşi & garnizon kapasitesi

| Bina | Atk menzil | Atk hasar | Atk aralık (s) | Garnizon kap. | Kaynak |
|---|---|---|---|---|---|
| Castle | 9 | 18 | 1.5 | 15 | [BuildingDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L77) |
| Watch Tower | 6 | 7 | 2.0 | 0 | [BuildingDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L85) |
| Town Center | 0 (pasif) | — | — | 10 | [BuildingDefs.cs:65](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L65) |
| Diğer tüm binalar | 0 | — | — | 0 | [BuildingDefs.cs:64-93](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L64) |

Garnizon savunma ateşi sabitleri (BuildingCombatSystem):

| Sabit | Değer | Kaynak |
|---|---|---|
| GarrisonRange (pasif bina ateş menzili) | 8 | [BuildingCombatSystem.cs:18](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L18) |
| GarrisonArrowDamage (garnizon birimi başına ok) | 6 | [BuildingCombatSystem.cs:19](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L19) |
| GarrisonInterval (pasif bina cooldown) | 1.2 | [BuildingCombatSystem.cs:20](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L20) |
| MaxGarrisonArrows (ek ok üst sınırı) | 5 | [BuildingCombatSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L21) |
| MuzzleHeight (namlu yüksekliği) | 5 | [BuildingCombatSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L14) |

Garnizon yaşam döngüsü sabitleri (GarrisonSystem):

| Sabit | Değer | Kaynak |
|---|---|---|
| HealRate (iyileşme, hp/s) | 5 | [GarrisonSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/GarrisonSystem.cs#L13) |
| GateBack (çıkış offseti) | 3.5 | [GarrisonSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/GarrisonSystem.cs#L14) |
| EnterPad (varış toleransı) | 1 | [GarrisonSystem.cs:15](../../AgeOfArenaUnity/Assets/Scripts/GarrisonSystem.cs#L15) |

### 3.4 Drop-off (kaynak teslim noktaları)

| Bina | Kabul ettiği kaynak | Maske | Kaynak |
|---|---|---|---|
| Town Center | Food + Wood + Gold + Stone | MaskAll | [BuildingDefs.cs:65](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L65) |
| Lumber Camp | Wood | MaskWood | [BuildingDefs.cs:71](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L71) |
| Mining Camp | Gold + Stone | MaskMine | [BuildingDefs.cs:72](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L72) |
| Mill | Food | MaskFood | [BuildingDefs.cs:73](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L73) |
| Dock | Food | MaskFood | [BuildingDefs.cs:93](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L93) |

### 3.5 Üretim binaları (eğitilebilen birimler)

> Birim statları için bkz. [Birimler](./02-units.md). Çağ kilitleri
> `BuildingEntity.MinAgeFor`
> ([BuildingEntity.cs:175](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L175))
> ile filtrelenir; `(Feudal+)` / `(Castle+)` etiketi bu switch'te açıkça listelenen
> kilidi gösterir, etiketsiz birimler Dark Age'den itibaren eğitilebilir
> (switch'te yoksa `_ => Age.Dark`).

| Bina | Eğitilebilen birimler | Kaynak |
|---|---|---|
| Town Center | Villager | [BuildingEntity.cs:100-103](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L100) |
| Barracks | Militia, Scout, Spearman (Feudal+) | [BuildingEntity.cs:105-110](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L105) |
| Archery Range | Archer (Feudal+) | [BuildingEntity.cs:112-115](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L112) |
| Archery Range (Britons) | Archer (Feudal+), Longbowman (Castle+) | [BuildingEntity.cs:118-122](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L118) |
| Stable | Cavalry (Castle+) | [BuildingEntity.cs:124-127](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L124) |
| Castle | Trebuchet (Castle+), Medic (Castle+) | [BuildingEntity.cs:129-133](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L129) |
| Monastery | Monk (Castle+) | [BuildingEntity.cs:135-138](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L135) |
| Market | Trade Cart | [BuildingEntity.cs:140-143](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L140) |
| Dock | Galley (Feudal+) | [BuildingEntity.cs:145-148](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L145) |

> Önemli: Scout (`UnitType.Scout`) `MinAgeFor` switch'inde **yer almaz**
> ([BuildingEntity.cs:175-186](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L175)),
> bu yüzden `_ => Age.Dark`'a düşer — yani Scout Dark Age'den itibaren eğitilebilir,
> yalnızca Spearman Feudal+ kilitlidir. Militia da kilitsizdir.
>
> Teknoloji araştırma binaları (Blacksmith, University) ve Monastery ek olarak
> `GetResearchables()` ile teknoloji sunar; ayrıntı için bkz.
> [Teknoloji Ağacı](./05-tech-tree.md).

## 4. Strateji & counter

- **Drop-off optimizasyonu:** Lumber/Mining Camp'i kaynağa yakın kurmak villager
  yürüme süresini kısaltır. Town Center her kaynağı kabul eder ama genelde merkezde
  olduğundan kenar kaynaklar için ayrı kamp şarttır. Mill, Farm yiyeceğini toplar.
- **Savunma:** Castle (atk 18, menzil 9, melee/pierce zırh 8) en güçlü savunma yapısı;
  2000 HP + yüksek zırh ile melee/pierce'a dayanıklıdır ama **kuşatmaya karşı zırhsız**
  (Trebuchet/Galley taş hasarı zırhı baypas eder). Watch Tower erken-oyun ucuz
  savunmasıdır (atk 7, 500 HP).
- **Garnizon savunması:** Saldırı altında villager/okçuları Town Center veya Castle'a
  garnizonlamak hem onları iyileştirir hem ek ok sağlar (birim başına 6 hasar, en fazla
  5 ek ok). Town Center yalnızca **garnizonluyken** ateş açar — boşken pasiftir.
- **Garnizon riski:** Bina yıkılırsa içindeki tüm birimler ölür. Kale düşmek üzereyse
  garnizonu boşalt.
- **Wall + Gate:** Wall (200 HP, zırh 10/10) yol bulmayı keser; Gate (450 HP) geçit
  açar. İkisi de kuşatmaya karşı zayıf — duvar arkasını Castle/Watch Tower ile destekle.
- **Counter:** Binalara karşı en verimli birimler kuşatma sınıfı (Trebuchet, `Siege`
  hasar). Düz melee/pierce birimleri yüksek zırhlı binalarda (Castle, Wall) çok yavaş
  ilerler.

## 5. Çapraz bağlantılar

- [Oyun Akışı & Çağlar](./01-game-flow-ages.md) — çağ kilitleri ve ilerleme
- [Birimler](./02-units.md) — bu binaların eğittiği birimlerin statları
- [Birim Yükseltme Zincirleri](./03-unit-upgrades.md) — Blacksmith yükseltmeleri
- [Teknoloji Ağacı](./05-tech-tree.md) — Blacksmith/University/Monastery araştırmaları
- [Medeniyetler](./06-civilizations.md) — Britons'ın Archery Range Longbowman bonusu
- [Savaş & Counter Sistemi](./07-combat-counters.md) — zırh/hasar tipi etkileşimi
- [Ekonomi & Ticaret](./08-economy-trade.md) — drop-off ve Market/Trade Cart
- [Zafer Koşulları](./10-victory-objectives.md) — Wonder (Anıt) zafer binası
- [Kontroller & UI](./11-controls-ui-feedback.md) — inşa menüsü, hotkey, rally point

## 6. Kod referansları (file:line, derivation)

- **Tek doğruluk tablosu** — `BuildingDefs.Table`
  ([BuildingDefs.cs:62-94](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L62)):
  her binanın maliyet/HP/zırh/çağ/drop-off/atk/garnizon değerleri.
- **Varsayılan zırh** — ctor `meleeArm: 1f, pierceArm: 3f`
  ([BuildingDefs.cs:40](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L40)).
- **HP/zırh ataması** — `BuildingEntity.Start()`
  ([BuildingEntity.cs:45-52](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L45)):
  `maxHp = MaxHpFor(type)`, zırh tablodan kopyalanır.
- **Hasar formülü** — `TakeDamage` `hp -= max(1, amount - armor)`; Siege → armor 0
  ([BuildingEntity.cs:60-74](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L60)).
- **Drop-off maskeleri** — `MaskAll/Wood/Mine/Food`
  ([BuildingDefs.cs:56-60](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L56)),
  kontrol `AcceptsDropoff`
  ([BuildingDefs.cs:113-117](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L113)).
- **Çağ kilidi** — `UnlockedAt`
  ([BuildingDefs.cs:107](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L107)).
- **Eğitilebilen birimler & filtre** — `GetTrainables` + `MinAgeFor`
  ([BuildingEntity.cs:188-215](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L188),
  [BuildingEntity.cs:175-186](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L175)).
  Scout switch'te yok → `_ => Age.Dark` (kilitsiz).
- **Savunma ateşi** — `BuildingCombatSystem.Tick`
  ([BuildingCombatSystem.cs:23-52](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L23)):
  menzil seçimi, ok spawn, garnizon ek oku, cooldown.
- **Garnizon yaşam döngüsü** — `GarrisonSystem.Tick/UngarrisonAll/OnBuildingDestroyed`
  ([GarrisonSystem.cs:17-74](../../AgeOfArenaUnity/Assets/Scripts/GarrisonSystem.cs#L17)).
- **Garnizon kapasite/durum** — `BuildingEntity.GarrisonCapacity/HasGarrisonSpace`
  ([BuildingEntity.cs:36-38](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L36)).

## 7. AoE2 farkı (reference köprü)

Tam karşılaştırma: [docs/reference/03-buildings-by-age.md](../reference/03-buildings-by-age.md).

- **Ölçek:** AoA HP değerleri AoE2'nin "dikey dilim" küçültülmüş hâlidir (Castle
  AoA 2000 vs AoE2 4800; Town Center 600 vs 2400; Wonder 3000 vs 4800). Maliyetler
  de daha düşük (Wonder AoA 500w/800g/600s vs AoE2 1000/1000/1000).
- **Çağ kilidi farkları:** AoA'da Castle'ın `minAge` parametresi verilmemiş, varsayılan
  `Age.Dark`'a düşer — yani kodda Dark Age'den inşa edilebilir; AoE2'de Castle Age
  kilidi vardır. Benzer şekilde AoA'da Stable Castle çağında açılır (AoE2'de Feudal),
  Archery Range Feudal'da açılır (AoE2 ile uyumlu). Bu sapma §8'de izlenir.
- **Eksik binalar (AoE2'de var, AoA'da yok):** Siege Workshop, Bombard Tower,
  Outpost, Fish Trap, ayrı Palisade/Stone/Fortified Wall katmanları, Guard Tower/Keep
  kule yükseltme zinciri. AoA tek "Wall" + tek "Watch Tower" kullanır.
- **Wall katmanları:** AoE2'de Palisade → Stone → Fortified Wall yükseltme zinciri ve
  ayrı Palisade Gate var; AoA tek Wall (200 HP) + tek Gate (450 HP) sunar.
- **Garnizon kapasitesi:** AoA Town Center 10 / Castle 15; AoE2 Town Center 15 /
  Castle 20.
- **Dock:** AoA'da Dock mevcut (Galley üretir) ama Fish Trap/balıkçı ekonomisi yok.

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| CSTL | bug/balance | Castle `minAge` verilmemiş → Dark Age'den inşa edilebilir; Castle Age kilidi olmalı | docs/reference/03-buildings-by-age.md (Castle Age) | XS |
| SGWS | feature | Siege Workshop binası yok (Ram/Mangonel/Scorpion üretimi) | 03-buildings-by-age.md §Castle Age | L |
| BBTW | feature | Bombard Tower yok (top mermili savunma kulesi) | 03-buildings-by-age.md §Imperial | M |
| WLTR | feature | Wall katman zinciri yok: Palisade/Stone/Fortified ayrımı + Palisade Gate | 03-buildings-by-age.md §Tüm Çağlarda | M |
| TWUP | feature | Kule yükseltme zinciri yok: Watch → Guard Tower → Keep | 03-buildings-by-age.md §Castle/Imperial | M |
| OUTP | feature | Outpost (ok atmayan görüş kulesi) yok | 03-buildings-by-age.md §Dark Age | S |
| FSHT | feature | Fish Trap + balıkçı ekonomisi yok (Dock var ama su kaynağı yok) | 03-buildings-by-age.md §Feudal | M |
| MKTF | balance | Market kaynak takas oranı dalgalanması (her işlemde fiyat değişimi) doğrulanmalı | 03-buildings-by-age.md §Feudal (Market notu) | S |
