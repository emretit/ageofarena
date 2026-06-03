# Wiki §8 Birleşik Backlog

Tüm wiki §8 eksikleri (gap analizi) tek listede birleştirildi, tekilleştirildi ve
önceliklendirildi. Kaynak: `docs/reference/` gap analizi JSON çıktısı.

ID'ler HANDOFF.md P3 tablosundaki mevcut ID'lerle (SKI, SPN2, SCT2, SIEG, CAVA, CAML,
CIVX, BTOW) **çakışmayacak** şekilde yeniden adlandırıldı.

Öncelik dağılımı: **P1** = 7, **P2** = 30, **P3** = 35 (toplam 72 tekil madde).

---

## HANDOFF.md P3'e aktarılacak satırlar

> Aşağıdaki blok doğrudan HANDOFF.md P3 tablosuna kopyalanabilir. Sütunlar:
> `| ID | Ref | Madde | Durum | Not |`. Durum hücreleri boş kutu (⬜).

```markdown
| ID | Ref | Madde | Durum | Not |
|----|-----|-------|-------|-----|
| AGEB | game flow | Çağ atlama için bina önkoşulu kontrolü (2 bina) | ⬜ | TechDefs.IsAvailable yalnız önceki çağı kontrol ediyor (TechDefs.cs:97) |
| AGFX | game flow | Çağ atlama görsel/ses kutlama feedback'i | ⬜ | ResearchSystem.Apply yalnız FireAgeAdvanced tetikliyor (ResearchSystem.cs:103-113) |
| SWRK | ref/03 | Siege Workshop üretim binası (Ram/Mangonel/Scorpion) | ⬜ | BuildingDefs.Table'da Siege Workshop tipi yok |
| BSMT | ref/04 | Blacksmith saldırı/zırh 2. ve 3. kademeleri | ⬜ | Yalnız Forging(+2) + ScaleMail var (TechDefs.cs:47-49) |
| SPEAR | ref/04 | Spearman→Pikeman→Halberdier hattı | ⬜ | TechType enum'da süvari counter terfisi yok (GameTypes.cs:33) |
| ECON | ref/04 | Ekonomi tech'leri (Loom, Horse Collar/Heavy Plow/Crop Rotation, Bow Saw, Mining) | ⬜ | Yalnız DoubleBitAxe + Wheelbarrow var (TechDefs.cs:52-53) |
| SKIR | ref/02+07 | Skirmisher → Elite Skirmisher hattı (anti-archer) | ⬜ | UnitType enum'da Skirmisher yok |
| CIVB | ref/01 | Byzantines buildingHpMult & healRateMult tüketilmiyor | ⬜ | CivilizationDefs.cs:24-25 tanımlı ama tüketici yok; CombatSystem.cs:226 healRateMult çarpmaz |
| CIVF | ref/01 | Franks farmDecayMult uygulanmıyor | ⬜ | CivilizationDefs.cs:28 tanımlı; ResourceNode.cs:27 civ çarpanı okumuyor |
| STIC | UI | Attack-stance ikonları + görünür stance göstergesi | ⬜ | Stance var ama yalnız Q döngüsü (HUD.cs:1016) |
| SPLASH | ref/02+07 | Alan hasarı (AoE splash) — Mangonel/Onager | ⬜ | UnitEntity/Projectile tek hedefe TakeDamage; AoE yok |
| BMBT | ref/03 | Bombard Tower (yüksek hasarlı top kulesi) | ⬜ | Yalnız Castle + Watch Tower atış yapıyor |
| TWUP | ref/03 | Watch Tower → Guard Tower → Keep yükseltme zinciri | ⬜ | Tek WatchTower defi (BuildingDefs.cs:85) |
| WLUP | ref/03 | Wall varyant/yükseltme (Palisade/Stone/Fortified) | ⬜ | Tek Wall defi 200 HP (BuildingDefs.cs:80) |
| FISH | ref/03 | Dock deniz ekonomisi (balıkçı gemi + Fish Trap) | ⬜ | Dock yalnız Galley üretiyor (BuildingEntity.cs:145) |
| RELC | ref/03+05 | Monastery relic tutma + relic-gold; Monk taşıma/deposit | ⬜ | Monastery Monk üretir ama relic gelir mekaniği yok (RelicEntity.cs:51) |
| FARM | ref/03+05 | Çiftlik tükenme + reseed + kapasite tech zinciri | ⬜ | FarmField sabit 300 food; kapasite tech yok (ResourceFactory.cs:96) |
| ARRM | ref/04 | Okçu zırh hattı (Padded/Leather/Ring) + Bracer | ⬜ | Yalnız Fletching+Bodkin var (TechState.cs:24,63) |
| MKTT | ref/04 | Market tech'leri (Coinage, Banking, Caravan, Guilds) | ⬜ | Market bina TechDefs tablosunda yok (TechDefs.cs:39) |
| UNIV | ref/04 | University tech'leri (Ballistics, Chemistry, Architecture, kule yük.) | ⬜ | Yalnız Masonry + Fortified var (TechDefs.cs:64-65) |
| CAVT | ref/04 | Stable: Husbandry (hız) + Light Cavalry/Hussar hattı | ⬜ | Bloodlines/Cavalier/Paladin var; Husbandry yok (TechDefs.cs:54,61-62) |
| BFUR | ref/02 | Çok basamaklı demirci (Iron Casting/Blast Furnace, Chain/Plate Mail) | ⬜ | Tek Forging + tek ScaleMail; basamaklı zincir yok |
| RAMS | ref/07 | Battering Ram (pierce-immune, anti-bina) | ⬜ | Trebuchet var ama Ram zırhı/immunity'si yok |
| VTAT | bug | Veterancy +%10 attack uygulanmıyor (yorum/kod tutarsızlığı) | ⬜ | UnitEntity.cs:424-434 yorumda +%10 atk der; kod yalnız +10 HP |
| CIVS | ref/01 | Oyuncu civ seçim ekranı (rastgele atama yerine) | ⬜ | WorldRoot.cs:625-628 Random.Range ile atama |
| CIVU | ref/01 | Civ başına unique birim (yalnız Longbowman var) | ⬜ | BuildingEntity.cs:117-121,195 yalnız Britons dallanması |
| CIVT | ref/01 | Unique tech (Castle/Imperial) sistemi | ⬜ | CivBonus düz çarpan; unique tech kavramı yok |
| CIVM | ref/01 | Takım bonusu kavramı | ⬜ | Her civ kendi statını taşır; yayılan bonus yok |
| TRIB | ref/05 | Takımlar arası kaynak haraç (tribute) + vergi | ⬜ | ResourceManager.Gain/Deduct var ama transfer akışı yok |
| CARA | ref/05 | Caravan hız tech'i + Trade Cog deniz ticaret varyantı | ⬜ | TradingSystem tek-yön lineer (TradingSystem.cs:12) |
| GRATE | ref/05 | Kaynak-türü-bazlı farklı hasat hızı | ⬜ | Tek sabit GatherRate (GatherSystem.cs:12-13,21) |
| AISC | ai | AI script/davranış katmanı (3 sabit personality yetersiz) | ⬜ | ApplyPersonality EnemyAI.cs:122-142 5 knob'a sıkışmış |
| AIWN | ai | AI multi-victory aware (yalnız Conquest hedefliyor) | ⬜ | Hedef skorlama EnemyAI.cs:689-705 yalnız TC win-condition |
| VTIME | ref/06 | Zaman-limiti (Time Limit) skor zafer modu | ⬜ | MatchSystem.Score() yalnız bitince raporlanıyor |
| FOWD | FogOfWar | Fog of War varsayılan açık + dengeleme | ⬜ | FogOfWarSystem.cs:31 fogEnabled=false varsayılan |
| SAVF | SaveLoad | Tam oyun durumu kaydı (birim/bina pozisyonları) | ⬜ | SaveSystem yalnız kaynak/çağ/tech snapshot (SaveSystem.cs:18) |
| HKEY | Controls | Özelleştirilebilir hotkey haritası | ⬜ | Tuşlar sabit KeyCode literal (CommandSystem.cs/HUD.cs) |
| HPWB | UI | Dünya-uzayında birim HP barları | ⬜ | HP barı yalnız seçili binada HUD'da (HUD.cs:717) |
| DARK | ages | Dark çağına özgü kısıtlama/avantaj | ⬜ | Age.Dark yalnız başlangıç durumu (TechState.cs:11) |
| ARES | game flow | Restart sonrası civ/zorluk seçimi korunmuyor | ⬜ | WorldRoot.SetupGameplay her build'de rastgele civ (WorldRoot.cs:626-628) |
| STRT | game flow | Oyun başı kurulum ekranı (harita/civ/zorluk) | ⬜ | GameBootstrap.Boot doğrudan WorldRoot.Build (GameBootstrap.cs:11-20) |
| CAVAR | ref/02 | Cavalry Archer hattı (mobil okçu) | ⬜ | UnitType'da süvari okçusu yok (GameTypes.cs:9) |
| NAVX | ref/02 | Naval çeşitlilik (Fire/Demo/Cannon Galleon, War Galley upgrade) | ⬜ | Yalnız tek Galley var (UnitFactory.cs:262) |
| EAGLE | ref/02 | Eagle Warrior hattı (medeniyete özgü piyade-süvari) | ⬜ | UnitType enum'da Eagle yok |
| MFAITH | ref/02+05 | Monk faith + relic taşıma; Monastery deposit | ⬜ | UnitEntity.cs:53 sabit 4s dönüştürme; faith yok |
| CAMEL | ref/02+07 | Camel Rider hattı (anti-cavalry uzmanı) | ⬜ | UnitType'da Camel yok; tek counter Spearman |
| VETATK | balance | Veteranlık attack bonusu (yorum +%10, kod yalnız +HP) | ⬜ | AddKill UnitEntity.cs:424 yalnız +10 HP uygular |
| THSW | tier | Two-Handed Swordsman ara tier'i (Long→2H→Champion) | ⬜ | TechDefs.cs:56-58 Longsword'dan doğrudan Champion'a |
| ARMR | balance | Tier-terfilerde zırh (melee/pierce) artışı | ⬜ | TechState HpBonus/AttackBonus var; armor artmıyor |
| RETR | mechanic | Araştırılan HP terfisi canlı birimlere geriye dönük | ⬜ | UnitEntity.cs:207 yalnız Start()'ta ekleniyor |
| MONK | ref/04 | Monastery/Monk tech'leri (Redemption, Sanctity, Block Printing) | ⬜ | Monk/Monastery sistemi TechType'da yok (GameTypes.cs:33) |
| RPCT | ResearchSystem | Toplama bonusunu AoE2 bileşik (taşıma+hız) modeline yaklaştır | ⬜ | GatherMult düz yüzde çarpan (TechState.cs:73) |
| CIVC | ref/01 | Medeniyet sayısı 5 → 45 genişletme (CIVX dışı ek) | ⬜ | CivilizationDefs.cs:7 enum 5 civ + None |
| CIVD | ref/01 | Civ kimliklerini genişlet (arketip/mimari stil) | ⬜ | CivBonus yalnız gather/cav/archer/infantry çarpanları |
| CIVV | ref/01 | Süvari HP/hız bonusu Start()'ta donuyor (sonradan güncellenmez) | ⬜ | UnitEntity.cs:210-220 tek seferlik uygulama |
| AREA | mekanik | Mangonel/Onager projectile alan hasarı | ⬜ | Projectile.cs tek hedefe TakeDamage |
| BNUS | mekanik | Toplamalı bonus-damage modeli (AoE2 net formülü) | ⬜ | AoA çarpan kullanır (3x); AoE2 net=max(1,atk+bonus-armor) |
| ARMC | mekanik | Genişletilmiş armor class'ları (cavalry/siege/building) | ⬜ | UnitEntity.cs:365-377 yalnız melee/pierce + siege-bypass |
| CONV | mekanik | Olasılıksal conversion + Heresy/Atonement/Theocracy tech | ⬜ | CombatSystem.cs:235 sabit 4s conversion |
| MINR | mekanik | Kuşatma minimum menzil (yakın infantry counter'ı) | ⬜ | Trebuchet/Galley min-range yok |
| STONE | balance | Stone başlangıç 0 ve ekonomik döngüde marjinal | ⬜ | ResourceManager.cs:14 stone=0 başlar |
| AICH | ai | Difficulty kaynak/handicap hilesi yok | ⬜ | ApplyDifficulty EnemyAI.cs:96-118 yalnız üretim/cap ölçekler |
| AIDF | ai | 4 difficulty seviyesi → 6 AoE2 seviyesi | ⬜ | Difficulty enum GameTypes.cs:27 = Easy/Normal/Hard/Insane |
| AIRD | ai | RoundToInt round-half-to-even türetilmiş değer tutarsızlığı | ⬜ | EnemyAI.cs:104 RoundToInt(6.5)=6, doküman 7 diyor |
| AIDP | ai | Diplomasi/ittifak AI davranışı (Allied/Neutral/tribute) | ⬜ | EnemyAI.cs'de diplomasi/tribute mantığı yok |
| VREGI | ref/06 | Regicide (Kral) modu | ⬜ | Kral birimi + ölüm eleme mantığı yok |
| VDEATH | ref/06 | Deathmatch modu | ⬜ | Yüksek başlangıç kaynaklı saf askeri mod yok |
| VNOMAD | ref/06 | Nomad (TC'siz başlangıç) modu | ⬜ | WorldRoot başlangıçta sabit TC kuruyor |
| VHOLD | ref/06 | Wonder/Relik bekleme süresi harita boyutu + Atheism ölçeklemesi | ⬜ | MatchSystem.cs:17-18 sabit 60 sn |
| VDIPL | ref/06 | Diplomasi (Allied/Neutral/Enemy + Tribute) | ⬜ | Takım ilişkileri sabit düşmanlık |
| DIPL | UI | Diplomasi / ittifak paneli | ⬜ | Diplomasi UI'ı kodda yok |
| CMDP | UI | Komut kartı sayfalama | ⬜ | HUD grid sabit 15 slot (HUD.cs:68-69) |
| MMTR | Minimap | Minimap terrain/explored render | ⬜ | Düz arka plan + nokta overlay |
| MPNG | Minimap | Minimap ping / işaret sistemi | ⬜ | Yalnız tıkla-git/komut |
| SUBT | Audio | Birim-spesifik seçim/onay sesleri | ⬜ | AudioManager.cs:13 tek generic UnitSelect |
| BPOP | ref/03 | House/TC nüfus tavanı toplama mantığı doğrulanmadı | ⬜ | popProvided alanı var; toplama/cap mantığı doğrulanmadı |
| CSTL | ref/03 | Castle açık çağ kilidi (minAge) | ⬜ | BuildingDefs.cs:77 minAge verilmemiş, varsayılan Dark |
| OUTP | ref/03 | Outpost (ok atmayan görüş kulesi) | ⬜ | BuildingType tablosunda Outpost yok |
```

