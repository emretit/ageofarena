# AI & Zorluk — AoA Wiki

> Düşman takımları, üç **AIPersonality** (Balanced / Rusher / Boomer) ile ayarlanan
> tek bir beyinden (`EnemyAI`) sürülür; bunun üzerine küresel bir **Difficulty**
> (Easy / Normal / Hard / Insane) çarpan katmanı oturur. Personality "nasıl"
> oynanacağını (ekonomi mi baskı mı), Difficulty ise "ne kadar sert" oynanacağını
> belirler. AI kaynak hilesi yapmaz; basitleştirilmiş bir ekonomi çalıştırır,
> ordusunu bir rally noktasında toplar, hedefe topluca yürür ve kayıp eşiğini
> aşınca regroup için geri çekilir.
>
> **Kod kaynağı:** [EnemyAI.cs](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs),
> [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs)

---

## 1. Ne olduğu

Her düşman takımının kendi `EnemyAI` bileşeni vardır ve `GameManager.teamRes`
içinde kendi `ResourceManager` slotunu kullanır. AI iki paralel döngü çalıştırır:

- **Ekonomi döngüsü:** Villager üretip kaynak toplatır, `villagerTarget` sayısına
  kadar köylü hedefler.
- **Askeri döngü:** Birim üretir, ordusunu bir **Stance** durum makinesi
  (Gathering → Rallying → Attacking → Retreating) üzerinden yönetir; tek tek
  birim göndermek yerine orduyu toplayıp topluca saldırtır.

İki ayar ekseni:

- **AIPersonality** (`GameTypes.cs:23`): `Balanced`, `Rusher`, `Boomer`. Ordu
  boyutu, baskı zamanlaması ve ekonomi ağırlığını belirleyen baz değerleri kurar
  (`ApplyPersonality`).
- **Difficulty** (`GameTypes.cs:27`): `Easy`, `Normal`, `Hard`, `Insane`. Bu baz
  değerleri çarpanlarla ölçekler (`ApplyDifficulty`). Varsayılan `Normal`'da hiçbir
  değişiklik uygulanmaz, yani personality baz değerleri = efektif değerler.

`Init()` sırası: önce `ApplyPersonality`, sonra `ApplyDifficulty`
(`EnemyAI.cs:81-82`). Oyuncu oyun ortasında zorluğu değiştirirse HUD
`SetDifficulty()` çağırır ve aynı sırayla yeniden türetilir (`EnemyAI.cs:87-91`).

---

## 2. Nasıl çalışır (mekanik + formül)

### Personality baz değerleri

`ApplyPersonality` beş ana knob'u ayarlar (`EnemyAI.cs:122-142`):

- `_spawnInterval` — birim üretim aralığı (saniye); düşük = daha hızlı üretim.
- `_armyCap` — saldırıya başlamadan önceki tavan ordu büyüklüğü.
- `_rushThreshold` — saldırıyı tetikleyen minimum ordu büyüklüğü.
- `_villagerTarget` — hedeflenen köylü sayısı (ekonomi ağırlığı).
- `_retreatLoss` — saldırı gücünün kaçta kaçı kaybedilince geri çekilineceği.

### Difficulty çarpanları

`ApplyDifficulty` personality baz değerinin üzerine uygular (`EnemyAI.cs:96-118`):

| Difficulty | spawnInterval | armyCap | rushThreshold | villagerTarget |
|---|---|---|---|---|
| Easy | `× 1.6` | `× 0.6` (min 4) | `× 1.3` | `− 1` (min 1) |
| Normal | değişmez | değişmez | değişmez | değişmez |
| Hard | `× 0.75` | `× 1.35` | `× 0.85` (min 3) | `+ 2` |
| Insane | `× 0.55` | `× 1.7` | `× 0.7` (min 3) | `+ 4` |

