# 01 — Ekonomi & Kaynak Yönetimi

## 1. Durum Özeti

4 kaynak (food/wood/gold/stone) per-team `ResourceManager` ile takip ediliyor; köylüler
`GatherSystem` ile düğümlerden toplayıp en yakın uygun depoya bırakıyor (carry capacity +
tech çarpanı). Çiftlikler tükenince yeniden tohumlanıyor. `MarketSystem` sabit kurla kaynak
alıp satıyor. Eksik olan: ekonomiyi **derinleştiren** AoE2 katmanları — ticaret arabası/rota,
pasif gelir, kaynak çeşidi (berry/deer/fish), idle-worker yönetimi ve uzaklık verimi.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| 4 kaynak + per-team ledger | ✅ | [ResourceManager.cs:11-19](../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L11) | Core |
| `CanAfford`/`Deduct` harcama | ✅ | [ResourceManager.cs:43-46](../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L43) | Core |
| Köylü toplama döngüsü + carry + dropoff | ✅ | [GatherSystem.cs:43](../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L43) | Core |
| Depo maskesi (LumberCamp=wood, Mill=food…) | ✅ | [BuildingDefs.cs:60-62](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L60) | Core |
| Çiftlik reseed (renewable node) | ✅ | [ResourceNode.cs](../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs) | Core |
| Market (sabit kur al/sat) | ✅ | [MarketSystem.cs:10](../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L10) | Core |
| Tech gather çarpanı (DoubleBitAxe/Wheelbarrow) | ✅ | [TechDefs.cs:47-48](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L47) | Derinlik |
| Relic pasif altın trickle | ✅ | [RelicEntity.cs:45](../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L45) | Derinlik |
| **Trade Cart + ticaret rotası** | ❌ | — | Derinlik |
| **Idle-worker tespiti/yönetimi** | ❌ | — | Core (QoL) |
| **Dalgalı market fiyatı (arz-talep)** | ❌ | MarketSystem sabit kur | Derinlik |
| **Kaynak çeşidi** (berry/deer/fish/forage) | ❌ | tek tip tree/mine/farm | Derinlik |
| **Drop-off uzaklık verimi/penaltı** | ❌ | — | Derinlik |
| **Tribute (oyuncular arası kaynak transferi)** | ❌ | — | Derinlik |
| **Çiftlik decay / yeniden ekim maliyeti** | ❌ | reseed bedava | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Trade Cart + ticaret rotası (Faz C)
- **Neden:** Geç-oyun altın ekonomisinin AoE2 imzası; Market'i pasif gelir motoruna çevirir.
- **Yaklaşım:** `UnitType`'a `TradeCart` ekle ([GameTypes.cs:9](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L9)); Market'i kaynak binası olarak işaretle; cart Market↔müttefik/uzak-Market arası gidip gelir, mesafeye orantılı altın bırakır. Mevcut `GatherSystem` dropoff döngüsü ([GatherSystem.cs:43](../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L43)) "git-getir" iskeleti olarak yeniden kullanılır; üretim `MarketSystem`/`TrainingQueue` üzerinden.
- **Dokunulacak dosyalar:** `GameTypes.cs`, `UnitFactory.cs`, `MarketSystem.cs`, `GatherSystem.cs`, `BuildingDefs.cs` (Market kaynak rolü), `HUD.cs` (üretim butonu).
- **Kabul Kriteri:** Market seçilip Trade Cart üretilince cart iki market arasında otomatik mekik yapar ve her tur `gold` artar; mesafe arttıkça tur başına altın artar.
- **Doğrulama:** `Unity_ManageEditor(Play)` → Market+2. market kur → cart üret → `Unity_Camera_Capture` ile mekik hareketi gözle; HUD altın sayacının periyodik arttığını doğrula; `Unity_GetConsoleLogs(Error)` temiz.

