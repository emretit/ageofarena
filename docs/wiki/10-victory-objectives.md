# Zafer Koşulları & Oyun Modları — AoA Wiki

> Age of Arena'da bir maç birden çok yoldan biriyle biter: **Fetih** (rakibin Town
> Center'ı yıkılır), **Anıt** (Wonder bir süre ayakta tutulur), **Kalıntı**
> (haritadaki tüm relikler bir süre kontrol edilir), **Regicide** (Kral birimi
> öldürülür) ve **Süre** dolduğunda en yüksek skorlu takım. Tüm bu yolların hakemi
> tek bir sistemdir: `MatchSystem`. Relik yakalama mekaniği `RelicSystem` +
> `RelicEntity` tarafından, oyun modu kurulumu (`Deathmatch`/`Regicide`/`Nomad`)
> `WorldRoot` tarafından yürütülür.
>
> **Kod kaynağı:** [MatchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs),
> [RelicSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs),
> [RelicEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs),
> [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs) (GameMode enum),
> [WorldRoot.cs](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs) (mod kurulumu),
> [BuildingDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs)
> (Wonder maliyeti), [UnitFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs)
> (King birimi).

---

## 1. Ne olduğu

Zafer koşulları, maçın hangi durumda kazanılıp kaybedileceğini belirleyen
kurallardır. AoA'da bunlar tek bir hakem sınıfı olan `MatchSystem` içinde
toplanmıştır. `MatchSystem.Update()` her gerçek saniyede bir (`CheckInterval = 1s`,
`Time.unscaledDeltaTime`) sahneyi tarar (`CheckEnd()`) ve aktif zafer yollarını
kontrol eder:

- **Fetih (Conquest):** Bir takım, Town Center'ı yıkıldığında elenir. Oyuncu
  (takım 0) kendi TC'si düşerse kaybeder; düşman takımların (1-3) hiçbirinde TC
  kalmayınca kazanır. Düşman tespiti artık diplomasi tabanlıdır (`IsEnemy`).
- **Anıt zaferi (Wonder):** Bir takım, tamamlanmış bir `Wonder` binasını belirli
  süre (`WonderHoldTime`) ayakta tutarsa kazanır. Wonder yıkılırsa sayaç sıfırlanır.
- **Kalıntı zaferi (Relic):** Bir takım, haritadaki **tüm** relikleri belirli süre
  (`RelicHoldTime`) elinde tutarsa kazanır. Tek bir relik kaybedilirse sayaç sıfırlanır.
- **Regicide zaferi (M10):** Yalnızca `GameMode.Regicide` aktifken işler. Oyuncunun
  Kralı (`UnitType.King`) ölürse kaybeder; tüm düşman Kralları ölünce kazanır.
- **Süre zaferi (M10/VTIME):** `MatchTimeLimit > 0` ise süre dolduğunda
  (`CheckTimeUp()`) tüm takımlar skorlanır, en yüksek skorlu takım kazanır.

Maç bittiğinde takım başına bir **kompozit skor** hesaplanır (ordu, bina, ekonomi,
relik ve çağ ağırlıklı) ve sonuç ekranında gösterilir. Bu kompozit skor, *Süre
zaferi* dışında ayrı bir zafer yolu değil — bitiş ekranında gösterilen bir
performans göstergesidir.

---

## 2. Oyun modları (M10)

Oyun modu, `GameMode` enum ile seçilir
([GameTypes.cs:56](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L56)) ve
`GameBootstrap.NextGameMode` üzerinden maç başında `gm.gameMode`'a yazılır
([WorldRoot.cs:100](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L100)). Mod,
başlangıç kurulumunu ve hangi zafer yollarının aktif olacağını belirler.

| GameMode | Kurulum farkı | Kaynak |
|---|---|---|
| **Random** (standart) | Klasik arena: her takım üssünü (TC + asker) kurar, standart kaynak | [GameTypes.cs:56](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L56) |
| **Deathmatch** | Tüm takımlar yüksek başlangıç kaynağıyla başlar (askeri odak) | [WorldRoot.cs:792](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L792) |
| **Regicide** | Her takıma bir **King** birimi spawn edilir; Kral ölürse eleme | [WorldRoot.cs:795](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L795) |
| **Nomad** | TC'siz başlangıç: üs hiç kurulmaz, takım başına 6 villager dağılır | [WorldRoot.cs:104](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L104) |

