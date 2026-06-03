# Ekonomi & Ticaret — AoA Wiki

> Age of Arena ekonomisi dört kaynak (Food, Wood, Gold, Stone) üzerine kurulu: köylüler düğümlerden toplar, en yakın depo noktasına taşır ve takım defterine yatırır. Üstüne dinamik fiyatlı **Market**, mesafe-bazlı **Trade Cart** ticareti ve pasif gelir sağlayan **Relik** sistemi gelir.
>
> **Kod kaynağı:** [GatherSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs), [ResourceManager.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs), [ResourceNode.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs), [ResourceFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs), [MarketSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs), [TradingSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs), [RelicSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs), [RelicEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs)

## 1. Ne olduğu

Ekonomi, AoA'da bütün üretim ve askeri gücün motorudur. Sistem dört alt parçadan oluşur:

- **Toplama (Gather):** Köylüler ağaç (Wood), altın madeni (Gold), taş madeni (Stone), çiftlik/meyve/balık (Food) düğümlerinden kaynak çıkarır, en yakın depoya taşır, takım defterine (`ResourceManager`) yatırır.
- **Kaynak defteri (Ledger):** Her takımın `ResourceManager`'ı food/wood/gold/stone + nüfus tutar. Tüm satın almalar buradan düşülür.
- **Market:** Arz/talep modelli, dalgalanan fiyatlı kaynak takası. Fazla kaynağı altına çevirmeye veya eksik kaynağı altınla almaya yarar.
- **Ticaret (Trade Cart):** Kendi Market'i ile düşman/nötr Market arasında gidip gelerek mesafe orantılı altın üreten birim.
- **Relik:** Harita üzerindeki nötr kontrol noktaları; ele geçirilince pasif altın akışı sağlar.

Bu sayfa, AoE2'nin ekonomi modelinin AoA'daki uyarlamasını koddan birebir doğrulanmış sayılarla anlatır.

## 2. Nasıl çalışır (mekanik + formül)

### Toplama döngüsü
Köylü dört durumlu bir makine ile çalışır (`GatherSystem.StepUnit`):

1. **Moving** — NavMeshAgent düğüme yürür; menzile girince `Gathering`'e geçer. Menzil kaynağa göre: Wood **1.8**, Gold/Stone **2.2**, diğer (Food) **1.4** dünya birimi.
2. **Gathering** — her **0.6 sn**'lik tick'te `GatherRate` (1 birim) toplanır. `carrying.amount >= 10` (CarryCapacity) olunca veya düğüm bitince geri dönülür.
3. **ReturningToDropoff** — en yakın uygun depoya gidilir; menzile (depo yarıçapı + 2.2) girince yatırma yapılır.
4. Yatırma sonrası: düğümde yer varsa devam, yoksa aynı türde en yakın düğüme geçilir.

**Yatırma çarpanı formülü** (`GatherSystem.StepUnit`, L101-103):
```
gained = round( carrying.amount × techMult(kind) × civMult(kind) )
```
- `techMult` — araştırılmış toplama yükseltmeleri (DoubleBitAxe, Wheelbarrow vb.) → `teamTech.GatherMult`.
- `civMult` — medeniyet bonusu (örn. Franks +%20 food) → `CivGatherMult`.

Yani efektif toplama hızı = `(GatherRate / GatherInterval) × techMult × civMult` (taşıma + yürüme süresi gecikmesi hariç). Temel hız ≈ **1 / 0.6 = 1.67 birim/sn** (yatırma anında çarpanlar uygulanır).

### Çiftlik (Farm) mekaniği
Çiftlik bir bina olarak kalır, tükenmez şekilde **yenilenir** (`ResourceFactory.FarmField`):
- Başlangıç food: **300** (varsayılan parametre).
- Tükenince sahibinin defterinden **60 wood** harcanarak `maxAmount`'a geri doldurulur (`ResourceNode.Update`).
- Boştaki çiftlik **2 food/sn** çürür (decay) → köylü atamayı teşvik eder.
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

### Trade Cart geliri
`TradingSystem` Trade Cart birimlerini sürer. Cart kendi Market'inden çıkar, en yakın **düşman/nötr** Market'e gider, dönüşte altın yatırır:
```
earned = round( max(8, tripDist × 0.18) )
```
- `0.18` = dünya-birimi başına altın (TradeGoldPerUnit), `8` = MinGold tabanı.
- `tripDist` = home Market ile target Market arası düz mesafe. Uzak market = lineer olarak daha çok altın.
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
| GatherInterval (tick) | 0.6 sn | [GatherSystem.cs:12](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L12) |
| GatherRate (tick başı) | 1 | [GatherSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L13) |
| CarryCapacity | 10 | [GatherSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L14) |
| DropoffRange | 2.2 | [GatherSystem.cs:15](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L15) |
| Gather menzili — Wood | 1.8 | [GatherSystem.cs:23](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L23) |
| Gather menzili — Gold | 2.2 | [GatherSystem.cs:24](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L24) |
| Gather menzili — Stone | 2.2 | [GatherSystem.cs:25](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L25) |
| Gather menzili — diğer (Food) | 1.4 | [GatherSystem.cs:26](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L26) |

