# Savaş & Counter Sistemi — AoA Wiki

> Birimler birbirini taş-kâğıt-makas mantığıyla dengeler: Spearman süvariye, deve süvariye, avcı okçuya, koçbaşı/Trebuchet binaya, Mangonel kümeye. **M7'de counter modeli baştan değişti:** eski "tip kontrolüne göre hasarı katla" (çarpansal) yaklaşımı kaldırıldı; yerine AoE2'deki **additive armor-class** modeli geldi — saldırgan, hedefin ait olduğu zırh sınıf(lar)ına göre **düz (flat) bonus hasar** ekler, sonra hedefin zırhı bu toplamdan **çıkarılır**. Tüm muharebe döngüsü `CombatSystem.Tick` içinde sürer; statlar `UnitEntity` içindeki `switch`-tabanlı property'lerden gelir; uzaktan saldırılar `Projectile` ile, hedef birleştirmesi `IDamageable` ile yapılır.
>
> **Kod kaynağı:** [CombatSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs), [UnitEntity.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs), [GameTypes.cs](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs), [Projectile.cs](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs), [IDamageable.cs](../../AgeOfArenaUnity/Assets/Scripts/IDamageable.cs)

---

## 1. Ne olduğu

Savaş sistemi, birimlerin ve binaların birbirine hasar verdiği, hedef seçtiği ve öldüğü çekirdek döngüdür. AoA'da dört katman vardır:

1. **Muharebe sürücüsü** (`CombatSystem`): her frame tüm birimleri dolaşır; saldırı emri varsa hedefi kovalar, menzile girince cooldown'a bağlı vuruş yapar. Boştaki saldırgan birimler aggro yarıçapı içindeki en yakın düşmanı otomatik seçer.
2. **Birim statları** (`UnitEntity`): hasar, menzil, saldırı aralığı, aggro yarıçapı, zırh, hangi armor-class'a ait olduğu (`ArmorClasses`) ve sınıfa-özel bonus hasar (`BonusDamageVs`) birim tipine göre `switch` ifadeleriyle tanımlanır.
3. **Armor-class modeli** (`GameTypes.ArmorClass`): her birim/bina bir veya birden çok zırh sınıfına aittir; saldırganın bonusu hedefin sınıfına bakarak **eklenir**.
4. **Hasar uygulama** (`IDamageable.TakeDamage`): hasar tipine (`DamageType.Melee/Pierce/Siege`) göre zırhı çıkarır; minimum 1 hasar garantisi vardır.