> `Random, Deathmatch, Regicide, Nomad` enum sırasıyla tanımlıdır
> ([GameTypes.cs:56](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L56)).
> Mod-bazlı kurulum `SetupGameplay`'in sonunda bir `switch` ile uygulanır
> ([WorldRoot.cs:790-801](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L790)).
> Restart (R) modu korur: `GameBootstrap.NextGameMode = gm.gameMode`
> ([MatchSystem.cs:64](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L64)).

### Deathmatch (VDEATH)
`ApplyDeathmatch(gm)` tüm 4 takımın kaynaklarını bol başlangıç değerlerine yükseltir
(`Mathf.Max` ile, yani mevcut değerin altına düşürmez):

| Kaynak | Değer | Kaynak |
|---|---|---|
| Food | 20.000 | [WorldRoot.cs:810](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L810) |
| Wood | 20.000 | [WorldRoot.cs:811](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L811) |
| Gold | 10.000 | [WorldRoot.cs:812](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L812) |
| Stone | 5.000 | [WorldRoot.cs:813](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L813) |

### Regicide (VREGI)
`SpawnKings(gm, …)` her takıma (0-3) bir `King` birimi üssünün hemen önüne dikiyor
([WorldRoot.cs:818-826](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L818)).
Eleme mantığı `MatchSystem.CheckEnd()` içinde **yalnızca** `gameMode == Regicide`
iken çalışır: yaşayan Kral takımları `kingAlive[]`'a yazılır;
`!kingAlive[0]` → oyuncu kaybeder, hiçbir düşman Kralı kalmazsa → oyuncu kazanır
([MatchSystem.cs:127-139](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L127)).

King birim statları
([UnitFactory.cs:504-524](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L504)):

| Stat | Değer | Kaynak |
|---|---|---|
| HP | 75 | [UnitFactory.cs:519](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L519) |
| Hareket hızı | 3.2 | [UnitFactory.cs:520](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L520) |
| Melee zırh | 1 | [UnitFactory.cs:521](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L521) |
| Pierce zırh | 1 | [UnitFactory.cs:522](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L522) |

> King görseli Militia silüetinden türetilir (taç + cüppe); ayrı bir Castle birimi
> değildir, `UnitType.King` olarak işaretlenir
> ([GameTypes.cs:22](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L22)).

### Nomad (VNOMAD)
TC'siz başlangıç: `gm.gameMode != Nomad` koşulu yüzünden `BuildBase` hiç çağrılmaz
([WorldRoot.cs:104-106](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L104)).
Onun yerine `SpawnNomad` her takıma harita merkezine doğru bir hat üzerinde **6
villager** dağıtır ([WorldRoot.cs:829-843](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L829)).
Oyuncu üssünü baştan kurmak zorundadır.

> **Dikkat (iç tutarlılık):** Nomad modunda hiçbir takımın TC'si yoktur, ama Fetih
> kontrolü TC tabanlıdır. Oyuncu ilk TC'sini inşa edene kadar `tcAlive[0]` false
> kalır; bu durumda Fetih eleme mantığının erken tetiklenip tetiklenmediği koddan
> netleşmiyor — Nomad için ayrı bir koruma (grace period) görülmüyor. Doğrulama
> için Play testi gerekir.

---

## 3. Nasıl çalışır (mekanik + formül)

### Tarama döngüsü
`MatchSystem.Update()` sayacı `Time.unscaledDeltaTime` ile azaltır; yani tarama
oyun hızından (`Time.timeScale`) bağımsız olarak her gerçek 1 saniyede bir çalışır
([MatchSystem.cs:71-84](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L71)).
`_matchElapsed` her karede artar; süre limiti aşılınca `CheckTimeUp()` çağrılır,
aksi halde sayaç dolunca `CheckEnd()` çalışır.

