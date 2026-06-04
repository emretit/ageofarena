# AI & Zorluk — AoA Wiki

> Düşman takımları, üç **AIPersonality** (Balanced / Rusher / Boomer) ile ayarlanan
> tek bir beyinden (`EnemyAI`) sürülür; bunun üzerine küresel bir **Difficulty**
> (Easy / Moderate / Normal / Hard / Insane / Extreme — **6 seviye**) çarpan katmanı
> oturur. Personality "nasıl" oynanacağını (ekonomi mi baskı mı), Difficulty ise
> "ne kadar sert" oynanacağını belirler. **AI bedava kaynak hilesi yapmaz** — sadece
> toplama/araştırma hızı bir eko çarpanıyla (`_ecoMult`) ölçeklenir. AI basitleştirilmiş
> bir ekonomi çalıştırır, ordusunu bir rally noktasında toplar, hedefe topluca yürür
> ve kayıp eşiğini aşınca regroup için geri çekilir.
>
> **Kod kaynağı:** [EnemyAI.cs](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs),
> [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs)

---

## 1. Ne olduğu

Her düşman takımının kendi `EnemyAI` bileşeni vardır ve `GameManager.teamRes`
içinde kendi `ResourceManager` slotunu kullanır. AI iki paralel döngü çalıştırır:

- **Ekonomi döngüsü:** Villager üretip kaynak toplatır, `villagerTarget` sayısına
  kadar köylü hedefler. Toplama/araştırma hızı difficulty'nin `_ecoMult` çarpanıyla
  ölçeklenir (AICH).
- **Askeri döngü:** Birim üretir, ordusunu bir **Stance** durum makinesi
  (Gathering → Rallying → Attacking → Retreating) üzerinden yönetir; tek tek
  birim göndermek yerine orduyu toplayıp topluca saldırtır.

İki ayar ekseni:

- **AIPersonality** (`GameTypes.cs:69`): `Balanced`, `Rusher`, `Boomer`. Ordu
  boyutu, baskı zamanlaması ve ekonomi ağırlığını belirleyen baz değerleri kurar
  (`ApplyPersonality`). Ayrıca **birim karışımı profili** (AISC) de personality'den
  gelir (`AIProfile.For`, [EnemyAI.cs:21](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L21)).
- **Difficulty** (`GameTypes.cs:77`): `Easy`, `Moderate`, `Normal`, `Hard`,
  `Insane`, `Extreme`. Bu baz değerleri çarpanlarla ölçekler ve `_ecoMult`'u
  yayımlar (`ApplyDifficulty`). Varsayılan `Normal`'da hiçbir değişiklik uygulanmaz
  (`_ecoMult = 1`), yani personality baz değerleri = efektif değerler.

`Init()` sırası: önce `ApplyPersonality`, sonra `ApplyDifficulty`
([EnemyAI.cs:108-109](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L108)).
Init eko çarpanını `gm.teamEcoMult[teamId]`'a yazar
([EnemyAI.cs:110-113](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L110)) — buradan
GatherSystem/ResearchSystem okur. Oyuncu oyun ortasında zorluğu değiştirirse HUD
`SetDifficulty()` çağırır ve aynı sırayla yeniden türetilip eko çarpanı yeniden
yayımlanır ([EnemyAI.cs:118-125](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L118)).

---

## 2. Nasıl çalışır (mekanik + formül)

### Personality baz değerleri

`ApplyPersonality` beş ana knob'u ayarlar
([EnemyAI.cs:178-198](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L178)):

- `_spawnInterval` — birim üretim aralığı (saniye); düşük = daha hızlı üretim.
- `_armyCap` — saldırıya başlamadan önceki tavan ordu büyüklüğü.
- `_rushThreshold` — saldırıyı tetikleyen minimum ordu büyüklüğü.
- `_villagerTarget` — hedeflenen köylü sayısı (ekonomi ağırlığı).
- `_retreatLoss` — saldırı gücünün kaçta kaçı kaybedilince geri çekilineceği.

Ayrıca personality başlangıç zamanlayıcılarını da kurar (`_spawnTimer`, `_techTimer`)
ve birim karışımı profilini (AISC) belirler.

### Difficulty çarpanları

`ApplyDifficulty` personality baz değerinin üzerine uygular
([EnemyAI.cs:133-175](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L133)). Altı
seviye monoton olarak sertleşir:

| Difficulty | spawnInterval | armyCap | rushThreshold | villagerTarget | ecoMult |
|---|---|---|---|---|---|
| Easy | `× 2.0` | `× 0.50` (min 3) | `× 1.5` | `− 2` (min 1) | **0.65** |
| Moderate | `× 1.4` | `× 0.75` (min 4) | `× 1.2` | `− 1` (min 1) | **0.85** |
| Normal | değişmez | değişmez | değişmez | değişmez | **1.00** |
| Hard | `× 0.80` | `× 1.30` | `× 0.85` (min 3) | `+ 2` | **1.15** |
| Insane | `× 0.60` | `× 1.65` | `× 0.72` (min 3) | `+ 4` | **1.35** |
| Extreme | `× 0.42` | `× 2.10` | `× 0.55` (min 2) | `+ 6` | **1.60** |

Kaynak: Easy [L143](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L143),
Moderate [L149](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L149),
Normal (baseline, no-op),
Hard [L156](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L156),
Insane [L162](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L162),
Extreme [L168](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L168).

### AICH — eko çarpanı (bedava kaynak YOK)

Difficulty **kaynak hilesi yapmaz**. Bunun yerine `_ecoMult`
([EnemyAI.cs:131](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L131)) bir takım
ölçek faktörüdür: `gm.teamEcoMult[teamId]`'a yazılır ve GatherSystem/ResearchSystem
**toplama ve araştırma hızını** buna göre ölçekler. Yani harder AI aynı köylülerle
daha hızlı kaynak biriktirir, ama ona hediye kaynak verilmez. Easy 0.65× (yavaş
toplar) → Extreme 1.60× (1.6 kat hızlı toplar/araştırır).

### AIRD — round-half-up (banker's rounding değil)