> **M7 öncesi → sonrası:** Eskiden Spearman süvariye `dmg *= 3.0` (AntiCavalryMultiplier), Trebuchet binaya `dmg *= AntiStructureMultiplier` uygulardı. **Bu çarpanlar tamamen kaldırıldı.** Artık Spearman süvariye `+8` flat bonus, Trebuchet binaya `+70` flat bonus ekler ([UnitEntity.cs:230-236](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L230)); ardından hedefin zırhı düşülür. Charge (×2.5) ve flanking (+%25) çarpanları korundu (bunlar armor-class'a değil, vuruş bağlamına bağlı).

---

## 2. Armor-class sistemi (M7/ARMC)

Her birim/bina bir veya birden çok **zırh sınıfına** aittir. Saldırganın bonusu hedefin taşıdığı bayrağa bakar — bir hedef birden çok sınıfta olabilir, böylece Spearman hattı tüm süvari-türevlerini sayar.

`ArmorClass` flag enum'ı ([GameTypes.cs:35-45](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L35)): `Infantry`, `Cavalry`, `Archer`, `Siege`, `Building`, `Ship`, `Camel`.

### Hangi birim hangi sınıf(lar)da

`UnitEntity.ArmorClasses` ([UnitEntity.cs:200-214](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L200)):

| Birim | Armor-class(ları) |
|---|---|
| Militia, Spearman, TeutonicKnight, Samurai, Eagle, EliteEagle, King | `Infantry` |
| Archer, Skirmisher, Longbowman | `Archer` |
| CavalryArcher, Mangudai | `Archer \| Cavalry` |
| Cavalry, Scout, WarElephant | `Cavalry` |
| Camel | `Cavalry \| Camel` |
| Trebuchet, Mangonel, Ram | `Siege` |
| Galley, FireShip, DemoShip | `Ship` |
| Villager, Monk, Medic | `None` |
| Tüm binalar | `Building` ([BuildingEntity.cs:65](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L65)) |

> **Tasarım notu:** CavalryArcher / Mangudai / Camel bilerek `Cavalry` sınıfını da taşır → böylece Spearman bonusu (+8 vs Cavalry) hepsini sayar. WarElephant da `Cavalry`'dir; Spearman ona da bonus vurur.

---

## 3. Nasıl çalışır (mekanik + formül)

### Muharebe döngüsü (`CombatSystem.Tick`)
Her frame her birim için ([CombatSystem.cs:23-51](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L23)):
- Saldırı cooldown'u azalır ([CombatSystem.cs:33](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L33)).
- Cavalry savaş dışındayken `chargeTimer` dolar ([CombatSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L36)).
- `attackTarget` varsa `StepCombat`; yoksa `attackMove` aktifse `StepAttackMove`; o da yoksa ve birim boştaysa `TryAcquire` ile aggro taraması ([CombatSystem.cs:43-49](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L43)).

### Menzil ve yaklaşma
Efektif erişim = `AttackRange + target.Radius` ([CombatSystem.cs:94](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L94)). Hedef bu erişim + 0.15 toleransının dışındaysa birim `MovingToAttack` durumuna geçip `RepathInterval` (0.25 s) throttle ile yaklaşır; içindeyse `Attacking` durumunda durup vurur. Kuşatma silahları `MinAttackRange` içinde (Trebuchet 3, Mangonel 2, Galley 1.5) yapışık hedefe ateş edemez ([UnitEntity.cs:241-247](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L241)).

### Hasar formülü — YENİ additive model (saldırı tarafı)
Bir vuruş anında hasar şöyle hesaplanır ([CombatSystem.cs:123-158](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L123)):

```
# 1) Taban + armor-class bonusu (M7/BNUS)
dmg = AttackDamage + BonusDamageVs(target)      # CombatSystem.cs:129

# 2a) Ranged ise: ok/mermi fırlat (charge/flanking UYGULANMAZ)
if IsRanged:
    Projectile.Spawn(..., dmg, DamageKind, SplashRadius)

# 2b) Melee ise:
else:
    if ChargeReady (Cavalry):  dmg *= 2.5   # ChargeMultiplier, chargeTimer sıfırlanır
    if arkadan vuruş (dot > 0.5): dmg *= 1.25  # flanking
    target.TakeDamage(dmg, DamageKind)

# 3) TakeDamage içinde zırh DÜŞÜLÜR (hedef tarafı)
armor = (Siege ? 0 : (Pierce ? pierceArmor : meleeArmor)) + tech armor bonus
hp -= max(1, dmg - armor)                        # UnitEntity.cs:525
```

> **Kaldırılan eski yol:** `dmg *= AntiCavalryMultiplier` / `dmg *= AntiStructureMultiplier` artık YOK. Bonus, hedefin `ArmorClasses` bayrağına göre `BonusDamageVs` ile **eklenir**, çarpılmaz. `AttackDamage` kendisi türetilir ([UnitEntity.cs:148-161](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148)): `base + tech AttackBonus`, sonra piyade için `infantryAttackMult`, okçu için `archerAttackMult`, en son `VeteranMult` (+%10/rütbe).

### Bonus hasar tablosu (`BonusDamageVs`)
Saldırgan, hedefin eşleşen armor-class'ı varsa flat bonus ekler ([UnitEntity.cs:223-239](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L223)):

| Saldırgan | Bonus | Hedef sınıfı | Kaynak |
|---|---|---|---|
| Spearman | **+8** | `Cavalry` | [UnitEntity.cs:230](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L230) |
| Camel | **+7** | `Cavalry` | [UnitEntity.cs:231](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L231) |
| Skirmisher | **+3** | `Archer` | [UnitEntity.cs:232](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L232) |
| Trebuchet | **+70** | `Building` | [UnitEntity.cs:233](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L233) |
| Ram | **+16** | `Building` | [UnitEntity.cs:234](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L234) |
| WarElephant | **+30** | `Building` | [UnitEntity.cs:235](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L235) |
| Mangudai | **+10** | `Siege` | [UnitEntity.cs:236](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L236) |

> Bu değerler, base-stat'lı bir counter'ın eski çarpansal modelle yaklaşık aynı efektif hasarı vermesi için ayarlandı: Spearman +8 (eski ×3), Camel +7 (×2), Skirmisher +3 (×2), Trebuchet +70 (×3), Ram +16 (×5).

### Zırh & hasar uygulama (hedef tarafı)
`TakeDamage` zırhı hasar tipine göre seçer, **base armor (UnitFactory) + canlı tech bonusu** toplar ([UnitEntity.cs:513-528](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L513)):

```
armor = Pierce ? (pierceArmor + tech.PierceArmorBonus) :
        Melee  ? (meleeArmor  + tech.MeleeArmorBonus)  :
        0   # Siege hasar tipi zırhı TAMAMEN bypass eder
hp -= max(1, amount - armor)   # her zaman en az 1 hasar
```

**Siege DamageType, zırhı tamamen yok sayar** ([UnitEntity.cs:524](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L524)) — Trebuchet/Mangonel/Ram/DemoShip hedefin meleeArmor/pierceArmor'una takılmaz (yine de min-1 ve `BonusDamageVs` geçerli).

### Zırh kaynakları (base + tech)
- **Base armor:** `UnitFactory` her birime `meleeArmor`/`pierceArmor` atar (aşağıdaki §4 tablosu).
- **Tech bonusu:** `TechState.MeleeArmorBonus` / `PierceArmorBonus`, her vuruşta canlı okunur ([TechState.cs:156-183](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L156)):
  - **Blacksmith zırh hatları:** piyade ScaleMail→ChainMail→PlateMail (+1/+1/+2 melee&pierce), süvari ScaleBarding→ChainBarding→PlateBarding, okçu PaddedArcherArmor→Leather→Ring ([TechState.cs:140-152](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L140)).
  - **Tier yükseltme armor'u:** Longswordsman/Champion +1 melee; Pikeman/Halberdier +1 melee; Cavalier/Paladin +1 melee; Crossbowman/Arbalest +1 pierce ([TechState.cs:161-173](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L161)).
  - **Loom:** Villager +1 zırh; **Ironclad** (Teutons): kuşatma +4 zırh.

### Charge, flanking, veterancy
- **Cavalry charge ×2.5:** Cavalry birimi 4 saniye savaş dışı kalınca `ChargeReady` olur; ilk **melee** vuruşta hasar ×2.5, `chargeTimer` sıfırlanır ([UnitEntity.cs:45](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L45), [256](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L256); uygulama [CombatSystem.cs:139-140](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L139)).
- **Flanking +%25:** Melee saldırgan hedefin **arkasından** vurursa (`dot(forward, toAttacker) > 0.5`) hasar ×1.25 ([CombatSystem.cs:141-147](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L141)).
- **Veterancy +%10/rütbe:** 1 kill → Veteran (×1.1), 3 kill → Elite (×1.2), `VeteranMult` ile `AttackDamage` içine girer; her rütbe +10 maxHP ([UnitEntity.cs:57-61](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L57), [575-584](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L575)).

> **Önemli:** Charge ve flanking yalnızca **melee** kolunda uygulanır ([CombatSystem.cs:136-157](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L136)). Ranged mermilerde (ok/taş) charge/flanking **uygulanmaz**; sadece `AttackDamage + BonusDamageVs` taşınır. `BonusDamageVs` ise ranged/melee ayrımından **önce** ([CombatSystem.cs:129](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L129)) hesaplandığı için her iki kolda da geçerlidir.

### Uzaktan saldırı & splash (`Projectile`)
Ranged birimler (`IsRanged`: Archer, Trebuchet, Longbowman, Galley, Skirmisher, Mangonel, CavalryArcher, FireShip, DemoShip, Mangudai — [UnitEntity.cs:274-278](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L274)) ok/mermi fırlatır. Mermi 22 birim/sn homing hareketle hedefe gider, varınca `TakeDamage` uygular ([Projectile.cs:46-98](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L46)). `SplashRadius > 0` ise (Mangonel 1.8, DemoShip 2.5 — [UnitEntity.cs:249-254](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L249)) varış noktasında **aynı takımdaki diğer birimlere** alan hasarı verir ([Projectile.cs:70-91](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L70)).

### Aggro & stance
Boştaki birim `AggroRadius > 0` ve stance `NoAttack` değilse en yakın düşmanı seçer ([CombatSystem.cs:47-49](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L47)). `StandGround` stance'ında birim hedefi kovalamaz, menzil dışına çıkarsa bırakır ([CombatSystem.cs:99-105](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L99)).

---

## 4. Gerçek statlar (koddan)

### Temel muharebe statları

| Birim | Hasar | Menzil | Aralık (s) | Aggro | Hasar tipi | meleeArmor | pierceArmor |
|---|---|---|---|---|---|---|---|
| Militia | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L138) | [1.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L172) | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L187) | Melee | 0 | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L74) |
| Archer | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122) | [6.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L138) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L172) | [9](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L187) | Pierce | 0 | 0 |
| Cavalry | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L122) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L138) | [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L172) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L187) | Melee | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L127) | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L128) |
| Spearman | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L123) | [1.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L139) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L173) | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L188) | Melee | 0 | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L234) |
| Skirmisher | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124) | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L176) | [9](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L189) | Pierce | 0 | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L261) |
| Camel | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140) | [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L176) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L189) | Melee | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L293) | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L294) |
| Trebuchet | [35](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L123) | [15](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L139) | [5.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L173) | [15](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L188) | Siege | 0 | 0 |
| Mangonel | [25](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) | [9](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L141) | [4.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L177) | [11](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L190) | Siege (splash 1.8) | 0 | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L353) |
| Ram | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L141) | [3.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L177) | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L190) | Siege | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L323) | [180](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L324) |
| Longbowman | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L123) | [8.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L139) | [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L173) | [11](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L188) | Pierce | 0 | 0 |
| CavalryArcher | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L125) | [4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L141) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L177) | [10](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L190) | Pierce | 0 | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L385) |
| Galley | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L124) | [5.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L140) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L176) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L189) | Pierce | 0 | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L619) |
| TeutonicKnight | [12](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L128) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L143) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L179) | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L192) | Melee | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L407) | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L408) |
| WarElephant | [20](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L128) | [1.4](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L143) | [2.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L179) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L192) | Melee | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L432) | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L433) |
| Mangudai | [6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L128) | [5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L143) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L179) | [10](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L192) | Pierce | 0 | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L457) |
| Samurai | [9](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129) | [1.2](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L144) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L180) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L193) | Melee | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L476) | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L477) |
| Eagle | [7](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129) | [1.3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L144) | [1.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L180) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L193) | Melee | 0 | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L497) |
| FireShip | [6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L126) | [3](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L142) | [0.8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L178) | [8](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L191) | Pierce | 0 | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L673) |
| DemoShip | [40](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L126) | [1.5](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L142) | [2.0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L178) | [6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L191) | Siege (splash 2.5) | 0 | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L698) |
| King | [6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L130) | varsayılan [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L145) | varsayılan [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L181) | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L195) | Melee | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L521) | [1](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L522) |
| Scout | varsayılan [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) | varsayılan [1.1](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L145) | varsayılan [1.6](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L181) | LightCavalry'ye kadar [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L194) | Melee | 0 | [2](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs#L186) |
| Medic / Scout / Villager (savaşmaz) | [0/2](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L133) | varsayılan 1.1 | varsayılan 1.6 | [0](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L195) | Melee | — | — |