### Fetih
`CheckEnd()` tüm binaları gezer, takım başına `tcAlive[]` (yaşayan TC) dizisini
doldurur. Sonra:
- `!tcAlive[0]` → oyuncu kaybeder ("Fetih (TC yıkıldı)").
- Hiçbir **düşman** takımda (`IsEnemy(0,t)`) TC yoksa → oyuncu kazanır ("Fetih").

Artık düşman tespiti diplomasi tabanlıdır (`IsEnemy`); müttefik/nötr takımlar Fetih
zaferini engellemez ([MatchSystem.cs:141-147](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L141)).
Yalnızca **Town Center** sayılır; diğer binaların yıkımı eleme tetiklemez.

### Anıt (Wonder)
Her takım için, ayakta ve **inşası bitmiş** (`!underConstruction`) bir Wonder varsa
`_wonderTimer[t]` her tarama turunda `CheckInterval` (=1s) kadar artar; yoksa
sıfırlanır:

```
hasWonder[t] ? _wonderTimer[t] += 1 : _wonderTimer[t] = 0
if (_wonderTimer[t] >= WonderHoldTime) → "Anıt zaferi"
```

Wonder yıkılır veya henüz inşa hâlindeyse sayaç anında 0'a döner
([MatchSystem.cs:101,111-112](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L101)).

### Kalıntı (Relic)
Bir takım, haritadaki relik sayısı kadar relik kontrol ediyorsa sayaç ilerler:

```
holdsAllRelics = totalRelics > 0 && relicSystem.CountControlled(t) == totalRelics
holdsAllRelics ? _relicTimer[t] += 1 : _relicTimer[t] = 0
if (_relicTimer[t] >= RelicHoldTime) → "Kalıntı zaferi"
```

`CountControlled(teamId)` ([RelicSystem.cs:107](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L107))
tüm reliklerin `controllingTeam` alanını sayar.

### Relik yakalama (capture) mekaniği
`RelicSystem.Tick()` her kareye bir kez çalışır. M10 ile **Monk taşıma zinciri**
(AoE2 modeli) eklendi:
1. **Taşınan relikler** Monk'u takip eder; Monk ölürse düşer. Monk relikleri dost
   bir **Monastery**'ye 4 birim mesafeye getirince relik orada kilitlenir
   (`heldInMonastery = true`, `ForceControl`) ve pasif altın üretir
   ([RelicSystem.cs:26-57](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L26)).
2. **Yakınlık taraması:** menzilindeki (`CaptureRange = 3.5`, karesel mesafe)
   birimler her reliğin `unitsNearby` listesine eklenir.
3. **Yakalama / pasif altın:** Monastery'de tutulan relik altın üretir; serbest
   relik `UpdateCapture(dt)` ile yakalanmaya açıktır.
4. **Monk pickup:** boş bir reliğin üstündeki, henüz relik taşımayan Monk reliği
   sırtlanır; sonra Monastery'ye taşır
   ([RelicSystem.cs:89-103](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L89)).

`RelicEntity.UpdateCapture()` mantığı (serbest relikler için):
- Yakındaki birimleri takıma göre sayar (`counts[4]`).
- En kalabalık tek takımı bulur. **Beraberlik** veya hiç birim yoksa → "contested"
  (kimse yakalayamaz), `dominant = -1`.
- Baskın takım mevcut sahip değilse: `captureProgress += dt`. İlerleme
  `CaptureSeconds = 5` saniyeye ulaşınca relik o takıma geçer.
- Aksi halde ilerleme `DecayRate = 1.5`/sn ile geri çürür.
- Sahip takım her saniye `GoldPerSecond = 0.5` altın pasif gelir kazanır
  ([RelicEntity.cs:52-104](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L52)).

> **Not:** Herhangi bir birim reliği yakınlıkla *kontrol* edebilir (5 sn üstünde
> durarak), ama yalnızca **Monk** reliği sırtlayıp Monastery'ye taşıyarak *kalıcı*
> kontrole (`heldInMonastery`) bağlayabilir. Bu, eski "her birim yakalar"
> mekaniğinin üstüne eklenen AoE2-tarzı zincirdir.

### Süre zaferi & skor (VTIME)
`MatchTimeLimit > 0` ise süre dolunca `CheckTimeUp()` çalışır: her takım için basit
skor hesaplanır, en yüksek skorlu takım kazanır ("Süre bitti")
([MatchSystem.cs:151-164](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L151)).
Burada kullanılan **basit skor**:

```
skor = units*10 + buildings*20 + gold
```
([MatchSystem.cs:167-174](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L167)).

`TimeRemaining` property'si HUD için kalan süreyi verir; limit yoksa `float.MaxValue`
döner ([MatchSystem.cs:41-43](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L41)).

> **Önemli (kodda henüz tam yok):** `MatchTimeLimit` public ve mantığı tamamen
> hazır, ancak `WorldRoot` veya başka bir yer onu **set etmiyor** (grep ile
> doğrulandı: yalnızca tanım + iç kullanım). Yani şu an süre limiti fiilen hep
> `0` = limitsiz; süre zaferi altyapısı mevcut ama maçta tetiklenmiyor.

### Kompozit skor formülü (bitiş ekranı)
`MatchSystem.Score(gm, team)` — iki adet `Score` overload'ı var; bu kompozit olan
maç bitiş altyazısı için kullanılır:

```
skor = units*10 + military*15 + blds*25 + resTotal/10 + relics*100 + age*75
```

- `units` = takımın tüm birimleri, `military` = Villager olmayanlar
- `blds` = yaşayan binalar, `resTotal` = food+wood+gold+stone toplamı
- `relics` = kontrol edilen relik, `age` = ulaşılan çağ indeksi (0-3)

Oyun bitince `End()` skoru altyazıda gösterir: `"{reason} · Skorun: {score}"`
([MatchSystem.cs:184-218](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L184)).

---

## 4. Gerçek statlar (koddan)

### Zafer eşikleri (MatchSystem)

| Stat | Değer | Kaynak |
|---|---|---|
| Tarama aralığı (`CheckInterval`) | 1 sn | [MatchSystem.cs:16](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L16) |
| Anıt tutma süresi (`WonderHoldTime`) | 60 sn (varsayılan) | [MatchSystem.cs:18](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L18) |
| Kalıntı tutma süresi (`RelicHoldTime`) | 60 sn (varsayılan) | [MatchSystem.cs:20](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L20) |
| Maç süre limiti (`MatchTimeLimit`) | 0 = limitsiz | [MatchSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L28) |
| Süre skoru: birim | ×10 | [MatchSystem.cs:170](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L170) |
| Süre skoru: bina | ×20 | [MatchSystem.cs:171](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L171) |
| Süre skoru: + altın | ham | [MatchSystem.cs:172](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L172) |
| Kompozit skor: birim | ×10 | [MatchSystem.cs:217](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L217) |
| Kompozit skor: askeri birim | ×15 | [MatchSystem.cs:217](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L217) |
| Kompozit skor: bina | ×25 | [MatchSystem.cs:217](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L217) |
| Kompozit skor: kaynak | toplam ÷ 10 | [MatchSystem.cs:217](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L217) |
| Kompozit skor: relik | ×100 | [MatchSystem.cs:217](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L217) |
| Kompozit skor: çağ | ×75 | [MatchSystem.cs:217](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L217) |

> **VHOLD:** `WonderHoldTime` ve `RelicHoldTime` public alanlardır ve kod yorumu
> "Set by WorldRoot" der; ancak grep ile doğrulandı ki **şu an hiçbir yerden set
> edilmiyor** — dolayısıyla ikisi de fiilen 60 sn sabittir. Ayarlanabilir zafer
> süresi altyapısı hazır, fakat bir UI/WorldRoot bağlantısı henüz yok.

### Relik yakalama (RelicSystem + RelicEntity)

| Stat | Değer | Kaynak |
|---|---|---|
| Ele geçirme menzili (`CaptureRange`) | 3.5 birim | [RelicSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L14) |
| Monastery teslim menzili (`DepositRange`) | 4 birim | [RelicSystem.cs:17](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L17) |
| Yakalama süresi (`CaptureSeconds`) | 5 sn | [RelicEntity.cs:31](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L31) |
| İlerleme çürüme hızı (`DecayRate`) | 1.5 / sn | [RelicEntity.cs:32](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L32) |
| Pasif altın geliri (`GoldPerSecond`) | 0.5 / sn | [RelicEntity.cs:33](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L33) |
| Başlangıç `controllingTeam` | -1 (nötr) | [RelicEntity.cs:18](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L18) |

