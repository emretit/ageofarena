# Age of Arena — Unity Port Handoff

## Proje

`/Users/emreaydin/ageofarena/AgeOfArenaUnity/` — Unity **6000.4.1f1**, Built-in Render Pipeline.
Three.js web sürümü **kaldırıldı** (git geçmişinde mevcut). Bu repo artık tamamen Unity.

> **⚠️ Eşzamanlı oturum notu:** Oturum 11 (AoE komut barı, UI) ile Oturum 9–10 (AI koordinasyon,
> Fog of War) **paralel** çalışıldı ve aynı çalışma ağacını paylaştı. Dosya çakışması yok
> (O11: HUD/Selection/Command/BuildingPlacement + SafeBaseInput + manifest.json; O9–10:
> EnemyAI/GameManager/WorldRoot/GameTypes + FogOfWar*). **Oturum 12** (Faz 5 + Relic) de aynı ağacı
> paylaşan ayrı bir oturumdu. **Oturum 13** hepsini Unity MCP ile birlikte doğruladı: tek çakışma
> `BuildingFactory.Wall` (O12 metodu ↔ eski `Wall` renk alanı) idi, düzeltildi → **0 error / 0 warning**.

---

## Oturum 13 (2026-06-02) — Doğrulama (ilk MCP'li oturum) + Komut Barı Revizyonu ✅ MCP ile teyitli

Plan: `~/.claude/plans/handoff-haz-r-sonraki-session-calm-token.md`.
**Unity MCP'nin gerçekten yüklü olduğu ilk oturum** — O5–O12'nin tüm "MCP'siz / runtime
doğrulanmadı" notları burada kapatıldı.

### Derleme + runtime doğrulama (MCP)
- **Blocker bulundu + düzeltildi:** `BuildingFactory.cs` **CS0102** — O12'nin yeni `Wall()` factory
  metodu, var olan `static readonly Color Wall` alanıyla çakışıyordu → renk alanı **`Plaster`** olarak
  yeniden adlandırıldı. Bu olmadan tüm Assembly-CSharp fail ediyordu (O12 kodu fiilen derlenmiyordu).
- Düzeltme sonrası **0 error / 0 warning** (MCP `GetConsoleLogs`).
- **Submit input spam'i GİTTİ** — `StandaloneInputModule.inputOverride = SafeBaseInput` runtime'da
  doğrulandı, Play'de 0 exception. (O11 fix'i çalışıyormuş; eski spam logları fix derlenmeden önceki Play'den.)
- `Custom/FogOfWar` shader **bulundu + render ediyor**; sahne 22 birim / 24 bina ile kuruluyor;
  `GameManager.fow` + `relicSystem` canlı; **Relic sistemi çalışıyor** (ekranda 0/3, capture 5s, +0.5 altın/s).
- Görsel teyit: MCP `Camera_Capture` Play'de RenderTexture'lar (minimap/FoW) yüzünden patlıyor →
  `ScreenCapture.CaptureScreenshot` ile game-view PNG alındı.

### Komut barı revizyonu (`HUD.cs`) — kullanıcı 4 eksende cila istedi
- **Düzen:** komut kartı artık **sabit 5×3 AoE ızgarası** (dikey ortalı); boş slotlar koyu çerçeve,
  komutlar ilk N slotu doldurur → az/çok buton fark etmez hep düzgün durur, **taşma yok**. İlerleme
  çubuğu + kuyruk metni **sol info paneline** taşındı (kart sadece ızgara).
