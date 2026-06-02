# 07 — UI/UX & Yaşam Kalitesi (QoL)

## 1. Durum Özeti

AoE2-tarzı komut barı çalışıyor: sol bilgi paneli + hp barı, sağ 5×3 komut kartı, üretim
kuyruğu şeridi, hover tooltip, çağ popup, oyun-sonu ekranı, Türkçe lokalizasyon
([HUD.cs:15](../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L15)). Seçim (tek+shift+drag-box),
sağ-tık komut, isometric kamera, minimap (RenderTexture, takım renkli noktalar) var. Eksik
olan **mikro-yönetim QoL'u**: control group (1-9), idle-worker butonu, minimap click-to-pan,
attack-stance ikonları ve özelleştirilebilir hotkey.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Komut barı (info + 5×3 kart) | ✅ | [HUD.cs:25-54](../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L25) | Core |
| Üst kaynak/çağ/relic barı | ✅ | [HUD.cs](../AgeOfArenaUnity/Assets/Scripts/HUD.cs) | Core |
| Üretim kuyruğu görseli + iptal | ✅ | [TrainingQueue.cs:90](../AgeOfArenaUnity/Assets/Scripts/TrainingQueue.cs#L90), [HUD.cs](../AgeOfArenaUnity/Assets/Scripts/HUD.cs) | Core |
| Hover tooltip + çağ popup + game over | ✅ | [HUD.cs](../AgeOfArenaUnity/Assets/Scripts/HUD.cs) | Core |
| Seçim (tek/shift/drag-box) | ✅ | [SelectionSystem.cs:32](../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L32) | Core |
| Sağ-tık komut + formasyon + marker | ✅ | [CommandSystem.cs](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs) | Core |
| Rally bayrağı | ✅ | [CommandSystem.cs:291](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L291) | Core |
| Isometric kamera (pan/zoom/rotate/shake) | ✅ | [IsometricCameraRig.cs](../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs) | Core |
| Minimap (RenderTexture, noktalar) | ✅ | [MinimapSystem.cs:10](../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L10) | Core |
| **Control group (1-9 ata/çağır)** | ❌ | — | Core (QoL) |
| **Idle-worker butonu + döngü** | ❌ | — | Core (QoL) |
| **Minimap click-to-pan / sağ-tık komut** | ❌ | sadece çizim [MinimapSystem.cs:20](../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L20) | Core (QoL) |
| **Attack-stance ikonları** | ❌ | stance yok, bkz [04](04-combat.md) | Core |
| **Çift-tık = ekrandaki tüm aynı tip** | ❌ | — | Derinlik |
| **Garnizon paneli (sayaç + ungarrison)** | ❌ | garnizon yok, bkz [03](03-buildings.md) | Core |
| **Özelleştirilebilir hotkey + patrol** | ❌ | hotkey sabit | Derinlik |
| **Oyun hızı kontrolü (pause/slow/fast)** | ❌ | — | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Control group (1-9)
- **Neden:** RTS mikro-yönetiminin en temel QoL'u; orduyu gruplayıp anında çağırmak.
- **Yaklaşım:** `SelectionSystem`'e ([SelectionSystem.cs:32](../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L32)) `Dictionary<int,List<UnitEntity>>`; Ctrl+1..9 = ata, 1..9 = seç, çift-bas = seç+kamera odakla. Ölü birimleri otomatik temizle.
- **Dokunulacak dosyalar:** `SelectionSystem.cs`, `IsometricCameraRig.cs` (odak), opsiyonel `HUD.cs` (grup rozetleri).
- **Kabul Kriteri:** Ctrl+1 ile seçili birimler gruba atanır; 1'e basınca tekrar seçilir; çift-bas kamerayı gruba taşır; grup üyesi ölünce listeden düşer.
- **Doğrulama:** `Unity_ManageEditor(Play)` → 5 birim seç → Ctrl+1 → boşa tıkla → 1 → `Unity_Camera_Capture`'da aynı 5 birimin seçili olduğunu gör.

### [P1] Idle-worker butonu + döngü
- **Neden:** Boşta köylü = kayıp ekonomi; [01](01-economy.md) ile ortak QoL.
- **Yaklaşım:** `UnitState.Idle` + Villager sayan sayaç; HUD'da buton + `.` hotkey sıradaki idle köylüye seç+kamera. Idle yoksa buton pasif/gizli.
- **Dokunulacak dosyalar:** `HUD.cs`, `SelectionSystem.cs`, `IsometricCameraRig.cs`.
- **Kabul Kriteri:** ≥1 idle köylü varken sayaç görünür; `.` döngüsel seçer + kamerayı taşır; idle yokken buton pasif.
- **Doğrulama:** Play → köylüleri durdur → sayaç artar → `.` döngüsünü Camera.Capture'da izle.

### [P1] Minimap click-to-pan + sağ-tık komut
- **Neden:** Minimap çiziliyor ama tıklanamıyor — harita gezme/komut hız kaybı.
- **Yaklaşım:** `MinimapSystem._screenRect` ([MinimapSystem.cs:20](../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L20)) içinde tıklamayı dünya koordinatına çevir; sol-tık kamerayı taşı, sağ-tık seçili birimlere move emri ver (CommandSystem köprüsü).
- **Dokunulacak dosyalar:** `MinimapSystem.cs`, `IsometricCameraRig.cs`, `CommandSystem.cs`.
- **Kabul Kriteri:** Minimap'e sol-tık kamerayı o noktaya taşır; birim seçiliyken sağ-tık o noktaya yürüme emri verir.
- **Doğrulama:** Play → minimap köşesine tıkla → kameranın oraya kaydığını gör; birim seçip minimap'e sağ-tık → birimin oraya yürüdüğünü izle.

### [P1] Attack-stance ikonları + garnizon paneli
- **Stance:** [04](04-combat.md) stance'i gelince HUD'a 4 ikon (aggressive/defensive/stand/no-attack) + hotkey. **Kabul:** seçili birimin stance'i ikonla görünür ve tıkla/hotkey ile değişir.
- **Garnizon paneli:** [03](03-buildings.md) garnizonu gelince bina seçilince "N/cap" + ungarrison butonu. **Kabul:** garnizonlu bina seçilince sayaç ve çıkar butonu görünür.

### [P2] Çift-tık tüm-aynı-tip, özelleştirilebilir hotkey, patrol, oyun hızı
- **Çift-tık:** ekrandaki aynı tip birimleri seç. **Kabul:** bir okçuya çift-tık → ekrandaki tüm okçular seçili.
- **Patrol:** iki nokta arası devriye emri. **Kabul:** patrol verilen birim iki nokta arası gider-gelir.
- **Oyun hızı:** `Time.timeScale` slow/normal/fast (MatchSystem zaten timeScale kullanıyor, [MatchSystem.cs](../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs)). **Kabul:** hız tuşu simülasyonu hızlandırır/yavaşlatır.
- **Hotkey özelleştirme:** tuş haritası ayarı. **Kabul:** kullanıcı bir komutun tuşunu değiştirip kaydedebilir.

## 4. Referans Repolardan Notlar

- **unity-rts**: minimap + RenderTexture vision + seçim/drag-box; minimap-pan deseni doğrudan örnek.
- **UnityTutorials-RTS**: üretim kuyruğu UI, kaynak paneli, control group örnekleri.

## 5. Bağımlılıklar

- Idle-worker → [01-economy.md](01-economy.md).
- Stance ikonu → [04-combat.md](04-combat.md).
- Garnizon paneli → [03-buildings.md](03-buildings.md).
- UI sesleri → [08-audio-animation-vfx.md](08-audio-animation-vfx.md).
- Diplomasi paneli → [09-victory-objectives.md](09-victory-objectives.md).