---

## P1 — Öncelikli (7 madde)

| ID | Ref | Madde | Durum | Not |
|----|-----|-------|-------|-----|
| SWRK | ref/03 | Siege Workshop üretim binası (Ram/Mangonel/Scorpion) | ⬜ | BuildingDefs.Table'da Siege Workshop tipi yok |
| BSMT | ref/04 | Blacksmith saldırı/zırh 2. ve 3. kademeleri | ⬜ | Yalnız Forging(+2) + ScaleMail var (TechDefs.cs:47-49) |
| SPEAR | ref/04 | Spearman→Pikeman→Halberdier hattı | ⬜ | TechType enum'da süvari counter terfisi yok (GameTypes.cs:33) |
| ECON | ref/04 | Ekonomi tech'leri (Loom, Horse Collar/Heavy Plow/Crop Rotation, Bow Saw, Mining) | ⬜ | Yalnız DoubleBitAxe + Wheelbarrow var (TechDefs.cs:52-53) |
| SKIR | ref/02+07 | Skirmisher → Elite Skirmisher hattı (anti-archer) | ⬜ | UnitType enum'da Skirmisher yok |
| CIVB | ref/01 | Byzantines buildingHpMult & healRateMult tüketilmiyor | ⬜ | CivilizationDefs.cs:24-25 tanımlı ama tüketici yok |
| CIVF | ref/01 | Franks farmDecayMult uygulanmıyor | ⬜ | CivilizationDefs.cs:28 tanımlı; ResourceNode.cs:27 okumuyor |
| STIC | UI | Attack-stance ikonları + görünür stance göstergesi | ⬜ | Stance var ama yalnız Q döngüsü (HUD.cs:1016) |

