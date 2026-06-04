# Medeniyetler — AoA Wiki

> Age of Arena'da her takım, oyun başında bir **medeniyet** (`Civilization`) alır ve o
> medeniyetin pasif **stat çarpanlarını** (`CivBonus`) miras alır. AoA'da medeniyet artık
> sadece düz stat çarpanı değildir; **unique birim + civ-özel unique tech + takım (paylaşılan)
> bonusu** katmanları da eklendi. Yine de hiçbir civ için bespoke sistem kodu yok — tüm
> bonuslar data-only çarpan/erişim olarak uygulanır.
>
> **Kod kaynağı:** [CivilizationDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs) —
> `enum Civilization`, `struct CivBonus`, `struct TeamBonus`, `static CivilizationDefs.Table`.
> Bonusları tüketen sistemler: [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs),
> [GatherSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs),
> [BuildingEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs),
> [CombatSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs),
> [ResourceNode.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs),
> [TrainingQueue.cs](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs).
>
> **CIVX:** [CivilizationDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs) her
> civ'in ID + görünen ismi için **tek kanonik kaynaktır**. HUD, civ seçim ekranı ve bu wiki
> hep `CivBonus.display`'ı okur — civ ismini başka hiçbir yerde hard-code etme. Bu sayfadaki
> **her sayı koddan teyit edilmiştir.**

## 1. Ne olduğu

Medeniyet, bir takımın oyun boyunca taşıdığı kimliktir. AoA'da artık **10 oynanabilir
medeniyet** + bir nötr `None` (bonussuz, dengeli) seçeneği vardır
([CivilizationDefs.cs:11-16](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L11)):

Orijinal 5:
- **Franks** (Franklar) — ekonomi + ağır süvari + çiftlik
- **Britons** (Britanyalılar) — okçu uzmanı (unique birim: Longbowman)
- **Mongols** (Moğollar) — hızlı süvari + altın + hızlı eğitim (unique birim: Mangudai)
- **Japanese** (Japonlar) — piyade + odun ekonomisi (unique birim: Samurai)
- **Byzantines** (Bizanslılar) — savunma + iyileştirme (bina +%10 HP, heal +%50)

M9/CIVC genişlemesi (yeni 5):
- **Aztecs** (Aztekler) — yiyecek + hızlı eğitim + heal + takım bonusu (unique birim: Eagle)
- **Teutons** (Tötonlar) — savunma/piyade (bina +%15 HP, piyade +%5; unique birim: TeutonicKnight)
- **Persians** (Persler) — yiyecek + süvari (unique birim: WarElephant)
- **Vikings** (Vikingler) — okçu saldırı + odun
- **Saracens** (Saracenler) — altın + okçu saldırı

**Civ seçimi:** Artık oyuncuya **civ seçim ekranı** sunulur
([CivSelectScreen.cs:5-13](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L5)) —
`WorldRoot.Build()` sonunda kod ile kurulur, oyuncu bir civ (veya "Yok"/None) seçer, seçim
canlı olarak takım 0'a uygulanır (mevcut team-0 birimleri yeniden hesaplanır) ve
`GameBootstrap.PlayerCiv`'de saklanır ([CivSelectScreen.cs:111](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L111)).
AI takımları (1-3) hâlâ **rastgele** atanır
([WorldRoot.cs:741-744](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L741)). Atanan civ
HUD'da "Medeniyet: …" olarak gösterilir ([HUD.cs:312](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L312)).

## 2. Nasıl çalışır (mekanik + formül)

Bonuslar **data-only** çarpanlardır. Her sistem `CivilizationDefs.Get(civ)` veya
`GameManager.TeamCivBonus(teamId)` / `TeamSharedBonus(teamId)` ile struct'ları okur ve kendi
tick mantığında çarpan uygular.

