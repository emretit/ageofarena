# Teknoloji Ağacı — AoA Wiki

> Age of Arena'da teknolojiler tek bir statik tabloda (`TechDefs.Table`) tanımlıdır.
> **Tam olarak 66 teknoloji** vardır: 3 çağ ilerlemesi + 7 birim kademe hattı (Militia/Archer/
> Cavalry) + 7 counter/mobil kademe hattı (Spearman/Skirmisher/Camel/Scout/CavArcher/Galley) +
> Blacksmith saldırı/zırh hatları (melee/archer/barding) + ekonomi (Loom/BowSaw/madencilik/çiftlik/
> Wheelbarrow/DoubleBitAxe) + University (Masonry/Fortified/Tower/Ballistics/Chemistry/Architecture) +
> Manastır (Sanctity/BlockPrinting/Redemption/Theocracy) + Market (Coinage/Banking/Guilds/Caravan) +
> civ-özel unique (Chivalry/BeardedAxe/Ironclad/Crenellations/EliteEagle).
> Her tech bir binadan, bir çağ önkoşuluyla, sabit maliyet ve süreyle araştırılır.
> Bonuslar `TechState` üzerinden **canlı okunur** (saldırı/menzil/zırh/toplama/hız) veya
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

Ana gruplar:

- **Çağ ilerlemeleri** (`FeudalAge`, `CastleAge`, `ImperialAge`) — Town Center'da araştırılır,
  takımı bir sonraki çağa geçirir ve yeni binaları/tech'leri açar.
- **Blacksmith saldırı hatları** (Forging→IronCasting→BlastFurnace melee; Fletching/Bodkin/Bracer
  + Chemistry archer) — toplamsal saldırı.
- **Blacksmith zırh hatları** — piyade (ScaleMail→ChainMail→PlateMail), süvari eyer zırhı
  (ScaleBarding→ChainBarding→PlateBarding), okçu zırhı (Padded→Leather→Ring).
- **Kademe yükseltmeleri** (ManAtArms→Longswordsman→TwoHandedSwordsman→Champion, Crossbowman→Arbalest,
  Cavalier→Paladin, Pikeman→Halberdier, EliteSkirmisher, HeavyCamel, LightCavalry→Hussar,
  HeavyCavalryArcher, WarGalley→Galleon) — saldırı + hp + zırh, bazıları önceki kademeyi önkoşul tutar.
- **Ekonomi** (Loom, DoubleBitAxe, BowSaw, GoldMining, StoneMining, HorseCollar→HeavyPlow→CropRotation,
  Husbandry, Wheelbarrow) — toplama çarpanı, çiftlik kapasitesi, taşıma/hız.
- **University** (Masonry, Fortified, GuardTower→Keep, Ballistics, Chemistry, Architecture) — bina
  zırhı/hp, kule, mermi.
- **Manastır** (Sanctity, BlockPrinting, Redemption, Theocracy) — keşiş hp/dönüştürme.
- **Market** (Coinage, Banking, Guilds, Caravan) — ticaret/altın.
- **Civ-özel unique** (Chivalry/BeardedAxe → Franks, Ironclad/Crenellations → Teutons, EliteEagle →
  Aztecs) — yalnızca `requiredCiv` eşleşirse görünür.

Oyuncu (takım 0) araştırmayı bir binaya kuyruğa atar ve süre dolunca tamamlanır
([ResearchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L28)). AI takımları
ise `Apply` ile **anında** araştırır (kendi kaynaklarını önce düşerek)
([ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86)).

## 2. Nasıl çalışır (mekanik + formül)

### Araştırma akışı (oyuncu)

1. **Uygunluk filtresi** — bir binadaki seçilebilir tech'ler `ForBuilding(building, age, tech, civ)`
   ile listelenir: bina eşleşmeli, çağ önkoşulu sağlanmalı, henüz araştırılmamış olmalı, (varsa)
   önkoşul tech araştırılmış olmalı ve (varsa) `requiredCiv` oyuncunun medeniyetiyle eşleşmeli
   ([TechDefs.cs:158](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L158)).
