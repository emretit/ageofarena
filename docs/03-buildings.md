# 03 — Binalar, Garnizon & Savunma

## 1. Durum Özeti

13 bina tipi statik tablo ile tanımlı ([BuildingDefs.cs:54-70](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L54)):
TownCenter, House, Barracks, ArcheryRange, Stable, Farm, LumberCamp, MiningCamp, Mill, Market,
Castle, Wall, Gate. İnşaat ilerlemesi, çağ/araştırma filtreli üretim, rally point ve Castle
otomatik ok atışı çalışıyor. **En büyük gerçek P0 eksik: garnizon** (birimin binaya girip
korunması/iyileşmesi). Ayrıca kule savunması (sadece Castle ateş ediyor), repair, Blacksmith/
Monastery ve bina aurası yok.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| 13 bina tablosu (cost/time/pop/hp/hotkey) | ✅ | [BuildingDefs.cs:54-70](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L54) | Core |
| İnşaat ghost + yerleştirme | ✅ | [BuildingPlacement.cs](../AgeOfArenaUnity/Assets/Scripts/BuildingPlacement.cs) | Core |
| İnşaat ilerlemesi (çok köylü) | ✅ | [BuildSystem.cs](../AgeOfArenaUnity/Assets/Scripts/BuildSystem.cs) | Core |
| Çağ/araştırma filtreli üretim | ✅ | [BuildingEntity.cs](../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs), [BuildingDefs.cs:84](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L84) | Core |
| Rally point + bayrak | ✅ | [CommandSystem.cs:70-71](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L70), [CommandSystem.cs:291](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L291) | Core |
| Castle otomatik ok (9u/18dmg/1.5s) | ✅ | [BuildingDefs.cs:66](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L66), [BuildingCombatSystem.cs:16](../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L16) | Core |
| Wall/Gate (carving, geçirgen kapı) | ✅ | [BuildingDefs.cs:69-70](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L69) | Core |
| **Garnizon** (birim binaya girer, korunur, iyileşir) | ❌ | — | **Core (P0)** |
| **Watch/Bombard Tower** (ucuz savunma kulesi) | ❌ | sadece Castle ateş ediyor | Core |
| **Repair** (köylü binayı tamir eder) | ❌ | inşa var, tamir yok | Core |
| **Blacksmith** (askeri tech binası) | ❌ | tech'ler mevcut binalarda | Derinlik |
| **Monastery** (Monk + relic) | ❌ | — | Derinlik |
| **Bina aurası** (Blacksmith yakını üretim hızı) | ❌ | — | Derinlik |
| **Palisade vs Stone Wall ayrımı** | ❌ | tek Wall tipi | Derinlik |
| **Wonder** (zafer binası) | ❌ | bkz [09](09-victory-objectives.md) | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P0] Garnizon mekaniği (Faz A)
- **Neden:** Çekirdek RTS savunması; köylü kuşatmada TC'ye sığınır, birim Castle'da iyileşir, garnizonlu TC/Castle ek ok atar.
- **Yaklaşım:** `BuildingEntity`'ye `List<UnitEntity> garrison` + `int garrisonCap`; `CommandSystem` sağ-tık binaya = garnizon emri ([CommandSystem.cs:63](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L63) "selected own building" dalı kalıbı yeniden kullanılır). İçerideki birim render kapatılır, pop korunur, saniyede iyileşir; "ungarrison" rally noktasına çıkarır. Garnizonlu TC/Castle `BuildingCombatSystem` ([BuildingCombatSystem.cs:16](../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L16)) atış sayısını içerideki okçu sayısına göre artırır.
- **Dokunulacak dosyalar:** `BuildingEntity.cs`, `CommandSystem.cs`, `BuildingCombatSystem.cs`, `UnitEntity.cs` (gizle/iyileş), `HUD.cs` (garnizon sayacı + ungarrison butonu).
- **Kabul Kriteri:** Köylü TC'ye girince sahneden kaybolur ama pop sayılır; içerideki yaralı birim zamanla iyileşir; garnizonlu Castle daha çok ok atar; ungarrison rally'ye çıkarır.
- **Doğrulama:** `Unity_ManageEditor(Play)` → köylüyü TC'ye sağ-tıkla → `Unity_Camera_Capture`'da birimin kaybolduğunu ve HUD garnizon sayacının arttığını gör; Castle'a okçu doldur, atış frekansının arttığını izle; `Unity_GetConsoleLogs(Error)` temiz.