**Erişim API'si** ([GameManager.cs](../../AgeOfArenaUnity/Assets/Scripts/GameManager.cs)):
```
playerCiv               → CivBonus               (oyuncunun civ'i)
TeamCivBonus(team)      → CivilizationDefs.Get(teamCivs[team])
TeamSharedBonus(team)   → o civ'in TeamBonus'u (M11 ittifaklarında paylaşılacak)
```

**Uygulama formülleri** (her biri ilgili sistemden):

- **Toplama (gather):** `etkin = baz × GatherMult(tech) × CivGatherMult` — kaynak türüne göre
  Food/Wood/Gold çarpanı ([GatherSystem.cs:124](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L124),
  [:218-228](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L218)).
- **Takım yiyecek bonusu (teamBonus):** Food depozitine `× (1 + gatherFoodBonus)` eklenir
  ([GatherSystem.cs:127](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L127)). Şu an
  yalnız Aztecs'te tanımlı (+%5).
- **Piyade saldırısı:** `atk = (baz + tech) × infantryAttackMult`, Militia & Spearman için
  ([UnitEntity.cs:157](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L157)).
- **Okçu saldırısı:** `atk = (baz + tech) × archerAttackMult`, okçu sınıfı (Archer/Longbowman/
  Skirmisher/CavalryArcher) için ([UnitEntity.cs:158](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L158)).
- **Okçu menzili:** `range = (baz + tech) + archerRangeBonus`, toplamsal (Britons)
  ([UnitEntity.cs:162+](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L162)).
- **Süvari HP:** `(baseMaxHp + tech + veterancy) × cavalryHpMult`, `RecomputeMaxHp()` içinde
  idempotent — spawn/araştırma/rütbe atlamada yeniden hesaplanır
  ([UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs)).
- **Süvari hız:** `moveSpeed × cavalrySpeedMult` (Mongols).
- **Bina HP:** `maxHp × buildingHpMult` ([BuildingEntity.cs:48-49](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L48)).
- **İyileştirme:** Medic heal `× healRateMult` ([CombatSystem.cs:224-225](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L224)).
- **Çiftlik bozunması:** boşta çiftlik decay `× farmDecayMult` ([ResourceNode.cs:61-62](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs#L61)).
- **Eğitim süresi:** `time × unitTrainTimeMult` (<1 = hızlı) ([TrainingQueue.cs:42](../../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L42)).

> **Not (M1+):** `buildingHpMult`, `healRateMult`, `farmDecayMult` eskiden ölü bonustu; artık
> hepsi tüketiliyor. Bu sayfadaki hiçbir çarpan inert değildir.

## 3. Gerçek statlar (koddan)

Tüm değerler [CivilizationDefs.cs Table](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L74)
içinden. `1.0` = bonus yok (nötr), `0` = toplamsal bonus yok. Tablo `Row(...)` yardımcısıyla
kurulur; varsayılanlar nötr, sadece geçilen alanlar override edilir
([CivilizationDefs.cs:60-72](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L60)).

| Medeniyet | food | wood | gold | cavHp | cavSpd | archRange | infAtk | bldHp | heal | farmDecay | archAtk | trainTime | teamFood | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **None** (Yok) | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | +0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:76](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L76) |
| **Franks** (Franklar) | **1.2** | 1.0 | 1.0 | **1.2** | 1.0 | +0 | 1.0 | 1.0 | 1.0 | **0.5** | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L77) |
| **Britons** (Britanyalılar) | 1.0 | **1.15** | 1.0 | 1.0 | 1.0 | **+1** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:78](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L78) |
| **Mongols** (Moğollar) | 1.0 | 1.0 | **1.1** | 1.0 | **1.25** | +0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | **0.9** | 0 | [CivilizationDefs.cs:79](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L79) |
| **Japanese** (Japonlar) | 1.0 | **1.1** | 1.0 | 1.0 | 1.0 | +0 | **1.1** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:80](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L80) |
| **Byzantines** (Bizanslılar) | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | +0 | 1.0 | **1.1** | **1.5** | 1.0 | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L81) |
| **Aztecs** (Aztekler) | **1.15** | 1.0 | 1.0 | 1.0 | 1.0 | +0 | 1.0 | 1.0 | **1.2** | 1.0 | 1.0 | **0.9** | **+0.05** | [CivilizationDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L83) |
| **Teutons** (Tötonlar) | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | +0 | **1.05** | **1.15** | 1.0 | 1.0 | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:84](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L84) |
| **Persians** (Persler) | **1.1** | 1.0 | 1.0 | **1.1** | 1.0 | +0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 0 | [CivilizationDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L85) |
| **Vikings** (Vikingler) | 1.0 | **1.1** | 1.0 | 1.0 | 1.0 | +0 | 1.0 | 1.0 | 1.0 | 1.0 | **1.1** | 1.0 | 0 | [CivilizationDefs.cs:86](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L86) |
| **Saracens** (Saracenler) | 1.0 | 1.0 | **1.15** | 1.0 | 1.0 | +0 | 1.0 | 1.0 | 1.0 | 1.0 | **1.1** | 1.0 | 0 | [CivilizationDefs.cs:87](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L87) |