Tüm tam sayı sonuçları lokal `Round` ile yuvarlanır:
`Mathf.FloorToInt(v + 0.5f)` ([EnemyAI.cs:139](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L139)).
Bu **deterministik round-half-up**'tır — eski `Mathf.RoundToInt`'in
round-half-to-even (banker's rounding) davranışı **artık kullanılmıyor**. Örn.
`6.5 → 7`, `13.5 → 14` (ikisi de yukarı). Türetilmiş değerler artık naif
"0.5 yukarı" beklentisiyle tutarlı; eski wiki'deki Rusher×Easy rush=6 gibi
banker's-rounding sapması ortadan kalktı.

**Önemli:** `_retreatLoss`, `_spawnTimer`, `_techTimer` ve sabit cadence'lar
(assess / gather / tech) Difficulty tarafından **ölçeklenmez**; yalnızca yukarıdaki
dört alan + `_ecoMult` etkilenir.

### Sabit cadence'lar (personality/difficulty'den bağımsız)

`AssessInterval = 3s`, `GatherCheckInterval = 6s`, `TechInterval = 8s`
([EnemyAI.cs:43-45](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L43)). Başlangıç
zamanlayıcıları field initializer'lardan gelir: `_assessTimer = 2f`,
`_gatherTimer = 3f` ([EnemyAI.cs:82-83](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L82)).
`_spawnTimer` ve `_techTimer` ise personality tarafından kurulur (ilk saldırı/araştırma
baskısının zamanlaması).

### Stance makinesi (ordu koordinasyonu)

Tüm ordu tek bir stance paylaşır: **Gathering → Rallying → Attacking → Retreating**
([EnemyAI.cs:67](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L67),
sürücü `Assess` [L388](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L388)):

- **Gathering** — ordu `_rushThreshold`'a ulaşana kadar bekler, sonra hedef seçip
  rally noktasına yollar ([L447](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L447)).
- **Rallying** — `ArriveFraction = 0.7` oranında birim toplanınca veya
  `RallyTimeoutTicks = 5` (~15s) sonra commit edilir
  ([L463](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L463)).
- **Attacking** — hedefe basar; kayıp `_retreatLoss` eşiğini aşınca Retreating'e
  geçer; hedef düşerse dağılmadan re-target eder
  ([L488](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L488)).
- **Retreating** — eve döner; `%60` eve ulaşınca veya `RetreatTimeoutTicks = 6`
  (~18s) sonra yeniden Gathering'e geçer
  ([L512](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L512)).

### AISC — birim karışımı profili (personality başına)

`AIProfile` melee/archer/cavalry/siege ağırlıkları tutar
([EnemyAI.cs:10-27](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L10)) ve
`ChooseUnit` bunları olasılıksal örnekler — kompozisyon oyundan oyuna değişir ama
temaya sadık kalır:

| Profil | melee | archer | cavalry | siege | Kaynak |
|---|---|---|---|---|---|
| Rusher | 0.65 | 0.15 | 0.15 | 0.05 | [EnemyAI.cs:17](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L17) |
| Boomer | 0.25 | 0.25 | 0.30 | 0.20 | [EnemyAI.cs:18](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L18) |
| Balanced | 0.35 | 0.35 | 0.20 | 0.10 | [EnemyAI.cs:19](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L19) |

### Counter-awareness (enemyCav → Spearman)

`ChooseUnit` profil örneklemesinden **önce** sert öncelikler uygular: düşman
ordusunda çok Cavalry varsa (Feudal+) Spearman üretir — `ownSpear < enemyCav/2 + 1`
olana dek ([EnemyAI.cs:336-341](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L336)).
Ayrıca Castle+ için ~6 orduda 1 Trebuchet (kuşatma) ve ~6 orduda 1 Medic (destek)
hedefler ([L329](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L329),
[L344](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L344)).

### AIWN — Wonder/Relic countdown önceliği

Bir Wonder/Relic zafer geri sayımı işliyorsa (`MatchManager.TimeRemaining`),
`FindWinConditionTarget` düşman Wonder binasını döndürür ve ordu genel ekonomi
tacizinden vazgeçip onu yıkmaya öncelik verir
([EnemyAI.cs:755-772](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L755)).
Rallying ve Attacking, hedef düştüğünde bu kontrolü yeniden yapar
([L469](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L469),
[L503](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L503)).

### AIDP — Allied/Neutral hedeflenmez

AI yalnızca `gm.IsEnemy(_teamId, ...)` dönen takımları hedefler; müttefik ve nötr
takımlar saldırı hedef skorlamasına alınmaz ve üs tehdidi hesabına katılmaz
(`FindBestTarget` [L738](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L738),
[L746](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L746); garrison tehdit
kontrolü `CheckGarrison` [L423-424](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L423)).

### Hedef skorlama

Hedef seçimi ekonomi odaklıdır: Villager `65`, diğer birim `35`; bina değerleri
TownCenter `60`, üretim binaları `45`, ekonomi binaları `40`, diğer `25`; mesafe
cezası `dist / 8` ([EnemyAI.cs:774-791](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L774)).
Yani AI öncelikle köylüleri ve TC'yi hedefler — TC kazanma koşuludur.

---

## 3. Gerçek statlar (koddan)

### Personality baz değerleri (Normal'da efektif)

| Personality | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | retreatLoss | initSpawnTimer (s) | initTechTimer (s) | Kaynak |
|---|---|---|---|---|---|---|---|---|
| Balanced | 15 | 12 | 8 | 3 | 0.4 | 15 | 12 | [EnemyAI.cs:193](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L193) |
| Rusher | 11 | 10 | 5 | 2 | 0.6 | 8 | 16 | [EnemyAI.cs:183](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L183) |
| Boomer | 13 | 18 | 12 | 6 | 0.3 | 22 | 8 | [EnemyAI.cs:188](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L188) |

### Efektif değerler (Personality × Difficulty)

`retreatLoss` Difficulty'den etkilenmez; tablolarda personality değerini korur.
`ecoMult` difficulty'ye bağlıdır (personality'den bağımsız), sütun olarak eklendi.

**Balanced** (retreatLoss 0.4):

| Difficulty | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | ecoMult | Kaynak |
|---|---|---|---|---|---|---|
| Easy | 30.0 | 6 | 12 | 1 | 0.65 | [EnemyAI.cs:143](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L143) |
| Moderate | 21.0 | 9 | 10 | 2 | 0.85 | [EnemyAI.cs:149](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L149) |
| Normal | 15 | 12 | 8 | 3 | 1.00 | [EnemyAI.cs:193](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L193) |
| Hard | 12.0 | 16 | 7 | 5 | 1.15 | [EnemyAI.cs:156](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L156) |
| Insane | 9.0 | 20 | 6 | 7 | 1.35 | [EnemyAI.cs:162](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L162) |
| Extreme | 6.3 | 25 | 4 | 9 | 1.60 | [EnemyAI.cs:168](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L168) |

