# Age of Arena — Asset Şeması & Planı

> Tüm birim/bina/mod için **ortak görsel asset şeması**. Tek kaynak: bu doküman.
> Plan/DoD ana kaynağı [PLAN.md](PLAN.md) ile birlikte okunur.

## Context

Oyunda 35 birim + 23 bina + 9 mod var ama görseller dağınık: çoğu birim **procedural
primitive** (kutu/küre) ile çiziliyor. Mevcut durum (audit):

| Kategori | Gerçek model | Primitive | Not |
|---|---|---|---|
| **Birimler (35)** | 10 KayKit + 3 Kenney = **13** | **22** | Tüm 14 unique unit, tüm atlı/develi/filli, tüm gemiler primitive |
| **Binalar (23)** | ~8 kısmi Kenney | ~14 | FishTrap factory'si **yok** (enum var) |

**Kilit içgörü:** Projede zaten **kullanılmayan varlık var** — KayKit 5 karakter (Knight,
Barbarian, Rogue, RogueHooded, Mage) + 40 silah FBX + 4 Skeleton + **240 Kenney bina parçası**
(167 FantasyTown + 76 Castle, sadece ~17'si bağlı). Barbarian artık **boşta** (Villager ondan
ayrıldı). Yani çoğu boşluk **yeni indirme olmadan**, mevcut varlıkları doğru bağlayarak kapanır.

---

## Ortak şema — 3 ilke

### 1. Stil çapası
- **Flat-shaded low-poly** (KayKit/Quaternius estetiği). Yeni asset bu dile uymalı.
- **Takım rengi** = mevcut `_Color` MaterialPropertyBlock tint ([UnitFactory.cs:1003-1008](../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L1003-L1008)). Yeni asset'lerin shader'ı `_Color`'a saygı duymalı (KayKit/Kenney duyuyor).
- **Ölçek/collider/animator** konvansiyonu `Finish()` ile sabit ([UnitFactory.cs:995-1044](../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L995-L1044)). Animasyonlu prefab Animator kontratı: **`IsMoving`(bool) · `Attack`(trigger) · `Die`(trigger)** ([UnitEntity.cs:621-646](../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L621-L646)). Statik mesh → Animator yok → kod **procedural bob/swing** uygular ([UnitEntity.cs:648-668](../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L648-L668)).

### 2. Reuse-first (AoE2 de böyle yapar)
35 birim için 35 model gerekmez. Birimler **arketiplere** ayrılır; aynı base model + silah/tint
varyasyonu ile farklılaşır. Mevcut KayKit 5 karakter + silahlar çoğu insansı birimi karşılar.

### 3. Kaynak aileleri (hepsi CC0/serbest, edinme güvenilirliğine göre)
| Aile | Durum | Edinme | Kapsam |
|---|---|---|---|
| **KayKit** Adventurers+Skeletons+Weapons | **Projede var** | — | İnsansı birimler (piyade/okçu/keşiş/unique) |
| **Kenney** Castle+FantasyTown+Nature | **Projede var** | direct curl ✓ | Kuşatma, binalar, çevre |
| **Quaternius** Animated Animals / RPG | yok | Google Drive (manuel) ⚠️ | Atlar/deve/fil, ek karakter |
| **Kenney** Pirate/Ships | yok | direct curl ✓ | Gemiler |

**Edinme gerçeği (test edildi):** Unity `ImportExternalModel` yalnızca `.fbx`/`.zip` URL kabul
ediyor (GLB **reddedildi**). Kenney = doğrudan curl (otomatik). Quaternius = Drive-gated (manuel
indirme). poly.pizza = sadece GLB (yerel dönüştürücü yok → kullanılamaz). GitHub rastgele
`villager.fbx` = lisans belirsiz → **kullanılmaz**.

---

## Birim → asset atama tablosu (35)

**Tier 0** = projede var, bağlı · **T1** = sıfır indirme, mevcut KayKit'i bağla · **T2** = yeni
indirme gerek (hayvan/gemi) · base = kullanılacak KayKit prefab.