- **Kontrast:** `Dim()` artık koyu arduvaza lerp (eski düz ×0.32 teal'i neredeyse karartıyordu).
- **Font/okunabilirlik:** buton ad/maliyet/hotkey'lerine siyah `Outline`; hotkey için koyu rozet zemini;
  üst bar sayıları kalın + outline.
- **Boyut:** `BarH 210`, `Btn 60`, `Gap 6`, `Cols 5 × Rows 3`; `CanvasScaler.matchWidthOrHeight 0.5`.
- Üst barda altın aksan çizgisi + info paneli ile ızgara arasında dikey ayraç.

### Relic göstergesi üst bara taşındı
- `RelicSystem`'in IMGUI `OnGUI` "Relikler: N/3" çizimi **kaldırıldı** (ağaçların üstüne biniyordu).
- Yerine HUD üst barına **Relic N/3** girdisi (her frame `relicSystem.CountControlled(0)`).

### Relic kararı (kullanıcı)
- **Sadece gelir** — ayrı relic zafer koşulu YOK. Relic'ler pasif altın sağlar; kazanma TC-eliminasyonuyla.
  `MatchSystem`'e dokunulmadı.

### Commit
- **Checkpoint `3c67d47`** — tüm O5–O12 + relic (51 dosya, 2572+ satır), 0 error. Push yok.
- O13'ün HUD/RelicSystem + HANDOFF değişiklikleri ayrı commit'lendi.

---

## Oturum 12 (2026-06-02) — Faz 5: Yeni Mekanikler (Scout + Medic + Duvar/Kapı + Relic) ✅ kod / ⚠️ MCP'siz

Plan: `~/.claude/plans/handoff-u-incele-oyunu-geli-tirmeye-crispy-sphinx.md`. Faz 5'in dört
mekaniği eklendi. Mevcut Fog of War (O10) ile uyumlu; **EnemyAI'a dokunulmadı** (FSM güvende).

### Scout (gözcü) — hızlı, hasarsız keşif birimi
- **Barracks**'tan **[S]** (30Y / 14s), Dark çağ. `moveSpeed 6.5` (en hızlı), `hp 40`,
  `BaseAttackDamage 0`, `AggroRadius 0`. `CombatSystem.StepCombat` başı guard → saldırı emrinde boşta kalır.
- **FoW görüşü 13u** (`FogOfWarSystem.UnitSight`) — sisi en çok açan birim.

### Medic (şifacı) — yakındaki dost birimi iyileştirir
- **Castle**'dan **[H]** (60Y / 26s), Kale çağı. `hp 35`, `HealRadius 6u`, `HealPower 3 hp/sn`.
- `UnitEntity.Heal(amount)` eklendi. `CombatSystem.StepHeal`: Medic **Idle** iken menzildeki en düşük
  hp%'li dost `UnitEntity`'yi (kendisi/dolu hariç) iyileştirir; hareket emri önceliklidir.

### Duvar & Kapı (`BuildingType.Wall`, `Gate`)
- **Wall** [**W**, 10 odun, 200hp] — **kare hücre** (rotasyon yok; her yöne döşenir) + **carving'li
  `NavMeshObstacle`** → hareketi gerçekten engeller (**oyundaki ilk gerçek pathfinding-engeli**).
- **Gate** [**O**=opening, 30 odun, 450hp] — NavMeshObstacle **yok** → herkese geçirgen choke-point.
- `BuildingPlacement`: ghost'tan NavMeshObstacle çıkarılır (önizleme NavMesh oymaz); Wall/Gate çakışma
  kutusu 0.7 → bitişik segment dizilebilir. `HandleBuildHotkeys` veri-güdümlü olduğu için W/O otomatik çalışır.

### Relic / Kontrol Noktası (yeni: `RelicEntity` / `RelicSystem` / `RelicFactory`)
- Merkeze **3 relic** (`(0,0,0)`, `(-16,0,16)`, `(16,0,-16)`). Bir takım **3.5u** içinde tek başına
  **5s** durunca ele geçirir (çekişmeli=kimse alamaz); kontrol eden **0.5 altın/sn**; orb sahibinin rengine boyanır.
- **Fırsatçı** — tüm takımların birimleri kapar (AI orduları merkezden geçerken alır). Minimap'te büyük
  renkli nokta; sol-üstte "Relikler: N/3". IDamageable değil (yok edilemez), FoW gizlemesi dışında.
- `GameManager.relics` + `RegisterRelic` + `relicSystem.Tick`; `WorldRoot.BuildRelics`.

### Değişen/yeni dosyalar
`GameTypes` (UnitType+Scout,Medic / BuildingType+Wall,Gate), `UnitFactory` (Scout/Medic mesh),
`UnitEntity` (statlar + HealRadius/HealPower/Heal), `TrainingQueue` (dispatch), `BuildingEntity`
(Barracks+Scout, Castle+Medic, MinAgeFor), `CombatSystem` (Scout/Medic guard + StepHeal),
`BuildingDefs` (Wall/Gate), `BuildingFactory` (Wall/Gate + NavMeshObstacle), `BuildingPlacement`
(ghost obstacle strip + küçük check box), `FogOfWarSystem` (Scout görüşü), `HUD` (TR isimler),
`MinimapSystem` (relic noktaları), `GameManager`/`WorldRoot` (relic wiring). **Yeni:** `RelicEntity.cs`,
`RelicSystem.cs`, `RelicFactory.cs`.

**Doğrulama:** Bu session'da Unity MCP **yüklenmedi** → derleme MCP ile doğrulanamadı. Kod elle gözden
geçirildi: tüm `UnitType`/`BuildingType` switch'lerinde `_` default var (CS8509 yok), API imzaları uyumlu.
**Sonraki oturum:** Unity'ye odaklan → recompile → 0 error/0 warning teyidi + şu testler:
1) Barracks→S: hızlı/hasarsız gözcü, sis çok açılır. 2) Castle→H: Medic yaralı dostu iyileştirir.
3) Köylü→W: duvar inşa olur, **birim duvarı dolaşır** (carving); O→kapı, birimler içinden geçer.
4) Merkezdeki relic'e birim götür → ele geçir, altın artışı + minimap noktası sahibinin renginde.

---

## Oturum 11 (2026-06-02) — Age of Empires Tarzı Alt Komut Barı (UI) ⚠️ runtime kısmen doğrulandı

