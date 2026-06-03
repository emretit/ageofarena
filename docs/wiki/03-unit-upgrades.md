# Birim Yükseltme Zincirleri — AoA Wiki

> AoA'da birim yükseltmeleri (Man-at-Arms, Crossbowman, Paladin vb.) ayrı bir birim
> tipi yaratmaz. Bunun yerine **takım çapında stat bonusu** veren araştırılabilir
> teknolojilerdir: bir kez araştırılınca o takımın ilgili `UnitType`'ına ait *tüm*
> birimleri (geçmişte üretilmiş + ileride üretilecek) güçlenir. Birimin görünen adı
> ve `UnitType` enum'u değişmez; yalnızca saldırı/HP/menzil değerleri artar.
>
> **Kod kaynağı (tek doğruluk):**
> [TechDefs.cs](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs) (maliyet/çağ/önkoşul),
> [TechState.cs](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs) (bonus formülleri),
> [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs) (`TechType` enum),
> [ResearchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs) (retroaktif uygulama),
> [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs) (canlı okuma).

---

## 1. Ne olduğu

AoE2'de Militia → Man-at-Arms → Long Swordsman → Champion zinciri birimi *başka bir
birime dönüştürür*. AoA'da bu model **tier-terfi teknolojisi** olarak modellenmiştir:
her tier ayrı bir `TechType`'tır ([GameTypes.cs:44](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L44)) ve araştırıldığında
ilgili birim hattına eklenen stat bonusu **diğer tier'lerle birikir (stack)**.

AoA'da kodda tanımlı **üç tam tier-terfi hattı** vardır:

| Hat | `UnitType` | Tier 2 | Tier 3 / 4 (Imperial) | Üretildiği bina |
|---|---|---|---|---|
| Piyade (Militia line) | `Militia` | `ManAtArms` (Feudal) | `Longswordsman` (Castle) → `Champion` (Imperial) | Barracks |
| Okçu (Archer line) | `Archer` | `Crossbowman` (Castle) | `Arbalest` (Imperial) | ArcheryRange |
| Süvari (Cavalry line) | `Cavalry` | `Cavalier` (Castle) | `Paladin` (Imperial) | Stable |

Piyade hattı bir tier fazladır: `ManAtArms` zaten Feudal'da gelir, yani Militia
hattı 4 kademelidir (Militia → ManAtArms → Longswordsman → Champion).

Bunlara ek olarak **flat "blacksmith" yükseltmeleri** (Forging, Fletching, Bodkin,
ScaleMail, Bloodlines) tier değildir ama aynı birim statlarını besler — burada
formüllere dahildirler çünkü efektif statı belirlerler.

---

## 2. Nasıl çalışır (mekanik + formül)

**Araştırma akışı.** Bir tech `ResearchSystem.Apply(type, teamId)` ile uygulanır
([ResearchSystem.cs:86](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L86)):

1. Uygulama öncesi her `UnitType` için mevcut HP bonusu kaydedilir
   ([ResearchSystem.cs:97](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L97)).
2. `tech.Mark(type)` ile araştırılan set'e eklenir; `Version` artar
   ([TechState.cs:20](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L20)).
3. HP delta'sı (yeni − eski) hesaplanır ve o takımın **zaten sahada olan** tüm
   birimlerine `maxHp += d; hp += d` olarak uygulanır
   ([ResearchSystem.cs:130](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L130)).

Yani HP yükseltmesi **retroaktiftir** — eski birimler de anında güçlenir.

**Canlı okunan statlar.** Saldırı ve menzil bonusları depolanmaz; her çağrıda
`TechState`'ten *canlı* okunur:

- Saldırı: `AttackDamage = BaseAttackDamage + TeamTech.AttackBonus(type)`
  ([UnitEntity.cs:115](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L115)).
- Menzil: `AttackRange = BaseAttackRange + TeamTech.RangeBonus(type)`
  ([UnitEntity.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125)).
- HP: yeni doğan birim `Start()`'ta o anki `HpBonus`'u alır
  ([UnitEntity.cs:207](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L207)).

**Birikim (stack) formülleri** ([TechState.cs:27](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L27)):