| Birim | Arketip | Atama | Tier |
|---|---|---|---|
| Villager | eko | yeni köylü modeli (devam ediyor) / primitive yedek | T0.5 |
| Militia·Spearman | ağır piyade | KayKit **Knight** | T0 ✓ |
| Archer·Skirmisher | okçu | KayKit **Rogue** | T0 ✓ |
| Longbowman | okçu | KayKit **RogueHooded** | T0 ✓ |
| Monk·Medic | kaster | KayKit **Mage** | T0 ✓ |
| King | royal | KayKit Knight + taç | T0 ✓ |
| TeutonicKnight·Samurai·Huskarl | ağır piyade UU | **Knight** base (+tint/silah) | **T1** |
| Berserk·WoadRaider·ThrowingAxeman | barbar UU | **Barbarian** base (boşta!) | **T1** |
| ChuKoNu·Janissary | ranged UU | **Rogue** base | **T1** |
| Eagle·EliteEagle | hafif UU | **RogueHooded** base | **T1** |
| Cavalry·Cavalier·Paladin | süvari | at + Knight binici | **T2** at |
| Scout·LightCav·Hussar | hafif süvari | at + RogueHooded | **T2** at |
| CavalryArcher·Mangudai | atlı okçu | at + Rogue | **T2** at |
| Cataphract | zırhlı süvari UU | at + Knight | **T2** at |
| Camel·Mameluke | develi | deve + binici | **T2** deve |
| WarElephant | fil UU | fil + howdah | **T2** fil |
| Trebuchet·Ram·Mangonel | kuşatma | Kenney Castle siege | T0 ✓ |
| TradeCart | ticaret | Kenney FantasyTown cart (var) | **T1** |
| Galley·FireShip·DemoShip | savaş gemisi | gemi modeli | **T2** gemi |
| FishingShip | balıkçı | küçük tekne | **T2** gemi |

**Sonuç:** ~14 birim **T1** (sıfır indirme, sadece `UnitVisualLibrary.VisualFor` switch'ini
genişlet). Gerçek yeni indirme yalnızca **2 kategori**: hayvanlar (at/deve/fil) + gemiler.

> Polish (T1.5): unique unit'leri ayırt etmek için KayKit silah swap (40 silah projede var) — base
> aynı kalır, ele balta/kılıç/yay takılır. Bone-attach gerektirir; ilk dalgadan sonra.

---

## Bina → asset planı (23)

Çoğu boşluk **projede zaten olan** Kenney FantasyTown/Castle parçalarıyla kapanır (sıfır indirme):

| Durum | Binalar | Aksiyon |
|---|---|---|
| Kenney bağlı ✓ | TownCenter, House, Barracks, Castle, Wall, Gate, Mill, Market, LumberCamp | korunur |
| Primitive → **mevcut Kenney** | ArcheryRange, Stable, Farm, MiningCamp, Blacksmith, Monastery, University, Dock, SiegeWorkshop, Outpost, WatchTower, BombardTower, Wonder | **T1** — FantasyTown/Castle parçalarından kompoze et |
| **Eksik** | FishTrap (factory yok) | **T1** — factory ekle + Kenney parça |

Bu, **Kenney trim'i (eski Part B) revize eder:** FantasyTown/Castle'dan daha fazla parça
kullanacağız → trim yalnızca gerçekten hiç kullanılmayanları (çoğu Nature 329) hedefler; keep-list
bina kompozisyonu netleştikten **sonra** üretilir.

---

## Mod → asset etkisi (9)

Modlar kural; çoğu yeni asset istemez. İstisnalar:
- **Regicide** → King birimi (görsel var, T0).
- **KingOfTheHill** → merkez kontrol noktası işaretçisi (bayrak/alan) — küçük, Kenney flag parçası.
- Diğerleri (Deathmatch/Nomad/EmpireWars/SuddenDeath/Treaty/Turbo) → asset gerektirmez.

---

## Faz planı

1. **Faz 1 — Köylü (devam):** yeni köylü modeli (ayrı karar) + remap (kod yapıldı).
2. **Faz 2 — T1 insansı reuse (sıfır indirme, en yüksek etki):** `UnitVisualLibrary` + `VisualFor`
   genişlet → 14 unique/insansı birim mevcut KayKit base'lere bağlanır. ~22 primitive → ~8'e iner.
3. **Faz 3 — T1 binalar (sıfır indirme):** primitive binaları mevcut Kenney parçalarıyla kompoze;
   FishTrap factory ekle. Sonra Kenney trim keep-list'i üret.
4. **Faz 4 — T2 hayvanlar:** at/deve/fil (CC0) edin → atlı/develi/filli birimler.
5. **Faz 5 — T2 gemiler:** 2-3 gemi modeli (CC0) → donanma.
6. **Faz 6 — T1.5 polish:** unique unit silah-swap ile ayrıştırma.

Her faz ayrı commit + Unity'de Play doğrulaması (0 error/warning).

---

## Açık kararlar
- **D1:** Köylü modeli kaynağı (Quaternius Farmer dönüştür / Kenney / kullanıcı indirir / primitive kal).
- **D2:** T2 hayvan kaynağı — Quaternius Animated Animals (manuel Drive indirme) vs başka CC0.
- **D3:** T2 gemi kaynağı — Kenney Pirate Kit (direct curl) vs Quaternius.
- **D4:** Trim agresifliği — bina kompozisyonu netleştikten sonra keep-list.
