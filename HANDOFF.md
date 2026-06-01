# Age of Arena — Unity Port Handoff

## Proje
`/Users/emreaydin/ageofarena/AgeOfArenaUnity/` — Unity 6000.4.1f1, Built-in Render Pipeline.
Three.js web sürümü **kaldırıldı** (git geçmişinde mevcut). Bu repo artık tamamen Unity.
GitHub'daki Unity RTS klonları (FloaterTS/RTSUnityGameLicenta, nefrob/unity-rts) **yalnızca mimari referans** — lisans yok, kod kopyalanmadı.

---

## Oturum 1 (2026-06-01) — Temel + 4 Faz

### Altyapı / Ortam Düzeltmeleri
- **Unity MCP bağlantısı çözüldü** — `Assets/Editor/McpForceDirectConnections.cs` reflection ile `ConnectionCensus.MaxDirect` kapasitesini 0→8 zorluyor. Silme.
- **Input bug düzeltildi** — `ProjectSettings.asset` `activeInputHandler: -1 → 2` (Both).
- **`Application.runInBackground = true`** — `WorldRoot.Build()`'e eklendi.

### Faz A — Yeni bina/birim tipleri + dinamik popCap
- `BuildingDefs.cs` (yeni) — merkezi bina veri tablosu.
- `GameTypes.cs` — `UnitType` += Archer, Cavalry; `BuildingType` += ArcheryRange, Stable, Farm; `UnitState` += Constructing.
- `UnitEntity.cs` — tip-bazlı combat stats, `BuildOrder()`, `constructTarget`.
- `UnitFactory.cs` — `Archer()`, `Cavalry()` mesh.
- `BuildingFactory.cs` — ArcheryRange/Stable/Farm mesh; `NewBuilding()` helper; `Create()` dispatcher.
- `BuildingEntity.cs` — underConstruction/buildProgress/buildTime; `GetTrainables()`.
- `GameManager.cs` — `RecomputePop()`; null compaction.
- `TrainingQueue.cs` — popCap kontrolü; yeni birim spawn.

### Faz B — Menzilli savaş
- `Projectile.cs` (yeni) — ok/bolt Lerp+hasar. Speed=22 u/s.
- `CombatSystem.cs` — `IsRanged` ise Projectile.Spawn, değilse anlık hasar.

### Faz C — Bina inşası
- `BuildSystem.cs` (yeni) — builder villager döngüsü; çok builder hızlandırır.
- `BuildingPlacement.cs` (yeni) — hayalet mesh + CheckBox + villager BuildOrder.
- `CommandSystem.cs` — `HandleBuildHotkeys()`.
- **Hotkey'ler (villager seçili):** `H`=House, `B`=Barracks, `R`=ArcheryRange, `T`=Stable, `F`=Farm.

### Faz D — Düşman AI
- `EnemyAI.cs` (yeni) — SpawnInterval=8s, ArmyCap=8, RushThreshold=5.
- `WorldRoot.cs` — 4 base EnemyAI init.

---

## Oturum 3 (2026-06-01) — Denge/Tuning + Kazan/Kaybet ✅

### AI Tuning
- **`EnemyAI.cs`** — 4 değer güncellendi:
  - `SpawnInterval = 8f` → `15f`
  - `ArmyCap = 8` → `12`
  - `RushThreshold = 5` → `8`
  - `_spawnTimer = 4f` → `15f` (ilk spawn gecikmesi)
- Etki: ilk düşman birimi ~15s sonra çıkar, ordu yavaş büyür, rush 8 birime ulaşınca başlar.

### popCap "DOLU" uyarısı
- **`HUD.cs`** `Refresh()` — `pop >= popCap` iken `_popText.text = "X/Y DOLU"`, renk kırmızı `0xff5555`; dolmadığında beyaza döner.

