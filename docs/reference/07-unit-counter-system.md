# AoE2 Birim Counter Sistemi — Tam Referans

> "Rock-Paper-Scissors" mekanizması: her birim türünün güçlü/zayıf olduğu tipler.
> Bonus damage = standart damage'ın üstüne eklenen ek hasar (zırhı yok sayar).

## Temel Üçgen

```
Piyade (Infantry)
    ↑ güçlü     ↓ zayıf
Süvari (Cavalry) ←→ Okçu (Archer)
    Spearman        Skirmisher
```

**Kısa Kural:**
- **Spearman** → Cavalry'yi döver (+15-32 bonus damage)
- **Skirmisher** → Archer'ı döver (+3-4 bonus damage + pierce zırhı)
- **Knight/Cavalry** → Archer'ı döver (hızlı yaklaşır, yüksek HP)
- **Archer** → Piyadeyi döver (uzaktan, güvenli)
- **Piyade (Champion)** → Her şeye karşı genel (yavaş ama ucuz)

---

## Tam Counter Matrisi

### Piyade Karşıtları

| Saldıran | Hedef | Bonus Damage | Neden Etkili |
|---|---|---|---|
| Spearman | Cavalry (tümü) | +15 | Düşük maliyet, cavalry killer |
| Pikeman | Cavalry (tümü) | +22 | Upgrade Spearman |
| Halberdier | Cavalry (tümü) | +32 | En güçlü cavalry karşıtı |
| Halberdier | Camel Rider | +26 | Camel'e de bonus |
| Halberdier | War Elephant | +28 | Fil'e büyük bonus |
| Jaguar Warrior (Aztec) | Infantry | +10 | Piyade'yi döver (unique) |

### Okçu Karşıtları

| Saldıran | Hedef | Bonus Damage | Neden Etkili |
|---|---|---|---|
| Skirmisher | Archer | +3 pierce bonus | + yüksek pierce zırh (3/4) |
| Elite Skirmisher | Archer | +4 pierce bonus | — |
| Skirmisher | Cavalry Archer | +4 | — |
| Onager/Mangonel | Archer kümesi | alan hasarı | Archer'lar kalabalık = mükemmel hedef |
| Knight/Cavalry | Archer | — | Hızlı yaklaşır, archer ölür |

### Cavalry (Süvari) Karşıtları

| Saldıran | Hedef | Bonus Damage | Neden Etkili |
|---|---|---|---|
| Spearman | Cavalry | +15 | — |
| Pikeman | Cavalry | +22 | — |
| Halberdier | Cavalry | +32 | — |
| Camel Rider | Cavalry | +9 | Camel'in özel mekanizması |
| Heavy Camel | Cavalry | +18 | — |
| Mameluke (Saracen) | Cavalry | özel | Camel hattı + farklı zırh |
| Monk | Cavalry | dönüştürme | Cavalry menzilden çarpamazsa risk |

### Bina Karşıtları

| Saldıran | Hedef | Bonus | Neden Etkili |
|---|---|---|---|
| Trebuchet | Bina | 200 hasar | Uzun menzil, çok hasar |
| Battering Ram | Bina | 200+ | Pierce zırh immune (ok etkilemez) |
| Capped Ram | Bina | 250+ | — |
| Siege Ram | Bina | 300+ | — |
| Petard | Kapı/Duvar | 500 | Self-destruct; tek seferlik |
| Bombard Cannon | Bina | yüksek | Barut; uzun menzil |
| Tarkan (Hun) | Bina | +12 | Unique; binaya bonus |
| Woad Raider (Celt) | Bina | +2 | Küçük bonus |
| Throwing Axeman (Frank) | Bina | +4 | — |

### Monk Karşıtları (Conversion'ı engellemek)

| Yöntem | Açıklama |
|---|---|
| Hız | Monk yavaş; atlı/binici kaçar |
| Heresy tech | Dönüştürülen kendi birimin ölmesi |
| Okçu | Monk düşük HP → uzaktan öldür |
| Atonement tech | Kendi Monk'un karşı Monk'u dönüştürür |
| Theocracy tech | Dönüştürme sonrası tek Monk recharge |

### Kuşatma Silahı Karşıtları

| Saldıran | Hedef | Neden Etkili |
|---|---|---|
| Knight/Cavalry | Trebuçet/Mangonel | Hızlı ulaşır; kuşatma zayıf HP |
| Infantry rush | Siege | Siege'in minimum menzili var; yakına geliremez |
| Skirmisher | Scorpion | Pierce resist yüksek |
| Light Cavalry | Trade Cart/Monk | Hızlı, yumuşak hedef avcısı |

---

## Zırh Tipleri (Damage Classes)

AoE2'de her birimde iki zırh değeri vardır:
- **Melee Armor**: Yakın dövüş hasarını azaltır
- **Pierce Armor**: Uzaktan (ok/fırlatıcı) hasarını azaltır

| Birim | Melee Zırh | Pierce Zırh |
|---|---|---|
| Militia | 0 | 1 |
| Champion | 1 | 1 |
| Knight | 2 | 3 |
| Paladin | 2 | 4 |
| Archer | 0 | 0 |
| Skirmisher | 0 | 3-4 |
| Trebuchet | 2 | 8 |
| Battering Ram | 180 | 180 |
| Stone Wall | 0 | 7 |

**Minimum hasar kuralı:** Saldırı − zırh < 1 ise hasar yine de **1** olur (hiçbir şey 0 hasar vermez).

---

## AoE2 Damage Formula

```
net_damage = max(1, attack + bonus_damage - armor)
```

- `attack`: birim temel saldırı
- `bonus_damage`: hedefe karşı özel bonus (örn. Spearman'ın cavalry bonusu)
- `armor`: hedefin ilgili zırh türü (melee veya pierce)

---

## Rock-Paper-Scissors Genişletilmiş Şema

```
Piyade (Militia/Champion)
    güçlü → Spearman'a bağlı
    zayıf ← Okçu

Spearman/Halberdier ──────────── Cavalry (Knight/Cavalier/Paladin)
    güçlü → Cavalry                güçlü → Archer
    zayıf ← Archer                 zayıf ← Spearman, Camel

Archer/Skirmisher ────────────── Camel Rider
    güçlü → Piyade (uzaktan)       güçlü → Cavalry
    zayıf ← Cavalry, Skirmisher    zayıf ← Spearman, Archer

Kuşatma Silahları (Mangonel/Scorpion/Trebuchet/Ram)
    güçlü → Archer kalabalığı, Binalar
    zayıf ← Cavalry, yakın gelen Infantry

Monk
    güçlü → herhangi (conversion)
    zayıf ← Hussar/hafif süvari, okçu
```

---

## AoA Counter Sistemi Durumu

| Counter | AoE2 | AoA |
|---|---|---|
| Spearman → Cavalry | ✅ Aktif (ARM O16; +32 bonus) | ✅ |
| Skirmisher → Archer | ⬜ Eksik (Skirmisher yok) | Kısmen |
| Cavalry → Archer | ✅ Charge bonus (2.5×) | ✅ |
| Trebuchet → Bina | ✅ 3× anti-structure | ✅ |
| Monk → Dönüştürme | ✅ 4s conversion (O21) | ✅ |
| Ram → Bina (pierce immune) | ⬜ Eksik | — |
| Camel → Cavalry | ⬜ Eksik | — |
| Zırh tipleri (melee/pierce) | ✅ DamageType enum (ARM O16) | ✅ |
