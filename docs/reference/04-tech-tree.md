# AoE2 Teknoloji Ağacı — Tam Referans

> Tüm standart teknolojiler, hangi binadan araştırıldıkları, çağları, maliyetleri ve etkileri.
> Her medeniyetin kendi unique tech'leri için bkz. [01-civilizations.md](01-civilizations.md).

## Çağ İlerleme Maliyetleri

| Geçiş | Maliyet | Süre | Önkoşul (bina) |
|---|---|---|---|
| Dark → Feudal | 500Y | 130s | 2 Dark Age bina |
| Feudal → Castle | 800Y + 200A | 160s | 2 Feudal Age bina |
| Castle → Imperial | 1000Y + 800A | 190s | 2 Castle Age bina |

(AoA'da farklı: Feudal 400Y/25s, Castle 600Y+200A/35s, Imperial 1000Y+600A/50s)

---

## Ekonomi Teknolojileri

### Town Center'da Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Loom | Dark Age | 50A | Villager +1/+2 zırh, +15 HP |
| Wheelbarrow | Feudal Age | 175Y 50A | Villager +26.95% taşıma, +10% hız |
| Hand Cart | Castle Age | 300Y 200A | Wheelbarrow'u tamamlar; daha hızlı ödenir |
| Town Watch | Feudal Age | 75Y | Tüm binalar +4 görüş |
| Town Patrol | Castle Age | 300Y | Town Watch + 4 görüş daha |

### Mill'de Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Horse Collar | Feudal Age | 75Y | Çiftlik +15% üretim |
| Heavy Plow | Castle Age | 125Y 75A | Çiftlik +15% daha |
| Crop Rotation | Imperial Age | 250Y 250A | Çiftlik +15% daha |

Toplam çiftlik verimliliği: 3 tech ile +45% (sabit oran, tech ile artar)

### Lumber Camp'ta Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Double-Bit Axe | Feudal Age | 100Y | Odun toplama +20% |
| Bow Saw | Castle Age | 150Y 100A | Odun toplama +20% daha |
| Two-Man Saw | Imperial Age | 300Y 200A | Odun toplama +10% daha |

### Mining Camp'ta Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Gold Mining | Feudal Age | 100Y 75A | Altın toplama +15% |
| Gold Shaft Mining | Castle Age | 200Y 100A | Altın toplama +15% daha |
| Stone Mining | Feudal Age | 100Y 75A | Taş toplama +15% |
| Stone Shaft Mining | Castle Age | 200Y 100A | Taş toplama +15% daha |

### Market'te Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Coinage | Castle Age | 150Y | Vergi alma / ceza kaldırır |
| Banking | Imperial Age | 300Y | Maliyet yarıya düşer |
| Caravan | Castle Age | 200Y 200A | Trade Cart hızı +50% |
| Guilds | Imperial Age | 150Y 100A | Trade maliyeti -25% |

---

## Askeri Teknolojiler

### Blacksmith'te Araştırılan

**Piyade Saldırı:**
| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Forging | Feudal Age | 150Y | Piyade +1 saldırı |
| Iron Casting | Castle Age | 220Y 120A | Piyade +1 saldırı |
| Blast Furnace | Imperial Age | 275Y 225A | Piyade +2 saldırı |

**Piyade Zırh:**
| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Scale Mail Armor | Feudal Age | 100Y | Piyade +1/+1 zırh |
| Chain Mail Armor | Castle Age | 200Y 100A | Piyade +1/+1 zırh |
| Plate Mail Armor | Imperial Age | 300Y 150A | Piyade +1/+2 zırh |

**Okçu Saldırı:**
| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Fletching | Feudal Age | 100Y 50A | Okçu +1 saldırı, +1 menzil |
| Bodkin Arrow | Castle Age | 150Y 100A | Okçu +1 saldırı, +1 menzil |
| Bracer | Imperial Age | 200Y 150A | Okçu +1 saldırı, +1 menzil |

**Okçu Zırh:**
| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Padded Archer Armor | Feudal Age | 75Y | Okçu +1/+1 zırh |
| Leather Archer Armor | Castle Age | 150Y 150A | Okçu +1/+1 zırh |
| Ring Archer Armor | Imperial Age | 250Y 250A | Okçu +1/+2 zırh |

**Süvari Saldırı:**
| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Forging | Feudal Age | 150Y | Süvari +1 saldırı |
| Iron Casting | Castle Age | 220Y 120A | Süvari +1 saldırı |
| Blast Furnace | Imperial Age | 275Y 225A | Süvari +2 saldırı |

**Süvari Zırh:**
| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Scale Barding Armor | Feudal Age | 150Y | Süvari +1/+1 zırh |
| Chain Barding Armor | Castle Age | 250Y 150A | Süvari +1/+1 zırh |
| Plate Barding Armor | Imperial Age | 350Y 200A | Süvari +1/+2 zırh |

### Barracks'ta Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Militia-line upgrade | Feudal Age | 100Y 40A | Man-at-Arms |
| Long Swordsman | Castle Age | 150Y 100A | Long Swordsman |
| Two-Handed Swordsman | Imperial Age | 200Y 100A | Two-Handed |
| Champion | Imperial Age | 300Y 150A | Champion (sadece bazı civler) |
| Spearman | Feudal Age | 50Y 35O | Spearman |
| Pikeman | Castle Age | 215Y 90O | Pikeman |
| Halberdier | Imperial Age | 250Y 200O | Halberdier (sadece bazı civler) |
| Squires | Castle Age | 200Y | Piyade hareket +10% |
| Arson | Imperial Age | 150Y 50A | Piyade binalara +2 saldırı |
| Supplies | Feudal Age | 150Y | Militia hattı üretim maliyeti azalır |
| Gambesons | Imperial Age | 100Y 100A | Militia hattı +1 pierce zırh |

### Archery Range'de Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Thumb Ring | Castle Age | 300Y 250A | Okçu +ateş hızı, %100 isabet |
| Parthian Tactics | Imperial Age | 200Y 300A | Cavalry Archer +pierce zırh |
| Crossbowman upgrade | Castle Age | 150Y 100A | — |
| Arbalester upgrade | Imperial Age | 350Y 300A | — |
| Elite Skirmisher | Imperial Age | 250Y 250O | — |
| Heavy Cavalry Archer | Imperial Age | 250Y 250A | — |

### Stable'da Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Light Cavalry | Castle Age | 80Y | Scout Cavalry upgrade |
| Hussar | Imperial Age | 250Y | Light Cavalry upgrade (bazı civler) |
| Cavalier | Imperial Age | 300Y 300A | Knight upgrade |
| Paladin | Imperial Age | 500Y 600A | Cavalier upgrade (bazı civler) |
| Heavy Camel Rider | Imperial Age | 200Y 150A | — |
| Blood Lines | Castle Age | 150Y 100A | Tüm süvari +20 HP |
| Husbandry | Castle Age | 150Y | Süvari hareket +10% |

### University'de Araştırılan

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Masonry | Castle Age | 175Y 75A | Binalar +%10 HP, +%15 küçülme (az yer kaplar) |
| Architecture | Imperial Age | 175Y 150A | Binalar +%25 HP daha |
| Ballistics | Castle Age | 175Y 125A | Kuşatma silahları ve kule/bina okları hareketli hedefleri takip eder |
| Guard Tower | Castle Age | 200O | Watch Tower → Guard Tower |
| Keep | Imperial Age | 500O | Guard Tower → Keep |
| Bombard Tower | Imperial Age | 125O 100T | Bombard Tower açılır |
| Fortified Wall | Imperial Age | 250O 200T | Stone Wall güçlenir |
| Heated Shot | Castle Age | 350Y | TC, kule, Castle ateşi gemi ve kuşatma silahlarına +bonus |
| Murder Holes | Castle Age | 200Y | TC, kule, Castle yakın birime de vurabilir |
| Treadmill Crane | Castle Age | 300Y 200A | Bina inşaat hızı +20% |
| Siege Engineers | Imperial Age | 400Y 200A | Kuşatma menzili +1, binalara +%20 hasar, petard +%40 hasar |
| Chemistry | Imperial Age | 700Y 100A | Okçu/kule +1 saldırı; Bombard/Cannon Galleon açılır |
| Sappers | Imperial Age | 400Y | Piyade binalara +15 hasar |
| Conscription | Imperial Age | 150Y | Askeri üretim %33 hızlanır |
| Redemption | Castle Age | 475Y 450A | Monk, bina + kuşatma makinesini dönüştürebilir |
| Atonement | Castle Age | 175Y 175A | Monk, Monk dönüştürebilir |
| Heresy | Imperial Age | 1000A | Dönüştürülen kendi birimin ölmesi |
| Sanctity | Castle Age | 120Y | Monk +15 HP |
| Illumination | Castle Age | 120Y | Monk yeniden enerji dolma süresi azalır |
| Block Printing | Imperial Age | 75Y | Monk menzili +3 |
| Theocracy | Imperial Age | 200Y | Dönüştürme sonrası sadece bir Monk recharge |

---

## Monastery'de Araştırılan

(Yukarıdaki University'de listelenen Monk tech'lerine ek olarak)

| Tech | Çağ | Maliyet | Etki |
|---|---|---|---|
| Fervor | Castle Age | 140Y | Monk hareket +15% |

---

## AoA'daki Tech Karşılıkları

AoA'da mevcut ekonomi tech'leri:
- Çift Balta = Double-Bit Axe (+%25 odun)
- El Arabası = Wheelbarrow (+%20 tüm toplama)
- Horse Collar: yok (sadece genel çiftlik yenileme var)

AoA'da mevcut askeri tech'ler:
- Dövme = Forging (Militia/Cavalry +2 saldırı) → şimdi Blacksmith'te
- Oklama = Fletching (Archer +1 saldırı/menzil) → Blacksmith'te
- Pul Zırh = Scale Mail (Militia/Cavalry +20 HP) → Blacksmith'te
- ManAtArms/Longswordsman/Crossbowman/Cavalier → Barracks/ArcheryRange/Stable'da
- Champion/Arbalest/Paladin → Imperial tier (TIER O21)
- Masonry/Fortified → University (O21)