## P2 — Orta öncelik (30 madde)

| ID | Ref | Madde | Durum | Not |
|----|-----|-------|-------|-----|
| AGEB | game flow | Çağ atlama için bina önkoşulu kontrolü (2 bina) | ⬜ | TechDefs.IsAvailable yalnız önceki çağı kontrol ediyor (TechDefs.cs:97) |
| AGFX | game flow | Çağ atlama görsel/ses kutlama feedback'i | ⬜ | ResearchSystem.Apply yalnız FireAgeAdvanced tetikliyor (ResearchSystem.cs:103-113) |
| SPLASH | ref/02+07 | Alan hasarı (AoE splash) — Mangonel/Onager | ⬜ | UnitEntity/Projectile tek hedefe TakeDamage; AoE yok |
| BMBT | ref/03 | Bombard Tower (yüksek hasarlı top kulesi) | ⬜ | Yalnız Castle + Watch Tower atış yapıyor |
| TWUP | ref/03 | Watch Tower → Guard Tower → Keep yükseltme zinciri | ⬜ | Tek WatchTower defi (BuildingDefs.cs:85) |
| WLUP | ref/03 | Wall varyant/yükseltme (Palisade/Stone/Fortified) | ⬜ | Tek Wall defi 200 HP (BuildingDefs.cs:80) |
| FISH | ref/03 | Dock deniz ekonomisi (balıkçı gemi + Fish Trap) | ⬜ | Dock yalnız Galley üretiyor (BuildingEntity.cs:145) |
| RELC | ref/03+05 | Monastery relic tutma + relic-gold; Monk taşıma/deposit | ⬜ | Monastery Monk üretir ama relic gelir mekaniği yok (RelicEntity.cs:51) |
| FARM | ref/03+05 | Çiftlik tükenme + reseed + kapasite tech zinciri | ⬜ | FarmField sabit 300 food; kapasite tech yok (ResourceFactory.cs:96) |
| ARRM | ref/04 | Okçu zırh hattı (Padded/Leather/Ring) + Bracer | ⬜ | Yalnız Fletching+Bodkin var (TechState.cs:24,63) |
| MKTT | ref/04 | Market tech'leri (Coinage, Banking, Caravan, Guilds) | ⬜ | Market bina TechDefs tablosunda yok (TechDefs.cs:39) |
| UNIV | ref/04 | University tech'leri (Ballistics, Chemistry, Architecture, kule yük.) | ⬜ | Yalnız Masonry + Fortified var (TechDefs.cs:64-65) |
| CAVT | ref/04 | Stable: Husbandry (hız) + Light Cavalry/Hussar hattı | ⬜ | Bloodlines/Cavalier/Paladin var; Husbandry yok (TechDefs.cs:54,61-62) |
| BFUR | ref/02 | Çok basamaklı demirci (Iron Casting/Blast Furnace, Chain/Plate Mail) | ⬜ | Tek Forging + tek ScaleMail; basamaklı zincir yok |
| RAMS | ref/07 | Battering Ram (pierce-immune, anti-bina) | ⬜ | Trebuchet var ama Ram zırhı/immunity'si yok |
| VTAT | bug | Veterancy +%10 attack uygulanmıyor (yorum/kod tutarsızlığı) | ⬜ | UnitEntity.cs:424-434 yorumda +%10 atk der; kod yalnız +10 HP |
| CIVS | ref/01 | Oyuncu civ seçim ekranı (rastgele atama yerine) | ⬜ | WorldRoot.cs:625-628 Random.Range ile atama |
| CIVU | ref/01 | Civ başına unique birim (yalnız Longbowman var) | ⬜ | BuildingEntity.cs:117-121,195 yalnız Britons dallanması |
| CIVT | ref/01 | Unique tech (Castle/Imperial) sistemi | ⬜ | CivBonus düz çarpan; unique tech kavramı yok |
| CIVM | ref/01 | Takım bonusu kavramı | ⬜ | Her civ kendi statını taşır; yayılan bonus yok |
| TRIB | ref/05 | Takımlar arası kaynak haraç (tribute) + vergi | ⬜ | ResourceManager.Gain/Deduct var ama transfer akışı yok |
| CARA | ref/05 | Caravan hız tech'i + Trade Cog deniz ticaret varyantı | ⬜ | TradingSystem tek-yön lineer (TradingSystem.cs:12) |
| GRATE | ref/05 | Kaynak-türü-bazlı farklı hasat hızı | ⬜ | Tek sabit GatherRate (GatherSystem.cs:12-13,21) |
| AISC | ai | AI script/davranış katmanı (3 sabit personality yetersiz) | ⬜ | ApplyPersonality EnemyAI.cs:122-142 5 knob'a sıkışmış |
| AIWN | ai | AI multi-victory aware (yalnız Conquest hedefliyor) | ⬜ | Hedef skorlama EnemyAI.cs:689-705 yalnız TC win-condition |
| VTIME | ref/06 | Zaman-limiti (Time Limit) skor zafer modu | ⬜ | MatchSystem.Score() yalnız bitince raporlanıyor |
| FOWD | FogOfWar | Fog of War varsayılan açık + dengeleme | ⬜ | FogOfWarSystem.cs:31 fogEnabled=false varsayılan |
| SAVF | SaveLoad | Tam oyun durumu kaydı (birim/bina pozisyonları) | ⬜ | SaveSystem yalnız kaynak/çağ/tech snapshot (SaveSystem.cs:18) |
| HKEY | Controls | Özelleştirilebilir hotkey haritası | ⬜ | Tuşlar sabit KeyCode literal (CommandSystem.cs/HUD.cs) |
| HPWB | UI | Dünya-uzayında birim HP barları | ⬜ | HP barı yalnız seçili binada HUD'da (HUD.cs:717) |