**Rusher** (retreatLoss 0.6):

| Difficulty | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | ecoMult | Kaynak |
|---|---|---|---|---|---|---|
| Easy | 22.0 | 5 | 8 | 1 | 0.65 | [EnemyAI.cs:143](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L143) |
| Moderate | 15.4 | 8 | 6 | 1 | 0.85 | [EnemyAI.cs:149](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L149) |
| Normal | 11 | 10 | 5 | 2 | 1.00 | [EnemyAI.cs:183](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L183) |
| Hard | 8.8 | 13 | 4 | 4 | 1.15 | [EnemyAI.cs:156](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L156) |
| Insane | 6.6 | 17 | 4 | 6 | 1.35 | [EnemyAI.cs:162](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L162) |
| Extreme | 4.62 | 21 | 3 | 8 | 1.60 | [EnemyAI.cs:168](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L168) |

**Boomer** (retreatLoss 0.3):

| Difficulty | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | ecoMult | Kaynak |
|---|---|---|---|---|---|---|
| Easy | 26.0 | 9 | 18 | 4 | 0.65 | [EnemyAI.cs:143](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L143) |
| Moderate | 18.2 | 14 | 14 | 5 | 0.85 | [EnemyAI.cs:149](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L149) |
| Normal | 13 | 18 | 12 | 6 | 1.00 | [EnemyAI.cs:188](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L188) |
| Hard | 10.4 | 23 | 10 | 8 | 1.15 | [EnemyAI.cs:156](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L156) |
| Insane | 7.8 | 30 | 9 | 10 | 1.35 | [EnemyAI.cs:162](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L162) |
| Extreme | 5.46 | **38** | 7 | 12 | 1.60 | [EnemyAI.cs:168](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L168) |

> Oyundaki en yüksek ordu tavanı: **Boomer × Extreme = 38** (spawnInterval 5.46s,
> ecoMult 1.60). En sert kombinasyon.

### Sabit cadence'lar (tüm personality/difficulty)

| Sabit | Değer | Kaynak |
|---|---|---|
| AssessInterval | 3 s | [EnemyAI.cs:43](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L43) |
| GatherCheckInterval | 6 s | [EnemyAI.cs:44](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L44) |
| TechInterval | 8 s | [EnemyAI.cs:45](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L45) |
| init _assessTimer | 2 s | [EnemyAI.cs:82](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L82) |
| init _gatherTimer | 3 s | [EnemyAI.cs:83](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L83) |

### AI iç üretim maliyetleri (oyuncu maliyetlerinden ayrı)

Bunlar AI'ın basitleştirilmiş ekonomisi için ayarlanmış sabitlerdir; oyuncunun
`BuildingDefs` maliyetlerini **kasıtlı olarak yansıtmaz**. Personality/difficulty
tarafından ölçeklenmez.

| Birim | Food | Wood | Gold | Kaynak |
|---|---|---|---|---|
| Militia | 60 | — | — | [EnemyAI.cs:49](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L49) |
| Archer | — | 35 | 25 | [EnemyAI.cs:50](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L50) |
| Cavalry | 80 | — | — | [EnemyAI.cs:52](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L52) |
| Trebuchet | — | 200 | 100 | [EnemyAI.cs:53](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L53) |
| Villager | 50 | — | — | [EnemyAI.cs:55](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L55) |
| Spearman | 35 | 25 | — | [EnemyAI.cs:56](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L56) |
| Medic | 60 | — | — | [EnemyAI.cs:58](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L58) |

### Ordu koordinasyonu & hedefleme (sabit)

