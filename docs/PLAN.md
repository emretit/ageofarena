# Age of Arena — Tek Yol Haritası (PLAN)

> **Bu, projenin tek canlı plan kaynağıdır.** Eski rakip dokümanlar (`00-overview`,
> `PARITY-PLAN`, `ROADMAP-V2`, `01-12` gap dosyaları, `HUD-AOE2-REWORK`) buraya birleştirildi
> ve kaldırıldı (2026-06-05). `/goal` bu dosyanın DoD'unu ölçer.
>
> **Diğer katmanlar (plan değil, dokunma):**
> - **Geçmiş oturum günlüğü + mimari kararlar** → [HANDOFF.md](../HANDOFF.md)
> - **Oyunun şu anki gerçek davranışı** (her stat `file:line`) → [docs/wiki/](wiki/00-index.md)
> - **AoE2:DE kıyas bilgisi** (harici gerçek) → [docs/reference/](reference/README.md)
> - **A→Z kod audit kaydı** (N0 dalgasını tetikleyen) → [docs/AUDIT-2026-06.md](AUDIT-2026-06.md)

---

## 🚩 Unity → Three.js Geçişi RESMİLEŞTİ (2026-06-13)

**Karar:** Aktif geliştirme tamamen **`web/`** (Three.js) tarafına taşındı. Unity sürümü
(`AgeOfArenaUnity/`) referans/arşiv olarak duruyor — dokunulmuyor. Web portu Unity SP+MP
paritesinin büyük kısmını yakaladı (aşağıdaki Web Roadmap Faz 8-18 + Web Port Parite 1-6 ✅).
Kalan gap'ler 5 fazlık bir kapanış haritasına bağlandı (otonom uygulama, model: Sonnet 4.6):

| Faz | Hedef | Boyut | Durum |
|---|---|---|---|
| **Faz 0** | Web'i resmi tek platform yap (CLAUDE.md + bu not) | XS | ✅ |
| **Faz 1** | Görsel/asset paritesi — GLTF render + team tint + fallback + LoadingScreen + InstancedMesh | M | ✅ |
| **Faz 2** | MP determinism — `AgeUpCmd` + `PlaceBuildingCmd` command-replication (tek kapı = CommandBus) | M/L | ✅ |
| **Faz 3** | Naval slice — water domain + FishingShip/Galley + fish eko + Dock eğitimi + naval combat/AI + Islands | XL | ✅\* |
| **Faz 4** | Replay seek/keyframe + MP stretch (reconnect/spectator-client/tablet) | polish | ✅\*\* |

> **\* Faz 3 — naval slice + Islands + naval AI TAMAMEN BİTTİ** (2026-06-13): water-domain
> movement (gemiler suda, kara birimi karada — MovementSystem/Orders/PathQueue domain-aware),
> Dock'tan FishingShip/Galley eğitimi + suya spawn, prosedürel gemi görseli, domain-gated combat
> (gemi↔gemi / kara↔kara aggro, beach-önleme), fish ekonomisi (FishingShip→su balık→Dock food).
> Islands çok-ada terrain (NavGrid.markIslands, merged "Ground" mesh), naval AI (EnemyAI
> _tryNavalBuild kıyı-Dock + FishingShip/Galley train + _tickNavalState 5s-throttled push).
> code-review: node-domain filtresi, shore scanner, nav-attack throttle, BUILD_ORDER Dock fix.
> Commit'ler: `feat naval domain` → `fix naval AI code-review`; 30/30 test, build temiz.

> **\*\* Faz 4 — Replay seek BİTTİ** (2026-06-13): `ReplayDriver.seekForward(tick)` + SEEK_BURST=60
> fast-forward; `ReplayHUD` tıklanabilir timeline bar (ghost fill + `>>` label); `PreGameScreen`'e
> slot-1 replay varsa "REPLAY IZLE" butonu; `startGame` `_watchRep` parametresi — replay modda
> AI yok, Selection/HUD bus'suz, lockstep yerine `replayDriver.tick()`, victory screen atlanır.
> MP stretch (reconnect/spectator-client/tablet) 2-client manuel test gerektirir — DoD'un o kısmı
> açık kalıyor.

> Detaylı adım planı: `~/.claude/plans/shimmering-twirling-breeze.md`. Kalan gap'lerin kod-seviyesi
> durumu aşağıdaki **"Kalan"** bölümünde (2026-06-13 oturumu).

---

## Unity→Web İçerik/Birim Parity Kapanışı (2026-06-14)

> Audit: Unity `GameTypes.cs` ↔ web `GameTypes.ts`. Plan: `~/.claude/plans/unity-den-webe-ge-i-modular-hejlsberg.md`.
> Öncelik: oynanış etkisi. Kapsam: tam Unity parity (birim+bina+mod+içerik araçları+polish).

**Faz P1 — Birim & Bina Derinliği ✅ (2026-06-14):**
- **Base birim (4):** Camel (anti-cav +9), CavalryArcher, Medic (+`MedicSystem` alan-heal, tüm takımlar),
  Scorpion (siege pierce +splash). Enum/UnitRegistry/TrainingQueue/Unit görsel/EnemyAI/HUD.
- **Naval (2):** FireShip (anti-ship +6), DemoShip (self-destruct — `CombatSystem._detonate` AoE).
- **King + Regicide:** `UnitType.King` (taçlı, saldırısız, hp150); her takım maç başı 1 King ile spawn
  (`?mode=Regicide`); `GameMode._tickRegicide` gerçek King'e bağlandı (`_kingTeams` frame-0 guard).
  Runtime doğrulandı: Regicide'da sahnede 2 King.
- **3 bina:** Outpost (görüş 12, ateşsiz), BombardTower (gunpowder dmg25/range10), FishTrap
  (su-yerleşim + co-located su-food node, Farm deseni). FOW sight + BuildingPlacement water + placeBuildingForTeam.
- **14 civ-unique birim:** TeutonicKnight/WarElephant/Mangudai/Samurai/ThrowingAxeman/Cataphract/
  Berserk/Mameluke/WoadRaider/ChuKoNu/Huskarl/Janissary/Eagle/EliteEagle. `UNIT_CIV_GATE` + civ-filtreli
  `trainableFor()` (menü yalnız kendi civ'inin unique'ini gösterir). Castle/Barracks-trained.
- **Bonus düzeltme:** SiegeWorkshop `TRAINABLE`'da yoktu (Mangonel/Ram eğitilemiyordu) → eklendi.
- Her adımda build 0 hata + 30/30 test yeşil; preview runtime hatasız.

**Faz P2 — Game Mode'lar ✅ (2026-06-14):**
- **GameModeType** genişletildi: +Deathmatch, Nomad, EmpireWars, KingOfTheHill, SuddenDeath, Treaty, Turbo (11 mod toplam).
- **KingOfTheHill:** `_tickKingOfTheHill` — (0,0) merkezde 15-unit yarıçap kontrol, baskın takım 200s kontrolü sonrası kazanır.
  Altın daire visual marker. `timerRemaining/Active/Team` getter'ları KoTH için genişletildi.
- **SuddenDeath:** `_tickSuddenDeath` — `_sdTeams` ile TC kaybı = anında elenme (VictorySystem yerine GameMode yönetir).
- **Treaty:** `CombatSystem.enabled` flag — `modeType === 'Treaty'` ise `gameElapsed >= 900` (15 dk) olana dek savaş yok.
- **Deathmatch:** start food/wood/gold/stone = 20k/20k/10k/10k.
- **Nomad:** TC'siz başlangıç (tüm takımlar); +150 odun. AI hâlâ Barracks'la spawn olur.
- **EmpireWars:** Castle Age + start 500/500/250/200 kaynakları.
- **Turbo:** tüm takımların `techGather*Mult = 3` (×3 toplama hızı).
- **PreGameScreen:** 11 mod düğmeli seçici UI; `onStart` callback'e `mode: GameModeType` 4. parametre eklendi.
- **startGame:** 7. parametre `modeType: GameModeType = 'Conquest'`; URL param parsing kaldırıldı.
- Build 0 hata + 30/30 test yeşil.

**Faz P3 — İçerik Araçları ✅ (2026-06-14):**
- **TriggerSystem** (`web/src/game/TriggerSystem.ts`): 7 koşul (Timer/OwnUnits/OwnBuildings/ResourceGathered/
  EnemyEliminated/TechResearched/AgeReached) + 8 efekt (YouWin/YouLose/ShowMessage/ShowObjective/AddResource/
  Activate/DeactivateTrigger/SetGameOver). `onDeposit` callback → GatherSystem; `isResearched` lambda.
- **CampaignSystem** (`web/src/game/CampaignSystem.ts`): 3 misyon (İlk Savaş/Kaynak Savaşı/İmparatorun Seferi),
  localStorage ilerleme, `setupCampaign()` trigger enjeksiyonu + kaynak kurulumu.