## P3 — Düşük öncelik (35 madde)

| ID | Ref | Madde | Durum | Not |
|----|-----|-------|-------|-----|
| DARK | ages | Dark çağına özgü kısıtlama/avantaj | ⬜ | Age.Dark yalnız başlangıç durumu (TechState.cs:11) |
| ARES | game flow | Restart sonrası civ/zorluk seçimi korunmuyor | ⬜ | WorldRoot.SetupGameplay her build'de rastgele civ (WorldRoot.cs:626-628) |
| STRT | game flow | Oyun başı kurulum ekranı (harita/civ/zorluk) | ⬜ | GameBootstrap.Boot doğrudan WorldRoot.Build (GameBootstrap.cs:11-20) |
| CAVAR | ref/02 | Cavalry Archer hattı (mobil okçu) | ⬜ | UnitType'da süvari okçusu yok (GameTypes.cs:9) |
| NAVX | ref/02 | Naval çeşitlilik (Fire/Demo/Cannon Galleon, War Galley upgrade) | ⬜ | Yalnız tek Galley var (UnitFactory.cs:262) |
| EAGLE | ref/02 | Eagle Warrior hattı (medeniyete özgü piyade-süvari) | ⬜ | UnitType enum'da Eagle yok |
| MFAITH | ref/02+05 | Monk faith + relic taşıma; Monastery deposit | ⬜ | UnitEntity.cs:53 sabit 4s dönüştürme; faith yok |
| CAMEL | ref/02+07 | Camel Rider hattı (anti-cavalry uzmanı) | ⬜ | UnitType'da Camel yok; tek counter Spearman |
| VETATK | balance | Veteranlık attack bonusu (yorum +%10, kod yalnız +HP) | ⬜ | AddKill UnitEntity.cs:424 yalnız +10 HP uygular |
| THSW | tier | Two-Handed Swordsman ara tier'i (Long→2H→Champion) | ⬜ | TechDefs.cs:56-58 Longsword'dan doğrudan Champion'a |
| ARMR | balance | Tier-terfilerde zırh (melee/pierce) artışı | ⬜ | TechState HpBonus/AttackBonus var; armor artmıyor |
| RETR | mechanic | Araştırılan HP terfisi canlı birimlere geriye dönük | ⬜ | UnitEntity.cs:207 yalnız Start()'ta ekleniyor |
| MONK | ref/04 | Monastery/Monk tech'leri (Redemption, Sanctity, Block Printing) | ⬜ | Monk/Monastery sistemi TechType'da yok (GameTypes.cs:33) |
| RPCT | ResearchSystem | Toplama bonusunu AoE2 bileşik (taşıma+hız) modeline yaklaştır | ⬜ | GatherMult düz yüzde çarpan (TechState.cs:73) |
| CIVC | ref/01 | Medeniyet sayısı 5 → 45 genişletme (CIVX dışı ek) | ⬜ | CivilizationDefs.cs:7 enum 5 civ + None |
| CIVD | ref/01 | Civ kimliklerini genişlet (arketip/mimari stil) | ⬜ | CivBonus yalnız gather/cav/archer/infantry çarpanları |
| CIVV | ref/01 | Süvari HP/hız bonusu Start()'ta donuyor (sonradan güncellenmez) | ⬜ | UnitEntity.cs:210-220 tek seferlik uygulama |
| AREA | mekanik | Mangonel/Onager projectile alan hasarı | ⬜ | Projectile.cs tek hedefe TakeDamage |
| BNUS | mekanik | Toplamalı bonus-damage modeli (AoE2 net formülü) | ⬜ | AoA çarpan kullanır (3x); AoE2 net=max(1,atk+bonus-armor) |
| ARMC | mekanik | Genişletilmiş armor class'ları (cavalry/siege/building) | ⬜ | UnitEntity.cs:365-377 yalnız melee/pierce + siege-bypass |
| CONV | mekanik | Olasılıksal conversion + Heresy/Atonement/Theocracy tech | ⬜ | CombatSystem.cs:235 sabit 4s conversion |
| MINR | mekanik | Kuşatma minimum menzil (yakın infantry counter'ı) | ⬜ | Trebuchet/Galley min-range yok |
| STONE | balance | Stone başlangıç 0 ve ekonomik döngüde marjinal | ⬜ | ResourceManager.cs:14 stone=0 başlar |
| AICH | ai | Difficulty kaynak/handicap hilesi yok | ⬜ | ApplyDifficulty EnemyAI.cs:96-118 yalnız üretim/cap ölçekler |
| AIDF | ai | 4 difficulty seviyesi → 6 AoE2 seviyesi | ⬜ | Difficulty enum GameTypes.cs:27 = Easy/Normal/Hard/Insane |
| AIRD | ai | RoundToInt round-half-to-even türetilmiş değer tutarsızlığı | ⬜ | EnemyAI.cs:104 RoundToInt(6.5)=6, doküman 7 diyor |
| AIDP | ai | Diplomasi/ittifak AI davranışı (Allied/Neutral/tribute) | ⬜ | EnemyAI.cs'de diplomasi/tribute mantığı yok |
| VREGI | ref/06 | Regicide (Kral) modu | ⬜ | Kral birimi + ölüm eleme mantığı yok |
| VDEATH | ref/06 | Deathmatch modu | ⬜ | Yüksek başlangıç kaynaklı saf askeri mod yok |
| VNOMAD | ref/06 | Nomad (TC'siz başlangıç) modu | ⬜ | WorldRoot başlangıçta sabit TC kuruyor |
| VHOLD | ref/06 | Wonder/Relik bekleme süresi harita boyutu + Atheism ölçeklemesi | ⬜ | MatchSystem.cs:17-18 sabit 60 sn |
| VDIPL | ref/06 | Diplomasi (Allied/Neutral/Enemy + Tribute) | ⬜ | Takım ilişkileri sabit düşmanlık |
| DIPL | UI | Diplomasi / ittifak paneli | ⬜ | Diplomasi UI'ı kodda yok |
| CMDP | UI | Komut kartı sayfalama | ⬜ | HUD grid sabit 15 slot (HUD.cs:68-69) |
| MMTR | Minimap | Minimap terrain/explored render | ⬜ | Düz arka plan + nokta overlay |
| MPNG | Minimap | Minimap ping / işaret sistemi | ⬜ | Yalnız tıkla-git/komut |
| SUBT | Audio | Birim-spesifik seçim/onay sesleri | ⬜ | AudioManager.cs:13 tek generic UnitSelect |
| BPOP | ref/03 | House/TC nüfus tavanı toplama mantığı doğrulanmadı | ⬜ | popProvided var; toplama/cap mantığı doğrulanmadı |
| CSTL | ref/03 | Castle açık çağ kilidi (minAge) | ⬜ | BuildingDefs.cs:77 minAge verilmemiş, varsayılan Dark |
| OUTP | ref/03 | Outpost (ok atmayan görüş kulesi) | ⬜ | BuildingType tablosunda Outpost yok |

---

## Tekilleştirme / ID değişiklik notları

JSON içinde tekrar eden veya HANDOFF.md mevcut ID'leri ile çakışan suggestedId'ler:

- **SKIR** — JSON'da 3 kez (Skirmisher hattı / archer-counter). Tek maddeye birleştirildi
  (P1'e yükseltildi; en yüksek öncelik kazandı). HANDOFF'taki `SKI` ile karıştırmamak için
  `SKIR` korundu (farklı ID).
- **SIEGE / SIEG** — JSON `SIEG` (Siege terfi hatları) ile HANDOFF mevcut `SIEG` çakışıyor;
  Siege Workshop binası `SWRK`, Ram `RAMS`, splash `SPLASH`/`AREA` ayrı ID'lere dağıtıldı.
  JSON `SIEGE` (kuşatma çeşitliliği) `SWRK` + `RAMS` kapsamına alındı.
- **CAML** — HANDOFF mevcut `CAML` ile çakışıyor; JSON'daki Camel maddeleri (`CAMEL`+`CAML`)
  tek `CAMEL` ID'sinde birleştirildi.
- **FARM** — JSON'da 2 kez (tükenme/reseed + kapasite tech). Tek `FARM` maddesinde birleştirildi.
- **MONK** — JSON'da 3 kez (Monk tech / relic taşıma / Monastery). `MONK` (tech ağacı) +
  `MFAITH` (faith/relic taşıma) + `RELC` (Monastery relic-gold) olarak ayrıştırıldı.
- **RELC** — Monastery relic tutma + relic-gold için tek ID; `RVMONK` ve `MFAITH` ile örtüşen
  Monk-taşıma kısmı `MFAITH`'e taşındı, deposit/gelir `RELC`'te kaldı.
- **VTAT / VETATK** — aynı veterancy attack bug'ı; `VTAT` (P2, bug etiketi) ana madde,
  `VETATK` (P3, balance açısı) ayrı tutuldu ama aynı kök neden.
- **DIPL / VDIPL / AIDP** — diplomasi 3 açıdan: `VDIPL` (oyun modu/kural), `DIPL` (UI panel),
  `AIDP` (AI davranışı). Üçü ayrı tutuldu.
- HANDOFF'taki `SKI, SPN2, SCT2, SIEG, CAVA, CAML, CIVX, BTOW` ID'lerinin hiçbiri bu listede
  yeniden kullanılmadı.