2. **Çağ kapısı** — çağ ilerlemeleri "tam bir sonraki çağ" kuralına tabidir: Feudal yalnız
   Dark'tayken, Castle yalnız Feudal'dayken, Imperial yalnız Castle'dayken görünür. Diğer
   tech'ler `age >= requiredAge` ise görünür
   ([TechDefs.cs:175](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L175)).
3. **Civ kapısı** — `requiredCiv != None` ise yalnızca o medeniyetin oyuncusunda listelenir
   ([TechDefs.cs:168](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L168)).
4. **Kaynak + meşguliyet** — bina başka tech araştırmıyorsa ve kaynak yetiyorsa kaynak düşülür
   ve item kuyruğa girer ([ResearchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L28)).
5. **İlerleme + iptal/iade** — her frame `elapsed += dt`; süre dolunca `Apply`. `CancelActive`
   tam kaynağı geri verir ([ResearchSystem.cs:49](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L49)).

### Bonus uygulama mekaniği

- **Canlı okunur (eylem gerekmez):** saldırı, menzil, zırh, toplama, hız, ticaret bonusları her
  frame `TechState`'ten okunur — `AttackBonus`, `RangeBonus`, `ArmorBonus`, `GatherMult`,
  `MoveSpeedMult`, `TradeGoldMult` vb.
- **Araştırma anında uygulanır:** çağ ilerlemesi `tech.age`'i değiştirir; **hp bonusları** mevcut
  canlı birimlere geriye dönük uygulanır — `Apply` deltayı hesaplayıp yaşayan birimlerin `maxHp`
  ve `hp`'sine ekler, böylece eski ordu da anında güçlenir
  ([ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86)).

### Toplama çarpanı formülü

`GatherMult(kind)` kademeli/toplamsaldır ([TechState.cs:230](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L230)):

- **Odun** = `1 + (DoubleBitAxe ? 0.25) + (BowSaw ? 0.20)` → ikisi birlikte **×1.45**
- **Altın** = `1 + (GoldMining ? 0.15)` → **×1.15**
- **Taş** = `1 + (StoneMining ? 0.15)` → **×1.15**

> Not: Wheelbarrow artık toplama çarpanına değil, **taşıma kapasitesine** (`CarryCapacityMult ×1.25`)
> ve **köylü hızına** (`×1.1`) etki eder ([TechState.cs:187](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L187)).

### Kademeli (stacklenen) saldırı/hp/zırh formülü

Saldırı toplamsal birikir ([TechState.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L60)):

- Militia saldırı = melee hattı (Forging+2 / IronCasting+1 / BlastFurnace+2) + kademe
  (ManAtArms+1 / Longswordsman+2 / TwoHandedSwordsman+2 / Champion+2) [+ BeardedAxe+2 (Franks)]
- Archer saldırı = archer hattı (Fletching+1 / Bodkin+1 / Bracer+1 / Chemistry+1) + kademe
  (Crossbowman+2 / Arbalest+2)
- Cavalry saldırı = melee hattı + kademe (Cavalier+2 / Paladin+3)

Zırm da kademe yükseltmeleriyle artar: melee tarafı Longswordsman/Champion, Pikeman/Halberdier,
Cavalier/Paladin +1; pierce tarafı Crossbowman/Arbalest +1
([TechState.cs:156](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L156)).

## 3. Gerçek statlar (koddan)

