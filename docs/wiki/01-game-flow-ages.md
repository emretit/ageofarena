# Oyun Akışı & Çağlar — AoA Wiki

> Age of Arena bir oyun oturumunun nasıl başladığı (runtime sahne kurulumu), nasıl
> ilerlediği (çağ yükseltmeleri) ve nasıl bittiği üzerine ansiklopedik + kod-temelli
> rehber. Çağ sistemi, AoE2'deki gibi tüm bina/birim/teknoloji ağacının kapısını açan
> dört kademeli (`Dark → Feudal → Castle → Imperial`) bir omurgadır.
>
> **Kod kaynağı:** Sahne kurulumu [GameBootstrap.cs](../../AgeOfArenaUnity/Assets/Scripts/GameBootstrap.cs) +
> [WorldRoot.cs](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs); kurulum ekranı
> [CivSelectScreen.cs](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs); çağ tanımı ve
> maliyetleri [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs) +
> [TechDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs); çağ atlama önkoşulu ve
> uygulaması [ResearchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs); çağ durumu
> [TechState.cs](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs).
>
> Bu sayfadaki **tüm sayılar koddan** (`TechDefs.cs` tablosu) türetilmiştir; teyit
> edilemeyen hiçbir değer uydurulmadı.

---

## 1. Ne olduğu

Age of Arena bir AoE2-tarzı izometrik RTS'tir. Bir oturum, oyuncu Play'e bastığında
**tamamen kod ile** kurulan tek bir savaş arenasında geçer — elle yazılmış bir `.unity`
sahnesi yoktur. Oyunun zamansal omurgası **çağ ilerlemesidir**: takımlar `Dark` çağında
başlar ve `Feudal → Castle → Imperial` sırasıyla yükselerek daha güçlü bina, birim ve
teknolojilerin kilidini açar.

İki ayrı kavram vardır:

- **Oyun akışı (game flow):** Bir oturumun yaşam döngüsü — `GameBootstrap.Boot()` ile
  başlangıç, `WorldRoot.Build()` ile arena kurulumu, oyun sırasında ekonomi/savaş, ve
  `GameBootstrap.Restart()` ile yeniden başlatma.
- **Çağ sistemi (ages):** `Age` enum'u ile modellenen 4 kademeli teknoloji ilerlemesi.
  Çağ yükseltmeleri, normal teknolojilerle aynı araştırma kuyruğundan akar (Town
  Center'da araştırılan birer `TechType`'tır).

AoA, AoE2'nin çağ omurgasını sadeleştirir: aynı 4 çağ adı korunur ama maliyetler ve
süreler oldukça düşürülmüştür (hızlı, arena-odaklı oturumlar için).

---

## 2. Nasıl çalışır (mekanik + formül)

### Oturum başlangıcı (boot)

1. Play'e basıldığında `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` ile
   `GameBootstrap.Boot()` otomatik tetiklenir — boş bir sahnede bile çalışır.
2. `BuildIfNeeded()` zaten bir `WorldRoot` varsa hiçbir şey yapmaz; yoksa
   `AgeOfArena` GameObject'ini yaratıp `mapSeed`'i verip `WorldRoot.Build()` çağırır.
3. `WorldRoot.Build()` tüm arenayı kurar: 4 üslü elmas yerleşim, orman halkası,
   madenler, relikler, su gövdeleri, NavMesh bake, kamera, ve tüm gameplay sistemleri.
   `Build()` ayrıca `GameBootstrap.NextGameMode` ve `NextDifficulty`'yi GameManager'a yansıtır.
4. AI takımları (1-3) rastgele bir `Civilization` alır; takım 0 (oyuncu) civ'ini
   `GameBootstrap.PlayerCiv`'den alır (`SetupGameplay`). Sadece takım 0 (güney)
   oyuncu-kontrollüdür, takım 1-3 `EnemyAI` beyinleriyle oynar.
