# AoE2 Ekonomi & Ticaret Sistemi — Tam Referans

## Dört Kaynak

| Kaynak | Toplama Yöntemi | Depo Noktası |
|---|---|---|
| **Yiyecek (Food)** | Avlanma, meyve toplama, çiftçilik, balıkçılık, hayvancılık | Town Center, Mill |
| **Odun (Wood)** | Ağaç kesme | Lumber Camp |
| **Altın (Gold)** | Altın madeni, ticaret, Relik, haraç | Mining Camp |
| **Taş (Stone)** | Taş madeni | Mining Camp |

### Standart Başlangıç Kaynakları (Random Map)
- Food: 200, Wood: 200, Gold: 100, Stone: 200
- 3 Villager + Scout

---

## Villager Toplama Verimliliği

| Kaynak | Temel (yiyecek/dk) | Wheelbarrow sonrası | Hand Cart sonrası |
|---|---|---|---|
| Çiftlik | 31.5 | 40 | 50 |
| Meyve (Berries) | 24.8 | 31.5 | 39 |
| Avlanma (Deer/Boar) | 33 | 42 | 52 |
| Balıkçılık | 35 | 44 | 55 |
| Odun | 25 | 32 | 39 |
| Altın madeni | 27 | 34 | 42 |
| Taş madeni | 27 | 34 | 42 |

**Yürüme süresi:** Villager kaynak → depo yürüme süresi verimliliği düşürür. Depoyu kaynağa yakın koy.

---

## Çiftlik Yönetimi

### Temel Kurallar
- Farm başlangıç kapasitesi: **250 yiyecek**
- Tükenince tarlayı yenileme gerekir (60 odun)
- Mümkünse **Town Center / Mill'e bitişik** kur (yürüme süresi = 0)
- 6+ çiftlik için "auto-requeue" tech'i araştır (Feudal Age yeterli)

### Çiftlik Teknoloji Zinciri
| Tech | Etki | Kümülatif |
|---|---|---|
| Horse Collar (Feudal) | +15% | 287 yiyecek/farm |
| Heavy Plow (Castle) | +15% daha | 330 yiyecek/farm |
| Crop Rotation (Imperial) | +15% daha | 380 yiyecek/farm |

### Optimal Çiftlik Yerleşimi
```
[Mill][Farm][Farm][Farm]
[Farm][Farm][TC  ][Farm]
[Farm][Farm][Farm][Farm]
```
TC'ye / Mill'e bitişik çiftlikler drop-off süresi sıfır → maksimum verimlilik.

---

## Market Sistemi

### Kaynak Takası
- Her kaynak türü için takas oranı **dinamik olarak değişir**.
- Başlangıç kuru: ~100 kaynak = 100 altın (yaklaşık)
- Her işlemde fiyat oyuncular aleyhine değişir (arz/talep modeli)
- **Coin Return Formula**: `gold = 0.92 × (resource_sold / price_per_100)`

### Fiyat Dalgalanma Kuralı
- Sattıkça o kaynağın altın karşılığı düşer
- Alındıkça o kaynağın altın maliyeti artar
- Fiyatlar yavaşça "market neutral"a döner (recovery mekanizması)

### Trade Cart
| Özellik | Değer |
|---|---|
| Maliyet | 100O 50A |
| HP | 70 |
| Hareket hızı | 1.0 (Caravan tech ile +50%) |
| Üretim binası | Market |
| Minimum uzaklık | 5 tile |

**Ticaret Mekaniği:**
- Trade Cart, kendi Market'inden ÇIKAR, müttefik/nötr Market'e GİDER, altın getirir
- Kazanılan altın = `distance × 0.46 × karavan sayısı orantısı`
- Uzak market = daha çok altın (quadratic artış değil, linear ama önemli fark)
- Yol güvenliği kritik: uzun yol → daha fazla risk

**Trade Cog (Denizde):**
- Dock'tan üretilir
- Kendi Dock → rakip/müttefik Dock arası
- Aynı mesafe formülü geçerli
- Su haritalarında karada Trade Cart gibi işlev görür

---

## Relik Sistemi

| Özellik | Değer |
|---|---|
| Relik sayısı (haritada) | 5-9 (haritaya göre değişir) |
| Altın üretimi | 30 altın/dk/relik |
| Taşıma birimi | Monk |
| Depo binası | Monastery |
| Relic win condition | Tüm relikleri belirli süre tut → zafer |

**Relic Toplama Prosedürü:**
1. Monk'u relike gönder
2. Monk "pikap" yapar (birim gibi taşır)
3. Kendi Monastery'e geri dön
4. Relik Monastery içinde üretmeye başlar

**Relic savaşı:**
- Rakip relik taşıyan Monk'u öldürürsen relik düşer
- Kendi Monk'unla yerden alabilirsin

---

## Nüfus Sistemi

| Bina | Nüfus Kapasitesi |
|---|---|
| House | +5 |
| Town Center | +5 |
| Castle | +20 |

- Varsayılan nüfus limiti: **200** (Definitive Edition)
- Oyun başlangıcında: kapasiteli TC → 5 nüfus (3 köylü + 2 yer = oyuncuya göre değişir)
- Pop cap aşılırsa üretim durur, mevcut birimler kalır

---

## Kaynak Haritası (Standart Random Map)

| Kaynak | Konum | Miktar |
|---|---|---|
| Gold mines | Ev çevresinde 2-3 küme | ~800 altın/maden |
| Stone mines | Ev çevresinde 1-2 küme | ~350 taş/maden |
| Ormanlık | Harita kenarı/orta | Tükenmez |
| Geyik (Deer) | Hızlı başlangıç yiyeceği | 2-4 hayvan yakında |
| Yaban domuzu (Boar) | Tehlikeli ama verimli | 1-2 adet yakında |
| Meyveler | Güvenli ama az | 125 yiyecek/çalı |

---

## AoA Ekonomi Karşılaştırması

| Mekanik | AoE2 | AoA |
|---|---|---|
| Çiftlik kapasitesi | 250-380 (tech ile) | Renewable (60 odun ile yenileme) |
| Market fiyatı | Dinamik dalgalı | ✅ Dalgalı (O22 MKT) |
| Trade Cart | Yuvarlak sefer, uzaklık bazlı | ✅ TradingSystem (O22 TRD) |
| Relik altın | 30 altın/dk/relik | ✅ 0.5 altın/s (RelicSystem) |
| Haraç (tribute) | Altın transfer, %30 vergi | ✅ (O22 TRB) |
| Kaynak çeşidi | Geyik, domuzu, meyve, balık | ✅ BerryBush + FishPond (O22 RES) |