> EliteEagle hasar 9 / aralık 1.4 ([UnitEntity.cs:129](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L129)); diğer atanmamış birimler base default 2 hasar / 1.1 menzil / 1.6 aralık alır.

### Counter çarpanları ve özel mekanikler

| Mekanik | Değer | Koşul | Kaynak |
|---|---|---|---|
| Cavalry charge | ×2.5 | İlk melee vuruş, `chargeTimer ≥ 4s` | [UnitEntity.cs:256](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L256) |
| Charge timer eşiği | 4 s | `ChargeReady` | [UnitEntity.cs:44-45](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L44) |
| Flanking (arkadan) | ×1.25 | `dot(forward, toAttacker) > 0.5`, sadece melee | [CombatSystem.cs:147](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L147) |
| Veterancy (Veteran) | +%10 attack, +10 maxHP | 1 kill | [UnitEntity.cs:61](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L61), [578](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L578) |
| Veterancy (Elite) | +%20 attack, +20 maxHP | 3 kill | [UnitEntity.cs:578](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L578) |
| Minimum hasar | 1 | Her zaman (zırh sonrası taban) | [UnitEntity.cs:525](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L525) |
| Splash (Mangonel/DemoShip) | 1.8 / 2.5 yarıçap | Ranged, varış noktası | [UnitEntity.cs:249-254](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L249) |

