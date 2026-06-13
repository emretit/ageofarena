# Age of Arena — Proje Talimatları

## ⚠️ ÖNEMLİ: Aktif geliştirme artık **web/** (Three.js) tarafında

Proje Unity (C#) prototipi olarak başladı, ama 2026-06'da `web/` altındaki Three.js portu Unity
SP+MP paritesinin büyük kısmını yakaladı ve **aktif geliştirme tamamen web'e taşındı**
(kullanıcı kararı 2026-06-13). AoE2-tarzı izometrik RTS.

- **Çalışma alanı:** [web/](web/) — Vite + TypeScript + Three.js
- **Kod:** [web/src/](web/src/) — tüm `.ts` kaynak burada
- **Çalıştırma:** `npm run dev --prefix web` (port 5173; `.claude/launch.json` → "web")
- **Build/test:** `npm run build --prefix web` (tsc+vite, 0 hata olmalı), `npm run test --prefix web` (vitest)
- Bir istek "oyuna X ekle" / "web" derse hedef **varsayılan olarak `web/`'dir.**

Mimari, Unity adlarını 1:1 koruyarak port edildi (`web/src/core/Config.ts` ↔ WorldRoot sabitleri,
`CameraRig` ↔ IsometricCameraRig, `CommandBus`/`CommandExecutor` ↔ Faz 13 command pattern…).
Tüm emirler `CommandBus` tek kapısından akar (SP loopback + MP lockstep aynı sim state'i üretir).

### Unity sürümü (AgeOfArenaUnity/) — referans/arşiv, DOKUNMA

Unity projesi git'te referans olarak duruyor; **aktif geliştirilmiyor.** Geri dönülmez bir şey
yapma (silme/taşıma/dosya düzenleme) — kullanıcı açıkça istemedikçe. Aşağıdaki Unity teknik
notları yalnızca o referans sürüm içindir.

## Unity teknik notları (referans/arşiv)

- **Sahne kod ile kurulur** — elle `.unity` sahnesi yok. `GameBootstrap.cs`
  (`[RuntimeInitializeOnLoadMethod]`) Play'e basınca `WorldRoot.Build()` ile tüm sahneyi kurar.
- **NavMesh runtime baked** — `NavMeshBuilder` low-level API, ek paket gerektirmez.
- **`activeInputHandler: 2`** (`ProjectSettings.asset`) — legacy Input + yeni Input System birlikte.
  `-1` olursa tüm `UnityEngine.Input` çağrıları patlar; değiştirme.
- **`Assets/Editor/McpForceDirectConnections.cs`** — Unity MCP direct-connection cap'ini
  reflection ile 0→8 zorlar (hesap entitlement'ı 0 dönüyor). **Silersen MCP bağlantısı kopar.**
- **`Prims.Spawn()` collider'ları siler** — unit root'larına `CapsuleCollider`, bina root'larına
  `BoxCollider` ayrıca eklenir.
- **UI font:** yeni `UnityEngine.UI.Text` için `font = UiFonts.Default` kullan (`UiFonts.cs` →
  `LegacyRuntime.ttf`). Unity 6'da `font = null` **hiçbir şey render etmez** — boş menü bug'ı
  (ScenarioEditor/CampaignScreen/ReplayViewer bundan boştu). _(Eski not yanlıştı: "font=null Unity 6
  default" — değil.)_ UI butonlarında emoji kullanma; built-in font glyph'leri yok.
- `ResourceManager.stone = 200` başlar (M8/STONE — AoE2-parite taş ekonomisi; Castle/University/
  kuleler taş harcar, yetersizken inşa engellenir). _Eski: stone=0 (Three.js'te stone yoktu);
  kullanıcı onayıyla 2026-06 parite için aktive edildi._

## Detaylı durum

**Plan · backlog · DoD tek kaynağı → [docs/PLAN.md](docs/PLAN.md).** Yeni iş buradan seçilir,
`/goal` bunu ölçer. Geçmiş oturum günlüğü + mimari kararlar → [HANDOFF.md](HANDOFF.md);
Unity açma/kontroller → [AgeOfArenaUnity/README.md](AgeOfArenaUnity/README.md).

Oyunun mevcut A→Z davranışı (her stat `file:line`) → [docs/wiki/00-index.md](docs/wiki/00-index.md);
AoE2:DE kıyas bilgisi → [docs/reference/README.md](docs/reference/README.md).

## Mimari referans alınan Unity RTS repoları (kod kopyalanmadı, lisans yok)

Yalnızca yapı/yaklaşım incelemesi için:
- [FloaterTS/RTSUnityGameLicenta](https://github.com/FloaterTS/RTSUnityGameLicenta) — Unity 3D RTS
- [nefrob/unity-rts](https://github.com/nefrob/unity-rts) — basit RTS iskeleti
- [MinaPecheux/UnityTutorials-RTS](https://github.com/MinaPecheux/UnityTutorials-RTS) — seçim/inşa/kaynak eğitim serisi
- [skyteks/WarKingdoms](https://github.com/skyteks/WarKingdoms) — Warcraft 3 tarzı Unity RTS prototipi
- [mrdav30/LockstepRTSEngine](https://github.com/mrdav30/LockstepRTSEngine) — deterministik lockstep (multiplayer referansı)
- [SFTtech/openage](https://github.com/SFTtech/openage) — AoE2 motoru klonu (Python/C++; oyun mantığı/teknoloji ağacı referansı)