| Parametre | Değer | Kaynak |
|---|---|---|
| RallyRadius | 6 u | [EnemyAI.cs:61](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L61) |
| ArriveFraction | 0.7 | [EnemyAI.cs:62](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L62) |
| RallyTimeoutTicks | 5 (~15s) | [EnemyAI.cs:63](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L63) |
| RetreatTimeoutTicks | 6 (~18s) | [EnemyAI.cs:64](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L64) |
| rally bias / cap | 0.4 / 18 u | [EnemyAI.cs:626](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L626) |
| retreat home fraction | 0.6 | [EnemyAI.cs:516](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L516) |
| trebuchetPerArmy | 1 / 6 | [EnemyAI.cs:332](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L332) |
| medicPerArmy | 1 / 6 | [EnemyAI.cs:345](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L345) |
| garrisonThreatRadius | 28 u | [EnemyAI.cs:417](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L417) |
| unitValue Villager / diğer | 65 / 35 | [EnemyAI.cs:774](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L774) |
| buildingValue TC | 60 | [EnemyAI.cs:779](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L779) |
| buildingValue üretim | 45 | [EnemyAI.cs:780](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L780) |
| buildingValue ekonomi | 40 | [EnemyAI.cs:782](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L782) |
| buildingValue diğer | 25 | [EnemyAI.cs:784](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L784) |
| distPenaltyDivisor | 8 | [EnemyAI.cs:790](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L790) |

---

## 4. Strateji & counter

**Personality'leri tanı:**

- **Rusher** — erken baskı (init spawn 8s, rushThreshold 5), zayıf ekonomi
  (villagerTarget 2), kayıpları en çok tolere eden (retreatLoss 0.6), profil ağır
  melee (0.65). İlk dakikalarda küçük melee grupları gelir. **Counter:** TC'ye
  Villager garnizonu, erken Spearman/Militia savunma, erken çatışmadan kaç; AI'ın
  zayıf ekonomisi oyunu uzattıkça kendi aleyhine döner.
- **Boomer** — geç ama büyük ordu (armyCap 18, Extreme'de **38**), en erken çekilen
  (retreatLoss 0.3), upgrade ağır (techTimer 8 = en erken araştırır), profilde en
  yüksek siege (0.20) ve cavalry (0.30). **Counter:** Erken saldır — boom kurarken
  zayıftır; büyük geç-oyun ordusunun olgunlaşmasına izin verme. Cavalry ağırlığına
  karşı Spearman/Pikeman hazır tut.
- **Balanced** — orta yol, hafif archer eğilimi (melee/archer 0.35); öngörülebilir
  tempo. **Counter:** standart counter matrisi yeter
  (bkz. [07-combat-counters.md](./07-combat-counters.md)).

**Zorluk seçimi:** Easy AI çok yavaş üretir (spawnInterval ×2.0), küçük ordu kurar,
geç saldırır ve yavaş toplar (ecoMult 0.65×). Yukarı çıktıkça hem üretim/commit
hızlanır hem de eko çarpanı artar — Extreme'de AI 1.6× hızlı kaynak biriktirir,
2.1× ordu tavanına ulaşır ve %42 spawn aralığıyla üretir. **Hile yok**: bütün
avantaj hız ve tavan ölçeklemesinden gelir, bedava kaynaktan değil.

**retreatLoss'u sömür:** Boomer'a yıpratma baskısı yap — küçük kayıplar bile onu
geri çektirir (%30), böylece sürekli regroup döngüsünde tutarsın (~18s timeout).
Rusher ise overcommit eder (%60); bekleyip yarısını biçtikten sonra karşı saldırı
kârlıdır.

**Counter-awareness'i sömür:** AI Cavalry görünce Spearman'a kayar. Cavalry
göstererek Spearman üretmeye zorla, sonra Archer/Crossbow ile o Spearman'ları biç.

**Hedeflemeyi sömür:** AI köylüleri (65) ve TC'yi (60) önceliklendirir. Köylüleri
TC/Castle içine garnizon et veya geride tut; ordu garrisonThreatRadius (28u)
dışındaysa AI o tehdidi hesaplamaz.

**Diplomasi:** AI müttefik/nötr takımlara saldırmaz (AIDP); aynı şekilde
geri-sayım işleyen düşman Wonder'ı öncelik haline gelir (AIWN) — Wonder ile
kazanmaya çalışıyorsan AI'ın doğrudan üstüne geleceğini hesaba kat.

---

## 5. Çapraz bağlantılar

- [01-game-flow-ages.md](./01-game-flow-ages.md) — AI çağ ilerlemesini techTimer
  ile sürer; çağ kapıları aynı sistemi paylaşır.
- [02-units.md](./02-units.md) — AI'ın ürettiği birim tipleri (Militia, Archer,
  Cavalry, Trebuchet, Spearman, Medic) ve AISC karışım profili.
