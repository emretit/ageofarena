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
sayıları okur. `BuildingType` enum'u 22 bina tipi tanımlar
([GameTypes.cs:47](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L47)).

Binalar fonksiyonel olarak şu gruplara ayrılır:

- **Merkez & nüfus:** Town Center, House
- **Askeri üretim:** Barracks, Archery Range, Stable, Castle, Monastery, Dock, Siege Workshop
- **Ekonomi & drop-off:** Town Center, Mill, Lumber Camp, Mining Camp, Farm, Market, Dock
- **Teknoloji:** Blacksmith, Monastery, University
- **Savunma:** Castle, Watch Tower, Bombard Tower, Outpost, Wall, Gate
- **Zafer:** Wonder (Anıt)

> **Taş ekonomisi aktif (M8/STONE):** Stone artık 200 başlangıç değeriyle gerçek bir
> kaynak ([ResourceManager.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L14)).
> Castle (650), University (150), Bombard Tower (100), Wonder (600) ve Outpost (5) taş
> harcar — yani savunma/teknoloji binaları için Mining Camp'te taş madenciliği şart.
> (CLAUDE.md'deki "stone = 0 başlar" notu eskimiştir; kod 200'e geçti.)

Garnizon, dost bir binanın içine birim sığınması mekaniğidir: birimler gizlenir,
iyileşir ve binanın savunma ateşine ek ok katar. Yalnızca `garrisonCapacity > 0`
olan binalar (Town Center, Castle) garnizon kabul eder.

## 2. Nasıl çalışır (mekanik + formül)

### Maliyet, inşa, nüfus, HP
Her bina `BuildingDef` struct'ı ile tanımlanır: `food/wood/gold/stone`, `buildTime`
(saniye), `popProvided` (nüfus kapasitesi katkısı), `maxHp`, `hotkey`, `buildable`,
`minAge`. HP `BuildingEntity.Start()` içinde `MaxHpFor(type)` ile tablodan çekilir
(Byzantines `buildingHpMult` ve University Architecture HP çarpanı uygulanır).

### Zırh ve hasar
`BuildingEntity.TakeDamage(amount, damageType)` her bina için ayrı `meleeArmor` ve
`pierceArmor` uygular. Formül:

```
hasar = max(1, amount - armor)
```

`armor` saldırının `DamageType`'ına göre seçilir:
- `DamageType.Melee` → `meleeArmor` (+ University tech armor)
- `DamageType.Pierce` → `pierceArmor` (+ University tech armor)
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
`def.attackRange > 0` olan binalar (Castle, Watch Tower, Bombard Tower) menzildeki en
yakın düşman birime cooldown ile mermi atar. Pasif bina (Town Center) yalnızca
**içinde garnizon varken** `GarrisonRange = 8` menzille ateş eder.