```
# Saldırı (additive)
MilitiaLineAtk = (ManAtArms ? +1) + (Longswordsman ? +2) + (Champion ? +2)
CavalryLineAtk = (Cavalier   ? +2) + (Paladin       ? +3)
ArcherLineAtk  = (Crossbowman? +2) + (Arbalest      ? +2)

AttackBonus(Militia) = MeleeAttackBonus(Forging? +2) + MilitiaLineAtk
AttackBonus(Cavalry) = MeleeAttackBonus(Forging? +2) + CavalryLineAtk
AttackBonus(Archer)  = ArcherAttackBonus(Fletching? +1; Bodkin? +1) + ArcherLineAtk

# Menzil (yalnız Archer)
RangeBonus(Archer) = (Fletching? +0.5) + (Crossbowman? +0.5) + (Arbalest? +0.5)

# HP (additive)
HpBonus(Militia) = (ScaleMail? +20) + (ManAtArms? +10) + (Longswordsman? +15) + (Champion? +20)
HpBonus(Cavalry) = (ScaleMail? +20) + (Bloodlines? +20) + (Cavalier? +20) + (Paladin? +25)
HpBonus(Archer)  = (Crossbowman? +10) + (Arbalest? +15)
```

**Önkoşul zinciri.** Imperial/Castle tier'leri bir önceki tier'i ister
([TechDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L57)). `ForBuilding`
filtresi hem çağı hem önkoşulu doğrular
([TechDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L89)): `Longswordsman`
ancak `ManAtArms` araştırılmışsa, `Champion` ancak `Longswordsman` araştırılmışsa
listelenir. Okçu/süvari hattının tier 2'si (`Crossbowman`, `Cavalier`) önkoşulsuzdur
ama Castle çağı şartı vardır.

---

## 3. Gerçek statlar (koddan)

> GİRDİ olarak verilen "gerçek stat JSON" **boştu** (`[]`). Bu nedenle aşağıdaki
> tüm değerler doğrudan kaynak koddan türetilmiştir; her satır ilgili `*.cs:NN`
> satırına bağlanır. Koddan teyit edilemeyen hiçbir değer eklenmemiştir.

### 3.1 Tier-terfi teknolojileri — maliyet / çağ / önkoşul

| Tech | Çağ | Food | Wood | Gold | Stone | Süre (s) | Bina | Önkoşul | Kaynak |
|---|---|---|---|---|---|---|---|---|---|
| `ManAtArms` | Feudal | 100 | 0 | 40 | 0 | 25 | Barracks | — | [TechDefs.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L56) |
| `Longswordsman` | Castle | 150 | 0 | 100 | 0 | 30 | Barracks | `ManAtArms` | [TechDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L57) |
| `Champion` | Imperial | 200 | 0 | 150 | 0 | 35 | Barracks | `Longswordsman` | [TechDefs.cs:58](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L58) |
| `Crossbowman` | Castle | 150 | 0 | 100 | 0 | 30 | ArcheryRange | — | [TechDefs.cs:59](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L59) |
| `Arbalest` | Imperial | 200 | 0 | 150 | 0 | 35 | ArcheryRange | `Crossbowman` | [TechDefs.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L60) |
| `Cavalier` | Castle | 150 | 0 | 100 | 0 | 30 | Stable | — | [TechDefs.cs:61](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L61) |
| `Paladin` | Imperial | 200 | 0 | 150 | 0 | 35 | Stable | `Cavalier` | [TechDefs.cs:62](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L62) |

### 3.2 Tier başına stat artışı (additive bonus)