- [05-tech-tree.md](./05-tech-tree.md) — AI techInterval (8s) ile araştırma yapar;
  Boomer upgrade ağırlıklı; ecoMult araştırma hızını da ölçekler.
- [07-combat-counters.md](./07-combat-counters.md) — AI ordularını karşılarken
  counter matrisi; AI'ın kendi counter-awareness'i (enemyCav→Spearman).
- [08-economy-trade.md](./08-economy-trade.md) — AI'ın iç üretim maliyetleri ve
  ecoMult ile ölçeklenen toplama hızı.
- [10-victory-objectives.md](./10-victory-objectives.md) — AI hedeflemesi TC'yi
  kazanma koşulu işaretler; AIWN ile Wonder geri-sayımını önceliklendirir.

---

## 6. Kod referansları (file:line, derivation)

- **Init sırası:** `ApplyPersonality` → `ApplyDifficulty`
  ([EnemyAI.cs:108-109](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L108)), eko
  çarpanı yayımı L110-113. Mid-game yeniden türetme `SetDifficulty()`
  ([EnemyAI.cs:118-125](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L118)).
- **Personality baz değerleri:** `ApplyPersonality`
  ([EnemyAI.cs:178-198](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L178));
  Rusher L183, Boomer L188, Balanced default L193.
- **Difficulty çarpanları:** `ApplyDifficulty`
  ([EnemyAI.cs:133-175](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L133)).
  Easy L143 (spawn×2.0, cap×0.5 min3, rush×1.5, vill−2 min1, eco 0.65);
  Moderate L149 (spawn×1.4, cap×0.75 min4, rush×1.2, vill−1 min1, eco 0.85);
  Normal = baseline no-op (eco 1.0); Hard L156 (spawn×0.80, cap×1.30, rush×0.85 min3,
  vill+2, eco 1.15); Insane L162 (spawn×0.60, cap×1.65, rush×0.72 min3, vill+4,
  eco 1.35); Extreme L168 (spawn×0.42, cap×2.10, rush×0.55 min2, vill+6, eco 1.60).
- **AICH eko çarpanı:** `_ecoMult` field [L131](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L131);
  yayımı `gm.teamEcoMult[teamId]` L112-113 / L123-124. Bedava kaynak yok — yalnızca
  GatherSystem/ResearchSystem hız ölçeği.
- **AIRD yuvarlama:** lokal `Round` = `Mathf.FloorToInt(v + 0.5f)`
  ([EnemyAI.cs:139](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L139)).
  Round-half-up; banker's rounding değil. Örn. 6.5→7, 13.5→14.
- **AISC karışım profili:** `AIProfile` struct + `For`
  ([EnemyAI.cs:10-27](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L10));
  ağırlıklı seçim `ChooseUnit` [L349-370](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L349).
- **Counter-awareness:** enemyCav→Spearman
  ([EnemyAI.cs:336-341](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L336)).
- **AIWN Wonder/Relic:** `FindWinConditionTarget`
  ([EnemyAI.cs:755-772](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L755)),
  çağrılar L469 (Rallying) ve L503 (Attacking).
- **AIDP diplomasi:** `gm.IsEnemy` filtreleri — `FindBestTarget` L738/L746,
  `CheckGarrison` L423-424.
- **Stance makinesi:** enum [L67](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L67),
  `Assess` [L388](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L388);
  TickGathering L447, TickRallying L463, TickAttacking L488, TickRetreating L512.
- **Sabit cadence'lar:** AssessInterval/GatherCheckInterval/TechInterval
  ([EnemyAI.cs:43-45](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L43));
  init timer field'ları L82-83.
- **AI üretim maliyetleri:** sabitler
  ([EnemyAI.cs:49-58](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L49)),
  yorum L47-48 (oyuncu maliyetlerini kasıtlı yansıtmaz).
- **Hedef skorlama:** unit/building değerleri ve mesafe cezası
  ([EnemyAI.cs:774-791](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L774)).
- **Enum'lar:** `AIPersonality` (Balanced, Rusher, Boomer)
  ([GameTypes.cs:69](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L69));
  `Difficulty` (Easy, Moderate, Normal, Hard, Insane, Extreme — 6 seviye)
  ([GameTypes.cs:77](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L77)).

---

## 7. AoE2 farkı (reference köprü)