- Castle/Watch Tower oku: `def.attackDamage`, `DamageType.Pierce`
- Bombard Tower mermisi: `def.attackDamage`, `DamageType.Siege` (zırh baypas — duvar/bina katili)
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
| Town Center | 0 | 0 | 0 | 0 | 60 | +5 | 600 | Dark | hayır | [BuildingDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L69) |
| House | 0 | 30 | 0 | 0 | 12 | +5 | 300 | Dark | evet | [BuildingDefs.cs:70](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L70) |
| Barracks | 0 | 120 | 0 | 0 | 25 | 0 | 400 | Dark | evet | [BuildingDefs.cs:71](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L71) |
| Archery Range | 0 | 120 | 0 | 0 | 25 | 0 | 400 | Feudal | evet | [BuildingDefs.cs:72](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L72) |
| Stable | 0 | 120 | 0 | 0 | 25 | 0 | 400 | Castle | evet | [BuildingDefs.cs:73](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L73) |
| Farm | 0 | 60 | 0 | 0 | 12 | 0 | 200 | Dark | evet | [BuildingDefs.cs:74](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L74) |
| Lumber Camp | 0 | 50 | 0 | 0 | 10 | 0 | 150 | Dark | evet | [BuildingDefs.cs:75](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L75) |
| Mining Camp | 0 | 50 | 0 | 0 | 10 | 0 | 150 | Dark | evet | [BuildingDefs.cs:76](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L76) |
| Mill | 0 | 60 | 0 | 0 | 12 | 0 | 150 | Dark | evet | [BuildingDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L77) |
| Market | 0 | 175 | 0 | 0 | 25 | 0 | 350 | Dark | evet | [BuildingDefs.cs:78](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L78) |
| Castle | 0 | 0 | 0 | **650** | 80 | +10 | 2000 | Castle | evet | [BuildingDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L81) |
| Wall | 0 | 10 | 0 | 0 | 4 | 0 | 200 | Dark | evet | [BuildingDefs.cs:84](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L84) |
| Gate | 0 | 30 | 0 | 0 | 8 | 0 | 450 | Dark | evet | [BuildingDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L85) |
| Wonder (Anıt) | 0 | 500 | 800 | **600** | 150 | 0 | 3000 | Imperial | evet | [BuildingDefs.cs:87](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L87) |
| Watch Tower (Gözetleme Kulesi) | 0 | 125 | 0 | 0 | 18 | 0 | 500 | Feudal | evet | [BuildingDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L89) |
| Blacksmith (Demirci) | 0 | 150 | 0 | 0 | 20 | 0 | 350 | Feudal | evet | [BuildingDefs.cs:91](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L91) |
| Monastery (Manastır) | 0 | 175 | 0 | 0 | 22 | 0 | 350 | Castle | evet | [BuildingDefs.cs:93](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L93) |
| University (Üniversite) | 0 | 200 | 0 | **150** | 28 | 0 | 400 | Castle | evet | [BuildingDefs.cs:95](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L95) |
| Dock (Liman) | 0 | 150 | 0 | 0 | 25 | 0 | 300 | Dark | evet | [BuildingDefs.cs:97](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L97) |
| Siege Workshop (Kuşatma Atölyesi) | 0 | 200 | 0 | 0 | 28 | 0 | 400 | Castle | evet | [BuildingDefs.cs:99](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L99) |
| Outpost (Gözcü Kulesi) | 0 | 25 | 0 | **5** | 10 | 0 | 200 | Dark | evet | [BuildingDefs.cs:101](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L101) |
| Bombard Tower (Bombard Kulesi) | 0 | 125 | 0 | **100** | 24 | 0 | 600 | Imperial | evet | [BuildingDefs.cs:103](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L103) |

> **Taş harcayan binalar (kalın `s` sütunu):** Castle 650, Wonder 600, University 150,
> Bombard Tower 100, Outpost 5. Bu binaları kurmak için Mining Camp'te taş madenciliği
> gerekir.
>
> Not: Castle artık `minAge: Age.Castle` ile gerçekten Castle Age'de kilitlidir
> ([BuildingDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L81)) — eski
> "Dark'a düşer" sapması düzeltilmiştir, AoE2 ile uyumludur.

### 3.2 Zırh (melee / pierce)

> Varsayılan zırh `BuildingDef` ctor'unda `meleeArm: 1`, `pierceArm: 3`
> ([BuildingDefs.cs:42](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L42)).
> Aşağıdaki tabloda yalnızca varsayılandan farklı tanımlananlar açık değerle, geri
> kalanlar varsayılanla gösterilir.

| Bina | Melee zırh | Pierce zırh | Kaynak |
|---|---|---|---|
| Town Center | 3 | 5 | [BuildingDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L69) |
| Castle | 8 | 8 | [BuildingDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L81) |
| Wall | 10 | 10 | [BuildingDefs.cs:84](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L84) |
| Wonder | 5 | 8 | [BuildingDefs.cs:87](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L87) |
| Watch Tower | 2 | 4 | [BuildingDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L89) |
| Bombard Tower | 3 | 6 | [BuildingDefs.cs:103](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L103) |
| Outpost | 1 (varsayılan) | 3 (varsayılan) | [BuildingDefs.cs:101](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L101) |
| Diğer tüm binalar | 1 (varsayılan) | 3 (varsayılan) | [BuildingDefs.cs:42](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L42) |

> Kuşatma (`DamageType.Siege`) hasarı zırhı tamamen baypas eder
> ([BuildingEntity.cs:67](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L67)).

### 3.3 Savunma ateşi & garnizon kapasitesi

| Bina | Atk menzil | Atk hasar | Hasar tipi | Atk aralık (s) | Garnizon kap. | Kaynak |
|---|---|---|---|---|---|---|
| Castle | 9 | 18 | Pierce | 1.5 | 15 | [BuildingDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L81) |
| Bombard Tower | 8 | 30 | **Siege** | 2.0 | 0 | [BuildingDefs.cs:103](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L103) |
| Watch Tower | 6 | 7 | Pierce | 2.0 | 0 | [BuildingDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L89) |
| Town Center | 0 (pasif) | — | — | — | 10 | [BuildingDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L69) |
| Outpost | 0 (ateş yok, görüş) | — | — | — | 0 | [BuildingDefs.cs:101](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L101) |
| Diğer tüm binalar | 0 | — | — | — | 0 | [BuildingDefs.cs:66-103](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L66) |

