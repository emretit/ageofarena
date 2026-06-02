# 10 — Multiplayer & Lockstep

## 1. Durum Özeti

Proje **tamamen single-player**: takım 0 oyuncu, 1-3 `EnemyAI`. Hiçbir ağ katmanı yok ve
simülasyon **deterministik değil** (float fizik, `Time.deltaTime`, `Math.Random` kullanımı,
NavMesh runtime bake). Multiplayer en uzun-vadeli (P2) hedef; bu dosya **mimari kararı** ve
determinizm ön-koşullarını belgeler — çünkü determinizm gereksinimi ileride combat/AI tasarımını
etkiler, geç fark edilmemeli.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Olay yayını (komut→sistem) iskeleti | 🟡 | [GameEvents.cs:9-12](../AgeOfArenaUnity/Assets/Scripts/GameEvents.cs#L9) | — |
| Self-healing GameManager.Instance | ✅ | [GameManager.cs](../AgeOfArenaUnity/Assets/Scripts/GameManager.cs) | — |
| **Ağ katmanı** (transport/sync) | ❌ | — | Core (MP hedefse) |
| **Deterministik simülasyon** (fixed-point, sabit tick) | ❌ | float + deltaTime | Core (MP hedefse) |
| **Lockstep komut kuyruğu** (turn-based input) | ❌ | komutlar anında uygulanıyor | Core (MP hedefse) |
| **Lobby / oyuncu listesi / ready** | ❌ | — | Core (MP hedefse) |
| **Desync tespiti** (state hash karşılaştırma) | ❌ | — | Core (MP hedefse) |
| **Lag compensation / reconnect** | ❌ | — | Derinlik |
| **Replay (komut kaydı)** | ❌ | — | Derinlik |

## 3. Eksikler — Öncelikli Backlog

> Bütünüyle P2 (uzun vade). Ama **mimari karar ve determinizm ön-koşulu** erken alınmalı:
> bu kararlar [04-combat.md](04-combat.md) ve [06-ai.md](06-ai.md) tasarımını etkiler.

### [P2] Mimari karar: Lockstep vs Client-Server
- **Neden:** İki yol radikal farklı kod gerektirir; geç değişim pahalıdır.
- **Yaklaşım/Trade-off:**
  - **Deterministik Lockstep** (LockstepRTSEngine deseni): her istemci aynı simülasyonu çalıştırır, sadece input senkronlanır → düşük bant genişliği, çok birim ölçeklenir; ama **tam determinizm** şart (fixed-point matematik, sabit tick, sıralı iterasyon, NavMesh determinizmi). RTS için kanonik.
  - **Client-Server / state-sync** (unity-rts/Mirror deseni): sunucu otorite, state yayar → kolay yazılır, determinizm gerekmez; ama bant genişliği birim sayısıyla büyür, RTS ölçeğinde zorlanır.
- **Karar çıktısı:** Bu doküma bir ADR (architecture decision record) eklenir; öneri: hedef ciddi MP ise **Lockstep**, hızlı co-op denemesi ise **Mirror**.
- **Kabul Kriteri:** `docs/`'ta seçilen yaklaşım ve gerekçesi yazılı; etkilenen sistemler (combat/AI/pathfinding) için determinizm gereksinim listesi çıkarılmış.
- **Doğrulama:** Kod yok — karar dokümanı review edilir; "ne deterministik olmalı" envanteri tamamlandı.

### [P2] Determinizm ön-koşulu (lockstep seçilirse)
- **Neden:** Mevcut kod determinist değil; MP'den önce bu borç ödenmeli.
- **Yaklaşım:** Sabit tick (`FixedUpdate`/manuel accumulator); fixed-point veya kontrollü float; `Math.Random`→seedli deterministik RNG; sistem iterasyon sırası sabit (id-sıralı); NavMesh yerine deterministik grid/flow-field pathfinding değerlendir (LockstepRTS influence map).
- **Dokunulacak dosyalar:** `GameManager.cs` (tick), tüm `*.Tick(dt)` sistemleri, `CombatSystem.cs`, `EnemyAI.cs` (RNG), pathfinding katmanı.
- **Kabul Kriteri:** Aynı seed + aynı input ile iki çalıştırma bit-bit aynı state üretir (state hash eşleşir).
- **Doğrulama:** Aynı senaryoyu 2× çalıştır → her N tick'te state hash'i logla → `Unity_GetConsoleLogs` ile hash'lerin özdeş olduğunu doğrula.

### [P2] Transport + lobby + desync tespiti
- **Yaklaşım:** Lockstep ise hafif UDP/relay + turn buffer; lobby (oyuncu listesi/ready/ayar); her K tick'te state hash mübadelesi → uyuşmazlıkta uyarı.
- **Kabul Kriteri:** 2 istemci aynı maça girer, komutlar senkron uygulanır, kasıtlı bozma desync uyarısı tetikler.
- **Doğrulama:** İki instance (editor + build) bağlanır; bir tarafta komut → diğerinde aynı sonuç; manuel state bozma → desync log.

## 4. Referans Repolardan Notlar

- **LockstepRTSEngine**: deterministik 2D fizik (X-Z), fixed-point math, lockstep değişkenleri, Forge Networking, influence-map pathfinding — lockstep yolu için birebir referans.
- **unity-rts**: Mirror + FizzySteamworks P2P, RenderTexture FOW — client-server yolu için pragmatik örnek.
- **openage**: event-loop + turn/time modeli — komut zamanlama soyutlaması.

## 5. Bağımlılıklar

- Determinizm → [04-combat.md](04-combat.md) (hasar/RNG), [06-ai.md](06-ai.md) (AI RNG), pathfinding.
- Lobby/maç ayarı → [09-victory-objectives.md](09-victory-objectives.md) (zafer tipi) + [11-civilizations-balance.md](11-civilizations-balance.md) (civ seçimi).
