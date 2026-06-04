# Ekonomi & Ticaret — AoA Wiki

> Age of Arena ekonomisi dört kaynak (Food, Wood, Gold, Stone) üzerine kurulu: köylüler düğümlerden toplar, en yakın depo noktasına taşır ve takım defterine yatırır. Toplama hızı artık **kaynak türüne göre değişir** (Food en hızlı, Wood en yavaş), taşıma kapasitesi Wheelbarrow ile büyür ve **stone tam bir ekonomi kaynağı** olarak (başlangıç 200) Kale/Üniversite/kule maliyetlerini besler. Üstüne dinamik fiyatlı **Market** (Guilds ile daralan spread), takımlar arası **haraç** (Coinage ile vergisiz), mesafe-bazlı **Trade Cart** ticareti (Caravan/Banking ile artan) ve pasif gelir sağlayan **Relik** sistemi gelir.
>
> **Kod kaynağı:** [GatherSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs), [ResourceManager.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs), [ResourceNode.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs), [ResourceFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs), [MarketSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs), [TributeSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs), [TradingSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs), [TechState.cs](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs), [RelicSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs), [RelicEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs)

## 1. Ne olduğu

Ekonomi, AoA'da bütün üretim ve askeri gücün motorudur. Sistem dört alt parçadan oluşur:

- **Toplama (Gather):** Köylüler ağaç (Wood), altın madeni (Gold), taş madeni (Stone), çiftlik/meyve/balık (Food) düğümlerinden kaynak çıkarır, en yakın depoya taşır, takım defterine (`ResourceManager`) yatırır. Toplama hızı **kaynak türüne göre** farklıdır (M6/GRATE); taşıma kapasitesi tech ile artar (M6/RPCT).
- **Kaynak defteri (Ledger):** Her takımın `ResourceManager`'ı food/wood/gold/stone + nüfus tutar. **Stone** artık 200 ile başlar ve Kale/Üniversite/kule gibi binaların maliyetini besler (M8/STONE). Tüm satın almalar buradan düşülür.
- **Market:** Arz/talep modelli, dalgalanan fiyatlı kaynak takası. Fazla kaynağı altına çevirmeye veya eksik kaynağı altınla almaya yarar; Guilds tech'i spread'i daraltır.
- **Haraç (Tribute):** Takımlar arası kaynak transferi. Coinage araştırılmamışsa %30 vergi kesilir; araştırılmışsa vergisiz (M8/TRIB).
- **Ticaret (Trade Cart):** Kendi Market'i ile düşman/nötr Market arasında gidip gelerek mesafe orantılı altın üreten birim; Caravan/Banking tech'leri verimi artırır.
- **Relik:** Harita üzerindeki nötr kontrol noktaları; ele geçirilince pasif altın akışı sağlar.

Bu sayfa, AoE2'nin ekonomi modelinin AoA'daki uyarlamasını koddan birebir doğrulanmış sayılarla anlatır.

## 2. Nasıl çalışır (mekanik + formül)

### Toplama döngüsü
Köylü dört durumlu bir makine ile çalışır (`GatherSystem.StepUnit`):

1. **Moving** — NavMeshAgent düğüme yürür; menzile girince `Gathering`'e geçer. Menzil kaynağa göre: Wood **1.8**, Gold/Stone **2.2**, diğer (Food) **1.4** dünya birimi.
2. **Gathering** — her tick'te `GatherRate` (1 birim) toplanır. **Tick aralığı kaynak türüne göre değişir** (M6/GRATE, `GatherIntervalFor`): Food **0.5 sn** (en hızlı) < Gold/Stone **0.6 sn** < Wood **0.7 sn** (en yavaş). Yani efektif toplama hızı Wood < Gold/Stone < Food. `carrying.amount >= CarryCapacityFor(v)` olunca veya düğüm bitince geri dönülür.
3. **ReturningToDropoff** — en yakın uygun depoya gidilir; menzile (depo yarıçapı + 2.2) girince yatırma yapılır.
4. Yatırma sonrası: düğümde yer varsa devam, yoksa aynı türde en yakın düğüme geçilir.

**Taşıma kapasitesi formülü** (M6/RPCT, `GatherSystem.CarryCapacityFor`, L43-48):
```
carryCap = round( 10 × CarryCapacityMult ) + CarryBonus
```
- Taban **10** birim. `CarryCapacityMult` — Wheelbarrow ×**1.25** ([TechState.cs:198](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L198)). Wheelbarrow ayrıca köylü hızını ×1.1 yapar ([TechState.cs:193](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L193)). `CarryBonus` = 0 (gelecekteki Hand Cart kademesi için ayrılmış, L200).

