# Savaş & Counter Sistemi — AoA Wiki

> Birimler birbirini taş-kâğıt-makas mantığıyla dengeler: Spearman süvariye, Cavalry charge'ı yumuşak hedeflere, Trebuchet binaya. AoA'da counter mekaniği **çarpan (multiplier) tabanlıdır** — AoE2'deki "armor class'a göre düz bonus damage" yerine her saldırgan tipi hedef tipine göre hasarını katlar. Tüm muharebe döngüsü `CombatSystem.Tick` içinde sürer; statlar `UnitEntity` içindeki `switch`-tabanlı property'lerden gelir; uzaktan saldırılar `Projectile` ile, hedef birleştirmesi `IDamageable` ile yapılır.
>
> **Kod kaynağı:** [CombatSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs), [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs), [Projectile.cs](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs), [IDamageable.cs](../../AgeOfArenaUnity/Assets/Scripts/IDamageable.cs)

---

## 1. Ne olduğu

Savaş sistemi, birimlerin ve binaların birbirine hasar verdiği, hedef seçtiği ve öldüğü çekirdek döngüdür. AoA'da üç katman vardır:

1. **Muharebe sürücüsü** (`CombatSystem`): her frame tüm birimleri dolaşır; saldırı emri varsa hedefi kovalar, menzile girince cooldown'a bağlı vuruş yapar. Boştaki saldırgan birimler aggro yarıçapı içindeki en yakın düşmanı otomatik seçer.
2. **Birim statları** (`UnitEntity`): hasar, menzil, saldırı aralığı, aggro yarıçapı, zırh ve counter çarpanları birim tipine göre `switch` ifadeleriyle tanımlanır.
3. **Hasar uygulama** (`IDamageable.TakeDamage`): zırh tipine (`DamageType.Melee/Pierce/Siege`) göre azaltma yapar; minimum 1 hasar garantisi vardır.

Counter sistemi dört özel çarpanla kurulur: **Spearman → Cavalry** (3×), **Cavalry charge** (2.5× ilk vuruş), **Trebuchet → bina** (3×) ve arkadan vuruş (**flanking**, 1.25×). Ek olarak Monk dönüştürme ve Medic iyileştirme destek mekanikleri vardır.

---

## 2. Nasıl çalışır (mekanik + formül)

### Muharebe döngüsü (`CombatSystem.Tick`)
Her frame her birim için:
- Saldırı cooldown'u azalır ([CombatSystem.cs:33](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L33)).
- Cavalry savaş dışındayken `chargeTimer` dolar ([CombatSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L36)).
- `attackTarget` varsa `StepCombat`; yoksa `attackMove` aktifse `StepAttackMove`; o da yoksa ve birim boştaysa `TryAcquire` ile aggro taraması ([CombatSystem.cs:43-49](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L43)).

### Menzil ve yaklaşma
Efektif erişim = `AttackRange + target.Radius` ([CombatSystem.cs:94](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L94)). Hedef bu erişim + 0.15 toleransının dışındaysa birim `MovingToAttack` durumuna geçip `RepathInterval` (0.25 s) throttle ile yaklaşır; içindeyse `Attacking` durumunda durup vurur.

### Hasar formülü (saldırı tarafı)
Bir vuruş anında hasar şöyle hesaplanır ([CombatSystem.cs:123-153](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L123)):

```
dmg = AttackDamage                       # base + tech + civ çarpanı
if Spearman ve hedef Cavalry:  dmg *= 3.0   # AntiCavalryMultiplier
if hedef BuildingEntity:       dmg *= AntiStructureMultiplier   # Trebuchet 3×
# (melee yolu)
if Cavalry ve ChargeReady:     dmg *= 2.5   # ChargeMultiplier, chargeTimer sıfırlanır
if arkadan vuruş (dot > 0.5):  dmg *= 1.25  # flanking
```

`AttackDamage` kendisi de türetilir ([UnitEntity.cs:111-119](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L111)): `base + tech AttackBonus`, ardından piyade (Militia/Spearman) ise civ `infantryAttackMult` ile çarpılır.