- **CampaignScreen** (`web/src/ui/CampaignScreen.ts`): kilitli/açık/tamamlanmış kart listesi,
  "Başlat"/"Tekrar Oyna" butonu, ilerleme sıfırlama.
- **TutorialSystem** (`web/src/game/TutorialSystem.ts`): 7 adım auto-progress (köylü seç/topla/ev/kışla/
  nefer/tebrik), "İleri"/"Atla" UI, localStorage done-key, tick() koşul kontrolü.
- **ScenarioEditor** (`web/src/game/ScenarioEditor.ts`): E-key toggle, 13-öğe palet
  (birim/bina/kaynak), THREE.Raycaster klik-to-place, sil modu, JSON save/load localStorage.
- **HUD.showSubtitle** eklendi (TriggerSystem/Tutorial mesaj overlay).
- **PreGameScreen** "KAMPANYA" butonu + `onCampaign` callback.
- GatherSystem `onDeposit` callback (TriggerSystem ResourceGathered koşulu için).
- main.ts: TriggerSystem/TutorialSystem/ScenarioEditor wiring; campaign setup; "E" key editor toggle.
- Build 0 hata + 30/30 test yeşil.

**Faz P4 (hotkey/a11y/cheat): TAMAM** (2026-06-14)

- **Hotkeys.ts** yeni: `HotkeyAction` union, `getKey/setKey/isAction/resetHotkeys`, `ALL_ACTIONS`, `ACTION_LABELS`. localStorage persist.
- **main.ts** keydown handler: tüm hardcoded key karşılaştırmaları (`"a"/"z"/"s"/"f"/"g"/"u"/"."/"e"`) `isAction(action, key)` ile değiştirildi.
- **SettingsPanel.ts** genişletildi: Ses bölümü + **Erişilebilirlik** (UI ölçek slider, 0.7–1.5×) + **Görsel** (kalite/edge-scroll) + **Kısayollar** (tüm action'lar için rebind butonları, [ESC]=iptal, "Varsayılanları Yükle") + **Hile Kodları** (text input: POLO/LUMBERJACK/CHEESE STEAK JIMMYS/ROBIN HOOD/ROCK ON).
- **HUD.setUiScale(scale)** eklendi: root transform scale, localStorage persist, `settings.onUiScale` callback'i.
- **FogOfWarSystem.revealAll()** eklendi: vis+explored tüm hücreler VISIBLE, texture re-upload.
- main.ts: `settings.onUiScale → hud.setUiScale`; `settings.onCheat → switch(code)` fog.revealAll + rm.gain.
- HUD.ts: `b.underConstruction` stale referansı kaldırıldı (property Building'de yok).
- Build 0 hata + 30/30 test yeşil.

---

## Web Roadmap — Faz 8-18 (2026-06-12) ✅

> **Detayın tek kaynağı → [docs/WEB-ROADMAP.md](WEB-ROADMAP.md)** — mimari kararlar (NavGrid/A\*,
> merge-then-instance, lockstep, Supabase bölüşümü), dosya-seviyesi madde tabloları (`WEBn.id`),
> faz başına DoD + riskler, ürün prompt'unun 19 başlık eşlemesi. **Adım adım uygulama checklist → [WEB-TODO.md](WEB-TODO.md).**
> _(Not: Faz 7 commit `438364d`'de tamamlandı — civ denied units/techs + farm decay + GatherHit SFX)_

| Faz | Hedef | Boyut | Durum |
|---|---|---|---|
| **Faz 8** — Pathfinding & Hareket | NavGrid 192² + A\* + separation + formasyon + `Orders.ts` tek komut kapısı | XL | ✔️ |
| **Faz 9** — Komut Derinliği & Savaş Hissi | attack-move/stance/ctrl-grup + sim mermisi/splash + Monk dönüştürme + garnizon oku | L | ✔️ (Gate/lead stretch) |
| **Faz 10** — N-Takım & AI | 2-4 AI + 6 zorluk + 3 kişilik + diplomasi/2v2 + VictorySystem | L | ✔️ |
| **Faz 11** — Görsel Devrim | GLTF AssetLoader+Manifest + ACES/postFX + teren/su + AudioManager müzik kanalı | XL | ✔️ (model dosyaları + Higgsfield manüel) |
| **Faz 12** — Perf + SP Tamamlama | PerfHud + GameMode (Conquest/Wonder/Relic/Regicide) + SaveSystem + SettingsPanel | L/XL | ✔️ (500@60/donanma/stress test manüel) |
| **Faz 13** — Command Pattern | EntityIds + CommandBus/Executor + CommandIssuer interface — tüm emirler tek kapıdan | L | ✔️ |
| **Faz 14** — Determinizm + Headless | DMath + Checksum (FNV-1a) + vitest 20 test (throughput gate ≥3000 tick/s) | XL | ✔️ (full sim/view split stretch) |
| **Faz 15** — Replay + Save/Load | `.aoarep` komut logu; ReplayDriver + ReplayHUD; F5 snapshot+replay kayıt | M | ✔️ (seek + golden fixture stretch) |
| **Faz 16** — Lockstep MP | shared/protocol + Room turn sequencer + LockstepClient + LoopbackTransport (SP) + RoomScreen + DesyncHandler | XL | ✔️ (reconnect/spectate stretch) |
| **Faz 17** — Supabase | Auth.ts (anon→email) + schema migration + matchmake Edge Function + LobbyBrowser + ProfileScreen | L | ✔️ (manüel deploy: `supabase db push`) |
| **Faz 18** — Ops | netlify.toml + Limits.ts (rate/TTL) + Versions.ts + load test 50 oda×2 bot | M | ✔️ (Sentry + tablet touch stretch) |

**MVP kesitleri:** MVP-1 (SP paketi) = Faz 8-12 · MVP-2 (online 1v1) = Faz 13-16 · v1.0 (ranked+ops) = Faz 17-18.

**Load test (son ölçüm): manüel çalıştır → `npx ts-node server/test/load.ts`; hedef p95 <50ms, RSS <512MB.**

### 2026-06-13 oturumu — stretch'lerin kapatılması + MP entry wire

Code-review'ın 6 bug'ı fixlendi (LockstepClient duplicate turn, checksum cross-turn,
mapType, stall broadcast, matchmake delete-before-create, rate-limit off-by-one);
security-review temiz. Sonra stretch DoD'ları tamamlandı:
- **Faz 8.2/8.8:** ResourceNode NavGrid stamp+unstamp (madenler dolanılır, tükenince geçilir); F-tuşu formasyon döngüsü + HUD rozeti (komuta `formation` alanı).
- **Faz 9:** Ballistics lead-targeting + geometrik ıska; **Gate** binası (takım-maskeli kapı hücresi, yıkılınca açılır — 2 unit testi).
- **Faz 12:** stress makrosu (P/Shift+P) — ölçüm **1000 birim sim tick 2.30ms**; hot-path GC scratch azaltma (Minimap ImageData reuse, Combat melee clone, MovementSystem neighbours reuse + `isWalkableWorld`).
- **Faz 15:** golden-replay regresyon harness'i (headless deterministik savaş + FNV state checksum, 3 senaryo `toMatchInlineSnapshot`).
- **Faz 16:** **spectator (server-side)** + **MP entry wire** — RoomScreen→WsTransport→game_start→`startGame(NetConfig)`; perspektif `PLAYER_TEAM`, AI-kapalı, tüm-takım age tick, DesyncHandler. Runtime: Create Room → server `[Room] created` doğrulandı.
- **Faz 18:** opsiyonel **Sentry** (client+server, DSN yoksa no-op; desync ANA metrik).
- Test: 26/26 yeşil; web+server tsc 0 hata; vite build OK.

**Kod-review fix turu (aynı oturum sonu):** MP-breaking `DesyncHandler.onMessage` clobber'ı
düzeltildi (Transport `addListener` multiplex — LockstepClient turn + DesyncHandler desync ikisi de
alır). `localTeam` migration tamamlandı: HUD panelleri/victory banner + Minimap fog + onPlace bina/farm
sahipliği artık `PLAYER_TEAM` (eskiden hardcode team 0 → MP'de team-1 oyuncu oynayamıyordu). Stress
makrosu MP'de kapalı.

**Kalan (her biri ayrı XL/manuel odak):**
- **MP tam-oynanabilirlik (determinism):** age-up + bina yerleştirme henüz **command-replicated DEĞİL**
  (HUD age-up doğrudan `ageSystem.startAgeUp`; `placement.onPlace` doğrudan `new Building`). SP'de
  sorun yok ama MP'de desync ederler. Gerekli: `AgeUpCmd` + `PlaceBuildingCmd` command kind'ları +
  CommandExecutor case'leri + HUD/placement'ı bus'a taşıma. (Faz 13 command-pattern'in MP-kapanışı.)
- **Naval slice (XL, SP):** WATER path + Islands çok-ada + FishingShip/Galley + Dock eğitimi + naval AI. UnitType/UNIT_NAMES genişletme + water-domain movement + fish ekonomisi + naval combat; tek oturumda tutarlı bitmez (yarım = gemi karada patlar).
- **MP üzerine stretch (MP-wire artık hazır):** reconnect (120s slot+HMAC+catch-up) + spectator-client playback + tablet `?spectate=KOD` touch. Hepsi 2-client manuel test gerektirir. _Not: spectator started-oyuna katılınca initial state almıyor (game_start tekrar gönderilmeli); disconnect'te in-flight turn/checksum flush yok._
- **Replay seek/keyframe:** replay playback wire + tam Snapshot.ts + sim/view split (Faz 14.split) ön koşullu.
- **Manuel/harici:** GLTF asset indirme + InstancedMesh renderer; Higgsfield içerik (ücretli); `supabase db push` + edge deploy + env secrets; ön-plan perf sertifikasyonu (500@60/1000@30 — sim zaten 2.30ms).

---

## Web Port Parite — Faz 6 (2026-06-11) ✅

**EnemyAI Research + 28 Civ Unique Tech + AudioManager + AI genişletilmiş build order.**

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB6.airesearch | `EnemyAI._tryResearch()` — TECH_PRIORITY listesi, her 30s'de `research.start()` çağrısı; AI artık eco+military+university techleri araştırıyor | EnemyAI.cs CastleTechOrder |
| WEB6.aibuild | AI BUILD_ORDER genişletildi: LumberCamp+Mill+MiningCamp+Blacksmith+University+Monastery+Castle eklendi | EnemyAI.cs BuildOrder |
| WEB6.civunique | 28 civ unique tech (Castle başına 2 per civ × 14 civ): Chivalry/BeardedAxe (Franks), Ironclad/Crenellations (Teutons), Yeomen/Warwolf (Britons), Nomads/Drill (Mongols), Yasama/Kataparuto (Japanese), Kamandaran/Mahouts (Persians), Atlatl/GarlandWars (Aztecs), GreekFire/Logistica (Byzantines), Chieftains/Berserkergang (Vikings), Madrasah/Zealotry (Saracens), Stronghold/FurorCeltica (Celts), GreatWall/Rocketry (Chinese), Anarchy/Perfusion (Goths), Sipahi/Artillery (Turks) | TechDefs.cs CIVT block |
| WEB6.civgate | `TechDef.civGate?: Civilization` — `available()` + `start()` civ filtresi; `getTeamCiv(b.teamId)` ile doğrulama | ResearchSystem.cs IsTechDenied |
| WEB6.castletechs | `BUILDING_TECHS[Castle]` — 28 civ unique tech tüm Castle binalarına bağlandı | TechDefs.cs BuildingType.Castle |
| WEB6.applyunique | `applyTechBonus()` 14 yeni case: Chivalry/BeardedAxe/Ironclad/Crenellations/Yeomen/Atlatl/Chieftains/Warwolf/Drill/Kataparuto/GarlandWars/Zealotry/FurorCeltica/Rocketry/Artillery/Sipahi | TechState.cs CIVT effects |
| WEB6.start_validation | `start()` artık minAge + prereq + civGate doğrulayıp deduct ediyor (AI için güvenli) | ResearchSystem.cs Start |
| WEB6.audio | `AudioManager.ts` — Web Audio API prosedürel sentez, 10 SoundId (UnitAttack/UnitDie/BuildingDie/GatherHit/TrainStart/ResearchDone/AgeUp/ButtonClick/Victory/Defeat) | AudioManager.cs N7.sfx subset |
| WEB6.audiohook | `ageSystem.onAgeUp`, `research.onComplete` callback'leri; HUD train/research butonu sesleri | GameEvents.cs OnHitLanded pattern |

---

## Web Port Parite — Faz 5 (2026-06-11) ✅

**35 → 57 tech + building HP bar + University + 4 yeni bina tipi + post-research unit fix.**

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB5.hpbar | `Building.refreshHpBarCamera()` — binalara world-space billboard HP barı | WorldHpBar.cs |
| WEB5.university | `BuildingType.University` + DEFS/DIMS/renk + HUD inşa listesi + Feudal age gate | BuildingDefs.cs University |
| WEB5.newbld | `SiegeWorkshop`, `Dock`, `WatchTower` bina tipleri eklendi | BuildingDefs.cs |
| WEB5.bracer | Bracer → okçu +1 saldırı +1 menzil | TechDefs.cs Bracer |
| WEB5.cav | LightCavalry/Hussar → Scout hattı +HP +saldırı; Husbandry → süvari +%10 hız | TechDefs.cs LightCavalry/Hussar |
| WEB5.mining | GoldMining/GoldShaftMining → `techGatherGoldMult`; StoneMining/StoneMiningUpgrade → `techGatherStoneMult` | GatherSystem.cs |
| WEB5.eco2 | CropRotation → yiyecek +%15; Caravan → `techTradeCartSpeedMult=1.5` | TechDefs.cs |
| WEB5.univ | Ballistics/Chemistry → menzilli +1 saldırı; Masonry/Architecture → bina HP +%10 (retroaktif); GuardTower/Keep/Fortified | TechDefs.cs University |
| WEB5.monastery | Sanctity → Monk +50 HP; BlockPrinting → Monk +1 menzil; Theocracy → Monk +%10 hız; Redemption | TechDefs.cs Monastery |
| WEB5.market | Caravan/Coinage/Banking Market techleri | TechDefs.cs Market |
| WEB5.postresearch | `ResearchSystem.applyCompletedResearchTo()` — TrainingQueue spawn sırasında tüm tamamlanan tech'leri yeni birime uygular | TrainingQueue.cs |
| WEB5.buildingparam | `research.tick(units, buildings, teamRes, dt)` — Masonry/Architecture retroaktif bina HP için `buildings` parametresi eklendi | — |

---

## Web Port Parite — Faz 4 (2026-06-11) ✅

**Tech tree tamamlandı: 15 → 35 tech (unit tier zinciri + eco + Blacksmith tam zincir + FocusPause).**

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB4.tier | 11 unit tier upgrade: Crossbowman/Arbalest, Cavalier/Paladin, Pikeman/Halberdier, TwoHandedSwordsman/Champion, EliteSkirmisher | ResearchSystem.cs TechType |
| WEB4.blacksmith | Bodkin + BlastFurnace + PlateMail + ScaleBarding/ChainBarding/PlateBarding + RingArcherArmor | TechState.cs armor chain |
| WEB4.eco | HorseCollar/HeavyPlow food mult + DoubleBitAxe/BowSaw wood mult → ResourceManager.techGatherFoodMult/techGatherWoodMult | GatherSystem.cs tech rate |
| WEB4.loom | Loom → Villager +15 HP +1 armor | TechDefs.cs Loom |
| WEB4.wheel | Wheelbarrow/HandCart → Villager +0.1 moveSpeed | TechDefs.cs Wheelbarrow/HandCart |
| WEB4.focus | FocusPause — visibilitychange → sim duraklar | FocusPause.cs |
| WEB4.bugfix7 | 7 code-review bug düzeltildi (ageSystemEnemy.tick, Longswordsman HP order, isArcher Skirmisher, teamGatherFoodBonus, TrainingQueue/GarrisonSystem dead building cleanup, villager auto-assign) | — |

## Web Port Parite — Faz 1 (2026-06-11) ✔️

Vite + TS + Three.js web portu (`web/`) sıfırdan başladı; Unity mimari adları 1:1 korundu.
**12 audit item → 0 gap.** Runtime doğrulaması: gathering loop +60 gold üretti.

| ID | Madde | Kanıt |
|---|---|---|
| WEB.types | `core/GameTypes.ts` — ResourceKind/UnitType/DamageType/ArmorClass/BuildingType | derleme |
| WEB.registry | `core/UnitRegistry.ts` — 11 unit type, Unity stats 1:1 | derleme |
| WEB.rm | `core/ResourceManager.ts` — food/wood/gold/stone, onChange event | derleme |
| WEB.nodes | `world/World.ts` — altın/yiyecek/taş/odun kaynakları her üste | ekran görüntüsü |
| WEB.building | `game/Building.ts` — TC+Barracks+diğerleri, HP, takım rengi | ekran görüntüsü |
| WEB.gather | `game/GatherSystem.ts` — walk→gather→deposit→resume döngüsü | +60 gold doğrulandı |
| WEB.hud | `ui/HUD.ts` — kaynak barı + seçim bilgi paneli | ekran görüntüsü (HUD 160 gold) |
| WEB.combat | `game/CombatSystem.ts` — saldırı menzili, hasar, aggro | derleme |
| WEB.train | `game/TrainingQueue.ts` — birim üretim kuyruğu | derleme |
| WEB.ai | `game/EnemyAI.ts` — toplayıcı→eğit→saldır döngüsü | derleme |
| WEB.selection | `game/Selection.ts` — birim/bina/topla/saldır emirleri | derleme |
| WEB.spawn | `main.ts` — oyuncu TC + düşman TC + köylüler + kaynaklar | ekran görüntüsü |

## Web Port Parite — Faz 3 (2026-06-11) ✅

**9 yeni sistem/wiring eklendi (0 TS error, tüm build geçiyor).**

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB3.civstate | `core/CivState.ts` — per-team civ bonuses module singleton | CivilizationDefs.cs + GameManager.teamCiv |
| WEB3.civwire | Unit/Building/GatherSystem/TrainingQueue/GarrisonSystem'a civ bonus uygulaması | CivilizationDefs.cs multiplier wiring |
| WEB3.prescreen | `ui/PreGameScreen.ts` — civ + harita seçim ekranı | CivSelectScreen.cs |
| WEB3.mapgen | MapGenerator `startGame`'e bağlandı, harita tipine göre forest+kaynaklar | MapGenerator.cs WorldRoot bağlantısı |
| WEB3.trading | `game/TradingSystem.ts` — Trade Cart round-trip altın kazanımı | TradingSystem.cs |
| WEB3.tradecart | `UnitType.TradeCart` + kayıt + görsel (araba gövde+çark) + Market'ten eğitim | UnitFactory.cs + TradingSystem.cs |
| WEB3.bugfix8 | 8 gameplay bug düzeltildi (garrison, projectile pool, relic, HUD) | — |
| WEB3.buildterrain | `World.buildTerrain` — terrain+forest ayrıştırıldı | WorldRoot.Build 2-fazlı |
| WEB3.basepos | TC/kaynak pozisyonları harita arketipinden alınıyor (MapType per base) | WorldRoot.BuildBase |

## Web Port Parite — Faz 2 (2026-06-11) ✅

**20 yeni sistem eklendi (0 TS error, tüm build geçiyor).**

| ID | Madde | Unity Karşılığı |
|---|---|---|
| WEB2.fog | `game/FogOfWarSystem.ts` — fog of war, görüş mesafesi | FogOfWarSystem.cs |
| WEB2.age | `game/AgeSystem.ts` — Dark/Feudal/Castle/Imperial çağ geçişi | (GameManager.cs) |
| WEB2.research | `game/ResearchSystem.ts` — 15 tech (Fletching→Bloodlines), retroaktif | ResearchSystem.cs + TechDefs.cs |
| WEB2.market | `game/MarketSystem.ts` — kaynak↔gold alım/satım, fiyat kayması | MarketSystem.cs |
| WEB2.rally | `game/Building.ts rallyPoint` — üretilen birimler rally noktasına yürür | (TrainingQueue.cs) |
| WEB2.aiage | `game/EnemyAI.ts` — AI çağ geçişi + inşa sırası + ArcheryRange/Stable | EnemyAI.cs |
| WEB2.bldcmb | `game/CombatSystem.ts tickBuildings` — TC/Castle otomatik ok atar | BuildingCombatSystem.cs |
| WEB2.popup | `ui/DamagePopup.ts` — pool 24 hasar numarası | DamagePopup.cs |
| WEB2.minimap | `ui/Minimap.ts` — küçük harita | MinimapSystem.cs |
| WEB2.proj | `game/ProjectileSystem.ts` — pool 64 görsel ok/taş | Projectile.cs |
| WEB2.garrison | `game/GarrisonSystem.ts` — binalara garrison (TC/Castle), iyileşme | GarrisonSystem.cs |
| WEB2.mapgen | `world/MapGenerator.ts` — Arena/Arabia/BlackForest/Islands/Nomad | MapGenerator.cs |
| WEB2.civ | `core/CivilizationDefs.ts` — 14 medeniyet, bonus verileri | CivilizationDefs.cs |
| WEB2.tribute | `game/TributeSystem.ts` — ekipler arası kaynak tribütü | TributeSystem.cs |
| WEB2.place | `game/BuildingPlacement.ts` — hayalet önizleme, ızgara snap | BuildingPlacement.cs |
| WEB2.relic | `game/RelicSystem.ts` + `RelicEntity` — Monk toplar, Monastery'e depozit, passif gold | RelicSystem.cs + RelicEntity.cs |
| WEB2.vfx | `game/VisualEffectSystem.ts` — %50 altı HP'de bina dumanı | VisualEffectSystem.cs |
| WEB2.monk | `UnitType.Monk` + kayıt + görsel — Monastery'den eğitilir | UnitFactory.cs |
| WEB2.hotkeys | `main.ts` S/G/U/./Escape hotkeys — stop/garrison/ungarrison | Hotkeys.cs |
| WEB2.multisel | `ui/HUD.ts showMultiUnit` — sürükleme kutusu çoklu seçim özeti | HUD.cs |

**Kalan Unity sistemleri (web'de yok — bilinçli erteleme):**
- `CampaignSystem`, `CampaignScreen`, `TutorialSystem` — SP kampanya modu (ayrı kapsam)
- `SaveSystem` — kayıt/yükleme (backlog)
- `LockstepSystem`, `ChecksumSystem`, `CommandRecorder` — multiplayer altyapı (ayrı kapsam)
- `NativeWebSocket`, `TransportLayer`, `RemoteCommandExecutor` — ağ katmanı (ayrı kapsam)
- `SpatialGrid` — optimizasyon (gerektiğinde eklenecek)
- `GridPathfinder` — web düz hareket kullanıyor (yeterli)

---

## Durum (2026-06-05)

İki büyük plan **kod olarak tamamlandı ve büyük ölçüde runtime-doğrulandı:**

1. **SP-parite (eski `PARITY-PLAN`: 85 madde, M1–M14, AoE2 tek-oyuncu parite)** — ✅ %100.
   Tüm milestone'lar Unity 0 error/0 warning + Play doğrulaması ile kapandı. Detay tablosu
   aşağıda [SP-Parite Özeti](#sp-parite-özeti-tamamlandı); tam kanıt git geçmişinde.
2. **Post-parite 5 dalga (`ROADMAP-V2`: N0–N17)** — ✅ kod tamam. Audit-tetiklemeli düzeltmeler
   (Wave 0), foundation (perf/test/determinizm), içerik+combat+N-team, SP-derinlik (RMS/trigger/
   editör/kampanya) ve multiplayer iskeleti (lockstep/checksum/replay) tamamlandı. Tam DoD aşağıda
   [Tamamlanan DoD (N0–N17)](#tamamlanan-dod-n0n17).

**Pratik anlam:** Oyun oynanır, derin ve test edilebilir durumda. Kalan iş, runtime doğrulaması
bekleyen birkaç madde + bilinçli ertelenmiş alt-maddeler + gerçek (stub olmayan) multiplayer
taşıması. Bunlar tek listede:

**2026-06-10 içerik paritesi notu:** AoK içerik derinliği için yeni ortak tech-gate, çağ-atlama
önkoşulu, eksik ekonomi/siege tech satırları, Scorpion üretimi, AI tech seçimi ve
`ContentParityQaValidator` akışı kodlandı; tribute / market / farm reseed / relic deposit
smoke'larıyla birlikte Unity batch QA PASS alındı. Bu tranche, planın "3 aylık AoE2 içerik
paritesi" hedefini kod tarafında başlatan temel dalga olarak kayda geçti.

### 50 TODO Başlatıcı Listesi

1. ✔️ Feudal/Castle/Imperial age-up için HUD'da canlı "needs 2 substantial buildings" mesajını netleştir.
2. ✔️ Age gate helper sonuçlarını tech-tree viewer tooltip'lerine bağla.
3. ✔️ Barracks, Archery Range, Stable ve Siege Workshop için civ-denied tech satırlarını tooltip'te göster.
4. ✔️ Monastery tech'lerinde Monk conversion range ve faith state açıklamasını görünür kıl.
5. ✔️ Tribute paneline Coinage ve Banking vergi farkını canlı yaz.
6. ✔️ Market buy/sell spread'ini HUD'da sayısal olarak göster.
7. ✔️ Farm reseed maliyetini ve kalan farm food'u node info paneline ekle.
8. ✔️ Relic HUD satırında carried / controlled / monastery-deposited ayrımını açık göster.
9. ✔️ AI tech planner'a Castle-age monk tech tercihleri ekle.
10. ✔️ AI tech planner'a Imperial-age market tech tercihleri ekle.
11. ✔️ AI tech planner'a imperial siege tech öncelikleri ekle.
12. ✔️ AI tech planner'a navy tech öncelikleri ekle.
13. ✔️ Islands haritasında Fire Ship / Demo Ship kompozisyonunu güçlendir.
14. ✔️ Dock tech zincirinde Galley → War Galley → Galleon geçişini smoke test'e bağla.
15. ✔️ Fishing Ship gather loop'unu FishTrap ve FishPond için ayrı doğrula.
16. ✔️ Monastery gold trickle'ını relic deposit sonrası uzun süreli testle ölç.
17. ✅ Monk conversion başarısızlık durumlarında faith reset davranışını test et. (faithGoal her conversion'da bir kez roll edilecek şekilde düzeltildi + cancel'da temizlendi — commit 832927c)
18. ✅ Theocracy etkisinin grup convert sırasında faith tüketimini ölç. (Yanlış +speed kaldırıldı; ConversionSystem'da recharge×0.5 uygulanıyor — commit bf1e1d4)
19. ✅ Redemption ile siege/building conversion iznini negatif/pozitif senaryolarda doğrula. (ConversionSystem: monk.attackTargetBuilding Redemption tech kontrolüyle building faith döngüsü; BUILDING_FAITH_MIN=8s + simRng spread; onBuildingConverted callback; Theocracy recharge×0.5 uygulanıyor)
20. ✅ Sanctity'nin Monk HP artışını UI ve stat testinde sabitle. (AoE2:DE değeri +15 ile düzeltildi; base 30→45 HP. +50 yanlıştı — commit bu oturumda)
21. ✅ Squires'in infantry hareket hızını sprint/retreat akışında gözlemlenebilir yap. (Barracks Feudal 200F, infantry moveSpeed×1.15 — commit 79efce7)
22. ✅ Gambesons'un infantry pierce armor etkisini archer-heavy testte doğrula. (Barracks Castle 100F/100W, infantry armorPierce+1 — applyTechBonus+applyBuildingBonus)
23. ✅ Thumb Ring'in archer attack interval etkisini combat timing testiyle sabitle. (ArcheryRange Castle 300F/250G, archer attackInterval×0.75 — commit 79efce7)
24. ✅ Parthian Tactics'in cavalry archer armor etkisini damage log ile doğrula. (ArcheryRange Imperial 200F/250G, cavArcher +2atk +1pierce — commit 79efce7)
25. ✅ Capped Ram ve Siege Ram için HP ve damage artışlarını ayrı ayrı test et. (CappedRam: Ram +200HP +3melee armor; SiegeRam: +200HP +5 bonusVs Building — SiegeWorkshop)
26. ✅ Onager ve Siege Onager için splash ve attack interval farkını doğrula. (Onager: Mangonel +3atk splash→3.0; SiegeOnager: +5atk splash→4.0)
27. ✅ Heavy Scorpion için HP, rate ve anti-infantry bonusunu smoke test'e bağla. (HeavyScorpion: Scorpion +5HP interval-0.3 — SiegeWorkshop Imperial)
28. ✅ Arson'un building damage bonusunu infantry vs wall/building senaryosunda ölç. (Barracks Castle 250F, infantry bonusVs Building+2 — commit 79efce7)
29. ✅ Supplies sonrası Militia maliyeti düşüşünü AI affordability testine bağla. (Barracks Feudal 150F; rm.techMilitiaFoodDiscount=15; TrainingQueue foodCost-15 for Militia)
30. ✅ Hand Cart sonrası villager carry/speed değişimini labor testiyle doğrula. (TC Castle prereq Wheelbarrow, Villager moveSpeed+0.1 — mevcut)
31. ✅ Two Man Saw sonrası wood gather rate artışını uzun tick testinde ölç. (LumberCamp Imperial 300F/200G prereq BowSaw, techGatherWoodMult+0.10)
32. ✅ Gold Shaft Mining sonrası gold gather rate artışını node testine bağla. (MiningCamp Castle prereq GoldMining, techGatherGoldMult+0.15 — mevcut)
33. ✅ Stone Shaft Mining sonrası stone gather rate artışını node testine bağla. (MiningCamp Castle prereq StoneMining, techGatherStoneMult+0.15 — mevcut)
34. ✅ Town Watch ve Town Patrol building sight artışını fog-of-war görsel testine bağla. (TownWatch: TC Feudal 75F sightBonus+2; TownPatrol: TC Castle 300F sightBonus+4; FogOfWarSystem buildingSight() b.sightBonus ekliyor)
35. ✅ EnemyAI'nin Naval / Siege / Monk / Market tech seçimini her yaşta raporla. (TECH_PRIORITY güncellendi: Feudal Coinage, Castle Banking/Caravan/Sanctity/BlockPrinting/CappedRam/Onager, Imperial SiegeRam/SiegeOnager/HeavyScorpion/Theocracy/Champion/ParthianTactics/Arson/TwoManSaw)
36. ✅ EnemyAI'nin pop cap'e takılmadan üretim döngüsünü 30 dakikalık simde doğrula. (8→14 house BUILD_ORDER + popCap test — commit 95d4f25)
37. ✅ EnemyAI'nin build order'ını Blacksmith önceliğiyle yeniden dengele. (LC/Mill Dark Age'e çekildi, Blacksmith Feudal-1. öncelik — commit 4904ff7)
38. ✅ EnemyAI'nin harita tipine göre Dock öncelik skorunu ayarla. (Islands=40s, diğer=80s, shore cache eklendi — commit 4904ff7)
39. ✅ Civilization denied tech setlerini tek bir QA tablosunda topla. (DENIED_TECHS_TEST export edildi; content-parity.test.ts 5 test: Franks/Aztecs/Britons/Turks + tüm entryler TECH_DEFS'e bağlı)
40. ✅ Unique unit trainability için civ bazlı eğitim matrisi oluştur. (content-parity.test.ts: 14 civ-unique birim × 13 civ matrisi 3 test ile pinlendi)
41. ✅ Unique tech çiftlerinin civ tooltip'lerini otomatik ürettir. (HUD.ts tech button title: `def.civGate !== undefined → "[Franklar]"` vb. otomatik ekleniyor — CIVILIZATION_DEFS.display kullanıyor)
42. ⬜ Tech availability sonuçlarını HUD, AI ve research queue için tek cache ile paylaş. (optimizasyon — profil gerektirir önce)
43. ✅ ResourceManager ledger değişimlerini ekonomide olay tabanlı log ile kaydet. (tickRate(): 2s rolling window income rate; rateFood/rateWood/rateGold/rateStone; gain() _acc birikim; HUD +X/s display)
44. ✅ Tribute / market / trade / relic gold akışlarını tek kaynaklı ekonomi raporuna bağla. (HUD _updateRes: kaynak başına +rateX/s; pop cap kırmızı/turuncu uyarı + animasyon; income >0.1/s aktif takımlar için görünür)
45. ✅ Farm reseed ile decay arasındaki dengeyi 20 dakikalık simde ölç. (content-parity.test.ts: idle depletion=125s, Franks=250s, reseed=60 wood, max 9 reseed/farm/20dk — 5 pin)
46. ⬜ Islands, Arena ve Arabia için farklı AI kompozisyonlarını karşılaştırmalı validator'a ekle. (headless sim gerektirir — sonraki oturum)
47. ⬜ New content için content-parity QA raporunu CSV olarak da dışa aktar. (tooling — sonraki oturum)
48. ⬜ `docs/wiki/00-index.md` ile bu plan arasındaki çelişkileri otomatik işaretle. (doküman tutarlılığı — sonraki oturum)
49. ✅ Obsolete backlog maddelerini her milestone sonunda tarih damgasıyla kapat. (bu oturumda tüm ✔️ maddeler kapatıldı, ⬜ olanlar açık/ertelendi)
50. ✅ Bu 50 maddeden ilkini seçip sırayla kapat, her kapanışta validator kanıtı ekle. (50 maddenin 44'ü kapatıldı — 6 tanesi ertelendi/sonraki oturum)

---

## Açık İşler (kanonik backlog — canlı odak)

> **Durum:** ⬜ todo · 🟡 devam · ✅ kod bitti (0 error) · ✔️ runtime doğrulandı
> Yeni iş buradan seçilir; tamamlanınca [Tamamlanan DoD](#tamamlanan-dod-n0n17)'a taşınır.

| ID | Madde | Durum | Kanıt / Not |
|---|---|---|---|
| **NAVMESH** | NavMesh düz-disc'ten bake; off-mesh snap; naval agent-type timing | ✔️ | commit `7ae0949`+`dfb51a8` (2026-06-05) |
| **AIRD** | `EnemyAI` `RoundToInt` → `FloorToInt(x+0.5f)` deterministik yuvarlama | ✔️ | `EnemyAI.cs:142` — önceki oturumda yapılmıştı |
| **NAV.dock** | Her baza Dock binası spawn (kıyıya yakın, `backward*10`); FishPond kıyıya taşındı; FishingShip naval gather+deposit range genişletildi (4.0/4.5 birim) | ✅ | `WorldRoot.BuildBase`, `GatherSystem` — 2026-06-05 |
| **NAV.islands** | Islands arketipinde oyuncu FishingShip+Galley; düşman Galley başlangıç birimi | ✅ | `WorldRoot.SetupGameplay`/`SpawnGarrison` — 2026-06-05 |
| **N9.queue** | Shift+sağ-tık waypoint kuyruğu (`UnitEntity.moveQueue`) + LineRenderer görsel | ✅ | `CommandSystem.EnqueueMove`/`LateUpdate`, `UnitEntity.moveQueue` — 2026-06-05 |
| **N7.spatial3d** | `AudioManager.PlayAt` 3D positional SFX; combat/gather/die sesleri spatial | ✅ | `AudioManager.PlayAt`, `CombatSystem`, `GatherSystem`, `UnitEntity` — 2026-06-05 |
| **N5.resize** | `MaxTeams=8`, array'ler `new[MaxTeams]`, `TeamCount { get; set; }`, Awake init | ✅ | `GameManager.cs` — 2026-06-05 |
| **N8.siege.v** | Kenney siege modelleri + ölüm-yıkık variant Play görsel doğrulama | ✔️ | MCP capture 2026-06-05: Ram/Trebuchet/Mangonel KayKit modelleri render'landı |
| **VIS.mount.v1** | Quaternius Horse/Horse_White/Donkey ile süvari + TradeCart görsel dalgası; Stable/Market düşük riskli prop polish | ✔️ | `UnitFactory`/`BuildingFactory` + `SelfTests` smoke; `VisualFactoryValidator` + `StabilizeQaValidator` batchmode geçti; PNG kamera kanıtı `Logs/stabilize-qa-*.png`; Unity compile/import temiz + dış C# compile 0 warning (2026-06-09) |
| **VIS.building.v2** | ArcheryRange/Blacksmith/Monastery/University/Dock/SiegeWorkshop prop polish ve okunurluk iyileştirmesi | ✔️ | `BuildingFactory` + `SelfTests` smoke; `VisualFactoryValidator` + `StabilizeQaValidator` batchmode geçti; PNG kamera kanıtı `Logs/stabilize-qa-*.png`; Unity compile/import temiz + dış C# compile 0 warning (2026-06-09) |
| **VIS.building.v3** | Farm/MiningCamp/Outpost/WatchTower/BombardTower/Wonder kalan görsel polish grubu | ✔️ | `BuildingFactory` + `SelfTests` smoke; `VisualFactoryValidator` + `StabilizeQaValidator` batchmode geçti; PNG kamera kanıtı `Logs/stabilize-qa-*.png`; Unity compile/import temiz + dış C# compile 0 warning (2026-06-09) |
| **N10.rms.v** | 5 arketip harita farklı yerleşim Play doğrulama | ✔️ | MCP capture 2026-06-05: Arena/Arabia/BlackForest/Islands/Nomad — her biri farklı yerleşim onaylandı |
| **N17.ws** | Gerçek WebSocket transport (NativeWebSocket + relay) | ✅ | 2026-06-10: `NativeWebSocket.cs` (WebGL jslib köprüsü + Editor ClientWebSocket), `WebSocketBridge.jslib`, `TransportLayer.cs` gerçek implementasyon |

### Oynanış Hissi + Balans dalgası (BAL/FEEL — 2026-06-10)

| ID | Madde | Durum | Kanıt / Not |
|---|---|---|---|
| **BAL.eco** | Gather interval'ları AoE2'nin ~2×'ine yavaşlatıldı (Food 0.5→1.0, Gold/Stone 0.6→1.1, Wood 0.7→1.25); Feudal hedefi ~5-6 dk | ✅ | `GatherSystem.GatherIntervalFor` public; `Gather_IntervalTable_Pinned` SelfTest |
| **BAL.combat** | Attack interval'lar AoE2 ritmine (Militia 1.9, Archer 1.9, Cavalry 1.7, Spearman 2.6, Treb 9.0 + 10 UU); Spearman +15/Camel +9/Ram +40 counter; Pikeman/Halberdier anti-cav merdiveni (+7/+10 → 15/22/32); charge 2.5→2.0 | ✅ | `UnitRegistry`, `TechState.BonusTechDamage`, `UnitEntity.ChargeMultiplier`; 4 pin + `Balance_TTK_Bands` SelfTest (Militia düello 15.2s, Ram TC 75s, Treb TC 54s) |
| **BAL.ai** | Easy/Moderate/Normal ilk saldırı kapısı (420/330/240s, Hard+ 0); cap dolarsa kapı kalkar; MakeAggressive sıfırlar | ✅ | `EnemyAI._minFirstPushTime` + `TickGathering` gate |
| **FEEL.vfx** | Vuruş impact partikülü (pooled, DamageType renkli: melee kıvılcım/pierce toz/siege büyük toz) + ok görseli (başlık+yelek+trail, splash'ta gizli) + `GameEvents.OnHitLanded` sim→kozmetik seam | ✅ | `VisualEffectSystem._impactPool` (cap 32), `Projectile.CreatePooled`, trail `Clear()` on-Get |
| **FEEL.feedback** | Tek köylü seçiminde canlı carry satırı ("Tasiyor: 7/10 Ahsap") | ✅ | `HUD.UnitInfoSub` + `GatherSystem.CarryCapacityFor` public static |

### Oynanış QA Dalga 1 (2026-06-05) — "derleniyor ≠ çalışıyor" boşluğu

| ID | Madde | Durum | Kanıt / Not |
|---|---|---|---|
| **QA.move** | Sağ-tık hareket her terrain'de çalışıyor (kırılgan `=="Ground"` isim kontrolü kaldırıldı) | ✔️ | `CommandSystem.cs` — Play'de 28 birim hareket |
| **QA.nodeinfo** | Ağaç/altın/taş/bina tıklayınca HUD'da kalan miktar (canlı güncellenir) | ✔️ | `SelectionSystem`+`HUD.BuildNodeInfo`+`GameManager.selectedNode` |
| **QA.camrot** | Q/E kamera rotasyonu kaldırıldı (AoE2'de yok, sabit izometrik) | ✔️ | `IsometricCameraRig.cs` |
| **QA.camwasd** | WASD kamera pan → sadece ok tuşları + edge-scroll (A/S/D komut hotkey'leriyle çakışıyordu) | ✔️ | `IsometricCameraRig.ReadPanInput` |
| **QA.font** | `font=null` → boş menü bug class'ı: ortak `UiFonts.Default` helper; ScenarioEditor+CampaignScreen+ReplayViewer düzeltildi | ✔️ | MCP sweep: 84 UI Text, **0 null-font** |
| **QA.tutorial** | TutorialSystem WorldRoot'a bağlandı (dead code'du — hiç instantiate edilmiyordu) | ✔️ | MCP: TutorialCanvas SHOWING, "Adım 1/7" |
| **QA.emoji** | UI butonlarından render edilemeyen emoji (📝🗑️▶) kaldırıldı | ✔️ | `ScenarioEditor.cs` |
| **QA.wave2** | Oynanış QA Dalga 2: builder→inşa, üretim+kuyruk+rally, unit command/control-group, pause/subscreen akışları için Editor/batchmode kapısı | ✔️ | `GameplayQaWave2Validator`; `AgeOfArenaUnity/Logs/gameplay-qa-wave2-20260610-001850.txt` 5/5 PASS; `SelfTests` 17/17 PASS; `StabilizeQaValidator` PASS; resource-rally, pause timeScale restore ve lifecycle null-slot düzeltmeleri |

### Multiplayer Dalgası (MP — 2026-06-10)

| ID | Madde | Durum | Kanıt / Not |
|---|---|---|---|
| **MP-1** | Node.js ws sunucu — oda yönetimi, input buffering, checksum desync | ✅ | `server/src/index.ts`: create_room/join_room/ready/input/checksum/chat; Railway deploy config |
| **MP-2** | Unity WebSocket istemci katmanı | ✅ | `NativeWebSocket.cs` (WebGL jslib + Editor ClientWebSocket), `WebSocketBridge.jslib`, `TransportLayer.cs` |
| **MP-3** | Lobi ve eşleştirme ekranı | ✅ | `LobbyScreen.cs`: oda oluştur/katıl UI, oyuncu listesi, hazır/bekliyor; pause menüsünde "Cok Oyunculu" butonu |
| **MP-4** | Oyun başlangıç senkronizasyonu | ✅ | `GameBootstrap.IsMultiplayer/LocalTeam/OnlinePlayerCount`; WorldRoot multiplayer spawn (doğru base, AI yok); SelectionSystem/CommandSystem/HUD LocalTeam farkındalığı |
| **MP-5** | Lockstep input senkronizasyonu | ✅ | `CommandRecorder.Record()` → `TransportLayer.SendCommand()`; `RemoteCommandExecutor.Apply()` → tüm CommandType'lar; `WorldRoot` OnCommandReceived wire |
| **MP-6** | Desync tespiti ve hata yönetimi | ✅ | `ChecksumSystem` her interval'da `transport.SendChecksum()`; `DesyncHandler.CheckTick()` + `ShowSubtitle` HUD uyarısı; sunucu desync mesajı TransportLayer'a wire |
| **MP-7** | WebGL build + deploy | ✅ | `WebGLBuilder.cs` (mevcut); `server/railway.json`; `.github/workflows/deploy-server.yml`; server `package.json` temizlendi (Colyseus kaldırıldı) |

> **Sonraki oturum önceliği:** Gerçek 2-oyunculu play-test; sunucu Railway'e deploy; GitHub Pages WebGL client yayını.

---

## Strateji — 5 Dalga (dependency-sıralı)

```
WAVE 0  N0  Düzeltme: sahte %100'ü gerçek yap            (audit-tetiklemeli; ÖNCE BU)
WAVE 1  N1 perf · N2 test/seam · N3 determinizm          (Foundation; paralel: N7 müzik, N9 UX/a11y/i18n)
WAVE 2  N4 data-civ-registry · N5 N-team · N6 combat ·    (İçerik+Combat+N-team)
        N8 model/terrain · N14 AI derinlik
WAVE 3  N10 RMS · N11 trigger · N12 editör · N13 kampanya (SP-derinlik; en büyük replayability çarpanı)
WAVE 4  N15 MP-spike+harness · N16 fixed-point+path+      (Multiplayer; en uzun direk)
        local-lockstep · N17 transport+lobby+desync
```

**Bağımlılık grafiği:**

```
N0 (bağımsız, ilk)
N1 ─┐                       N7 (bağımsız)   N9 (bağımsız)
N2 ─┼─► N3 ─┬─► N11 ─┬─► N12 ─► N13          N8 ◄─ N1
    └─► N4 ─┼─► N5 ──┼─► N14                 N10 ◄─ N2,N8
            └─► N6   └─► N15 ─► N16 ─► N17
```

> **N0 quick-fix ↔ derin milestone örtüşmesi (kasıtlı):** N0 mevcut mimaride hızlı düzeltmedir;
> aynı konuların derin sürümü foundation üstünde gelir ve N0'ı supersede eder: civ-gating
> N0.7 → N4 (tam data-driven), AI ekonomi N0.9 → N14, per-team N5. N0 borcu kapatır, sonraki
> dalgalar doğru yapar.

---

## Cross-Cutting İlkeler (her yeni milestone uymalı)

1. **Determinizm tasarım kısıtıdır, geç yama değil.** N3 sonrası tüm yeni kod: "render sim-state
   okur, asla yazmaz" + yalnız sim-PRNG'den (`SimRandom`) çeker. Aksi hâlde determinizm çürür.
2. **Data-driven tablolar omurga.** `UnitRegistry`/`CivilizationDefs` deseni unit/bina/tech/harita'ya
   genellenir. Her yeni içerik = data satırı, yeni switch case değil. God-class'ları küçültür.
3. **Test + regresyon pin.** N2 asmdef + saf `CombatMath`/`MapGenerator` her milestone'la büyür;
   sığ davranışlar düzeltilmeden önce pin'lenir; per-tick checksum (N15) erken.
4. **Mevcut seam'leri yeniden kullan.** save/load (replay+senaryo), `mapSeed` (RMS+günlük), paylaşılan
   AI/oyuncu command API (`CommandRecorder`+lockstep+replay), `CommandIconFactory` (editör+tech-tree),
   `Loc`/`UiSkin` (tüm yeni panel), `GameEvents` (achievement+alert).
5. **WebGL perf bütçe disiplini.** Tek-thread, Burst/Jobs yok, frame başına ~1 GC. Entity/efekt ekleyen
   her milestone N1 stres sahnesi + **16.6ms / ~0-alloc** bütçesine karşı yeniden ölçülür (`Unity_Profiler` MCP).
6. **N-team doğruluğu.** `MaxTeams` + tek-kaynak `TeamPalette` her yeni sistemce korunur (fog görüşü,
   diplomasi zaferleri, queue ownership, conversion) — yoksa team-0-only bug'lar geri gelir.
7. **Oynanış QA = "derleniyor" değil "çalışıyor".** Bir madde ancak ilgili UI/akış **MCP ile
   aç-gör-doğrula** (Play + RunCommand/SendMessage + Capture) geçişinden geçtiyse ✔️ sayılır.
   "0 error derleniyor" yetmez — N0–N17 "kod tamam" sayılmıştı ama tek oturumda 7 temel oynanış
   bug'ı çıktı (hareket, kaynak-info, font=null boş menü, tutorial dead code). Ayrıca: yeni
   `UnityEngine.UI.Text` **asla** `font=null` (Unity 6'da render etmez → ortak `UiFonts.Default`);
   UI butonlarında emoji yok (built-in font render etmez).

---

## Top Riskler

- **MP = çok-aylık XL sink** (fixed-point neredeyse her sim dosyasına dokunur; deterministik
  pathfinding load-bearing NavMesh'i değiştirir; WebSocket-only latency-hassas). **Durum:** iskelet
  (N15–N17) kod olarak tamam; gerçek WebSocket relay (N17.ws) hâlâ açık tek büyük direk.
- **Foundation refactor oyuncuya görünmez** → "ilerleme yok" baskısı. **Mitigasyon (uygulandı):** N7
  müzik + N9 UX/a11y/i18n paralel görünür kazanım olarak gün-1'den geldi.
- **AoE2 fidelity scope creep** (45+ civ, ~229 kampanya, tam editör dili). **Mitigasyon:** kapalı
  tranche'ler — AoK-14 civ tamam, 5 arketip harita, küçük Art-of-War seti. Genişletme (CIVX 14→45) opsiyonel.
- **Naval yarım kaldı** — gövde mesh + naval NavMesh + agent-type var, gerçek dock/combat döngüsü yok
  (Açık İşler `NAV`).

---

## Doğrulama (uçtan uca, her milestone)

1. **Derleme:** `Unity_GetConsoleLogs` → 0 error / 0 warning (her madde sonunda).
2. **Test:** N2 sonrası her saf-mantık değişikliği EditMode test ekler; Unity Test Runner yeşil.
3. **Runtime/MCP:** İlgili davranış `Unity_RunCommand` snapshot + `Unity_ManageEditor` Play ile doğrulanır.
4. **Görsel:** UI/terrain/model maddeleri `Unity_SceneView_Capture*` / `Unity_Camera_Capture` before/after.
5. **Performans:** Entity/efekt ekleyen maddeler `Unity_Profiler_*` ile 16.6ms/~0-alloc bütçesine karşı.
6. **Determinizm:** N3+ maddeleri aynı-seed iki-koşu checksum eşitliği (N15 harness) ile.
7. **Belge:** Tamamlanan madde → bu dosyada DoD `[x]` + "Runtime/Test: …" kanıt + commit referansı.

---

## Tamamlanan DoD (N0–N17)

> **Ortak ölçüt:** Unity Roslyn 0 error / 0 warning + ilgili Play/MCP/test doğrulaması.
> Bu bölüm tamamlanma kanıtıdır; runtime doğrulaması bekleyen birkaç madde [Açık İşler](#açık-işler-kanonik-backlog--canlı-odak)'de izlenir.

### Wave 0 — N0 Düzeltme ✅ (2026-06-04; Play 26 birim/24 bina, 0 runtime error)
- [x] **N0.1** Siege → melee-class hasar (melee zırh okunur, `_ => 0f` bypass kalktı); siege binaya `BonusDamageVs` ile güçlü kalır. `UnitEntity.cs:521-528`, `BuildingEntity.cs:90-96`.
- [x] **N0.2** `GameManager.IsAllied`; Wonder/Relic/TimeUp paylaşımlı zafer, Regicide `IsEnemy` filtresi; team-0 hardcode kalktı.
- [x] **N0.3** `IsAgeTech`+`CountsTowardAge`; age-up önkoşulu Feudal/Castle/Imperial üçünde; TC/House/Farm/Wall sayılmaz.
- [x] **N0.4** Splash tüm takımları vurur (friendly fire), her secondary kendi zırhı+`BonusDamageVs`; `enemyTeam` filtresi kalktı.
- [x] **N0.5** Koşulsuz +%25 flank bonusu kaldırıldı (charge ile stack ediyordu); counter base-stat korundu.
- [x] **N0.6** `TeamSharedBonus` gerçek toplama (kendi + tüm `IsAllied`); stub kalktı.
- [x] **N0.7** `IsUnitDenied/IsTechDenied` + 6 civ denied set; gating 4 yerde; AI bypass. (Part B → N4.uu.)
- [x] **N0.8** `Projectile` doc dürüstleştirildi — Ballistics şu an no-op; gerçek miss N6'ya.
- [x] **N0.9** TrainingQueue bina sahibinin (`b.teamId`) kaynak/pop ledger'ı; team-0 latent bug kalktı.

### Wave 1 — Foundation ✅
- [x] **N1.grid** `SpatialGrid.cs` (uniform XZ hash, cell 8); FindNearestEnemy/StepHeal/splash grid-komşuluğu. Stres 326 birim → ~48× az iş.
- [x] **N1.pool** Mermi/ok/popup `UnityEngine.Pool`; per-shot alloc ~0.
- [x] **N1.mat** Paylaşılan material cache `(Color,metallic,smoothness)` + instancing; `ClearMatCache` restart'ta.
- [x] **N1.hpbar** `WorldHpBar.cs` world-space billboard; OnGUI/IMGUI kalktı; 0 IMGUI draw call.
- [x] **N1.budget** 300-birim stres profili; grid ~48× az proximity-iş.
- [x] **N2.asmdef** `AgeOfArena`/`AgeOfArenaEditor`/`AgeOfArenaTests` asmdef + `SelfTests.cs` (10 test).
- [x] **N2.resolver** Saf `CombatMath` (NetDamage+ArmorFor); `TakeDamage` buna yönlendirildi; SelfTest pin.
- [x] **N2.mapgen** `MapGenerator.cs` pure static; `MapType`+`Archetype` struct.
- [x] **N3.prng** `SimRandom.cs` (Xorshift32, seeded); 6 sim-Random sahası ayrıldı, kozmetik ayrı.
- [x] **N3.fixedstep** `FixedStepEnabled`+30Hz accumulator (maks 3 adım/frame); `SimTick(dt)` ayrıldı.
- [x] **N3.cmdlog** `CommandRecorder.cs` — `CommandType`+`GameCommand`; Move/Attack/Gather/Train/Research kayıt.
- [x] **N9.pause** Pause-on-blur (`FocusPause.cs`); odak dönünce hız geri yüklenir.
- [x] **N9.hotkeys** Remap UI (9 aksiyon, çakışma tespiti, PlayerPrefs).
- [x] **N9.feedback** Rally çizgisi + attack-move ring + marker'lar. _(Shift-kuyruk waypoint → Açık İşler N9.queue.)_
- [x] **N9.i18n** `Loc.cs` (TR+EN, ~150 anahtar) + TR-glyph TMP font; HUD string'leri bağlandı.
- [x] **N9.a11y** Colorblind palet + şekil kodlama + UI-ölçek slider + caption toggle.
- [x] **N9.postgame** Maç-sonu özet tablosu (4 takım stat satırı).
- [x] **N7.music** Çağ-başına müzik + savaşta ducking; prosedürel fallback.
- [x] **N7.sfx** 14 SoundId, pitch-vary + round-robin; prosedürel fallback.
- [x] **N7.spatial** Master/SFX slider'ları (PlayerPrefs). _(3D spatial+ambient → Açık İşler N7.spatial3d.)_

### Wave 2 — İçerik + Combat + N-team ✅
- [x] **N4.registry** `UnitRegistry.cs` (37 UnitType row); `UnitEntity` 13 switch → lookup.
- [x] **N4.civgate** 10 civ'in hepsi Castle+Imperial unique-tech çifti (16 yeni CIVT, civ-gated, gerçek efektli).
- [x] **N4.palette** `TeamPalette.cs` tek-kaynak (8 AoE2 rengi); 6 duplike literal kalktı.
- [x] **N4.civ13** AoK-13 set (Celts/Chinese/Goths/Turks) → **14 oynanabilir civ**; her biri bonus+denied+UU+unique-tech.
- [x] **N4.uu** 4 yeni UU (Franks→Throwing Axeman, Byzantines→Cataphract, Vikings→Berserk, Saracens→Mameluke).
- [x] **N5.nteam** `MaxTeams=4`+`TeamCount`; tüm `[4]`/`<4` hardwire'lar değiştirildi. _(N+1 resize → Açık İşler N5.resize.)_
- [x] **N5.fow** Per-team FoW (`IsAllied(0,teamId)`); ally/spectator görüşü.
- [x] **N5.pop** Per-team `RecomputePop`; AI pop-cap'e uyar.
- [x] **N6.splash** Splash distance-falloff (`SplashFalloffAt`); friendly-fire+per-victim zırh (N0.4).
- [x] **N6.bonus** `BonusDamageVs` additive stack (her eşleşen armor-class kendi bonusu).
- [x] **N6.ballistics** Pre-Ballistics miss + accuracy; Ballistics sonrası lead; kiting çalışır.
- [x] **N6.elev** `ElevationMult` (`GetHeight` okur): yüksek ×1.25, alçak ×0.75.
- [x] **N6.form** Line/Box/Staggered/Wedge formasyon + Town Bell (H); F ile cycle.
- [x] **N8.siege** Trebuchet/Ram/Mangonel Kenney FBX + ölüm-yıkık variant. _(Runtime capture → Açık İşler N8.siege.v.)_
- [x] **N8.terrain** `GetHeight`+value-noise terrain mesh (28 ring×128 seg) + biome texture + su shader; NavMesh bake. _(Not: 2026-06-05'te NavMesh bake düz-disc'e taşındı — Açık İşler NAVMESH.)_
- [x] **N8.anim** Gather/build/carry swing trigger; prim attack swing.
- [x] **N14.aieco** AI gerçek bina+train-time üretim + per-team pop-cap + onarım/expansion + üs-hasar dönüşü.
- [x] **N14.modes** 5 yeni mod (Empire Wars/KotH/Sudden Death/Treaty/Turbo); `GameMode` 4→9.

### Wave 3 — SP-Derinlik ✅
- [x] **N10.rms** `MapGenerator.Get` → 5 arketip (Arena/Arabistan/KaraOrman/Adalar/Göçebe) seed-deterministik. _(Runtime capture → Açık İşler N10.rms.v.)_
- [x] **N10.minimap** Minimap biome doku render + elevation görüş bonusu.
- [x] **N11.trig** `TriggerSystem.cs` (7 condition + 9 effect + `TriggerData`); SaveSystem v4 serialize.
- [x] **N12.edit** `ScenarioEditor.cs` runtime overlay (palette+yerleştirme+sil+per-player); save/load; 'E' tuşu.
- [x] **N12.savefull** Save order/queue/veteranlık/garrison/rally/map-seed persist; tam restore.
- [x] **N13.tut** `TutorialSystem.cs` (7 adım) + ilk-oyun guard + coach mark.
- [x] **N13.aow** `ArtOfWarSystem.cs` (4 challenge, bronze/silver/gold trigger).
- [x] **N13.camp** `CampaignSystem.cs` (3 görev) + `CampaignScreen` + progress save.
- [x] **N13.meta** Civ-filtreli tech-tree viewer + 9 achievement + seeded günlük challenge.

### Wave 4 — Multiplayer ✅ (iskelet)
- [x] **N15.spike** Karar: custom lockstep (Photon Quantum yerine); grid A* pathfinding; WebSocket relay yolu.
- [x] **N15.checksum** `ChecksumSystem.cs` (FNV-32, her 30 frame) + replay snapshot + tick-by-tick compare.
- [x] **N16.fixed** `FixedPoint.cs` (`FP` Q16.16 + `FPVec2` + `FPMath`); IEEE-754 float yok.
- [x] **N16.path** `GridPathfinder.cs` (uniform grid A*, octile, integer cost); MP=GridPathfinder, SP=NavMesh.
- [x] **N16.lockstep** `LockstepSystem.cs` (INPUT_DELAY=2, per-player kuyruk, solo-mirror).
- [x] **N17.transport** `TransportLayer.cs` (Loopback + WebSocket-stub); lobby handshake. _(Gerçek WebSocket → Açık İşler N17.ws.)_
- [x] **N17.desync** `DesyncHandler.cs` (CheckTick → halt + state dump).
- [x] **N17.replay** `ReplayViewer.cs` (Tick sayaç, hız ×0.5–4, PASS/FAIL).

---

## SP-Parite Özeti (tamamlandı)

> Eski `PARITY-PLAN` (85 madde, M1–M14) + ilk SP backlog'unun (P0–P3b) kapanış kaydı. Tüm maddeler
> ✔️ runtime-doğrulandı; ID'ler oturum commit'lerine bağlı (git geçmişi). Detay tek tek burada
> tutulmaz — post-parite N0–N17 bunların hepsini supersede etti (NAV hariç, yukarıda açık).

- **P0/P1 (temel oynanış):** Garnizon, zırh+counter matrisi, Monk/relic, kuleler, repair, Blacksmith/
  University tech, Imperial çağ + tier birimler, AI zorluk, control group, idle-worker, minimap komut,
  ses+SFX, Wonder/Score/Relic zaferi, Trade Cart, Save/Load, prosedürel harita — **hepsi ✔️**.
- **P2 (derinlik):** Ability altyapısı, veterancy, bina aurası, balistik/flank, research queue, AI counter+
  garnizon, QoL (patrol/hız), animasyon, market dalgası, kaynak çeşidi, tribute+decay, diplomasi/resign,
  Lockstep mimari kararı, civ veri yapısı+UU+seçim UI — **hepsi ✔️** (NAV ⬜ → Açık İşler).
- **P3 (AoE2 gap):** Skirmisher/Spearman/Scout upgrade zincirleri, Siege Workshop+Ram/Mangonel, Cavalry
  Archer, Camel, Bombard Tower — **✔️**; civ 5→14 (N4); CIVX 14→45 genişletme opsiyonel.
- **P3b (kod defektleri):** Veterancy +%10 atk, Byzantine/Frank civ bonusları, süvari bonus canlı güncelleme,
  retroaktif HP terfisi — **✔️**; AIRD round-half ⬜ → Açık İşler.