5. **Kurulum ekranı (`CivSelectScreen`):** `Build()` sonunda, `PlayerCiv` hâlâ
   `None` ise (yani ilk oturum) arena üzerine bir prosedürel uGUI ekranı açılır.
   Oyuncu medeniyetini (veya "Yok"), **zorluğu** (Kolay→Efsanevi 6 kademe) ve
   **modu** (Rastgele/Ölüm Maçı/Regicide/Göçebe) seçer. Seçimler `GameBootstrap`'ta
   kalıcı tutulur, böylece restart ekranı tekrar göstermez ([CivSelectScreen.cs](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs)).

### Çağ ilerleme mekaniği

Çağ yükseltmeleri **teknoloji olarak modellenir** — `TechType.FeudalAge`,
`TechType.CastleAge`, `TechType.ImperialAge`. Her biri Town Center'da araştırılır,
bir önceki çağı gerektirir ve food (+ daha sonra gold) maliyeti vardır.

Hangi yükseltmenin **şu an** araştırılabilir olduğu `TechDefs.IsAvailable` ile
belirlenir — yükseltmeler "bir sonraki çağa" kilitlidir:

```
FeudalAge   → yalnızca takım Dark çağındaysa
CastleAge   → yalnızca takım Feudal çağındaysa
ImperialAge → yalnızca takım Castle çağındaysa
```

Araştırma tamamlandığında `ResearchSystem.Apply()` çağrılır; ilgili çağa göre
`TechState.age` güncellenir ve `GameEvents.FireAgeAdvanced(teamId, age)` tetiklenir:

```
FeudalAge   → tech.age = Age.Feudal
CastleAge   → tech.age = Age.Castle
ImperialAge → tech.age = Age.Imperial
```

`Age` bir `enum`'dur (`Dark=0, Feudal=1, Castle=2, Imperial=3`), bu yüzden çağ
karşılaştırmaları `age >= d.requiredAge` gibi sayısal sıralama ile yapılır
(bkz. `TechDefs.IsAvailable` ve `ForBuilding`). Bir teknoloji/birim/bina belirli bir
`requiredAge` ister; takım o çağa ulaşmadıkça menüde görünmez.

### Bina önkoşulu (AGEB — AoE2 "2 bina" kuralı)

`IsAvailable` çağı kapısıdır, ama **Dark → Feudal** geçişinin ek bir önkoşulu vardır:
takım, Town Center **dışında en az 2 tamamlanmış bina**ya sahip olmalıdır. Bu kontrol
`ResearchSystem.Enqueue` içinde, `def.type == TechType.FeudalAge` için
`MeetsBuildingPrereq(teamId, 2)` ile yapılır — başarısızsa kaynak düşülmez, araştırma
başlamaz ([ResearchSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L36)).

- `MeetsBuildingPrereq` takımın binalarını sayar; `underConstruction` olanlar ve
  `TownCenter` **sayılmaz** ([ResearchSystem.cs:129](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L129)).
- HUD, çağ butonunu etkin/pasif yapmak için `CanAdvanceAge()` çağırır; bu da
  `tech.age != Age.Dark` (yani Feudal+ ise önkoşul yok) **veya** `MeetsBuildingPrereq(0, 2)`
  döndürür ([ResearchSystem.cs:146](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L146)).
- Sonuç: yalnızca **ilk** çağ atlaması (Dark→Feudal) 2-bina ister; Feudal→Castle ve
  Castle→Imperial yalnızca kaynak + önceki çağ ister.

### Yeniden başlatma (restart)

Oyun bittiğinde `GameBootstrap.Restart(seed)` çağrılır. Build settings'te authored
sahne olmadığı için `SceneManager.LoadScene` güvenilir değildir; bunun yerine:

1. `Time.timeScale = 1`, `GameEvents.Reset()`.
2. Mevcut `WorldRoot` yok edilir, `NavMesh.RemoveAllNavMeshData()` ile nav verisi temizlenir.
3. Bir `RebuildKick` helper'ı (world root'un **çocuğu değil**) eklenir; bir frame bekleyip
   `BuildIfNeeded()` ile arenayı yeniden kurar, sonra kendini yok eder.
4. `seed = 0` ise `WorldRoot.Build()` yeni rastgele bir harita seed'i seçer (farklı harita).