### Wonder binası maliyeti (BuildingDefs)

> Constructor alan sırası `(type, food, wood, gold, stone, ...)` —
> [BuildingDefs.cs:35](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L35).
> Wonder satırı `0, 500, 800, 600` → **food=0, wood=500, gold=800, stone=600**.

| Stat | Değer | Kaynak |
|---|---|---|
| Yiyecek (food) | 0 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Odun (wood) | 500 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Altın (gold) | 800 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Taş (stone) | 600 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| İnşa süresi | 150 sn | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| HP | 3000 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Minimum çağ | Imperial | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |
| Melee zırh / Pierce zırh | 5 / 8 | [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83) |

> **Özet maliyet: 500 odun / 800 altın / 600 taş** (food yok). En pahalı bina;
> yalnızca Imperial çağında inşa edilebilir.

### King birimi (Regicide)

| Stat | Değer | Kaynak |
|---|---|---|
| HP | 75 | [UnitFactory.cs:519](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L519) |
| Hareket hızı | 3.2 | [UnitFactory.cs:520](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L520) |
| Melee / Pierce zırh | 1 / 1 | [UnitFactory.cs:521](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L521) |

### Deathmatch başlangıç kaynağı

| Kaynak | Değer | Kaynak |
|---|---|---|
| Food / Wood | 20.000 | [WorldRoot.cs:810](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L810) |
| Gold | 10.000 | [WorldRoot.cs:812](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L812) |
| Stone | 5.000 | [WorldRoot.cs:813](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L813) |

---

## 5. Strateji & counter

### Fetih
- **Saldıran:** Rakibin TC'sini odakla. Diğer binaları yıkmak gerekmez — TC
  düşünce o takım elenir. Kuşatma birimleri (siege) zırhı bypass ettiği için TC
  baskısında en verimli.
- **Savunan:** TC'yi kale/duvar ve garnizonlu birimlerle koru. TC yıkılırsa maç
  biter; tek noktalı yenilgi riski en yüksek koşul budur.

### Anıt (Wonder)
- **İnşa eden:** Anıt pahalıdır (**500 odun / 800 altın / 600 taş**) ve yalnızca
  Imperial çağında dikilebilir. İnşa bitince **60 saniye** ayakta kalması yeterli —
  geç oyunda güçlü bir ekonomiyle sürpriz bir zafer açar.
- **Counter:** Sayaç tüm takımlara `VictoryStatus` üzerinden HUD'da görünür. Wonder
  3000 HP'ye ve yüksek zırha (5/8) sahip olsa da yıkılabilir; siege birimleriyle
  saldır. Yıkım sayacı **anında** sıfırlar — 59. saniyede yıkmak bile yeterli.

### Kalıntı (Relic)
- **Toplayan:** Haritadaki **tüm** relikleri ele geçir. Bir relik üstünde tek
  takımın birimi 5 saniye durunca yakalanır; kalıcı kontrol için bir **Monk** ile
  reliği sırtlayıp Monastery'ye taşı. Sonra 60 saniye **hepsini** elinde tut. Her
  kontrol edilen relik saniyede 0.5 altın pasif gelir verir.
- **Counter:** Tek bir reliğe birim gönder — beraberlik yaratınca rakibin yakalama
  ilerlemesi durur ve çürür (1.5/sn). Monk taşırken öldürürsen relik düşer. Bir
  relik bile kaybedilirse 60s sayaç sıfırlanır. Relikler yok edilemez
  (`IDamageable` değil), yalnızca kontrol el değiştirir.

### Regicide
- **Saldıran:** Doğrudan rakibin Kralı'nı hedefle. King yalnızca **75 HP** ve düşük
  zırha (1/1) sahip; süvari baskını veya nişancı yaylım ateşiyle hızlı düşer. Bir
  Kralı düşürmek o takımı tamamen eler.
- **Savunan:** Kralı'nı en arkada, duvar ve garnizon arkasında tut; tek bir baskın
  maçı bitirebilir. King savaş birimi değildir — ön hatta çıkarma.