**Yatırma çarpanı formülü** (`GatherSystem.StepUnit`, L123-131):
```
gained = round( carrying.amount × techMult(kind) × civMult(kind) × teamFoodBonus × aiEcoMult )
```
- `techMult` — kaynak türüne özel toplama tech'leri → `teamTech.GatherMult(kind)` ([TechState.cs:230-247](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L230)): Wood için DoubleBitAxe +%25 ve BowSaw +%20; Gold için GoldMining +%15; Stone için StoneMining +%15. (Wheelbarrow artık yatırmayı çarpmaz — taşıma kapasitesine taşındı.)
- `civMult` — medeniyet bonusu (örn. Franks +%20 food) → `CivGatherMult`.
- `teamFoodBonus` — takım paylaşımlı food bonusu (yalnız food yatırmada).
- `aiEcoMult` — zorluk-bazlı AI ekonomi çarpanı (yalnız AI takımlar; oyuncu = 1×).

Temel hızlar (yatırma anında çarpanlar uygulanır): Food ≈ **1 / 0.5 = 2.0 birim/sn**, Gold/Stone ≈ **1.67**, Wood ≈ **1.43** (taşıma + yürüme süresi gecikmesi hariç).

### Çiftlik (Farm) mekaniği
Çiftlik bir bina olarak kalır, tükenmez şekilde **yenilenir** (`ResourceFactory.FarmField`):
- Başlangıç food: **300** (varsayılan parametre).
- Tükenince sahibinin defterinden **60 wood** harcanarak yeniden tohumlanır (`ResourceNode.Update`, L44-57).
- **Farm kapasite tech'leri** (M8/FARM): Horse Collar, Heavy Plow, Crop Rotation — her biri yeniden tohumlanan çiftliğin food kapasitesine **+75** ekler (`TechState.FarmCapacityBonus`, [TechState.cs:224-225](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L224)). Üçü birden = +225 (300 → 525). Bonus reseed anında uygulanır.
- Boştaki çiftlik **2 food/sn** çürür (decay) → köylü atamayı teşvik eder (Franks: yarı hızda çürür, `farmDecayMult` 0.5).
- Eş zamanlı toplayıcı sınırı (`gathererCap`): **4**.

### Market fiyatı (dalgalanma)
`MarketSystem` üç takas edilebilir kaynağı (Food/Wood/Stone; Gold takas edilemez) için ayrı sell/buy oranı tutar. Bir batch = **100 birim**.

