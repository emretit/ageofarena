# Teknoloji Ağacı — AoA Wiki

> Age of Arena'da teknolojiler tek bir statik tabloda (`TechDefs.Table`) tanımlıdır.
> **Tam olarak 19 teknoloji** vardır: 3 çağ ilerlemesi + 4 blacksmith bonusu (Forging,
> Fletching, ScaleMail, Bodkin) + 3 ekonomi/stable tech (DoubleBitAxe, Wheelbarrow,
> Bloodlines) + 7 birim kademe yükseltmesi + 2 university tech (Masonry, Fortified).
> Her tech bir binadan, bir çağ önkoşuluyla, sabit maliyet ve süreyle araştırılır.
> Bonuslar `TechState` üzerinden **canlı okunur** (saldırı/menzil/toplama) veya
> araştırma anında uygulanır (çağ + hp).
>
> **Kod kaynağı (tek doğruluk kaynağı):**
> [TechDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs),
> [TechState.cs](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs),
> [ResearchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs).

---

## 1. Ne olduğu

Teknoloji ağacı, oyuncunun zaman ve kaynak harcayarak ordusunu/ekonomisini kalıcı olarak
güçlendirdiği araştırma sistemidir. AoE2'deki gibi her teknoloji belirli bir binaya
bağlıdır ve belirli bir çağa ulaşıldığında açılır.

Üç ana grup vardır:

- **Çağ ilerlemeleri** (`FeudalAge`, `CastleAge`, `ImperialAge`) — Town Center'da araştırılır,
  takımı bir sonraki çağa geçirir ve yeni binaları/tech'leri açar.
- **Düz bonus tech'leri** (Forging, Fletching, ScaleMail, Bodkin, Bloodlines, DoubleBitAxe,
  Wheelbarrow, Masonry, Fortified) — saldırı, zırh/hp, toplama veya bina dayanıklılığı verir.
- **Kademe yükseltmeleri** (ManAtArms → Longswordsman → Champion vb.) — bir birim sınıfını
  bir üst kademeye taşır, hem saldırı hem hp artışı sağlar, bazıları önceki kademeyi önkoşul tutar.

Oyuncu (takım 0) araştırmayı bir binaya kuyruğa atar ve süre dolunca tamamlanır
([ResearchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L28)). AI takımları
ise `Apply` ile **anında** araştırır (kendi kaynaklarını önce düşerek)
([ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86)).

## 2. Nasıl çalışır (mekanik + formül)

### Araştırma akışı (oyuncu)

1. **Uygunluk filtresi** — bir binadaki seçilebilir tech'ler `ForBuilding(building, age, tech)`
   ile listelenir: bina eşleşmeli, çağ önkoşulu sağlanmalı, henüz araştırılmamış olmalı ve
   (varsa) önkoşul tech araştırılmış olmalı
   ([TechDefs.cs:82](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L82)).
2. **Çağ kapısı** — çağ ilerlemeleri "tam bir sonraki çağ" kuralına tabidir: Feudal yalnız
   Dark'tayken, Castle yalnız Feudal'dayken, Imperial yalnız Castle'dayken görünür. Diğer
   tech'ler `age >= requiredAge` ise görünür
   ([TechDefs.cs:97](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L97)).
3. **Kaynak + meşguliyet** — bina başka tech araştırmıyorsa ve kaynak yetiyorsa kaynak düşülür
   ve item kuyruğa girer ([ResearchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L28)).
4. **İlerleme** — her frame `elapsed += dt`; `elapsed >= totalTime` olunca `Apply` çağrılır
   ([ResearchSystem.cs:60](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L60)).
5. **İptal + iade** — `CancelActive` aktif araştırmayı iptal eder ve tam kaynağı geri verir
   ([ResearchSystem.cs:49](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L49)).

### Bonus uygulama mekaniği

Bonuslar iki şekilde devreye girer:

- **Canlı okunur (eylem gerekmez):** saldırı, menzil ve toplama bonusları her frame
  `TechState`'ten okunur — `AttackBonus`, `RangeBonus`, `GatherMult`
  ([TechState.cs:36](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L36)).
- **Araştırma anında uygulanır:** çağ ilerlemesi `tech.age`'i değiştirir; **hp bonusları**
  ise mevcut canlı birimlere geriye dönük uygulanır. `Apply` tech'ten önce her birim tipinin
  hp bonusunu kaydeder, tech'i işaretler, sonra deltayı hesaplayıp o takımın yaşayan
  birimlerinin `maxHp` ve `hp`'sine ekler — böylece eski ordu da anında güçlenir
  ([ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86)).

### Toplama çarpanı formülü

`GatherMult(kind)` = `1 + (Wheelbarrow ? 0.20 : 0) + (kind==Wood && DoubleBitAxe ? 0.25 : 0)`
([TechState.cs:73](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L73)).
Örnek: Wheelbarrow + DoubleBitAxe ile odun toplama = 1 + 0.20 + 0.25 = **×1.45**.

### Kademeli (stacklenen) saldırı/hp formülü

Saldırı bonusu birim tipine göre **toplamsal** birikir
([TechState.cs:22](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L22)):

- Militia saldırı = Forging(+2) + ManAtArms(+1) + Longswordsman(+2) + Champion(+2)
- Cavalry saldırı = Forging(+2) + Cavalier(+2) + Paladin(+3)
- Archer saldırı = Fletching(+1) + Bodkin(+1) + Crossbowman(+2) + Arbalest(+2)

## 3. Gerçek statlar (koddan)

> **NOT:** Bu sayfa için verilen stat JSON girdisi **boştu** (`[]`). Aşağıdaki tüm sayılar
> bu nedenle doğrudan `TechDefs.cs` tablosundan ve `TechState.cs` bonus tanımlarından
> okunmuştur (kod = tek doğruluk kaynağı). Tabloda **tam olarak 19 tech** vardır.

### 3.1 Tech tanımları (maliyet + süre + bina + çağ)

| Tech (`TechType`) | Görünen ad | Bina | Çağ önkoşulu | Yemek | Odun | Altın | Süre (s) | Önkoşul tech | Kaynak |
|---|---|---|---|---|---|---|---|---|---|
| FeudalAge | Derebeylik Çağı | TownCenter | Dark | 400 | 0 | 0 | 25 | — | [TechDefs.cs:42](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L42) |
| CastleAge | Kale Çağı | TownCenter | Feudal | 600 | 0 | 200 | 35 | — | [TechDefs.cs:43](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L43) |
| ImperialAge | İmparatorluk Çağı | TownCenter | Castle | 1000 | 0 | 600 | 50 | — | [TechDefs.cs:44](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L44) |
| Forging | Dövme | Blacksmith | Feudal | 150 | 0 | 0 | 20 | — | [TechDefs.cs:47](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L47) |
| Fletching | Oklama | Blacksmith | Feudal | 100 | 0 | 50 | 20 | — | [TechDefs.cs:48](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L48) |
| ScaleMail | Pul Zırh | Blacksmith | Castle | 150 | 0 | 100 | 25 | — | [TechDefs.cs:49](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L49) |
| Bodkin | İğne Ucu | Blacksmith | Castle | 150 | 0 | 100 | 25 | — | [TechDefs.cs:50](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L50) |
| DoubleBitAxe | Çift Balta | LumberCamp | Feudal | 100 | 0 | 0 | 18 | — | [TechDefs.cs:52](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L52) |
| Wheelbarrow | El Arabası | TownCenter | Feudal | 150 | 50 | 0 | 22 | — | [TechDefs.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L53) |
| Bloodlines | Soyağacı | Stable | Castle | 150 | 0 | 100 | 25 | — | [TechDefs.cs:54](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L54) |
| ManAtArms | Piyade | Barracks | Feudal | 100 | 0 | 40 | 25 | — | [TechDefs.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L56) |
| Longswordsman | Uzun Kılıç | Barracks | Castle | 150 | 0 | 100 | 30 | ManAtArms | [TechDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L57) |
| Champion | Şampiyon | Barracks | Imperial | 200 | 0 | 150 | 35 | Longswordsman | [TechDefs.cs:58](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L58) |
| Crossbowman | Arbaletçi | ArcheryRange | Castle | 150 | 0 | 100 | 30 | — | [TechDefs.cs:59](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L59) |
| Arbalest | Arbalet | ArcheryRange | Imperial | 200 | 0 | 150 | 35 | Crossbowman | [TechDefs.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L60) |
| Cavalier | Ağır Süvari | Stable | Castle | 150 | 0 | 100 | 30 | — | [TechDefs.cs:61](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L61) |
| Paladin | Paladin | Stable | Imperial | 200 | 0 | 150 | 35 | Cavalier | [TechDefs.cs:62](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L62) |
| Masonry | Duvar Ustalığı | University | Castle | 150 | 0 | 0 | 22 | — | [TechDefs.cs:64](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L64) |
| Fortified | Takviyeli Duvar | University | Imperial | 200 | 0 | 150 | 30 | — | [TechDefs.cs:65](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L65) |