| Tech | Hat | +Saldırı | +HP | +Menzil | Kaynak (atk / hp / range) |
|---|---|---|---|---|---|
| `ManAtArms` | Militia | +1 | +10 | — | [TechState.cs:27](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L27) / [TechState.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L56) / — |
| `Longswordsman` | Militia | +2 | +15 | — | [TechState.cs:28](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L28) / [TechState.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L57) / — |
| `Champion` | Militia | +2 | +20 | — | [TechState.cs:29](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L29) / [TechState.cs:58](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L58) / — |
| `Crossbowman` | Archer | +2 | +10 | +0.5 | [TechState.cs:32](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L32) / [TechState.cs:63](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L63) / [TechState.cs:48](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L48) |
| `Arbalest` | Archer | +2 | +15 | +0.5 | [TechState.cs:33](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L33) / [TechState.cs:64](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L64) / [TechState.cs:49](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L49) |
| `Cavalier` | Cavalry | +2 | +20 | — | [TechState.cs:30](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L30) / [TechState.cs:61](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L61) / — |
| `Paladin` | Cavalry | +3 | +25 | — | [TechState.cs:31](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L31) / [TechState.cs:62](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L62) / — |

### 3.3 Destekleyici flat yükseltmeler (tier değil, ama statı besler)

| Tech | Etkilediği | +Saldırı | +HP | +Menzil | Kaynak |
|---|---|---|---|---|---|
| `Forging` | Militia, Cavalry | +2 | — | — | [TechState.cs:23](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L23) |
| `Fletching` | Archer | +1 | — | +0.5 | [TechState.cs:24](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L24), [TechState.cs:47](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L47) |
| `Bodkin` | Archer | +1 | — | — | [TechState.cs:24](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L24) |
| `ScaleMail` | Militia, Cavalry | — | +20 | — | [TechState.cs:55](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L55), [TechState.cs:59](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L59) |
| `Bloodlines` | Cavalry | — | +20 | — | [TechState.cs:60](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L60) |

### 3.4 Türetilen efektif statlar (taban + tüm tier'ler)

Taban değerler: `BaseAttackDamage` ([UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96)),
`BaseAttackRange` ([UnitEntity.cs:105](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L105)),
taban `maxHp` ([UnitFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs)).
Aşağıdaki tablolar **sadece tier teknolojileri** araştırılmış varsayar (flat/civ
bonusları hariç) ve birikimli (kümülatif) toplamı gösterir.

**Piyade hattı** (taban: Atk 5, HP 40):

| Kademe | Toplam Atk | Toplam HP | Türetme |
|---|---|---|---|
| Militia (taban) | 5 | 40 | [UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96), [UnitFactory.cs:49](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L49) |
| + ManAtArms | 6 | 50 | 5+1, 40+10 |
| + Longswordsman | 8 | 65 | +2, +15 |
| + Champion | 10 | 85 | +2, +20 |

**Okçu hattı** (taban: Atk 4, HP 30, Menzil 6.5):

| Kademe | Toplam Atk | Toplam HP | Toplam Menzil | Türetme |
|---|---|---|---|---|
| Archer (taban) | 4 | 30 | 6.5 | [UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96), [UnitEntity.cs:105](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L105), [UnitFactory.cs:72](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L72) |
| + Crossbowman | 6 | 40 | 7.0 | +2, +10, +0.5 |
| + Arbalest | 8 | 55 | 7.5 | +2, +15, +0.5 |

**Süvari hattı** (taban: Atk 8, HP 75):

| Kademe | Toplam Atk | Toplam HP | Türetme |
|---|---|---|---|
| Cavalry (taban) | 8 | 75 | [UnitEntity.cs:96](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96), [UnitFactory.cs:102](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L102) |
| + Cavalier | 10 | 95 | +2, +20 |
| + Paladin | 13 | 120 | +3, +25 |

> Not: Süvari ek olarak ilk vuruşta `ChargeMultiplier` 2.5× alır
> ([UnitEntity.cs:148](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148)); bu yükseltmeden
> bağımsızdır. Trebuchet/Scout/Medic/Spearman/Monk/Galley/Longbowman için **kodda
> tanımlı bir tier-terfi yükseltme hattı yoktur** — yalnızca yukarıdaki üç hat
> tier teknolojisine sahiptir.

---

## 4. Strateji & counter

- **Imperial atılımı pahalıdır.** Bir hattı sonuna kadar yükseltmek (örn. Cavalry →
  Cavalier → Paladin) sadece 350 food + 250 gold araştırma maliyeti getirir
  ([TechDefs.cs:61](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L61)); buna age-advance
  maliyetleri eklenince tek hatta kilitlenmek riskli olur. Bkz. [05-tech-tree.md](./05-tech-tree.md).
