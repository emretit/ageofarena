# 05 — Teknoloji Ağacı & Çağ İlerlemesi

## 1. Durum Özeti

3 çağ var: Dark → Feudal → Castle ([GameTypes.cs:14](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L14)).
17 tech statik tabloda ([TechDefs.cs:42-58](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L42)):
2 çağ atlama (FeudalAge/CastleAge), askeri blacksmith bonusları (Forging/Fletching/Bodkin/
ScaleMail/Bloodlines), tier yükseltmeler (ManAtArms→Longswordsman, Crossbowman, Cavalier) ve
ekonomi (DoubleBitAxe/Wheelbarrow). `TechState` bonusları runtime canlı okuyor, `ResearchSystem`
kuyruğu işliyor. Eksik: **Imperial (4.) çağ**, University tech (Ballistics/Chemistry), research
queue ve prerequisite zincirinin genişlemesi.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| 3 çağ + çağ-kapısı (UnlockedAt) | ✅ | [GameTypes.cs:14](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L14), [BuildingDefs.cs:84](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L84) | Core |
| 17 tech tablosu (cost/time/bina/çağ) | ✅ | [TechDefs.cs:42-58](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L42) | Core |
| Çağ atlama (TC'de FeudalAge/CastleAge) | ✅ | [TechDefs.cs:42-43](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L42), [TechDefs.cs:91-92](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L91) | Core |
| Tier yükseltme hatları | ✅ | [TechDefs.cs:55-58](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L55) | Derinlik |
| Prerequisite (Longsword←ManAtArms) | ✅ | [TechDefs.cs:56](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L56) | Core |
| Canlı bonus hesabı (atk/range/hp/gather) | ✅ | [TechState.cs](../AgeOfArenaUnity/Assets/Scripts/TechState.cs) | Core |
| Araştırma kuyruğu + uygulama | ✅ | [ResearchSystem.cs:60](../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L60) | Core |
| OnResearchCompleted event | ✅ | [GameEvents.cs:12](../AgeOfArenaUnity/Assets/Scripts/GameEvents.cs#L12) | Core |
| **Imperial (4.) çağ** | ❌ | Age enum 3 değer | Core |
| **University binası + tech** (Ballistics/Chemistry/Masonry) | ❌ | — | Core |
| **Çoklu research queue (kuyruk biriktirme)** | 🟡 | tek tech akışı | Derinlik |
| **Imperial tier birimleri** (Halberdier/Arbalester/Paladin) | ❌ | tier 2-3'te duruyor | Derinlik |
| **Civ'e özel unique tech** | ❌ | bkz [11](11-civilizations-balance.md) | Derinlik |
| **Tech ağacı görselleştirme paneli** | ❌ | — | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Imperial (4.) çağ
- **Neden:** AoE2 progression'ın doruğu; geç-oyun birim/tech katmanını açar, mevcut "Castle'da takılı kalma" hissini giderir.
- **Yaklaşım:** `Age` enum'a `Imperial` ekle ([GameTypes.cs:14](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L14)); `TechType.ImperialAge` + TC'de araştırma satırı ([TechDefs.cs:42-43](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L42) kalıbı); `IsAgeAdvance` zincirine ekle ([TechDefs.cs:91-92](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L91)). `UnlockedAt` ([BuildingDefs.cs:84](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L84)) otomatik çalışır. HUD çağ popup'ı zaten OnAgeAdvanced'e bağlı.
- **Dokunulacak dosyalar:** `GameTypes.cs`, `TechDefs.cs:42-43,91-92`, opsiyonel `HUD.cs` (çağ etiketi metni).
- **Kabul Kriteri:** Castle çağında TC'de "İmparatorluk Çağı" araştırılabilir; tamamlanınca çağ Imperial olur, popup çıkar, Imperial-gate'li tech/birim açılır.
- **Doğrulama:** `Unity_ManageEditor(Play)` → Castle'a ulaş → Imperial araştır → `Unity_Camera_Capture`'da çağ popup + HUD çağ etiketini gör; `Unity_GetConsoleLogs(Error)` temiz.

### [P1] University binası + tech (Ballistics/Chemistry/Masonry)
- **Neden:** Okçu/kuşatma/savunma derinliğini açan klasik AoE2 tech binası.
- **Yaklaşım:** `BuildingType.University` ([03](03-buildings.md)); yeni TechType'lar tablo satırı olarak: Ballistics (projectile isabet — [04](04-combat.md) balistiği ile bağlanır), Chemistry (+ok hasarı), Masonry (+bina hp). `TechState` bonus okuması mevcut desenle genişler.
- **Dokunulacak dosyalar:** `GameTypes.cs`, `TechDefs.cs`, `TechState.cs`, `BuildingDefs.cs`, `BuildingFactory.cs`.
- **Kabul Kriteri:** University kurulunca tech kartında Ballistics/Chemistry çıkar; araştırılınca ilgili stat ölçülebilir artar (ok hasarı/bina hp).
- **Doğrulama:** Play → University kur → Chemistry araştır → okçu hasarının arttığını savaş testinde gözle.

### [P1] Imperial tier birimleri
- **Yaklaşım:** Tier yükseltme hattını ([TechDefs.cs:55-58](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L55)) Imperial'a uzat: Halberdier (anti-cav, [02](02-units.md) Spearman hattı), Arbalester, Paladin. Sadece tablo satırı + `TechState` stat sıçraması.
- **Kabul Kriteri:** Imperial'da ilgili tech araştırılınca birim adı+statları yükselir.
- **Doğrulama:** Play → Imperial tech araştır → birim display adı ve hasarının değiştiğini gör.

### [P2] Çoklu research queue + tech ağacı paneli
- **Queue:** Birden çok tech sıraya alınır, sırayla işlenir. **Kabul:** 3 tech sıraya alınınca sırayla tamamlanır.
- **Tech ağacı paneli:** Tüm tech'leri çağ/bina ile gösteren salt-okunur ekran (TechDefs'ten üretilir). **Kabul:** panel tüm tech'leri kilit/açık durumuyla listeler.

## 4. Referans Repolardan Notlar

- **openage**: tech ağacı nyan veri nesneleri — bizim `TechDefs` tablosu aynı felsefede; civ unique tech ([11](11-civilizations-balance.md)) buraya `civ` alanı eklenerek bağlanır.
- **UnityTutorials-RTS**: tech tree + upgrade UI deseni.

## 5. Bağımlılıklar

- University tech → [03-buildings.md](03-buildings.md) (bina) + [04-combat.md](04-combat.md) (Ballistics/Chemistry etkisi).
- Imperial tier → [02-units.md](02-units.md) (birim statları).
- Civ unique tech → [11-civilizations-balance.md](11-civilizations-balance.md).
