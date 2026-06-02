# 08 — Ses · Animasyon · VFX

## 1. Durum Özeti

Görsel efekt katmanı var ama **ses tamamen yok**. `VisualEffectSystem`
([VisualEffectSystem.cs:9](../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L9))
`GameEvents`'e abone olup ölüm partikülü + kamera sarsıntısı üretiyor; inşaat animasyonu ve
çağ popup'ı var. Birim/bina mesh'leri **statik** (procedural, animasyon yok). En büyük boşluk:
sıfırdan bir **ses sistemi** (SFX + müzik) ve ikincil olarak birim animasyonu. Bu kategori
büyük oranda *polish* ama oyun hissini en çok yükseltecek alan.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Ölüm partikülü + kamera shake | ✅ | [VisualEffectSystem.cs:9](../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L9) | Derinlik |
| Event-güdümlü VFX (OnUnitKilled vb.) | ✅ | [GameEvents.cs:9-12](../AgeOfArenaUnity/Assets/Scripts/GameEvents.cs#L9) | Derinlik |
| İnşaat animasyonu (yükselen mesh) | ✅ | [BuildSystem.cs](../AgeOfArenaUnity/Assets/Scripts/BuildSystem.cs) | Derinlik |
| ParticleSystem modülü (manifest) | ✅ | manifest.json (com.unity.modules.particlesystem) | — |
| **Ses sistemi (AudioManager)** | ❌ | yok | Derinlik (yüksek etki) |
| **Birim SFX** (seçim/komut/saldırı/ölüm) | ❌ | — | Derinlik |
| **Bina SFX** (inşaat/tamamlanma/yıkım) | ❌ | — | Derinlik |
| **Ambient müzik** (çağa göre track) | ❌ | — | Derinlik |
| **UI sesleri** (tık/onay/hata) | ❌ | — | Derinlik |
| **Birim animasyonu** (yürüme/saldırı/ölüm) | ❌ | mesh statik | Derinlik |
| **Bina hasar görseli** (duman/çatlak) | ❌ | — | Derinlik |
| **Mekansal ses (3D AudioSource)** | ❌ | — | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Ses sistemi temeli (AudioManager + event köprüsü)
- **Neden:** En düşük maliyetli en yüksek "oyun hissi" kazancı; altyapı `GameEvents` zaten hazır.
- **Yaklaşım:** Yeni `AudioManager` (singleton, GameManager'a bağlı) `GameEvents` ([GameEvents.cs:9-12](../AgeOfArenaUnity/Assets/Scripts/GameEvents.cs#L9)) eventlerine abone olur: OnUnitKilled→ölüm sesi, OnBuildingDestroyed→yıkım, OnAgeAdvanced→fanfar, OnResearchCompleted→ding. Bir/iki müzik AudioSource + SFX havuzu (3D `AudioSource.PlayClipAtPoint`). Clip'ler `Resources/Audio` veya küçük procedural ton (asset-free çizgiyle uyumlu) — telifsiz/proc tercih.
- **Dokunulacak dosyalar:** yeni `AudioManager.cs`, `GameManager.cs` (kayıt), opsiyonel `GameEvents.cs` (gerekirse OnUnitTrained/OnAttack eventi ekle).
- **Kabul Kriteri:** Birim ölünce/bina yıkılınca/çağ atlayınca/tech bitince ilgili ses çalar; arkada ambient müzik döner; ses olayların konumunda mekansal duyulur.
- **Doğrulama:** `Unity_ManageEditor(Play)` → çatışma çıkar → ölüm/yıkım seslerini duy (veya `Unity_GetConsoleLogs`'ta AudioManager log'u); çağ atla → fanfar; `Unity_GetConsoleLogs(Error)` temiz (eksik clip uyarısı yok).

### [P1] Birim/bina/UI SFX seti
- **Neden:** Komut geri bildirimi (seçim/move/attack sesi) RTS okunabilirliğini artırır.
- **Yaklaşım:** `SelectionSystem`/`CommandSystem` çağrılarına AudioManager hook: seçim "tık", move "onay", saldırı "kılıç/ok". Bina inşa başlangıcı/tamamlanması `BuildSystem`'den. UI butonları `HUD`'dan tık/hata sesi.
- **Dokunulacak dosyalar:** `AudioManager.cs`, `SelectionSystem.cs`, `CommandSystem.cs`, `BuildSystem.cs`, `HUD.cs`.
- **Kabul Kriteri:** Birim seçince/komut verince farklı sesler; geçersiz yerleştirmede hata sesi; bina tamamlanınca onay sesi.
- **Doğrulama:** Play → birim seç/komut ver/geçersiz bina koy → her etkileşimde uygun ses; konsol temiz.

### [P2] Birim animasyonu
- **Neden:** Statik mesh "oyuncak" hissi veriyor; basit prosedürel animasyon canlandırır.
- **Yaklaşım:** Asset-free çizgiyle uyumlu: kod-tabanlı bob/sway (yürürken hafif sallanma), saldırıda kısa "lunge" tween, ölümde devrilme. `UnitEntity` state'ine ([GameTypes.cs:7](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L7) `UnitState`) bağlı. Tam iskelet animasyonu kapsam dışı (mesh procedural).
- **Dokunulacak dosyalar:** `UnitEntity.cs` veya yeni `UnitAnimator.cs`, `Prims.cs` (parça referansı).
- **Kabul Kriteri:** Yürüyen birim sallanır, saldıran birim öne atılır, ölen birim devrilir — frame drop olmadan.
- **Doğrulama:** Play → birimleri hareket/savaş içinde `Unity_Camera_Capture` ardışık kareleriyle hareketi gözle; Profiler'da maliyet ihmal edilebilir.

### [P2] Bina hasar görseli + mekansal ses ince ayarı
- **Hasar görseli:** hp düştükçe duman partikülü / çatlak renk. **Kabul:** hasarlı bina gözle görülür duman çıkarır.
- **Mekansal ses:** kameradan uzak olaylar daha kısık. **Kabul:** ekran dışı çatışma kısık duyulur.

## 4. Referans Repolardan Notlar

- **UnityTutorials-RTS**: ses optimizasyonu bölümü (AudioSource havuzu, mesafe-bazlı kısma) doğrudan referans.
- **openage**: Opus codec ile ses; bizde Unity AudioClip yeterli.

## 5. Bağımlılıklar

- Ses event'leri → [05-tech-ages.md](05-tech-ages.md) (OnResearch/OnAge) + [04-combat.md](04-combat.md) (saldırı/ölüm).
- UI sesleri → [07-ui-ux-qol.md](07-ui-ux-qol.md).
- Animasyon → [02-units.md](02-units.md) (birim davranış state'i).