---

## 3. Gerçek statlar (koddan)

> **Stat JSON girdisi boştu (`[]`)**, bu yüzden tüm sayılar doğrudan
> `TechDefs.cs` tablosundan ve `GameTypes.cs` enum'undan alınmıştır.

### Çağ yükseltme maliyetleri & süreleri

| Geçiş | TechType | Bina | Önkoşul çağ | Ek önkoşul | Food | Wood | Gold | Stone | Süre (s) | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|
| Dark → Feudal | `FeudalAge` | TownCenter | `Dark` | ≥2 TC-dışı bina | 400 | 0 | 0 | 0 | 25 | [TechDefs.cs:52](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L52) |
| Feudal → Castle | `CastleAge` | TownCenter | `Feudal` | — | 600 | 0 | 200 | 0 | 35 | [TechDefs.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L53) |
| Castle → Imperial | `ImperialAge` | TownCenter | `Castle` | — | 1000 | 0 | 600 | 0 | 50 | [TechDefs.cs:54](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L54) |

### Çağ enum tanımı

| Çağ | Enum değeri | Başlangıç durumu | Kaynak |
|---|---|---|---|
| Dark | `Age.Dark` (0) | Tüm takımlar burada başlar | [GameTypes.cs:50](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L50) |
| Feudal | `Age.Feudal` (1) | — | [GameTypes.cs:50](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L50) |
| Castle | `Age.Castle` (2) | — | [GameTypes.cs:50](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L50) |
| Imperial | `Age.Imperial` (3) | — | [GameTypes.cs:50](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L50) |

Başlangıç çağı `TechState.age = Age.Dark` ile sabittir: [TechState.cs:11](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L11).

### Oturum kurulum sabitleri (arena düzeni)