### Başlangıç kaynakları (ledger)
| Kaynak | Başlangıç | Kaynak |
|---|---|---|
| Food | 200 | [ResourceManager.cs:11](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L11) |
| Wood | 200 | [ResourceManager.cs:12](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L12) |
| Gold | 100 | [ResourceManager.cs:13](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L13) |
| Stone | 0 | [ResourceManager.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L14) |
| Başlangıç popCap | 5 | [ResourceManager.cs:17](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L17) |

### Düğüm stokları
| Düğüm | Stok | Kaynak |
|---|---|---|
| Tree (Wood) | 100 | [ResourceFactory.cs:12](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L12) |
| GoldMine | 800 | [ResourceFactory.cs:13](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L13) |
| StoneMine | 600 | [ResourceFactory.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L14) |
| BerryBush (Food) | 200 | [ResourceFactory.cs:130](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L130) |
| FishPond (Food) | 250 | [ResourceFactory.cs:145](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L145) |
| FarmField başlangıç food | 300 | [ResourceFactory.cs:96](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L96) |
| Farm yeniden tohum maliyeti | 60 wood | [ResourceFactory.cs:104](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L104) |
| Farm decay | 2 food/sn | [ResourceFactory.cs:105](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L105) |
| Farm gathererCap | 4 | [ResourceFactory.cs:103](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L103) |
| FishPond gathererCap | 3 | [ResourceFactory.cs:146](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L146) |
| Varsayılan gathererCap | 6 | [ResourceNode.cs:13](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs#L13) |

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

### Trade Cart parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| TradeGoldPerUnit | 0.18 altın/birim | [TradingSystem.cs:12](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L12) |
| MinGold (taban) | 8 | [TradingSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L13) |
| DepositRange | 4 | [TradingSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L14) |

### Relik parametreleri
| Parametre | Değer | Kaynak |
|---|---|---|
| CaptureRange | 3.5 | [RelicSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L14) |
| CaptureSeconds (ele geçirme) | 5 sn | [RelicEntity.cs:24](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L24) |
| DecayRate (ilerleme erimesi) | 1.5 /sn | [RelicEntity.cs:25](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L25) |
| GoldPerSecond (pasif gelir) | 0.5 /sn | [RelicEntity.cs:26](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L26) |

## 4. Strateji & counter

- **Depoyu kaynağa yakın kur.** Toplama hızı sabit (1.67 birim/sn) ama yürüme + taşıma süresi efektif verimi düşürür. Lumber/Mining Camp'i ağaç hattına / madene bitişik kur.
- **Çiftlik = sonsuz food ama wood pompası.** Her tükeniş 60 wood yer ve boştaki çiftlik 2 food/sn çürür. Çiftlikleri ancak köylü atayacaksan yap; aksi halde wood'u boşa harcarsın. Erken oyunda BerryBush (200) / FishPond (250) daha ucuz food sağlar.
- **Market arbitrajı.** Fiyat dalgalı: tek seferde çok satarsan sellRate 0.3 tabanına kadar düşer (kaynak başına gittikçe az altın). Küçük partiler hâlinde sat, drift'in fiyatı toparlamasını bekle. Acil altın ihtiyacında stone/wood fazlasını sat; uzun vadede madene köylü daha verimli.
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

- **Toplama makinesi:** [GatherSystem.cs:53-124](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L53) — `StepUnit` 4 durumlu FSM (Moving/Gathering/ReturningToDropoff). Tick aralığı L12, yatırma çarpanı L101-103.
- **Civ toplama çarpanı:** [GatherSystem.cs:190-202](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L190) — `CivGatherMult`, food/wood/gold için ayrı bonus.
- **Depo seçimi:** [GatherSystem.cs:158-172](../../AgeOfArenaUnity/Assets/Scripts/GatherSystem.cs#L158) — `NearestDropoff`, `BuildingDefs.AcceptsDropoff` ile tip filtresi.
- **Kaynak defteri:** [ResourceManager.cs:11-17](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L11) başlangıç değerleri; `Gain`/`CanAfford`/`Deduct` L30-53.
- **Düğüm + yenilenme/çürüme:** [ResourceNode.cs:39-62](../../AgeOfArenaUnity/Assets/Scripts/ResourceNode.cs#L39) — `Update` auto-reseed (renewable + wood harca) ve decay; `Take` L65.
- **Düğüm fabrikası:** [ResourceFactory.cs:12-14](../../AgeOfArenaUnity/Assets/Scripts/ResourceFactory.cs#L12) stok const'ları; FarmField L96-109, BerryBush L114-131, FishPond L134-148.
- **Market:** [MarketSystem.cs:40-61](../../AgeOfArenaUnity/Assets/Scripts/MarketSystem.cs#L40) — `Sell`/`Buy` fiyat kayması; `Tick` drift L30-37.
- **Ticaret:** [TradingSystem.cs:30-60](../../AgeOfArenaUnity/Assets/Scripts/TradingSystem.cs#L30) — `StepCart` round-trip; gelir formülü L52.
- **Relik:** [RelicEntity.cs:45-94](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L45) — `UpdateCapture` baskınlık/contested/decay/passive gold; [RelicSystem.cs:17-41](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L17) proximity tarama.

## 7. AoE2 farkı (reference köprü)

Tam karşılaştırma: [AoE2 Ekonomi & Ticaret Referansı](../reference/05-economy-trade.md)

Öne çıkan farklar:

- **Başlangıç stone:** AoE2'de 200; AoA'da **0** (oyuncu kararı, [ResourceManager.cs:14](../../AgeOfArenaUnity/Assets/Scripts/ResourceManager.cs#L14)). AoA'da köylü stone'a atanmadıkça stone biriktirilmez.
- **Çiftlik modeli:** AoE2'de farm sabit kapasite (250→380 tech'le) ve tükenince elle yeniden kuyruğa alınır. AoA'da farm **bina olarak kalır ve 60 wood ile otomatik yenilenir** + 2 food/sn çürür — tech ile kapasite artışı yok.
- **Toplama verimi:** AoE2 kaynak başına farklı dakika/birim oranları (odun 25, altın 27...) ve Wheelbarrow/Hand Cart kademeli artışı kullanır. AoA'da tek tip **1 birim / 0.6 sn** taban; fark sadece tech `GatherMult` + civ `CivGatherMult` çarpanlarından gelir.
- **Market:** Konsept aynı (dalgalı fiyat), ama AoA basitleştirilmiş lineer kayma (0.05/işlem, 0.002/sn drift) kullanır; AoE2'nin %92 coin-return komisyonu ve daha karmaşık eğrisi yoktur.
- **Trade Cart:** İkisi de mesafe-orantılı. AoA katsayısı `0.18 altın/birim` + 8 taban; AoE2 ~0.46 ve tile-bazlı, ayrıca min 5 tile mesafe şartı var (AoA'da min mesafe şartı yok).
- **Relik:** AoE2'de relik **Monk ile fiziksel taşınıp Monastery'ye konur**, 30 altın/dk üretir. AoA'da relik **yerinde durur ve birim yakınlığıyla ele geçirilir** (5 sn baskınlık), 0.5 altın/sn üretir — Monk/Monastery taşıma akışı yok.
- **Haraç (Tribute):** AoE2'de %30 vergiyle altın/kaynak transferi var. AoA'da **kodda tanımlı değil** (reference O22 TRB olarak işaretlese de aktif kaynak dosyası bulunamadı).

## 8. Eksikler / Yapılacaklar

| ID-aday | Sınıf | Eksik | AoE2-ref | Efor |
|---|---|---|---|---|
| TRIB | TradingSystem/ResourceManager | Oyuncular/AI arası kaynak transferi (haraç) %30 vergiyle yok | reference §AoA tablosu "Haraç (tribute)" | Orta |
| MONK | RelicSystem/UnitEntity | Relik fiziksel taşıma (Monk pickup → Monastery deposit) akışı yok; relik yerinde kapılıyor | reference §Relik Toplama Prosedürü | Yüksek |
| FARM | TechSystem/ResourceNode | Horse Collar/Heavy Plow/Crop Rotation gibi farm kapasite tech'leri yok (AoA farm renewable, kapasite sabit 300) | reference §Çiftlik Teknoloji Zinciri | Orta |
| GATE | GatherSystem | Kaynak türüne göre farklı toplama hızı yok (hepsi 1/0.6 sn); AoE2 odun/altın/balık farklı oranlar | reference §Villager Toplama Verimliliği | Orta |
| HUNT | ResourceFactory/UnitEntity | Geyik (Deer) / domuz (Boar) gibi hareketli/avlanan food kaynağı yok | reference §Kaynak Haritası (Deer/Boar) | Yüksek |
| MFEE | MarketSystem | Market komisyonu (AoE2 ~%92 coin-return) modellenmemiş; AoA sadece spread kullanır | reference §Coin Return Formula | Düşük |
| TSEA | TradingSystem | Trade Cog (deniz ticareti, Dock→Dock) yok | reference §Trade Cog | Yüksek |