Plan: `~/.claude/plans/handoff-md-revize-edildi-de-i-iklikler-harmonic-deer.md`

### Yapılanlar
- **`HUD.cs`** baştan yazıldı: üst kaynak barı korundu; alt kısım artık **tam genişlik AoE komut
  barı** — solda seçili bina/birim adı + HP barı, sağda **tıklanabilir komut buton ızgarası**
  (5'li grid, kategori renkli: eğitim=mavi, araştırma=mor, çağ atlama=altın, inşa=yeşil, pazar=teal).
  Her butonda Türkçe ad + maliyet + hotkey rozeti.
  - **Bina seçili** → `GetTrainables`/`GetResearchables` butonları (çağ atlama dahil); Market → 4 takas butonu
  - **Köylü seçili** → `BuildingDefs.Buildable()` inşa menüsü (çağ-kilitli olanlar gizli)
  - Afford edilemeyen / pop dolu butonlar otomatik gri/pasif. **Klavye hotkey'leri hâlâ çalışır.**
  - `EventSystem` + `GraphicRaycaster` runtime kuruluyor (uGUI tık için).
- **`SelectionSystem.cs` + `CommandSystem.cs`** — `EventSystem.current.IsPointerOverGameObject()`
  guard'ı: HUD'a tıklama dünya seçimini/komutunu tetiklemiyor.
- **`BuildingPlacement.cs`** — köylü "Bina yap:" OnGUI text ipucu kaldırıldı (artık butonlar).
- **`SafeBaseInput.cs`** (yeni) — `BaseInput` türevi; eksik InputManager eksenlerinden gelen
  `ArgumentException`'ı yutar. `StandaloneInputModule.inputOverride` olarak bağlanır.

### Bulunan + düzeltilen blocker'lar (önceki oturumlardan kalma)
1. **`VisualEffectSystem.cs` (O8) HİÇ derlenmiyordu** — `ParticleSystem` kullanıyor ama
   `Packages/manifest.json`'da **particlesystem modülü yoktu** → tüm Assembly-CSharp fail
   (dolayısıyla O8–O10'un "0 error" notları aslında geçersizdi). → manifest'e
   `com.unity.modules.particlesystem` eklendi. **Artık 0 CS error** (Editor.log).
2. **"Input Button Submit is not setup" exception spam'i** — `InputManager.asset`'te yalnızca
   Horizontal/Vertical/Mouse ScrollWheel tanımlı; `StandaloneInputModule` her frame "Submit"/"Cancel"
   yokluyordu → her frame exception, Editor.log 1M+ satıra patladı, FPS dibe vurdu →
   **"oyun oynanmaz görünüyor" şikayetinin sebebi buydu.** → `SafeBaseInput` ile çözüldü.

### ⚠️ Doğrulama durumu
- **Derleme: tüm proje (O9+O10+O11) 0 CS error** — Editor.log ile doğrulandı (MCP bu oturumda yüklenmedi).
- **`SafeBaseInput` Submit fix YAZILDI ama Play'de teyit edilMEDİ.** Unity Play modunda script
  derlemez; kullanıcı Play'i durdurdu ama Unity henüz recompile etmedi (odak/refresh bekliyor).
  **Sonraki oturum:** Unity'ye odaklan → recompile → Play → Submit spam'inin gittiğini + komut barının
  çalıştığını teyit et, **ekran görüntüsü al** (kullanıcı görünümü beğenmedi; bar boyutu/cila gözden geç-).

---

## Oturum 10 (2026-06-02) — Fog of War (Faz 4) ✅

### Mimari
- **Görsel FoW** — AI/combat sunucu tarafı; FoW tamamen istemci görsel katmanıdır.
- **Dünya boyutu:** 120×120u (−60..+60 XZ) → 128×128 Texture2D (1 piksel ≈ 0.94u).
- Üç görünürlük katmanı (kırmızı kanal): `0`=keşfedilmemiş (siyah) · `70`=shroud/gölge · `255`=şu an görünür.

### Yeni dosyalar
| Dosya | Görev |
|---|---|
| `Assets/Shaders/FogOfWar.shader` | Built-in RP surface shader; `noambient` → siyah keşfedilmemiş alan; fog texture'ı world-position UV ile örnekler |
| `Assets/Scripts/FogOfWarSystem.cs` | 128×128 CPU Texture2D; her frame sight circle boya → GPU'ya yükle; her 0.5s düşman renderer toggle |

### Görüş yarıçapları
| Birim/Bina | Yarıçap |
|---|---|
| Cavalry | 9u |
| Archer | 8u |
| Militia | 7u |
| Villager / diğer | 5u |
| Trebuchet | 4u |
| TownCenter | 10u |
| Castle | 8u |
| Barracks/ArchRange/Stable | 7u |
| Diğer binalar | 5u |

### Değişen dosyalar
- **`GameManager.cs`** — `public FogOfWarSystem fow;` eklendi
- **`WorldRoot.cs`** — `_groundRenderer` field'i; `SetupGround` renderer'ı kaydeder; `Build()` sonunda `gm.fow = AddComponent<FogOfWarSystem>(); gm.fow.Init(_groundRenderer)`

**Doğrulama:** Bu oturumda Unity MCP tool'ları yüklenmedi → **0 error/0 warning teyidi Unity'de gerekiyor.** Kontrol edilecekler: `Custom/FogOfWar` shader'ı Unity import ederek `Shader.Find` bulabilmeli; `noambient` sözdizimi Built-in RP'de geçerli (Unity 2019.3+); `Color32[]` tekrar kullanımı GC baskısını minimumda tutar.

---

## Oturum 9 (2026-06-02) — AI Koordinasyon (Faz 2) ✅

### AI Kişiliği (`AIPersonality` enum)
- **`GameTypes.cs`** — `AIPersonality { Balanced, Rusher, Boomer }` eklendi.
- **`EnemyAI.Init(..., AIPersonality)`** — kişiliğe göre ayar (`ApplyPersonality`):

| Param | Balanced | Rusher | Boomer |
|---|---|---|---|
| spawnInterval | 15s | 11s | 13s |
| armyCap | 12 | 10 | 18 |
| rushThreshold | 8 | 5 | 12 |
| villagerTarget | 3 | 2 | 6 |
| retreatLoss | %40 | %60 | %30 |
| ilk spawn | 15s | 8s | 22s |
| ilk tech | 12s | 16s | 8s |

- **`WorldRoot`** — `Personalities[]`: team1=Rusher (kırmızı), team2=Boomer (yeşil), team3=Balanced (sarı). AI GameObject adı `EnemyAI_T{t}_{Personality}`.

### Ordu Koordinasyonu (rally → attack → retreat state machine)
- Eski `Assess`: birimler tek tek RushThreshold'da saldırıya gönderiliyordu. Yeni: tüm ordu **tek beden** olarak `Stance { Gathering, Rallying, Attacking, Retreating }` üzerinden hareket eder (her `AssessInterval`=3s tick).
  - **Gathering** → ordu `rushThreshold`'a ulaşınca hedef seç, rally point hesapla (`ComputeRally`: home→hedef %40, max 18u), herkesi `Scatter`'lı rally'e yolla.
  - **Rallying** → ordunun ≥%70'i rally yarıçapında (`RallyRadius`=6u) **veya** 5 tick timeout → `_attackForce` kaydet, hep birlikte `CommandAttack`.
  - **Attacking** → ordu `_attackForce`'un `retreatLoss` oranını kaybederse → Retreating. Hedef ölürse yeni en yakın düşmanı hedefle (dağılmadan basmaya devam). `CommandAttack` yalnızca boşta/hedefi ölmüş birime emir verir → CombatSystem aggro ile yakındaki düşmana giren birim kendi kavgasını korur.
  - **Retreating** → eve dön; ordunun ≥%60'ı evdeyse **veya** 6 tick timeout → Gathering (yeniden topla).
- **`TrySpawn`** — saldırı sürerken üretilen takviye birim anında orduya katılır (`AttackOrder(_target)`), evde boş beklemez.

### AI Derinleştirme (kompozisyon + akıllı hedefleme + kuşatma)
- **Birim çeşitliliği** — AI artık çağa göre Militia/Archer'a ek olarak **Cavalry** (Kale) ve **Trebuchet** (Kale) üretir. `ChooseUnit` planlayıcısı: Kale çağında ~her 6 orduya 1 Trebuchet (kuşatma hattı), kalanı Militia→Archer→Cavalry rotasyonu (kilitli/parasız olanı atlar). Maliyetler AI'a özel: Cavalry 80Y, Trebuchet 200O+100A. `_spawnArcher` bool kaldırıldı → `_trainCursor`.
- **Ağırlıklı stratejik hedefleme** — `FindBestTarget` artık "en yakın" yerine **değer** seçer (ordu merkezinden mesafeyle iskontolu): düşman **villager 65** (eko avı) > **TC 60** (kazanma koşulu) > üretim binası 45 > eko binası 40 > asker 35 > ev 25. AI ekonomiyi avlar ve kazanma koşuluna baskı yapar.
- **Rol-bazlı saldırı** — `CommandAttack`: Trebuchet en yakın **binayı** hedefler (3× anti-structure), bina kalmazsa ana hedefe döner; kalan ordu paylaşılan `_target`'a basar. Aggro ile yakındaki düşmana giren birim kendi kavgasını korur.
- **Hafif formasyon** — `RallyPosFor`: melee rally hattında, Archer 3u arkada, Trebuchet 5u arkada (eve doğru) → ön hat menzilli/kuşatmayı perdeler.
- **`IsMilitary`** artık Trebuchet'i de sayar (ordu koordinasyonuna katılır).

**Doğrulama:** Bu oturumda Unity MCP tool'ları yüklenmedi (server claude'dan sonra bağlandı) → **derleme bu session'da MCP ile doğrulanamadı**. Kod elle gözden geçirildi (API/imza uyumlu, kullanılmayan `_personality` alanı kaldırıldı → CS0414 yok). Unity'de Play/refresh ile 0 error/0 warning teyidi gerekiyor.

---

## Oturum 8 (2026-06-02) — Event System + Görsel Cila ✅

### Event System
- **`GameEvents.cs`** (yeni) — statik event hub: `OnUnitKilled`, `OnBuildingDestroyed`, `OnAgeAdvanced`, `OnResearchCompleted`. `Reset()` ile restart'ta stale closure'lar temizlenir.
- **`UnitEntity.Die()`** → `GameEvents.FireUnitKilled(this, teamId)`
- **`BuildingEntity.Die()`** → `GameEvents.FireBuildingDestroyed(this, teamId)`
- **`ResearchSystem.Apply()`** → çağ atlamada `FireAgeAdvanced`, diğer tech'lerde `FireResearchCompleted`
- **`HUD.cs`** — `TickAgeText` polling kaldırıldı; `OnAgeAdvanced` event'ine reactive subscribe
- **`GameBootstrap.Restart()`** → `GameEvents.Reset()`

### Görsel Cila
- **`VisualEffectSystem.cs`** (yeni) — `OnUnitKilled`'da particle burst (turuncu/kırmızı, 12 parçacık); `OnBuildingDestroyed`'da büyük particle (28 parçacık) + kamera shake (TC/Castle/Barracks = 0.35s mag 0.4)
- **`IsometricCameraRig.cs`** — `Shake(float duration, float magnitude)` eklendi
- **`BuildSystem.cs`** — inşaat sırasında Y scale 0.05→1 lerp (`buildProgress`); tamamlanınca `Vector3.one`
- **`HUD.cs`** — `OnAgeAdvanced` (team=0)'da 3s fade-out popup ("DEREBEYLİK ÇAĞI!" altın renk)
- **`GameManager.cs`** — `vfx` (VisualEffectSystem) + `cameraRig` (IsometricCameraRig) alanları
- **`WorldRoot.cs`** — `gm.vfx` + `gm.cameraRig` wired

**Doğrulandı: 0 error, 0 warning.**

---

## Oturum 7 (2026-06-02) — Savaş İyileştirmeleri ✅

### Cavalry Charge Bonus
- `chargeTimer = 4f` (baştan şarjlı), `ChargeReady` (Cavalry && timer ≥ 4s), `ChargeMultiplier = 2.5f`
- `CombatSystem.Tick`: Cavalry `UnitState.Attacking` değilken `chargeTimer += dt`; ilk vuruşta `effectiveDmg *= 2.5f` + `chargeTimer = 0`
- **Etki:** Cavalry 4s savaş dışında → ilk vuruş 8→20 hasar (tech bonusu hariç)

### Trebuchet Siege Unit
- `UnitType.Trebuchet`: dmg 35, range 15, interval 5.5s, aggro 15, IsRanged=true, `AntiStructureMultiplier = 3f`
- `UnitFactory.Trebuchet()`: ahşap çerçeve + karşı ağırlık + sapan + tekerlekler (prosedürel mesh), hp=150, moveSpeed=1.8
- Castle'dan **[S]** eğitilir (200O 100A / 40s); binaya 35×3=105 hasar, birliğe 35 hasar

**Doğrulandı: 0 error/0 warning, 4 base sahne kuruldu.**

---

## Oturum 6 (2026-06-02) — Çağ İlerleme & Tech Tree ✅

**3 çağ:** Karanlık → Derebeylik → Kale. İleri bina/birimler çağa kilitli.

- **`TechState.cs`** — per-team `Age` + `HashSet<TechType>`; bonus erişimcileri + `Version` sayacı
- **`TechDefs.cs`** — statik tech/çağ tablosu; `ForBuilding(type, age, tech)` helper
- **`ResearchSystem.cs`** — per-building araştırma kuyruğu (`TrainingQueue` aynası); `Apply(tech, teamId)` static helper

| Tech | Bina | Çağ | Maliyet | Etki |
|---|---|---|---|---|
| Dövme | Barracks | Derebeylik | 150Y | Militia/Cavalry +2 saldırı |
| Oklama | ArcheryRange | Derebeylik | 100Y 50A | Archer +1 saldırı, +0.5 menzil |
| Çift Balta | LumberCamp | Derebeylik | 100Y | +%25 odun toplama |
| El Arabası | TownCenter | Derebeylik | 150Y 50O | +%20 tüm toplama |
| Pul Zırh | Barracks | Kale | 150Y 100A | Militia/Cavalry +20 hp |
| Soyağacı | Stable | Kale | 150Y 100A | Cavalry +20 hp |
| İğne Ucu | ArcheryRange | Kale | 150Y 100A | Archer +1 saldırı |

Çağ atlama: Derebeylik 400Y/25s, Kale 600Y+200A/35s (TC'de **1/2** tuşu).

**Doğrulandı: Forging dmg 5→7; ScaleMail hp 40→60; ArcheryRange Feudal'da açılıyor.**

---

## Oturum 5 (2026-06-02) — Market + Castle + Farm Renewable ✅

- **Market** [K, 175O, 350hp] — sabit kur: 100 kaynak → 70 altın / 100 altın → 100 yiyecek. Hotkey 1/2/3/4.
- **Castle** [E, 650T, 2000hp, +10 pop] — otomatik ok (range 9, dmg 18, interval 1.5s). `BuildingCombatSystem.cs` yeni.
- **Farm Renewable** — food 0 + gatherer 0 → 60 wood düşer, `maxAmount`'a dolar; afford edemezse boş kalır.

---

## Oturum 4 (2026-06-02) — AI Ekonomisi ✅

- `GameManager.teamRes[4]` — per-team kaynak; `resources => teamRes[0]` alias
- Her enemy base'e 3 villager (TC arkası, gather'a hazır)
- `EnemyAI`: wood→food→gold öncelikli gather; `TryTrainVillager` (<3 villager); militia 60Y / archer 35O+25A

---

## Oturum 3 (2026-06-01) — Denge + Kazan/Kaybet ✅

- AI tuning: SpawnInterval=15s, ArmyCap=12, RushThreshold=8, ilk gecikme=15s
- `MatchSystem.cs` — TC taraması 1s; team0 TC yok → YENİLDİN; tüm düşman TC yok → ZAFER; R → Restart
- `HUD.ShowGameOver(bool)` — tam ekran overlay

---

## Oturum 1–2 (2026-06-01) — Temel + Ekonomi ✅

- MCP fix, input fix, `runInBackground=true`
- BuildingDefs, tüm unit tipleri, TrainingQueue, PopCap, Projectile, ranged combat
- BuildSystem, BuildingPlacement, GatherSystem drop-off kampları, Farm food node

---

## Mevcut C# Scriptler (`Assets/Scripts/`)

| Dosya | Durum | Görev |
|---|---|---|
| `GameBootstrap.cs` | güncellendi O8 | Boot + Restart + GameEvents.Reset |
| `GameManager.cs` | güncellendi O10 | Merkez hub; teamRes[4] + teamTech[4] + vfx + cameraRig + **fow** |
| `WorldRoot.cs` | güncellendi O10 | 4 base, NavMesh, tüm sistem init; `_groundRenderer` kaydedilir; FoW wired |
| `FogOfWarSystem.cs` | **yeni O10** | 128×128 CPU FoW; sight circle boya; düşman renderer toggle (0.5s) |
| `Assets/Shaders/FogOfWar.shader` | **yeni O10** | Built-in RP surface shader (`noambient`); fog texture world-UV |
| `GameEvents.cs` | **yeni O8** | Statik event hub; OnUnitKilled, OnBuildingDestroyed, OnAgeAdvanced, OnResearchCompleted |
| `VisualEffectSystem.cs` | **yeni O8** | Particle burst + kamera shake (GameEvents listener) |
| `IsometricCameraRig.cs` | güncellendi O8 | WASD/zoom/rotate + Shake() |
| `BuildSystem.cs` | güncellendi O8 | İnşaat sırasında Y scale lerp |
| `HUD.cs` | güncellendi O11 | **AoE alt komut barı** (tıklanabilir buton ızgarası); EventSystem+GraphicRaycaster+SafeBaseInput kurar; çağ popup; OnAgeAdvanced reactive |
| `SafeBaseInput.cs` | **yeni O11** | BaseInput türevi; eksik InputManager ekseni exception'ını yutar (StandaloneInputModule.inputOverride) |
| `MatchSystem.cs` | stabil | TC taraması; ZAFER/YENİLDİN; R → Restart |
| `TechState.cs` | stabil | Per-team çağ + araştırma seti + stat bonus erişimcileri |
| `TechDefs.cs` | stabil | Statik tech/çağ tablosu |
| `ResearchSystem.cs` | stabil | Per-building araştırma kuyruğu; Apply(tech, teamId) |
| `MarketSystem.cs` | stabil | Kaynak takası |
| `BuildingCombatSystem.cs` | stabil | Bina otomatik ateşi (Castle) |
| `BuildingDefs.cs` | stabil | Tüm bina tanımları |
| `BuildingFactory.cs` | stabil | Prosedürel bina mesh'leri |
| `BuildingEntity.cs` | stabil | IDamageable; GetTrainables (çağ filtreli); GetResearchables |
| `BuildingPlacement.cs` | güncellendi O11 | Ghost önizleme; çağ kilitli bina reddi; köylü build menüsü HUD barına taşındı (OnGUI hint kaldırıldı) |
| `EnemyAI.cs` | güncellendi O9 | Ekonomi + çağ atlama + kişilik (Rusher/Boomer/Balanced) + rally→attack→retreat ordu koordinasyonu |
| `TrainingQueue.cs` | stabil | Per-building üretim kuyruğu |
| `UnitEntity.cs` | stabil | State machine; cavalry charge; IDamageable |
| `UnitFactory.cs` | stabil | Villager/Militia/Archer/Cavalry/Trebuchet mesh |
| `CombatSystem.cs` | stabil | Melee + ranged + charge timer + anti-structure |
| `GatherSystem.cs` | stabil | NearestDropoff; GatherMult; deposit |
| `CommandSystem.cs` | güncellendi O11 | Tüm hotkey'ler + sağ-tık UI pointer guard |
| `SelectionSystem.cs` | güncellendi O11 | Sol tık, drag-box, Shift toggle + UI pointer guard |
| `ResourceManager.cs` | stabil | food/wood/gold/stone + pop/popCap + OnChanged |
| `ResourceNode.cs` | stabil | Kaynak düğümü; renewable (Farm) |
| `ResourceFactory.cs` | stabil | Tree/GoldMine/StoneMine/FarmField |
| `Projectile.cs` | stabil | Homing mermi (speed=22 u/s) |
| `IDamageable.cs` | stabil | Ortak hasar arayüzü |
| `MinimapSystem.cs` | stabil | RenderTexture minimap |
| `SelectionRing.cs` | stabil | LineRenderer seçim halkası |
| `Prims.cs` | stabil | Prosedürel mesh + materyal yardımcıları |

**`Assets/Editor/`:**
| `McpForceDirectConnections.cs` | **kalıcı — silme!** | MCP direct cap = 8 (yoksa bağlantı kopar) |

---

## Sahne İçeriği (Play'e basınca kurulur)

- 120×120 yeşil zemin + NavMesh (runtime baked, flat)
- **4 base** elmas: güney(team0/mavi), kuzey(kırmızı), batı(yeşil), doğu(sarı)
- Her base: sur + 4 kule + kapı + TC(600hp) + 4 House + Barracks
- 80 ağaçlık orman halkası, 2 GoldMine, 2 StoneMine (harita merkezi)
- **Team 0 başlangıç:** 3 Villager + 2 Militia (TC önü) / Food 200 / Wood 200 / Gold 100 / Stone 0
- **Team 1-3 başlangıç:** 3 Militia + 3 Villager (TC arkası, gather'a hazır)

---

## Kontroller

| Eylem | Tuş |
|---|---|
| Birim/bina seç | Sol tık (Shift=toggle, drag-box) |
| Hareket / Saldır / Topla | Sağ tık |
| Bina inşa (villager seçili) | H=House, B=Barracks, R=ArcheryRange*, T=Stable**, F=Farm, L=LumberCamp, G=MiningCamp, I=Mill, K=Market, E=Castle** |
| Birim eğit (bina seçili) | V=Villager, M=Militia, A=Archer*, C=Cavalry**, S=Trebuchet** |
| Araştır / Çağ atla (bina seçili) | **1..N** (bina panelinde listelenir) |
| Market takas | 1=Yiy sat, 2=Odu sat, 3=Taş sat, 4=Yiy al |
| Kamera | WASD/ok pan, tekerlek zoom, Q/E döndür |
| Yeniden başlat (oyun sonu) | R |

\* Derebeylik Çağı &nbsp; \*\* Kale Çağı

---

## Önemli Teknik Notlar

- **`activeInputHandler: 2`** (`ProjectSettings.asset`) — -1 olursa `UnityEngine.Input` patlar; **değiştirme**.
- **`Packages/manifest.json` → `com.unity.modules.particlesystem`** — O11'de eklendi; **silme**, yoksa
  `VisualEffectSystem.cs` (ParticleSystem) derlenmez ve tüm Assembly-CSharp fail eder.
- **HUD runtime'da `EventSystem` + `StandaloneInputModule` + `SafeBaseInput` kurar.** `InputManager.asset`
  yalnızca Horizontal/Vertical/Mouse ScrollWheel tanımlar (Submit/Cancel yok); `SafeBaseInput`
  `inputOverride` olarak bağlı olmazsa modül her frame `ArgumentException` fırlatır. **SafeBaseInput'u silme.**
- **`McpForceDirectConnections.cs`** — **silme**, MCP bağlantısı kopar.
- **NavMesh runtime baked** — `NavMeshBuilder` low-level API, ek paket gerektirmez.
- **`Prims.Spawn()`** collider'ları siler; unit'lere `CapsuleCollider`, binalara `BoxCollider` ayrıca eklenir.
- **`HUD.cs`** — `text.font = null` (Unity 6; LegacyRuntime.ttf çalışmaz).
- **`ResourceManager.stone = 0`** başlar.
- **`teamRes[4]` / `teamTech[4]`:** index 0 = oyuncu, 1-3 = düşman. `resources` / `tech` property'leri index-0 alias'ı.
- **Tech bonusları canlı okunur:** `AttackDamage/AttackRange` her swing'de `TeamTech` erişimcilerini çağırır.
- **Restart:** `GameEvents.Reset()` → `WorldRoot` yeniden kurulur → `GameManager` + `teamTech` fresh Dark Çağ.
- **`GameEvents` stale closure riski:** `OnEnable`/`OnDisable` veya `Init`/`OnDestroy` çiftleriyle subscribe/unsubscribe — `Reset()` Restart'ta tüm subscriber'ları temizler.
- **Derleme standardı: 0 error, 0 warning — koruyun.**

---

## Yapılacaklar (Sıradaki Fazlar)

Detaylı yol haritası: `~/.claude/plans/bunlar-n-hepsini-inceleyip-kendimize-pure-lecun.md`

| Faz | Hedef | Durum |
|---|---|---|
| ~~Faz 1 — Event System~~ | GameEvents hub | ✅ Oturum 8 |
| ~~Faz 3 — Görsel Cila~~ | Particle, shake, inşaat anim, çağ popup | ✅ Oturum 8 |
| ~~Faz 2 — AI Koordinasyon~~ | Rally point, Personality enum (Rusher/Boomer/Balanced), retreat döngüsü | ✅ Oturum 9 |
| ~~Faz 4 — Fog of War~~ | CPU Texture2D FoW, ground shader, düşman görünürlük toggle | ✅ Oturum 10 |
| ~~Faz 5 — Yeni Mekanikler~~ | Scout, Medic, Duvar/Kapı, Relic (sadece gelir; zafer koşulu yok) | ✅ Oturum 12 (kod) + 13 (doğrulama) |
| Faz 6 — Multiplayer | Deterministic lockstep altyapısı | Sıradaki |

---

## ✅ Doğrulama: Oturum 13'te MCP ile TAMAMLANDI

**Derleme + runtime O13'te MCP ile teyit edildi: 0 error / 0 warning** (BuildingFactory CS0102 düzeltildikten
sonra). Submit spam gitti (SafeBaseInput çalışıyor), `Custom/FogOfWar` render ediyor, Relic sistemi canlı,
sahne 22 birim/24 bina ile kuruluyor, komut barı butonları çalışıyor. Aşağıdaki tarihsel kontrol listesi
referans için bırakıldı.

**Kalan tek doğrulama — insan tarafından oyun-hissi testi** (henüz elle oynanmadı): 1) Barracks→S gözcü
hızlı/hasarsız + sis çok açılıyor mu; 2) Castle→H Medic yaralı dostu iyileştiriyor mu; 3) Köylü→W duvar
birimi gerçekten dolaştırıyor (carving), O→kapı geçirgen mi; 4) Relic'e birim götürünce ele geçiriliyor +
altın artıyor mu; 5) AI rally→attack→retreat sahada doğru mu.

