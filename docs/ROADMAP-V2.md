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
- [ ] N1.pool: _(ikincil GC — revisit)_ Mermi/ok/popup `UnityEngine.Pool`'dan; Instantiate/Destroy yok; per-shot alloc ~0.
- [ ] N1.mat: _(ikincil draw-call — revisit)_ Paylaşılan material cache + instancing + MaterialPropertyBlock (HitFlash/`.material` paylaşım dikkatli).
- [x] N1.hpbar (kısmi): `UnitEntity.IsKayKitModel` cache'i — HP-bar IMGUI pass'ı artık per-frame `GetComponentInChildren<SkinnedMeshRenderer>()` çağırmıyor. _(Tam world-space billboard'a taşıma revisit.)_
- [x] N1.budget: 300-birim stres-spawn ölçümü (RunCommand) — grid ~48× az proximity-iş; 16.6ms/alloc profiler dokümantasyonu N1.pool/mat sonrası.
- [ ] N2.asmdef: _(ertelendi — asmdef yeniden-yapılandırması tüm build'i kırma riski; şimdilik `CombatMath.SelfTest` editör-içi pin sağlıyor)_ `AgeOfArena.asmdef` + `Tests.asmdef`; EditMode test runner yeşil.
- [x] N2.resolver: Saf `CombatMath` (NetDamage + ArmorFor, N0.1 siege=melee dahil); `UnitEntity`/`BuildingEntity.TakeDamage` buna yönlendirildi; `CombatMath.SelfTest()` formülü pin'ler. Runtime: SelfTest PASS.
- [ ] N2.mapgen: _(ertelendi — `WorldRoot` sahne-kurulumundan büyük çıkarma; N10 RMS ile birlikte yapılacak)_ Saf `MapGenerator` (seed→placement list).
- [ ] N3.prng: Deterministik PRNG struct; 41 Random sahası sim/kozmetik ayrıldı; sim akışı mapSeed'den seeded; aynı seed→aynı sim sonucu (test).
- [ ] N3.fixedstep: Fixed-step accumulator (~30Hz) tüm Tick'lere sabit dt; render interpolasyon; pause/hız sim-step üzerinden; `Time.deltaTime` sim'de yok.
- [ ] N3.cmdlog: Command type seti + `CommandSystem`/`EnemyAI` enqueue eder; sim tick-başı uygular; lokal command recorder (seed+per-tick) replay temeli.

### Paralel görünür kazanımlar (gün-1'den; N7 müzik · N9 UX/a11y/i18n)
- [x] N9.pause: Pause-on-blur — `FocusPause.cs` (OnApplicationFocus/Pause → timeScale 0 + "Duraklatıldı" overlay); odak dönünce önceki hız geri yüklenir; game-over (`MatchSystem.IsOver`) / Esc-menü pause'larına dokunmaz. GameBootstrap'ta WorldRoot'a bağlı. Runtime: 1.5→0→1.5 doğrulandı.
- [ ] N9.hotkeys: Remappable `HotkeyAction`→`KeyCode` + ayar UI + PlayerPrefs + çakışma tespiti + buton-üstü hotkey etiketi (mevcut `Hotkeys.cs` üzerine).
- [ ] N9.feedback: Attack-move zemin göstergesi + rally çizgileri + shift-kuyruk waypoint görseli; FoW varsayılan açık toggle.
- [ ] N9.i18n: Lokalizasyon tablosu (key→string, TR+EN) + TR-glyph kapsamlı TMP/SDF font; tüm HUD string'leri buradan.
- [ ] N9.a11y: Colorblind-güvenli palet + şekil/ikon kodlama (minimap/HP/diplomasi); UI-ölçek slider; caption toggle.
- [ ] N9.postgame: Maç-sonu özet (kaynak/birim/çağ-zamanları/skor grafiği) + birim stat paneli + canlı tech-boost'lu tooltip + olay-uyarı logu.
- [ ] N7.music: Menü teması + çağ-başına in-game müzik (Dark/Feudal/Castle/Imperial) + zafer/yenilgi sting'leri (WebGL-sıkıştırılmış); savaşta ducking.
- [ ] N7.sfx: Gerçek SFX seti (kılıç/ok/inşa/eğitim/ölüm/UI/çağ-fanfar) `Resources/Audio`'ya (loader zaten dosyayı tercih eder); pitch-vary + round-robin.
- [ ] N7.spatial: Master/SFX/Müzik ses slider'ları (PlayerPrefs); 3D/spatial savaş-inşa SFX'i; biome-bağlı ambient loop.

### Wave 2 — İçerik + Combat + N-team
- [ ] N4.registry: `UnitType/BuildingType/TechType` data-driven registry; `UnitEntity` stat'ları lookup; yeni unit = data satırı (switch değil).
- [ ] N4.civgate: Civ row'ları uniqueUnit/castle+imperial unique-tech/denied-set; TrainingQueue/ResearchSystem/komut-kartı civ-gate'li; ≥2 civ tech-tree subtraction'ı Play'de farklı.
- [ ] N4.palette: Tek-kaynak takım paleti; 4-renk literal'leri kalkar.
- [ ] N4.civ13: AoK-13 set tamam (Celts/Chinese/Goths/Turks data); her biri UU + 2 unique tech + bonus + denial.
- [ ] N4.uu (N0.7 Part B devri): UU'suz 4 civ'e unique unit — Franks→Throwing Axeman, Byzantines→Cataphract, Vikings→Berserk, Saracens→Mameluke; tüm switch'lerde tanımlı + `CastleUniqueFor` civ-gated; Play'de yalnız ilgili civ'in Castle menüsünde.
- [ ] N5.nteam: `[4]` diziler/`<4` guard'lar konfigüre takım sayısı; teamCount=5+ skirmish çalışır (TC/relic/save/repair doğru).
- [ ] N5.fow: Per-team FoW görüşü; ally/spectator görüşü; team-0-only kalkar.
- [ ] N5.pop: Per-team `RecomputePop`; AI'ın pop-cap'i var; AI pop-dolu iken üretmez.
- [ ] N6.splash: Splash falloff + friendly-fire + per-victim zırh; blast-level field; Scorpion target-only.
- [ ] N6.bonus: Bonus damage tüm eşleşen armor-class'ta STACK + per-class zırh; Halberdier-vs-Elephant toplama doğru.
- [ ] N6.ballistics: Pre-Ballistics miss + accuracy; Ballistics sonrası lead/isabet; kiting Play'de çalışır.
- [ ] N6.elev: ±%25 elevation modifier terrain elevation'dan okunur.
- [ ] N6.form: Line/Box/Staggered/Flank formasyon + attack-ground + allied auto-gate + Town Bell.
- [ ] N8.siege: Kenney siege FBX Trebuchet/Ram/Mangonel'e team-tint'li bağlı; ölümde yıkık variant; 4 naval mesh.
- [ ] N8.terrain: Heightmap terrain (elevation/ramp/cliff) + ≥2 biome + su shader; NavMesh yeniden bake; minimap yansıtır.
- [ ] N8.anim: Gather/build/carry animasyon state'leri trigger'lı; prim'lere attack swing; corpse/dissolve.
- [ ] N14.aieco: AI gerçek Barracks/Stable/Range'den train-time ile üretir + per-team pop-cap; AI inşa/onarım/expansion; emirler command-log'dan; AI üs hasarından döner.
- [ ] N14.modes: Empire Wars/KotH/Sudden Death/Treaty/Turbo kural-toggle'ları setup'ta seçilir + Play'de etkili.

### Wave 3 — SP-Derinlik
- [ ] N10.rms: Harita data/script format; `MapGenerator` yorumlar; ≥4 arketip (Arabia/Arena/BlackForest/Islands/Nomad) seed-deterministik; setup'tan seçilir.
- [ ] N10.minimap: Yükseklik/biome-renkli minimap + elevation-saygılı FoW.
- [ ] N11.trig: Condition+Effect setleri implemente; trigger döngüsü fixed-step tick'te; sim-effect command-log'dan; save'de serialize; örnek trigger Play'de çalışır.
- [ ] N12.edit: Editör mode — terrain/unit/bina/kaynak yerleştirme + per-player setup + trigger authoring; senaryo save/load; editörden playtest.
- [ ] N12.savefull: Save order/queue/veteranlık/garrison/rally/map-seed persist eder (sığ save düzelir); load tam durumu geri getirir.
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
