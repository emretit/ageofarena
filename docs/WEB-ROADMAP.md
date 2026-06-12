# Age of Arena — Web Roadmap (Three.js): Faz 8-18

> **Durum takibi [docs/PLAN.md](PLAN.md)'dedir** ("Web Roadmap — Faz 8-18" indeksi; ✅/✔️/🟡/⬜
> sembolleri orada güncellenir). Bu dosya detay katmanıdır: mimari kararlar, dosya-seviyesi madde
> tabloları, faz başına DoD + riskler.
>
> Kaynak: kullanıcının 19 başlıklı ürün prompt'u — AoE2-ilhamlı, tarayıcı-yerlisi,
> "realistic medieval / NOT cartoon / NOT cheap mobile" hedefli RTS. 19 başlığın tamamının
> eşlemesi [§5](#5-ürün-promptunun-19-başlığı--eşleme)'te.
>
> Yazım: 2026-06-12. Taban: web Faz 1-7 (PLAN.md kayıtları + commit `438364d`).

---

## 0. Mevcut Durum (Faz 1-7 sonu)

**Var olan** (~5.5k satır TS, 34 dosya; Vite + TS + Three.js ^0.170, framework yok):
ekonomi (4 kaynak, gather→dropoff), 18 bina, 13 birim, 57+ tech / 4 çağ, 14 civ (bonus + 28
unique tech + denied unit/tech), tek EnemyAI (build order + research + age-up + 240s push),
FoW (128² CPU grid, 3 katman), minimap (tıkla-git + FoW), market/tribute/trade/relic/garrison,
prosedürel WebAudio SFX (11 SoundId), HP barları, bina yerleştirme ghost'u, projectile pool
(görsel), 5 harita arketipi, 30Hz sabit sim tick + Mulberry32 seeded RNG.

**Kritik eksikler** (bu roadmap'in konusu):

| Eksik | Bugünkü davranış |
|---|---|
| Pathfinding | Düz çizgi yürüyüş — ağaç/bina/birim içinden geçer, çarpışma yok |
| Komut derinliği | Attack-move, stance, patrol, kontrol grubu, shift-kuyruk yok |
| Takım ölçeği | 2 takım hardcode (`main.ts`, `CivState`, `Minimap`); tek AI, zorluk yok |
| Görsel | Tamamı prosedürel primitif (kutu/küre/koni) — "ucuz mobil" görünüm |
| Ölçek | Birim başına ayrı `THREE.Group` (instancing yok) — 1000 birim hedefini taşımaz |
| Kalıcılık | Save/load, replay yok; sim state serileştirmesi sıfır |
| Network | İstemcide sıfır network kodu. `server/src/index.ts`'de naif ws relay stub'ı var (oda/ready/checksum iskeleti, Railway config); `server/src/RtsRoom.ts` ölü Colyseus artığı |

**Bilinen canlı determinizm sızıntıları** (Faz 14'te kapanacak; şimdiden yenisi eklenmemeli):
`TrainingQueue.ts` spawn offset'inde `Math.random()`, `MarketSystem.ts` modül-global fiyat
durumu, `main.ts` hardcoded seed'ler (42 / 1453), `EnemyAI._tryBuild` + `RelicSystem`'de
`Math.cos/sin`.

---

## 1. Mimari Kararlar

Unity'nin dersi (HANDOFF audit): lockstep/FixedPoint/GridPathfinder **yazıldı ama hiç wire
edilmedi**. Bu roadmap'te her altyapı parçası aynı faz içinde wire edilir ve DoD'si runtime'da
doğrulanır — "yazıldı ama bağlanmadı" kod merge edilemez (Faz 14-15'in test/golden-replay
tripwire'ları bunun bekçisi).

| Konu | Karar | Gerekçe |
|---|---|---|
| Pathfinding | **192×192 NavGrid** (±96 dünya, 1.0u/hücre, `Uint8Array`: LAND/WATER/BLOCKED + takım-kapı bitleri) + binary-heap **A\*** (octile, 10/14 integer cost, stable tie-break) + grid-LOS **string-pulling** + spatial-hash **yumuşak separation**. RVO / flow-field / hiyerarşik YOK | 192² gridde en kötü ada-karşısı yol ~2-6k düğüm (<0.3ms). FoW'un 128² gridiyle bilerek hizalanmıyor: FoW görsel doku, NavGrid sim doğruluğu (TC 5×5 footprint tam hücreye oturur). Unity `GridPathfinder.cs` ile birebir parite; integer grid + sıralı genişletme determinizme hazır doğar. AoE2'nin kendisi de birim çözünürlüğünde "kaba ama okunur" |
| Render ölçeği | **Merge-then-instance**: GLTF model yüklemede tek `BufferGeometry`'ye bake; arketip başına tek `InstancedMesh`; takım rengi `instanceColor` + vertex `aTeamMask` (`onBeforeCompile` ile `mix(diffuse, instanceColor, mask)`). 13 birim tipi ≈ 13 draw call. Binalar (<100 adet) bireysel GLTF klonu kalır | Mevcut durum birim başına 4-8 mesh. Skinned instancing (bone texture + custom shader) solo-dev hızında değil; SkinnedMesh klonları 500 birimde CPU skinning + draw call patlaması. Bina instancing'i maliyetine değmez, raycast seçim bedavaya korunur |
| Animasyon | **Prosedürel transform**: hız-oranlı yürüme bob'u (sin), `attackTimer` tetikli saldırı lunge'ı, 0.8s ölümde bat-küçül. İskelet animasyonu yok | AoE2 zoom seviyesinde iskelet farkı okunmaz; KayKit modelleri statik pozda da iyi. İskelet yalnız ileride tekil "hero piece" için |
| PostFX | `ACESFilmicToneMapping` + EffectComposer: **MSAA(4) RT → quarter-res UnrealBloom(0.25) → vignette/grade**. SSAO YOK. 3 tier: High (MSAA4+bloom+vignette) / Medium (FXAA+vignette) / Low (composer kapalı, ACES kalır); ilk 5s frame-time ölçümüyle otomatik seçim + ayarlardan elle | Sabit izometrik açı + gerçek gölgelerde SSAO katkısı ms maliyetini ödemez |
| Determinizm | **Float sim kalır** (fixed-point YOK — Unity'nin kullanılmayan `FixedPoint.cs`'i anti-pattern kanıtı). Üç kural: (1) sim yalnız Command'larla mutasyon, (2) rastgelelik yalnız seeded `SimRandom` stream'lerinden, (3) sim'de transcendental `Math.*` yasak → `DMath` lookup (sin/cos); `+ − * / sqrt` IEEE-754 cross-engine bit-identik | Sim'deki transcendental yüzeyi 2 call-site — tam cross-browser determinizm 2 dosyalık maliyetle alınıyor. ESLint `no-restricted-properties` bekçi |
| Netcode | **Deterministik lockstep**: 1 network turn = 4 sim tick (133ms), turn T'de verilen komut T+2'de exec (~266ms input delay — AoE2 communication-turn bandı). Server = **turn sequencer** (komutları toplar, herkesinki gelince turn bundle broadcast — self-clocking stop-and-wait). JSON protokol v1 (<1 KB/s/client; binary ancak ölçüm gerektirirse) | Komut-only bant 1000 birimde sabit kalır; replay bedavaya çıkar. SP de `LoopbackTransport`'tan akar → SP/MP/replay tek kod yolu |
| Save/Replay | **Save = replay = `.aoarep` komut logu** (`{version-triple, mapSeed, settings, players, commands[], checksums[]}`) + 60s'de bir keyframe snapshot (ölçüm gate'li). Faz 12'deki SP-snapshot save MP öncesi geçici köprü | Tek format = replay + bug raporu + golden regression fixture. 36k tick (20dk) @ ≥3k tick/s headless ≤12s, keyframe'le <1s |
| Backend bölüşümü | **Supabase**: anonymous-first auth, profiller, lobby browser (Realtime ~1 update/s), matchmaking (Edge Function, cron 10s), ELO/match history, replay Storage. **Node `ws` server (Railway)**: maç rölesi + **sonuçların tek yazarı** (service secret; client RLS ile sonuç YAZAMAZ) | Supabase Realtime 7.5 turn/s lockstep rölesi için uygunsuz (ekstra hop +50-150ms, kota, oda-yerel sıralama yok); düşük frekanslı lobby/persistence için tam yeri |
| Anti-cheat (dürüst kayıt) | v1: lockstep full state'i her client'a verir → **maphack mümkün**. Server komut sahiplik/şekil/rate doğrular; checksum farkı maçı void eder; ranked = claimed (email) hesap şartı. Server-side sim doğrulama v1 non-goal | Lockstep'in doğası; bunu gizlemek yerine kayda geçiriyoruz |
| UI | **Vanilla DOM devam** (React yok). Lobby/menü ekranları da vanilla TS component | Oyun içi HUD performans-kritik ve yazılmış durumda; migration sıfır oynanış değeri katar |
| Asset pipeline | **GLTF low-poly**: KayKit (karakter/nature) + Kenney (siege/gemi) + Quaternius (at/bina) — Unity ikiziyle aynı paketler, native GLB, CC0/CC-BY (`public/assets/CREDITS.md`). **Higgsfield MCP**: `generate_image` → 14 civ portresi + menü key-art + loading art; `generate_audio` → 4 çağ müziği + savaş katmanı + stinger'lar; (ops.) `generate_3d` hero-piece deneyleri | Tarayıcıda "grounded medieval" hissinin en gerçekçi yolu; instancing stratejisiyle uyumlu. Higgsfield stil tutarlılığı tek prompt şablonuyla |

## 2. Hedef Dizin Yapısı

```
web/src/
├── sim/        # Faz 8+: NavGrid, Pathfinder, SpatialHash, MovementSystem, Orders, Formation,
│               #   SimRng → Faz 13+: EntityIds, Command, CommandBus, CommandExecutor, GameClock
│               #   → Faz 14+: SimWorld, SimUnit/SimBuilding/SimNode, SimRandom, DMath,
│               #   HeadlessRunner, Checksum, Snapshot  (KURAL: sıfır three/DOM importu)
├── view/       # Faz 14: UnitView, BuildingView, ViewRegistry (sim'i okur, asla yazmaz)
├── render/     # Faz 11: AssetLoader, AssetManifest, TeamTintMaterial, UnitRenderer,
│               #   HpBarRenderer, TerrainRenderer, Lighting, PostFx, Anim
├── net/        # Faz 16+: Transport, WsTransport, LoopbackTransport, LockstepClient,
│               #   DesyncHandler, Auth (Faz 17)
├── replay/     # Faz 15: ReplayFile, ReplayDriver
├── dev/        # Faz 12: PerfHud
├── game/ core/ world/ ui/ camera/   # mevcut (kademeli olarak sim/view'a göçer)
web/public/assets/{models,audio,img}/  # Faz 11 (GLTF + müzik + portre; CREDITS.md)
shared/         # Faz 16: protocol.ts, Versions.ts (web+server tsconfig alias; monorepo tooling YOK)
server/src/     # Faz 16: index.ts (turn sequencer'a yeniden yazılır), Room.ts, Limits.ts,
                #   Report.ts (Faz 17) — RtsRoom.ts SİLİNİR
```

---

## 3. SP Derinlik Hattı (Faz 8-12)

### Faz 8 — Pathfinding & Hareket Temeli (XL, 2-3 oturum)

**Hedef:** Birimler engellerin etrafından dolaşır, üst üste binmez, grup halinde formasyonla
hareket eder; tüm emirler tek komut kapısından (`Orders.ts`) akar.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB8.navgrid | `src/sim/NavGrid.ts` — 192×192 hücre (±96, 1.0u/hücre), `Uint8Array` bayraklar (LAND/WATER/BLOCKED + takım-kapı bitleri); `worldToCell/cellToWorld`, `isWalkable(cell, teamId, domain)`, supercover `lineWalkable()` (LOS), `nearestFreeCell()`, `stampRect/stampCircle/unstamp` | GridPathfinder.cs grid katmanı |
| WEB8.stamp | Engel basma: `Building` DIMS footprint'leri (yerleşince stamp, yıkılınca unstamp), `ResourceNode`'lar (deplete'te unstamp), ağaçlar — `MapGenerator.buildForest` artık `TreeInstance[] {x,z,scale}` DÖNDÜRÜR (görseli main.ts ekler; aynı veri Faz 11 instancing'de kullanılır); `r > LandRadius` hücreleri WATER | WorldRoot NavMesh bake |
| WEB8.astar | `src/sim/Pathfinder.ts` — binary-heap A\*, octile heuristic, köşe-kesme yasak, stable tie-break; bloklu hedefte goal-relaxation (en yakın boş hücre — bina/kaynak hedefleri için şart); erişilemezse best-effort partial path; string-pulling smoothing | GridPathfinder.cs (N16.path) |
| WEB8.queue | Path istek kuyruğu: tick başına maks 8 path / 4000 düğüm; öncelik: oyuncu > AI > re-path; re-path tetikleri: sonraki waypoint bloklandı, takip hedefi ≥2 hücre kaydı; birim başına ≥0.5s throttle; <6 hücre kısa yolda A\* atla (düz LOS yürü) | — (web'e özgü bütçe) |
| WEB8.spatial | `src/sim/SpatialHash.ts` — XZ uniform hash (hücre 4), her sim tick rebuild, `queryCircle(pos, r)`; `CombatSystem._findAggro` + `tickBuildings` + separation buna geçer (O(n²) ölür) | SpatialGrid.cs (N1.grid) |
| WEB8.move | `Unit.ts` hareket bloğu → `src/sim/MovementSystem.ts`: waypoint takibi + separation (yarıçap 0.7u, maks 6 komşu, itme clamp'li + walkability clamp) + varış gevşetme (grup hedefinde "yeterince yakın"da dur); `Unit` artık `path: PathState` + `vel` taşır (Faz 9 lead targeting kullanacak) | UnitEntity.cs NavMeshAgent eşleniği |
| WEB8.orders | `src/sim/Orders.ts` — düz-veri emir objeleri `{kind, unitIds, x?, z?, targetId?, queued?}` + tek executor; `Selection.order()`, main.ts hotkey'leri, `GarrisonSystem`, `EnemyAI` saldırı emri hepsi buradan (Faz 13 komut-paterni dikişi) | CommandRecorder.cs + CommandSystem.cs |
| WEB8.formation | `src/sim/Formation.ts` — `FormationOffsets(n, type)`: Grid/Line/Staggered/Wedge, spacing 1.5; F tuşu döngüsü + HUD rozeti; grup emrinde merkez path TEK hesaplanır, birimler offset hedefe yürür (Selection'daki ad-hoc 3-kolon grid silinir) | CommandSystem.cs FormationType/FormationOffsets |
| WEB8.gather | `GatherSystem` entegrasyonu: `_approachPoint` → NavGrid `nearestFreeCell` komşusu; node/dropoff'a path ile; dolu node'da bekleme = komşu boş hücre | GatherSystem.cs |
| WEB8.chase | `CombatSystem` takip: her tick `moveTo` yerine throttle'lı re-path; bina hedefinde perimeter hücresine yaklaş; `GarrisonSystem`/`TradingSystem`/`RelicSystem` hareketleri path'e geçer | CombatSystem.cs chase |
| WEB8.aipath | `EnemyAI`: saldırı dalgası `Orders.attackTarget` üzerinden; `_tryBuild` spiral yerleşimi NavGrid boşluk kontrolüyle (ağaç/kaynak üstüne bina basma bug'ı kapanır) | EnemyAI.cs |
| WEB8.place | `BuildingPlacement` geçerlilik = footprint hücreleri boş + LAND; ghost kırmızı/yeşil bunu okur; yerleşince üstte kalan birimler en yakın boş hücreye itilir | BuildingPlacement.cs |
| WEB8.simrng | Sim determinizm hijyeni: `TrainingQueue` `Math.random` spawn offset → bina-kenarı deterministik slot; `mulberry32` sim-RNG'si `src/sim/SimRng.ts` olarak tekleşir | SimRandom.cs (N3.prng) |

**DoD:**
- 50 birim orman kuşağının karşısına sağ-tık → hiçbiri ağaç/bina/maden içinden geçmez, 10s içinde varır, varışta merkezler arası ≥0.5u (yığılma yok).
- TC arkasındaki altına giden villager binayı dolaşır; öne yeni Barracks konunca sonraki yolculuklar onu da dolaşır, üstünde kalan birim dışarı itilir.
- 30 villager tek madene atanır → 6 slot çalışır, kalanlar komşu hücrelerde bekler; FPS ≥ 55.
- F ile 4 formasyon döner; 20 birim hedefte seçili şekli okunur biçimde kurar.
- Aynı seed + aynı emir dizisi iki koşuda aynı pozisyon hash'i üretir (konsol FNV karşılaştırma).
- 60 birim aynı tick'te path isterken sim tick < 8ms.

**Risk:** Roadmap'in en riskli fazı — hareket bloğuna bağımlı HER sistem (gather/combat/garrison/
trade/relic/AI) dokunulur. Faz içi sıra: navgrid+stamp → astar → move (separation'sız) → sistem
entegrasyonları → separation → formation. Separation birimleri engele itebilir → itme sonrası
walkability clamp şart. Gather döngüsünün her dönüşte re-path istemesi kuyruğu taşırabilir →
kısa-yol LOS atlaması.

### Faz 9 — Komut Derinliği & Savaş Hissi (L, 1-2 oturum)

**Hedef:** Attack-move/stance/kontrol grubuyla ordu yönetimi; gerçek mermi uçuşu, Monk
dönüştürme, kapı, garnizon oku ve vuruş geri bildirimiyle savaş "hissedilir" olur.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB9.attackmove | `UnitState.AttackMove` + `Orders.attackMove`; A tuşu pending-click akışı; yürürken SpatialHash aggro taraması, düşman ölünce rotaya devam; hedefe bayrak marker'ı | CommandSystem.cs BeginAttackMove (N9.feedback) |
| WEB9.patrol | Z ile iki nokta arası attack-move döngüsü (`patrolA/B`) | CommandSystem.cs BeginPatrol |
| WEB9.stance | `AttackStance` enum: Aggressive (sınırsız takip) / Defensive (8u leash) / StandGround (takipsiz) / NoAttack; CombatSystem aggro+chase bunu okur; HUD'da 4 buton + hotkey | GameTypes.cs AttackStance |
| WEB9.ctrlgroup | `src/game/ControlGroups.ts` — Ctrl+1..9 ata, 1..9 çağır, çift-bas kamera odak; ölüleri otomatik ayıkla | Hotkeys.cs (P1 CTRL) |
| WEB9.idlevill | '.' boş villager DÖNGÜSÜ (index rotasyonu) + kamera odak + HUD boş-villager sayacı rozeti | P1 IDLE |
| WEB9.shiftqueue | Shift+sağ-tık waypoint kuyruğu (`queued:true` → birim emir kuyruğu); gather/move/attack karışık kuyruklanabilir | N9.queue |
| WEB9.proj | `ProjectileSystem` SİM'e taşınır (30Hz tick): hasar VARIŞTA uygulanır; ranged hasar dalı `projectiles.fireSim(attacker, target)` olur; ölü hedefe varan mermi boşa düşer | Projectile.cs (FEEL.vfx) |
| WEB9.lead | Lead targeting + isabet modeli: uçuş süresi × hedef `vel` öngörüsü; Ballistics ÖNCESİ SimRng sapması (koşan hedefe ıska), Ballistics SONRASI tam lead — kiting gerçek olur | N6.ballistics |
| WEB9.splash | Splash varış NOKTASINDA patlar: SpatialHash daire sorgusu, mesafe falloff, friendly-fire dahil, kurban başına kendi zırhı | N0.4 + N6.splash |
| WEB9.monk | `src/game/ConversionSystem.ts` — Monk 4.5u menzilde faith biriktirir (4-10s SimRng), dolunca `unit.convertTo(teamId)`: teamId mutable, re-tint, state sıfır, pop ledger düzelt; 30s recharge; HUD faith bar | Unity Monk conversion (Monastery techleri mevcut) |
| WEB9.gate | `BuildingType.Gate` (1×4, duvar hattına snap); NavGrid kapı hücreleri takım-maskeli (sahibi+müttefik geçer, düşman bloklu); yaklaşan dosta açılma animasyonu; duvar sürükle-çiz modu (basılı tut → segment dizisi) | BuildingType.Gate (GameTypes.cs:60) |
| WEB9.garrarrow | `BUILDING_COMBAT`'a WatchTower {range 8, dmg 6, interval 2.0}; Castle/TC/kule hasarı garnizonla ölçeklenir: `dmg × (1 + 0.4 × garrison)` cap 5 | BuildingCombatSystem.cs + GarrisonSystem.cs |
| WEB9.shake | `CameraRig.shake(amp, dur)` — bina yıkımı/treb vuruşu görüş alanındaysa; üstel sönüm, amplitüd cap | — |
| WEB9.death | Ölümde anında silme yerine 0.8s yere batma + kararma (render-side); siege'de duman pufu; ölü temizleme 0.8s gecikmeli | N8.anim ölüm variantı |
| WEB9.duck | Ambient loop (rüzgar+kuş prosedürel) + savaş yoğunluğu sayacı (son 3s vuruş/s) → ambient/müzik gain ducking dikişi (müzik Faz 11'de, dikiş şimdi); yeni SoundId: Conversion, GateOpen, HornAttack | N7.music ducking |

**DoD:**
- A+tık ile 20 asker düşman üssüne yürür; yoldaki her düşman grubuyla savaşır, temizleyince rotaya döner; StandGround okçu hattı takipsiz menzilde vurur.
- Ballistics'siz okçu koşan Scout'a belirgin ıskalar, Ballistics sonrası vurur; havadaki mermi hedef ölünce hasarı ikinci kez uygulamaz.
- Monk düşman Cavalry'yi dönüştürür: renk değişir, pop ledger iki tarafta düzelir, birim eski emrini unutur.
- Gate kendi birimlerine yol verir (path kapıdan geçer), düşman path'i dolaşır; kapı yıkılınca hücreler herkese açılır.
- 5 villager garnizonlu TC'nin ok hasarı boş TC'nin ~3 katı; Ctrl+1 / çift-1 grup çağır + odak çalışır.
- Trebuchet vuruşunda ekran sarsılır; splash dost birimlere de falloff'lu işler; ölen birim anında kaybolmaz.

**Risk:** Faz 8'e tam bağımlı (SpatialHash, NavGrid maskesi, Orders, vel). Mermi gecikmesi TTK'yı
uzatır → BAL pin'leri (Militia düello ~15s bandı) yeniden ölçülmeli. `teamId` mutable'lığı
readonly varsayan yerleri kırabilir (grep şart: FoW, Minimap, ledger). Kapı maskesi A\* sıcak
döngüsüne bir AND ekler — ölçüp gerekirse inline et.

### Faz 10 — N-Takım & AI Ölçeklenmesi (L, 1-2 oturum)

**Hedef:** 2-4 AI rakip, 6 zorluk seviyesi, 3 kişilik ve diplomasi (2v2 dahil) ile SP maç
kurulumu gerçek bir lobiye döner.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB10.nteam | 2-takım hardcode sökümü: `main.ts` `GameSetup {players: [{civ, teamSlot, isAI, difficulty, personality, allianceGroup}]}` ile N TC + N villager spawn (`arch.basePositions` zaten 4'lü); `teamRes`/`ageSystem` dizileri N; `CivState` dinamik | N5.nteam (GameManager MaxTeams) |
| WEB10.palette | `Config.TeamColors` → 8 AoE2 rengi tek kaynak; `Minimap.TEAM_COLORS` ve Unit/Building tint'i buradan türer | TeamPalette.cs (N4.palette) |
| WEB10.diplo | `src/core/Diplomacy.ts` — `DiplomacyState {Enemy, Neutral, Allied}` matrisi, `isAllied/isEnemy`; CombatSystem aggro + EnemyAI hedefleme + Conversion + garrison `teamId !==` yerine bunu okur; allianceGroup'tan otomatik kurulum | GameManager.IsAllied + DiplomacyState (N0.2) |
| WEB10.fowteam | FoW boyaması team 0 + müttefiklerini dahil eder; müttefik birimleri her zaman görünür | N5.fow |
| WEB10.victoryN | `src/game/VictorySystem.ts` — main.ts'teki TC kontrolü buraya: takım yenilgisi = TC yok; zafer = tüm düşman takımlar yenik (müttefikler birlikte kazanır); Faz 12 modları bunu genişletir | N0.2 paylaşımlı zafer |
| WEB10.aidiff | `Difficulty` 6 seviye tablosu: gatherMult {0.7/0.85/1.0/1.15/1.3/1.5} (**deposit anında** çarpan = AI eco cheat), firstPush {420/330/240/150/90/60}s, villager hedefi {8..28}, train cadence, ordu eşiği | Difficulty enum + EnemyAI.RetuneForDifficulty |
| WEB10.aipers | `AIPersonality {Balanced, Rusher, Boomer}`: Rusher push −%40 + erken 2×Barracks + küçük ordu eşiği; Boomer push +%50 + villager ×1.6 + eco tech önceliği; BUILD_ORDER/TECH_PRIORITY kişilik filtreli | AIPersonality (GameTypes.cs:94) |
| WEB10.aimicro | AI ordu durum makinesi: Gathering → Rallying (üs önünde formasyonla toplan) → Attacking (`Orders.attackMove`) → Retreating (ordu %40 eridi → üsse dön; Hard+); Insane+ hedef önceliği: önce savunma binası menzilindeki birimleri çek | EnemyAI.cs Stance enum |
| WEB10.aimulti | Her AI takımı kendi `EnemyAI` instance'ı; interval check'lerine tick offset'i (aynı tick'te 3 AI build-check çakışmasın); AI↔AI savaşı (hedef = en yakın düşman TC) | N14.aieco çoklu brain |
| WEB10.setup | `PreGameScreen` v2: rakip satırları (1-3 AI: civ/random, zorluk, kişilik, takım slotu), harita + mod seçimi; random civ seed'li RNG'ye | CivSelectScreen.cs + LobbyScreen.cs SP yarısı |

**DoD:**
- 3 AI ile FFA: 4 renk minimap'te, AI'lar birbirine de saldırır; son TC'si düşen elenir, son kalan kazanır.
- 2v2: müttefik AI'ya aggro yok, ally birimleri fog'da görünür ve görüşleri haritayı açar; düşman takım elenince iki müttefik aynı anda zafer görür.
- Easy ilk saldırı ≥400s ve 25dk'da hâlâ Feudal; Extreme ~60s'de baskın, ~20dk'da Castle Age.
- Rusher 5-8 birimle erken gelir; Boomer 20+ villager'la geç ve kalabalık push atar (iki koşuda gözle ayırt edilir).
- Hard+ dalgası attack-move ile gelir ve %40 kayıpta geri çekilir.
- 4 takım + 200 birimde sim tick < 10ms.

**Risk:** `teamId !== 0` / `=== 1` grep temizliği sinsi — oyuncu-niyetli kontroller (Selection)
KALIR, düşmanlık-niyetli olanlar Diplomacy'ye döner; tek tek ayıklanmalı. FoW ally-OR boyama
maliyeti birimle artar (repaint 0.2s korunur, gerekirse 0.3s). AI eco cheat çarpanı deposit'te
uygulanmalı (gather interval'ında değil) yoksa node tüketimi bozulur.

### Faz 11 — Görsel Devrim: GLTF + Işık + PostFX + Higgsfield (XL, 2-3 oturum)

**Hedef:** Prosedürel kutular gider; KayKit/Kenney/Quaternius GLTF + sinematik ışık + postFX ile
"ucuz mobil" görünüm "gerçekçi-temelli ortaçağ"a döner ve renderer 1000 birime hazır olur.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB11.loader | `src/render/AssetLoader.ts` — `GLTFLoader` (+ KTX2/meshopt ops.), `public/assets/models/{units,buildings,props}/*.glb`; PreGame "OYNA" → `ui/LoadingScreen.ts` progress → startGame; toplam bütçe < 20MB | Unity import pipeline |
| WEB11.manifest | `src/render/AssetManifest.ts` — UnitType→{file, scale, yawOffset, teamMaskMaterials[]}: Villager→KayKit Adventurer, Militia/Spearman→KayKit Knight, Archer/Longbowman/Skirmisher→KayKit Rogue (+yay), Monk→KayKit Mage, Cavalry/Scout→Quaternius Horse + Knight kompozit, Ram/Mangonel/Trebuchet→Kenney Siege, TradeCart→Kenney cart; 19 bina→Quaternius RTS/Kenney Medieval; native GLB (FBX dönüşümü YOK) | UnitVisualLibrary.cs + KenneyModels.cs + QuaterniusBuildings.cs |
| WEB11.bake | Yüklemede model → tek `BufferGeometry` merge + manifest materyal adlarından `aTeamMask` vertex attribute; `src/render/TeamTintMaterial.ts` `onBeforeCompile` ile `mix(diffuse, instanceColor, mask)` | VIS.mount/building dalgaları |
| WEB11.unitrender | `src/render/UnitRenderer.ts` — arketip başına `InstancedMesh` (instanceColor=takım, instanceMatrix=poz/yön/anim); `Unit` görsel sahipliğini BIRAKIR (`root: Group` → düz veri: pos/rotY/fowVisible); seçim halkası ayrı küçük InstancedMesh; Garrison/FoW `root.visible` yerine bayrak yazar | render=sim-okur ilkesi |
| WEB11.pick | Birim seçimi raycast yerine ekran-uzayı en-yakın-birim (drag-box projeksiyonu zaten var); binalar GLTF klon Group raycast'iyle seçilmeye devam | SelectionSystem.cs |
| WEB11.anim | `src/render/Anim.ts` — prosedürel: hız-oranlı yürüme bob'u + saldırı lunge + 0.8s ölüm bat-küçül (Faz 9 death ile birleşir); instance matrislerine yazar | N8.anim |
| WEB11.hpbar | `src/render/HpBarRenderer.ts` — TEK InstancedMesh çifti (bg/fg quad); kamera açısı sabit → statik billboard (per-frame lookAt tamamen silinir); yalnız hasarlı/seçili birimler, cap 200 | WorldHpBar.cs (N1.hpbar) |
| WEB11.trees | Ağaçlar Faz 8 `TreeInstance[]`'tan 2 InstancedMesh'e (KayKit Nature pine); ResourceNode görselleri GLTF bireysel kalır (<60 adet) | N8.terrain forest |
| WEB11.terrain | `src/render/TerrainRenderer.ts` — disc'e subdivision + vertex-color splat (merkez çim → toprak → kıyı kumu, value-noise detay); Ground raycast adı korunur | N8.terrain biome |
| WEB11.water | Okyanus `ShaderMaterial`: iki kayan noise normal + kıyı köpük bandı + zaman animasyonu | N8.terrain su |
| WEB11.light | `src/render/Lighting.ts` — HemisphereLight (gök/zemin) + güneş dir (sıcak renk; gölge frustum'u kamera odağına kilitli dar ±40), `ACESFilmicToneMapping` + doğru color space; gölge 2048 PCFSoft kalır | WorldRoot ışık |
| WEB11.postfx | `src/render/PostFx.ts` — EffectComposer: MSAA(4) RT → UnrealBloom (quarter-res, 0.25) → final pass (vignette+grade); High/Medium(FXAA)/Low tier + ilk 5s otomatik seçim | — (web Unity'nin önüne geçer) |
| WEB11.hfportrait | **Higgsfield** `generate_image`: 14 civ + "Random" portresi (tek stil şablonu: "painted medieval commander portrait, oil texture, dark backdrop" + civ başına kültürel varyant) → `public/assets/img/portraits/`; PreGame civ kartlarına | CivSelectScreen görselleri |
| WEB11.hfmenu | **Higgsfield**: ana menü key-art (ortaçağ ordugâh sahnesi) + loading screen art → PreGame/LoadingScreen arka planları | — |
| WEB11.hfmusic | **Higgsfield** `generate_audio`: 4 çağ müziği (2-3dk loop, ortaçağ ensemble; çağ atlayınca crossfade) + savaş yoğunluğu katmanı + zafer/yenilgi stinger → `public/assets/audio/` | N7.music |
| WEB11.music | `AudioManager` müzik kanalı: `AudioBufferSourceNode` loop + crossfade + Faz 9 ducking dikişine bağlanır + `musicVol` localStorage | AudioManager.cs müzik katmanı |

**DoD:**
- 13 birim tipi + 19 bina + ağaçlar + kaynaklar GLTF; ekranda tek primitif kutu/koni kalmaz; takım rengi tabard/bayrakta okunur (4 takım ayırt edilir).
- 500 birim sahnede `renderer.info.render.calls` < 300; M-serisi laptop'ta 60fps.
- High tier'da bloom+vignette+MSAA; Low tier'da composer kapalı ama ACES kalır; tier geçişi oyun içinde canlı.
- Yürüyen ordu bob'lu, saldıran lunge'lı, ölen batarak kaybolur; HP barları yalnız hasarlı/seçilide.
- PreGame'de portreli civ kartları + key-art; oyunda çağ müziği, çağ atlayınca geçiş, büyük savaşta ducking; ses ayarı kalıcı.
- Islands'ta su animasyonlu + kıyı köpüğü; terende çim/toprak/kum geçişi.

**Risk:** Model başına ölçek/yön el ayarı zaman yer (manifest `scale/yawOffset`). instanceColor
maskesi materyal adlarına güvenir — pakete göre değişirse fallback: ayrı bayrak-quad
InstancedMesh. `Unit.root` sökümü geniş grep ister (FoW/Garrison/DamagePopup/VFX root okurları).
Higgsfield stil tutarlılığı: tek prompt şablonu + aynı referans; müzik OGG ~1-2MB/parça bütçeye
sayılır. Lisanslar `public/assets/CREDITS.md`'ye (CC0/CC-BY).

### Faz 12 — Performans Sertifikasyonu + SP Tamamlama (L/XL, 2-3 oturum)

**Hedef:** 200-1000 birim hedefi ölçülüp mühürlenir; deniz dilimi, zafer modları, save/load ve
ayarlarla tek-oyunculu paket tamamlanır.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB12.perfhud | `src/dev/PerfHud.ts` — FPS (EMA+p95), sim tick ms, draw call/üçgen, path kuyruk derinliği, birim sayısı; F3 toggle | N1.budget |
| WEB12.stress | Debug stress: P = 250v250, Shift+P = 500v500 ordu spawn + otomatik attack-move karşılaşması; 5dk ölçüm senaryosu dokümante | N1.budget 300-birim stres |
| WEB12.hotpath | Ölç-düzelt turu: separation komşu cap'i, FoW repaint (N-takım sonrası gerekirse 0.3s + dirty-rect), Minimap ImageData, GC avı (per-tick `clone()` → scratch Vector3) | N1.grid/pool eşleniği |
| WEB12.navgrid2 | NavGrid su domain'i: gemiler WATER domain'inde path alır; kıyı = LAND ∧ komşu-WATER; `Dock` yalnız kıyıya (BuildingPlacement domain kontrolü) | NAV.dock + naval agent-type |
| WEB12.islands | Islands gerçek adalara dönüşür: çok-disc ada listesi (2 büyük + 2 küçük) + su şeridi; NavGrid land/water stamp; balık node'ları (Food, su hücresi) | N10.rms Islands |
| WEB12.ships | `UnitType.FishingShip/Galley/TransportShip` + registry + Dock eğitimi; FishingShip balık→Dock döngüsü (GatherSystem WATER domain); Galley pierce naval savaş; Transport 5 birim garnizon + kıyıda indirme (**stretch**) | Kenney PirateKit + FishingShip loop |
| WEB12.navalai | AI Islands'ta Dock + 2 FishingShip + 2 Galley (BUILD_ORDER harita-koşullu satır); Galley'ler kıyı hedeflerini attack-move'lar | EnemyAI Dock (50-TODO #38) |
| WEB12.modes | `GameMode {Conquest, Wonder, Relic, Regicide}` — PreGame seçimi; VictorySystem genişler: Wonder dikili 300s geri sayım (HUD banner; yıkılırsa iptal), Relic: 3 relic Monastery'de 200s, Regicide: King ölünce eleme | GameMode enum (GameTypes.cs:69) |
| WEB12.save | `src/game/SaveSystem.ts` — snapshot JSON v1 (`schemaVersion`, tick, seed, mode, per-team res/age/techler, units[], buildings[], nodes[], diplomacy, fog RLE-base64, AI durumları); localStorage slot + dosya indir/yükle. Komut-log replay BİLEREK Faz 15'e bırakıldı (determinizm orada) | SaveSystem.cs (N12.savefull) |
| WEB12.settings | `ui/SettingsPanel.ts` — ESC pause menüsü: master/sfx/müzik, grafik tier, edge-scroll, kamera hızı, renk-körü palet; localStorage kalıcı; pause sim'i durdurur | N9.a11y + AccessibilitySettings.cs |
| WEB12.techtree | (**stretch**) `ui/TechTreeViewer.ts` — civ-filtreli ağaç; araştırılan/kapalı/denied renk kodu; PreGame + pause'dan açılır | N13.meta |

**DoD (kabul metrikleri):**
- Stress 250v250 (≈500 birim): M-serisi laptop'ta median ≥ 60fps, sim tick p95 < 16ms; 500v500 (1000 birim): floor ≥ 30fps, GC duraksaması > 30ms yok; draw call < 300 (PerfHud kanıtı).
- Islands: FishingShip balık→Dock döngüsüyle food akar; 2 Galley düşman Galley'i batırır; gemi karaya, kara birimi suya path alamaz; Dock yalnız kıyıya.
- Wonder: dikilince tüm takımlar geri sayımı görür, yıkılınca iptal, dolunca zafer; Relic 3+200s ile biter.
- Kaydet → sayfayı yenile → yükle: kaynaklar, pozisyonlar/HP, techler, fog keşfi ve AI davranışı kaldığı yerden; 20dk kayıt < 1MB.
- ESC menüden müzik/tier/edge-scroll değişir; yenilemede kalıcı.

**Risk:** Sertifikasyon Faz 11 renderer'ı olmadan ölçülemez (sıra bundan). Islands dengesi: kara
AI'sı adada mahsur → Islands'ta default mod Wonder/Relic önerilir; Galley kıyı bombardımanı +
Transport stretch'i telafi. Save şeması sonraki fazlarda alan ekledikçe kırılgan → toleranslı
load + `schemaVersion` zorunlu. Stretch'ler (Transport, Regicide, techtree) taşmada ilk kesilir.

---

## 4. Determinizm + Multiplayer Hattı (Faz 13-18)

### Faz 13 — Command Pattern Refactor (L) — kilit taşı

**Hedef:** Her emir serileştirilebilir `{tick, teamId, seq, type, payload}` objesi olur ve tek
CommandBus'tan tick sınırında çalışır — oyuncu, HUD, hotkey ve AI aynı kapıdan. Bu fazdan sonra
oyun AYNI oynanır; network yok.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB13.ids | Kalıcı entity ID: `src/sim/EntityIds.ts` (maç başına monotonik sayaç; unit+building+node ortak ID uzayı — bugün ID YOK) + `EntityIndex.ts` (`Map<number, Entity>`, spawn/death register); `Unit/Building/ResourceNode`'a `readonly id` | UnitEntity.unitId |
| WEB13.cmd | `src/sim/Command.ts` — discriminated union (15 tip): Move/Stop/Attack/Gather/Garrison/UngarrisonAll/UngarrisonOne/Train/Research/AgeUp/PlaceBuilding/SetRally/MarketSell/MarketBuy/Tribute; pozisyonlar wire'da integer `qx = round(x*256)`; `unitIds` issue anında sıralı | CommandRecorder.cs CommandType (14 tip) |
| WEB13.bus | `CommandBus.issue()` damgalar (`tick: current + delay`, teamId, takım-başına monotonik seq); `executeTick(t)` o tick'in komutlarını **(teamId, seq) sıralı** drain edip `CommandExecutor`'a verir — sistemleri çağıran TEK yer; executor exec ANINDA yeniden-doğrular (kaynak/sahiplik/yaşıyor-mu/çağ) ve illegal komutu sessiz düşürür (lockstep şartı: state delay sırasında değişebilir); formasyon offset'leri executor'da sıralı unitIds'ten (issue'da değil — seçim sırası client-yerel); bus tam log tutar (`getLog()`) | RemoteCommandExecutor.cs Apply() |
| WEB13.sel | `Selection.order()` → `bus.issue(...)` (entity id'li); seçim/raycast/drag-box yerel kalır (seçim komut DEĞİL) | CommandSystem.cs → Record wire (MP-5) |
| WEB13.hud | HUD buton handler'ları (train/research/age-up/market/ungarrison) + main.ts hotkey'leri + `placement.onPlace` (kaynak düşümü executor'a taşınır) → bus; HUD'daki "alabilir mi" grileme yerel tahmin olarak kalır (salt-okur) | HUD.cs command-bar |
| WEB13.ai | `EnemyAI` tüm kararları bus'tan (`ai:true` bayraklı — MP'de wire'a GİTMEZ, her client aynı komutları üretir): `_tryBuild` (bugün doğrudan `new Building` — placement doğrulamasını da bypass ediyor; kapanır), train, research, age-up, gather, push | EnemyAI.cs + ortak AI/oyuncu komut API'si (PLAN.md ilke 4) |
| WEB13.loop | `main.ts` sim döngüsü: `bus.executeTick(tick); systems.tick(); tick++`; `src/sim/GameClock.ts` kanonik tick sayacı; SP delay = 1 tick (33ms, algılanmaz) | LockstepSystem.cs (local mod) |

**DoD:**
- Grep kanıtı: Selection/HUD/EnemyAI'de sıfır doğrudan sistem çağrısı (`assignGather|attackUnit|orderGarrison|\.train\(|research.start|startAgeUp|moveTo\(`) — hepsi `bus.issue`.
- SP maç (insan vs AI) öncekiyle aynı oynanır; her emir ≤2 tick'te görünür/duyulur.
- 5dk maç sonrası `__game.bus.getLog()` JSON round-trip lossless (tüm payload integer).
- Console'dan illegal komut (kaynaksız train, düşman birimine emir) throw'suz düşer.
- Aynı seed'li iki AI-vs-AI koşusu aynı komut logunu üretir (prefix karşılaştırma; tam determinizm Faz 14'ün işi).

**Risk:** Faz 10 N-team'e bağımlı (executor her yerde teamId alır; `Selection`'daki beş
`teamId === 0` kontrolü `localTeamId` sabitine çevrilir → MP'de tek satır). Exec-anı doğrulaması
davranışı 1 tick kaydırır — HUD tahmini çift düşmemeli. `placement.onPlace`'in farm-node yan
etkisi executor'a taşınmazsa ileride desync kaynağı.

### Faz 14 — Determinizm Sertleştirme + Headless Harness (XL — en pahalı kalem sim/view ayrımı; 2 oturum bütçele)

**Hedef:** Aynı seed + aynı komut logu ⇒ bit-identik checksum; tarayıcısız (headless) CI'da
kanıtlanır.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB14.split | **Sim/view ayrımı.** Bugün `Unit.ts` pozisyonu `root.position` (THREE.Vector3) olarak tutuyor, constructor'da mesh kuruyor, `tick()` hareket+billboard karışık. Ayrım: `src/sim/SimUnit|SimBuilding|SimNode.ts` — düz objeler `{id, teamId, type, x, z, hp, state, ...}`, SIFIR `three` importu; hareket `SimUnit.tick(dt)` (atan2 rotasyon düşer — view yönü hızdan türetir); `src/view/UnitView|BuildingView|ViewRegistry.ts` — mesh/ring/HP bar, her render frame'de SimWorld entity set'ini diff'leyerek yaratır/yok eder; `src/sim/SimWorld.ts` — units/buildings/nodes/teamRes/rng/tick + main.ts'teki sistem tick SIRASI buraya (victory dahil; HUD'a event yayar). Sistemler `scene` parametresini kaybeder; Selection ViewRegistry mesh'lerinden `userData.entityId` çözer | PLAN.md ilke 1 ("render sim-state okur, asla yazmaz") + AUDIT'in "yazıldı ama bağlanmadı" anti-pattern'inden kaçınma |
| WEB14.rng | `src/sim/SimRandom.ts` — `mulberry32` üstüne serializable-state sınıf, SimWorld sahipli, maç ayarından seed. **Canlı desync bug'ları kapanır:** TrainingQueue `Math.random`, MarketSystem modül-global fiyatlar (restart'ta sıfırlanmama bug'ı da düzelir), hardcoded seed 42/1453 → `settings.mapSeed`, PreGame random civ maç başlamadan çözülüp settings'e yazılır. ESLint: `no-restricted-properties` ile `Math.random`/`Date.now` `src/sim/**` altında yasak | SimRandom.cs (N3.prng) |
| WEB14.dmath | `src/sim/DMath.ts` — deterministik sin/cos (4096-entry lookup; runtime `Math.sin`'den DEĞİL, önceden hesaplanmış sabitlerden); sim'deki 2 call-site (EnemyAI spiral, RelicSystem) buna geçer. `Math.sqrt` kalır (IEEE-754 correctly-rounded — her engine'de donanım komutu). **Karar: tam cross-browser determinizm** — yüzey 2 dosya olduğundan ucuz olan doğru olandır. atan2 split sonrası view-only | FixedPoint.cs — gidilmeyen yol (Unity'de kullanılmadı; anti-pattern kanıtı) |
| WEB14.headless | `src/sim/HeadlessRunner.ts`: `runMatch({seed, settings, commandLog, ticks}) → {checksums[], finalState}` — DOM/three/view olmadan koşar; Vitest `node` environment'ta sim/'i import edip çalıştığını doğrular; `vitest` devDep + `npm run test` | N15 harness ("headless re-simulation") |
| WEB14.checksum | `src/sim/Checksum.ts`: FNV-1a 32-bit, kanonik sıra — tick, rng state; birim (id sıralı): id, q(x), q(z), hp, state, type, teamId; bina: id, hp, type, teamId; takım: quantize edilmiş kaynaklar, çağ, pop. `q(v)=round(v*256)`. 30 tick'te bir. Dosya başına iterasyon-sırası notu: JS Map/Set insertion-ordered (deterministik insert'le güvenli); string-keyed obje iterasyonu sim'de yasak; sort her zaman total-order comparator'la | ChecksumSystem.cs |
| WEB14.test | `src/sim/__tests__/determinism.test.ts`: (a) **golden run** — sabit seed + kayıtlı 2k-tick log → iki taze koşuda identik checksum dizisi; (b) **fuzz** — AI-vs-AI, 25 random seed × 1000 tick × 2 koşu, her checksum karşılaştırılır; (c) **perf gate** — headless tick/s ölç, ≥3000/s assert et ve SAYIYI KAYDET (Faz 15/16 keyframe kararlarını boyutlandırır) | N15.checksum DoD |

**DoD:**
- `npm run test` yeşil: golden + 25-seed fuzz, sıfır checksum mismatch.
- Aynı build Chrome ve Safari'de (veya Firefox), aynı seed + log → tick 10.000'de aynı checksum (cross-engine iddiasının uçtan uca kanıtı; `__game.checksum()` ile elle karşılaştırma).
- `src/sim/**` sıfır `three` importu + sıfır yasaklı Math (lint zorlar).
- Oyun view katmanıyla görsel regresyonsuz; 200-birim savaş aynen render olur.
- Headless throughput sayısı PLAN.md'ye işlendi (hedef ≥3000 tick/s).

**Risk:** Ayrım dürüst risktir — bugün her sistem `u.pos` (Vector3) okur, `u.x/u.z` sayılarına
geçer; `distanceTo/normalize` için küçük bir `SimVec` yardımcıs gerekir; 1-2 gün mekanik churn +
gather/combat menzil kontrolü regresyonları beklenir. Faz 8 pathfinding'i zaten sim-grid tabanlı
olduğundan bu refactor'a hazır doğar. Kalıcı risk: ileride sneak eden bir `Math.pow`'u yalnız
cross-browser elle kontrol yakalar → release checklist maddesi.

### Faz 15 — Replay + Save/Load (M)

**Hedef:** Her maç `.aoarep` dosyasına kaydedilir; ×1-×8 hızda, istenen takımın perspektifinden
izlenir; save/load aynı eseri kullanır.

**Karar — save = komut logu, state snapshot değil:** 20dk maç = 36k tick; WEB14.test'in ölçtüğü
≥3k tick/s ile yükleme ≤12s, keyframe'le <1s. Komut-log kaydı küçüktür (<1MB), versiyon
kontrol edilebilir, ve aynı anda replay + bug raporu + regression fixture'dır; ayrı snapshot
serializer ikinci bir format bakımı olurdu. (Faz 12'nin SP-snapshot save'i bu noktada emekliye
ayrılır / desync state-dump formatı olarak yaşar.)

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB15.format | `src/replay/ReplayFile.ts`: `.aoarep` JSON `{magic:"AOAREP", appVersion, simVersion, mapSeed, mapType, settings, players[], commands[], checksums[], result?}`; writer = `CommandBus.getLog()` + checksum akışı; maç sonunda otomatik indirme + "Save replay"; loader version-triple doğrular | SaveSystem.cs + CommandRecorder.Snapshot() |
| WEB15.engine | `src/replay/ReplayDriver.ts`: log'u taze SimWorld'e kayıtlı tick'lerde besler, **canlı AI kapalı** (AI komutları log'da — Faz 13 kararının getirisi: replay AI rebalance'a dayanıklı); hız ×1/×2/×4/×8 = frame başına 1/2/4/8 tick; pause = tick durdur; seek = en yakın keyframe'den (yoksa t=0) view-detached re-sim | ReplayViewer.cs |
| WEB15.keyframe | 1800 tick'te (60s) bir keyframe: `src/sim/Snapshot.ts` — SimWorld tam serileştirme (split sonrası düz objeler + RNG state + sistem içi durumlar: kuyruklar, research timer'ları, market fiyatları, garrison listeleri, fog explored gridleri); `.aoarep` içine gömülü; seek + save-resume + (Faz 16) reconnect fallback kullanır. **Gate:** WEB14 <3000 tick/s ölçtüyse VEYA seek UX kötüyse yap; değilse re-sim ile geç | — |
| WEB15.ui | `src/ui/ReplayHUD.ts` (vanilla DOM): timeline (tık=seek), play/pause, hız, saat; **perspektif dropdown** (Takım 1..N FoW'u / full vision) + serbest kamera; FoW refactor'ı: takım başına grid (128² byte × N — ucuz; explored geçmişi toggle'da yeniden hesaplanamaz → replay sim'i sırasında TÜM gridler güncel tutulur), `setPerspective(team|null)`; PreGame'e `.aoarep` sürükle → "Replay izle" | ReplayViewer.cs UX + FogOfWarSystem.cs |
| WEB15.save | "Save game" = o-ana-kadar log (+ son keyframe); "Load" = ReplayDriver sona fast-forward → kontrol canlı CommandBus'a devredilir (replay→play mod geçişi main.ts'te); v1 SP-only (MP resume = Faz 16 reconnect) | SaveSystem.cs |
| WEB15.verify | Playback sırasında checksum yeniden hesaplanıp kayıtlılarla karşılaştırılır — mismatch = "replay bu build ile uyumsuz" banner'ı. Ayrıca 3 golden `.aoarep` fixture `src/sim/__tests__/fixtures/` + Vitest assert — **her gelecekteki sim değişikliğinin regression harness'ı** | ReplayViewer.cs PASS/FAIL |

**DoD:**
- 15dk SP maç kaydet → sayfayı yenile → ×8'de aynı zafere sıfır checksum mismatch ile izle.
- Perspektif geçişi her takımın fog'unu doğru gösterir (explored ≠ visible katmanları) + full vision.
- 20dk replay'de %50'ye seek <6s (keyframe'le <1s).
- Savaş ortasında save → hard-refresh → load → resume tick'inde aynı checksum, oradan zafere oynanır.
- Golden-replay Vitest'i, bilinçli bir balans sabiti değişikliğinde KIRILIR (harness'ın ısırdığının kanıtı).

**Risk:** Tamamen Faz 14'e bağımlı (replay = determinizm harness'ının UI'lısı). Sistem-başına
snapshot eksiksizliği bug çiftliğidir — unutulan bir timer/fiyat/garrison listesi yalnız seek
sonrası sapma verir; dedektör playback-doğrulayıcısıdır → onu İLK yaz. FoW refactor'ı render-path
dosyasına dokunur; sim'e dokunmaz.

### Faz 16 — Lockstep WebSocket Server + Client Transport (XL)

**Hedef:** İki tarayıcı internette tam 1v1 oynar; SP aynı yoldan loopback olur.
**Layout:** `shared/` (protocol, Versions) iki tsconfig'e alias (vite alias `@shared` +
server `include`); monorepo tooling YOK; `server/src/RtsRoom.ts` (ölü Colyseus) silinir,
mevcut `index.ts`'in oda/kod/ready iskeleti korunarak turn sequencer'a yeniden yazılır.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB16.proto | `shared/protocol.ts` — JSON v1 mesaj şeması: C→S `join{roomCode?, name, versions, reconnectToken?}`, `ready`, `turn_input{turn, commands[]}` (her turn, boş da olsa), `checksum{tick, hash}`, `chat`; S→C `room_state`, `game_start{seed, settings, players[]}`, `turn{turn, commands[][per-player]}`, `stall{waitingFor[]}`, `desync{tick}`, `player_left/joined`, `reconnect_sync{settings, commandLog, currentTurn}`. **JSON v1 ölçülene kadar nihai**: ağır oyunda ≈ <1 KB/s/client; binary (msgpack) ancak ölçüm gerektirirse — `encode/decode` bu modülde izole, upgrade tek dosya | TransportLayer.cs mesaj yüzeyi |
| WEB16.server | `server/src/Room.ts` — oda başına `{players, settings, seed, turn, turnBuffer, fullLog, checksums}`; ilerleme kuralı: bağlı HERKESİN turn T inputu (boş dahil) gelince `turn{T}` broadcast — self-clocking stop-and-wait (sunucu timer'ı yok, maç en yavaş client'a kendiliğinden kilitlenir); >1s eksik inputta diğerlerine `stall`. Komut başına doğrulama: gönderenin takımı, şema şekli, payload int aralığı, ≤32 komut/turn, ≤8KB/turn — sunucu OYUN legality'si doğrulamaz (sim'i yok); executor re-validation (WEB13) + checksum'lar kapatır. Version-triple join'de; seed `crypto.randomInt` | server/src/index.ts stub'ı (MP-1) → upgrade; N17.ws |
| WEB16.client | `src/net/Transport.ts` arayüzü + `WsTransport.ts` + `LoopbackTransport.ts` (turn'ü anında echo'lar, turn=1 tick — **SP artık buradan**: SP özel-yolu silinir, tek kod yolu); `src/net/LockstepClient.ts`: turn = 4 tick @30Hz (133ms), komut T+2'de exec (~266ms); son turn gelmeden sim İLERLEYEMEZ → sim freeze + "Waiting for {name}…" overlay + ping göstergesi (`src/ui/NetStatus.ts`); yerel komut yankısı yalnız server bundle'ından (client-side erken-exec yok); checksum raporu 60 tick'te bir | LockstepSystem.cs (N16 — Unity'de hiç wire edilmedi; burada 1. gün wire edilir) |
| WEB16.lobby | `src/ui/RoomScreen.ts`: "Multiplayer → Oda kur (5-harf kod) / Katıl"; oyuncu listesi + ready (server `room_state`); host harita/civ seçer → `game_start{seed, settings}` iki client'ta da `startGame()`'e aynı ayarları verir; maç içi text chat | LobbyScreen.cs (MP-3) |
| WEB16.desync | Server checkpoint başına checksumları karşılaştırır: fark → herkese `desync{tick}`; client (`src/net/DesyncHandler.ts`): sim pause, banner, otomatik dump indirme (o-ana-kadar `.aoarep` + yerel snapshot — diff için), maç void. **2 oyuncuda çoğunluk oyu imkânsız — dürüst karar: "mismatch, halt"**; 3+ oyuncuda majority-kick sonraki sürüm maddesi | DesyncHandler.cs (N17.desync) |
| WEB16.reconnect | Socket kopunca slot 120s korunur (`disconnected` bayrağı; diğerleri "X koptu — bekleniyor" görür, lockstep stall'da; 120s sonra düşürme → tüm client'larda deterministik AI-takeover VEYA maç biter). Server `fullLog`'u bellekte tutar (20dk <1MB) → `reconnectToken` (join'de verilen HMAC, sessionStorage'da) ile rejoin → `reconnect_sync{log}` → client headless fast-forward (view kapalı). **Catch-up matematiği:** 36k tick @ ≥3k tick/s ≤12s + progress overlay; WEB14 daha yavaş ölçtüyse server son client-raporlu keyframe'i de saklar (karar gate'i, spekülatif iş değil) | — (Unity'de yok; web-first) |
| WEB16.spectate | Spectator = `role:"spectator"` ile join: `game_start` + turn akışını **2 turn gecikmeli** alır (ucuz anti-ghosting), `turn_input` kabul edilmez; aynı sim + WEB15 perspektif toggle'ı; spectator chat'i oyunculara gitmeyen ayrı kanal | — |
| WEB16.dev | `server/package.json` `dev: ts-node --watch`; root `package.json` + `concurrently` ile `npm run dev` (web:5173 + server:2567); web `VITE_WS_URL` okur (default `ws://localhost:2567`); iki-sekme test akışı PLAN.md'ye | MP-7 deploy scriptleri |

**DoD:**
- İki tarayıcı sekmesi (sonra iki makine) tam 1v1'i zafere kadar ≥30dk **sıfır desync** oynar — her checkpoint'te checksumlar eşit, server logu kanıt.
- Sekmeyi öldür: diğeri "waiting" görür; 120s içinde tekrar aç + auto-rejoin → <15s catch-up → zafere devam, checksumlar eşit.
- SP LoopbackTransport'tan oynar (grep: main.ts'te transport-dışı sim-ilerletme yolu yok), algılanır input lag'i yok (≤2 tick).
- Console'dan kurcalanmış client (hp×2) sapmadan ≤2s sonra iki tarafta desync banner + dump tetikler.
- `SIM_VERSION`'ı farklı client join'de net mesajla reddedilir.
- Üçüncü client canlı maçı izler, perspektif değiştirir, komut sokamaz (server testi).

**Risk:** Faz 14'süz lockstep debug'ı cehennemdir — sıralama bilinçli. 266ms input delay UI
ack'leri ister (tık sesi + anında bayrak marker — Faz 9/13'ten hazır) yoksa laggy hissedilir.
Stop-and-wait en yavaş oyuncunun her paket kaybında herkesi bekletir — v1 kabul; adaptif turn
uzunluğu (yüksek RTT'de 4→6 tick) v1.1 backlog. AI-takeover state-uzayını ikiler — taşarsa önce
pause-only gemiye biner.

### Faz 17 — Hesaplar, Lobby, Matchmaking, Persistence — Supabase (L)

**Hedef:** Anonymous-first kimlik, açık lobby browser'ı, ranked 1v1 quick-match,
server-authoritative sonuç/ELO, Storage'da replay'ler.

**İş bölümü (nihai):** Supabase = kimlik, profil, lobby satırları, kuyruk, rating, geçmiş,
replay dosyaları. Game server = sonuçların TEK yazarı (service secret) + maç trafiğinin tek
rölesi. Client sonuç YAZAMAZ — hiçbir zaman.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB17.auth | `src/net/Auth.ts` + `@supabase/supabase-js`: ilk oyunda `signInAnonymously()` (signup duvarı yok); sonradan "hesabı sahiplen" → email'e upgrade (`updateUser`, uid sabit kalır → rating/geçmiş korunur); `profiles` satırı Postgres trigger'ıyla otomatik; profil UI (isim, avatar — upload VEYA **Higgsfield** üretimi portre, `avatars/` bucket) | — |
| WEB17.schema | Migration (Supabase MCP): `profiles(id→auth.users, username citext unique, avatar_url)`, `matches(id, mode, map_seed, map_type, version_triple, started/ended_at, winner_team, status[live,finished,desynced,abandoned], replay_path)`, `match_players(match_id, profile_id, team, civ, result, rating_before/after)`, `ratings(profile_id, ladder, rating=1200, games, wins, season)`, `mm_queue(profile_id PK, ladder, rating, enqueued_at)`, `lobbies(id, room_code, host_profile, map_type, slots, status)`, `leaderboard` view. **RLS:** client SELECT serbest (kendi satırları/public view'lar); INSERT/UPDATE yalnız `profiles` + `mm_queue` own-row — sonuçlar yalnız service role | — |
| WEB17.results | Postgres fn `apply_match_result(match jsonb, secret)`: `GAME_SERVER_SECRET` doğrular, matches + match_players insert + ELO (K=32, expected-score) tek transaction'da; ranked = 1v1; takım oyunları v1'de unranked kayıt; desynced/abandoned rating'siz. `server/src/Report.ts`: maç sonunda sonuç POST + `fullLog`'dan `.aoarep` derleyip Storage `replays/{matchId}.aoarep`'e yükler | — |
| WEB17.lobby | `src/ui/LobbyBrowser.ts`: açık oda listesi `lobbies` tablosundan **Supabase Realtime** ile (~1 update/s — Realtime'ın tam yeri; maç trafiği ASLA buradan akmaz); game server oda kur/başlat/kapat'ta lobbies satırını service role ile upsert/siler; "Katıl" → ws `join{roomCode}` | LobbyScreen.cs listesi |
| WEB17.mm | Quick-match: client kendi `mm_queue` satırını insert eder; Edge Function `matchmake` (enqueue tetikli + 10s cron): rating penceresi ±100 (30s'de +50 genişler) içinde eşleştirir, game server'da authenticated `POST /internal/create-room` ile oda açar, iki oyuncuya room code + token yazar. Daemon yok — bu ölçekte kuyruk derinliği tek haneli | — |
| WEB17.history | `src/ui/ProfileScreen.ts`: son 20 maç (sonuç/civ/harita/rating delta), "Replay izle" → Storage'dan indir → Faz 15 viewer; leaderboard ekranı (top 100 + kendi sıran) | — |

**DoD:**
- Taze incognito → 3 tıkta MP maçta, signup yok; sonradan email upgrade rating/geçmişi korur.
- İki ranked hesap 1v1 bitirir; rating'ler ELO'ya göre oynar (el hesabıyla doğrulanır); kaybedenin console'dan `apply_match_result` çağrısı reddedilir (RLS + secret).
- Lobby browser yeni odayı ≤2s'de gösterir, başlayınca düşürür (Realtime, polling yok).
- Pencere içindeki iki kuyruk oyuncusu ≤15s'de aynı odada.
- Biten maçın replay'i Storage'dan iner ve Faz 15 viewer'da temiz oynar.
- Supabase MCP `get_advisors` yeni tablolarda sıfır security advisory.

**Risk:** Faz 16'ya bağımlı (güvenilir raporcu = server). Anon + ranked = smurf/abandon
çiftliği → v1 önlemi: ranked kuyruk claimed (email) hesap ister; anon yalnız casual. Supabase
çağrıları sim path'ine ASLA girmez (yalnız menü/lobby ekranları).

### Faz 18 — Ops + Sertleştirme (M, final)

**Hedef:** Deploy'lu, versiyonlu, rate-limit'li, gözlemlenebilir; tablet spectator'lı;
load-test'li headroom.

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB18.deploy | Client: Netlify (mevcut `netlify.toml` `web/dist` build'liyor — koru). Server: **Railway** tek bölge (mevcut `server/railway.json` — yeniden platformlaşma yok); `start:prod: node dist/index.js`; health endpoint mevcut. Ölçek modeli README'ye: process başına N oda, tüm state = bellekte komut logları, maç sonunda Storage'a flush → process disposable; yatay ölçek (oda-kodu→instance haritası) v1 DIŞI | MP-7 (railway.json) |
| WEB18.limits | `server/src/Limits.ts`: IP başına bağlantı cap (4), socket başına token-bucket (30 msg/s, burst 60), process başına oda cap (env `MAX_ROOMS=100`), boş oda TTL 5dk, maks mesaj 16KB, dakikada join denemesi; taşan socket kodlu sebeple kapanır | — |
| WEB18.version | `shared/Versions.ts`: `{APP_VERSION, SIM_VERSION, PROTO_VERSION}` — SIM_VERSION her sim-etkileyen değişiklikte elle bump (PLAN.md'de checklist); join'de zorunlu (WEB16), replay'de damgalı (WEB15), HUD köşesinde görünür. CI: golden replay'lerin simVersion'ı güncel mi / fixture yenilenmiş mi | N17 lobby handshake |
| WEB18.sentry | `@sentry/browser` (main.ts: hatalar + desync event'leri oda/tick tag'li) + `@sentry/node` (server); sourcemap'ler Netlify build'inde. **Desync oranı ANA metrik** — maçların >%1'i desync'liyse alarm | — |
| WEB18.load | `server/test/load.ts`: ws bot harness — 50 oda × 2 bot, 5 cmd/s, 20 sim-dk; assert: p95 turn-röle gecikmesi <50ms (bölge-içi), sıfır drop, RSS <512MB (en küçük Railway instance); sayılar PLAN.md'ye | — |
| WEB18.tablet | Mobil/tablet **spectator** (ürün prompt'unun "mobile/tablet spectator" satırını ucuza kapatır — view-only, komut UI'sı yok): `CameraRig` + touch (tek parmak pan, pinch zoom); ReplayHUD/NetStatus ≥768px media-query düzeni; `?spectate=ROOMCODE` paylaşılabilir link. Mobil OYNAMA bilinçli olarak değil | — |
| WEB18.clean | `server/src/RtsRoom.ts` sil (ölü Colyseus; deps'te colyseus yok zaten); `server/package.json` buda; PLAN.md Faz 13-18 tablolarını DoD kanıt stiliyle günceller | MP-7 notu |

**DoD:**
- Public URL: yabancı biri tablette, deploy'lu altyapıda (Netlify + Railway + Supabase) iki masaüstü oyuncusunun canlı ranked maçını izler.
- Load test deploy'lu instance boyutunda geçer; sayılar PLAN.md'de.
- Version'ı uyuşmayan eski build prod server'a katılamaz (prod'a karşı test edildi).
- Sentry'de kasıtlı client hatası + zorlanmış desync event'i maç tag'leriyle görünür.
- Flood eden bot kısılıp atılır; aynı process'teki sağlıklı oda etkilenmez.

**Risk:** Tek bölge = kıtalararası eşleşmede 200ms+ RTT → stop-and-wait stall'ları; dürüst
kapsam (v1 tek bölge) + adaptif-turn backlog maddesi. Railway hobby tier'ın ws uyutması
keep-alive ile doğrulanmalı (gerekirse $5 tier). SIM_VERSION disiplini insana bağlı — golden
replay CI backstop'u.

---

## 5. Ürün Prompt'unun 19 Başlığı → Eşleme

| # | Başlık | Karşılığı |
|---|---|---|
| 1 | Core gameplay loop | Faz 1-7 (mevcut: gather→build→train→research→age→fight) + Faz 8-9 derinleşme |
| 2 | Web architecture | §1 Mimari Kararlar + §2 dizin yapısı (sim/view/render/net/replay/shared ayrımı) |
| 3 | Multiplayer architecture | Faz 13 (command) + 16 (lockstep turn sequencer + reconnect + spectator) |
| 4 | RTS rendering system | Faz 11 (merge-then-instance GLTF + ACES/postFX tier'ları) |
| 5 | Unit system | Mevcut UnitRegistry + Faz 8 (hareket/formasyon) + Faz 9 (stance/conversion) |
| 6 | Resource system | Mevcut (4 kaynak + market/tribute/trade/relic) + Faz 12 balık |
| 7 | Building system | Mevcut 18 tip + Faz 9 Gate/duvar-çiz + Faz 12 Dock işlevselliği |
| 8 | Pathfinding architecture | Faz 8 (NavGrid 192² + A\* + string-pulling + separation; Faz 12 WATER domain) |
| 9 | Fog of war system | Mevcut 3-katman grid + Faz 10 müttefik görüşü + Faz 15 perspektif/takım-başına grid |
| 10 | Combat calculations | Mevcut (melee/pierce/siege + armor class + bonus) + Faz 9 (mermi uçuşu/lead/splash/garnizon oku) |
| 11 | UI system | Vanilla DOM devam; Faz 11 (Loading) + Faz 12 (Settings/TechTree) + Faz 15-17 (Replay/Room/Lobby/Profile ekranları) |
| 12 | Optimization strategy | Faz 11 (draw call <300) + Faz 12 sertifikasyon (500@60fps, 1000@30fps floor) + WEB12.hotpath |
| 13 | Database structure | Faz 17 (Supabase şema + RLS + service-role sonuç yazımı) |
| 14 | Match synchronization | Faz 14 (determinizm + checksum) + Faz 16 (lockstep/stall/desync/reconnect) |
| 15 | Asset production pipeline | Faz 11: GLTF (KayKit/Kenney/Quaternius) + **Higgsfield** (portreler, key-art, müzik `generate_audio`, ops. `generate_3d`) |
| 16 | Suggested folder structure | §2 |
| 17 | Production roadmap | Faz 8-18 sıralı tablolar (bu doküman) + PLAN.md durum indeksi |
| 18 | MVP scope | §6 |
| 19 | Future expansion ideas | §7 |

## 6. MVP Kesitleri

| Kesit | Kapsam | "Bitti" demek |
|---|---|---|
| **MVP-1 — SP paketi** | Faz 8-12 | Pathfinding'li, formasyonlu, 4-AI'lı, GLTF görselli, 1000-birim-sertifikalı, save'li tek-oyunculu oyun. Tek başına yayınlanabilir kalite |
| **MVP-2 — Online 1v1** | Faz 13-16 | İki tarayıcı oda koduyla desync'siz tam maç; replay + spectator; SP aynı koddan loopback |
| **v1.0 — Ranked + Ops** | Faz 17-18 | Anon-first hesap, lobby browser, ELO quick-match, Storage replay'leri, deploy + izleme + tablet spectator |

## 7. Gelecek Genişleme (backlog — v1.0 sonrası)

- **Civ unique UNIT'leri** — Faz 11 manifest'i + registry satırı ile trivial içerik fazı (Longbowman zaten tip olarak var; per-civ swap kuralı eksik)
- Hava durumu / gündüz-gece döngüsü (Faz 11 Lighting üstüne; yağmur partikülü + fog yoğunluğu)
- Kampanya / senaryo editörü; harita editörü
- Adaptif turn length (yüksek RTT'de 4→6 tick); binary protokol (msgpack) — ölçüm gerektirirse
- Takım ranked (2v2 ladder), sezonlar, turnuva modu
- 3+ oyunculu desync majority-kick; server-side sim doğrulama (anti-cheat v2)
- Mobil OYNAMA (spectator'ın ötesi — dokunmatik komut UI'sı)
- Hierarchical pathfinding (yalnız harita ±96'nın üstüne büyürse)
- Higgsfield `generate_3d` hero-piece hattı (Wonder inşa sahnesi, sinematik intro)

## 8. Sıralama, Esneklik, İlkeler

- **Zincir:** 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16 → 17 → 18.
  - 10 ↔ 11 yer değiştirebilir (görünür ilerleme baskısı olursa görsel öne; 11'in `Unit` API
    değişikliği 10'un grep işini hazır bulur diye AI önce kondu).
  - 15, erken 16 ile iç içe geçebilir (farklı dosyalar).
- **Faz 13'ün dikişi Faz 8'de atılır:** `Orders.ts` tek-kapı kuralı sayesinde command-pattern
  refactor'ı ucuzlar. Faz 8'den itibaren sim'e `Math.random`/`Date.now`/transcendental sokmak yasak.
- Her faz 1-3 oturumda inecek şekilde kesildi; **stretch** işaretli maddeler (TransportShip,
  Regicide, TechTreeViewer) taşmada ilk kesilir.
- Cross-cutting (PLAN.md ilkeleriyle uyumlu): determinizm-dostu sim, data-driven tablolar,
  render sim'i okur asla yazmaz, her faz kendi DoD'sini runtime'da kanıtlar.