> Tüm sayılar doğrudan `TechDefs.cs` tablosundan ve `TechState.cs` bonus tanımlarından okunmuştur
> (kod = tek doğruluk kaynağı). Tabloda **tam olarak 66 tech** vardır. Tüm girdilerde `stone = 0`
> (taş maliyeti hiçbir tech'te kullanılmıyor; F = Yemek, W = Odun, G = Altın).

### 3.1 Çağ ilerlemeleri (TownCenter)

| Tech | Görünen ad | Çağ | F | W | G | Süre | Kaynak |
|---|---|---|---|---|---|---|---|
| FeudalAge | Derebeylik Çağı | Dark | 400 | 0 | 0 | 25 | [TechDefs.cs:52](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L52) |
| CastleAge | Kale Çağı | Feudal | 600 | 0 | 200 | 35 | [TechDefs.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L53) |
| ImperialAge | İmparatorluk Çağı | Castle | 1000 | 0 | 600 | 50 | [TechDefs.cs:54](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L54) |

### 3.2 Blacksmith — saldırı hatları

| Tech | Görünen ad | Çağ | F | W | G | Süre | Önkoşul | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|
| Forging | Dövme | Feudal | 150 | 0 | 0 | 20 | — | Militia+Cavalry saldırı +2 | [TechDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L57) · [TechState.cs:25](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L25) |
| IronCasting | Demir Döküm | Castle | 220 | 0 | 120 | 28 | Forging | melee saldırı +1 | [TechDefs.cs:94](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L94) · [TechState.cs:26](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L26) |
| BlastFurnace | Yüksek Fırın | Imperial | 275 | 0 | 225 | 32 | IronCasting | melee saldırı +2 | [TechDefs.cs:95](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L95) · [TechState.cs:27](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L27) |
| Fletching | Oklama | Feudal | 100 | 0 | 50 | 20 | — | Archer saldırı +1, menzil +0.5 | [TechDefs.cs:58](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L58) · [TechState.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L29) |
| Bodkin | İğne Ucu | Castle | 150 | 0 | 100 | 25 | — | Archer saldırı +1 | [TechDefs.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L60) · [TechState.cs:30](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L30) |
| Bracer | Kol Koruması | Imperial | 200 | 0 | 175 | 30 | Bodkin | Archer saldırı +1, tüm okçu menzil +0.5 | [TechDefs.cs:107](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L107) · [TechState.cs:31](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L31), [TechState.cs:82](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L82) |

### 3.3 Blacksmith — zırh hatları

| Tech | Görünen ad | Çağ | F | W | G | Süre | Önkoşul | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|
| ScaleMail | Pul Zırh | Castle | 150 | 0 | 100 | 25 | — | Militia/Spear/Cav hp +20, piyade zırh +1 | [TechDefs.cs:59](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L59) · [TechState.cs:93](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L93), [TechState.cs:143](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L143) |
| ChainMail | Zincir Zırh | Castle | 200 | 0 | 100 | 28 | ScaleMail | piyade melee+pierce zırh +1 | [TechDefs.cs:97](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L97) · [TechState.cs:143](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L143) |
| PlateMail | Levha Zırh | Imperial | 300 | 0 | 150 | 32 | ChainMail | piyade melee+pierce zırh +2 | [TechDefs.cs:98](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L98) · [TechState.cs:143](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L143) |
| ScaleBarding | Pul Eyer Zırhı | Feudal | 150 | 0 | 0 | 22 | — | süvari (Cav/Camel/Scout) zırh +1 | [TechDefs.cs:100](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L100) · [TechState.cs:145](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L145) |
| ChainBarding | Zincir Eyer Zırhı | Castle | 250 | 0 | 150 | 28 | ScaleBarding | süvari zırh +1 | [TechDefs.cs:101](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L101) · [TechState.cs:145](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L145) |
| PlateBarding | Levha Eyer Zırhı | Imperial | 350 | 0 | 200 | 32 | ChainBarding | süvari zırh +2 | [TechDefs.cs:102](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L102) · [TechState.cs:145](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L145) |
| PaddedArcherArmor | Dolgulu Okçu Zırhı | Feudal | 100 | 0 | 50 | 22 | — | okçu sınıfı zırh +1 | [TechDefs.cs:104](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L104) · [TechState.cs:147](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L147) |
| LeatherArcherArmor | Deri Okçu Zırhı | Castle | 150 | 0 | 100 | 28 | PaddedArcherArmor | okçu zırh +1 | [TechDefs.cs:105](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L105) · [TechState.cs:147](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L147) |
| RingArcherArmor | Halka Okçu Zırhı | Imperial | 250 | 0 | 200 | 32 | LeatherArcherArmor | okçu zırh +1 | [TechDefs.cs:106](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L106) · [TechState.cs:147](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L147) |

### 3.4 Birim kademe hatları (Militia / Archer / Cavalry)

| Tech | Görünen ad | Bina | Çağ | F | W | G | Süre | Önkoşul | Etki (saldırı / hp / zırh) | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|
| ManAtArms | Piyade | Barracks | Feudal | 100 | 0 | 40 | 25 | — | +1 / +10 | [TechDefs.cs:66](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L66) · [TechState.cs:37](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L37) |
| Longswordsman | Uzun Kılıç | Barracks | Castle | 150 | 0 | 100 | 30 | ManAtArms | +2 / +15 / melee zırh +1 | [TechDefs.cs:67](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L67) · [TechState.cs:38](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L38) |
| TwoHandedSwordsman | İki Elli Kılıç | Barracks | Imperial | 150 | 0 | 120 | 32 | Longswordsman | +2 / +15 | [TechDefs.cs:68](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L68) · [TechState.cs:39](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L39) |
| Champion | Şampiyon | Barracks | Imperial | 200 | 0 | 150 | 35 | TwoHandedSwordsman | +2 / +20 / melee zırh +1 | [TechDefs.cs:69](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L69) · [TechState.cs:40](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L40) |
| Crossbowman | Arbaletçi | ArcheryRange | Castle | 150 | 0 | 100 | 30 | — | +2 / +10 / pierce zırh +1, menzil +0.5 | [TechDefs.cs:70](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L70) · [TechState.cs:44](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L44) |
| Arbalest | Arbalet | ArcheryRange | Imperial | 200 | 0 | 150 | 35 | Crossbowman | +2 / +15 / pierce zırh +1, menzil +0.5 | [TechDefs.cs:71](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L71) · [TechState.cs:45](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L45) |
| Cavalier | Ağır Süvari | Stable | Castle | 150 | 0 | 100 | 30 | — | +2 / +20 / melee zırh +1 | [TechDefs.cs:72](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L72) · [TechState.cs:42](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L42) |
| Paladin | Paladin | Stable | Imperial | 200 | 0 | 150 | 35 | Cavalier | +3 / +25 / melee zırh +1 | [TechDefs.cs:73](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L73) · [TechState.cs:43](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L43) |

### 3.5 Counter & mobil birim hatları (M2 / M4)

| Tech | Görünen ad | Bina | Çağ | F | W | G | Süre | Önkoşul | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|
| Pikeman | Mızrakçı | Barracks | Castle | 100 | 0 | 50 | 28 | — | saldırı +2, hp +15, melee zırh +1 | [TechDefs.cs:75](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L75) · [TechState.cs:47](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L47) |
| Halberdier | Teberli | Barracks | Imperial | 150 | 0 | 100 | 32 | Pikeman | saldırı +3, hp +20, melee zırh +1 | [TechDefs.cs:76](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L76) · [TechState.cs:48](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L48) |
| EliteSkirmisher | Seçkin Avcı | ArcheryRange | Imperial | 150 | 0 | 100 | 30 | — | saldırı +1, hp +10 | [TechDefs.cs:77](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L77) · [TechState.cs:49](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L49) |
| HeavyCamel | Ağır Deve | Stable | Imperial | 150 | 0 | 100 | 30 | — | saldırı +3, hp +20 | [TechDefs.cs:78](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L78) · [TechState.cs:50](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L50) |
| LightCavalry | Hafif Süvari | Stable | Castle | 150 | 0 | 50 | 25 | — | saldırı +5, hp +15 (savaşçı yapar) | [TechDefs.cs:79](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L79) · [TechState.cs:52](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L52) |
| Hussar | Hüsar | Stable | Imperial | 150 | 0 | 100 | 30 | LightCavalry | saldırı +2, hp +15 | [TechDefs.cs:80](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L80) · [TechState.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L53) |
| HeavyCavalryArcher | Ağır Atlı Okçu | Stable | Imperial | 150 | 0 | 125 | 30 | — | saldırı +2, hp +20 | [TechDefs.cs:81](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L81) · [TechState.cs:54](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L54) |
| WarGalley | Savaş Kadırgası | Dock | Castle | 150 | 0 | 50 | 28 | — | saldırı +2, hp +20 | [TechDefs.cs:82](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L82) · [TechState.cs:55](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L55) |
| Galleon | Kalyon | Dock | Imperial | 150 | 0 | 100 | 32 | WarGalley | saldırı +2, hp +30 | [TechDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L83) · [TechState.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L56) |

> Süvari/kara birimleri ek olarak `Bloodlines` (+20 hp) ve `Husbandry` (×1.1 hız) bonusundan da
> yararlanır; gemiler `Chemistry` (+1 mermi saldırı) alır.

### 3.6 Ekonomi tech'leri

| Tech | Görünen ad | Bina | Çağ | F | W | G | Süre | Önkoşul | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|
| Loom | Dokuma | TownCenter | Dark | 0 | 0 | 50 | 25 | — | köylü hp +15, zırh +1 | [TechDefs.cs:109](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L109) · [TechState.cs:118](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L118), [TechState.cs:148](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L148) |
| DoubleBitAxe | Çift Balta | LumberCamp | Feudal | 100 | 0 | 0 | 18 | — | odun toplama ×+0.25 | [TechDefs.cs:62](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L62) · [TechState.cs:236](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L236) |
| BowSaw | Tezgah Testere | LumberCamp | Castle | 150 | 0 | 100 | 25 | DoubleBitAxe | odun toplama ×+0.20 | [TechDefs.cs:110](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L110) · [TechState.cs:237](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L237) |
| GoldMining | Altın Madenciliği | MiningCamp | Feudal | 100 | 0 | 75 | 22 | — | altın toplama ×+0.15 | [TechDefs.cs:111](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L111) · [TechState.cs:240](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L240) |
| StoneMining | Taş Madenciliği | MiningCamp | Feudal | 100 | 0 | 75 | 22 | — | taş toplama ×+0.15 | [TechDefs.cs:112](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L112) · [TechState.cs:243](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L243) |
| HorseCollar | At Koşumu | Mill | Feudal | 75 | 0 | 0 | 20 | — | çiftlik kapasitesi +75 | [TechDefs.cs:85](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L85) · [TechState.cs:224](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L224) |
| HeavyPlow | Ağır Saban | Mill | Castle | 125 | 0 | 0 | 25 | HorseCollar | çiftlik kapasitesi +75 | [TechDefs.cs:86](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L86) · [TechState.cs:224](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L224) |
| CropRotation | Ekin Rotasyonu | Mill | Imperial | 250 | 0 | 100 | 28 | HeavyPlow | çiftlik kapasitesi +75 | [TechDefs.cs:113](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L113) · [TechState.cs:225](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L225) |
| Husbandry | Hayvancılık | Stable | Castle | 150 | 0 | 0 | 22 | — | süvari hızı ×1.1 | [TechDefs.cs:115](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L115) · [TechState.cs:192](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L192) |
| Wheelbarrow | El Arabası | TownCenter | Feudal | 150 | 50 | 0 | 22 | — | taşıma ×1.25, köylü hızı ×1.1 | [TechDefs.cs:63](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L63) · [TechState.cs:193](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L193), [TechState.cs:198](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L198) |

### 3.7 University tech'leri

| Tech | Görünen ad | Çağ | F | W | G | Süre | Önkoşul | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|
| Masonry | Duvar Ustalığı | Castle | 150 | 0 | 0 | 22 | — | bina melee+pierce zırh +2 | [TechDefs.cs:88](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L88) · [TechState.cs:212](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L212) |
| Fortified | Takviyeli Duvar | Imperial | 200 | 0 | 150 | 30 | — | bina melee+pierce zırh +3 | [TechDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L89) · [TechState.cs:212](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L212) |
| GuardTower | Muhafız Kulesi | Castle | 100 | 0 | 50 | 22 | — | kule saldırı +3 | [TechDefs.cs:90](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L90) · [TechState.cs:218](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L218) |
| Keep | Burç | Imperial | 150 | 0 | 100 | 28 | GuardTower | kule saldırı +4, menzil +1.5 | [TechDefs.cs:91](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L91) · [TechState.cs:218](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L218), [TechState.cs:220](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L220) |
| Ballistics | Balistik | Castle | 300 | 0 | 175 | 35 | — | mermiler hareketli hedefi daha iyi vurur (lead-fire) | [TechDefs.cs:119](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L119) · [Projectile.cs:10](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L10) |
| Chemistry | Kimya | Imperial | 300 | 0 | 200 | 40 | — | tüm mermi saldırı +1 (okçu/kule/gemi) | [TechDefs.cs:120](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L120) · [TechState.cs:34](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L34) |
| Architecture | Mimari | Castle | 300 | 0 | 0 | 35 | — | bina hp ×1.10, melee+pierce zırh +1 | [TechDefs.cs:121](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L121) · [TechState.cs:209](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L209), [TechState.cs:213](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L213) |

### 3.8 Manastır tech'leri (M7)

| Tech | Görünen ad | Çağ | F | W | G | Süre | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|
| Sanctity | Kutsallık | Castle | 120 | 0 | 0 | 30 | keşiş hp +15 | [TechDefs.cs:124](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L124) · [TechState.cs:119](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L119) |
| BlockPrinting | Matbaa | Castle | 0 | 0 | 200 | 32 | keşiş dönüştürme menzili +1.5 (2.5→4.0) | [TechDefs.cs:125](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L125) · [TechState.cs:126](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L126) |
| Redemption | Kurtarış | Castle | 0 | 0 | 475 | 35 | keşiş bina/kuşatmayı da dönüştürebilir | [TechDefs.cs:126](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L126) · [TechState.cs:130](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L130) |
| Theocracy | Teokrasi | Imperial | 0 | 0 | 200 | 40 | dönüştüren keşiş imanının yarısını korur (hızlı şarj) | [TechDefs.cs:127](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L127) · [TechState.cs:128](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L128) |

### 3.9 Market tech'leri (M8 / M6)

| Tech | Görünen ad | Çağ | F | W | G | Süre | Önkoşul | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|
| Coinage | Sikke Basımı | Castle | 0 | 0 | 200 | 30 | — | (Banking önkoşulu) | [TechDefs.cs:130](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L130) |
| Banking | Bankacılık | Imperial | 0 | 0 | 300 | 35 | Coinage | ticaret altını ×1.2 | [TechDefs.cs:131](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L131) · [TechState.cs:203](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L203) |
| Guilds | Loncalar | Imperial | 300 | 0 | 0 | 35 | — | Market al/sat farkını daraltır | [TechDefs.cs:132](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L132) · [TechState.cs:206](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L206) |
| Caravan | Kervan | Castle | 0 | 0 | 200 | 28 | — | ticaret altını ×1.5 | [TechDefs.cs:117](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L117) · [TechState.cs:203](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L203) |

> Caravan + Banking ticaret altını çarpanları **stack** olur: ×1.5 × ×1.2 = **×1.8**.

### 3.10 Civ-özel unique tech'ler (M9, `requiredCiv`)

| Tech | Görünen ad | Bina | Çağ | F | W | G | Süre | Medeniyet | Etki | Kaynak |
|---|---|---|---|---|---|---|---|---|---|---|
| EliteEagle | Seçkin Kartal | Barracks | Imperial | 200 | 0 | 100 | 35 | Aztecs | Eagle saldırı +3, hp +20 | [TechDefs.cs:135](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L135) · [TechState.cs:71](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L71), [TechState.cs:120](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L120) |
| Chivalry | Şövalyelik | Castle | Castle | 0 | 0 | 400 | 40 | Franks | süvari hp +20 | [TechDefs.cs:138](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L138) · [TechState.cs:102](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L102) |
| BeardedAxe | Sakallı Balta | Castle | Imperial | 0 | 0 | 400 | 40 | Franks | Militia hattı saldırı +2 | [TechDefs.cs:139](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L139) · [TechState.cs:41](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L41) |
| Ironclad | Zırhlı | Castle | Castle | 0 | 0 | 400 | 40 | Teutons | kuşatma (Treb/Mangonel/Ram) zırh +4 | [TechDefs.cs:140](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L140) · [TechState.cs:150](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L150) |
| Crenellations | Mazgallar | Castle | Imperial | 0 | 0 | 400 | 40 | Teutons | kule menzili +1 | [TechDefs.cs:141](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L141) · [TechState.cs:221](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L221) |

## 4. Strateji & counter

- **Çağ tempo'su:** Feudal 400 yemek/25s ile ucuz ve hızlı; erken Feudal baskını AoA'da çok agresif
  oynanabilir. Imperial 1000 yemek + 600 altın, ekonomi destekli oyuncuyu ödüllendirir.
- **Tam blacksmith hattı:** Saldırı (Forging→IronCasting→BlastFurnace = +5 melee) ve zırh
  (Scale→Chain→Plate, barding, okçu zırhı) artık AoE2'deki gibi çok seviyeli. Saldırı/zırh yarışını
  kazanan ordu ezici üstünlük sağlar; Imperial'de tüm hattı tamamlamak öncelik.
- **HP tech'leri geriye dönüktür:** Zırh/hp tech'leri ve kademe yükseltmeleri mevcut ordunu da
  anında güçlendirir ([ResearchSystem.cs:130](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L130)) —
  savaş ortasında bile değerli.
- **Counter okuması:** Rakip okçu yığıyorsa piyade tarafında PlateMail + Champion yüksek pierce zırhı
  ile hasarı emer; süvari için PlateBarding aynısını yapar. Mızrakçı→Teberli hattı süvariye, deve
  hattı süvariye karşı; detay için [./07-combat-counters.md](./07-combat-counters.md).
- **University zinciri:** Chemistry tüm mermi birimlerine +1 saldırı verir (okçu+kule+gemi tek tech);
  kule savunması GuardTower→Keep + Chemistry ile katlanır. Architecture binaları hem hp hem zırh
  olarak güçlendirir.
- **Ekonomi katmanı:** Loom (köylü hp/zırh, ucuz), odun (DoubleBitAxe+BowSaw ×1.45), altın/taş
  madenciliği, çiftlik hattı (HorseCollar→HeavyPlow→CropRotation +225 kapasite) ve Wheelbarrow/
  Husbandry hız bonusları büyüme hızını belirler.
- **Civ unique'leri:** Franks Chivalry (+20 süvari hp) ve BeardedAxe; Teutons Ironclad (kuşatma zırhı)
  ve Crenellations (kule menzili); Aztecs EliteEagle — yalnızca ilgili medeniyette Castle'da/Barracks'ta
  görünür. Detay: [./06-civilizations.md](./06-civilizations.md).
- **AI dezavantajı:** AI anında araştırır (süre yok); insan oyuncu araştırma süresi beklediği için AI
  tech yarışında avantajlıdır — erken ekonomi ile telafi et.

## 5. Çapraz bağlantılar

- [./01-game-flow-ages.md](./01-game-flow-ages.md) — çağ ilerleme tech'leri ve çağ kapıları
- [./02-units.md](./02-units.md) — tech bonuslarının uygulandığı birim tipleri
- [./03-unit-upgrades.md](./03-unit-upgrades.md) — kademe yükseltme zincirleri detayı
- [./04-buildings.md](./04-buildings.md) — tech'lerin araştırıldığı binalar + bina zırhı/hp tech'leri
- [./06-civilizations.md](./06-civilizations.md) — medeniyet tech erişimi + unique tech'ler
- [./07-combat-counters.md](./07-combat-counters.md) — saldırı/zırh tech'lerinin counter dengesine etkisi
- [./08-economy-trade.md](./08-economy-trade.md) — ekonomi/market/ticaret tech'leri
- [./09-ai-difficulty.md](./09-ai-difficulty.md) — AI'nın anında araştırma davranışı

## 6. Kod referansları (file:line, derivation)

- **Tech tablosu (66 giriş):** [TechDefs.cs:49-142](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L49).
  `static readonly TechDef[] Table` — her satır `new(...)`; doğrulama:
  `grep -c "new(TechType" TechDefs.cs` = **66**.
- **TechDef struct (üç ctor):** [TechDefs.cs:8](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L8) —
  taban, önkoşul (`requires`/`hasRequires`) [TechDefs.cs:31](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L31),
  civ-gate (`requiredCiv`) [TechDefs.cs:39](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L39).
- **Bina başına seçilebilir tech filtresi (civ dahil):** [TechDefs.cs:158](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L158) (`ForBuilding`).
- **Çağ kapısı:** [TechDefs.cs:175](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L175) (`IsAvailable`).
- **Bonus türetimleri (canlı):** saldırı [TechState.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L60),
  menzil [TechState.cs:77](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L77),
  hp [TechState.cs:91](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L91),
  zırh [TechState.cs:178](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L178),
  toplama [TechState.cs:230](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L230),
  hız [TechState.cs:187](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L187),
  ticaret [TechState.cs:203](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L203),
  bina hp/zırh [TechState.cs:209](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L209),
  kule [TechState.cs:218](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L218),
  keşiş [TechState.cs:126](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L126).
- **`Version` damgası:** [TechState.cs:17](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L17) — UI
  araştırma seti değişince ucuza fark algılar.
- **Oyuncu araştırma kuyruğu:** [ResearchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L28)
  (`Enqueue`), [ResearchSystem.cs:60](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L60) (`Tick`),
  iptal/iade [ResearchSystem.cs:49](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L49).
- **Tech uygulama + geriye dönük hp:** [ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86) (`Apply`).
- **`TechType` enum (66 üye):** [GameTypes.cs:83](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L83).

## 7. AoE2 farkı (reference köprü)

Tam AoE2 teknoloji listesi için: [../reference/04-tech-tree.md](../reference/04-tech-tree.md).

Başlıca farklar:

- **Artık çok daha kapsamlı ağaç:** AoA'da 66 tech var — Blacksmith saldırı/zırh hatları, ekonomi
  (Loom/madencilik/çiftlik), University (Ballistics/Chemistry/Architecture/Tower), Manastır, Market
  ve civ-özel unique'ler dahil. Önceki sürümdeki büyük boşlukların çoğu kapandı.
- **Stat modeli farkı:** Bazı tech'ler AoE2'deki zırhın yanında **hp** de ekler (ScaleMail +20 hp +
  zırh +1). Kademe yükseltmeleri hem hp hem zırh hem saldırı verir; AoE2'de bunlar ayrı tech'ler.
- **Forging +2:** AoA'da Forging tek başına +2 melee (AoE2'de +1); IronCasting +1, BlastFurnace +2
  ([TechState.cs:25](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L25)).