- **HP retroaktif, saldırı her zaman canlı.** Tek bir Imperial tech araştırmak,
  sahadaki *tüm* ordunun HP'sini anında bump eder
  ([ResearchSystem.cs:130](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L130)) — savaş
  ortasında bile araştırmayı bitirmek bir "comeback" hamlesidir.
- **Paladin en yüksek tek-kademe atlamasıdır** (+3 atk / +25 HP); Bloodlines (+20 HP)
  ile birlikte süvari yumruğunu maksimize eder. Karşılığı: Spearman'in 3× anti-cavalry
  çarpanı ([UnitEntity.cs:150](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L150)) Paladin'in
  yüksek HP'sini de eritebilir. Counter matrisi için bkz. [07-combat-counters.md](./07-combat-counters.md).
- **Arbalest menzili 7.5'e çıkar** (Fletching eklenirse 8.0); bu okçuların piyadeyi
  kite etmesini kolaylaştırır ama Britons'un `archerRangeBonus`'u
  ([UnitEntity.cs:127](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L127)) bunun da üstüne
  biner. Bkz. [06-civilizations.md](./06-civilizations.md).
- **Önkoşul kilidi.** `Champion`'ı almak için önce `Longswordsman` araştırmak
  zorunludur; tier atlanamaz ([TechDefs.cs:90](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L90)).
  Bu, hatları "yarım bırakma" cezasını gerçek kılar.

---

## 5. Çapraz bağlantılar