### Deathmatch
- Ekonomi safhası neredeyse yok: 20k food/wood, 10k gold ile başlarsın. Hemen
  askeri üretim binalarına ve sürekli birim akışına yatır. Kaynak toplamak yerine
  pop cap ve üretim hızı belirleyici.

### Nomad
- Üs baştan kurulur: önce villager'ları toparla, hızlıca TC + ekonomi binaları dik.
  TC'siz başladığın için erken baskına çok açıksın; konum seçimi ve ilk TC hızı
  kritik.

### Süre / skor
- Süre limiti şu an fiilen kapalı (`MatchTimeLimit = 0`). Kompozit skorda relik
  (×100) ve çağ (×75) ağırlıkları en yüksek; geç oyunda Imperial'e ulaşmak ve relik
  tutmak skoru en hızlı yükselten yatırımlardır.

---

## 6. Çapraz bağlantılar

- [01-game-flow-ages.md](./01-game-flow-ages.md) — Imperial çağı (Wonder ön koşulu)
  ve çağ ilerlemesi.
- [04-buildings.md](./04-buildings.md) — Town Center, Wonder, Monastery ve garnizon;
  bina HP/zırh tablosu.
- [02-units.md](./02-units.md) — Monk (relik taşıma), King ve siege (Wonder/TC yıkımı).
- [07-combat-counters.md](./07-combat-counters.md) — siege'in zırhı bypass etmesi,
  Wonder/TC kuşatması.
- [08-economy-trade.md](./08-economy-trade.md) — relik pasif altın geliri ve Wonder
  maliyetini karşılayan ekonomi.
- [09-ai-difficulty.md](./09-ai-difficulty.md) — AI'nin relikleri fırsatçı toplaması
  ve zafer koşullarına tepkisi.
- [11-controls-ui-feedback.md](./11-controls-ui-feedback.md) — `VictoryStatus` HUD
  geri sayımı, `TimeRemaining`, sonuç ekranı ve R ile restart.

---

## 7. Kod referansları (file:line, derivation)

- **Zafer hakemi & döngü:**
  [MatchSystem.cs:53-85](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L53)
  — `Update()` `unscaledDeltaTime` ile sayaç, süre/`CheckEnd()` dallanması.
- **GameMode enum:**
  [GameTypes.cs:56](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L56)
  — `Random, Deathmatch, Regicide, Nomad`.
- **Mod kurulum switch'i:**
  [WorldRoot.cs:790-801](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L790)
  — `SetupGameplay` sonunda mod-bazlı post-setup.
- **Deathmatch kaynak:**
  [WorldRoot.cs:805-815](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L805).
- **Regicide Kral spawn:**
  [WorldRoot.cs:818-826](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L818);
  **eleme mantığı** [MatchSystem.cs:127-139](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L127).