- **Daha ucuz/hızlı çağlar:** AoA Feudal 400Y/25s (AoE2: 500Y/130s), Castle 600Y+200A/35s
  (AoE2: 800Y+200A/160s), Imperial 1000Y+600A/50s (AoE2: 1000Y+800A/190s).
- **Civ-gate tek-medeniyet:** AoA unique tech'leri tek `requiredCiv` alanı ile kapılı; AoE2'nin
  Castle Unique Tech + ortak tech matrisinden daha sade.
- **Hâlâ eksik:** Squires (piyade hız), Conscription (üretim hızı), Thumb Ring/Parthian Tactics
  (okçu), Siege Engineers, Atonement/Faith/Heresy (monk) ve çoğu civ'in tam unique tech çifti.

## 8. Eksikler / Yapılacaklar

| ID-aday | Sınıf | Eksik | AoE2-ref | Efor |
|---|---|---|---|---|
| SPDT | Hız tech | Squires (piyade hız), Conscription (üretim hızı) yok; Husbandry/Wheelbarrow var | reference §Stable/Barracks | Orta |
| ARCT | Okçu tech | Thumb Ring (atış hızı/doğruluk), Parthian Tactics (atlı okçu zırh+saldırı) yok | reference §ArcheryRange | Orta |
| SIGE | Kuşatma tech | Siege Engineers (menzil/hasar), kuşatma kademe hattı yok | reference §Siege Workshop | Yüksek |
| MONT | Monk tech | Atonement, Faith, Heresy, Illumination yok (Sanctity/BlockPrinting/Redemption/Theocracy var) | reference §Monastery | Orta |
| CIVU | Civ unique çifti | Çoğu medeniyetin 2. unique tech'i + diğer civ'lerin unique'leri eksik | reference §Civilizations | Yüksek |
| BLST | Ballistics modeli | Ballistics zaten lead-fire ile karşılanıyor; ayrı stat etkisi yok (denge/doğrulama) | reference §University | Düşük |
| AIRS | AI araştırma dengesi | AI tech'i anında alıyor (süre yok); insan oyuncuya karşı adaletsiz tempo | — (denge) | Düşük |