AoE2'de zorluk seviyeleri **Easiest / Standard / Moderate / Hard / Hardest /
Extreme** olarak sıralanır; AoA artık **6 seviyeyle** (Easy/Moderate/Normal/Hard/
Insane/Extreme) bu granülerliğe çok daha yakın. Birkaç temel fark:

- **Hile yok, hız avantajı var.** AoE2'nin modern (Definitive Edition) AI'ı kaynak
  hilesi yapmaz; gerçek üstünlüğü her birimi aynı anda mikro edebilmesi ve köylü/
  upgrade üretmeyi asla unutmamasıdır. Extreme dışındaki tüm seviyelerde **TC
  üretimi kasıtlı yavaşlatılmıştır** (handicap). AoA da hile yapmaz — bunun yerine
  difficulty doğrudan üretim hızı, ordu tavanı, commit zamanlaması ve **toplama/
  araştırma hızını (`_ecoMult`)** ölçekler. Bu, AoE2'nin "handicap" felsefesine
  çok yakındır: Easy 0.65× yavaş ekonomi (AoE2 handicap'ine benzer), Extreme 1.60×
  hızlı ama yine bedava kaynak yok.
- **Personality sistemi.** AoE2'de AI davranışı `.ai` script'leri ile belirlenir ve
  oldukça karmaşıktır. AoA bunu üç sabit personality'ye (Balanced/Rusher/Boomer)
  indirger; her biri tek `ApplyPersonality` switch'inde beş knob + bir birim-karışımı
  profili (AISC) ile tanımlanır.
- **Kampanya istisnası.** AoE2'de AI yalnızca taunt ile açıkça istenirse veya bazı
  kampanya senaryolarında bedava kaynak alır. AoA'da böyle bir senaryo katmanı yoktur.
- **Diplomasi/oyun modları:** AoA AI'ı artık diplomasi-bilinçlidir (AIDP: müttefik/
  nötr hedeflenmez) ve Wonder/Relic zafer geri-sayımını önceliklendirir (AIWN), ama
  AoE2'nin tribute/ittifak müzakereleri ve mod-spesifik AI davranışları
  (Regicide/Nomad/Deathmatch) hâlâ yoktur
  (bkz. [reference/06-victory-game-modes.md](../reference/06-victory-game-modes.md)).

Kaynaklar:
[Steam — Does the AI cheat?](https://steamcommunity.com/app/813780/discussions/0/3771239049944817711/),
[Steam — Is the AI cheating on Hard?](https://steamcommunity.com/app/813780/discussions/0/1660069015243592489/),
[AoE Forums — AI difficulty levels](https://forums.ageofempires.com/t/ai-difficulty-levels/107341)

---

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor | durum |
|---|---|---|---|---|---|
| AISC | feature | Birim-karışımı profili eklendi (melee/archer/cav/siege ağırlıkları, olasılıksal örnekleme) ama tam strateji/script katmanı yok; davranış hâlâ tek `switch`. | AoE2 `.ai` script sistemi | L | ✅ kısmen (M12) |
| AICH | balance | Difficulty bedava kaynak hilesi yapmaz; `_ecoMult` ile toplama/araştırma hızı ölçekleniyor (Easy 0.65× → Extreme 1.60×). AoE2 TC handicap modeline yakın ama daha kaba. | AoE2 TC üretim handicap'i | M | ✅ (M12) |
| AIDF | balance | 6 AoA seviyesi (Easy/Moderate/Normal/Hard/Insane/Extreme) — AoE2'nin 6 seviyesiyle eşleşiyor. | AoE2 6-seviye difficulty | S | ✅ (M12) |
| AIRD | bug | Yuvarlama `FloorToInt(x+0.5f)` round-half-up'a geçirildi; banker's-rounding sapması giderildi (doküman/UI tutarlı). | — | S | ✅ (M12) |
| AIWN | feature | AI, geri-sayım işleyen düşman Wonder'ı önceliklendiriyor (`FindWinConditionTarget`); Relic-carrier ve Score zaferi için tam takip hâlâ kısmi. | AoE2 multi-victory AI | M | ✅ kısmen (M12) |
| AIDP | feature | AI müttefik/nötr takımları hedeflemiyor (`IsEnemy` filtreleri); ama tribute/ittifak müzakeresi yok. | AoE2 diplomasi + tribute | M | ✅ kısmen (M12) |