### Hasar uygulama ve zırh (hedef tarafı)
`TakeDamage` zırhı hasar tipine göre seçer ([UnitEntity.cs:365-377](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L365)):

```
armor = Pierce ? pierceArmor : Melee ? meleeArmor : 0   # Siege zırhı yok sayar
hp -= max(1, amount - armor)                            # minimum 1 hasar
```

### Uzaktan saldırı (`Projectile`)
Ranged birimler (`IsRanged`: Archer, Trebuchet, Longbowman, Galley) temas yerine ok fırlatır ([CombatSystem.cs:134-136](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L134)). Ok 22 birim/sn homing hareketle hedefe gider, varınca `TakeDamage` uygular, hedef uçuş sırasında ölürse zararsız yok olur ([Projectile.cs:37-59](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L37)).

> **Not:** Flanking bonusu (1.25×) yalnızca melee kolunda uygulanır; ok mermilerinde flanking/charge **kodda uygulanmaz**. Spearman anti-cavalry ve Trebuchet anti-structure çarpanları ise vuruş anında, ranged/melee ayrımından önce hesaplanır.

### Aggro & stance
Boştaki birim `AggroRadius > 0` ve stance `NoAttack` değilse en yakın düşmanı seçer ([CombatSystem.cs:47-49](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L47)). `StandGround` stance'ında birim hedefi kovalamaz, menzil dışına çıkarsa hedefi bırakır ([CombatSystem.cs:99-105](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L99)).

---

## 3. Gerçek statlar (koddan)

> Stat JSON girdisi boş geldiği için tüm sayılar doğrudan koddan teyit edilmiştir; her satır ilgili `switch`/property'nin geçtiği kesin satıra link verir.

### Temel muharebe statları

| Birim | Hasar | Menzil | Saldırı aralığı (s) | Aggro yarıçapı | Hasar tipi |
|---|---|---|---|---|---|
| Militia | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L105) | [1.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L132) | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140) | Melee |
| Archer | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96) | [6.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L105) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L132) | [9](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140) | Pierce |
| Cavalry | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L96) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L105) | [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L132) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140) | Melee |
| Trebuchet | [35](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L97) | [15](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L106) | [5.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) | [15](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L141) | Siege |
| Spearman | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L97) | [1.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L106) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L141) | Melee |
| Longbowman | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L97) | [8.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L106) | [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) | [11](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L141) | Pierce |
| Galley | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L98) | [5.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L107) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L134) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L142) | Pierce |
| Scout | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L100) | varsayılan [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L108) | varsayılan [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L135) | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L143) | Melee |
| Medic | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L100) | varsayılan [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L108) | varsayılan [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L135) | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L143) | Melee |
| Diğer (Villager dahil) | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L101) | [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L108) | [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L135) | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L143) | Melee |

### Counter çarpanları ve özel mekanikler

| Mekanik | Değer | Koşul | Kaynak |
|---|---|---|---|
| Spearman anti-cavalry | ×3.0 | Spearman, hedef Cavalry UnitEntity | [UnitEntity.cs:150](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L150) |
| Trebuchet anti-structure | ×3.0 | Trebuchet, hedef BuildingEntity | [UnitEntity.cs:146](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L146) |
| Cavalry charge | ×2.5 | İlk melee vuruş, `chargeTimer ≥ 4s` | [UnitEntity.cs:148](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148) |
| Charge timer eşiği | 4 s | `ChargeReady` | [UnitEntity.cs:36-37](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L36) |
| Flanking (arkadan) | ×1.25 | `dot(forward, toAttacker) > 0.5`, sadece melee | [CombatSystem.cs:150](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L150) |
| Minimum hasar | 1 | Her zaman (zırh sonrası taban) | [UnitEntity.cs:374](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L374) |

### Destek mekanikleri

