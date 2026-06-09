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
| **N17.ws** | Gerçek WebSocket transport (NativeWebSocket + relay) | ⬜ | `TransportLayer.cs` — NativeWebSocket paket kurulumu gerekiyor |

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

> **Sonraki oturum önceliği:** Oynanış QA Dalga 2 — gerçek input ile uçtan-uca akış testi
> (villager→bina yerleştir→inşa; bina→train→kuyruk; attack-move/patrol/control-group;
> pause menü alt-ekranları). Sonra N17.ws (multiplayer).

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
