# 02 — Birimler & Counter Sistemi

## 1. Durum Özeti

7 birim tipi var: Villager, Militia, Archer, Cavalry, Trebuchet, Scout, Medic
([GameTypes.cs:9](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9)), hepsi procedural mesh
([UnitFactory.cs](../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs)) ve NavMeshAgent ile hareket
ediyor. Savaş davranışı melee/ranged ayrımı + cavalry charge + Trebuchet anti-yapı bonusu içeriyor.
Eksik olan: **gerçek bir counter (taş-kağıt-makas) sistemi** — şu an hasar düz `AttackDamage`,
zırh tipi yok; Monk dönüştürme, naval birimler, özel yetenek altyapısı ve veterancy de yok.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| 7 birim tipi + procedural mesh | ✅ | [UnitFactory.cs](../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs) | Core |
| Hareket (NavMeshAgent), durum makinesi | ✅ | [UnitEntity.cs](../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs) | Core |
| Cavalry charge (2.5×, 4s şarj) | ✅ | [CombatSystem.cs:123](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L123) | Derinlik |
| Trebuchet anti-yapı bonusu | ✅ | [CombatSystem.cs:113-114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L113) | Core |
| Medic iyileştirme (alan içi) | ✅ | [CombatSystem.cs](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs) | Derinlik |
| Scout hızlı/hasarsız keşif | ✅ | [UnitFactory.cs](../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs) | Derinlik |
| Pop maliyeti + popCap | ✅ | [ResourceManager.cs:16-17](../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L16) | Core |
| Tier yükseltme (ManAtArms→Longsword, Crossbow, Cavalier) | ✅ | [TechDefs.cs:55-58](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L55) | Derinlik |
| **Zırh tipleri (melee/pierce/siege)** | ❌ | düz `AttackDamage` [CombatSystem.cs:114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L114) | Core |
| **Net counter zinciri** (mızrak>süvari>okçu>piyade) | ❌ | Spearman/Pikeman yok | Core |
| **Monk** (dönüştürme + relic taşıma + iyileştirme) | ❌ | — | Derinlik |
| **Naval** (Galley, balıkçı gemisi, Dock) | ❌ | — | Derinlik |
| **Özel yetenek altyapısı** (ability) | ❌ | — | Derinlik |
| **Veterancy / rütbe** | ❌ | — | Derinlik |
| **Birim ses tepkisi** | ❌ | bkz [08](08-audio-animation-vfx.md) | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Zırh tipleri + counter matrisi
- **Neden:** RTS'in kalbi; "hangi birim neyi yener" olmadan savaş yumruk-yarışına döner.
- **Yaklaşım:** `UnitEntity`/`BuildingDefs`'e `meleeArmor`/`pierceArmor` + saldırıya `damageType` (melee/pierce/siege) ekle. `CombatSystem` hasar hesabını [CombatSystem.cs:114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L114) `effectiveDmg = max(1, atk - hedefZırhı[tip])` yap (overkill mevcut davranışı korunur). Yeni `Spearman` birimini `UnitType`'a ekleyip cavalry'ye bonus hasar (anti-cavalry) ver — taş-kağıt-makasın eksik kenarı.
- **Dokunulacak dosyalar:** `GameTypes.cs` (UnitType+Spearman, damageType enum), `UnitEntity.cs` (armor alanları), `CombatSystem.cs:113-125`, `BuildingDefs.cs` (yapı zırhı), `UnitFactory.cs`.
- **Kabul Kriteri:** Spearman, Cavalry'ye karşı belirgin daha hızlı öldürür; Archer pierce-zırhlı hedefe daha az hasar yapar; piyade okçuya yaklaşınca üstün gelir.
- **Doğrulama:** Play → 5 Spearman vs 5 Cavalry tek tıkla çarpıştır → `Unity_Camera_Capture` ile kalan canı say; aynı sayıda Cavalry vs Cavalry referans alınır. `Unity_GetConsoleLogs(Error)` temiz.

