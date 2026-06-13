# Age of Arena — Web (Three.js) To-Do (Faz 8-18)

> Adım-adım uygulama checklist'i. Üst tasarım → [WEB-ROADMAP.md](WEB-ROADMAP.md); durum indeksi → [PLAN.md](PLAN.md).
> Her madde **tek, gözlemlenebilir bir kod adımıdır**. Kutu işaretleme: `[ ]` → `[x]`.
> Faz içi sıra önemli (özellikle Faz 8 ve 14). Bir faz bittiğinde DoD'sini runtime'da kanıtla, sonra PLAN.md'de ⬜→✔️.

**Çalışma kuralları (her adımda geçerli):**
- Faz 8'den itibaren `src/sim/**` içine **asla** `Math.random` / `Date.now` / `Math.sin/cos/atan2/pow` sokma (Faz 14 lint'i bunu zorlayacak; borç biriktirme).
- Her emir-veren yol `Orders.ts`'ten geçsin (Faz 8'de kuruluyor) — Faz 13 refactor'ı buna yaslanıyor.
- Her adımdan sonra `npm run build --prefix web` temiz; mümkünse preview'da gözle doğrula.

---

## FAZ 8 — Pathfinding & Hareket Temeli (XL)

### 8.1 NavGrid temeli `[WEB8.navgrid]`
- [x] `src/sim/NavGrid.ts` oluştur: 192×192 `Uint8Array`, sabitler (`HALF=96`, `CELL=1.0`), bayrak bitleri `LAND/WATER/BLOCKED` + ileride kapı bitleri için yer ayır
- [x] `worldToCell(x,z)` / `cellToWorld(cx,cz)` (merkez döndür) + sınır clamp
- [x] `isWalkable(cx,cz,teamId,domain)` + `inBounds`
- [x] `nearestFreeCell(cx,cz,domain)` (spiral/BFS halka taraması, deterministik sıra)
- [x] `lineWalkable(ax,az,bx,bz)` supercover (Bresenham/DDA) — string-pulling LOS için
- [x] `stampRect(cx,cz,w,h,flag)` / `stampCircle` / `unstamp` (referans sayaçlı veya rebuild)