Tüm tam sayı sonuçları `Mathf.RoundToInt` ile yuvarlanır — bu **round-half-to-even**
(banker's rounding) kullanır, yani `6.5 → 6`, `13.5 → 14`. Bu, türetilmiş
değerlerin neden naif "0.5 yukarı yuvarla" sonucundan farklı olabildiğini açıklar.

**Önemli:** `_retreatLoss`, `_spawnTimer`, `_techTimer` ve sabit cadence'lar
(assess / gather / tech) Difficulty tarafından **ölçeklenmez**; yalnızca yukarıdaki
dört alan ölçeklenir.

### Sabit cadence'lar (personality/difficulty'den bağımsız)

`AssessInterval = 3s`, `GatherCheckInterval = 6s`, `TechInterval = 8s`
(`EnemyAI.cs:18-20`). Başlangıç zamanlayıcıları field initializer'lardan gelir:
`_assessTimer = 2f`, `_gatherTimer = 3f` (`EnemyAI.cs:57-58`). `_spawnTimer` ve
`_techTimer` ise personality tarafından kurulur (örn. ilk saldırı baskısının
zamanlaması).

### Ordu koordinasyonu

`Stance` makinesi orduyu tek bir stance altında toplar: rally noktasında
`ArriveFraction = 0.7` oranında birim toplanınca commit edilir; `RallyTimeoutTicks = 5`
(~15s) sonra stragglerlar gelmese de saldırılır; `RetreatTimeoutTicks = 6` (~18s)
sonra tam eve dönülmese de yeniden toplanmaya geçilir (`EnemyAI.cs:36-39`).

### Hedef skorlama

Hedef seçimi ekonomi odaklıdır: Villager `65`, diğer birim `35`; bina değerleri
TownCenter `60`, üretim binaları `45`, ekonomi binaları `40`, diğer `25`; mesafe
cezası `dist / 8` (`EnemyAI.cs:689-705`). Yani AI öncelikle köylüleri ve TC'yi
hedefler — TC kazanma koşuludur.

---

## 3. Gerçek statlar (koddan)

### Personality baz değerleri (Normal'da efektif)

| Personality | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | retreatLoss | initSpawnTimer (s) | initTechTimer (s) | Kaynak |
|---|---|---|---|---|---|---|---|---|
| Balanced | 15 | 12 | 8 | 3 | 0.4 | 15 | 12 | [EnemyAI.cs:137](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L137) |
| Rusher | 11 | 10 | 5 | 2 | 0.6 | 8 | 16 | [EnemyAI.cs:127](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L127) |
| Boomer | 13 | 18 | 12 | 6 | 0.3 | 22 | 8 | [EnemyAI.cs:132](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L132) |

### Efektif değerler (Personality × Difficulty)

`retreatLoss` Difficulty'den etkilenmez; tablolarda personality değerini korur.

**Balanced:**

| Difficulty | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | retreatLoss | Kaynak |
|---|---|---|---|---|---|---|
| Easy | 24 | 7 | 10 | 2 | 0.4 | [EnemyAI.cs:103](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L103) |
| Normal | 15 | 12 | 8 | 3 | 0.4 | [EnemyAI.cs:117](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L117) |
| Hard | 11.25 | 16 | 7 | 5 | 0.4 | [EnemyAI.cs:108](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L108) |
| Insane | 8.25 | 20 | 6 | 7 | 0.4 | [EnemyAI.cs:113](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L113) |

**Rusher:**

| Difficulty | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | retreatLoss | Kaynak |
|---|---|---|---|---|---|---|
| Easy | 17.6 | 6 | 6 ⚠️ | 1 | 0.6 | [EnemyAI.cs:103](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L103) |
| Normal | 11 | 10 | 5 | 2 | 0.6 | [EnemyAI.cs:127](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L127) |
| Hard | 8.25 | 14 | 4 | 4 | 0.6 | [EnemyAI.cs:108](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L108) |
| Insane | 6.05 | 17 | 4 | 6 | 0.6 | [EnemyAI.cs:113](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L113) |

> ⚠️ Rusher × Easy `rushThreshold`: `Mathf.RoundToInt(5 × 1.3) = RoundToInt(6.5)`.
> Unity `RoundToInt` round-half-to-even kullandığı için sonuç **6**'dır (7 değil).
> Stat JSON'ında 7 yazıyordu; efektif kod değeri 6.
> ([EnemyAI.cs:104](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L104))

**Boomer:**

| Difficulty | spawnInterval (s) | armyCap | rushThreshold | villagerTarget | retreatLoss | Kaynak |
|---|---|---|---|---|---|---|
| Easy | 20.8 | 11 | 16 | 5 | 0.3 | [EnemyAI.cs:103](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L103) |
| Normal | 13 | 18 | 12 | 6 | 0.3 | [EnemyAI.cs:132](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L132) |
| Hard | 9.75 | 24 | 10 | 8 | 0.3 | [EnemyAI.cs:108](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L108) |
| Insane | 7.15 | 31 | 8 | 10 | 0.3 | [EnemyAI.cs:113](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L113) |

> Oyundaki en yüksek ordu tavanı: Boomer × Insane = **31**.

### Sabit cadence'lar (tüm personality/difficulty)

| Sabit | Değer | Kaynak |
|---|---|---|
| AssessInterval | 3 s | [EnemyAI.cs:18](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L18) |
| GatherCheckInterval | 6 s | [EnemyAI.cs:19](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L19) |
| TechInterval | 8 s | [EnemyAI.cs:20](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L20) |
| init _assessTimer | 2 s | [EnemyAI.cs:57](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L57) |
| init _gatherTimer | 3 s | [EnemyAI.cs:58](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L58) |

### AI iç üretim maliyetleri (oyuncu maliyetlerinden ayrı)

Bunlar AI'ın basitleştirilmiş ekonomisi için ayarlanmış sabitlerdir; oyuncunun
`BuildingDefs` maliyetlerini **kasıtlı olarak yansıtmaz**. Personality/difficulty
tarafından ölçeklenmez.

| Birim | Food | Wood | Gold | Kaynak |
|---|---|---|---|---|
| Militia | 60 | — | — | [EnemyAI.cs:24](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L24) |
| Archer | — | 35 | 25 | [EnemyAI.cs:25](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L25) |
| Cavalry | 80 | — | — | [EnemyAI.cs:27](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L27) |
| Trebuchet | — | 200 | 100 | [EnemyAI.cs:28](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L28) |
| Villager | 50 | — | — | [EnemyAI.cs:30](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L30) |
| Spearman | 35 | 25 | — | [EnemyAI.cs:31](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L31) |
| Medic | 60 | — | — | [EnemyAI.cs:33](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L33) |

### Ordu koordinasyonu & hedefleme (sabit)

| Parametre | Değer | Kaynak |
|---|---|---|
| RallyRadius | 6 u | [EnemyAI.cs:36](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L36) |
| ArriveFraction | 0.7 | [EnemyAI.cs:37](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L37) |
| RallyTimeoutTicks | 5 (~15s) | [EnemyAI.cs:38](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L38) |
| RetreatTimeoutTicks | 6 (~18s) | [EnemyAI.cs:39](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L39) |
| rallyBias / cap | 0.4 / 18 u | [EnemyAI.cs:563](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L563) |
| retreatHomeFraction | 0.6 | [EnemyAI.cs:453](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L453) |
| trebuchetPerArmy | 1 / 6 | [EnemyAI.cs:276](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L276) |
| medicPerArmy | 1 / 6 | [EnemyAI.cs:289](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L289) |
| garrisonThreatRadius | 28 u | [EnemyAI.cs:359](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L359) |
| unitValue Villager / diğer | 65 / 35 | [EnemyAI.cs:690](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L690) |
| buildingValue TC | 60 | [EnemyAI.cs:694](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L694) |
| buildingValue üretim | 45 | [EnemyAI.cs:696](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L696) |
| buildingValue ekonomi | 40 | [EnemyAI.cs:698](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L698) |
| buildingValue diğer | 25 | [EnemyAI.cs:699](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L699) |
| distPenaltyDivisor | 8 | [EnemyAI.cs:705](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L705) |

---

## 4. Strateji & counter

**Personality'leri tanı:**

- **Rusher** — erken baskı (init spawn 8s, rushThreshold 5), zayıf ekonomi
  (villagerTarget 2), kayıpları en çok tolere eden (retreatLoss 0.6). İlk
  dakikalarda küçük gruplar gelir. **Counter:** TC'ye Villager garnizonu, erken
  Spearman/Militia savunma duvarı, erken çatışmadan kaç; AI'ın zayıf ekonomisi
  oyunu uzattıkça kendi aleyhine döner.
- **Boomer** — geç ama büyük ordu (armyCap 18, Insane'de 31), en erken çekilen
  (retreatLoss 0.3 = ufak kayıpta geri çekilir), upgrade ağır (techTimer 8 = en
  erken araştırır). **Counter:** Erken saldır — boom kurarken zayıftır; büyük
  geç-oyun ordusunun olgunlaşmasına izin verme.
- **Balanced** — orta yol; öngörülebilir tempo. **Counter:** standart counter
  matrisi yeter (bkz. [07-combat-counters.md](./07-combat-counters.md)).

**Zorluk seçimi:** Easy AI yavaş üretir (spawnInterval ×1.6), küçük ordu kurar
ve geç saldırır; daha fazla köylü öldürebilmek için **rushThreshold artar**
(daha geç commit eder). Hard/Insane hızlı üretir, çok daha büyük ordu fielder ve
daha erken commit eder. Insane'de Boomer'ın 31'lik ordusu ve hızlı üretimi en
sert kombinasyondur.

**retreatLoss'u sömür:** Boomer'a yıpratma baskısı yap — küçük kayıplar bile onu
geri çektirir, böylece sürekli regroup döngüsünde tutarsın (~18s timeout). Rusher
ise overcommit eder; bekleyip yarısını biçtikten sonra karşı saldırı kârlıdır.

**Hedeflemeyi sömür:** AI köylüleri (65) ve TC'yi (60) önceliklendirir. Köylüleri
TC/Castle içine garnizon et veya geride tut; ordu garrisonThreatRadius (28u)
dışındaysa AI o tehdidi hesaplamaz.

---

## 5. Çapraz bağlantılar

- [01-game-flow-ages.md](./01-game-flow-ages.md) — AI çağ ilerlemesini techTimer
  ile sürer; çağ kapıları aynı sistemi paylaşır.
- [02-units.md](./02-units.md) — AI'ın ürettiği birim tipleri (Militia, Archer,
  Cavalry, Trebuchet, Spearman, Medic).
- [05-tech-tree.md](./05-tech-tree.md) — AI techInterval (8s) ile araştırma yapar;
  Boomer upgrade ağırlıklı.
- [07-combat-counters.md](./07-combat-counters.md) — AI ordularını karşılarken
  counter matrisi.
- [08-economy-trade.md](./08-economy-trade.md) — AI'ın iç üretim maliyetleri
  oyuncu ekonomisinden farklıdır.
- [10-victory-objectives.md](./10-victory-objectives.md) — AI hedeflemesi TC'yi
  kazanma koşulu olarak işaretler.

---

## 6. Kod referansları (file:line, derivation)

- **Init sırası:** `ApplyPersonality` → `ApplyDifficulty`
  ([EnemyAI.cs:81-82](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L81)).
  Mid-game yeniden türetme `SetDifficulty()`
  ([EnemyAI.cs:87-91](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L87)).
- **Personality baz değerleri:** `ApplyPersonality`
  ([EnemyAI.cs:122-142](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L122));
  Rusher L127-129, Boomer L132-134, Balanced default L137-139.
- **Difficulty çarpanları:** `ApplyDifficulty`
  ([EnemyAI.cs:96-118](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L96)).
  Easy L102-106 (spawn×1.6, cap×0.6 min4, rush×1.3, vill−1 min1); Hard L107-110
  (spawn×0.75, cap×1.35, rush×0.85 min3, vill+2); Insane L112-116 (spawn×0.55,
  cap×1.7, rush×0.7 min3, vill+4); Normal = değişmez (L117).
- **Yuvarlama:** Tüm int sonuçlar `Mathf.RoundToInt` (round-half-to-even). Örn.
  Rusher×Easy rush = `RoundToInt(6.5) = 6`; Rusher×Hard cap = `RoundToInt(13.5) = 14`.
- **Sabit cadence'lar:** AssessInterval/GatherCheckInterval/TechInterval
  ([EnemyAI.cs:18-20](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L18));
  init timer field'ları
  ([EnemyAI.cs:57-58](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L57)).
- **AI üretim maliyetleri:** sabitler
  ([EnemyAI.cs:24-33](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L24)),
  yorum L22-23 (oyuncu maliyetlerini kasıtlı yansıtmaz).
- **Ordu koordinasyonu:**
  ([EnemyAI.cs:36-39](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L36)),
  rally bias L563, retreat home L453, treb/medic oranları L276/L289, garrison
  tehdit L359.
- **Hedef skorlama:** unit/building değerleri ve mesafe cezası
  ([EnemyAI.cs:689-705](../../AgeOfArenaUnity/Assets/Scripts/EnemyAI.cs#L689)).
- **Enum'lar:** `AIPersonality` (Balanced, Rusher, Boomer)
  ([GameTypes.cs:23](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L23));
  `Difficulty` (Easy, Normal, Hard, Insane)
  ([GameTypes.cs:27](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L27)).

---

## 7. AoE2 farkı (reference köprü)

AoE2'de zorluk seviyeleri **Easiest / Standard / Moderate / Hard / Hardest /
Extreme** olarak sıralanır; AoA'nın dört seviyesi (Easy/Normal/Hard/Insane) bunun
sadeleştirilmiş bir alt kümesidir. Birkaç temel fark:

- **Hile yok, mikro avantajı var.** AoE2'nin modern (Definitive Edition) AI'ı
  kaynak hilesi yapmaz; gerçek üstünlüğü her birimi aynı anda mikro edebilmesi ve
  köylü/upgrade üretmeyi asla unutmamasıdır. Hatta Extreme dışındaki tüm
  seviyelerde **TC üretimi kasıtlı yavaşlatılmıştır** (handicap). AoA da hile
  yapmaz; bunun yerine difficulty doğrudan üretim hızı, ordu tavanı ve commit
  zamanlamasını ölçekler — AoE2'nin "handicap" felsefesine yakın ama çok daha
  kaba taneli.
- **Personality sistemi.** AoE2'de AI davranışı `.ai` script'leri (ör. topluluk
  yapımı "Barbarian", "DeathMatch" AI'ları) ile belirlenir ve oldukça karmaşıktır.
  AoA bunu üç sabit personality'ye (Balanced/Rusher/Boomer) indirger; her biri tek
  bir `ApplyPersonality` switch'inde beş knob ile tanımlanır.
- **Kampanya istisnası.** AoE2'de AI yalnızca taunt ile açıkça istenirse veya bazı
  kampanya senaryolarında bedava kaynak alır — bu standart skirmish'ten ayrı bir
  durumdur. AoA'da böyle bir senaryo katmanı yoktur.
- **Diplomasi/oyun modları:** AoE2 Regicide/Nomad/Deathmatch gibi modlara özel AI
  davranışları barındırır
  (bkz. [reference/06-victory-game-modes.md](../reference/06-victory-game-modes.md));
  AoA AI'ı yalnızca Conquest odaklıdır (TC = kazanma hedefi).

Kaynaklar:
[Steam — Does the AI cheat?](https://steamcommunity.com/app/813780/discussions/0/3771239049944817711/),
[Steam — Is the AI cheating on Hard?](https://steamcommunity.com/app/813780/discussions/0/1660069015243592489/),
[AoE Forums — AI difficulty levels](https://forums.ageofempires.com/t/ai-difficulty-levels/107341)

---

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| AISC | feature | Script/davranış katmanı yok; personality tek `switch` ile 5 knob'a sıkışmış. Strateji çeşitliliği düşük. | AoE2 `.ai` script sistemi | L |
| AICH | balance | Difficulty kaynak/handicap hilesi içermiyor; sadece üretim hızı + ordu cap ölçekleniyor. AoE2 TC handicap modeli yok. | AoE2 TC üretim handicap'i | M |
| AIDF | balance | 6 AoE2 seviyesine karşı 4 AoA seviyesi (Easiest/Moderate/Hardest eksik); granülerlik düşük. | AoE2 6-seviye difficulty | S |
| AIRD | bug | `Mathf.RoundToInt` round-half-to-even bazı türetilmiş değerleri (örn. Rusher×Easy rush=6) beklenmedik yapıyor; doküman/UI tutarsızlığı riski. | — | S |
| AIWN | feature | AI Wonder/Relic/Score zafer koşullarını kovalamıyor; yalnızca Conquest (TC) hedefliyor. | AoE2 multi-victory AI | M |
| AIDP | feature | Diplomasi/ittifak AI davranışı yok (Allied/Neutral/tribute). | AoE2 diplomasi + tribute | M |