Yeni oturuma girilince (Unity'yi Claude'dan ÖNCE aç) önce şunu çalıştır:

```
mcp__unity__get_console_logs (type: Error)
```

Hata varsa düzelt, temizse devam et. Özellikle kontrol edilmesi gerekenler:

| # | Kontrol | Neden riskli |
|---|---|---|
| 1 | `Custom/FogOfWar` shader bulunuyor mu? | `Shader.Find` yalnızca import edilmiş shader'ı bulur; editörde `Assets/Shaders/` görünmeli |
| 2 | `#pragma surface surf Lambert noambient` | `noambient` Unity 6000.4'te geçerli; zaten önceki Unity sürümlerinde de var ama teyit et |
| 3 | `EnemyAI.cs` — `UnitType?` nullable, `or` pattern | C# 9 özelliği; daha önce projede kullanılıyordu, sorun yok ama doğrula |
| 4 | `FogOfWarSystem` — `PixPerUnit` static init `const float / const float` | C# const float bölmesi derleme-zamanı sabit: geçerli |
| 5 | `WorldRoot._groundRenderer` — `SetupGround` → `Build()` sırası | `SetupGround` `Build()` içinde `SetupGameplay`'den önce çağrılıyor; field doğru doldurulmuş olmalı |

---

## Unity MCP Durumu

- Relay: `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64`
- `claude mcp list` → `unity: ✓ Connected`
- **⚠️ Unity claude'dan önce açık olmalı** — sonradan bağlanan MCP o session'a yüklenemiyor.

---

## Yeni Oturumda Başlangıç Promptu

```
Age of Arena Unity portuna devam.
Proje: /Users/emreaydin/ageofarena/AgeOfArenaUnity/
Unity'yi BU prompttan ÖNCE aç (yoksa MCP tool'ları yüklenmiyor).
HANDOFF.md oku. Derleme 0 error (Editor.log ile teyitli); O9/O10/O11 RUNTIME doğrulaması bekliyor.
Önce Play'e bas + console'u kontrol et: O11 Submit spam gitti mi, komut barı çalışıyor mu, FoW render ediyor mu.
```