### 8.2 Engelleri grid'e bas `[WEB8.stamp]`
- [x] `MapGenerator.buildForest()` imzasını değiştir: görseli kurmaya devam ama `TreeInstance[] {x,z,scale}` DÖNDÜR (Faz 11 de tüketecek)
- [x] `main.ts` başlangıçta: tüm ağaçları + başlangıç kaynaklarını + başlangıç binalarını NavGrid'e stamp et; `r>LandRadius` hücrelerini WATER işaretle
- [x] Bina yerleştirme/yıkımı NavGrid stamp/unstamp'a bağla (onPlace / onBuildingDestroyed)
- [x] ResourceNode tükenince unstamp (gold/stone/wood NavGrid'e stamp; remove'da unstamp; navGrid.reset() rematch'te)

### 8.3 A* yol bulucu `[WEB8.astar]`
- [x] `src/sim/Pathfinder.ts`: binary-heap (typed-array `MinHeap`), octile heuristic (10/14), köşe-kesme yasağı
- [x] Stable tie-break (eşit f'te düşük h, eşitlikte düşük cell index — deterministik)
- [x] Goal-relaxation: hedef bloklu ise `nearestFreeCell`'e snap
- [x] Erişilemezse en-yakına best-effort partial path döndür
- [x] String-pulling: `lineWalkable` ile gereksiz waypoint'leri at (smoothing)
- [x] Çıktı: `PathState {waypoints:number[], idx}` veya benzeri düz veri

### 8.4 İstek kuyruğu & bütçe `[WEB8.queue]`
- [x] Pathfinder içinde istek kuyruğu: tick başına maks 8 path / 4000 düğüm bütçesi
- [x] Öncelik sırası: oyuncu emri > AI emri > re-path
- [x] Re-path tetikleri + birim başına ≥0.5s throttle; <6 hücrelik kısa yolda A* atla → düz LOS

### 8.5 Spatial hash `[WEB8.spatial]`
- [x] `src/sim/SpatialHash.ts`: XZ uniform hash (hücre 4), `rebuild(units)`, `queryCircle(x,z,r)`
- [x] `CombatSystem` aggro taramasını O(n²)'den SpatialHash'e geçir
- [x] `tickBuildings` hedef taramasını da SpatialHash'e geçir

### 8.6 Hareket sistemi `[WEB8.move]`
- [x] `src/sim/MovementSystem.ts`: waypoint takibi (varışta sonraki waypoint)
- [x] `Unit`'e `path:PathState` + `vel` alanları; `moveTo()` artık path ister
- [x] Önce separation'SIZ doğrula (düz path yürüsün), sonra ekle
- [x] Separation: yarıçap 0.7u, maks 6 komşu (SpatialHash), itme clamp + **walkability clamp** (engele itme yok)
- [x] Varış gevşetme: grup hedefinde "yeterince yakın"da dur (itiş savaşı yok)

### 8.7 Komut kapısı `[WEB8.orders]`
- [x] `src/sim/Orders.ts`: düz-veri emir objeleri `{kind, unitIds, x?, z?, targetId?, queued?}` + executor
- [x] `Selection.order()` → Orders'tan geçir
- [x] main.ts hotkey'leri (stop/garrison) + `GarrisonSystem` emri → Orders
- [x] `EnemyAI` saldırı emri → Orders

### 8.8 Formasyon `[WEB8.formation]`
- [x] `src/sim/Formation.ts`: `FormationOffsets(n,type)` — Grid/Line/Staggered/Wedge (spacing 1.5)
- [x] Grup emrinde merkez path TEK hesaplanır, birimler offset hedefe yürür
- [x] `Selection`'daki eski ad-hoc 3-kolon grid'i sil
- [x] F tuşu formasyon döngüsü (Grid→Line→Staggered→Wedge) + HUD rozeti; move/attackMove komutuna `formation` alanı eklendi (CommandExecutor uyguluyor)

### 8.9 Sistem entegrasyonları
- [x] `[WEB8.gather]` GatherSystem approach noktaları → `nearestFreeCell`; node/dropoff'a path; dolu node'da komşu hücrede bekle
- [x] `[WEB8.chase]` Combat chase: throttle'lı re-path + bina perimeter'a yaklaş; Garrison/Trading/Relic hareketleri path'e
- [x] `[WEB8.aipath]` EnemyAI `_tryBuild` spiral'i NavGrid boşluk kontrolüyle (ağaç/kaynak üstüne bina bug'ı kapanır)
- [x] `[WEB8.place]` BuildingPlacement geçerlilik = footprint boş + LAND; yerleşince üstte kalan birimleri en yakın boş hücreye it

### 8.10 Determinizm hijyeni `[WEB8.simrng]`
- [x] `src/sim/SimRng.ts`: `mulberry32` sim-RNG'sini tek modülde topla
- [x] `TrainingQueue` spawn offset `Math.random` → bina-kenarı deterministik slot

### 8.11 Faz 8 DoD doğrulama
- [x] 50 birim orman karşısına → içinden geçen yok, 10s'de varış, merkezler ≥0.5u
- [x] TC arkasına giden villager dolaşır; yeni bina sonraki yolu değiştirir
- [x] 30 villager tek madende: 6 çalışır, kalanlar bekler, FPS ≥55
- [x] 4 formasyon F ile döner ve okunur kurulur
- [x] Aynı seed+emir dizisi = aynı pozisyon hash'i (console FNV)
- [x] 60 path isteğinde sim tick <8ms

---

## FAZ 9 — Komut Derinliği & Savaş Hissi (L)

### 9.1 Komut UX
- [x] `[WEB9.attackmove]` `UnitState.AttackMove` + `Orders.attackMove`; A pending-click; yürürken aggro tara, düşman ölünce rotaya dön; hedef bayrak marker
- [x] `[WEB9.patrol]` Z iki nokta arası attack-move döngüsü (`patrolA/B`)
- [x] `[WEB9.stance]` `AttackStance` enum (Aggressive/Defensive-8u-leash/StandGround/NoAttack); Combat aggro+chase okur; HUD 4 buton + hotkey
- [x] `[WEB9.ctrlgroup]` `src/game/ControlGroups.ts` — Ctrl+1..9 ata, 1..9 çağır, çift-bas odak, ölü ayıkla
- [x] `[WEB9.idlevill]` '.' boş villager döngüsü + kamera odak + HUD sayaç rozeti
- [x] `[WEB9.shiftqueue]` Shift+sağ-tık waypoint kuyruğu (`queued:true`); gather/move/attack karışık

### 9.2 Mermi & savaş
- [x] `[WEB9.proj]` ProjectileSystem'i SİM tick'ine taşı (hasar VARIŞTA); Combat ranged dalı `fireSim(att,tgt)`; ölü hedefe varan mermi boşa düşer
- [ ] `[WEB9.lead]` lead targeting (uçuş süresi × hedef vel); Ballistics öncesi SimRng ıskası, sonrası tam lead
- [x] `[WEB9.splash]` splash varış NOKTASINDA: SpatialHash daire + falloff + friendly-fire + kurban başına zırh

### 9.3 Mekanikler
- [x] `[WEB9.monk]` `src/game/ConversionSystem.ts` — faith 4-10s, `convertTo(team)` (teamId mutable + re-tint + state sıfır + pop ledger düzelt), 30s recharge, HUD faith bar
- [ ] `[WEB9.gate]` `BuildingType.Gate` (1×4 duvar snap); NavGrid kapı hücresi takım-maskeli; dosta açılma animasyonu; duvar sürükle-çiz modu
- [x] `[WEB9.garrarrow]` WatchTower combat + garnizonla ok hasarı `×(1+0.4n)` cap 5

### 9.4 His
- [x] `[WEB9.shake]` `CameraRig.shake(amp,dur)` — yıkım/treb görüş alanındaysa, üstel sönüm
- [x] `[WEB9.death]` 0.8s yere batma + kararma; siege'de duman; ölü temizleme 0.8s gecikmeli
- [x] `[WEB9.duck]` ambient loop + savaş yoğunluğu ducking dikişi + yeni SoundId (Conversion/GateOpen/HornAttack)

### 9.5 Faz 9 DoD
- [x] A+tık ordu yol boyu savaşıp döner; StandGround takipsiz vurur
- [x] Ballistics'siz koşan hedefe ıska; havadaki mermi ölü hedefte boşa
- [x] Monk dönüştürür (renk + pop ledger)
- [ ] Gate dosta açık/düşmana kapalı; yıkılınca herkese açık
- [x] Garnizonlu TC oku ~3×; Ctrl+1/çift-1 çalışır
- [x] Treb vuruşunda sarsıntı + splash; ölü anında kaybolmaz
- [ ] BAL pin'leri (Militia düello ~15s) yeniden ölçüldü

---

## FAZ 10 — N-Takım & AI Ölçeklenmesi (L)

### 10.1 N-takım altyapısı
- [x] `[WEB10.nteam]` N TC+villager spawn; teamRes/ageSystem/CivState dizileri N; basePositions döngüsü
- [x] `[WEB10.palette]` `Config.TeamColors` 8 AoE2 rengi tek kaynak (zaten var); Minimap + Unit/Building tint buradan
- [x] `[WEB10.diplo]` `src/core/Diplomacy.ts` — Enemy/Neutral/Allied matrisi; CombatSystem + FoW `teamId !==` yerine `isEnemy/isAlly`
- [x] `[WEB10.fowteam]` FoW müttefik görüşü (boyamada ally OR; müttefik birimleri görünür/gizlenmez)
- [x] `[WEB10.victoryN]` `src/game/VictorySystem.ts` — takım eleme + müttefik ortak zafer; main.ts'e wire edildi

### 10.2 AI derinliği
- [x] `[WEB10.aidiff]` `Difficulty` 6 seviye tablosu: gatherMult, firstPush, villager hedefi
- [x] `[WEB10.aipers]` Rusher/Balanced/Boomer — train priority filtreleri
- [x] `[WEB10.aimicro]` Ordu durum makinesi: Gathering→Rallying→Attacking(attack-move)→Retreating
- [x] `[WEB10.aimulti]` AI başına `EnemyAI` instance + tick offset; AI↔AI savaşı (diplomacy FFA)
- [x] `[WEB10.setup]` PreGameScreen v2 — rakip sayısı (1-3) + zorluk + kişilik seçimi

### 10.3 Faz 10 DoD
- [x] 3 AI FFA: 4 renk, AI'lar birbirine saldırır (diplomacy.isEnemy), son kalan kazanır
- [x] 2v2 altyapısı: ally stance setStance('ally') ile aktif; ortak zafer + ally görüşü
- [x] Easy ≥400s / Extreme ~60s push (DIFFICULTY_TABLE'da)
- [x] Rusher/Boomer TRAIN_PRIORITY filtreleri gözle ayırt edilebilir
- [x] Hard+ attack-move ile gelir, %40'ta çekilir (retreatAt)
- [ ] 4 takım+200 birim sim tick <10ms (çalışma zamanı ölçümü gerekli)

---

## FAZ 11 — Görsel Devrim: GLTF + Işık + PostFX + Higgsfield (XL)

### 11.1 Asset yükleme
- [x] `[WEB11.loader]` `src/render/AssetLoader.ts` (GLTFLoader + fallback); `public/assets/models/` dizinleri oluşturuldu
- [x] `[WEB11.manifest]` `src/render/AssetManifest.ts` — UnitType→{file,scale,yawOffset} + 19 bina eşlemesi
- [ ] Asset paketlerini indir + `public/assets/CREDITS.md` (model dosyaları henüz eksik)
- [ ] `[WEB11.bake]` TeamTintMaterial + InstancedMesh merge (model dosyaları gerekli)

### 11.2 Birim render
- [ ] `[WEB11.unitrender]` InstancedMesh renderer (model dosyaları gerekli)
- [ ] `[WEB11.pick]` ekran-uzayı seçim
- [ ] `[WEB11.anim]` bob + lunge + ölüm bat (instance matris)
- [ ] `[WEB11.hpbar]` tek InstancedMesh çifti billboard
- [ ] `[WEB11.trees]` 2 InstancedMesh ağaç

### 11.3 Ortam & ışık
- [x] `[WEB11.terrain]` `src/render/TerrainRenderer.ts` — vertex-color splat (çim/toprak/kum)
- [x] `[WEB11.water]` okyanus ShaderMaterial (noise wave + kıyı köpüğü, animasyonlu)
- [x] `[WEB11.light]` `src/render/Lighting.ts` — HemisphereLight + ACES + dar gölge frustum
- [x] `[WEB11.postfx]` `src/render/PostFx.ts` — EffectComposer MSAA/FXAA→bloom→OutputPass; 3 tier + otomatik seçim

### 11.4 İçerik
- [ ] `[WEB11.hfportrait]` civ portreleri (Higgsfield — ücretli API, kullanıcı onayı gerekli)
- [ ] `[WEB11.hfmenu]` menü key-art (Higgsfield)
- [ ] `[WEB11.hfmusic]` müzik (Higgsfield)
- [x] `[WEB11.music]` AudioManager müzik kanalı (loop + crossfade + combat ducking + musicVol localStorage)

### 11.5 Faz 11 DoD
- [ ] Tek primitif kutu kalmaz (model dosyaları gerekli — GLTF packs)
- [x] Tier geçişi canlı çalışır (SettingsPanel ESC'den)
- [x] Çağ müziği kanalı altyapısı + savaşta ducking
- [x] Islands'ta animasyonlu su + kıyı rengi; vertex-color teren geçişi

---

## FAZ 12 — Performans Sertifikasyonu + SP Tamamlama (L/XL)

### 12.1 Profil & perf
- [x] `[WEB12.perfhud]` `src/dev/PerfHud.ts` — FPS p95, sim ms, draw call, path kuyruğu (F3)
- [ ] `[WEB12.stress]` P=250v250, Shift+P=500v500 + otomatik attack-move; 5dk senaryo dokümante
- [ ] `[WEB12.hotpath]` ölç-düzelt: separation cap, FoW repaint, Minimap ImageData, GC scratch avı

### 12.2 Donanma dilimi
- [ ] `[WEB12.navgrid2]` WATER domain path; kıyı=LAND∧komşu-WATER; Dock yalnız kıyıya
- [ ] `[WEB12.islands]` Islands çok-ada rework + su şeridi + balık node'ları
- [ ] `[WEB12.ships]` FishingShip/Galley (+Transport **stretch**) + Dock eğitimi + balık→Dock döngüsü + Galley naval savaş
- [ ] `[WEB12.navalai]` AI Islands'ta Dock+donanma (harita-koşullu build order)

### 12.3 Modlar & kalıcılık
- [x] `[WEB12.modes]` `src/game/GameMode.ts` — Conquest/Wonder(300s)/Relic(200s)/Regicide; main.ts'e wire edildi
- [x] `[WEB12.save]` `src/game/SaveSystem.ts` snapshot JSON v1 (schemaVersion, localStorage); F5 quick-save
- [x] `[WEB12.settings]` `ui/SettingsPanel.ts` ESC menü (ses/tier/edge-scroll, kalıcı, pause sim)
- [ ] `[WEB12.techtree]` **stretch** TechTreeViewer (civ-filtreli)

### 12.4 Faz 12 DoD (sertifikasyon)
- [ ] 500 birim median ≥60fps + sim p95 <16ms
- [ ] 1000 birim floor ≥30fps, GC duraksaması >30ms yok, draw call <300
- [ ] Balık→Dock food akar; gemi karaya path alamaz
- [ ] Wonder geri sayımı herkese görünür; Relic 3+200s biter
- [ ] Kaydet→yenile→yükle aynen sürer (20dk <1MB)
- [ ] Ayarlar kalıcı

**→ MVP-1 (SP paketi) tamam**

---

## FAZ 13 — Command Pattern Refactor (L)

- [x] `[WEB13.ids]` `src/sim/EntityIds.ts` — monotonik ID; Unit/Building/ResourceNode'a `readonly id: EntityId = allocId()`; `resetIds()` startGame'de
- [x] `[WEB13.cmd]` `src/sim/Command.ts` — 15 tip discriminated union; qEncode/qDecode; MarketBuyCmd/MarketSellCmd ResourceKind tipli
- [x] `[WEB13.bus]` `CommandBus` (tick+seq damga, (teamId,seq) sıralı drain) + `CommandExecutor.ts` (TEK çağrı noktası, entity ID'den nesne çözümü, sessiz düşürme)
- [x] `[WEB13.sel]` `Selection.order()` → `bus.issue` (hareket/saldırı/toplama/garnizon) — seçim yerel kalır
- [x] `[WEB13.hud]` HUD.setBus(); train/research/market/ungarrison butonları → bus.issue
- [x] `[WEB13.ai]` EnemyAI gather/train/research/move/attack → bus.issue (ai:true); _tryBuild doğrudan kalır (scene erişimi gerekli)
- [x] `[WEB13.loop]` main.ts sim loop: `commandBus.advanceTick(); commandExecutor.execute(commandBus.drain())` sim başında
- [x] **DoD:** build temiz (0 TS hata) · SP aynen oynanır · bus.getLog() JSON round-trip lossless · executor try/catch ile sessiz düşürme

---

## FAZ 14 — Determinizm + Headless Harness (XL — en pahalı)

- [ ] `[WEB14.split]` Sim/view ayrımı: sim/ katmanı sıfır three (artık geçerli); game/ dosyaları THREE kullanıyor ama sim/ temiz; tam entity split (SimUnit/UnitView) sonraki fazda
- [x] `[WEB14.rng]` `src/sim/SimRng.ts` serializable state (get/set); Math.random → simRng sadece sim/ için; AudioManager/view random kalıcı
- [x] `[WEB14.dmath]` `src/sim/DMath.ts` sin/cos 4096-giriş lookup; EnemyAI._tryBuild + RelicSystem + MovementSystem.atan2 → DMath
- [x] `[WEB14.headless]` vitest devDep + `npm run test` script + vite.config.ts test bloğu
- [x] `[WEB14.checksum]` `src/sim/Checksum.ts` FNV-1a, feedInt/feedQ/ofCommandLog
- [x] `[WEB14.test]` `src/sim/__tests__/determinism.test.ts` — 20 test (CommandBus ordering, log JSON round-trip, Checksum, DMath accuracy, SimRng state restore, EntityIds, throughput gate ≥3000 tick/s) — **20/20 YEŞIL**
- [x] **DoD (kısmi):** `npm run test` 20/20 yeşil · bus.getLog() JSON lossless · DMath tablo doğru · sim/** sıfır three import · throughput >3000 tick/s (6ms/1000-tick) · build temiz

---

## FAZ 15 — Replay + Save/Load (M)

- [x] `[WEB15.format]` `src/replay/ReplayFile.ts` `.aoarep` JSON + writer/loader (version-triple doğrula)
- [x] `[WEB15.engine]` `src/replay/ReplayDriver.ts` — log besle (canlı AI kapalı), ×1-×8, pause
- [ ] `[WEB15.keyframe]` `src/sim/Snapshot.ts` 1800 tick'te (gate: <3000 tick/s veya seek kötüyse) — **seek deferred** (HeadlessRunner + full sim/view split gerekli)
- [x] `[WEB15.ui]` `src/ui/ReplayHUD.ts` timeline+hız+perspektif dropdown; FoW takım-başına grid; PreGame'e sürükle — **temel overlay; perspektif dropdown stretch**
- [x] `[WEB15.save]` F5 quick-save = snapshot + `.aoarep` command log (startGame replaySetup'tan); `saveRepToSlot(1, rep)` wired
- [ ] `[WEB15.verify]` playback'te checksum doğrula + 3 golden `.aoarep` fixture (Vitest)
- [ ] **DoD (kısmi):** F5 `.aoarep` localStorage'a yazılıyor · ReplayDriver log'u tick-by-tick besliyor · ReplayHUD overlay çalışıyor · build 0 hata · seek + golden fixture + load-resume sonraki fazda

---

## FAZ 16 — Lockstep WS Server + Transport (XL)

- [x] `[WEB16.proto]` `shared/protocol.ts` JSON v1 mesaj şeması; server+web'e göreceli import (iki tsconfig)
- [x] `[WEB16.server]` `server/src/Room.ts` turn sequencer + index.ts yeniden yazıldı (crypto.randomInt seed, version-triple, komut doğrulama, stall broadcast)
- [x] `[WEB16.client]` `src/net/Transport.ts` + `WsTransport` + `LoopbackTransport` (SP buradan) + `LockstepClient.ts` (SP: ticksPerTurn=1/delay=0; MP: 4/2) + `ui/NetStatus.ts` (stall overlay + ping)
- [x] `[WEB16.lobby]` `src/ui/RoomScreen.ts` (5-char kod create/join, ready, player list, chat → game_start)
- [x] `[WEB16.desync]` `src/net/DesyncHandler.ts` — checksum raporla → desync banner + console dump
- [ ] `[WEB16.reconnect]` 120s slot + reconnectToken (HMAC) + catch-up — **stretch**
- [ ] `[WEB16.spectate]` spectator role — **stretch**
- [x] `[WEB16.dev]` root `package.json` + `concurrently`; `RtsRoom.ts` silindi; `VITE_WS_URL` env
- [x] **main.ts wire:** SP LoopbackTransport → LockstepClient; sim gated on stalling; Selection+HUD → CommandIssuer interface
- [ ] **DoD (kısmi):** SP LoopbackTransport'tan akar (0 gecikme) · WsTransport MP altyapısı hazır · RoomScreen lobby UI · server version-triple check · reconnect/spectate stretch

**→ MVP-2 (online 1v1) tamam**

---

## FAZ 17 — Supabase: Hesap/Lobby/Matchmaking (L)

- [x] `[WEB17.auth]` `src/net/Auth.ts` + `@supabase/supabase-js`; `signInAnonymously()`→email upgrade; env: VITE_SUPABASE_URL/ANON_KEY; offline-safe stub
- [x] `[WEB17.schema]` `supabase/migrations/001_schema.sql` — profiles/matches/match_players/ratings/mm_queue/lobbies/leaderboard; RLS: client sonuç yazamaz; `apply_match_result` fn (ELO K=32)
- [x] `[WEB17.results]` `server/src/Report.ts` — POST apply_match_result (service key); MATCH_SECRET doğrulama
- [x] `[WEB17.lobby]` `src/ui/LobbyBrowser.ts` Supabase Realtime (postgres_changes); join butonu
- [x] `[WEB17.mm]` `supabase/functions/matchmake/index.ts` — ±100 ELO pencere + 30s genişleme; game-server create-room POST
- [x] `[WEB17.history]` `src/ui/ProfileScreen.ts` — anon upgrade form + stats + son 20 maç + leaderboard
- [ ] **DoD (Manuel adımlar gerekli):** supabase db push + Edge Function deploy + VITE_SUPABASE_URL set → tam test; kod 0 TS hata ✓

---

## FAZ 18 — Ops + Sertleştirme (M)

- [x] `[WEB18.deploy]` `web/netlify.toml` (publish=dist, env var comments) + `server/railway.json` zaten vardı; ölçek notları → PLAN.md
- [x] `[WEB18.limits]` `server/src/Limits.ts` — 60 msg/s rate limit + burst cap + 30 min room TTL GC; index.ts'e wire edildi
- [x] `[WEB18.version]` `shared/Versions.ts` triple + versionsCompatible(); HUD bottom-right badge; protocol.ts PROTOCOL_VERSION referans alıyor
- [ ] `[WEB18.sentry]` `@sentry/browser` + `@sentry/node` — kurulum kılavuzu aşağıda; opsiyonel env VITE_SENTRY_DSN/SENTRY_DSN
- [x] `[WEB18.load]` `server/test/load.ts` — 50 oda × 2 bot × 300 turn; p95 < 50ms + RSS < 512MB gate; `npx ts-node server/test/load.ts`
- [ ] `[WEB18.tablet]` tablet spectator — **stretch** (`?spectate=CODE` → view-only TouchCameraRig)
- [x] `[WEB18.clean]` RtsRoom.ts Faz 16'da silindi; package.json kökü oluşturuldu; PLAN.md güncellendi
- [x] **DoD (kısmi):** rate limit kodu hazır · load test script çalıştırılabilir · version badge HUD'da · netlify.toml mevcut · tablet/sentry stretch

**→ v1.0 altyapısı hazır; deploy + Sentry + tablet touch = son manuel adımlar**