> **Bombard Tower** mermisi `DamageType.Siege` olduğu için **bina/duvar zırhını baypas
> eder** — Watch Tower'ın ~4 katı hasar (30 vs 7) ile yapı katili savunma kulesidir
> ([BuildingDefs.cs:103](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L103)).
> **Outpost** ateş etmez (`attackRange = 0`); yalnızca ucuz görüş/keşif kulesidir.

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
| Town Center | Food + Wood + Gold + Stone | MaskAll | [BuildingDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L69) |
| Lumber Camp | Wood | MaskWood | [BuildingDefs.cs:75](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L75) |
| Mining Camp | Gold + Stone | MaskMine | [BuildingDefs.cs:76](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L76) |
| Mill | Food | MaskFood | [BuildingDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L77) |
| Dock | Food | MaskFood | [BuildingDefs.cs:97](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L97) |

### 3.5 Üretim binaları (eğitilebilen birimler)

> Birim statları için bkz. [Birimler](./02-units.md). Çağ kilitleri
> `BuildingEntity.MinAgeFor`
> ([BuildingEntity.cs:227-251](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L227))
> ile filtrelenir; `(Feudal+)` / `(Castle+)` etiketi bu switch'te açıkça listelenen
> kilidi gösterir, etiketsiz birimler Dark Age'den itibaren eğitilebilir
> (switch'te yoksa `_ => Age.Dark`).

| Bina | Eğitilebilen birimler | Kaynak |
|---|---|---|
| Town Center | Villager | [BuildingEntity.cs:109-112](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L109) |
| Barracks | Militia, Spearman (Feudal+), Scout | [BuildingEntity.cs:114-119](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L114) |
| Barracks (Aztecs) | Militia, Spearman (Feudal+), Scout, **Eagle (Castle+)** | [BuildingEntity.cs:159-165](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L159) |
| Archery Range | Archer (Feudal+), Skirmisher (Feudal+) | [BuildingEntity.cs:121-125](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L121) |
| Archery Range (Britons) | Archer (Feudal+), Skirmisher (Feudal+), **Longbowman (Castle+)** | [BuildingEntity.cs:128-133](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L128) |
| Stable | Cavalry (Castle+), Camel (Castle+), CavalryArcher (Castle+) | [BuildingEntity.cs:135-140](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L135) |
| Castle | Trebuchet (Castle+), Medic (Castle+) + civ-özel birim | [BuildingEntity.cs:142-146](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L142) |
| Monastery | Monk (Castle+) | [BuildingEntity.cs:167-170](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L167) |
| Market | Trade Cart | [BuildingEntity.cs:172-175](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L172) |
| Dock | Galley (Feudal+), FireShip (Feudal+), DemoShip (Castle+) | [BuildingEntity.cs:177-182](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L177) |
| Siege Workshop | Ram (Castle+), Mangonel (Castle+) | [BuildingEntity.cs:184-188](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L184) |
| Outpost / Watch Tower / Bombard Tower / Blacksmith / University / Wall / Gate / Wonder | (üretim yok — savunma/teknoloji/ekonomi) | [BuildingEntity.cs:253-268](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L253) |

#### Medeniyete bağlı (civ-koşullu) üretim

Üretim listesi takımın medeniyetine göre değişir
([BuildingEntity.cs:253-268](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L253)):

| Medeniyet | Bina | Eklenen özel birim | Kaynak |
|---|---|---|---|
| **Britons** | Archery Range | Longbowman (Castle+) | [BuildingEntity.cs:128-133](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L128) |
| **Aztecs** | Barracks | Eagle (Castle+) | [BuildingEntity.cs:159-165](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L159) |
| **Teutons** | Castle | Teutonic Knight (Castle+) | [BuildingEntity.cs:151](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L151) |
| **Persians** | Castle | War Elephant (Castle+) | [BuildingEntity.cs:152](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L152) |
| **Mongols** | Castle | Mangudai (Castle+) | [BuildingEntity.cs:153](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L153) |
| **Japanese** | Castle | Samurai (Castle+) | [BuildingEntity.cs:154](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L154) |

Castle'a eklenen civ-özel birim `CastleUniqueFor(civ)` ile seçilir ve standart Castle
birimlerinin (Trebuchet, Medic) sonuna eklenir
([BuildingEntity.cs:149-156](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L149),
[BuildingEntity.cs:216-224](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L216)).
Medeniyet ayrıntıları için bkz. [Medeniyetler](./06-civilizations.md).

> Önemli: Scout (`UnitType.Scout`) `MinAgeFor` switch'inde **yer almaz**
> ([BuildingEntity.cs:227-251](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L227)),
> bu yüzden `_ => Age.Dark`'a düşer — yani Scout Dark Age'den itibaren eğitilebilir,
> Militia da kilitsizdir; Spearman Feudal+ kilitlidir.
>
> Teknoloji araştırma binaları (Blacksmith, University) ve Monastery ek olarak
> `GetResearchables()` ile teknoloji sunar; ayrıntı için bkz.
> [Teknoloji Ağacı](./05-tech-tree.md).

## 4. Strateji & counter

- **Drop-off optimizasyonu:** Lumber/Mining Camp'i kaynağa yakın kurmak villager
  yürüme süresini kısaltır. Town Center her kaynağı kabul eder ama genelde merkezde
  olduğundan kenar kaynaklar için ayrı kamp şarttır. Mill, Farm yiyeceğini toplar.
- **Taş ekonomisi:** Castle/University/Bombard Tower/Wonder taş gerektirir; erken
  Mining Camp + taş madencisi atamadan bu binalara geçilemez.
- **Savunma katmanları:** Watch Tower (atk 7, Feudal) erken-oyun ucuz savunma; Castle
  (atk 18, menzil 9, zırh 8/8, 2000 HP) ana savunma; Bombard Tower (atk 30 **Siege**,
  Imperial) duvar/bina katili — ama hepsi kuşatmaya karşı zayıftır (Castle/Watch Tower
  kendi zırhı taş hasarını durduramaz). Outpost ateş etmez, yalnızca görüş sağlar.
- **Garnizon savunması:** Saldırı altında villager/okçuları Town Center veya Castle'a
  garnizonlamak hem onları iyileştirir hem ek ok sağlar (birim başına 6 hasar, en fazla
  5 ek ok). Town Center yalnızca **garnizonluyken** ateş açar — boşken pasiftir.
- **Garnizon riski:** Bina yıkılırsa içindeki tüm birimler ölür. Kale düşmek üzereyse
  garnizonu boşalt.
- **Wall + Gate:** Wall (200 HP, zırh 10/10) yol bulmayı keser; Gate (450 HP) geçit
  açar. İkisi de kuşatmaya karşı zayıf — duvar arkasını Castle/Bombard Tower ile destekle.
- **Üretim çeşitliliği:** Siege Workshop (Ram/Mangonel) kuşatma kolu, Dock
  (Galley/FireShip/DemoShip) deniz kolu açar. Medeniyet seçimi özel birimleri belirler
  (Britons→Longbowman, Aztecs→Eagle, Castle civ-birimleri).
- **Counter:** Binalara karşı en verimli birimler kuşatma sınıfı (Trebuchet, Ram,
  Mangonel — `Siege` hasar). Düz melee/pierce birimleri yüksek zırhlı binalarda
  (Castle, Wall) çok yavaş ilerler.

## 5. Çapraz bağlantılar

- [Oyun Akışı & Çağlar](./01-game-flow-ages.md) — çağ kilitleri ve ilerleme
- [Birimler](./02-units.md) — bu binaların eğittiği birimlerin statları
- [Birim Yükseltme Zincirleri](./03-unit-upgrades.md) — Blacksmith yükseltmeleri
- [Teknoloji Ağacı](./05-tech-tree.md) — Blacksmith/University/Monastery araştırmaları
- [Medeniyetler](./06-civilizations.md) — Britons/Aztecs/Castle civ-özel birimleri
- [Savaş & Counter Sistemi](./07-combat-counters.md) — zırh/hasar tipi etkileşimi
- [Ekonomi & Ticaret](./08-economy-trade.md) — drop-off ve Market/Trade Cart
- [Zafer Koşulları](./10-victory-objectives.md) — Wonder (Anıt) zafer binası
- [Kontroller & UI](./11-controls-ui-feedback.md) — inşa menüsü, hotkey, rally point

## 6. Kod referansları (file:line, derivation)

- **Tek doğruluk tablosu** — `BuildingDefs.Table`
  ([BuildingDefs.cs:66-104](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L66)):
  her binanın maliyet/HP/zırh/çağ/drop-off/atk/garnizon değerleri (22 bina).
- **`BuildingType` enum** — 22 bina tipi
  ([GameTypes.cs:47](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L47)).
- **Varsayılan zırh** — ctor `meleeArm: 1f, pierceArm: 3f`
  ([BuildingDefs.cs:42](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L42)).
- **HP/zırh ataması** — `BuildingEntity.Start()`
  ([BuildingEntity.cs:45-56](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L45)):
  `maxHp = MaxHpFor(type)`, civ/tech HP çarpanı, zırh tablodan kopyalanır.
- **Hasar formülü** — `TakeDamage` `hp -= max(1, amount - armor)`; Siege → armor 0
  ([BuildingEntity.cs:67-83](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L67)).
- **Drop-off maskeleri** — `MaskAll/Wood/Mine/Food`
  ([BuildingDefs.cs:60-64](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L60)),
  kontrol `AcceptsDropoff`
  ([BuildingDefs.cs:123-127](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L123)).
- **Çağ kilidi** — `UnlockedAt`
  ([BuildingDefs.cs:117](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L117)).
- **Eğitilebilen birimler & filtre** — `GetTrainables` + `MinAgeFor`
  ([BuildingEntity.cs:253-281](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L253),
  [BuildingEntity.cs:227-251](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L227)).
  Scout switch'te yok → `_ => Age.Dark` (kilitsiz).
- **Civ-koşullu üretim** — `CastleUniqueFor` + Aztecs/Britons dalları
  ([BuildingEntity.cs:149-156](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L149),
  [BuildingEntity.cs:259-260](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L259)).
- **Savunma ateşi** — `BuildingCombatSystem.Tick`
  ([BuildingCombatSystem.cs:23-52](../../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L23)):
  menzil seçimi, mermi spawn, garnizon ek oku, cooldown.
- **Garnizon yaşam döngüsü** — `GarrisonSystem.Tick/UngarrisonAll/OnBuildingDestroyed`
  ([GarrisonSystem.cs:17-74](../../AgeOfArenaUnity/Assets/Scripts/GarrisonSystem.cs#L17)).
- **Garnizon kapasite/durum** — `BuildingEntity.GarrisonCapacity/HasGarrisonSpace`
  ([BuildingEntity.cs:36-38](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L36)).

## 7. AoE2 farkı (reference köprü)

Tam karşılaştırma: [docs/reference/03-buildings-by-age.md](../reference/03-buildings-by-age.md).

- **Ölçek:** AoA HP değerleri AoE2'nin "dikey dilim" küçültülmüş hâlidir (Castle
  AoA 2000 vs AoE2 4800; Town Center 600 vs 2400; Wonder 3000 vs 4800). Maliyetler
  de daha düşük (Wonder AoA 500w/800g/600s vs AoE2 1000/1000/1000).
- **Çağ kilidi:** AoA'da Castle artık `Age.Castle` kilitli (AoE2 ile uyumlu, eski Dark
  sapması düzeltildi). Stable AoA'da Castle'da açılır (AoE2'de Feudal); Archery Range
  Feudal'da açılır (AoE2 ile uyumlu). Bombard Tower Imperial (AoE2 ile uyumlu).
- **Artık eklenmiş binalar:** Siege Workshop (Ram/Mangonel), Bombard Tower (Siege
  kule), Outpost (ateşsiz görüş kulesi), Dock (Galley/FireShip/DemoShip) artık kodda
  mevcut — eski wiki'deki "eksik" notu güncellendi.
- **Hâlâ eksik:** Wall katman zinciri (Palisade → Stone → Fortified + Palisade Gate),
  Watch → Guard Tower → Keep kule yükseltme zinciri, Fish Trap/balıkçı ekonomisi
  (Dock var ama su kaynağı yok). AoA tek "Wall" (200 HP) + tek "Gate" (450 HP) kullanır.
- **Garnizon kapasitesi:** AoA Town Center 10 / Castle 15; AoE2 Town Center 15 /
  Castle 20.

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| WLTR | feature | Wall katman zinciri yok: Palisade/Stone/Fortified ayrımı + Palisade Gate | 03-buildings-by-age.md §Tüm Çağlarda | M |
| TWUP | feature | Kule yükseltme zinciri yok: Watch → Guard Tower → Keep (GuardTower/Keep TechType var ama bina dönüşümü yok) | 03-buildings-by-age.md §Castle/Imperial | M |
| FSHT | feature | Fish Trap + balıkçı ekonomisi yok (Dock var ama su kaynağı/balık yok) | 03-buildings-by-age.md §Feudal | M |
| MKTF | balance | Market kaynak takas oranı dalgalanması (her işlemde fiyat değişimi) doğrulanmalı | 03-buildings-by-age.md §Feudal (Market notu) | S |
| SCRP | feature | Siege Workshop'ta Scorpion + Trebuchet ayrımı yok (Trebuchet Castle'da üretiliyor) | 03-buildings-by-age.md §Castle Age | S |
