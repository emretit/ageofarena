# Zafer Koşulları — AoA Wiki

> Age of Arena'da bir maç dört yoldan biriyle biter: **Fetih** (rakibin Town
> Center'ı yıkılır), **Anıt** (Wonder bir süre ayakta tutulur), **Kalıntı**
> (haritadaki tüm relikler bir süre kontrol edilir) ve oyun donduğunda gösterilen
> **kompozit skor**. Tüm bu yolların hakemi tek bir sistemdir: `MatchSystem`.
> Relik yakalama mekaniği `RelicSystem` + `RelicEntity` tarafından yürütülür.
>
> **Kod kaynağı:** [MatchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs),
> [RelicSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs),
> [RelicEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs),
> [BuildingDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs)
> (Wonder maliyeti).

---

## 1. Ne olduğu

Zafer koşulları, maçın hangi durumda kazanılıp kaybedileceğini belirleyen
kurallardır. AoA'da bunlar tek bir hakem sınıfı olan `MatchSystem` içinde
toplanmıştır. `MatchSystem` her saniye (`CheckInterval = 1s`) sahneyi tarar ve üç
aktif zafer yolunu kontrol eder:

- **Fetih (Conquest):** Bir takım, Town Center'ı yıkıldığında elenir. Oyuncu
  (takım 0) kendi TC'si düşerse kaybeder; düşman takımların (1-3) hiçbirinde TC
  kalmayınca kazanır.
- **Anıt zaferi (Wonder):** Bir takım, tamamlanmış bir `Wonder` binasını belirli
  süre (`WonderHoldTime`) ayakta tutarsa kazanır. Wonder yıkılırsa sayaç sıfırlanır.
- **Kalıntı zaferi (Relic):** Bir takım, haritadaki **tüm** relikleri belirli süre
  (`RelicHoldTime`) elinde tutarsa kazanır. Tek bir relik kaybedilirse sayaç sıfırlanır.

Ek olarak oyun bittiğinde takım başına bir **kompozit skor** hesaplanır (ordu,
bina, ekonomi, relik ve çağ ağırlıklı) ve sonuç ekranında gösterilir. Skor
şu an ayrı bir "zafer yolu" değil — maç bitiş ekranında gösterilen bir
performans göstergesidir.

---

## 2. Nasıl çalışır (mekanik + formül)

### Tarama döngüsü
`MatchSystem.Update()` sayacı `Time.unscaledDeltaTime` ile azaltır; yani tarama
oyun hızından (`Time.timeScale`) bağımsız olarak her gerçek 1 saniyede bir
çalışır. Sayaç dolunca `CheckEnd()` çağrılır.

### Fetih
`CheckEnd()` tüm binaları gezer, takım başına `tcAlive[]` (yaşayan TC) dizisini
doldurur. Sonra:
- `!tcAlive[0]` → oyuncu kaybeder ("Fetih (TC yıkıldı)").
- Hiçbir düşmanda TC yoksa (`!(tcAlive[1] || tcAlive[2] || tcAlive[3])`) → oyuncu
  kazanır ("Fetih").

Yalnızca **Town Center** sayılır; diğer binaların yıkımı eleme tetiklemez (AoE2
"Sudden Death" moduna benzer davranış).

### Anıt (Wonder)
Her takım için, ayakta ve **inşası bitmiş** (`!underConstruction`) bir Wonder
varsa `_wonderTimer[t]` her tarama turunda `CheckInterval` (=1s) kadar artar;
yoksa sıfırlanır:

```
hasWonder[t] ? _wonderTimer[t] += 1 : _wonderTimer[t] = 0
if (_wonderTimer[t] >= WonderHoldTime) → "Anıt zaferi"
```

Wonder yıkılır veya henüz inşa hâlindeyse sayaç anında 0'a döner.

### Kalıntı (Relic)
Bir takım, haritadaki relik sayısı kadar relik kontrol ediyorsa sayaç ilerler:

```
holdsAllRelics = totalRelics > 0 && relicSystem.CountControlled(t) == totalRelics
holdsAllRelics ? _relicTimer[t] += 1 : _relicTimer[t] = 0
if (_relicTimer[t] >= RelicHoldTime) → "Kalıntı zaferi"
```

`CountControlled(teamId)` (`RelicSystem.cs:44`) tüm reliklerin
`controllingTeam` alanını sayar.