### [P1] Watch/Bombard Tower
- **Neden:** Erken savunma; Castle çok pahalı (650 stone, [BuildingDefs.cs:66](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L66)).
- **Yaklaşım:** `BuildingType.WatchTower` tablo satırı; menzil/hasar Castle'dan düşük; `BuildingCombatSystem` zaten yapı-atışını destekliyor — yeni satır + atış parametresi yeter. Bombard Tower = Castle çağında yükseltme.
- **Dokunulacak dosyalar:** `GameTypes.cs`, `BuildingDefs.cs:54-70`, `BuildingFactory.cs`, `BuildingCombatSystem.cs` (zaten generic).
- **Kabul Kriteri:** Watch Tower kurulunca menzile giren düşmana ok atar; Castle'dan daha az menzil/hasar; çağ yükseltmesiyle Bombard'a geçince hasar artar.
- **Doğrulama:** Play → Tower kur → düşman yaklaşınca atışı Camera.Capture'da gözle.

### [P1] Repair (köylü tamir)
- **Neden:** İnşa var ama hasarlı bina tamir edilemiyor — kuşatma sonrası ekonomi eksik.
- **Yaklaşım:** `BuildSystem` ([BuildSystem.cs](../AgeOfArenaUnity/Assets/Scripts/BuildSystem.cs)) inşa ilerlemesini tersine kullan: hp<max olan binaya köylü sağ-tık = tamir; saniyede hp + küçük kaynak gideri.
- **Dokunulacak dosyalar:** `BuildSystem.cs`, `CommandSystem.cs`, `UnitEntity.cs` (Constructing state yeniden kullan).
- **Kabul Kriteri:** Hasarlı binaya köylü gönderince hp zamanla dolar ve kaynak harcanır; tam dolunca köylü idle olur.
- **Doğrulama:** Play → binayı dövüp hasarlat → köylü gönder → hp barının dolduğunu izle.

### [P1] Blacksmith + askeri tech taşınması (Faz C)
- **Neden:** AoE2'de askeri yükseltmeler Blacksmith'te toplanır; bina çeşidini ve tech ağacını netleştirir.
- **Yaklaşım:** `BuildingType.Blacksmith` ekle; Forging/Fletching/ScaleMail/Bodkin tech'lerinin `researchBuilding` alanını ([TechDefs.cs:45-52](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L45)) Blacksmith'e çevir.
- **Dokunulacak dosyalar:** `GameTypes.cs`, `BuildingDefs.cs`, `BuildingFactory.cs`, `TechDefs.cs:45-52`.
- **Kabul Kriteri:** Blacksmith kurulmadan askeri tech araştırılamaz; kurulunca tech kartında çıkar.
- **Doğrulama:** Play → Blacksmith'siz tech kartı boş → kur → tech butonları aktif olur.

### [P1] Monastery → Monk + relic (Faz C)
- **Yaklaşım:** `BuildingType.Monastery`; Monk üretir ([02](02-units.md)); relic teslimi burada toplanır ([09](09-victory-objectives.md)). **Kabul:** Monastery'den Monk çıkar; relic getirilince altın trickle başlar.

### [P2] Bina aurası + Palisade/Stone Wall ayrımı
- **Aura:** Blacksmith yakınındaki askeri binada üretim/atış hızı bonusu. **Kabul:** aura içinde üretim süresi ölçülebilir kısalır.
- **Wall çeşidi:** ucuz wood Palisade vs pahalı dayanıklı Stone Wall (`BuildingDefs` iki satır). **Kabul:** Stone Wall belirgin daha çok hp.

## 4. Referans Repolardan Notlar

- **unity-rts**: bina = base/resource-generator/unit-spawner kompozisyonu; dinamik NavMesh carving.
- **UnityTutorials-RTS**: yerleştirme/validasyon + üretim kuyruğu UI desenleri.
- **openage**: bina property'leri data-driven; garnizon/aura tablo alanı olarak modellenebilir.

## 5. Bağımlılıklar

- Garnizon → [04-combat.md](04-combat.md) (garnizonlu atış) + [07-ui-ux-qol.md](07-ui-ux-qol.md) (UI).
- Monastery → [02-units.md](02-units.md) (Monk) + [09-victory-objectives.md](09-victory-objectives.md) (relic).
- Blacksmith → [05-tech-ages.md](05-tech-ages.md) (tech yeniden konumlama).
- Wonder → [09-victory-objectives.md](09-victory-objectives.md).