### [P1] Monk (dönüştürme + relic taşıma + iyileştirme)
- **Neden:** Relic sistemi ([RelicSystem.cs](../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs)) zaten var; Monk onun doğal taşıyıcısı ve AoE2 derinlik birimidir.
- **Yaklaşım:** `UnitType.Monk` ekle (Monastery binasından üretilir, bkz [03](03-buildings.md)); yeni `MonkAbility`: menzilli "dönüştürme" zamanlayıcısı düşman birimi takıma geçirir; relic'i yerden alıp Monastery'ye götürme. Medic heal mantığı ([CombatSystem.cs](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs)) iyileştirme için temel alınır.
- **Dokunulacak dosyalar:** `GameTypes.cs`, `UnitFactory.cs`, `CombatSystem.cs` (convert döngüsü), `RelicSystem.cs` (taşıma), `BuildingDefs.cs` (Monastery).
- **Kabul Kriteri:** Monk düşman birimine yaklaşıp N sn sonra o birim oyuncunun takımına geçer (renk değişir, kontrol edilebilir); Monk relic'i alıp Monastery'ye bırakınca altın trickle başlar.
- **Doğrulama:** Play → Monk üret → düşman birimine yolla → Camera.Capture'da renk/takım değişimini gözle; relic taşımayı izle.

### [P2] Özel yetenek (ability) altyapısı
- **Neden:** Monk-convert, charge, heal hepsi ortak bir "yetenek" çatısı ister; tek tek if-else dağılmasını önler.
- **Yaklaşım:** LockstepRTSEngine'in `Initialize/Simulate/Deactivate` ability deseni referans; hafif bir `IUnitAbility { Tick(dt) }` arayüzü, `UnitEntity` üzerinde liste. Mevcut charge/heal bu arayüze taşınır (refactor, davranış aynı).
- **Dokunulacak dosyalar:** yeni `UnitAbility.cs`, `UnitEntity.cs`, `CombatSystem.cs`.
- **Kabul Kriteri:** Charge ve heal ability nesnesi olarak çalışır, eski davranışla bire bir aynı; yeni ability eklemek tek sınıf eklemekle olur.
- **Doğrulama:** Play → charge ve heal regression: cavalry ilk vuruş 2.5×, medic 3hp/s — değerler değişmemiş.

### [P2] Naval katmanı (Dock + Galley + balıkçı gemisi)
- **Neden:** Su haritaları ve harita çeşidi için ([12](12-maps-scenario-campaign.md)).
- **Yaklaşım:** Su terrain'i + NavMesh su alanı; `BuildingType.Dock`, `UnitType.Galley/FishingShip`. Büyük iş — harita üretimiyle birlikte planlanmalı.
- **Kabul Kriteri:** Su haritasında Dock kurulur, Galley üretilir ve suda hareket eder; balıkçı gemisi fish düğümünden food toplar.
- **Doğrulama:** Su haritası senaryosunda Play → Dock+Galley+fishing zincirini gözle.

### [P2] Veterancy / rütbe
- **Yaklaşım:** Birim öldürdükçe `kills` sayacı; eşikte +%10 stat ve görsel işaret. **Kabul:** N öldürme sonrası birim gözle görülür güçlenir.

## 4. Referans Repolardan Notlar

- **WarKingdoms / RTSUnityGameLicenta**: birim seçim + koordineli hareket + savaş OOP deseni.
- **LockstepRTSEngine**: ability sistemi (`Initialize/Simulate/Deactivate`), buff sistemi — yetenek çatısı için doğrudan referans.
- **openage**: birimler component + nyan property; counter/zırh tablo-güdümlü.

## 5. Bağımlılıklar

- Zırh/counter → [04-combat.md](04-combat.md) (hasar hesabı ortak).
- Monk → [03-buildings.md](03-buildings.md) (Monastery) + [09-victory-objectives.md](09-victory-objectives.md) (relic-win).
- Naval → [12-maps-scenario-campaign.md](12-maps-scenario-campaign.md) (su haritası).
- Birim sesleri → [08-audio-animation-vfx.md](08-audio-animation-vfx.md).