### Relik yakalama (capture) mekaniği
`RelicSystem.Tick()` her kareye bir kez çalışır:
1. Her reliğin `unitsNearby` listesini temizler.
2. Tüm birimleri (her takım) gezer; bir birim, bir reliğin **menzilindeyse**
   (`CaptureRange = 3.5` birim, karesel mesafe `CaptureRangeSq` ile karşılaştırılır)
   o reliğin `unitsNearby` listesine eklenir.
3. Her relikte `UpdateCapture(dt)` çağrılır.

`RelicEntity.UpdateCapture()` mantığı:
- Yakındaki birimleri takıma göre sayar (`counts[4]`).
- En kalabalık tek takımı bulur. **Beraberlik** veya hiç birim yoksa → "contested"
  (kimse yakalayamaz), `dominant = -1`.
- Baskın takım mevcut sahip değilse: `captureProgress += dt`. İlerleme
  `CaptureSeconds = 5` saniyeye ulaşınca relik o takıma geçer.
- Aksi halde ilerleme `DecayRate = 1.5`/sn ile geri çürür.
- Sahip takım her saniye `GoldPerSecond = 0.5` altın pasif gelir kazanır
  (kesirli birikir, tam altın olunca verilir).

**Formül özeti (yakalama):** boş relik, tek bir takımın birimi 5 saniye kesintisiz
üstünde durunca o takıma geçer. Karşı takım gelince beraberlik → ilerleme durur
ve saniyede 1.5 hızla çürür.

### Kompozit skor formülü
`MatchSystem.Score(gm, team)`:

```
skor = units*10 + military*15 + blds*25 + resTotal/10 + relics*100 + age*75
```

- `units` = takımın tüm birimleri, `military` = Villager olmayanlar
- `blds` = yaşayan binalar, `resTotal` = food+wood+gold+stone toplamı
- `relics` = kontrol edilen relik, `age` = ulaşılan çağ indeksi (0-3)

Oyun bitince `End()` skoru altyazıda gösterir: `"{reason} · Skorun: {score}"`.

---

## 3. Gerçek statlar (koddan)

> Not: Bu sayfa için sağlanan stat JSON girdisi **boştu**; aşağıdaki sayılar
> doğrudan kaynak dosyalardan (Read ile teyit edilmiş sabitler) alınmıştır. Her
> satır ilgili `file:line` anchor'ına bağlıdır.

### Zafer eşikleri (MatchSystem)

| Stat | Değer | Kaynak |
|---|---|---|
| Tarama aralığı (`CheckInterval`) | 1 sn | [MatchSystem.cs:16](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L16) |
| Anıt tutma süresi (`WonderHoldTime`) | 60 sn | [MatchSystem.cs:17](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L17) |
| Kalıntı tutma süresi (`RelicHoldTime`) | 60 sn | [MatchSystem.cs:18](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L18) |
| Skor: birim ağırlığı | ×10 | [MatchSystem.cs:140](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L140) |
| Skor: askeri birim ağırlığı | ×15 | [MatchSystem.cs:140](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L140) |
| Skor: bina ağırlığı | ×25 | [MatchSystem.cs:140](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L140) |
| Skor: kaynak ağırlığı | toplam ÷ 10 | [MatchSystem.cs:140](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L140) |
| Skor: relik ağırlığı | ×100 | [MatchSystem.cs:140](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L140) |
| Skor: çağ ağırlığı | ×75 | [MatchSystem.cs:140](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L140) |

### Relik yakalama (RelicSystem + RelicEntity)

| Stat | Değer | Kaynak |
|---|---|---|
| Ele geçirme menzili (`CaptureRange`) | 3.5 birim | [RelicSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L14) |
| Yakalama süresi (`CaptureSeconds`) | 5 sn | [RelicEntity.cs:24](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L24) |
| İlerleme çürüme hızı (`DecayRate`) | 1.5 / sn | [RelicEntity.cs:25](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L25) |
| Pasif altın geliri (`GoldPerSecond`) | 0.5 / sn | [RelicEntity.cs:26](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L26) |
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

> **Özet maliyet: 500 odun / 800 altın / 600 taş** (food yok). En pahalı
> bina; yalnızca Imperial çağında inşa edilebilir.

---