```
SellGold(k) = round(100 × sellRate[k])     // sat → altın
BuyCost(k)  = round(100 × buyRate[k])      // al  → altın maliyeti
```
- Temel oranlar: sellRate **0.7**, buyRate **1.3**.
- Her **Sell** → o kaynağın sellRate'i **0.05** düşer (taban **0.3**); buyRate de hafif geri çekilir ama spread korunur.
- Her **Buy** → buyRate **0.05** artar (tavan **2.5**); sellRate hafif yükselir.
- Her saniye oranlar tabana doğru **0.002/sn** sürüklenir (`Tick`, drift recovery).
- Spread her zaman ≥ 0.2 tutulur → takas asla bedava değildir.
- **Guilds** (oyuncu Market tech'i): sell oranını **+0.10**, buy oranını **−0.10** kaydırır → spread daralır, daha iyi satış + daha ucuz alış (`GuildsAdj`, [MarketSystem.cs:26-33](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L26)).

### Haraç (Tribute)
`TributeSystem.Tribute` bir takımdan diğerine kaynak transfer eder (M8/TRIB, [TributeSystem.cs:21-39](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs#L21)):
```
received = Coinage ? amount : round( amount × (1 − 0.30) )
```
- Gönderen daima tam `amount` öder; alıcı vergiden sonrasını alır.
- **Coinage** araştırılmamışsa **%30 vergi** kesilir (`TaxRate`, L13). Coinage varsa **vergisiz** (AoE2-stili) — vergi gönderenin Coinage tech'ine bakar.
- Geçersiz takım, sıfır/negatif miktar veya gönderen yetersizse no-op (false döner).

### Trade Cart geliri
`TradingSystem` Trade Cart birimlerini sürer. Cart kendi Market'inden çıkar, en yakın **düşman/nötr** Market'e gider, dönüşte altın yatırır:
```
earned = round( max(8, tripDist × 0.18) × TradeGoldMult )
```
- `0.18` = dünya-birimi başına altın (TradeGoldPerUnit), `8` = MinGold tabanı.
- `tripDist` = home Market ile target Market arası düz mesafe. Uzak market = lineer olarak daha çok altın.
- `TradeGoldMult` — Caravan ×**1.5** ve Banking ×**1.2** (çarpışırlar; ikisi birden = ×1.8) ([TechState.cs:203](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L203)). Aynı mesafe-bazlı rota, su haritası geldiğinde Trade Cog (deniz ticareti) için de yeniden kullanılır.
- Yatırma menzili (DepositRange): **4** birim.

### Relik geliri ve ele geçirme
`RelicSystem` her kare tüm birimleri tüm reliklere mesafe (CaptureRange **3.5**) için tarar. `RelicEntity.UpdateCapture`:
- Tek baskın takım (beraberlik = contested → kimse alamaz) varsa `captureProgress` artar.
- **5 sn** (CaptureSeconds) kesintisiz baskınlık → relik o takıma geçer.
- Kontrol edilmezse ilerleme **1.5/sn** (DecayRate) erir.
- Kontrol edilen relik **0.5 altın/sn** (GoldPerSecond) pasif gelir verir (kesirler birikip tam altın olarak yatar).

## 3. Gerçek statlar (koddan)

> Not: Bu kategori için ayrı stat JSON sağlanmadığından, tüm sayılar tek doğruluk kaynağı olarak doğrudan kaynak koddan (const tanımları) alınmış ve satır bazında teyit edilmiştir.

### Toplama parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| GatherInterval — Food (en hızlı) | 0.5 sn | [GatherSystem.cs:35](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L35) |
| GatherInterval — Gold | 0.6 sn | [GatherSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L36) |
| GatherInterval — Stone | 0.6 sn | [GatherSystem.cs:37](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L37) |
| GatherInterval — Wood (en yavaş) | 0.7 sn | [GatherSystem.cs:38](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L38) |
| GatherRate (tick başı) | 1 | [GatherSystem.cs:29](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L29) |
| CarryCapacity (taban) | 10 | [GatherSystem.cs:12](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L12) |
| DropoffRange | 2.2 | [GatherSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L13) |
| Gather menzili — Wood | 1.8 | [GatherSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L21) |
| Gather menzili — Gold | 2.2 | [GatherSystem.cs:22](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L22) |
| Gather menzili — Stone | 2.2 | [GatherSystem.cs:23](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L23) |
| Gather menzili — diğer (Food) | 1.4 | [GatherSystem.cs:24](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L24) |

### Başlangıç kaynakları (ledger)
| Kaynak | Başlangıç | Kaynak |
|---|---|---|
| Food | 200 | [ResourceManager.cs:11](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L11) |
| Wood | 200 | [ResourceManager.cs:12](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L12) |
| Gold | 100 | [ResourceManager.cs:13](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L13) |
| Stone (M8/STONE) | 200 | [ResourceManager.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L14) |
| Başlangıç popCap | 5 | [ResourceManager.cs:17](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L17) |

### Düğüm stokları
| Düğüm | Stok | Kaynak |
|---|---|---|
| Tree (Wood) | 100 | [ResourceFactory.cs:12](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L12) |
| GoldMine | 800 | [ResourceFactory.cs:13](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L13) |
| StoneMine | 600 | [ResourceFactory.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L14) |
| BerryBush (Food) | 200 | [ResourceFactory.cs:175](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L175) |
| FishPond (Food) | 250 | [ResourceFactory.cs:190](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L190) |
| FarmField başlangıç food | 300 | [ResourceFactory.cs:141](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L141) |
| Farm yeniden tohum maliyeti | 60 wood | [ResourceFactory.cs:149](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L149) |
| Farm decay | 2 food/sn | [ResourceFactory.cs:150](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L150) |
| Farm gathererCap | 4 | [ResourceFactory.cs:146](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L146) |
| FishPond gathererCap | 3 | [ResourceFactory.cs:191](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L191) |
| Varsayılan gathererCap | 6 | [ResourceNode.cs:13](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs#L13) |

### Ekonomi tech çarpanları (TechState)
| Tech | Etki | Kaynak |
|---|---|---|
| DoubleBitAxe | Wood gather +%25 | [TechState.cs:236](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L236) |
| BowSaw | Wood gather +%20 | [TechState.cs:237](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L237) |
| GoldMining | Gold gather +%15 | [TechState.cs:240](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L240) |
| StoneMining | Stone gather +%15 | [TechState.cs:243](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L243) |
| Wheelbarrow | Taşıma ×1.25 + köylü hızı ×1.1 | [TechState.cs:193](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L193), [198](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L198) |
| Loom | Köylü +15 hp, +1 zırh | [TechState.cs:118](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L118), [148](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L148) |
| HorseCollar / HeavyPlow / CropRotation | Farm kapasite +75 (her biri) | [TechState.cs:224-225](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L224) |
| Caravan | Trade cart altını ×1.5 | [TechState.cs:203](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L203) |
| Banking | Trade cart altını ×1.2 (Caravan ile çarpışır) | [TechState.cs:203](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L203) |
| Guilds | Market spread daraltır (sell +0.10 / buy −0.10) | [MarketSystem.cs:26](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L26) |
| Coinage | Haraç vergisiz | [TributeSystem.cs:33](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs#L33) |

### Haraç (Tribute) parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| TaxRate (Coinage yoksa) | %30 | [TributeSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs#L13) |
| Coinage varsa vergi | %0 (vergisiz) | [TributeSystem.cs:33-34](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs#L33) |

### Market parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| Batch (işlem başı birim) | 100 | [MarketSystem.cs:15](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L15) |
| BaseSell oranı | 0.7 | [MarketSystem.cs:17](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L17) |
| BaseBuy oranı | 1.3 | [MarketSystem.cs:18](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L18) |
| PriceShift (işlem başı) | 0.05 | [MarketSystem.cs:19](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L19) |
| DriftRate (geri sürükleme) | 0.002 /sn | [MarketSystem.cs:20](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L20) |
| MinSell tabanı | 0.3 | [MarketSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L21) |
| MaxBuy tavanı | 2.5 | [MarketSystem.cs:22](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L22) |
| GuildsAdjust (spread daraltma) | 0.10 /yön | [MarketSystem.cs:26](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L26) |

### Trade Cart parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| TradeGoldPerUnit | 0.18 altın/birim | [TradingSystem.cs:12](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L12) |
| MinGold (taban) | 8 | [TradingSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L13) |
| DepositRange | 4 | [TradingSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L14) |
| TradeGoldMult (Caravan ×1.5, Banking ×1.2) | uygulanır | [TradingSystem.cs:59](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L59) |

### Relik parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| CaptureRange | 3.5 | [RelicSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L14) |
| CaptureSeconds (ele geçirme) | 5 sn | [RelicEntity.cs:24](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L24) |
| DecayRate (ilerleme erimesi) | 1.5 /sn | [RelicEntity.cs:25](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L25) |
| GoldPerSecond (pasif gelir) | 0.5 /sn | [RelicEntity.cs:26](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L26) |

## 4. Strateji & counter

- **Depoyu kaynağa yakın kur.** Toplama hızı kaynağa göre değişir (Food 2.0 > Gold/Stone 1.67 > Wood 1.43 birim/sn) ve yürüme + taşıma süresi efektif verimi ayrıca düşürür. Lumber/Mining Camp'i ağaç hattına / madene bitişik kur. Wood en yavaş toplandığı için odun depolarını mutlaka hatta yapıştır.
- **Wheelbarrow erken al.** Taşıma kapasitesini ×1.25 (10→12-13 birim) ve köylü hızını ×1.1 yapar — her iki etki yürüme gecikmesini azaltıp efektif verimi yükseltir. Wood/Gold/Stone için kaynağa özel tech'leri (DoubleBitAxe, BowSaw, GoldMining, StoneMining) eklemek yatırma başına +%15-45 verir.
- **Stone'u yönet.** Stone artık 200 ile başlar ve Kale/Üniversite/kule/duvar maliyetlerini besler. Erken stone yatırımı savunma/Kale çağı planına bağlı; gereksiz köylüyü madende tutma, StoneMining ile +%15 al.
- **Çiftlik = sonsuz food ama wood pompası.** Her tükeniş 60 wood yer ve boştaki çiftlik 2 food/sn çürür. Çiftlikleri ancak köylü atayacaksan yap; aksi halde wood'u boşa harcarsın. Horse Collar/Heavy Plow/Crop Rotation kapasiteyi +75'er artırarak reseed sıklığını (wood harcamasını) düşürür. Erken oyunda BerryBush (200) / FishPond (250) daha ucuz food sağlar.
- **Market arbitrajı.** Fiyat dalgalı: tek seferde çok satarsan sellRate 0.3 tabanına kadar düşer (kaynak başına gittikçe az altın). Küçük partiler hâlinde sat, drift'in fiyatı toparlamasını bekle. Guilds spread'i daraltır → satışta daha çok, alışta daha az altın. Acil altın ihtiyacında stone/wood fazlasını sat; uzun vadede madene köylü daha verimli.
- **Haraç + Coinage.** Müttefik ekonomisini desteklerken Coinage'siz transferin %30'u buharlaşır. Coinage araştırılınca haraç vergisiz olur — yoğun haraç planlıyorsan önce Coinage al.
- **Trade Cart = güvenli uzun yol.** Gelir mesafeyle lineer (`tripDist × 0.18`). Uzak düşman Market'i hedeflemek karlı ama yol boyunca cart savunmasız; counter olarak düşman ticaret hattına süvari baskını at.
- **Relik kontrolü = sızan ama bedava altın.** 0.5 altın/sn düşük görünür ama birden çok relik + uzun maç birikir. Relikler IDamageable değil — yok edilemez, sadece kapılır. Counter: relik alanına kalabalık birim gönder (beraberlik → contested → kimse alamaz, kapışı durdurursun).

## 5. Çapraz bağlantılar

- Toplama yükseltmeleri (DoubleBitAxe, Wheelbarrow) ve farm tech'leri: [Teknoloji Ağacı](./05-tech-tree.md)
- Medeniyet toplama bonusları (Franks +%20 food, Britons +%15 wood vb.): [Medeniyetler](./06-civilizations.md)
- Trade Cart birimi, köylü, Monk birim statları: [Birimler](./02-units.md)
- Market, Lumber/Mining Camp, Mill, Monastery binaları ve depo kuralları: [Binalar & Garnizon](./04-buildings.md)
- Relik tabanlı zafer koşulu: [Zafer Koşulları](./10-victory-objectives.md)
- Kaynak HUD'u, Market sat/al hotkey'leri: [Kontroller & UI](./11-controls-ui-feedback.md)
- Çağ ilerlemesinin kaynak maliyetleri: [Oyun Akışı & Çağlar](./01-game-flow-ages.md)

## 6. Kod referansları (file:line, derivation)

- **Toplama makinesi:** [GatherSystem.cs:74-152](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L74) — `StepUnit` 4 durumlu FSM (Moving/Gathering/ReturningToDropoff). Kind-bazlı tick aralığı `GatherIntervalFor` L33-40, yatırma çarpanı L123-131.
- **Taşıma kapasitesi:** [GatherSystem.cs:43-48](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L43) — `CarryCapacityFor`, taban 10 × `CarryCapacityMult` + `CarryBonus`.
- **Civ toplama çarpanı:** [GatherSystem.cs:218-230](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L218) — `CivGatherMult`, food/wood/gold için ayrı bonus.
- **Depo seçimi:** [GatherSystem.cs:186-200](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L186) — `NearestDropoff`, `BuildingDefs.AcceptsDropoff` ile tip filtresi.
- **Ekonomi tech'leri:** [TechState.cs:198-247](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L198) — `CarryCapacityMult`, `TradeGoldMult`, `HasGuilds`, `FarmCapacityBonus`, `GatherMult(kind)`.
- **Kaynak defteri:** [ResourceManager.cs:11-17](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L11) başlangıç değerleri (stone=200); `Gain`/`CanAfford`/`Deduct` L30-53.
- **Düğüm + yenilenme/çürüme:** [ResourceNode.cs:39-71](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs#L39) — `Update` auto-reseed (renewable + wood harca + FarmCapacityBonus) ve decay; `Take` L71.
- **Düğüm fabrikası:** [ResourceFactory.cs:12-14](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L12) stok const'ları; FarmField L141-154, BerryBush L159-176, FishPond L179-193.
- **Market:** [MarketSystem.cs:46-67](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L46) — `Sell`/`Buy` fiyat kayması; `Tick` drift L36-43; Guilds spread L26-33.
- **Haraç:** [TributeSystem.cs:21-39](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs#L21) — `Tribute` %30 vergi / Coinage vergisiz transfer.
- **Ticaret:** [TradingSystem.cs:30-67](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L30) — `StepCart` round-trip; gelir formülü L51-60 (TradeGoldMult uygulanır).
- **Relik:** [RelicEntity.cs:45-94](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L45) — `UpdateCapture` baskınlık/contested/decay/passive gold; [RelicSystem.cs:17-41](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L17) proximity tarama.

## 7. AoE2 farkı (reference köprü)

Tam karşılaştırma: [AoE2 Ekonomi & Ticaret Referansı](../reference/05-economy-trade.md)

Öne çıkan farklar:

- **Başlangıç stone:** AoE2'de 200; AoA'da artık **200** ([ResourceManager.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L14), M8/STONE). Stone tam bir kaynak: madenden toplanır, StoneMining ile +%15, Kale/Üniversite/kule/duvar maliyetlerini besler. (Eski Three.js sürümünde stone yoktu.)
- **Çiftlik modeli:** AoE2'de farm sabit kapasite (250→380 tech'le) ve tükenince elle yeniden kuyruğa alınır. AoA'da farm **bina olarak kalır ve 60 wood ile otomatik yenilenir** + 2 food/sn çürür. Kapasite tech'leri eklendi (M8/FARM): Horse Collar/Heavy Plow/Crop Rotation her biri +75 (300→525).
- **Toplama verimi:** AoE2 kaynak başına farklı dakika/birim oranları (odun 25, altın 27...) kullanır. AoA artık **kaynağa göre farklı tick aralığı** uyguluyor (M6/GRATE): Food 0.5s, Gold/Stone 0.6s, Wood 0.7s → Wood < Gold/Stone < Food. Üstüne tech `GatherMult(kind)` + civ `CivGatherMult` çarpanları biner. Wheelbarrow taşıma kapasitesini (×1.25) + köylü hızını (×1.1) artırır (M6/RPCT); Hand Cart kademesi (CarryBonus) henüz boş.
- **Market:** Konsept aynı (dalgalı fiyat), ama AoA basitleştirilmiş lineer kayma (0.05/işlem, 0.002/sn drift) kullanır; AoE2'nin %92 coin-return komisyonu ve daha karmaşık eğrisi yoktur. Guilds spread daraltma eklendi.
- **Trade Cart:** İkisi de mesafe-orantılı. AoA katsayısı `0.18 altın/birim` + 8 taban + Caravan/Banking çarpanı; AoE2 ~0.46 ve tile-bazlı, ayrıca min 5 tile mesafe şartı var (AoA'da min mesafe şartı yok).
- **Relik:** AoE2'de relik **Monk ile fiziksel taşınıp Monastery'ye konur**, 30 altın/dk üretir. AoA'da relik **yerinde durur ve birim yakınlığıyla ele geçirilir** (5 sn baskınlık), 0.5 altın/sn üretir — Monk/Monastery taşıma akışı yok.
- **Haraç (Tribute):** İkisinde de %30 vergiyle transfer var (M8/TRIB, [TributeSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs)). Coinage araştırılınca vergisiz olur — AoE2 ile aynı davranış.

## 8. Eksikler / Yapılacaklar

> Tamamlananlar (M6/M8): **TRIB** (haraç %30 vergi / Coinage vergisiz — [TributeSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/TributeSystem.cs)), **FARM** (Horse Collar/Heavy Plow/Crop Rotation +75 kapasite — [TechState.cs:224](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L224)), **GRATE** (kind-bazlı toplama hızı — [GatherSystem.cs:33](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L33)), **RPCT** (Wheelbarrow taşıma kapasitesi — [TechState.cs:198](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L198)), **STONE** (stone=200 ekonomisi).

| ID-aday | Sınıf | Eksik | AoE2-ref | Efor |
|---|---|---|---|---|
| MONK | RelicSystem/UnitEntity | Relik fiziksel taşıma (Monk pickup → Monastery deposit) akışı yok; relik yerinde kapılıyor | reference §Relik Toplama Prosedürü | Yüksek |
| HUNT | ResourceFactory/UnitEntity | Geyik (Deer) / domuz (Boar) gibi hareketli/avlanan food kaynağı yok | reference §Kaynak Haritası (Deer/Boar) | Yüksek |
| HCRT | TechState/GatherSystem | Hand Cart kademesi yok (`CarryBonus` = 0); ikinci taşıma yükseltmesi boş | reference §Wheelbarrow/Hand Cart | Düşük |
| MFEE | MarketSystem | Market komisyonu (AoE2 ~%92 coin-return) modellenmemiş; AoA sadece spread kullanır | reference §Coin Return Formula | Düşük |
| TSEA | TradingSystem | Trade Cog (deniz ticareti, Dock→Dock) gerçek su rotası yok (StepCart yeniden kullanılacak) | reference §Trade Cog | Yüksek |