- **Nomad kurulum:** TC atlama
  [WorldRoot.cs:104-106](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L104);
  villager dağıtma [WorldRoot.cs:829-843](../../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs#L829).
- **Fetih kontrolü (diplomasi):**
  [MatchSystem.cs:141-147](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L141)
  — `IsEnemy(0,t)` ile düşman TC taraması.
- **Anıt sayacı:**
  [MatchSystem.cs:101,111-112](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L101)
  — `!underConstruction` koşulu + `WonderHoldTime` eşiği.
- **Kalıntı sayacı:**
  [MatchSystem.cs:115-118](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L115)
  — `CountControlled(t) == totalRelics` → `RelicHoldTime`.
- **Süre zaferi:**
  [MatchSystem.cs:74-79](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L74)
  (tetik) + [MatchSystem.cs:151-164](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L151)
  (`CheckTimeUp` en yüksek skor).
- **TimeRemaining property:**
  [MatchSystem.cs:41-43](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L41).
- **Kompozit skor:**
  [MatchSystem.cs:196-218](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L196).
- **Resign (teslim):**
  [MatchSystem.cs:46-51](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L46).
- **Restart modu korur:**
  [MatchSystem.cs:57-67](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L57).
- **Relik proximity + Monk zinciri:**
  [RelicSystem.cs:19-104](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L19).
- **Kontrol sayımı:**
  [RelicSystem.cs:107-115](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L107).
- **Relik yakalama state machine:**
  [RelicEntity.cs:52-104](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L52)
  — `CaptureSeconds = 5` (s.31), `DecayRate = 1.5` (s.32), `GoldPerSecond = 0.5` (s.33).
- **King birimi:**
  [UnitFactory.cs:504-524](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L504);
  enum [GameTypes.cs:22](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L22).
- **Wonder maliyeti:** constructor sırası
  [BuildingDefs.cs:35](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L35);
  Wonder satırı [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83).

---

## 8. AoE2 farkı (reference köprü)

Karşılaştırma kaynağı:
[06-victory-game-modes.md](../reference/06-victory-game-modes.md).

| Konu | AoA (kod) | AoE2 (referans) |
|---|---|---|
| Fetih | TC yıkımı = eleme; tüm düşman TC'leri düşünce zafer (diplomasi-bilinçli) | Tüm bina/birim yok edilmeli; "Conquest" daha geniş |
| Sudden Death | Fiilen aktif (yalnızca TC sayılır) | Ayrı bir mod; TC = anında eleme |
| Wonder maliyeti | **500 odun / 800 altın / 600 taş**, yalnızca Imperial | 1000 odun + 1000 taş + 1000 altın |
| Wonder tutma süresi | 60 sn (`WonderHoldTime`, ayarlanabilir altyapı var ama set edilmiyor) | ~200 yıl ≈ ~10 dk gerçek süre |
| Relik yakalama | Birim 5 sn üstünde dur **veya** Monk ile Monastery'ye taşı | Monk ile topla → Monastery'e götür |
| Relik zafer süresi | Tüm relikler 60 sn (`RelicHoldTime`) | Tüm relikler ~200 yıl (~10 dk) |
| Relik yok edilebilir mi | Hayır; sadece kontrol el değiştirir | Monastery yıkılırsa relik düşer |
| Relik pasif geliri | 0.5 altın/sn | Relik başına altın trickle (benzer) |
| Süre / skor zaferi | Altyapı var (`MatchTimeLimit` + `CheckTimeUp`), ama set edilmiyor → fiilen kapalı | Süre dolunca en yüksek skor kazanır |
| Regicide / Kral | **Var** (M10): King birimi, ölünce eleme; 75 HP, 1/1 zırh | Kral öldür = eleme (benzer) |
| Deathmatch | **Var** (M10): 20k/20k/10k/5k başlangıç kaynağı | Var (yüksek başlangıç kaynağı) |
| Nomad | **Var** (M10): TC'siz, takım başına 6 dağınık villager | Var (TC'siz dağınık başlangıç) |
| Diplomasi (Allied/Neutral) | Fetih artık `IsEnemy` kullanır; tam tribute/vergi yok | Tam diplomasi + tribute vergisi |

---

## 9. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| VHOLD | Denge/UI | `WonderHoldTime`/`RelicHoldTime` public ama WorldRoot'tan set edilmiyor; ayarlanabilir zafer süresi UI'ı yok (fiilen 60 sn sabit) | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Wonder/§Relic | Düşük |
| VTIME-WIRE | Zafer yolu | Süre zaferi mantığı (`MatchTimeLimit` + `CheckTimeUp`) hazır ama hiçbir yerden set edilmiyor → maçta tetiklenmiyor | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Score | Düşük |
| NOMD-TC | Mod tutarlılığı | Nomad'da hiç TC yokken Fetih kontrolünün erken eleme yapıp yapmadığı belirsiz; grace period görülmüyor | — (iç tutarlılık) | Orta |
| VSTA | UI | `SetStatus` yalnızca ilk countdown'ı gösteriyor; "en acil" sayacı seçme mantığı eksik (kod yorumuyla çelişiyor) | — (iç tutarlılık) | Düşük |
| MODE-UI | UI | GameMode seçimi için ön-maç UI (menü) — `GameBootstrap.NextGameMode` set eden bir ekran doğrulanmadı | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) | Orta |
| DIPL | Sistem | Diplomasi Fetih'e bağlandı; tam müttefik vision-share + tribute vergisi zafer entegrasyonu eksik | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Diplomasi | Yüksek |
