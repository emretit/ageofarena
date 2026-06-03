# Age of Arena — Gap-Analizi & Roadmap (İndeks)

> Bu klasör (`docs/`), **mevcut codebase ile tam bir AoE2-tarzı RTS arasındaki farkı**
> kategori kategori belgeler. Her dosya bir sonraki geliştirme adımının **planıdır**:
> ne var / ne eksik, öncelik (P0/P1/P2), önerilen yaklaşım, kabul kriteri ve MCP/Play
> doğrulaması içerir. Kod yazımı bu dokümanlardan **sonra**, ayrı adımlarda yapılır.

**Tek doğru kaynak hâlâ:** [HANDOFF.md](../HANDOFF.md) (oturum geçmişi + mimari) ve
[CLAUDE.md](../CLAUDE.md) (teknik notlar). Bu klasör onların üstüne *ileriye dönük* katmandır.

---

## Okuma sırası

| # | Dosya | Kapsam | Baskın öncelik |
|---|---|---|---|
| 01 | [Ekonomi & Kaynak](01-economy.md) | Toplama, depo, market, trade cart, idle worker | P1 |
| 02 | [Birimler & Counter](02-units.md) | Birim çeşitliliği, zırh tipleri, Monk, naval | P1 |
| 03 | [Binalar & Garnizon](03-buildings.md) | Garnizon, kule, repair, Blacksmith/Monastery | **P0**→P1 |
| 04 | [Savaş & Kuşatma](04-combat.md) | Hasar↔zırh matrisi, stance, formasyon | P1 |
| 05 | [Tech & Çağlar](05-tech-ages.md) | Imperial çağ, University tech, research queue | P1 |
| 06 | [AI Derinliği](06-ai.md) | Zorluk seviyeleri, Medic/Scout kullanımı, stance | P1 |
| 07 | [UI/UX & QoL](07-ui-ux-qol.md) | Control group, idle worker, minimap-pan, stance ikonu | P1 |
| 08 | [Ses · Animasyon · VFX](08-audio-animation-vfx.md) | Ses sistemi (sıfırdan), müzik, animasyon | P1 |
| 09 | [Zafer & Objektif](09-victory-objectives.md) | Wonder, relic-win, score, conquest | P1 |
| 10 | [Multiplayer](10-multiplayer.md) | Lockstep vs client-server, determinizm, lobby | P2 |
| 11 | [Medeniyet & Balance](11-civilizations-balance.md) | Civ bonus, unique unit/tech, balance | P2 |
| 12 | [Harita · Senaryo · Kampanya](12-maps-scenario-campaign.md) | Save/load, prosedürel harita, senaryo, kampanya | P1→P2 |

---

## Cross-cutting bilinen durumlar (önce bunlara bak)

> ⚠️ Plan aşamasında HANDOFF'tan gelen iki "P0 build bug" iddiası kod tabanında
> **doğrulandı ve güncellendi** — bayat çıktılar. Gerçek durum:

| Eski iddia | Gerçek durum (kodla doğrulandı) | Sonuç |
|---|---|---|
| 🔴 Build kırık: `UpdateRallyFlag` tanımsız | `UpdateRallyFlag` **tanımlı** — [CommandSystem.cs:291](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L291), [çağrı:43](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L43). Rally feature working-tree'de **tamamlanmış** (+122 satır, commit edilmemiş). | ✅ Çözülmüş — sadece commit'lenecek |
| 🟠 Medic AI crash | AI crash etmiyor; sadece **Medic/Scout üretmiyor** — üretim rotasyonu Militia→Archer→Cavalry→Trebuchet, [EnemyAI.cs:220](../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L220); `IsMilitary` [EnemyAI.cs:574](../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L574) Medic/Scout'u dışlıyor. | İçerik boşluğu → [06-ai.md](06-ai.md) |

**Gerçek P0 (çekirdek eksik, build sağlam):** Garnizon mekaniği yok → [03-buildings.md](03-buildings.md).

---

## Gap-skoru özeti

> Yüzdeler "tam AoE2 hissi"ne göre kabaca; mevcut implementasyon codebase'de doğrulanmıştır.

| Kategori | Olgunluk | Ana eksik |
|---|---|---|
| 01 Ekonomi | ~75% | Trade cart, idle worker, taxation, kaynak çeşidi |
| 02 Birimler | ~65% | Zırh tipleri, Monk dönüştürme, naval, özel yetenek |
| 03 Binalar | ~70% | **Garnizon (P0)**, kule, repair, Blacksmith/Monastery |
| 04 Savaş | ~60% | Hasar↔zırh matrisi, stance, formasyon kohezyonu |
| 05 Tech & Çağ | ~70% | **Imperial çağ**, University tech, research queue |
| 06 AI | ~70% | Zorluk seviyeleri, Medic/Scout, stance bilinci |
| 07 UI/UX & QoL | ~55% | Control group, idle worker, minimap-pan, stance ikon |
| 08 Ses/Anim/VFX | ~10% | **Ses sistemi sıfır**, müzik, birim animasyonu |
| 09 Zafer/Objektif | ~55% | **Wonder**, relic-win, score, conquest |
| 10 Multiplayer | ~0% | Lockstep/networking altyapısı |
| 11 Medeniyet | ~0% | Civ veri yapısı, bonus, unique unit/tech |
| 12 Harita/Senaryo | ~30% | Save/load, prosedürel harita, senaryo editörü |

