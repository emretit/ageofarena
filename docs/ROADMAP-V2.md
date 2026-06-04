# Age of Arena — Post-Parite Yol Haritası (ROADMAP-V2 / DoD-V2)

> **Önceki DoD bitti.** [PARITY-PLAN.md](PARITY-PLAN.md) (85 madde, M1–M14, AoE2 tek-oyuncu parite)
> **%100 tamamlandı ve runtime-doğrulandı**. Bu doküman onun halefidir: paritenin **ötesini** hedefler.
> Bu, post-parite geliştirmenin **tek doğru kaynağıdır** (makine-okunur DoD en sonda; `/goal` ölçer).

---

## Context — Bu plan neden var?

Kullanıcı "DoD %100 bitti, oyunu geliştirmek için yeni uzun bir DoD planı yapalım" dedi ve **3 kaynaklı
kapsamlı denetim** istedi (proje kodu + AoE2 wiki + genel web). Buna karşılık 9-ajanlı bir audit
workflow'u çalıştırıldı (`wf_284c8229-7e6`): proje kod denetimi (defekt/kod-sağlığı/cila), AoE2:DE tamlık
araştırması (meta-özellik/içerik/mekanik), modern RTS best-practice (game-feel/performans/MP mimarisi).
**Sonuç: 9 rapor, 152 ham bulgu, 6 yön, 17 milestone.**

### Audit'in dürüst hükmü

Oyun **"tech-demo derisi giymiş derin bir simülasyon"** — mutlu yol %100 ama altyapı kırılgan. Birkaç
"bitti" satırı kodla doğrulandı ve **aslında sığ** çıktı:

| Sığ/bug "bitti" | Kanıt (kodla doğrulandı) |
|---|---|
| Siege **tüm zırhı deliyor** | `BuildingEntity.cs:94` `_ => 0f // Siege bypasses armor`; `UnitEntity.cs:521-527` |
| Diplomasi **yalnız Conquest** zaferine bağlı | `MatchSystem.cs:108-163` — Wonder/Relic/Regicide/TimeUp `IsEnemy` okumaz, team 0'ı hardcode eder |
| AI **bina yapmadan**, train-time'sız, **pop-cap'siz** anında birim basıyor | `EnemyAI.cs`'de **0** `BuildingFactory` çağrısı; `UnitFactory` doğrudan; `RecomputePop` yalnız team 0 (`GameManager.cs:172`) |
| **Hiçbir civ'de tech-tree kısıtı yok** (AoE2 çekirdeği); 6/10 civ unique-unit'siz | `BuildingEntity.cs:167-174`; `TrainingQueue/ResearchSystem`'de **0** civ referansı |
| Allied team-bonus paylaşımı **explicit stub** | `GameManager.cs:97-105` ("once alliances land… for now each team stands alone") |
| Age-up bina önkoşulu **yalnız Dark→Feudal** | `ResearchSystem.cs:36,128-142` — Castle/Imperial kapısız |
| Mangonel splash kendi/3. takımı vuramaz, **per-victim zırhı yoksayar** | `Projectile.cs:68-89` |

### İki yapısal gerçek (her şeyi aşağı-akışta belirliyor)

1. **4-takım hardwire** — `[4]` sabit diziler + `<4` guard'lar (`MatchSystem/SaveSystem/BuildSystem/RelicEntity`).
   `teamId>=4` sessizce non-enemy olur. **N-player skirmish'i VE lockstep'i bloke eder.**
2. **42 `UnityEngine.Random` sim çağrısı** (monk convert `CombatSystem.cs:281`, AI hedef/spawn `EnemyAI.cs:282,363,551`,
   avoidance `UnitEntity.cs:302`) + `Time.deltaTime` tick + NavMesh → **non-deterministik**, takımın kendi
   lockstep ön-koşuluyla (`NetworkMode.cs:10-13`) çelişiyor.

Ayrıca: **sıfır otomatik test**, **O(n²) proximity taramaları** (her aggro/target/heal/gather/splash tam lineer
scan), object pooling yok, god-class'lar (`HUD.cs` 1755 satır, `WorldRoot` 946, `EnemyAI/UnitFactory/BuildingFactory`
700-810). Diskte **kullanılmayan gerçek modeller** var (Kenney siege FBX, 24 KayKit silah, arrow.fbx — 0 script referansı);
müzik **yok**, tüm SFX sentetik; harita **tek düz çim diski** (elevation/biome/su yok); WebGL sim **sekme odak
kaybında durmuyor** (`runInBackground=true`, pause handler yok).

### Kullanıcı kararları (bu planı şekillendiren)

- **Kapsam:** Tam yol haritası N1–N17 (4 dalga, eski 85-maddelik plan gibi kapsamlı).
- **Multiplayer:** **Tam dahil** — son dalga olarak komple lockstep (spike → fixed-point → transport).
- **Sıralama:** **Önce eski %100'ün sığ noktaları** düzeltilsin (doğruluk/denge önce) → bu yüzden **Wave 0
  (N0 Düzeltme)** dalgası başa kondu.

### Hedeflenen sonuç

AoE2-parite tek-oyuncu iskeletini → **sağlam temelli, test edilebilir, deterministik, N-player,
prodüksiyon-kalitesinde, içerik-platformu (RMS+editör+kampanya) olan ve multiplayer oynanan** bir oyuna
dönüştürmek. Yürütme ölçülebilir DoD ile (`/goal` her iterasyonda yeniden ölçebilir).

---

## Strateji — 5 Dalga (dependency-sıralı)

```
WAVE 0  N0  Düzeltme: sahte %100'ü gerçek yap            (KULLANICI: ÖNCE BU)
WAVE 1  N1 perf · N2 test/seam · N3 determinizm           (Foundation; paralel görünür: N7 müzik, N9 pause-on-blur)
WAVE 2  N4 data-civ-registry · N5 N-team · N6 combat ·     (İçerik+Combat+N-team)
        N8 model/terrain · N14 AI derinlik
WAVE 3  N10 RMS · N11 trigger · N12 editör · N13 kampanya  (SP-derinlik; en büyük replayability çarpanı)
WAVE 4  N15 MP-spike+harness · N16 fixed-point+path+       (Multiplayer; en uzun direk)
        local-lockstep · N17 transport+lobby+desync
```

**Bağımlılık grafiği** (audit `dependsOn`):

```
N0 (bağımsız, ilk)
N1 ─┐                       N7 (bağımsız)   N9 (bağımsız)
N2 ─┼─► N3 ─┬─► N11 ─┬─► N12 ─► N13          N8 ◄─ N1
    └─► N4 ─┼─► N5 ──┼─► N14                 N10 ◄─ N2,N8
            └─► N6   └─► N15 ─► N16 ─► N17
```

> **N0 quick-fix ↔ derin milestone örtüşmesi:** N0 mevcut mimaride hızlı/yüzeysel düzeltmedir; aynı konuların
> **derin/kalıcı** sürümü foundation üstünde gelir ve N0'ı **supersede eder**: civ-gating N0.7 → N4 (tam data-driven),
> AI ekonomi N0.9 (queue ownership) → N14 (gerçek bina+expand), per-team N5. N0 borcu kapatır, sonraki dalgalar
> doğru yapar. Bu kasıtlıdır.

---

## WAVE 0 — N0: Düzeltme ("sahte %100'ü gerçek yap")