`food/wood/gold` = gatherFoodMult/gatherWoodMult/gatherGoldMult, `cavHp` = cavalryHpMult,
`cavSpd` = cavalrySpeedMult, `archRange` = archerRangeBonus (toplamsal), `infAtk` =
infantryAttackMult, `bldHp` = buildingHpMult, `heal` = healRateMult, `farmDecay` =
farmDecayMult, `archAtk` = archerAttackMult, `trainTime` = unitTrainTimeMult, `teamFood` =
teamBonus.gatherFoodBonus.

## 4. Medeniyet detayları (bonus + unique birim + unique tech)

### Franks (Franklar)
- **Bonus:** yiyecek toplama **×1.2**, süvari HP **×1.2**, çiftlik bozunması **×0.5**
  ([CivilizationDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L77)).
- **Unique birim:** yok.
- **Unique tech:** **Chivalry** (Şövalyelik, Castle) → süvari +20 HP
  ([TechDefs.cs:138](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L138), etki
  [TechState.cs:102](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L102));
  **BeardedAxe** (Sakallı Balta, Imperial) → piyade +2 atk
  ([TechDefs.cs:139](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L139), etki
  [TechState.cs:41](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L41)).

### Britons (Britanyalılar)
- **Bonus:** odun toplama **×1.15**, okçu menzili **+1** (toplamsal)
  ([CivilizationDefs.cs:78](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L78)).
- **Unique birim:** **Longbowman** — Archery Range'de (Castle Age+). Eğitim listesi civ'e göre
  dallanır: `IsBritons ? ArcheryTrainablesBritons : ArcheryTrainables`
  ([BuildingEntity.cs:128-132](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L128),
  [:212](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L212),
  [:260](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L260)).
- **Unique tech:** yok.

### Mongols (Moğollar)
- **Bonus:** altın toplama **×1.1**, süvari hızı **×1.25**, eğitim süresi **×0.9** (daha hızlı)
  ([CivilizationDefs.cs:79](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L79)).
- **Unique birim:** **Mangudai** — Castle'da (`CastleUniqueFor`,
  [BuildingEntity.cs:153](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L153),
  Castle Age gating [:247](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L247)).
- **Unique tech:** yok.

### Japanese (Japonlar)
- **Bonus:** odun toplama **×1.1**, piyade saldırısı **×1.1**
  ([CivilizationDefs.cs:80](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L80)).
- **Unique birim:** **Samurai** — Castle'da
  ([BuildingEntity.cs:154](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L154)).
- **Unique tech:** yok.

### Byzantines (Bizanslılar)
- **Bonus:** bina HP **×1.1**, iyileştirme hızı **×1.5**
  ([CivilizationDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L81)).
- **Unique birim:** yok.
- **Unique tech:** yok.

