# AoE2-stili HUD: dokulu üst/alt bar + alt-barda diamond minimap

**Tarih:** 2026-06-03 · **Branch:** `feat/aoe2-parity-sp` · **Durum:** Unity 6000.4.1f1 — 0 error / 0 warning, Play'de doğrulandı.

Age of Empires II HD arayüzüne benzeyen bir HUD: dokulu (ahşap) **üst bar**, her zaman görünür
**kalıcı alt bar**, alt barın sağında **eşkenar dörtgen (diamond) minimap** ve minimap üzerinden
**tıkla-navigasyon**. Tüm UI runtime'da uGUI ile kuruluyor (elle sahne paneli yok).

---

## Ne değişti (özet)

| Bölüm | Önce | Sonra |
|------|------|-------|
| **Üst bar** | 56px düz koyu renk | 64px **ahşap dokulu** (9-slice), kaynaklar ikon çerçeveli, zorluk/civ pill'leri ahşap buton |
| **Alt bar** | 210px, **sadece seçimde** görünür, info-sol+grid-sağ | 220px, **her zaman görünür**, AoE2-sadık **4 bölge**: `[komut grid] · [seçili bilgi] · [orta amblem] · [diamond minimap]` |
| **Minimap** | Ayrı canvas, sağ-alt köşe, **dikdörtgen**, OnGUI blip | Alt bara gömülü, **45° diamond**, uGUI pooled blip (birim+bina+kalıntı), IPointer ile rotasyon-doğru tıklama |

---

## Dosyalar

**Yeni:**
- [`AgeOfArenaUnity/Assets/Scripts/UiSkin.cs`](../AgeOfArenaUnity/Assets/Scripts/UiSkin.cs) — `Resources/UI` altındaki 9-slice sprite'ları yükleyen/cache'leyen **fallback'li** statik yardımcı. Kit yoksa sprite'lar `null` döner, skinning no-op olur → eski düz-renk görünüm korunur, build kırılmaz.
- [`AgeOfArenaUnity/Assets/Editor/UiSpriteImporter.cs`](../AgeOfArenaUnity/Assets/Editor/UiSpriteImporter.cs) — `AssetPostprocessor`; `Resources/UI` altındaki PNG'leri **Sprite + doğru 9-slice border + NPOT=None** olarak import eder. Bu olmadan Unity onları düz `Texture2D` (NPOT-scaled, border'sız) yapar ve `Resources.Load<Sprite>` çalışmaz.
- `AgeOfArenaUnity/Assets/Resources/UI/*.png` — Kenney **UI Pack RPG Expansion** (CC0) brown seti: `panel_brown`, `panelInset_brown`, `buttonSquare_brown(_pressed)`, `buttonLong_brown(_pressed)`.

**Düzenlendi:**
- [`AgeOfArenaUnity/Assets/Scripts/HUD.cs`](../AgeOfArenaUnity/Assets/Scripts/HUD.cs) — üst bar doku; alt bar 4-bölge + kalıcı; `public RectTransform MinimapZone` expose; `Update`'ten seçim-gating `SetActive` kaldırıldı; `RebuildCard`'a boş/idle dalı; sabitler (`BarH 210→220`, yeni `CmdZoneW`, `MinW`).
- [`AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs`](../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs) — kendi canvas'ı kaldırıldı (HUD zone'una bağlanır, yoksa fallback canvas); RawImage 45° döndürülür; OnGUI/`WorldToMinimap` yerine **kamera-tabanlı** dönüşüm + uGUI pooled blip; tıklama `MinimapClick` (IPointerClick + IDrag).

---

## Nasıl çalışıyor (kritik noktalar)

- **Diamond geometri:** Top-down kare RTT, RawImage'ı `localRotation = Euler(0,0,45)` ile döndürünce tam diamond olur; harita köşeleri N/E/S/W'ye gelir, **harita alanı kaybolmaz**.
- **Blip'ler** döndürülmüş RawImage'ın child'ı → parent rotasyonunu otomatik miras alır, diamond'a oturur. Konum `minimapCamera.WorldToViewportPoint(world)` ile bulunur → orientation/sign hatasına bağışık. Havuzlanır (her frame yeniden oluşturma yok).
- **Tıkla-navigasyon:** `MinimapClick`, `RectTransformUtility.ScreenPointToLocalPointInRectangle` ile 45° rotasyonu **otomatik** çözer → local nokta → `minimapCamera.ViewportToWorldPoint` → world. Sol-tık/sol-drag = `cameraRig.FocusOn`; sağ-tık = `command.MoveSelectedTo` (seçim varsa).
- **"Over UI" bloklaması:** Minimap artık HUD canvas'ında (GraphicRaycaster var) ve RawImage `raycastTarget=true` → `EventSystem.IsPointerOverGameObject()` true döner, `SelectionSystem`/`CommandSystem` zaten bail eder, dünya seçimini bloklamaz.
- **Build sırası:** `HUD.Init` (WorldRoot.cs:640) senkron; `MinimapSystem.Start` ilk frame'de → `MinimapZone` hazır. Yine de null-guard + fallback canvas var.

---

## Lisans (ana projeye taşırken önemli)

UI sprite'ları **Kenney "UI Pack RPG Expansion"** — **CC0 (public domain)**, atıf/zorunluluk yok.
Proje zaten CC0 Kenney 3D varlıkları kullanıyor; aynı ekosistemde kalındı. Başka pakete geçilirse
`UiSpriteImporter.BorderFor` içindeki dosya-adı → border eşlemesi güncellenmelidir.

---

## Doğrulama (yapıldı)
- **Derleme:** Unity Roslyn 0 error / 0 warning.
- **Play:** Üst bar dokulu; alt bar kalıcı 4-bölge (TownCenter seçiliyken komut grid + "Şehir Merkezi" + HP + amblem + minimap dolu); boş seçimde idle bar.
- **Minimap:** Diamond render + team-renkli blip'ler (birim/bina/kalıntı).
- **Navigasyon:** Minimap kuzey noktasına tıklama → kamera odağı `z -131→+12` kuzeye kaydı (görüntüyle de teyit).

## Gelecek / nice-to-have
- Kamera **viewport göstergesi** (minimap üzerinde görünen alan dikdörtgeni) — kapsamı dar tutmak için eklenmedi.
- Orta amblem bölgesine **civ arması** görseli (şu an sadece "AGE OF ARENA" yazısı).
- Border değerleri Play'de göz kararı; istenirse ince ayar (`UiSpriteImporter.BorderFor`).