### [P1] Idle-worker tespiti & döngüsel seçim
- **Neden:** Ekonomi mikrosunun en temel QoL'u; boşta köylü = kayıp kaynak.
- **Yaklaşım:** `UnitState.Idle` ([GameTypes.cs:7](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L7)) + `type==Villager` filtreleyen sayaç; HUD'a buton + `.` hotkey ile sıradaki idle köylüye kamera+seçim. Detay UI tarafı [07-ui-ux-qol.md](07-ui-ux-qol.md) ile ortak.
- **Dokunulacak dosyalar:** `HUD.cs`, `SelectionSystem.cs`, `IsometricCameraRig.cs`.
- **Kabul Kriteri:** En az 1 köylü Idle iken HUD'da sayaç >0 görünür; `.` her basışta farklı idle köylüyü seçip kamerayı oraya taşır; idle yokken buton pasif.
- **Doğrulama:** Play → birkaç köylüyü durdur → idle sayacı artar → `.` ile döngüyü Camera.Capture'da gözle.

### [P2] Dalgalı market fiyatı (arz-talep)
- **Neden:** Sabit kur sömürülebilir; AoE2'de her satış oranı kötüleştirir.
- **Yaklaşım:** `MarketSystem`'e kaynak başına `rate` state'i ekle; her al/sat oranı kademeli kaydırır, zamanla toparlar.
- **Dokunulacak dosyalar:** `MarketSystem.cs` (stateless→stateful), `HUD.cs` (oran göstergesi).
- **Kabul Kriteri:** Aynı kaynağı arka arkaya satınca her seferinde daha az altın gelir; bekleyince oran normale döner.
- **Doğrulama:** Play → market hotkey ile 5× wood sat → HUD'da gelen altının azaldığını izle.

### [P2] Kaynak çeşidi (berry/deer/fish/forage)
- **Neden:** Harita okuma ve erken-oyun yer seçimini derinleştirir.
- **Yaklaşım:** `ResourceNode`'a alt-tür + farklı gather hızı/miktarı; `ResourceFactory`'ye mesh varyantları; `WorldRoot` spawn'ına dağıtım.
- **Dokunulacak dosyalar:** `ResourceNode.cs`, `ResourceFactory.cs`, `WorldRoot.cs`, `GatherSystem.cs`.
- **Kabul Kriteri:** Haritada en az 2 farklı food kaynağı (berry bush + deer) görünür ve farklı hızda toplanır.
- **Doğrulama:** Play → SceneView.Capture2DScene ile kaynak çeşidini gör; köylüyü her birine yolla, toplama hızını gözle.

### [P2] Tribute & çiftlik decay
- **Tribute:** Müttefik/diplomasi gelince (bkz [09](09-victory-objectives.md)) `ResourceManager` arası transfer + %30 vergi. **Kabul:** kaynak gönderince karşı tarafın ledger'ı artar, gönderenin azalır.
- **Çiftlik decay:** reseed'i ücretli yap (food maliyeti) — sonsuz bedava food'u dengeler. **Kabul:** çiftlik tükenince otomatik reseed yerine maliyet ister.

## 4. Referans Repolardan Notlar

- **openage**: kaynak/gather davranışı nyan veri nesneleri olarak modellenir — bizim
  `ResourceNode` alt-türlerini de data-driven tutmak (tablo) ileride civ bonuslarına ([11](11-civilizations-balance.md)) bağlanmayı kolaylaştırır.
- **unity-rts / UnityTutorials-RTS**: depo binası + dropoff resource-generator deseni; idle-worker ve resource UI örnekleri.

## 5. Bağımlılıklar

- Trade Cart → [02-units.md](02-units.md) (yeni birim tipi) + [03-buildings.md](03-buildings.md) (Market rolü).
- Idle-worker → [07-ui-ux-qol.md](07-ui-ux-qol.md) (UI/hotkey).
- Tribute → [09-victory-objectives.md](09-victory-objectives.md) (diplomasi paneli).
- Kaynak çeşidi → [12-maps-scenario-campaign.md](12-maps-scenario-campaign.md) (harita üretimi dağıtımı).
