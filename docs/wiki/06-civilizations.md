# Medeniyetler — AoA Wiki

> Age of Arena'da her takım, oyun başında bir **medeniyet** (`Civilization`) alır ve o
> medeniyetin pasif **stat çarpanlarını** (`CivBonus`) miras alır. AoE2'den farklı olarak
> AoA'da medeniyetler **unique birim + unique tech + takım bonusu** paketi değildir; bonuslar
> çoğunlukla doğrudan stat çarpanı olarak uygulanır (tek istisna: Britons → Longbowman erişimi).
>
> **Kod kaynağı:** [CivilizationDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs) —
> `enum Civilization`, `struct CivBonus`, `static CivilizationDefs.Table`. Bonusları tüketen
> sistemler: [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs),
> [GatherSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs),
> [BuildingEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs).
>
> Bu sayfadaki **her sayı koddan teyit edilmiştir**; kodda tanımlı olmayan değerler açıkça
> "kodda tanımlı değil" olarak işaretlenir.

## 1. Ne olduğu

Medeniyet, bir takımın oyun boyunca taşıdığı kimliktir. AoA'da **5 oynanabilir medeniyet** +
bir nötr `None` (bonussuz, dengeli) seçeneği vardır:

- **Franks** (Franklar) — ekonomi + ağır süvari
- **Britons** (Britanyalılar) — okçu uzmanı (tek gerçek unique birim sahibi: Longbowman)
- **Mongols** (Moğollar) — hızlı süvari + altın ekonomisi
- **Japanese** (Japonlar) — piyade + odun ekonomisi
- **Byzantines** (Bizanslılar) — savunma + iyileştirme (bina +%10 HP, heal +%50; M1'de aktif)

Medeniyetler **`SetupGameplay`** içinde 4 takımın hepsine **rastgele** atanır
(`Civilization.None` atlanır) — yani oyuncu civ seçim ekranı henüz yoktur, oyuncu da rastgele
bir civ ile başlar ([WorldRoot.cs:625-628](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L625)).
Atanan civ HUD'da "Medeniyet: …" olarak gösterilir
([HUD.cs:300](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L300)).

## 2. Nasıl çalışır (mekanik + formül)

Bonuslar **data-only** çarpanlardır. Her sistem `CivilizationDefs.Get(civ)` veya
`GameManager.TeamCivBonus(teamId)` ile `CivBonus` struct'ını okur ve kendi tick mantığında
çarpan uygular. Hiçbir civ için özel sistem kodu yoktur (Britons Longbowman erişimi hariç).

**Erişim API'si** ([GameManager.cs:66-69](../../AgeOfArenaUnity/Assets/Scripts/GameManager.cs#L66)):
```
playerCiv          → CivBonus           (oyuncunun civ'i)
TeamCivBonus(team) → CivilizationDefs.Get(teamCivs[team])
```

**Uygulama formülleri** (her biri ilgili sistemden):

- **Toplama (gather):** `etkin_toplama = baz_toplama × gatherXMult`
  — kaynak türüne göre Food/Wood/Gold çarpanı seçilir
  ([GatherSystem.cs:195-201](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L195)).
- **Piyade saldırısı:** `atk = (baz + tech) × infantryAttackMult`, sadece Militia & Spearman için
  ([UnitEntity.cs:117](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L117)).
- **Okçu menzili:** `range = (baz + tech) + archerRangeBonus`, sadece Archer & Longbowman için
  (toplamsal, çarpansal değil) ([UnitEntity.cs:127](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L127)).
- **Süvari HP:** Cavalry için `cavalryHpMult`, `RecomputeMaxHp()` içinde `baseMaxHp`'den
  türetilir (`computed = (baseMaxHp + tech + veterancy) * cavalryHpMult`) — spawn, araştırma ve
  rütbe atlamada yeniden hesaplanır, idempotenttir (çift-çarpma yok)
  ([UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs)). (M1'de düzeltildi: eski
  tek-seferlik `Start()` çarpımı kaldırıldı.)
- **Süvari hız:** `moveSpeed *= cavalrySpeedMult` `Start()`'ta base'den bir kez (civ takım başına
  sabit olduğu için hız tech ile değişmez).
- **Britons Longbowman erişimi:** Archery Range'in eğitim listesi civ'e göre dallanır —
  `IsBritons ? ArcheryTrainablesBritons : ArcheryTrainables`
  ([BuildingEntity.cs:172](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L172),
  [:195](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L195)). Longbowman Castle Age+'ta açılır.

**(M1'de düzeltildi — artık aktif):** `buildingHpMult`, `healRateMult`, `farmDecayMult` artık
tüketiliyor:
- `buildingHpMult` → `BuildingEntity.Start` (Byzantines bina maxHp ×1.1)
- `healRateMult` → `CombatSystem.StepHeal` (Byzantines Medic heal ×1.5)
- `farmDecayMult` → `ResourceNode` decay (Franks çiftlik bozunması ×0.5)

Eskiden bu üç çarpan tanımlı ama hiçbir sistemce okunmuyordu (ölü bonus); M1 milestone'da
canlandırıldı.

## 3. Gerçek statlar (koddan)

Tüm değerler [CivilizationDefs.cs Table](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L33)
içinden. `1.0` = bonus yok (nötr), `0` = toplamsal bonus yok.

| Medeniyet | gatherFood | gatherWood | gatherGold | cavalryHp | cavalrySpeed | archerRange | infantryAtk | buildingHp¹ | healRate¹ | farmDecay¹ | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **None** (Yok) | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | +0 | 1.0 | 1.0 | 1.0 | 1.0 | [CivilizationDefs.cs:35](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L35) |
| **Franks** (Franklar) | **1.2** | 1.0 | 1.0 | **1.2** | 1.0 | +0 | 1.0 | 1.0 | 1.0 | **0.5** | [CivilizationDefs.cs:40](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L40) |
| **Britons** (Britanyalılar) | 1.0 | **1.15** | 1.0 | 1.0 | 1.0 | **+1** | 1.0 | 1.0 | 1.0 | 1.0 | [CivilizationDefs.cs:45](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L45) |
| **Mongols** (Moğollar) | 1.0 | 1.0 | **1.1** | 1.0 | **1.25** | +0 | 1.0 | 1.0 | 1.0 | 1.0 | [CivilizationDefs.cs:51](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L51) |
| **Japanese** (Japonlar) | 1.0 | **1.1** | 1.0 | 1.0 | 1.0 | +0 | **1.1** | 1.0 | 1.0 | 1.0 | [CivilizationDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L57) |
| **Byzantines** (Bizanslılar) | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | +0 | 1.0 | **1.1** | **1.5** | 1.0 | [CivilizationDefs.cs:63](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L63) |

¹ **(M1'de aktifleştirildi):** `buildingHpMult` (BuildingEntity.Start), `healRateMult`
(CombatSystem.StepHeal), `farmDecayMult` (ResourceNode decay) artık tüketiliyor. Franks 0.5 /
Byzantines 1.1 & 1.5 değerleri oyunda gerçek etki yapar (bkz. §6).

**Türetilmiş etkiler (koddan, örnek):**

- Franks süvarisi: baz Cavalry HP × 1.2. (Cavalry baz statları için bkz. [02-units.md](./02-units.md).)
- Britons okçusu: Archer menzili `6.5 + tech + 1`, Longbowman menzili `8.5 + tech + 1`
  (baz menziller [UnitEntity.cs:103-109](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L103)).
- Mongols süvarisi: baz `moveSpeed × 1.25`.
- Japanese Militia/Spearman: `(baz_atk + tech) × 1.1` (Militia baz 5, Spearman baz 4 →
  [UnitEntity.cs:94-102](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L94)).

## 4. Strateji & counter

Atama rastgele olduğu için (oyuncu seçemez), bu bölüm "elinde X civ varsa nasıl oynarsın"
mantığındadır.

- **Franks:** En güçlü pratik civ. +%20 yiyecek toplama erken çağ ekonomisini hızlandırır,
  +%20 süvari HP'si Castle Age push'unu güçlendirir. Counter: Spearman (`AntiCavalryMultiplier`
  = 3× vs Cavalry, [UnitEntity.cs:150](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L150)) —
  HP bonusu Spearman 3× hasarına karşı yetmez.
- **Britons:** Tek unique birim sahibi (Longbowman, Castle Age+ Archery Range'de). +1 menzil
  okçu kompozisyonunu kite-etmede çok güçlü. Counter: hızlı süvari/Scout ile menzili kapat,
  veya Mongols hız bonusuyla yaklaş.
- **Mongols:** +%25 süvari hızı raid ve kaçış için en iyi mobilite; +%10 altın altın-yoğun
  birimlere (Cavalry/Longbowman) destek. Counter: Spearman duvarı + sabit savunma.
- **Japanese:** +%10 piyade saldırısı (Militia/Spearman) yakın dövüş baskısı; +%10 odun bina/okçu
  spam'ini destekler. Counter: okçu kite (piyade menzilsiz).
- **Byzantines:** Tasarımda savunma/iyileştirme civ'i; ancak **bina HP ve heal bonusu şu an
  inert** olduğundan pratikte **bonussuz `None` ile eşdeğer** oynar. En zayıf pratik seçenek.

**Genel counter mantığı:** Civ bonusları stat counter ilişkilerini (Spearman→Cavalry,
Archer→Infantry) **değiştirmez**, sadece ölçekler. Bkz. [07-combat-counters.md](./07-combat-counters.md).

## 5. Çapraz bağlantılar

- [02-units.md](./02-units.md) — civ çarpanlarının uygulandığı baz birim statları (Cavalry,
  Archer, Militia, Spearman, Longbowman).
- [03-unit-upgrades.md](./03-unit-upgrades.md) — tech bonusları civ çarpanından **önce** eklenir
  (`(baz + tech) × civ`).
- [04-buildings.md](./04-buildings.md) — Britons Archery Range eğitim listesi dallanması.
- [05-tech-tree.md](./05-tech-tree.md) — `TeamTech.AttackBonus/RangeBonus/HpBonus` ile civ
  çarpanlarının etkileşimi.
- [07-combat-counters.md](./07-combat-counters.md) — civ bonuslarının counter matrisindeki yeri.
- [08-economy-trade.md](./08-economy-trade.md) — gather çarpanlarının ekonomi etkisi.
- [01-game-flow-ages.md](./01-game-flow-ages.md) — Longbowman/Cavalry'nin Castle Age gating'i.

## 6. Kod referansları (file:line, türetme)

- **Tanım & tablo:** [CivilizationDefs.cs:7](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L7)
  (`enum Civilization`), [:9](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L9)
  (`struct CivBonus`), [:33](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L33)
  (`Table`), [:70](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L70) (`Get`).
- **Atama:** [WorldRoot.cs:625-628](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L625) —
  4 takıma rastgele civ (`Random.Range(1, civValues.Length)` ile `None` atlanır).
- **Erişim:** [GameManager.cs:60](../../AgeOfArenaUnity/Assets/Scripts/GameManager.cs#L60)
  (`teamCivs[]`), [:66-69](../../AgeOfArenaUnity/Assets/Scripts/GameManager.cs#L66)
  (`CivBonus` / `TeamCivBonus`).
- **Gather çarpanı:** [GatherSystem.cs:189-202](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L189)
  (`CivGatherMult`, Food/Wood/Gold switch).
- **Piyade atk:** [UnitEntity.cs:111-119](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L111)
  (`AttackDamage`, `infantryAttackMult`, Militia|Spearman gate).
- **Okçu menzil:** [UnitEntity.cs:121-129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L121)
  (`AttackRange`, `archerRangeBonus`, Archer|Longbowman gate, toplamsal).
- **Süvari HP/hız:** [UnitEntity.cs:210-220](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L210)
  (`Start()` içinde tek seferlik; `cavalryHpMult`, `cavalrySpeedMult`).
- **Britons Longbowman:** [BuildingEntity.cs:117-121](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L117)
  (`ArcheryTrainablesBritons`), [:172](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L172)
  (`IsBritons`), [:195](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L195) (dallanma).
- **HUD:** [HUD.cs:300](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L300) (`display` adı).

**Türetme notu — INERT çarpanlar:** `buildingHpMult` ([CivilizationDefs.cs:24](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L24)),
`healRateMult` ([:25](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L25)),
`farmDecayMult` ([:28](../../AgeOfArenaUnity/Assets/Scripts/CivilizationDefs.cs#L28)) için
codebase'de tüketici bulunamadı (grep: yalnızca `CivilizationDefs.cs`). İyileştirme
[CombatSystem.cs:205-226](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L205) `StepHeal`
sabit `HealPower` kullanır, `healRateMult` çarpmaz. Bina HP'si `BuildingEntity` HP atamasında
civ çarpanı içermez. Bu yüzden **Byzantines ve Franks farmDecay bonusu inert**.

## 7. AoE2 farkı (reference köprü)

Tam AoE2 medeniyet sistemi için: [docs/reference/01-civilizations.md](../reference/01-civilizations.md).
Özet farklar:

- **Sayı:** AoE2 DE'de **45 medeniyet** var; AoA'da **5** (+None). AoA'nın 5'i AoE2'nin orijinal
  AoK 13'ünden seçilmiş ([reference:76](../reference/01-civilizations.md)).
- **Yapı:** AoE2'de her civ = **1-3 unique birim + 2 unique tech (Castle/Imperial) + 3-5 civ
  bonusu + 1 takım bonusu**. AoA'da bu paket yok; bonuslar düz stat çarpanı. Tek unique birim
  Britons → Longbowman (AoE2'de de Britons unique'i Longbowman — uyumlu).
- **Takım bonusu:** AoE2'de takıma yayılan ayrı bir bonus var; AoA'da takım bonusu **kavramı
  yok** (her civ kendi statını taşır).
- **Bonus içerikleri:** AoE2 bonusları genelde "bina %X hızlı üretir/araştırır", "menzil/görüş
  +N", "kaynak indirimi" gibi çeşitlidir ([reference:28-74](../reference/01-civilizations.md)).
  AoA bonusları çok daha dar: gather çarpanı, süvari HP/hız, okçu menzil, piyade atk.
- **Franks:** AoE2'de Franks = Knight +%20 HP, ucuz Castle, +ekonomi. AoA Franks de süvari HP +
  yiyecek ekonomisi → **kavramsal olarak en yakın eşleşme**.
- **Britons:** AoE2'de Britons = okçu menzil bonusu + Longbowman + Archery Range hız.
  AoA'da +1 okçu menzil + Longbowman erişimi → uyumlu; Archery Range hız bonusu AoA'da yok.
- **Mongols:** AoE2'de süvari okçu (Mangudai) + Cav Archer hız civ'i. AoA'da Mangudai/cav-archer
  birimi yok; "hız" temasını genel süvari hızına genellemiş.
- **Byzantines:** AoE2'de savunma + ucuz counter birim + iyileştirme hızı. AoA tasarımı aynı temayı
  hedefliyor (buildingHp/heal) ama **implemente edilmediği için fark devasa** (bkz. §8).
- **Japanese:** AoE2'de piyade hız saldırısı (Samurai) + balıkçılık. AoA piyade atk +%10 ile
  piyade temasını korur; deniz bonusu yok.

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| CIVB | bug/balance | `buildingHpMult` & `healRateMult` tanımlı ama tüketilmiyor → Byzantines pratikte bonussuz | Byzantines (savunma/heal civ) [reference:39](../reference/01-civilizations.md) | S |
| CIVF | bug/balance | `farmDecayMult` tanımlı ama `ResourceNode.decayPerSecond`'a uygulanmıyor → Franks çiftlik bonusu inert | Franks ekonomi teması | S |
| CIVS | feature | Oyuncu civ seçim ekranı yok; civ rastgele atanıyor ([WorldRoot.cs:628](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L628)) | AoE2 lobide civ seçimi | M |
| CIVU | feature | Sadece 1 gerçek unique birim (Longbowman) var; diğer 4 civ unique birimsiz | Her civ 1-3 unique birim [reference:9](../reference/01-civilizations.md) | L |
| CIVT | feature | Unique tech (Castle/Imperial) sistemi yok | Her civ 2 unique tech [reference:11](../reference/01-civilizations.md) | L |
| CIVM | feature | Takım bonusu kavramı yok | AoE2 takım bonusu [reference:12](../reference/01-civilizations.md) | M |
| CIVC | feature | Sadece 5 civ var; AoE2'de 45 | 45 medeniyet [reference:3](../reference/01-civilizations.md) | L |
| CIVD | balance | Civ kimlikleri çok dar (1-3 düz çarpan); mimari stil/oynanış arketipi yok | AoE2 çeşitliliği [reference:14](../reference/01-civilizations.md) | M |
| CIVV | qol | Süvari HP/hız bonusu spawn anında `Start()`'ta donuyor; sonradan civ/tech değişimi mevcut birimi güncellemiyor ([UnitEntity.cs:210](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L210)) | — | S |