> Tüm girdilerde `stone = 0` (taş maliyeti hiçbir tech'te kullanılmıyor). Toplam **19 satır**.

### 3.2 Bonus değerleri (`TechState`'ten)

| Tech | Etki | Değer | Kaynak |
|---|---|---|---|
| Forging | Militia + Cavalry saldırı | +2 | [TechState.cs:23](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L23) |
| Fletching | Archer saldırı | +1 | [TechState.cs:24](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L24) |
| Fletching | Archer menzil | +0.5 | [TechState.cs:47](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L47) |
| Bodkin | Archer saldırı | +1 | [TechState.cs:24](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L24) |
| ScaleMail | Militia + Cavalry hp | +20 | [TechState.cs:55](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L55) |
| Bloodlines | Cavalry hp | +20 | [TechState.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L60) |
| ManAtArms | Militia saldırı / hp | +1 / +10 | [TechState.cs:27](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L27), [TechState.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L56) |
| Longswordsman | Militia saldırı / hp | +2 / +15 | [TechState.cs:28](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L28), [TechState.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L57) |
| Champion | Militia saldırı / hp | +2 / +20 | [TechState.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L29), [TechState.cs:58](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L58) |
| Crossbowman | Archer saldırı / menzil / hp | +2 / +0.5 / +10 | [TechState.cs:32](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L32), [TechState.cs:48](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L48), [TechState.cs:63](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L63) |
| Arbalest | Archer saldırı / menzil / hp | +2 / +0.5 / +15 | [TechState.cs:33](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L33), [TechState.cs:49](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L49), [TechState.cs:64](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L64) |
| Cavalier | Cavalry saldırı / hp | +2 / +20 | [TechState.cs:30](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L30), [TechState.cs:61](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L61) |
| Paladin | Cavalry saldırı / hp | +3 / +25 | [TechState.cs:31](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L31), [TechState.cs:62](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L62) |
| Wheelbarrow | Tüm toplama çarpanı | +0.20 | [TechState.cs:76](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L76) |
| DoubleBitAxe | Odun toplama çarpanı | +0.25 | [TechState.cs:77](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L77) |
| Masonry | Bina melee + pierce zırh | +2 / +2 | [TechState.cs:69](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L69) |
| Fortified | Bina melee + pierce zırh | +3 / +3 | [TechState.cs:69](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L69), [TechState.cs:70](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L70) |

> **Önemli:** `Forging` melee bonusu **+2**'dir (AoE2'de Forging tek başına +1). Bu AoA'ya
> özgü bir değerdir ([TechState.cs:23](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L23)).

## 4. Strateji & counter

- **Çağ tempo'su:** Feudal yalnızca 400 yemek/25s ile çok ucuz ve hızlıdır — erken Feudal
  baskını AoA'da AoE2'ye göre çok daha agresif oynanabilir. Imperial 1000 yemek + 600 altın
  ister, ekonomi destekli oyuncuyu ödüllendirir.
