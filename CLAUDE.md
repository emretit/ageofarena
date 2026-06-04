# Age of Arena — Proje Talimatları

## ⚠️ ÖNEMLİ: Bu proje artık **Unity** projesidir

Aktif geliştirme **tamamen Unity (C#)** tarafında. AoE2-tarzı izometrik RTS.

- **Çalışma alanı:** [AgeOfArenaUnity/](AgeOfArenaUnity/) — Unity **6000.4.1f1**, **Built-in Render Pipeline** (URP/HDRP yok)
- **Kod:** [AgeOfArenaUnity/Assets/Scripts/](AgeOfArenaUnity/Assets/Scripts/) — tüm C# scriptler burada
- **Durum:** Unity Roslyn ile **0 error, 0 warning** derleniyor; Unity MCP bağlı

### Eski Three.js web sürümü kaldırıldı

Proje Three.js/TypeScript web prototipinden Unity'ye taşındı ve **eski web kaynağı
silindi** (`src/`, `index.html`, `vite.config.ts`, `package.json`, `dist/`, `node_modules/`
vb.). Bu dosyalar yalnızca **git geçmişinde** mevcut — geri getirme, yeniden oluşturma.
Bir istek "oyuna X ekle" derse hedef **her zaman Unity tarafıdır** (`AgeOfArenaUnity/`).

## Unity teknik notları

- **Sahne kod ile kurulur** — elle `.unity` sahnesi yok. `GameBootstrap.cs`
  (`[RuntimeInitializeOnLoadMethod]`) Play'e basınca `WorldRoot.Build()` ile tüm sahneyi kurar.
- **NavMesh runtime baked** — `NavMeshBuilder` low-level API, ek paket gerektirmez.
- **`activeInputHandler: 2`** (`ProjectSettings.asset`) — legacy Input + yeni Input System birlikte.
  `-1` olursa tüm `UnityEngine.Input` çağrıları patlar; değiştirme.
- **`Assets/Editor/McpForceDirectConnections.cs`** — Unity MCP direct-connection cap'ini
  reflection ile 0→8 zorlar (hesap entitlement'ı 0 dönüyor). **Silersen MCP bağlantısı kopar.**
- **`Prims.Spawn()` collider'ları siler** — unit root'larına `CapsuleCollider`, bina root'larına
  `BoxCollider` ayrıca eklenir.
- `HUD.cs` → `text.font = null` (Unity 6 default; LegacyRuntime.ttf çalışmaz).
- `ResourceManager.stone = 200` başlar (M8/STONE — AoE2-parite taş ekonomisi; Castle/University/
  kuleler taş harcar, yetersizken inşa engellenir). _Eski: stone=0 (Three.js'te stone yoktu);
  kullanıcı onayıyla 2026-06 parite için aktive edildi._

## Detaylı durum

Güncel mimari, eklenen sistemler ve yapılacaklar için [HANDOFF.md](HANDOFF.md) ve
[AgeOfArenaUnity/README.md](AgeOfArenaUnity/README.md) tek doğru kaynaktır.

İleriye dönük **gap-analizi & roadmap** (codebase ↔ tam AoE2 farkı, kategori kategori, her
madde için kabul kriteri + MCP/Play doğrulaması) için: [docs/00-overview.md](docs/00-overview.md).

## Mimari referans alınan Unity RTS repoları (kod kopyalanmadı, lisans yok)

Yalnızca yapı/yaklaşım incelemesi için:
- [FloaterTS/RTSUnityGameLicenta](https://github.com/FloaterTS/RTSUnityGameLicenta) — Unity 3D RTS
- [nefrob/unity-rts](https://github.com/nefrob/unity-rts) — basit RTS iskeleti
- [MinaPecheux/UnityTutorials-RTS](https://github.com/MinaPecheux/UnityTutorials-RTS) — seçim/inşa/kaynak eğitim serisi
- [skyteks/WarKingdoms](https://github.com/skyteks/WarKingdoms) — Warcraft 3 tarzı Unity RTS prototipi
- [mrdav30/LockstepRTSEngine](https://github.com/mrdav30/LockstepRTSEngine) — deterministik lockstep (multiplayer referansı)
- [SFTtech/openage](https://github.com/SFTtech/openage) — AoE2 motoru klonu (Python/C++; oyun mantığı/teknoloji ağacı referansı)