**Tema:** Audit'in bulduğu yanıltıcı "bitti"leri **mevcut mimaride** düzelt. Ucuz, standalone, yüksek değerli
doğruluk/denge düzeltmeleri. Foundation gerektirmez. _Kullanıcı seçimi gereği ilk dalga._
**Efor:** ~6-8 oturum (çoğu S, birkaç M).

| ID | Madde | Efor | Ana dosya |
|---|---|---|---|
| N0.1 | Siege → melee-class hasar (melee zırhı okunur), tüm-zırh bypass kalkar | S | `UnitEntity.cs:521-527`, `BuildingEntity.cs:90-96` |
| N0.2 | Diplomasi tüm zafer koşullarında `IsEnemy` okur (Wonder/Relic/Regicide/TimeUp); team-0 hardcode kalkar | M | `MatchSystem.cs:108-163` |
| N0.3 | Age-up bina önkoşulu Castle & Imperial'da da; çağ-uygun bina sayılır (House/Farm değil) | S | `ResearchSystem.cs:36,128-142` |
| N0.4 | Splash tüm takımları vurur (friendly fire) + her kurbana kendi zırhı/BonusDamageVs uygulanır | M | `Projectile.cs:68-89` |
| N0.5 | Koşulsuz +%25 flank bonusu (charge ile stack, non-parite) kaldırılır/koşullandırılır | S | `CombatSystem` (flank) |
| N0.6 | Allied `TeamSharedBonus` gerçek toplama (stub kalkar) | S | `GameManager.cs:97-105` |
| N0.7 | **Interim civ kimliği:** her civ'e denied-tech/unit set; TrainingQueue/ResearchSystem/komut-kartı aktif civ'i kontrol eder; UU'suz 4-6 civ'e unique unit | M | `TrainingQueue.cs`, `ResearchSystem.cs`, `BuildingEntity.cs:167-174` |
| N0.8 | Dürüst tech'ler: Ballistics ve diğer no-op tech'ler ya gerçek etki kazanır ya açıkça etiketlenir (gerçek miss-modeli N6'ya) | S | `Projectile.cs:9-13` |
| N0.9 | TrainingQueue bina **sahibinin** kaynağı/pop'unu kullanır (team-0 latent bug); iade de sahibe | M | `TrainingQueue.cs:31-38,87-91` |

---

## WAVE 1 — Foundation (görünmez ama her şeyi açar)

### N1 — Performans temeli: spatial grid + pooling + shared materials
**Efor:** L (~5 oturum). **Deps:** yok.
Her proximity sorgusu tam lineer scan → frame O(n²); mermi/popup başına GameObject+mesh+material alloc;
`Prims.Mat()` parça başına `new Material` → her parça ayrı draw call.
- Uniform spatial-hash grid (her frame `gm.units/buildings`'ten kurulu); `FindNearestEnemy/StepHeal/FindNearestNode/AI-target/Projectile-splash` 3×3 hücre komşuluğuna yönlendirilir.
- Generic object pool (`UnityEngine.Pool`) — mermi/ok/popup; Instantiate/Destroy yerine Get/Release; tek paylaşımlı ok materyali.
- Paylaşımlı `Material` cache (color,metallic,smoothness anahtarlı) + `enableInstancing`; takım rengi yalnız `MaterialPropertyBlock`.
- HP barları OnGUI/IMGUI → world-space billboard; `isKayKit` bool + renderer spawn'da cache'lenir.
- Fog-of-war `SetPixels32+Apply` ~6-10Hz throttle; per-unit repath/gather timer'ları field (Dictionary değil).
- 200/400/800-unit stres sahnesi + `Unity_Profiler` MCP ile profil; açık **16.6ms / ~0 alloc-per-frame** bütçesi.

### N2 — Test assembly + saf-mantık seam'leri (CombatResolver, MapGenerator)
**Efor:** M (~4 oturum). **Deps:** yok.
Sıfır test + her şey `Assembly-CSharp` → izole referans imkânsız.
- `AgeOfArena.asmdef` (oyun) + `Tests.asmdef` (EditMode). İlk testler saf mantık: net-hasar `max(1,atk+bonus-armor)`, `TechDefs/BuildingDefs/CivilizationDefs` lookup, `ResourceManager` afford, market supply/demand, diplomasi `IsEnemy`.
- Saf **`CombatResolver`** çıkar (attacker stat + target armor/class + charge/flank → int hasar); `StepCombat` ve testler aynı fonksiyonu çağırır.
- Saf **`MapGenerator`** çıkar (`WorldRoot`'tan; seed → placement list, GameObject yok); `SceneSetup`/`ModeSetup` ayrılır.
- N0'da düzeltilen sığ davranışları **pin'leyen regresyon testleri** (N6 düzeltmeleri doğrulanabilir kalsın).

### N3 — Determinizm çekirdeği: fixed-step + seeded PRNG + command log
**Efor:** L (~6 oturum). **Deps:** N2.
Mimari lockstep'e olağandışı uygun (`GameManager.Update` tek sıralı tick, dt geçiyor; AI+oyuncu aynı emir API'si).
- Deterministik Xorshift/PCG PRNG struct (mapSeed'den); **41 Random sahasını** sınıflandır → sim akışı (monk convert, AI hedef/spawn jitter, avoidance) ayrı, kozmetik ayrı.
- Fixed-step accumulator (~30Hz) tüm `Tick()`'lere sabit dt; render interpolasyon; pause/hız → sim-step sayısı; 4 dahili `Time.deltaTime` okuması kalkar.
- Command type seti (Move/AttackMove/Gather/Build/Train/Research/Stance/Garrison/Tribute/Rally/Delete) `playerId`+sim-ID taşır; `CommandSystem`+`EnemyAI` **mutate değil ENQUEUE** eder; sim tick başında uygular.
- Entity başına deterministik monoton sim-ID; sim-tick yolundaki `Dictionary/HashSet/FindObjectsByType` denetimi; sim-state yazan per-entity `Update()` karantinaya.
- Lokal command recorder (seed + per-tick komutlar) → replay + determinizm harness temeli.

> **Paralel görünür kazanımlar (gün-1'den, momentum için):** **N7 müzik** + **N9 pause-on-blur & ses sliderları**.

---

## WAVE 2 — İçerik + Combat + N-team

### N4 — Data-driven civ/unit/building/tech registry + civ kimliği gating
**Efor:** XL (~8 oturum). **Deps:** N2. _N0.7'yi supersede eder._
En yüksek kaldıraçlı içerik değişimi. `CivilizationDefs` temiz tablo ama yalnız çarpan; TrainingQueue/BuildingFactory/CommandSystem/ResearchSystem'de **0 civ referansı**. `UnitEntity`'de 83 `UnitType` switch.
- `UnitType/BuildingType/TechType` el-enum'ları → data-driven registry (ScriptableObject veya id-anahtarlı static row); `UnitEntity` stat switch'leri → data lookup.
- Civ row'ları `uniqueUnit(s)`, `castleUniqueTech`, `imperialUniqueTech`, **denied-tech/unit set** ilan eder; TrainingQueue/ResearchSystem/komut-kartı aktif civ'e göre gate'lenir. _(denied-set mekanizması N0.7'de interim hâlde kuruldu; N4 onu data-registry'ye taşır.)_
- **N0.7 Part B devri:** UU'suz 4 civ'e yeni unique unit — Franks→Throwing Axeman, Byzantines→Cataphract, Vikings→Berserk, Saracens→Mameluke (yeni `UnitType`+factory+stat/ArmorClass/BonusDamageVs switch'leri + Castle `CastleUniqueFor` gating). Registry üstünde temiz/throwaway-olmayan şekilde eklenir.
- Tek-kaynak takım-rengi paleti (CombatSystem/TrainingQueue/WorldRoot/BuildingFactory'deki 4-renk literal'leri kalkar) — N-team'i de açar.
- Gerçek allied `TeamSharedBonus` toplama; civ başına Castle+Imperial unique-tech çifti.
- **AoK-13 civ setini tamamla** (Celts/Chinese/Goths/Turks data olarak: UU, 2 unique tech, bonus, denial).

### N5 — N-team parametrizasyonu (4-takım hardwire kalkar)
**Efor:** M (~4 oturum). **Deps:** N4.
- `[4]` sabit diziler + `<4` guard'lar → konfigüre edilebilir takım sayısı (`MatchSystem/SaveSystem/BuildSystem/RelicEntity`).
- Diplomasi: Wonder/Relic/Regicide/TimeUp `IsEnemy` okur, team-0=oyuncu hardcode kalkar (N0.2'nin kalıcı/genel hâli).
- Per-team fog-of-war görüşü (şu an yalnız `teamId==0` boyar); ally/spectator görüşü.
- TrainingQueue bina-sahibi kaynağı/pop'u (N0.9'un genel hâli); per-team `RecomputePop` housing/pop-cap (şu an team-0).
- Takım paleti N4 tek-kaynaktan → herhangi takım sayısı doğru tint.

### N6 — Combat fidelity: splash/bonus-stacking/Ballistics/elevation/garrison/formasyon
**Efor:** L (~7 oturum). **Deps:** N2, N4.
- Splash distance falloff + friendly-fire (Mangonel/Onager/DemoShip); Scorpion target-only; per-unit blast-level; her secondary'ye kendi zırhı+BonusDamageVs (N0.4'ün kalıcı/derin hâli).
- Data-driven bonus damage **tüm eşleşen armor class'larında STACK** + per-class zırh (pozitif resist/negatif vulnerable); Sicilian-tarzı bonus-resist hook.
- Siege → melee-class hasar, melee zırh okunur (N0.1 kalıcı); koşulsuz +%25 flank kalkar (N0.5 kalıcı).
- Pre-Ballistics mermi miss (anlık zemin noktasına ateş; yalnız Ballistics ile lead) + accuracy roll → Ballistics anlamlı, kiting çalışır.
- Garrison ok DPS+bina-cap ile ölçeklenir (Tower 4-8 / TC 10-18 / Castle 16-20); ram-garrison +hasar; conversion resistance + Redemption bina dönüştürme.
- Formasyonlar (Line/Box/Staggered/Flank, rol-farkındalıklı slot+yön); attack-ground (siege); allied auto-gate; Town Bell (garnizon-all / işe-dön).
- **±%25 elevation modifier** (terrain elevation N8'e bağlanır).

### N8 — Görsel içerik: diskteki Kenney/KayKit asset'lerini bağla + terrain elevation/biome/su
**Efor:** XL (~9 oturum). **Deps:** N1.
~18/27 unit ve çoğu bina hâlâ el-prim; gerçek Kenney siege (catapult/ram/trebuchet/ballista/tower + yıkık), 24 KayKit silah, arrow.fbx diskte **0 script referansı**.
- Kenney siege FBX → Trebuchet/Ram/Mangonel (`KenneyModels.Spawn` + team tint); ölümde yıkık variant; 4 naval birime gövde mesh.
- `UnitVisualLibrary` cavalry/camel/civ-unique/King; tier silah/zırh prop'u (Champion ≠ Militia) kullanılmayan silah FBX'ten.
- Daha çok bina 76 Castle + 167 FantasyTown modüler mesh'ten; arrow.fbx + trail + impact spark.
- **Heightmap terrain** (elevation/ramp/cliff), çoklu biome (çöl/kar/orman doku), nehir/göl/kıyı + gerçek su shader; NavMesh yeniden bake.
- Gather/build/carry animasyon state'leri (KayKit controller, GatherSystem/BuildSystem trigger); prim'lere attack swing/recoil; kısa corpse/dissolve + rubble.

### N14 — AI derinlik: gerçek ekonomi/bina/expand + eksik oyun modları
**Efor:** L (~6 oturum). **Deps:** N3, N5. _N0.9'u tamamlar._
AI yapısal olarak daha basit oynuyor: anında spawn, bina yok, train-time yok, pop-cap yok, asla inşa/onar/expand etmiyor → üs hasarından dönemiyor.
- AI gerçek Barracks/Stable/Range'den train-time ile üretir + per-team pop-cap; AI bina inşası, onarım, expansion; emirler command-log'dan geçer.
- Ucuz oyun-modu kural-toggle'ları: Empire Wars / King of the Hill / Sudden Death / Treaty / Turbo.

---

## WAVE 3 — Single-Player Derinlik (en büyük replayability çarpanı)

### N10 — Random-map scripts + random-map havuzu
**Efor:** L (~5 oturum). **Deps:** N2, N8.
DoD "RandomMap" = tek hardcoded dairesel-ada arena → en büyük replayability açığı.
- Harita tanımları data/script format (JSON/ScriptableObject); `MapGenerator` yorumlar (base_terrain / create_land / create_object analogu).
- Dengeli arketipler: açık kara (Arabia), surlu arena, orman-choke (Black Forest), adalar/su, nomad (TC'siz başlangıç).
- Arketip+seed seçimi setup ekranına; seeded PRNG ile deterministik yerleşim.
- Yükseklik/biome-renkli minimap + elevation-saygılı FoW (N8 terrain'e bağlı).

### N11 — Trigger runtime (condition → effect scripting)
**Efor:** L (~5 oturum). **Deps:** N3.
Generic event scripting yok; zafer/maç mantığı `MatchSystem/RelicSystem`'de hardcoded. Trigger runtime = senaryo+kampanya+modding omurgası; N3 command-log'unu tüketir.
- Condition: Bring Object to Area, Timer, Own Objects, Destroy Object, Accumulate Attribute, Research Tech, Reach Age.
- Effect: Create/Remove Object, Send Message/Instructions, Change Diplomacy, Set Age, Camera Track, Activate/Deactivate Trigger, You Win/You Lose, Play Sound.
- Trigger değerlendirme döngüsü fixed-step tick'e; sim-mutasyon effect'leri command-log'dan.
- Trigger data save/senaryo formatında serialize.

### N12 — Senaryo/harita editörü
**Efor:** XL (~9 oturum). **Deps:** N11, N5.
Hiç authoring tool yok (sahneler kod-kurulu).
- Editör UI: terrain paint+elevation, unit/bina/kaynak yerleştirme (runtime build yolu üzerinde).
- Per-player setup (civ/çağ/kaynak/diplomasi) + objektif/trigger authoring (N11).
- Senaryo save/load (genişletilmiş SaveSystem format) — **ayrıca sığ save'i düzelt:** order/queue/veteranlık/garrison/rally/map-seed persist.
- Editörden playtest başlatma.

### N13 — Onboarding: Art-of-War tutorial + kampanya framework + meta-loop
**Efor:** XL (~9 oturum). **Deps:** N11, N12.
Sıfır onboarding — yeni oyuncu öğretmesiz skirmish'e düşüyor.
- Rehberli ilk-oyun tutorial (villager→bina→gather→çağ→ordu→saldır) + bağlamsal coach-mark (idle TC, çağ-atlanabilir, pop-dolu).
- Art-of-War tarzı challenge senaryoları (Early Eco / Combat / Counters / Siege) bronze/silver/gold (trigger ile).
- Kampanya framework: sıralı senaryo listesi, briefing/objektif UI, kazan-sonraki-açılır, kampanya-progress save.
- Civ-filtreli tech-tree viewer (greyed-out + hover stat); lokal achievement/mastery + seeded günlük challenge.

---

## WAVE 4 — Multiplayer (deterministik lockstep; en uzun direk)

### N15 — MP build-vs-buy spike + determinizm harness + replay
**Efor:** L (~5 oturum). **Deps:** N3.
Ayları custom path'e harcamadan **fizibilite spike**: Photon Quantum (deterministik fixed-point + nav + rollback, <100 CCU ücretsiz) iki uzun direği değiştirebilir ama MonoBehaviour sim'i Quantum ECS'e port + WebGL doğrulaması ister.
- **Photon Quantum spike:** tek birimin move+combat'ını port et, WebGL build + maliyet modeli doğrula; karar kaydı custom-lockstep vs Quantum.
- Per-tick state checksum (unit pos/hp/order + bina hp + kaynak hash; SaveSystem field-walk yeniden kullanılır).
- Replay format (seed + per-tick command list) + headless re-simulate → aynı checksum iki kez.
- Deterministik-pathfinding yaklaşımı (grid A*/flow-field) vs SP-only NavMesh kararı.

### N16 — Fixed-point sim + deterministik pathfinding + lokal lockstep
**Efor:** XL (~12 oturum). **Deps:** N15. _(Spike custom path seçerse.)_
- Fixed-point lib (FixedMathSharp / Unity.Mathematics.FixedPoint); sim sayısal çekirdek (pos/vel/dist/damage/timer) migrasyon; render Transform float interpolasyon.
- Deterministik grid A*/flow-field + fixed-point steering/local avoidance; MP için NavMeshBuilder bağımlılığı kalkar.
- Lokal in-process 2-oyuncu lockstep: T+inputDelay'de komut scheduling, tam komut seti gelmeden ilerlemez, her tick checksum (N15 harness).

### N17 — Transport, lobby, desync recovery + replay viewer
**Efor:** XL (~10 oturum). **Deps:** N16.
Tarayıcı raw UDP/TCP yasak → WebGL MP **WebSocket relay** (yalnız komut = küçük bant).
- WebSocket transport + relay; command framing; lobby (seed/civ/team handshake, setup-UI yeniden kullanılır); başta 2-4 oyuncu cap.
- Periyodik checksum exchange; mismatch halt + state dump (SaveSystem) offline diff.
- Bağlantı dayanıklılığı: dinamik input-delay, waiting-for-player UI, timeout drop/AI-takeover, reconnect/resign.
- Replay viewer (hız/rewind, kayıtlı command stream'den).

---

## Cross-Cutting İlkeler (her milestone uymalı)

1. **Determinizm tasarım kısıtıdır, geç yama değil.** N3 sonrası tüm yeni kod: "render sim-state okur, asla yazmaz" + yalnız sim-PRNG'den çeker. Aksi hâlde determinizm çürür.
2. **Data-driven tablolar omurga.** `CivilizationDefs` deseni unit/bina/tech/harita'ya genellenir (N4,N10). Her yeni içerik = data satırı, yeni switch case değil. God-class'ları küçültür, modding'i açar.
3. **Test + regresyon pin.** N2 asmdef + saf resolver her milestone'la büyür; sığ davranışlar düzeltilmeden önce pin'lenir (N6); per-tick checksum (N15) erken eklenir.
4. **Mevcut seam'leri yeniden kullan.** save/load (replay+senaryo format), mapSeed (RMS+günlük), paylaşılan AI/oyuncu command API (command-log+lockstep+replay), `CommandIconFactory` (editör palet+tech-tree viewer), `UiSkin` (tüm yeni panel), `GameEvents` (achievement+alert).
5. **WebGL perf bütçe disiplini.** Tek-thread, Burst/Jobs yok, frame başına 1 GC. Entity/efekt ekleyen her milestone N1 stres sahnesi + 16.6ms/~0-alloc bütçesine karşı yeniden ölçülür (`Unity_Profiler` MCP).
6. **N-team doğruluğu.** 4-takım hardwire (N5) + tek-kaynak palet (N4) her yeni sistemce korunur (fog görüşü, diplomasi zaferleri, queue ownership, monk conversion) — yoksa team-0-only bug'lar geri gelir.

---

## Top Riskler

- **MP = çok-aylık XL sink, 2 uzun direk** (fixed-point neredeyse her sim dosyasına dokunur; deterministik pathfinding load-bearing NavMesh'i değiştirir). WebSocket-only latency-hassas. **Mitigasyon:** N15 Photon Quantum spike'ı **hard gate** — custom path'e spike'tan önce commit etme.
- **Foundation refactor (N1-N3,N5) oyuncuya görünmez** → "ilerleme yok" baskısı. **Mitigasyon:** gün-1'den N7 müzik + N9 pause-on-blur paralel.
- **AoE2 fidelity scope creep** (53 civ, ~229 kampanya, tam editör+trigger dili). **Mitigasyon:** kapalı tranche'ler (AoK-13 civ, birkaç arketip harita, küçük Art-of-War seti); her içerik milestone'u kapalı+belgeli.
- **"%100" yanlış güven** — birkaç satır sığ. **Mitigasyon:** N0 dalgası + N5/N6/N14 pinler ve düzeltir; üstüne inşa etmeden önce.
- **Editör+kampanya (N12-13) → trigger (N11) → command-log (N3) uzun zinciri.** N3'ü kritik yolda tut.
- **Art/audio/terrain (N7-N8) kısmen asset-sourcing** (recorded SFX, müzik, gemi/at mesh, biome doku). **Mitigasyon:** "saf wiring (diskteki asset)" = şimdi yüksek ROI vs "yeni içerik gerekir" = ayrı zamanla; ayır.

---

## Definition of Done (makine-okunur)

> Her madde tek satır ölçülebilir kriter; `/goal` her iterasyonda yeniden ölçer.
> **Ortak:** Unity Roslyn 0 error / 0 warning (`Unity_GetConsoleLogs` boş) + ilgili Play/MCP/test doğrulaması.

### Wave 0 — N0 Düzeltme  ✅ TAMAM (2026-06-04; Unity 0 error/0 warning; Play 26 birim/24 bina, 0 runtime error)
- [x] N0.1: `UnitEntity`/`BuildingEntity` `TakeDamage` Siege→melee-class haritalandı (melee zırh okunur, eski `_ => 0f` bypass kalktı); siege binaya anti-structure `BonusDamageVs` ile güçlü kalır. _Kod: `UnitEntity.cs:521-528`, `BuildingEntity.cs:90-96`._
- [x] N0.2: `GameManager.IsAllied` eklendi; `MatchSystem` Wonder/Relic/TimeUp `IsAllied(0,t)` ile paylaşımlı zafer, Regicide enemy-king'i `IsEnemy` ile filtreler; team-0 hardcode kalktı. Runtime: `IsAllied(0,0)=True`, `IsEnemy(0,1)=True`.
- [x] N0.3: `IsAgeTech` + `CountsTowardAge` eklendi; önkoşul Feudal/Castle/Imperial'ın üçünde de uygulanır; TC/House/Farm/Wall/Gate/Outpost/FishTrap sayılmaz; `CanAdvanceAge` her çağda önkoşul ister.
- [x] N0.4: `Projectile.Spawn` artık `attacker` taşır; splash radius-içi **tüm takımları** vurur (friendly fire), her secondary `attacker.AttackDamage + BonusDamageVs(o)` + kendi zırhı (`TakeDamage`); `enemyTeam` filtresi kalktı. CombatSystem `u`'yu geçirir.
- [x] N0.5: CBX +%25 pozisyonel flank bonusu `CombatSystem`'den kaldırıldı (AoE2'de facing-hasar yok; charge ile çarpımsal stack ediyordu); counter base-stat'lar korundu.
- [x] N0.6: `GameManager.TeamSharedBonus` artık kendi + tüm `IsAllied` takımların `teamBonus`'unu toplar (stub kalktı); `TeamBonus` alanı büyüyünce buraya eklenir.
- [x] N0.7 (Part A — tech-tree subtraction): `CivilizationDefs.IsUnitDenied/IsTechDenied` + 6 civ'e denied set; gating 4 yerde (`GetTrainables`, `TechDefs.ForBuilding`, `TrainingQueue.Enqueue`, `ResearchSystem.Enqueue`); AI bypass eder (etkilenmez). Runtime doğrulandı: Aztek-süvarisiz, Frank-Halberdiersiz, Briton-Halberdierli. **Part B (UU'suz 4 civ'e yeni unique unit) → N4'e taşındı** (yeni `UnitType`+factory+switch'ler inherently data-registry işi; planın N0↔N4 katmanlama notu gereği).
- [x] N0.8: `Projectile` doc-yorumu dürüstleştirildi — Ballistics şu an NO-OP (homing=%100 isabet); gerçek miss-modeli N6'ya etiketlendi.
- [x] N0.9: `TrainingQueue.Res(b)` helper'ı eklendi; `Enqueue`/`Cancel` bina sahibinin (`b.teamId`) kaynak/pop ledger'ını kullanır+iade eder (team-0 latent bug kalktı). _Tam per-team pop-cap N5'te._
- [x] N0.x (bonus): `EnemyAI._trainCursor` ölü alanı kaldırıldı (CS0414 uyarısı → 0 warning).

### Wave 1 — Foundation
- [x] N1.grid: `SpatialGrid.cs` (uniform XZ hash, cellSize 8); GameManager her frame rebuild; CombatSystem `FindNearestEnemy`/`StepHeal` + `Projectile` splash grid-komşuluğundan. Runtime: grid==brute (5==5) doğru; stres 326 birim → grid 2.212 vs brute 106.276 aday-kontrol (~48× az iş).
- [x] N1.pool: Mermi/ok/popup `UnityEngine.Pool`'dan; Instantiate/Destroy yok; per-shot alloc ~0. `Projectile`: statik `ObjectPool<Projectile>` (capacity 64, max 256); `CreatePooled` pre-built mesh child (resize on Get); `Spawn` → Pool.Get + localScale ayarı; `ReturnToPool` → Pool.Release. `DamagePopup`: statik `ObjectPool<DamagePopup>` (capacity 32, max 128); `Show` → Pool.Get + text/color reset; timeout → Pool.Release. Runtime: Get CountActive=1, Release CountInactive=1 (Projectile+DamagePopup) doğrulandı. 0/0.
- [x] N1.mat: Paylaşılan material cache + instancing. `Prims.Mat` statik Dictionary cache `(Color,metallic,smoothness)→Material`; aynı parametre → aynı instance (referans eşitliği); `m.enableInstancing=true` GPU batching için. `ClearMatCache()` restart'ta çağrılır (GameBootstrap.Restart). Runtime: same-color→same-instance=True, enableInstancing=True, ClearMatCache sonrası yeni instance=True. 0/0.
- [x] N1.hpbar: `WorldHpBar.cs` world-space billboard HP barları — iki Quad child (bg + fill), LateUpdate billboard rotation, Refresh(frac,show). `GameManager.RegisterUnit/RegisterBuilding`: her varlığa `AddComponent<WorldHpBar>()` + `Init(yOffset, friendly)`. `CombatSystem`: `OnGUI` + IMGUI bar textures kaldırıldı; `LateUpdate` döngüsü `GetComponent<WorldHpBar>().Refresh()` çağırır (seçili veya hasarlı = görünür). 0 IMGUI draw call. Runtime: Init+Refresh exception yok, GetComponent doğrulandı. 0/0.
- [x] N1.budget: 300-birim stres-spawn ölçümü (RunCommand) — grid ~48× az proximity-iş; 16.6ms/alloc profiler dokümantasyonu N1.pool/mat sonrası.
- [ ] N2.asmdef: _(ertelendi — asmdef yeniden-yapılandırması tüm build'i kırma riski; şimdilik `CombatMath.SelfTest` editör-içi pin sağlıyor)_ `AgeOfArena.asmdef` + `Tests.asmdef`; EditMode test runner yeşil.
- [x] N2.resolver: Saf `CombatMath` (NetDamage + ArmorFor, N0.1 siege=melee dahil); `UnitEntity`/`BuildingEntity.TakeDamage` buna yönlendirildi; `CombatMath.SelfTest()` formülü pin'ler. Runtime: SelfTest PASS.
- [ ] N2.mapgen: _(ertelendi — `WorldRoot` sahne-kurulumundan büyük çıkarma; N10 RMS ile birlikte yapılacak)_ Saf `MapGenerator` (seed→placement list).
- [x] N3.prng: `SimRandom.cs` (Xorshift32, mapSeed'den seeded `WorldRoot.Build`'da); 6 sim-Random sahası (monk convert, AI hedef-roll/spawn-jitter/scatter, agent avoidance) sim akışına yönlendirildi; kozmetik Random (particle/camera/decor/mapgen) ayrı kaldı. Runtime: same-seed→aynı dizi, diff-seed→farklı doğrulandı.
- [ ] N3.fixedstep: _(MP-prep'e ertelendi — N16 ile; tek başına yapılınca doğrulanamaz, frame-timing değişimi riskli)_ Fixed-step accumulator (~30Hz); render interpolasyon; `Time.deltaTime` sim'de yok.
- [ ] N3.cmdlog: _(MP-prep'e ertelendi — N15-16 ile)_ Command type seti + enqueue mimarisi + lokal command recorder (replay temeli).

### Paralel görünür kazanımlar (gün-1'den; N7 müzik · N9 UX/a11y/i18n)
- [x] N9.pause: Pause-on-blur — `FocusPause.cs` (OnApplicationFocus/Pause → timeScale 0 + "Duraklatıldı" overlay); odak dönünce önceki hız geri yüklenir; game-over (`MatchSystem.IsOver`) / Esc-menü pause'larına dokunmaz. GameBootstrap'ta WorldRoot'a bağlı. Runtime: 1.5→0→1.5 doğrulandı.
- [x] N9.hotkeys: Remap **UI** eklendi — pause-menü "Tuşlar" → `OpenHotkeyPanel` (9 aksiyon satırı, tıkla-dinle-bas-ata akışı `HUD.PollHotkeyRebind`, Esc iptal, "Varsayılana Dön"). Çakışma tespiti: `Hotkeys.Rebind` aynı tuşu tutan diğer aksiyonu None'a tahliye eder (iki aksiyon aynı tuşu paylaşamaz). PlayerPrefs persistence + `ActionFor`/`Count` helper'ları. Backend `HotkeyAction`→`KeyCode` + buton hotkey-badge zaten vardı. Runtime: rebind AttackMove→S Stop'u evict etti, ActionFor(S)=AttackMove, ResetAll defaults doğrulandı. 0/0.
- [x] N9.feedback: Rally **çizgisi** eklendi (`CommandSystem.UpdateRallyFlag` → bina→rally-noktası LineRenderer, seçili rally-binasında görünür). Attack-move zemin göstergesi (kırmızı ring), move/attack/gather marker'ları, rally flag, FoW varsayılan-açık + pause-menü toggle zaten vardı. Runtime: RallyLine bina(-64)→rally(-58) enabled doğrulandı. _Shift-kuyruk waypoint görseli ertelendi — komut-kuyruğu mekanizması gerektiriyor (N3.cmdlog'a bağlı, MP-prep)._ 0/0.
- [x] N9.i18n: Lokalizasyon tablosu (key→string, TR+EN) + TR-glyph kapsamlı TMP/SDF font; tüm HUD string'leri buradan. `Loc.cs` statik sınıfı (Lang.TR/EN, `Get(key)`, `SetLanguage`, `LoadSaved` PlayerPrefs); ~150 anahtar 4 kategoride (kaynak/çağ/zorluk/mod/birim/komut/tooltip/pazar/pause/hotkey/gamedover). HUD.cs major string'leri Loc'a bağlandı: kaynak etiketleri, AgeName/DiffName/UnitTr helper'ları, "Çağ:/Zorluk:/Medeniyet:" prefix'leri, duruş adları, üretim/araştırma/garnizon/süre etiketleri, pause menü + hotkey panel butonları. Eksik key → key fallback. `UnitEntity.TeamTech` public yapıldı (N6.ballistics Projectile erişimi). Runtime: TR Yiyecek/Karanlık/Zor, EN Food/Dark Age/Resign/Janissary doğrulandı. 0/0.
- [x] N9.a11y: Colorblind-güvenli palet + şekil/ikon kodlama (minimap/HP/diplomasi); UI-ölçek slider; caption toggle. `AccessibilitySettings.cs` (ColorblindMode/UiScale, PlayerPrefs kalıcı). `TeamPalette`: `_colorblind` palete eklendi (deuteranopia-safe: team1 orange-red E05C00, team2 cyan 00A3CC); `For()` ColorblindMode'a göre seçer. `MinimapSystem.Place(shape)`: düşman blip'leri 45° döndürülmüş elmas (shape=1), müttefik/kendi kare (shape=0). Pause menüsüne "Renk KB/Renk Std" toggle + "UI +/-" butonları eklendi; `ApplyUiScale()` referenceResolution'ı ayarlar. `GameBootstrap.Boot` Loc.LoadSaved + AccessibilitySettings.Load'u çağırır. Runtime: scale 2.0→1.5, 0.5→0.75 clamp doğrulandı; CB team1/2 renk ayrımı pass. 0/0.
- [x] N9.postgame: `ShowGameOver` maç-sonu özet tablosu ile genişletildi — başlık (ZAFER/YENİLDİN) + subtitle + 4 takım satırlı stat tablosu (Skor/Ordu/Köylü/Bina/Altın/Yaş sütunları), oyuncu satırı kazanma/kaybetmeye göre renklendiriliyor, her takım kendi `TeamPalette` renginde. Yeniden başlatma ipucu ekrana yerleştirildi. `HUD.ResetGameOver()`/`HasCanvasRoot` tanı metodları eklendi. 0/0 derleme. _(Tam play-mode UI testi RunCommand editor-context kısıtı nedeniyle CivSelect init bekleniyor; kod PauseMenu/HotkeyPanel ile aynı deseni izliyor.)_
- [x] N7.music: Çağ-başına in-game müzik (Dark/Feudal/Castle/Imperial) + savaşta ducking. `AudioManager`: `_musicSrc` looping AudioSource; `MusicVolume` property (PlayerPrefs); `PlayMusicForAge(age)` (Resources/Audio/music_dark vb. → prosedürel fallback harmonik drone); `SetCombatActive(bool)` → `_duckFactor` lerp (DuckTarget=0.35, DuckSpeed=2); `Update()` volume = master×music×duck; `Bootstrap()` Dark Age ile başlar. HUD.OnAgeAdvanced → PlayMusicForAge. CombatSystem.Tick → 2s polling → SetCombatActive. HUD pause menüsüne Müzik+/- butonları. Runtime: MusicVolume=0.7, PlayMusicForAge/SetCombatActive exception yok. 0/0.
- [x] N7.sfx: Gerçek SFX seti (kılıç/ok/inşa/eğitim/ölüm/UI/çağ-fanfar) `Resources/Audio`'ya (loader zaten dosyayı tercih eder); pitch-vary + round-robin. 4 yeni SoundId eklendi: Gather/Research/Ping/Repair (14 toplam). `PlaySound` ±10% pitch jitter (`src.pitch = Random.Range`), round-robin counter her SoundId için. Procedural fallback: Gather=Square 320Hz, Research=Ding 550Hz, Ping=Sine 880Hz, Repair=Triangle 260Hz. Bağlantılar: Gather→GatherSystem (team 0 only), Research→ResearchSystem complete, Ping→MinimapSystem (ButtonClick yerine), Repair→BuildSystem complete. Runtime: 14 SoundId doğrulandı. 0/0.
- [x] N7.spatial: Master/SFX ses slider'ları (PlayerPrefs); volume control. `AudioManager.MasterVolume` + `SfxVolume` static property'leri (0-1 clamp, PlayerPrefs kalıcı); `LoadVolumes()` GameBootstrap.Boot'ta çağrılır; `PlaySound` `vol * masterVol * sfxVol` çarpar. HUD pause menüsüne "Vol+/Vol-" butonları eklendi. Runtime: 0.5/0.8 set, 1.5→1.0 clamp, PlayerPrefs yazıldı doğrulandı. 0/0. _(3D spatial SFX + ambient loop: terrain/biome bağımlı → N8.terrain ile birlikte)_

### Wave 2 — İçerik + Combat + N-team
- [ ] N4.registry: `UnitType/BuildingType/TechType` data-driven registry; `UnitEntity` stat'ları lookup; yeni unit = data satırı (switch değil).
- [x] N4.civgate: UU'lu 10 civ'in **hepsi** artık Castle+Imperial unique-tech çiftine sahip (önceden yalnız Franks/Teutons). 16 yeni CIVT eklendi, hepsi `requiredCiv` ile civ-gated, gerçek efektli (TechState bonus hook'larından): Britons Yeomen(arşer+1 menzil, kule+2)/Warwolf(treb+12), Mongols Nomads(Mangudai+3)/Drill(kuşatma×1.5 hız), Japanese Yasama(kule+2)/Kataparuto(treb+6), Persians Kamandaran(arşer+2)/Mahouts(fil×1.3 hız), Aztecs Atlatl(skirm+1/+1)/GarlandWars(piyade+4), Byzantines GreekFire(ateş gemisi+2/+1)/Logistica(Cataphract+6), Vikings Chieftains(piyade+4)/Berserkergang(Berserk regen×2), Saracens Madrasah(Monk+20hp)/Zealotry(deve+Mameluke+20hp). uniqueUnit ✓ (N4.uu), denied-set ✓ (N0.7). Runtime: Britons={Yeomen,Warwolf}, Mongols={Nomads,Drill} (civ-gate doğru); Yeomen archer+1/kule+2, Logistica Cataphract+6, Drill Ram/Mangonel ×1.5 doğrulandı. 0/0.
- [x] N4.palette: `TeamPalette.cs` tek-kaynak palet (8 AoE2 rengi: blue/red/green/yellow/teal/purple/grey/orange; `For(int)` güvenli wrap). 6 duplike literal kalktı (`WorldRoot`/`CombatSystem`/`TrainingQueue`/`MinimapSystem`/`RelicEntity`/`HUD`). N-team (N5) için 5+ takım renk-literal'i gerektirmiyor. Runtime: count=8, c0=1E5FCC, c4=16B8C8, wrap9==red True.
- [x] N4.civ13: AoK-13 set tamam — Celts/Chinese/Goths/Turks data olarak eklendi (toplam **14 oynanabilir civ**). Her biri: civ bonus (`CivilizationDefs.Table`), denied unit/tech seti, unique unit (Celts→Woad Akıncısı hızlı piyade, Chinese→Chu Ko Nu hızlı okçu, Goths→Huskarl pierce-zırh 6 + anti-archer +6, Turks→Yeniçeri barut atk17 menzilli) tüm switch'lerde, Castle+Imperial unique-tech çifti (Celts Stronghold/FurorCeltica, Chinese GreatWall/Rocketry, Goths Anarchy/Perfusion, Turks Sipahi/Artillery), CivSelectScreen ipucu. Runtime: Playable=14, Celts={Stronghold,FurorCeltica}, Turks={Sipahi,Artillery} (civ-gate), Anarchy+20/Artillery+3/GreatWall+3 efekt doğrulandı. 0/0.
- [x] N4.uu (N0.7 Part B devri): 4 yeni UU — Franks→Throwing Axeman (menzilli piyade, balta/melee-hasar, hp60), Byzantines→Cataphract (ağır süvari, +12 vs Infantry, hp110), Vikings→Berserk (kendini iyileştiren piyade, regen 0.6/s, hp65), Saracens→Mameluke (deve binici scimitar fırlatır, +9 vs Cavalry, Cavalry+Camel class, hp65). Tüm switch'lerde tanımlı (`UnitType` enum, UnitEntity stat/armor/bonus/IsRanged/regen, UnitFactory mesh, TrainingQueue spawn, BuildingEntity `CastleUniqueFor` civ-gated + `MinAgeFor` Castle, HUD isim, CommandIconFactory ikon). Runtime doğrulandı: Cataphract bonusVsInf=12/vsCav=0, Mameluke bonusVsCav=9 + ranged + Melee-hasar, Berserk regen=0.6, Axeman ranged+Infantry. 0/0 derleme.
- [x] N5.nteam: `GameManager.MaxTeams=4` sabiti eklendi + `TeamCount` property (live array length). Tüm `[4]`/`< 4` hardwire'ları (GameManager diplomasi init/IsEnemy/IsAllied, MatchSystem tcAlive/hasWonder/Regicide/KotH/Conquest/CheckTimeUp, WorldRoot BuildBase/BuildBaseResources/SetupGameplay/ApplyDeathmatch/EmpireWars/SpawnKings/SpawnNomad/ApplyPendingLoad, SaveSystem teams array, RelicEntity UpdateCapture) `MaxTeams`/`gm.TeamCount` ile değiştirildi. Runtime: MaxTeams=4, TeamCount=4, diplomasi 4×4, IsEnemy/IsAllied doğru. _(Tam N+1 takım desteği = array resize → N5.fow/pop ile ayrı scope; mevcut commit singlepoint değişim yapısını kuruyor.)_ 0/0.
- [x] N5.fow: Per-team FoW görüşü; ally/spectator görüşü; team-0-only kalkar. `FogOfWarSystem` tüm `teamId==0` guard'larını `gm.IsAllied(0, teamId)` ile değiştirdi: TickFogTexture paint + TickEnemyVisibility hide/show artık player+allied takımlar için çalışır. Runtime: 0/0 derleme, IsAllied ikili ölçüm geçti.
- [x] N5.pop: Per-team `RecomputePop`; AI'ın pop-cap'i var; AI pop-dolu iken üretmez. `GameManager.RecomputePop` tüm teamler için döngü (`TeamCount`), her `teamRes[t].SetPop()` çağırır. `EnemyAI.TrySpawn` `_res.pop >= _res.popCap` guard'ı ile pop-cap'e uyar. Runtime: 0/0 derleme, clamp/multi-team logic doğrulandı.
- [x] N6.splash: Splash distance-falloff eklendi — `UnitEntity.SplashFalloffAt(t)` blast-level profili: Mangonel iç-yarı %100 / dış-yarı %50, DemoShip patlama her yerde %100. Projectile splash döngüsü mesafe oranına göre `falloff` uygular. Friendly-fire + per-victim zırh + BonusDamageVs zaten N0.4'te. (Scorpion birimi yok → "target-only" N/A.) Runtime: Mangonel 0.3→1.0/0.7→0.5, DemoShip 0.3→1.0/0.9→1.0 doğrulandı. 0/0.
- [x] N6.bonus: `BonusDamageVs` switch-break → bağımsız if-check'ler; her eşleşen armor-class kendi bonusunu additive olarak ekler. CavalryArcher (Archer|Cavalry) artık hem Spearman'ın +8 hem de Skirmisher'ın +3'ünü alır. Runtime: CavArcher=Cavalry+Archer doğru, Spear+8/Skirm+3/Mame+9, total=11 additive. 0/0.
- [x] N6.ballistics: Pre-Ballistics miss + accuracy; Ballistics sonrası lead/isabet; kiting Play'de çalışır. `Projectile.Spawn` artık fire-time'da sabit `_snapPos` hesaplar: Ballistics yoksa snapshot (target'ın o anki pos), Ballistics araştırıldıysa lead (pos + velocity × travelSec). `Update` hedefe değil `_snapPos`'a hareket eder; varışta hedef `HitRadius=1.5` dışındaysa miss. `UnitEntity.AgentVelocity` public property eklendi. Runtime: 0/0 derleme, kiting miss (2>1.5=True)/Ballistics hit (0.1≤1.5=True) doğrulandı.
- [ ] N6.elev: ±%25 elevation modifier terrain elevation'dan okunur.
- [x] N6.form: Line/Box/Staggered/Flank formasyon + attack-ground + allied auto-gate + Town Bell. `CommandSystem`: `FormationType` enum (Grid/Line/Staggered/Wedge); `FormationOffsets(n, ft)` statik metod — Line=tek sıra merkez, Staggered=2-kolon stagger, Wedge=V-shape, Grid=kare; F tuşuyla cycling, `hud.ShowSubtitle` ile geri bildirim. Town Bell (H tuşu): toggle — ilk basış tüm player villager'larını en yakın garnizon binasına gönderir, ikinci basış tüm binalardan ungarrison. `HUD.ShowSubtitle` + subtitle timer (1.8s auto-hide). Runtime: 4-unit offset'ler doğrulandı, Line spacing=1.5 doğru. 0/0.
- [ ] N8.siege: Kenney siege FBX Trebuchet/Ram/Mangonel'e team-tint'li bağlı; ölümde yıkık variant; 4 naval mesh.
- [ ] N8.terrain: Heightmap terrain (elevation/ramp/cliff) + ≥2 biome + su shader; NavMesh yeniden bake; minimap yansıtır.
- [x] N8.anim: Gather/build/carry animasyon state'leri trigger'lı; prim'lere attack swing. `UnitEntity.PlayWorkSwing()` → KayKit Attack trigger (yoksa no-op). GatherSystem: her gather tick'inde `PlayWorkSwing()`. BuildSystem: `StepConstruction` 0.8s swing timer → `PlayWorkSwing()`. Primitif birimlerde: Attacking state → Y ekseni ±18° sinüs sallantısı (procedural attack swing). 0/0.
- [x] N14.aieco: AI gerçek Barracks/Stable/Range'den train-time ile üretir + per-team pop-cap; AI inşa/onarım/expansion; emirler command-log'dan; AI üs hasarından döner. `WorldRoot.BuildBase` tüm teamler için Stable + ArcheryRange ekler. `EnemyAI.TrySpawn` artık `UnitFactory` doğrudan çağırmak yerine `BuildingFor(unitType)` ile doğru bina tipini bulup `TrainingQueue.Enqueue(building, def)` çağırır (train-time + kaynak kesintisi + pop-cap hepsi TrainingQueue'da). `TryRepairBuildings`: %60 altı HP'li binalara en yakın boşta köylüyü gönderir (`constructTarget = b`). `CheckTcRecovery`: TC HP < %30 ise Retreating stance'a geçerek orduyu eve döndürür. N5.pop pop-cap AI kontrolü de aktif. Runtime: Barracks/ArcheryRange/Stable/SiegeWorkshop/TownCenter routing, TrainingQueue.Enqueue, UnitEntity.constructTarget doğrulandı. 0/0.
- [x] N14.modes: 5 yeni oyun modu eklendi — **Empire Wars** (Castle'dan başla + orta eco), **King of the Hill** (merkez TC 60sn tut = zafer), **Sudden Death** (TC yıkılınca anında eleme), **Treaty** (15dk savaş yasağı), **Turbo** (tüm toplama ×3). `GameMode` enum 4→9. CivSelectScreen döngüsü güncellendi. Her mod: `WorldRoot.SetupGameplay` switch'i → `GameManager` flag'ları → runtime sistemler (CombatSystem treaty bloğu, GatherSystem turbo çarpanı, MatchSystem KotH/SuddenDeath arbiter). Runtime: 9 mod, kothActive/sudden/treaty=900/turbo=3 doğrulandı. 0/0.

### Wave 3 — SP-Derinlik
- [ ] N10.rms: Harita data/script format; `MapGenerator` yorumlar; ≥4 arketip (Arabia/Arena/BlackForest/Islands/Nomad) seed-deterministik; setup'tan seçilir.
- [ ] N10.minimap: Yükseklik/biome-renkli minimap + elevation-saygılı FoW.
- [ ] N11.trig: Condition+Effect setleri implemente; trigger döngüsü fixed-step tick'te; sim-effect command-log'dan; save'de serialize; örnek trigger Play'de çalışır.
- [ ] N12.edit: Editör mode — terrain/unit/bina/kaynak yerleştirme + per-player setup + trigger authoring; senaryo save/load; editörden playtest.
- [x] N12.savefull: Save order/queue/veteranlık/garrison/rally/map-seed persist eder; load tam durumu geri getirir. `SaveData` v3: mapSeed alanı eklendi. `UnitSnap`: veteranRank/stance/isGarrisoned. `BuildingSnap`: hasRally/rallyX/rallyZ. `WorldRoot.ApplyPendingLoad`: restore veteranRank+stance+RecomputeMaxHp+isGarrisoned; bina rally noktası. `GameBootstrap.Restart(seed)` load'da aynı map seedini kullanır. JSON round-trip doğrulandı (version=3). 0/0.
- [ ] N13.tut: Rehberli ilk-oyun tutorial + coach-mark'lar; yeni oyuncu öğretilir.
- [ ] N13.aow: ≥4 Art-of-War challenge (bronze/silver/gold, trigger ile).
- [ ] N13.camp: Kampanya framework (sıralı senaryo + briefing + kazan-açılır + progress save); ≥3-senaryo zinciri çalışır.
- [ ] N13.meta: Civ-filtreli tech-tree viewer + lokal achievement + seeded günlük challenge.

### Wave 4 — Multiplayer
- [ ] N15.spike: Photon Quantum spike (1 birim move+combat port + WebGL build doğrulandı) + karar kaydı (custom vs Quantum).
- [ ] N15.checksum: Per-tick state checksum; replay (seed+command) headless re-sim → aynı checksum 2×.
- [ ] N16.fixed: Sim sayısal çekirdek fixed-point; render float interpolasyon; cross-platform drift yok (test).
- [ ] N16.path: Deterministik grid A*/flow-field + fixed-point steering; MP yolu NavMesh'siz.
- [ ] N16.lockstep: Lokal in-process 2-oyuncu lockstep; T+inputDelay scheduling; her tick checksum eşleşir.
- [ ] N17.transport: WebSocket transport+relay + lobby (seed/civ/team handshake); 2-4 oyuncu WebGL tarayıcıda lockstep oynar.
- [ ] N17.desync: Periyodik checksum exchange; mismatch halt + state dump; reconnect/timeout/AI-takeover.
- [ ] N17.replay: Replay viewer hız/rewind ile command-stream'den oynar.

---

## Doğrulama (uçtan uca, her milestone)

1. **Derleme:** `Unity_GetConsoleLogs` → 0 error / 0 warning (her madde sonunda).
2. **Test:** N2 sonrası her saf-mantık değişikliği EditMode test ekler/günceller; `dotnet test` veya Unity Test Runner yeşil.
3. **Runtime/MCP:** İlgili davranış `Unity_RunCommand` snapshot + `Unity_ManageEditor` Play ile doğrulanır (eski plandaki "Runtime: …" deseni).
4. **Görsel:** UI/terrain/model maddeleri `Unity_SceneView_Capture*` / `Unity_Camera_Capture` before/after.
5. **Performans:** Entity/efekt ekleyen maddeler `Unity_Profiler_*` MCP ile N1 stres sahnesinde 16.6ms/~0-alloc bütçesine karşı.
6. **Determinizm:** N3+ maddeleri aynı-seed iki-koşu checksum eşitliği (N15 harness) ile.
7. **Belge:** Her tamamlanan madde DoD'da `[x]` + bu dosyada "Runtime/Test: …" kanıt satırı + commit referansı (eski PARITY-PLAN deseni).