| Mekanik | Değer | Kaynak |
|---|---|---|
| Monk dönüştürme süresi | 4 s | [UnitEntity.cs:53](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L53) |
| Monk dönüştürme menzili | 2.5 (2× dışında iptal) | [CombatSystem.cs:246-248](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L246) |
| Medic iyileştirme yarıçapı | 6 | [UnitEntity.cs:166](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L166) |
| Medic iyileştirme gücü | 3 hp/s | [UnitEntity.cs:168](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L168) |
| Veterancy: Veteran (1 kill) | +10 maxHP | [UnitEntity.cs:424-432](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L424) |
| Veterancy: Elite (3 kill) | +10 maxHP (rütbe başına) | [UnitEntity.cs:427](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L427) |
| Mermi hızı | 22 birim/sn | [Projectile.cs:11](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L11) |
| Mermi ömrü (safety despawn) | 3 s | [Projectile.cs:13](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L13) |

> **Zırh değerleri:** `meleeArmor` ve `pierceArmor` `UnitEntity` içinde alandır ([UnitEntity.cs:66-67](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L66)) ama değerleri burada değil `UnitFactory` tarafından atanır; bu sayfanın girdileri arasında olmadığından **kesin zırh sayıları bu dosyada teyit edilemedi**. Detay için [02-units.md](./02-units.md) ve [03-unit-upgrades.md](./03-unit-upgrades.md).

---

## 4. Strateji & counter

- **Süvariye karşı Spearman:** 3× çarpan, Spearman'ın düşük base hasarını (4) etkili 12'ye çıkarır; ucuz cavalry avcısı. Kovalamayı önlemek için süvariyi `StandGround` Spearman hattının üzerine sürmeyin.
- **Okçuya/yumuşak hedefe karşı Cavalry charge:** 4 saniye savaş dışı kaldıktan sonra ilk vuruş 2.5×. Hit-and-run ile her dalışta charge'ı yenileyerek okçu hattını eritebilirsiniz; sürekli temasta charge yenilenmez.
- **Binaya karşı Trebuchet:** 35 base × 3 = 105 efektif yapı hasarı, 15 menzilden. Siege hasar tipi zırhı tamamen yok sayar ([UnitEntity.cs:372](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L372)).
- **Flanking:** Melee birimleri hedefin arkasından sararsa +%25. Kuşatma ve cephe kontrolü kıymetli.
- **Pierce vs Melee:** Okçular Pierce hasar verir → yüksek `pierceArmor`'lı hedefler (örn. zırhlı piyade) okçuya dayanıklıdır; melee saldıranlar `meleeArmor`'a takılır.
- **Monk:** 4 saniyede düşman birimi takıma çevirir; düşük menzil (2.5) ve uzun kanal süresi nedeniyle okçu/süvariyle kolay kesilir.
- **Medic:** Cephe gerisinde 6 yarıçapta saniyede 3 hp iyileştirir; kovalamaz, pozisyoneldir, sürekli baskıda hattı ayakta tutar.

---

## 5. Çapraz bağlantılar

- [02-units.md](./02-units.md) — Birim tipleri, base statlar, zırh değerleri
- [03-unit-upgrades.md](./03-unit-upgrades.md) — Veterancy ve yükseltme zincirleri
- [04-buildings.md](./04-buildings.md) — Bina HP, garnizon, anti-structure hedefleri
- [05-tech-tree.md](./05-tech-tree.md) — `AttackBonus`/`RangeBonus`/`HpBonus` teknolojileri
- [06-civilizations.md](./06-civilizations.md) — `infantryAttackMult`, `archerRangeBonus`, cavalry civ çarpanları
- [11-controls-ui-feedback.md](./11-controls-ui-feedback.md) — HP barları, DamagePopup, hit-flash, ok/kılıç sesleri

---

## 6. Kod referansları (file:line, türetme)