---

## Mevcut codebase'in temel genişleme noktaları

Yeni içerik çoğunlukla **mimari değiştirmeden veri tablolarına satır** ekleyerek gelir:

- **Enum'lar:** [GameTypes.cs](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs) — `ResourceKind`(L5), `UnitType`(L9), `BuildingType`(L11), `Age`(L14), `TechType`(L26)
- **Bina tablosu:** [BuildingDefs.cs:54-70](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L54) — 13 satır, her bina bir `new(...)`
- **Tech tablosu:** [TechDefs.cs:42-58](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L42) — 17 tech
- **Event hub:** [GameEvents.cs:9-12](../AgeOfArenaUnity/Assets/Scripts/GameEvents.cs#L9) — 4 event (ses sistemi buraya abone olur)
- **Canlı bonus:** [TechState.cs](../AgeOfArenaUnity/Assets/Scripts/TechState.cs) — tech/çağ bonusu runtime okunur
- **Procedural mesh:** [Prims.cs](../AgeOfArenaUnity/Assets/Scripts/Prims.cs) + `*Factory.cs` — asset'siz görsel deseni

---

## AoE2 Kaynak Referans Dokümantasyonu

`docs/reference/` klasörü AoE2'nin gerçek mekaniklerini sayısal detayla saklar.
Gap-analiz dosyaları "AoA'da ne var?" diye sorarken, reference "AoE2'de gerçekte ne var?" sorusuna cevap verir.

| Dosya | İçerik |
|---|---|
| [reference/01-civilizations.md](reference/01-civilizations.md) | 45 medeniyetin tam tablosu (unique unit, tech, takım bonusu) |
| [reference/02-units-upgrade-chains.md](reference/02-units-upgrade-chains.md) | Militia/Archer/Scout/Knight/Siege hatları + stat'lar |
| [reference/03-buildings-by-age.md](reference/03-buildings-by-age.md) | Çağa göre tüm binalar: maliyet, HP, üretim/araştırma |
| [reference/04-tech-tree.md](reference/04-tech-tree.md) | Ekonomi + askeri teknoloji ağacı (bina, çağ, maliyet, etki) |
| [reference/05-economy-trade.md](reference/05-economy-trade.md) | Kaynak sistemi, çiftlik yönetimi, market, trade cart, relik |
| [reference/06-victory-game-modes.md](reference/06-victory-game-modes.md) | Zafer koşulları + oyun modları |
| [reference/07-unit-counter-system.md](reference/07-unit-counter-system.md) | Rock-paper-scissors counter matrisi + bonus damage değerleri |

---

## Oyun Wiki (AoA gerçek implementasyonu)

`docs/wiki/` klasörü AoA'nın **şu an gerçekte nasıl çalıştığını** sayfa sayfa, her stat
`file:line` referanslı olarak belgeler (O26'da çok-ajanlı workflow + adversarial stat denetimi
ile üretildi). Üç katman birbirini tamamlar: **reference** = "AoE2'de ne var", **bu klasör
(01-12)** = "ne eksik / nasıl kapatılır", **wiki** = "BİZDE ne var, nasıl çalışıyor".

→ Başlangıç: [wiki/00-index.md](wiki/00-index.md) — 11 kategori sayfası + okuma sırası + 4-katman diyagramı

Wiki sayfalarının §8 "Eksikler" bölümleri tek dosyada toplandı:
[wiki/99-backlog.md](wiki/99-backlog.md) (72 tekil madde). Bu, P3 backlog kaynağıdır;
onaylananlar [HANDOFF.md](../HANDOFF.md) P3 tablosuna taşınır.

---

## Referans repolar (kod kopyalanmadı, yalnızca yaklaşım)

- **openage** — data-driven (nyan DSL), event-loop → [11](11-civilizations-balance.md), [05](05-tech-ages.md)
- **LockstepRTSEngine** — deterministik fixed-point lockstep → [10](10-multiplayer.md)
- **unity-rts** — Mirror + RenderTexture FOW → [10](10-multiplayer.md)
- **UnityTutorials-RTS** — behavior tree, save/load (binary) → [06](06-ai.md), [12](12-maps-scenario-campaign.md)
- **WarKingdoms / RTSUnityGameLicenta** — birim/seçim/savaş OOP deseni → [02](02-units.md), [04](04-combat.md)