### Destek mekanikleri

| Mekanik | Değer | Kaynak |
|---|---|---|
| Monk dönüştürme süresi | 3–7 s rastgele (Theocracy ×0.6) | [UnitEntity.cs:69-71](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L69), [CombatSystem.cs:280-282](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L280) |
| Monk dönüştürme menzili | 2.5 (BlockPrinting +1.5; 2× dışında iptal) | [TechState.cs:126](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L126) |
| Monk faith yenileme | 12.5/s (~8 s tam dolum) | [UnitEntity.cs:75-76](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L75) |
| Medic iyileştirme yarıçapı | 6 | [UnitEntity.cs:282](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L282) |
| Medic iyileştirme gücü | 3 hp/s (Bizans ×1.5) | [UnitEntity.cs:284](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L284), [CombatSystem.cs:225](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L225) |
| Mermi hızı | 22 birim/sn | [Projectile.cs:16](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L16) |
| Mermi ömrü (safety despawn) | 3 s | [Projectile.cs:18](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L18) |

---

## 5. Counter cetveli — kim kime karşı güçlü

Etkili hasar = `(base + bonus) − hedef zırhı`, min 1. Örnekler base-stat (tech/civ/veterancy yok) içindir.

| Saldırgan | Hedef | Mekanizma | Efektif hasar (örnek) |
|---|---|---|---|
| **Spearman** | Cavalry / Camel / WarElephant / CavalryArcher / Mangudai | `+8` vs `Cavalry` sınıfı | 4 + 8 = 12; Cavalry zırhı 2 melee → **10/vuruş** |
| **Camel** | Cavalry sınıfı | `+7` vs `Cavalry` | 7 + 7 = 14; − 2 melee → **12/vuruş** |
| **Skirmisher** | Archer / Longbowman / Skirmisher | `+3` vs `Archer` (pierce) | 3 + 3 = 6; okçu pierce zırhı 0 → **6/vuruş**, kendi 3 pierce zırhıyla okçu ateşine dayanır |
| **Trebuchet** | Bina | `+70` vs `Building` + Siege zırh-bypass | 35 + 70 = **105/atış**, 15 menzilden |
| **Ram** | Bina | `+16` vs `Building` + Siege bypass; ayrıca 180 pierce zırhı → oklar min-1 | 4 + 16 = **20/vuruş**, okçuya neredeyse dokunulmaz |
| **WarElephant** | Bina (+ ezici melee) | `+30` vs `Building` + Siege değil (Melee) | 20 + 30 = 50; bina zırhına göre düşülür |
| **Mangonel** | Birim kümesi | Siege bypass + 1.8 splash, çok hedef | 25/vuruş × küme; kalabalık okçu/piyade biçer |
| **Mangudai** | Kuşatma (Siege sınıfı) | `+10` vs `Siege` (pierce) | 6 + 10 = 16; Trebuchet/Mangonel/Ram avcısı |
| **Cavalry (charge)** | Okçu / yumuşak hedef | ×2.5 ilk vuruş (4 s sonrası) | 8 × 2.5 = 20 ilk dalış; hit-and-run ile yenilenir |
| **Okçu/Longbow** | Düşük pierce-zırhlı melee | Pierce hasar, menzil avantajı | yüksek pierceArmor'lu hedefe (TeutonicKnight 2, Skirmisher 3) zayıf |