## 4. Strateji & counter

### Fetih
- **Saldıran:** Rakibin TC'sini odakla. Diğer binaları yıkmak gerekmez — TC
  düşünce o takım elenir. Kuşatma birimleri (siege) zırhı bypass ettiği için
  TC baskısında en verimli.
- **Savunan:** TC'yi kale/duvar ve garnizonlu birimlerle koru. TC yıkılırsa maç
  biter; tek noktalı yenilgi riski en yüksek koşul budur.

### Anıt (Wonder)
- **İnşa eden:** Anıt pahalıdır (**500 odun / 800 altın / 600 taş**) ve yalnızca
  Imperial çağında dikilebilir. İnşa bitince **60 saniye** ayakta kalması yeterli —
  bu kısa süre, geç oyunda güçlü bir ekonomiyle sürpriz bir zafer açar.
- **Counter:** Sayaç tüm takımlara `VictoryStatus` üzerinden HUD'da görünür.
  Wonder 3000 HP'ye ve yüksek zırha (5/8) sahip olsa da yıkılabilir; siege
  birimleriyle saldır. Yıkım sayacı **anında** sıfırlar — yani 59. saniyede
  yıkmak bile yeterli.

### Kalıntı (Relic)
- **Toplayan:** Haritadaki **tüm** relikleri ele geçir. Bir relik, üstünde tek
  takımın birimi 5 saniye durunca yakalanır; sonra 60 saniye **hepsini** elinde
  tut. Kontrol edilen her relik ayrıca saniyede 0.5 altın pasif gelir verir.
- **Counter:** Tek bir reliğe birim gönder — beraberlik yaratınca rakibin yakalama
  ilerlemesi durur ve çürür (1.5/sn). Bir relik bile kaybedilirse 60s sayaç
  sıfırlanır. Relikler yok edilemez (`IDamageable` değil), yalnızca kontrol
  el değiştirir.

### Skor
- Skor doğrudan kazanma yolu değil; ama maç bittiğinde performansı özetler.
  Relik (×100) ve çağ (×75) ağırlıkları en yüksek; geç oyunda Imperial'e
  ulaşmak ve relik tutmak skoru en hızlı yükselten yatırımlardır.

---

## 5. Çapraz bağlantılar

- [01-game-flow-ages.md](./01-game-flow-ages.md) — Imperial çağı (Wonder ön
  koşulu) ve çağ ilerlemesi.
- [04-buildings.md](./04-buildings.md) — Town Center, Wonder ve garnizon; bina
  HP/zırh tablosu.
- [02-units.md](./02-units.md) — relik yakalayan birimler ve siege (Wonder/TC
  yıkımı).
- [07-combat-counters.md](./07-combat-counters.md) — siege'in zırhı bypass etmesi,
  Wonder/TC kuşatması.
- [08-economy-trade.md](./08-economy-trade.md) — relik pasif altın geliri ve
  Wonder maliyetini karşılayan ekonomi.
- [09-ai-difficulty.md](./09-ai-difficulty.md) — AI'nin relikleri fırsatçı
  toplaması ve zafer koşullarına tepkisi.
- [11-controls-ui-feedback.md](./11-controls-ui-feedback.md) — `VictoryStatus`
  HUD geri sayımı, sonuç ekranı ve R ile restart.

---

## 6. Kod referansları (file:line, derivation)

- **Zafer hakemi & döngü:**
  [MatchSystem.cs:39-51](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L39)
  — `Update()` `unscaledDeltaTime` ile sayaç, `CheckEnd()` çağrısı.
- **Fetih kontrolü:**
  [MatchSystem.cs:92-96](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L92)
  — TC yaşayan takım taraması; oyuncu/düşman eleme.
- **Anıt sayacı:**
  [MatchSystem.cs:77-78](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L77)
  — `WonderHoldTime` eşiği; `!underConstruction` koşulu satır 67.
- **Kalıntı sayacı:**
  [MatchSystem.cs:81-84](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L81)
  — `CountControlled(t) == totalRelics` → `RelicHoldTime`.
- **Skor formülü:**
  [MatchSystem.cs:119-141](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L119)
  — kompozit skor türetimi.
- **Resign (teslim):**
  [MatchSystem.cs:32-37](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L32)
  — oyuncu iradi olarak kaybeder.