### Aztecs (Aztekler)
- **Bonus:** yiyecek toplama **×1.15**, iyileştirme hızı **×1.2**, eğitim süresi **×0.9**,
  **takım bonusu:** müttefik yiyecek **+%5** (`teamBonus.gatherFoodBonus = 0.05`)
  ([CivilizationDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L83)).
- **Unique birim:** **Eagle** (Kartal Savaşçı) — Barracks'ta (Castle Age+); eğitim listesi
  `IsAztecs ? BarracksTrainablesAztec : BarracksTrainables`
  ([BuildingEntity.cs:159-164](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L159),
  [:259](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L259)).
- **Unique tech:** **EliteEagle** (Seçkin Kartal, Imperial) → Eagle can/atk +
  ([TechDefs.cs:135](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L135), etki
  [TechState.cs:71](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L71),
  [:120](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L120)).

### Teutons (Tötonlar)
- **Bonus:** piyade saldırısı **×1.05**, bina HP **×1.15**
  ([CivilizationDefs.cs:84](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L84)).
- **Unique birim:** **TeutonicKnight** — Castle'da
  ([BuildingEntity.cs:151](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L151)).
- **Unique tech:** **Ironclad** (Zırhlı, Castle) → kuşatma birimi zırhı +4
  ([TechDefs.cs:140](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L140), etki
  [TechState.cs:150](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L150));
  **Crenellations** (Mazgallar, Imperial) → kule menzili +1
  ([TechDefs.cs:141](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L141), etki
  [TechState.cs:221](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L221)).

### Persians (Persler)
- **Bonus:** yiyecek toplama **×1.1**, süvari HP **×1.1**
  ([CivilizationDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L85)).
- **Unique birim:** **WarElephant** (Savaş Fili) — Castle'da
  ([BuildingEntity.cs:152](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L152)).
- **Unique tech:** yok.

### Vikings (Vikingler)
- **Bonus:** okçu saldırısı **×1.1**, odun toplama **×1.1**
  ([CivilizationDefs.cs:86](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L86)).
- **Unique birim:** yok.
- **Unique tech:** yok.

### Saracens (Saracenler)
- **Bonus:** altın toplama **×1.15**, okçu saldırısı **×1.1**
  ([CivilizationDefs.cs:87](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L87)).
- **Unique birim:** yok.
- **Unique tech:** yok.

> **Civ-gated tech mekaniği:** `TechDef.requiredCiv` ile bir tech yalnızca o civ'e açılır
> (`None` = herkese). Liste `TechDefs.Get(...)` içinde `requiredCiv` filtresinden geçer
> ([TechDefs.cs:18](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L18),
> [:159-168](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L159)). Castle unique tech'ler
> Castle/Imperial çağında, Castle binasında araştırılır.

## 5. Strateji & counter

- **Franks:** Erken ekonomi (+%20 yiyecek, yarı çiftlik bozunması) + güçlü süvari (+%20 HP,
  Chivalry ile +20 daha). Counter: Spearman/Camel (anti-cavalry çarpanı) — HP bonusu 3× hasara
  yetmez.
- **Britons:** +1 menzilli okçu + Longbowman; kite kompozisyonunda çok güçlü. Counter: hızlı
  süvari (Mongols/Scout) ile menzili kapat.
- **Mongols:** +%25 süvari hızı (raid/kaçış), +%10 altın, +hızlı eğitim, Mangudai mobil okçu.
  Counter: Spearman duvarı + sabit savunma.
- **Japanese:** +%10 piyade atk + Samurai yakın dövüş baskısı, +%10 odun spam. Counter: okçu kite.
- **Byzantines:** Savunma/heal civ'i — bina +%10 HP, Medic heal +%50 artık gerçekten etkili.
  Uzatmalı savunma oyunu için iyi.
- **Aztecs:** Yiyecek + hızlı eğitim + Eagle (anti-okçu/anti-kuşatma mobil piyade); takım
  oyununda müttefik yiyecek bonusu. Erken birim spam'i güçlü.