### Kazan/Kaybet + R ile yeniden başlat
- **`MatchSystem.cs`** (yeni) — 1s'de bir TC taraması; team0 TC yoksa → yenilgi, hiç düşman TC yoksa → zafer. `Time.timeScale=0`, `HUD.ShowGameOver()` çağrısı. `_over=true` iken R → `GameBootstrap.Restart()`.
- **`HUD.cs`** — `_canvasRoot` alanı eklendi; `ShowGameOver(bool)` metodu: tam ekran yarı-saydam overlay, büyük başlık (ZAFER!/YENİLDİN), "R ile yeniden başlat" ipucu.
- **`GameBootstrap.cs`** — `Restart()` static metodu: timeScale=1, eski WorldRoot destroy, NavMesh temizle, `RebuildKick` (bir sonraki Update'te `BuildIfNeeded()` → kendi kendini siler).
- **`GameManager.cs`** — `public MatchSystem match;` alanı eklendi.
- **`WorldRoot.cs`** — `SetupGameManager()` içine `gm.match = AddComponent<MatchSystem>()` eklendi.
- **Doğrulandı (MCP):** 0 error derleme; runtime: düşman TC'leri yıkılınca `Time.timeScale=0`, enemy TC=0.

---

## Oturum 2 (2026-06-01) — Ekonomi Overhaul

FloaterTS/RTSUnityGameLicenta referans repo incelendi (Worker/ResourceCamp/ResourceField deseni adaptasyonu).

### Faz E — Drop-off Kampları
**Problem:** `GM.dropoffs` tek Vector3 (TC'nin konumu, 0,0,-40). Merkezdeki madenler ~40 birim gidiş-dönüş yapıyordu.
**Çözüm:** Drop-off'u kind+team-aware bina sistemine çevir.

- **`GameTypes.cs`** — `BuildingType` += `LumberCamp, MiningCamp, Mill`.
- **`BuildingDefs.cs`** — `BuildingDef`'e `isDropoff` + `dropoffMask` (bit per ResourceKind). Yeni satırlar:
  - `TownCenter`: isDropoff=true, mask=tümü (garantili fallback)
  - `LumberCamp` [L] (50w, 10s, 150hp) → Wood only
  - `MiningCamp` [G] (50w, 10s, 150hp) → Gold+Stone
  - `Mill` [I] (60w, 12s, 150hp) → Food only
  - Helper: `BuildingDefs.AcceptsDropoff(type, kind)`, `IsDropoff(type)`.
- **`BuildingFactory.cs`** — `LumberCamp()`, `MiningCamp()`, `Mill()` prosedürel mesh + `Create()` case'leri.
- **`GatherSystem.cs`** — `NearestDropoff(pos, kind, teamId)` → `BuildingEntity` döndürür; team0 tamamlanmış binalar arasında en yakın kind-uyumlu olanı seçer. `BeginReturn` ve deposit kolu güncellendi.
- **`GameManager.cs`** — `dropoffs` listesi kaldırıldı; `nodes.RemoveAll(n=>n==null)` compaction eklendi.
- **`WorldRoot.cs`** — `gm.dropoffs.Add(tcPos)` satırı silindi (TC artık bina olarak drop-off).
- **Doğrulandı:** wood 200→340 (~20s); LumberCamp ağaca ~3.5 birim, TC'ye ~15 birim → kamp tercih edildi.

### Faz F — Farm Food Üretimi
**Problem:** Food'un hiç kaynağı yoktu (200 başlayıp sadece harcanıyordu).

- **`ResourceNode.cs`** — `destroyOnDeplete=true` flag eklendi; `Update()`: boş + gatherer=0 → `Destroy(gameObject)`. Farm node'ları `destroyOnDeplete=false`.
- **`ResourceFactory.cs`** — `FarmField(farmRoot, amount=300)` helper: Farm GO'ya Food ResourceNode ekler, ikinci collider yok.
- **`BuildSystem.cs`** — `buildProgress>=1f` kolunda: `Farm` tamamlanınca `ResourceFactory.FarmField(site.gameObject)` + `GM.RegisterNode`. Villager Farm'a sağ-tık → food toplar. Mill yanında kısa tur.

---

## Mevcut C# Scriptler (`Assets/Scripts/`)

| Dosya | Durum | Görev |
|---|---|---|
| `GameBootstrap.cs` | **güncellendi** | `Boot()` + `Restart()` + `RebuildKick` (yerinde rebuild) |
| `MatchSystem.cs` | **yeni** | Kazan/kaybet arbiter; `End()` → timeScale=0 + HUD overlay; R → Restart |
| `WorldRoot.cs` | **güncellendi** | 4 base, NavMesh, sistemler, garrison, EnemyAI |
| `IsometricCameraRig.cs` | eski | WASD pan, scroll zoom, Q/E rotate |
| `BuildingDefs.cs` | **güncellendi** | +isDropoff/dropoffMask; +LumberCamp/MiningCamp/Mill def; +AcceptsDropoff helper |
| `BuildingFactory.cs` | **güncellendi** | +LumberCamp/MiningCamp/Mill mesh; +Create cases |
| `BuildingEntity.cs` | **güncellendi** | IDamageable + hp + underConstruction/buildProgress/buildTime |
| `BuildingPlacement.cs` | **yeni** | Hayalet önizleme + yerleştirme + OnGUI ipuçları |
| `BuildSystem.cs` | **güncellendi** | +Farm tamamlanınca FarmField node kaydı |
| `EnemyAI.cs` | **güncellendi** | SpawnInterval=15s, ArmyCap=12, RushThreshold=8, ilk gecikme=15s |
| `Prims.cs` | eski | Prosedürel mesh helper'ları |
| `ResourceFactory.cs` | **güncellendi** | +FarmField(farmRoot) helper |
| `ResourceManager.cs` | eski | food/wood/gold/stone + pop/popCap + CanAfford/Deduct |
| `ResourceNode.cs` | **güncellendi** | +destroyOnDeplete flag; +Update() depletion-destroy |
| `GameTypes.cs` | **güncellendi** | +LumberCamp, MiningCamp, Mill BuildingType |
| `GameManager.cs` | **güncellendi** | dropoffs kaldırıldı; +nodes compaction; +match alanı |
| `SelectionSystem.cs` | **güncellendi** | placement guard |
| `CommandSystem.cs` | **güncellendi** | HandleBuildHotkeys(); placement guard |
| `CombatSystem.cs` | **güncellendi** | +ranged dal (Projectile.Spawn) |
| `Projectile.cs` | **yeni** | Homing mermi MonoBehaviour |
| `IDamageable.cs` | eski | Birim/bina için ortak saldırı arayüzü |
| `UnitEntity.cs` | **güncellendi** | +Archer/Cavalry stats; +BuildOrder; +constructTarget |
| `UnitFactory.cs` | **güncellendi** | +Archer(), +Cavalry() |
| `GatherSystem.cs` | **güncellendi** | +kind/team-aware NearestDropoff (bina tabanlı) |
| `TrainingQueue.cs` | **güncellendi** | +pop >= popCap kontrolü; +Archer/Cavalry spawn |
| `SelectionRing.cs` | eski | LineRenderer seçim halkası |
| `HUD.cs` | **güncellendi** | +popCap DOLU uyarısı (kırmızı); +ShowGameOver overlay; +_canvasRoot |
| `MinimapSystem.cs` | eski | RenderTexture minimap + birim noktaları |

**`Assets/Editor/`:**
| `McpForceDirectConnections.cs` | **kalıcı** | MCP direct connection cap fix |

---

## Sahne İçeriği (Play'e basınca kurulur)
- 120×120 yeşil zemin + NavMesh (runtime baked, flat)
- **4 base** elmas: güney(team0/mavi), kuzey(team1/kırmızı), batı(team2/yeşil), doğu(team3/sarı)
- Her base: sur + 4 kule + kapı + TC(600hp) + 4 House(300hp×4) + Barracks(400hp)
- 80 ağaçlık orman halkası, 2 GoldMine, 2 StoneMine
- **3 Villager + 2 Militia** (team 0, TC önünde)
- **HUD:** Food 200 / Wood 200 / Gold 100 / Stone 0 / Pop 5/25

---

## Gameplay — Dikey Dilim Özeti
- **Sol tık** unit/bina → seçim (Shift=toggle, drag-box mevcut)
- **Sağ tık zemin** → formasyon hareket
- **Sağ tık kaynak / Farm** → gather (villager)
- **Sağ tık düşman** → attack order
- **Villager seçili + H/B/R/T/F/L/G/I** → placement modu (sol tık koy, sağ tık/Esc iptal)
  - `H`=House, `B`=Barracks, `R`=ArcheryRange, `T`=Stable, `F`=Farm
  - `L`=LumberCamp (ormana yakın kur → wood kısa tur)
  - `G`=MiningCamp (madene yakın kur → gold/stone kısa tur)
  - `I`=Mill (Farm yanına kur → food kısa tur)
- **Bina seçili + V/M/A/C** → eğitim kuyruğu
- **Düşman AI:** SpawnInterval=15s, RushThreshold=8, ArmyCap=12, ilk gecikme=15s
- **Oyun sonu:** tüm düşman TC yıkılır → `ZAFER!`; oyuncu TC yıkılır → `YENİLDİN`; oyun dondurulur, `R` ile yeniden başlar
- **Combat:** melee + menzilli, HP barları, auto-aggro
- **runInBackground=true** — alt-tab'da oyun sürer

---

## Önemli Teknik Notlar
- **NavMesh runtime baked** (`NavMeshBuilder` low-level API, paket gerektirmez).
- **`activeInputHandler: 2`** (`ProjectSettings.asset`) — -1 olursa input patlar.
- **McpForceDirectConnections.cs** — silme, MCP çalışmaz.
- `Prims.Spawn()` collider'ları siliyor; unit'lere `CapsuleCollider`, binalara `BoxCollider` ayrıca ekleniyor.
- `HUD.cs` — `text.font = null` (Unity 6; LegacyRuntime.ttf çalışmaz).
- `ResourceManager.stone = 0` (oyuncu kararı).
- **Drop-off sistemi:** `BuildingDefs.dropoffMask` bit-field (bit i = ResourceKind i). TC mask=tüm bitler → fallback. Kamp yoksa TC'ye gider, TC da yoksa (savaşta yıkıldı) villager durur.
- **Farm node:** `destroyOnDeplete=false`; bina yıkılmadan food node sıfırlanabilir ama bina kalır. Yeniden dolum için yeni Farm inşası gerekir (renewable mekanizması eklenmedi).
- Derleme: **0 error, 0 warning**.

---

## Yapılacaklar (öncelik sırasıyla)

### ~~1. Denge / Tuning~~ ✅ TAMAMLANDI (Oturum 3)
- AI tuning yapıldı (SpawnInterval=15s, ArmyCap=12, RushThreshold=8, ilk gecikme=15s)
- popCap DOLU uyarısı HUD'a eklendi
- Kazan/kaybet + R restart sistemi eklendi

### ~~2. AI Ekonomisi~~ ✅ TAMAMLANDI (Oturum 4)
- `GameManager.teamRes[4]` array — her takım kendi `ResourceManager`'ına sahip; `resources` property `teamRes[0]` alias'ı (geriye dönük uyumlu)
- `GatherSystem` deposit artık `teamRes[v.teamId].Gain()` çağırıyor (team-aware)
- `WorldRoot.SpawnGarrison` her enemy base'e 3 villager ekliyor (arkada, gather'a hazır)
- `EnemyAI` tam ekonomi döngüsü:
  - Militia 60 food / Archer 35 wood+25 gold kaynak kesintisiyle üretiliyor
  - `AssignVillagersToGather()`: idle villagerlar wood→food→gold sırasıyla nearest node'a assign
  - `TryTrainVillager()`: villager sayısı <3 ve yeterli food varsa yeni villager üretiliyor
  - `_gatherTimer` her 6s'de bir economy tick

### 3. popCap & Daha Fazla Bina
- Market (kaynak takası), Castle (yüksek hp savunma)
- Farm food: `renewable` mekanizması (belli sürede yeniden dolum)

### 4. Savaş İyileştirmeleri
- Cavalry charge bonus (ilk vuruş hasar çarpanı)
- Siege weaponry (Trebuchet, uzun menzil bina hasarı)

### 5. Görsel İyileştirmeler
- Post-processing (Built-in RP: PostProcessLayer paketi veya URP geçiş)
- GLTF/FBX asset import ile prosedürel mesh değişimi
- İnşa iskele animasyonu (buildProgress görsel)

---

## Unity MCP Durumu (güncel — çalışıyor)
- Relay binary: `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64`
- `claude mcp list` → `unity: ✓ Connected`
- `McpForceDirectConnections.cs` → her açılışta direct cap=8 zorlanır
- Araçlar: `Unity_GetConsoleLogs`, `Unity_RunCommand`, `Unity_Camera_Capture`, `Unity_SceneView_Capture2DScene`
- Zaman-bazlı test için: `osascript -e 'tell application "Unity" to activate'`

---

## Yeni Oturumda Başlangıç Promptu
```
Age of Arena Unity portuna devam.
Proje: /Users/emreaydin/ageofarena/AgeOfArenaUnity/
HANDOFF.md oku. Unity MCP bağlı ve 0 error derleniyor.
[ne yapmak istiyorsun]
```