- **Relik proximity taraması:**
  [RelicSystem.cs:17-41](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L17)
  — `CaptureRange = 3.5` (satır 14) ile karesel mesafe testi.
- **Kontrol sayımı:**
  [RelicSystem.cs:44-52](../../AgeOfArenaUnity/Assets/Scripts/RelicSystem.cs#L44)
  — `CountControlled(teamId)`.
- **Relik yakalama state machine:**
  [RelicEntity.cs:45-94](../../AgeOfArenaUnity/Assets/Scripts/RelicEntity.cs#L45)
  — `CaptureSeconds = 5` (satır 24), `DecayRate = 1.5` (satır 25),
  `GoldPerSecond = 0.5` (satır 26).
- **Wonder maliyeti türetimi:** constructor alan sırası
  [BuildingDefs.cs:35-50](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L35)
  `(food, wood, gold, stone)`; Wonder satırı
  [BuildingDefs.cs:83](../../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs#L83)
  `0, 500, 800, 600` → **500W / 800G / 600S**.

---

## 7. AoE2 farkı (reference köprü)

Karşılaştırma kaynağı:
[06-victory-game-modes.md](../reference/06-victory-game-modes.md).

| Konu | AoA (kod) | AoE2 (referans) |
|---|---|---|
| Fetih | TC yıkımı = eleme; tüm düşman TC'leri düşünce zafer | Tüm bina/birim yok edilmeli; "Conquest" daha geniş |
| Sudden Death | Fiilen aktif (yalnızca TC sayılır) | Ayrı bir mod; TC = anında eleme |
| Wonder maliyeti | **500 odun / 800 altın / 600 taş**, yalnızca Imperial | 1000 odun + 1000 taş + 1000 altın |
| Wonder tutma süresi | 60 sn (`WonderHoldTime`) | ~200 yıl ≈ ~10 dk gerçek süre |
| Relik yakalama | Birim 5 sn üstünde dur (Monk gerekmez) | Monk ile topla → Monastery'e götür |
| Relik zafer süresi | Tüm relikler 60 sn (`RelicHoldTime`) | Tüm relikler ~200 yıl (~10 dk) |
| Relik yok edilebilir mi | Hayır; sadece kontrol el değiştirir | Monastery yıkılırsa relik düşer |
| Relik pasif geliri | 0.5 altın/sn | Relik başına altın trickle (benzer) |
| Skor zaferi | Ayrı zafer yolu değil; bitiş ekranı göstergesi | Süre dolunca en yüksek skor kazanır |
| Regicide / Kral | Yok | Kral öldür = eleme |
| Deathmatch / Nomad | Yok | Var (yüksek başlangıç kaynağı / TC'siz başlangıç) |
| Diplomasi (Allied/Neutral) | Kodda zafer mantığı sabit 4 takım düşman varsayar | Tam diplomasi + tribute vergisi |

---

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| REGI | Oyun modu | Regicide (Kral birimi; Kral ölünce eleme) | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Regicide | Yüksek |
| SCRT | Zafer yolu | Süre dolunca skor zaferi (timer + en yüksek skor kazanır) — şu an skor sadece bitiş ekranı göstergesi | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Score | Orta |
| RWND | Denge | Wonder/Relik tutma süreleri 60 sn; AoE2'ye göre çok kısa, ayarlanabilir zafer süresi opsiyonu | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Wonder/§Relic | Düşük |
| RMNK | Mekanik | Relik yakalamayı Monk birimine + Monastery'e taşıma zincirine bağla (şu an her birim yakalar) | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Relic | Yüksek |
| DMTC | Oyun modu | Deathmatch (yüksek başlangıç kaynağı, askeri odak) | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Deathmatch | Orta |
| NOMD | Oyun modu | Nomad (TC'siz dağınık başlangıç) | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Nomad | Orta |
| VSTA | UI | `SetStatus` yalnızca ilk countdown'ı gösteriyor; "en acil" sayacı seçme mantığı eksik (kod yorumuyla çelişiyor) | — (iç tutarlılık) | Düşük |
| DIPL | Sistem | Zafer mantığı sabit 4-takım-düşman varsayıyor; müttefik/diplomasi zafer kontrolü yok | [06-victory-game-modes.md](../reference/06-victory-game-modes.md) §Diplomasi | Yüksek |