- **Muharebe döngüsü:** [CombatSystem.cs:23-51](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L23) — `Tick` her birim için cooldown, charge timer, hedef dallanması.
- **Vuruş & hasar hesabı:** [CombatSystem.cs:116-162](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L116) — menzil kontrolü, anti-cavalry/anti-structure/charge/flanking çarpanları, ranged vs melee dallanması.
- **Anti-cavalry çarpan:** [CombatSystem.cs:126-128](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L126) (uygulama) + [UnitEntity.cs:150](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L150) (değer 3.0).
- **Charge çarpan:** [CombatSystem.cs:141-143](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L141) + [UnitEntity.cs:147-148](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L147).
- **Flanking:** [CombatSystem.cs:144-150](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L144) — sadece melee kolu, dot > 0.5.
- **AttackDamage türetme:** [UnitEntity.cs:111-119](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L111) — base + tech + civ infantry çarpanı.
- **AttackRange türetme:** [UnitEntity.cs:121-129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L121) — base + tech + civ archer menzil bonusu.
- **DamageKind / IsRanged:** [UnitEntity.cs:152-162](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L152).
- **Zırh & minimum hasar:** [UnitEntity.cs:365-377](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L365).
- **Veterancy:** [UnitEntity.cs:424-434](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L424).
- **Mermi:** [Projectile.cs:9-63](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L9).
- **Hedef arayüzü:** [IDamageable.cs:9-21](../../AgeOfArenaUnity/Assets/Scripts/IDamageable.cs#L9).

---

## 7. AoE2 farkı (reference köprü)

Tam karşılaştırma: [docs/reference/07-unit-counter-system.md](../reference/07-unit-counter-system.md).

| Konu | AoE2 | AoA |
|---|---|---|
| Counter modeli | Armor class'a göre **düz bonus damage** (örn. Halberdier +32 vs Cavalry) | Tip kontrolüne göre **çarpan** (Spearman ×3, Trebuchet ×3, charge ×2.5) |
| Spearman hattı | Spearman/Pikeman/Halberdier 3 kademe, artan bonus | Tek Spearman, sabit ×3 |
| Skirmisher → Archer | Var (+pierce zırh, +bonus) | **Yok** (Skirmisher birimi yok) |
| Camel → Cavalry | Var | **Yok** |
| Ram (pierce immune) | Var (zırh 180) | **Yok**; Trebuchet anti-structure ile kısmen karşılanır |
| Zırh tipleri | Melee + Pierce, ayrı bonus armor class'lar | Melee + Pierce alanları var; Siege zırhı yok sayar |
| Minimum hasar | `max(1, atk + bonus − armor)` | `max(1, amount − armor)` — aynı kural |
| Monk dönüştürme | Conversion + birçok tech (Heresy/Theocracy) | 4 s dönüştürme; destek tech yok |
| Flanking bonusu | **Yok** (AoE2'de arkadan vuruş bonusu yoktur) | Var (×1.25 melee) — AoA özgün |

AoE2 bonus damage referans değerleri (Spearman +15 / Pikeman +22 / Halberdier +32 vs Cavalry) için: [Steam — Bonus Damage Tables](https://steamcommunity.com/sharedfiles/filedetails/?id=641037501), [ageofnotes.com — Spearman Counters](https://ageofnotes.com/units-counters/spearman-counters-93), [AOEDB — Unit Counter Guide](https://aoedb.net/blog/understanding-unit-counters-age-of-empires-2/).

---

## 8. Eksikler / Yapılacaklar

| ID-aday | Sınıf | Eksik | AoE2-ref | Efor |
|---|---|---|---|---|
| SKIRM | Birim/Counter | Skirmisher birimi ve archer-counter (yüksek pierce zırh + anti-archer bonus) yok | reference §"Okçu Karşıtları" | Orta |
| CAMEL | Birim/Counter | Camel Rider ve cavalry-counter çarpanı yok | reference §"Cavalry Karşıtları" | Orta |
| RAM | Birim/Counter | Battering Ram (pierce-immune, bina yıkıcı) yok | reference §"Bina Karşıtları" | Orta |
| ARMC | Stat/Veri | `meleeArmor`/`pierceArmor` değerleri UnitFactory'de; wiki §3'te kesin sayı teyit edilemiyor, merkezi stat tablosu yok | reference §"Zırh Tipleri" | Düşük |
| PIKE | Yükseltme | Spearman → Pikeman → Halberdier kademeli bonus zinciri yok (tek sabit ×3) | reference §"Piyade Karşıtları" | Orta |
| RPROJ | Mekanik | Mermide flanking/charge yok ve area-of-effect (Mangonel/Onager splash) yok | reference §"Kuşatma Silahı Karşıtları" | Yüksek |
| MTECH | Tech | Monk destek teknolojileri (Heresy/Theocracy/Atonement) yok | reference §"Monk Karşıtları" | Düşük |
</content>