---

## 6. Strateji & counter

- **Süvariye karşı Spearman/Camel:** Flat +8 (Spearman) / +7 (Camel), bu birimlerin düşük base hasarını etkili counter'a çevirir; tek bonus tüm Cavalry-sınıfı türevlerini (Camel, CavalryArcher, Mangudai, WarElephant dahil) sayar.
- **Okçuya karşı Skirmisher:** +3 pierce bonusa ek olarak kendi 3 pierce zırhı sayesinde düşman okçu ateşinden neredeyse hasar almaz (oklar min-1'e düşer); klasik okçu-karşıtı.
- **Binaya karşı Trebuchet/Ram/WarElephant:** Trebuchet 105, Ram 20 (+ okçuya pierce-bağışık), WarElephant 50. Trebuchet/Ram'in Siege hasar tipi bina zırhını da bypass eder.
- **Kümeye karşı Mangonel:** Splash 1.8 yarıçapta tek atışla birden çok hedef; toplu okçu/piyade hattını eritir, ama yapışık hedefe `MinAttackRange 2` içinde ateş edemez.
- **Kuşatmaya karşı Mangudai:** +10 vs Siege; düşman Trebuchet/Mangonel/Ram'ini menzilden temizler.
- **Charge ile hit-and-run:** 4 s temas dışı kalıp dalan Cavalry ilk vuruşta ×2.5; sürekli temasta yenilenmez, geri çekilip yeniden dalmak gerekir.
- **Flanking:** Melee birimi hedefin arkasından sararsa +%25; kuşatma ve cephe kontrolü değerli (ranged'da yok).
- **Zırh yatırımı:** Blacksmith zırh hatları flat çıkarma olduğu için düşük-hasarlı counter'ları (Spearman'ın 4'ü gibi) min-1'e düşürebilir — zırhlı süvari Spearman'a karşı bile dayanır.

---

## 7. Çapraz bağlantılar

- [02-units.md](./02-units.md) — Birim tipleri, base statlar
- [03-unit-upgrades.md](./03-unit-upgrades.md) — Tier yükseltmeleri (zırh artışları dahil), veterancy
- [04-buildings.md](./04-buildings.md) — Bina HP, garnizon, `Building` armor-class hedefleri
- [05-tech-tree.md](./05-tech-tree.md) — Blacksmith zırh/saldırı, `AttackBonus`/`ArmorBonus` teknolojileri
- [06-civilizations.md](./06-civilizations.md) — `infantryAttackMult`, `archerAttackMult`, `archerRangeBonus`, Ironclad
- [11-controls-ui-feedback.md](./11-controls-ui-feedback.md) — HP barları, DamagePopup, hit-flash, ok/kılıç sesleri

---

## 8. Kod referansları (file:line, türetme)

- **Muharebe döngüsü:** [CombatSystem.cs:23-51](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L23) — `Tick` cooldown, charge timer, hedef dallanması.
- **Vuruş & hasar (yeni model):** [CombatSystem.cs:116-161](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L116) — `dmg = AttackDamage + BonusDamageVs`, sonra ranged/melee, charge, flanking.
- **Armor-class enum:** [GameTypes.cs:34-45](../../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L34).
- **Birim → sınıf eşlemesi:** [UnitEntity.cs:200-214](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L200); bina [BuildingEntity.cs:65](../../AgeOfArenaUnity/Assets/Scripts/BuildingEntity.cs#L65).
- **Bonus hasar (BNUS):** [UnitEntity.cs:223-239](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L223) — Spearman/Camel/Skirmisher/Trebuchet/Ram/WarElephant/Mangudai.
- **Charge çarpan:** [CombatSystem.cs:139-140](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L139) + [UnitEntity.cs:256](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L256).
- **Flanking:** [CombatSystem.cs:141-147](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L141) — sadece melee, dot > 0.5.
- **AttackDamage türetme:** [UnitEntity.cs:148-161](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L148) — base + tech + civ + veteran.
- **Zırh & min hasar (Siege bypass):** [UnitEntity.cs:513-528](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L513).
- **Tech armor bonus:** [TechState.cs:140-183](../../AgeOfArenaUnity/Assets/Scripts/TechState.cs#L140) — Blacksmith hatları + tier promosyon armor'u.
- **Base armor atamaları:** [UnitFactory.cs](../../AgeOfArenaUnity/Assets/Scripts/UnitFactory.cs) (§4 tablosu).
- **Veterancy:** [UnitEntity.cs:575-584](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L575).
- **Mermi & splash:** [Projectile.cs:29-98](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L29).
- **DamageKind / IsRanged / SplashRadius:** [UnitEntity.cs:249-278](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L249).
- **Hedef arayüzü:** [IDamageable.cs:9-23](../../AgeOfArenaUnity/Assets/Scripts/IDamageable.cs#L9).

---

## 9. AoE2 farkı (reference köprü)

| Konu | AoE2 | AoA |
|---|---|---|
| Counter modeli | Armor class'a göre **düz bonus damage** (örn. Halberdier +32 vs Cavalry) | **M7'den beri aynı yaklaşım:** armor-class'a göre düz bonus (Spearman +8, Camel +7, Trebuchet +70 vs sınıf). Eski çarpansal model kaldırıldı. |
| Armor sınıfları | Çok sayıda (infantry, cavalry, archer, ram, building, spearman vb.) | 7 sınıf: Infantry/Cavalry/Archer/Siege/Building/Ship/Camel |
| Spearman hattı | Spearman/Pikeman/Halberdier 3 kademe, artan bonus | Tek `BonusDamageVs` (+8), tier yükseltmeleri attack/armor artırır ama bonus sabit |
| Skirmisher → Archer | Var (+pierce zırh, +bonus) | **Var** (+3 vs Archer, 3 pierce zırh) |
| Camel → Cavalry | Var | **Var** (+7 vs Cavalry) |
| Ram (pierce immune) | Var (zırh 180) | **Var** (pierceArmor 180, +16 vs Building, Siege bypass) |
| Mangonel splash | Var | **Var** (1.8 yarıçap, dost-ateşi yok — sadece hedef takımı) |
| Zırh tipleri | Melee + Pierce, sınıf-bazlı bonus armor | Melee + Pierce alanları; **Siege hasarı zırhı bypass eder** |
| Minimum hasar | `max(1, atk + bonus − armor)` | Aynı kural ([UnitEntity.cs:525](../../AgeOfArenaUnity/Assets/Scripts/UnitEntity.cs#L525)) |
| Flanking bonusu | **Yok** | **Var** (×1.25 melee) — AoA özgün |
| Charge bonusu | Cataphract/knight charge yoktur (genel) | **Var** (Cavalry ×2.5 ilk vuruş) — AoA özgün |

---

## 10. Eksikler / Yapılacaklar

| ID-aday | Sınıf | Eksik | Efor |
|---|---|---|---|
| PIKE | Yükseltme | Spearman → Pikeman → Halberdier'de `BonusDamageVs` sabit (+8); AoE2'deki kademeli artan bonus (+15/+22/+32) yok | Düşük |
| BLDARM | Veri | Bina base armor (BuildingEntity) merkezi tabloda yok; §4'te yalnızca birimler listeli | Düşük |
| RAOE | Mekanik | Mangonel splash dost-ateşi (kendi takımına hasar) yok — AoE2'de friendly fire vardır | Orta |
| MTECH | Tech | Monk destek teknolojileri kısmen var (Theocracy/BlockPrinting/Redemption); Heresy/Atonement yok | Düşük |
</content>