| Parametre | Değer | Kaynak |
|---|---|---|
| Takım sayısı | 4 (1 oyuncu + 3 AI) | [WorldRoot.cs:105](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L105) |
| Üs konumları | elmas: ±58 (kuzey/güney/doğu/batı) | [WorldRoot.cs:30](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L30) |
| Harita tipi | "Arena adası": kara diski yarıçapı 92, okyanusla çevrili | [WorldRoot.cs:53](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L53) |
| Relik sayısı | 3 (merkez + 2 köşegen) | [WorldRoot.cs:646](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L646) |
| Mod / zorluk kaynağı | `GameBootstrap.NextGameMode` / `NextDifficulty` → GameManager | [WorldRoot.cs:100](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L100) |
| Restart başlangıç seed | 0 = rastgele | [GameBootstrap.cs:28](../../AgeOfArenaUnity/Assets/Scripts/GameBootstrap.cs#L28) |

> Not: Oyuncu üssü 3 köylü + 2 milis ile başlar ([WorldRoot.cs:764](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L764));
> nüfus tavanı team-0 binalarından (`RecomputePop`) türetilir. Bu sayfanın stat tablosu
> yalnızca çağ/akış sayılarına odaklanır; birim statları için bkz.
> [./02-units.md](./02-units.md), nüfus için bkz. [./04-buildings.md](./04-buildings.md).

### Çağa göre açılan içerik (kısa bakış)

Her bina/birim/tech bir `requiredAge` (veya bina için `minAge`) ister; takım o çağa
ulaşana kadar menüde görünmez. Aşağıdaki tablo yalnızca **o çağda yeni açılan** başlıca
öğeleri özetler (kaynaklar: bina için [BuildingDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs)
`minAge`; tech için [TechDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs) `reqAge`).

| Çağ | Yeni binalar (minAge) | Öne çıkan tech'ler / promosyonlar (reqAge) |
|---|---|---|
| **Dark** | Town Center, House, Barracks, Farm, Lumber/Mining Camp, Mill, Market, Wall, Gate, Outpost, Dock | Loom, Wheelbarrow-öncesi yok — temel ekonomi |
| **Feudal** | Archery Range, Blacksmith, Watch Tower | FeudalAge'in kendisi; Forging, Fletching, ManAtArms, Wheelbarrow, HorseCollar, DoubleBitAxe, GoldMining/StoneMining, ScaleBarding |
| **Castle** | Stable, Castle, Monastery, University, Siege Workshop | Longswordsman, Crossbowman, Cavalier, Pikeman, LightCavalry, Bloodlines, Masonry/Architecture/Ballistics, manastır tech'leri, civ-özel unique tech'ler |
| **Imperial** | Wonder, Bombard Tower | Champion, Arbalest, Paladin, Halberdier, Hussar, BlastFurnace, PlateMail/PlateBarding, Chemistry, Keep, Fortified, Banking |

---

## 4. Strateji & counter

- **Hızlı çağ atlama (fast-castle benzeri):** Feudal yalnızca 400 food / 25s; Castle
  600 food + 200 gold / 35s. Erken gold madenciliği yapan bir ekonomi, rakipten önce
  Castle çağına geçip `Cavalier`/`Crossbowman` tier üstünlüğü kazanabilir. Ancak
  Feudal'a geçmek için **önce ≥2 üretim/ekonomi binası** dikmek gerekir (AGEB) — bu da
  ilk dakikalarda en az iki bina (ör. House + Barracks veya Lumber Camp + Mill) zorunlu kılar.
- **Imperial spike:** Imperial 1000 food + 600 gold / 50s ile en pahalı eşiktir ama
  `Champion`/`Arbalest`/`Paladin` tier 3-4 promosyonları ve University zırh teklerini
  açar — geç oyun ordusunu domine eder.
- **Çağ kilidi sömürüsü:** Yükseltmeler yalnızca bir-sonraki-çağa kilitli olduğundan
  (`IsAvailable`), bir takım çağ atlamayı geciktirirse tüm tier promosyonları da
  gecikir; rakip aynı çağda kalıp daha erken yatırımla counter yapabilir.
- **Restart determinizmi:** Sabit `seed` ile yeniden başlatma aynı haritayı verir —
  strateji denemeleri/test için kullanışlı (`GameBootstrap.Restart(seed)`).

---

## 5. Çapraz bağlantılar

- [./05-tech-tree.md](./05-tech-tree.md) — çağa kilitli tüm teknolojiler ve `TechState` bonusları.
- [./03-unit-upgrades.md](./03-unit-upgrades.md) — çağ-bazlı tier promosyonları (ManAtArms→Champion vb.).
- [./02-units.md](./02-units.md) — çağa göre açılan birimler.
- [./04-buildings.md](./04-buildings.md) — bina çağ gereksinimleri ve nüfus tavanı.
- [./08-economy-trade.md](./08-economy-trade.md) — çağ atlamayı finanse eden ekonomi/toplama.
- [./10-victory-objectives.md](./10-victory-objectives.md) — oturumu bitiren zafer koşulları.
- [./09-ai-difficulty.md](./09-ai-difficulty.md) — AI'nin çağ atlama ve ordu zamanlaması.

---

## 6. Kod referansları (file:line, derivation)

- **Boot / restart akışı** — [GameBootstrap.cs:11](../../AgeOfArenaUnity/Assets/Scripts/GameBootstrap.cs#L11)
  (`Boot` → `BuildIfNeeded` → `WorldRoot.Build`), restart [GameBootstrap.cs:41](../../AgeOfArenaUnity/Assets/Scripts/GameBootstrap.cs#L41),
  kalıcı seçimler `PlayerCiv`/`NextGameMode`/`NextDifficulty` [GameBootstrap.cs:32](../../AgeOfArenaUnity/Assets/Scripts/GameBootstrap.cs#L32).
- **Arena kurulumu** — [WorldRoot.cs:83](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L83) (`Build`),
  gameplay/civ atama [WorldRoot.cs:739](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L739) (`SetupGameplay`).
- **Kurulum ekranı** — [CivSelectScreen.cs:18](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L18)
  (civ/zorluk/mod), `Choose` [CivSelectScreen.cs:101](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L101);
  spawn koşulu [WorldRoot.cs:128](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L128).
- **Çağ enum** — [GameTypes.cs:50](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L50) (`enum Age`),
  çağ-teknolojileri [GameTypes.cs:83](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L83) (`enum TechType`).
- **Çağ maliyet tablosu** — [TechDefs.cs:52](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L52)
  (Feudal/Castle/Imperial satırları); maliyet/süre değerleri doğrudan `TechDef` ctor argümanlarından okundu.
- **Araştırılabilirlik kapısı** — [TechDefs.cs:175](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L175)
  (`IsAvailable`: çağ atlamalar bir-sonraki-çağa kilitli, diğer tekler `age >= requiredAge`).
- **Bina önkoşulu (AGEB)** — [ResearchSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L36)
  (`Enqueue` Feudal kapısı), `MeetsBuildingPrereq` [ResearchSystem.cs:129](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L129),
  `CanAdvanceAge` [ResearchSystem.cs:146](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L146).
- **Çağ durumu** — [TechState.cs:11](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L11)
  (`age = Age.Dark` başlangıç).
- **Çağ uygulaması** — `ResearchSystem.Apply` çağ-bazlı `tech.age` setleri ve
  `FireAgeAdvanced` olayları ([ResearchSystem.cs:96-110](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L96)).

---

## 7. AoE2 farkı (reference köprü)

Tam karşılaştırma için bkz. [../reference/04-tech-tree.md](../reference/04-tech-tree.md) ("Çağ İlerleme Maliyetleri").

| Geçiş | AoA (kod) | AoE2 (referans) |
|---|---|---|
| Dark → Feudal | 400F / 25s | 500F / 130s |
| Feudal → Castle | 600F + 200G / 35s | 800F + 200G / 160s |
| Castle → Imperial | 1000F + 600G / 50s | 1000F + 800G / 190s |

**Temel farklar:**

- **Maliyet/süre küçültülmüş:** AoA çağ atlamaları AoE2'ye göre belirgin biçimde ucuz
  ve ~5× daha hızlıdır — arena temposunu hızlandırmak için.
- **Bina önkoşulu (kısmen):** AoE2'de her çağ atlaması için mevcut çağdan **2 bina**
  gerekir. AoA bunu yalnızca **ilk geçişte** uygular: Dark→Feudal için ≥2 TC-dışı
  tamamlanmış bina şartı `ResearchSystem.Enqueue` içinde aktiftir; Feudal→Castle ve
  Castle→Imperial'da bina önkoşulu yoktur (yalnızca kaynak + önceki çağ).
- **Aynı 4 çağ:** İsimlendirme (`Dark/Feudal/Castle/Imperial`) AoE2 ile birebir aynı.
- **Çağ = teknoloji modeli:** AoE2'de de çağ atlama bir araştırmadır; AoA bunu aynı
  `TechType` kuyruğundan akıtarak sadık kalır.

---

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| AGEB+ | game flow | Bina önkoşulu yalnızca Dark→Feudal'da var; Feudal→Castle ve Castle→Imperial için "2 bina" kuralı yok | her geçişte "2 bina" | S |
| DARK | ages | `Dark` çağ kendine özgü kısıtlama/avantaj yok; sadece başlangıç durumu | AoE2 Dark Age bina/birim kısıtları | S |
| AGFX | game flow | Çağ atlama anında görsel/ses kutlama (TC bayrak/anim) `FireAgeAdvanced` dışında belirsiz | AoE2 "Advancing to X Age" feedback | M |
| STRT+ | game flow | `CivSelectScreen` arena kurulduktan sonra üstüne açılıyor; gerçek lobi/harita seçimi (boyut/oyuncu sayısı) yok | AoE2 lobi/skirmish setup | L |

> **Çözülenler (kod ↔ eski TODO):** AGEB (Dark→Feudal 2-bina önkoşulu) ve STRT/ARES
> (`CivSelectScreen` ile civ + zorluk + mod seçimi, restart'ta kalıcı) artık kodda
> uygulanmış durumda — yukarıdaki AGEB+/STRT+ yalnızca kalan kısmi boşlukları izler.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
