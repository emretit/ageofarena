# 09 — Zafer Koşulları & Objektifler

## 1. Durum Özeti

Tek zafer koşulu var: **eliminasyon** — bir takımın Town Center'ı yıkılınca elenir; oyuncu
(takım 0) TC'si düşerse kaybeder, tüm düşman TC'leri düşerse kazanır
([MatchSystem.cs:35-55](../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L35)). Relic sistemi
mekanik olarak çalışıyor (ele geçirme + pasif altın) ama **zafer koşulu değil**. Eksik:
**Wonder zaferi**, **relic-sayısı zaferi**, **score sistemi**, conquest/king-of-the-hill ve
diplomasi (ittifak/teslim).

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Eliminasyon (TC-bazlı) | ✅ | [MatchSystem.cs:35-55](../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L35) | Core |
| Oyun-sonu pause + ekran | ✅ | [MatchSystem.cs:55](../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L55) (timeScale=0, HUD.ShowGameOver) | Core |
| Relic ele geçirme + pasif altın | ✅ | [RelicEntity.cs:45](../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L45), [RelicSystem.cs:17](../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L17) | Derinlik |
| Relic minimap işareti | ✅ | [MinimapSystem.cs](../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs) | Derinlik |
| **Wonder zaferi** (pahalı bina + geri sayım) | ❌ | — | Core |
| **Relic-sayısı zaferi** (tüm relic + süre) | ❌ | relic var ama win değil | Derinlik |
| **Score sistemi** (kaynak+birim+bina+tech) | ❌ | — | Core |
| **Conquest / King-of-the-Hill** | ❌ | — | Derinlik |
| **Diplomasi** (ittifak, tribute, vision paylaşımı) | ❌ | 4 takım sabit düşman | Derinlik |
| **Teslim ol (resign)** | ❌ | — | Derinlik |
| **Maç ayarları** (zafer tipi seçimi) | ❌ | sabit eliminasyon | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Wonder zaferi (Faz B)
- **Neden:** Eliminasyona alternatif klasik AoE2 zafer yolu; geç-oyuna hedef ve gerilim katar.
- **Yaklaşım:** `BuildingType.Wonder` ([03](03-buildings.md), çok pahalı, uzun inşa); tamamlanınca o takım için geri sayım başlar (örn. 200 "yıl" = N saniye). `MatchSystem` ([MatchSystem.cs:35](../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L35) kontrol döngüsü) Wonder geri sayımını da değerlendirir: süre dolarsa o takım kazanır; Wonder yıkılırsa sayım iptal. HUD'da geri sayım göstergesi (üst bar, relic sayacı kalıbı).
- **Dokunulacak dosyalar:** `GameTypes.cs`, `BuildingDefs.cs`, `BuildingFactory.cs`, `MatchSystem.cs`, `HUD.cs`.
- **Kabul Kriteri:** Wonder tamamlanınca geri sayım başlar ve HUD'da görünür; sayım biterse o takım zafer kazanır (oyun-sonu ekranı); Wonder düşman tarafından yıkılırsa sayım durur.
- **Doğrulama:** `Unity_ManageEditor(Play)` → Wonder kur (gerekirse test için ucuzlat) → `Unity_Camera_Capture`'da geri sayımı gör → süre dolunca/yıkınca doğru sonuç; `Unity_GetConsoleLogs(Error)` temiz.

### [P1] Score sistemi
- **Neden:** Maç durumunu okunur kılar; AI hedeflemesi ve oyun-sonu özeti için temel.
- **Yaklaşım:** Per-team skor = kaynak + birim + bina + tech ağırlıklı toplam; `MatchSystem` tick'inde ([MatchSystem.cs:16](../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L16)) periyodik hesapla; HUD'da skor rozeti, oyun-sonu ekranında döküm.
- **Dokunulacak dosyalar:** `MatchSystem.cs`, `HUD.cs`, (okuma) `ResourceManager.cs`/`TechState.cs`.
- **Kabul Kriteri:** Ekonomi/ordu büyüdükçe skor artar; oyun-sonu ekranı takım skor dökümünü gösterir.
- **Doğrulama:** Play → birim/bina üret → HUD skorunun arttığını gör; maç bitince döküm görünür.

### [P1] Relic-sayısı zaferi
- **Neden:** Var olan relic mekaniğini ([RelicSystem.cs:17](../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L17)) gerçek bir zafer yoluna bağlar.
- **Yaklaşım:** Bir takım tüm relic'leri (veya eşik sayıyı) N süre elinde tutarsa kazanır; `MatchSystem`'e relic-win dalı; HUD relic sayacı zaten var.
- **Dokunulacak dosyalar:** `MatchSystem.cs`, `RelicSystem.cs` (sahiplik sorgusu), `HUD.cs` (geri sayım).
- **Kabul Kriteri:** Tüm relic'ler tek takımda N saniye kalırsa o takım kazanır; relic kaybedilirse sayım sıfırlanır.
- **Doğrulama:** Play → tüm relic'leri ele geçir → geri sayımı izle → süre dolunca zafer; bir relic'i kaptır → sayım sıfırlanır.

### [P2] Diplomasi, teslim, conquest, maç ayarları
- **Diplomasi:** ittifak/düşman/tarafsız + vision paylaşımı + tribute ([01](01-economy.md)). **Kabul:** iki takım ittifak olunca birbirine saldırmaz, vision paylaşır.
- **Teslim (resign):** oyuncu çekilir, birimleri/binaları yok olur. **Kabul:** teslim tuşu maçı kaybettirir.
- **Conquest/KotH:** harita kontrol noktasını N süre tut. **Kabul:** kontrol noktasını tutan takım zamanla kazanır.
- **Maç ayarları:** başlangıçta zafer tipi seçimi (eliminasyon/wonder/relic/conquest). **Kabul:** seçilen koşula göre maç biter.

## 4. Referans Repolardan Notlar

- **openage**: AoE2 zafer koşulları (conquest/wonder/relic/score) referans modeli.
- **unity-rts**: gerçek-zamanlı zafer/yenilgi tespiti deseni.

## 5. Bağımlılıklar

- Wonder → [03-buildings.md](03-buildings.md) (bina) + [07-ui-ux-qol.md](07-ui-ux-qol.md) (geri sayım UI).
- Relic-win → [02-units.md](02-units.md) (Monk taşıma) + [03-buildings.md](03-buildings.md) (Monastery).
- Diplomasi/tribute → [01-economy.md](01-economy.md), [07-ui-ux-qol.md](07-ui-ux-qol.md).
- Maç ayarları → [12-maps-scenario-campaign.md](12-maps-scenario-campaign.md) (senaryo) + [10-multiplayer.md](10-multiplayer.md) (lobby).