- **HP tech'leri geriye dönüktür:** ScaleMail/Bloodlines ve kademe yükseltmeleri **mevcut
  ordunu da anında güçlendirir** ([ResearchSystem.cs:130](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L130)).
  Bu yüzden büyük bir ordu varken hp tech'i araştırmak savaş ortasında bile değerlidir.
- **Forging önceliği:** +2 saldırı (AoE2'deki iki seviyenin etkisi) tek tech'te geldiği için
  Militia/Cavalry için en yüksek değerli erken yatırımdır.
- **Kademe önkoşul zinciri:** Champion için Longswordsman, o da ManAtArms gerektirir; Imperial
  birim hayalini kuruyorsan kademeleri atlama — sırayla araştır
  ([TechDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L57)).
- **AI dezavantajı:** AI anında araştırır (kaynak harcayarak); insan oyuncu araştırma süresi
  beklediği için AI tech yarışında avantajlıdır — bunu erken ekonomi ile telafi et.
- **Counter okuması:** Rakip Crossbowman/Arbalest yığıyorsa, senin Militia hattının ScaleMail +
  Champion ile yüksek hp'si pierce hasarını emer; counter detayları için
  [./07-combat-counters.md](./07-combat-counters.md).

## 5. Çapraz bağlantılar

- [./01-game-flow-ages.md](./01-game-flow-ages.md) — çağ ilerleme tech'leri (FeudalAge/CastleAge/ImperialAge) ve çağ kapıları
- [./02-units.md](./02-units.md) — tech bonuslarının uygulandığı birim tipleri (Militia/Archer/Cavalry)
- [./03-unit-upgrades.md](./03-unit-upgrades.md) — kademe yükseltme zincirleri (ManAtArms→Champion vb.) detayı
- [./04-buildings.md](./04-buildings.md) — tech'lerin araştırıldığı binalar + Masonry/Fortified bina zırhı
- [./06-civilizations.md](./06-civilizations.md) — medeniyetlerin tech erişimi/unique tech'ler
- [./07-combat-counters.md](./07-combat-counters.md) — saldırı/zırh tech'lerinin counter dengesine etkisi
- [./08-economy-trade.md](./08-economy-trade.md) — Wheelbarrow/DoubleBitAxe toplama çarpanları
- [./09-ai-difficulty.md](./09-ai-difficulty.md) — AI'nın anında araştırma davranışı

## 6. Kod referansları (file:line, derivation)

- **Tech tablosu (19 giriş):** [TechDefs.cs:39-66](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L39).
  `static readonly TechDef[] Table` — her satır `new(...)` ile bir TechDef; doğrulama:
  `grep -c "new(TechType" TechDefs.cs` = **19**.
- **TechDef struct (alanlar + iki ctor):** [TechDefs.cs:8](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L8).
  İkinci ctor önkoşul (`requires`/`hasRequires`) içindir [TechDefs.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L29).
- **Bina başına seçilebilir tech filtresi:** [TechDefs.cs:82](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L82) (`ForBuilding`).
- **Çağ kapısı mantığı:** [TechDefs.cs:97](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L97) (`IsAvailable`).
- **Bonus türetimleri (canlı okunan):** saldırı [TechState.cs:36](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L36),
  menzil [TechState.cs:45](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L45),
  hp [TechState.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L53),
  toplama [TechState.cs:73](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L73),
  bina zırh [TechState.cs:69](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L69).
- **`Version` damgası:** [TechState.cs:17](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L17) — UI
  araştırma seti değişince ucuza fark algılar (`Mark` artırır).
- **Oyuncu araştırma kuyruğu:** [ResearchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L28)
  (`Enqueue`), [ResearchSystem.cs:60](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L60) (`Tick`),
  iptal/iade [ResearchSystem.cs:49](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L49).
- **Tech uygulama + geriye dönük hp:** [ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86)
  (`Apply`); hp deltası yaşayan birimlere [ResearchSystem.cs:130](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L130).
- **`TechType` enum (19 üye):** GameTypes.cs:33 (3 çağ + bonus + kademe + ekonomi + university).

## 7. AoE2 farkı (reference köprü)

Tam AoE2 teknoloji listesi için: [../reference/04-tech-tree.md](../reference/04-tech-tree.md).

Başlıca farklar:

- **Çok daha küçük ağaç:** AoA'da 19 tech vardır; AoE2'de düzinelerce ekonomi + askeri +
  monk + kuşatma + kule tech'i bulunur. AoA'da Loom, Hand Cart, Horse Collar/Heavy Plow/Crop
  Rotation, madencilik, market (Coinage/Banking/Caravan/Guilds), monk tech'leri, Ballistics,
  Thumb Ring, Husbandry vb. **yok**.
- **Birleştirilmiş seviyeler:** AoE2'de Forging/Iron Casting/Blast Furnace üç ayrı saldırı
  tech'i iken AoA'da tek `Forging` (+2). Aynı şekilde zırh hattı üç seviyeyken AoA'da tek
  `ScaleMail` (+20 hp olarak modellenmiş, zırh yerine).
- **Daha ucuz/hızlı çağlar:** AoA Feudal 400Y/25s (AoE2: 500Y/130s), Castle 600Y+200A/35s
  (AoE2: 800Y+200A/160s), Imperial 1000Y+600A/50s (AoE2: 1000Y+800A/190s).
- **Önkoşul yapısı sade:** AoA yalnızca kademe zincirinde tek-tech önkoşul kullanır
  (`hasRequires`); AoE2'nin çoklu önkoşul/bina-sayısı kapıları yok.
- **Stat modeli farkı:** AoA kademe yükseltmeleri "zırh" yerine doğrudan **hp** ekler; bina
  tech'leri (Masonry/Fortified) AoE2'deki hp+küçülme yerine doğrudan **zırh** verir.

## 8. Eksikler / Yapılacaklar

| ID-aday | Sınıf | Eksik | AoE2-ref | Efor |
|---|---|---|---|---|
| ECOT | Ekonomi tech | Loom, Hand Cart, Horse Collar/Heavy Plow/Crop Rotation, madencilik tech'leri yok | reference/04-tech-tree.md §Ekonomi | Orta |
| MKTT | Market tech | Coinage/Banking/Caravan/Guilds (ticaret tech'leri) yok | reference/04-tech-tree.md §Market | Orta |
| ARMT | Askeri zırh | Ayrı zırh hattı (Scale/Chain/Plate) ve okçu zırhı yok; ScaleMail hp olarak modellenmiş | reference/04-tech-tree.md §Blacksmith | Orta |
| ATKT | Askeri saldırı seviyeleri | Iron Casting/Blast Furnace/Bracer çok-seviyeli saldırı yok (tek Forging/Bodkin) | reference/04-tech-tree.md §Blacksmith | Düşük |
| MONT | Monk tech | Redemption/Atonement/Sanctity/Block Printing vb. monk tech'leri yok | reference/04-tech-tree.md §University/Monastery | Yüksek |
| SIGT | Kuşatma/kule tech | Ballistics, Guard Tower/Keep, Siege Engineers, Chemistry yok | reference/04-tech-tree.md §University | Yüksek |
| SPDT | Hız tech | Husbandry (süvari hız), Squires (piyade hız), Conscription (üretim hızı) yok | reference/04-tech-tree.md §Stable/Barracks | Orta |
| ELIT | Elite kademe | Elite Skirmisher, Hussar, Heavy Cavalry Archer, Two-Handed Swordsman kademeleri yok | reference/04-tech-tree.md §kademeler | Orta |
| AIRS | AI araştırma dengesi | AI tech'i anında alıyor (süre yok); insan oyuncuya karşı adaletsiz tempo | — (denge) | Düşük |