- **Teutons:** En sağlam savunma (bina +%15 HP), TeutonicKnight ağır piyade, Crenellations ile
  kule menzili. Yavaş ama kırılması zor.
- **Persians:** Yiyecek + süvari + WarElephant (yüksek HP kuşatma kırıcı). Counter: Camel/Spearman
  ve menzilli birimlerle kite.
- **Vikings/Saracens:** Okçu saldırı +%10 ile okçu-yoğun kompozisyon; Vikings odun, Saracens
  altın ekonomisi destekler. Counter: Skirmisher/süvari ile okçu hattına bas.

**Genel counter mantığı:** Civ bonusları stat counter ilişkilerini (Spearman→Cavalry,
Skirmisher→Archer) **değiştirmez**, sadece ölçekler. Bkz. [07-combat-counters.md](./07-combat-counters.md).

## 6. Çapraz bağlantılar

- [02-units.md](./02-units.md) — civ çarpanlarının uygulandığı baz birim statları + unique
  birimler (Longbowman, Eagle, TeutonicKnight, WarElephant, Mangudai, Samurai).
- [03-unit-upgrades.md](./03-unit-upgrades.md) — tech bonusları civ çarpanından **önce** eklenir.
- [04-buildings.md](./04-buildings.md) — Archery Range / Barracks / Castle eğitim listesi
  dallanmaları.
- [05-tech-tree.md](./05-tech-tree.md) — civ-gated unique tech'ler (Chivalry/BeardedAxe/Ironclad/
  Crenellations/EliteEagle).
- [07-combat-counters.md](./07-combat-counters.md) — civ bonuslarının counter matrisindeki yeri.
- [08-economy-trade.md](./08-economy-trade.md) — gather çarpanları + takım yiyecek bonusu.

## 7. AoE2 farkı (reference köprü)

Tam AoE2 medeniyet sistemi için: [docs/reference/01-civilizations.md](../reference/01-civilizations.md).
Özet farklar:

- **Sayı:** AoE2 DE'de **45 medeniyet**; AoA'da artık **10** (+None) — eski 5'ten genişledi
  (M9/CIVC).
- **Yapı:** AoA artık AoE2'ye yaklaşan bir paket sunuyor: çoğu civ'in **unique birimi**
  (6 civ), bazılarının **civ-gated unique tech'i** (Franks, Teutons, Aztecs) ve bir civ'in
  **takım bonusu** (Aztecs) var. Yine de bonus çeşitliliği AoE2'den dardır.
- **Takım bonusu:** AoE2'de her civ'in takıma yayılan bonusu var; AoA'da yalnız Aztecs'te
  (+%5 yiyecek) — ittifaklar (M11) ile paylaşılacak.
- **Unique birim eşleşmeleri:** Britons→Longbowman, Aztecs→Eagle, Teutons→TeutonicKnight,
  Persians→WarElephant, Mongols→Mangudai, Japanese→Samurai — hepsi AoE2 ile **kavramsal uyumlu**.

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| CIVM | feature | Takım bonusu yalnız Aztecs'te tanımlı; ittifak (M11) gelene kadar yalnız sahibinin takımına etki eder | AoE2: her civ'in takım bonusu var [reference:12](../reference/01-civilizations.md) | M |
| CIVU | feature | 4 civ (Franks/Byzantines/Vikings/Saracens) unique birimsiz | Her civ 1-3 unique birim [reference:9](../reference/01-civilizations.md) | L |
| CIVT | feature | Unique tech yalnız 3 civ'de (Franks/Teutons/Aztecs) | Her civ 2 unique tech [reference:11](../reference/01-civilizations.md) | M |
| CIVC | feature | 10 civ var; AoE2'de 45 | 45 medeniyet [reference:3](../reference/01-civilizations.md) | L |
| CIVD | balance | Civ kimlikleri hâlâ görece dar (çoğu 2 düz çarpan); mimari stil/arketip yok | AoE2 çeşitliliği [reference:14](../reference/01-civilizations.md) | M |