- [02-units.md](./02-units.md) — taban birim statları (HP/atk/menzil), `UnitType` listesi.
- [05-tech-tree.md](./05-tech-tree.md) — tüm `TechType`'ların ağaç görünümü, çağ kapıları.
- [07-combat-counters.md](./07-combat-counters.md) — DamageType / armor / counter çarpanları.
- [06-civilizations.md](./06-civilizations.md) — civ bonusları (infantryAttackMult, cavalryHpMult, archerRangeBonus).
- [01-game-flow-ages.md](./01-game-flow-ages.md) — çağ ilerlemesi (Feudal/Castle/Imperial gate'leri).
- [04-buildings.md](./04-buildings.md) — Barracks / ArcheryRange / Stable üretim binaları.

---

## 6. Kod referansları (file:line, türetme)

| Konu | Dosya:satır | Açıklama |
|---|---|---|
| `TechType` enum (tier listesi) | [GameTypes.cs:44](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L44) | ManAtArms…Paladin tier terfileri |
| Tier maliyet/çağ/önkoşul tablosu | [TechDefs.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L56) | `TechDef` kayıtları |
| Önkoşul filtresi | [TechDefs.cs:89](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L89) | `ForBuilding` çağ + `requires` kontrolü |
| Saldırı bonusu birikimi | [TechState.cs:27](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L27) | MilitiaLineAtk / CavalryLineAtk / ArcherLineAtk |
| `AttackBonus(UnitType)` switch | [TechState.cs:36](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L36) | flat + hat bonusu toplamı |
| `RangeBonus` (Archer) | [TechState.cs:45](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L45) | Fletching/Crossbowman/Arbalest +0.5 |
| `HpBonus(UnitType)` switch | [TechState.cs:53](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L53) | tier + flat HP birikimi |
| Canlı saldırı okuma | [UnitEntity.cs:115](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L115) | base + TeamTech.AttackBonus |
| Canlı menzil okuma | [UnitEntity.cs:125](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) | base + TeamTech.RangeBonus |
| Yeni birime HP uygulama | [UnitEntity.cs:207](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L207) | `Start()`'ta HpBonus |
| Retroaktif HP delta uygulama | [ResearchSystem.cs:93](../../AgeOfArenaUnity/Assets/Scripts/ResearchSystem.cs#L93) | sahadaki birimlere maxHp/hp += delta |

**Türetme örneği (Champion efektif HP):** taban 40 ([UnitFactory.cs:49](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L49))
+ ManAtArms 10 ([TechState.cs:56](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L56))
+ Longswordsman 15 ([TechState.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L57))
+ Champion 20 ([TechState.cs:58](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L58)) = **85 HP** (ScaleMail/civ hariç).

---

## 7. AoE2 farkı (referans köprü)

Tam AoE2 birim zincirleri ve baz statlar için: [../reference/02-units-upgrade-chains.md](../reference/02-units-upgrade-chains.md).

Öne çıkan farklar:

- **Birim dönüşümü yok.** AoE2'de upgrade birimi yeni bir birime *çevirir* (Militia →
  Man-at-Arms ayrı bir entity). AoA'da birim hep aynı `UnitType` kalır; yalnızca
  takım çapında stat artar. Bu yüzden AoA'da "kısmi yükseltilmiş" karma ordu olmaz —
  bir hat yükseltilince o hattın tamamı güçlenir.
- **Eksik hatlar.** AoE2'deki Spearman→Pikeman→Halberdier, Skirmisher, Cavalry Archer,
  Eagle Warrior, Camel, tüm siege (Ram/Mangonel/Scorpion/Onager) ve naval (Galley→
  Galleon, Fire Ship) **yükseltme zincirleri** AoA'da yoktur. AoA'da Spearman, Scout,
  Medic, Monk, Trebuchet, Galley, Longbowman birimleri mevcut ama bunların *tier-terfi
  teknolojileri tanımlı değildir* — tek seviye birimlerdir.
- **Stat ölçeği farklı.** AoA Paladin'i 120 efektif HP (kod), AoE2 Paladin'i 160 HP.
  AoA tüm değerleri kendi dengesiyle daha düşük tutar; doğrudan AoE2 sayısı taşınmamıştır.
- **Çağ eşleşmesi kaymıştır.** AoE2'de Champion ayrı, Two-Handed Swordsman ayrı bir
  Imperial kademesidir; AoA bu iki kademeyi tek `Champion` tech'ine indirger.
- **Önkoşul modeli aynı.** Hem AoE2 hem AoA bir üst tier için bir önceki tier'i
  zorunlu kılar (AoA: [TechDefs.cs:57](../../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs#L57) `requires`).

---

## 8. Eksikler / Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| `SPUP` | tier hattı | Spearman → Pikeman → Halberdier yükseltme tech'leri (anti-cavalry tier'leri); şu an Spearman tek seviye | [ref §Spearman Hattı](../reference/02-units-upgrade-chains.md) | M |
| `SCUP` | tier hattı | Scout → Light Cavalry → Hussar hattı (Scout şu an hasarsız tek seviye keşif) | [ref §Scout Hattı](../reference/02-units-upgrade-chains.md) | M |
| `SKUP` | yeni hat | Skirmisher → Elite Skirmisher (archer-counter hattı kodda hiç yok) | [ref §Skirmisher Hattı](../reference/02-units-upgrade-chains.md) | L |
| `SIUP` | yeni sınıf | Siege yükseltme zincirleri (Ram/Mangonel→Onager/Scorpion); Trebuchet dışında siege tier yok | [ref §Kuşatma](../reference/02-units-upgrade-chains.md) | L |
| `NVUP` | yeni hat | Galley → War Galley → Galleon naval yükseltme hattı (Galley tek seviye) | [ref §Deniz Birimleri](../reference/02-units-upgrade-chains.md) | M |
| `LBUP` | tier hattı | Longbowman için Elite Longbowman yükseltmesi (unique unit, tek seviye) | [ref §AoA Karşılaştırması](../reference/02-units-upgrade-chains.md) | S |
| `ARMV` | armor tech | Birim zırh yükseltmeleri (Padded/Leather/Ring Archer Armor, Plate Mail/Barding); HpBonus var ama armor tier yok | [ref §Süvari/Knight Hattı](../reference/02-units-upgrade-chains.md) | M |
| `RENM` | UI/feedback | Yükseltme sonrası birim *adının* değişmesi (Militia → "Şampiyon" gösterimi); kodda display rename yok | [ref §Militia Hattı](../reference/02-units-upgrade-chains.md) | S |
