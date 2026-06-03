# AoE2 Zafer Koşulları & Oyun Modları

## Standart Zafer Koşulları

### 1. Askeri Fetih (Conquest)
- **Koşul:** Tüm düşman birim ve binalarını yok et (veya teslim olmaları).
- **Mekanik:** Rakipin son binası yıkılınca elenir. Son rakip elenince kazanırsın.
- **Strateji:** Agresif baskı, TC eliminasyonu odağı.
- AoA'da aktif: TC eliminasyonuyla (MatchSystem).

### 2. Wonder Zaferi
- **Koşul:** Imperial Age'de Wonder inşa et ve belirli süre ayakta tut.
- **Maliyet:** 1000 odun + 1000 taş + 1000 altın (en pahalı bina)
- **Süre:** ~200 yıl ≈ ~10 dakika gerçek süre (oyun hızına bağlı)
- **Savunma:** Wonder büyük HP'ye sahip ama yıkılabilir. Rakipler yarışır.
- **Sayaç:** Ekranda geri sayım görünür; herkes izleyebilir.
- **Strateji:** Late-game boom ile ekonomi kurup sürpriz Wonder inşası.
- AoA'da aktif: WonderSystem (O18); Imperial Age gerektirir; 60s countdown.

### 3. Relik Kontrolü (Relic Victory)
- **Koşul:** Haritadaki TÜM relikleri topla ve belirli süre tut.
- **Süre:** ~200 yıl ≈ ~10 dakika (haritadaki tüm relikler toplandıktan sonra başlar)
- **Mekanik:** Monk ile reliği yakala → Monastery'e götür → süre başlar
- **Rakipler:** Monastery'ini yıkarak reliği düşürebilir (sayaç sıfırlanır)
- **Strateji:** Erken Monk + agresif relic kontrolü; Faith tech önce al.
- AoA'da aktif: RLW (O18); tüm relic 60s tut → zafer (RLicSystem + MatchSystem).

### 4. Score (Puan) Zaferi
- **Koşul:** Oyun süresi dolduğunda (ör. 200 yıl) en yüksek puana sahip olan kazanır.
- **Puan kaynakları:** Birimler (10pt), binalar (20pt), teknolojiler (30pt), çağ (100pt), reliker (20pt).
- **Mekanik:** Süre dolduğunda pano açılır, en yüksek skor kazanır.
- AoA'da aktif: SCR (O18); composite skor.

---

## Özel Oyun Modları

### Random Map (Standart)
- Harita rastgele üretilir (seed'e göre deterministik).
- Oyuncular Dark Age'de 3 Villager + Scout ile başlar.
- Kaynaklar: 200Y/200O/100A/200T.
- En çok oynanan mod; turnuvalar buradan.
- AoA: ✅ O22'de mapSeed eklendi.

### Deathmatch
- Başlangıç kaynakları: 20,000 yiyecek + 20,000 odun + 10,000 altın + 5,000 taş
- Ekonomi değil, saf askeri mücadele odaklı.
- Hızlı Castle/Imperial Age geçişi, büyük ordu savaşları.
- AoA'da: yok (ileride eklenebilir).

### Regicide (Kral Modu)
- Her oyuncuya başlangıçta bir "Kral" (King) birimi verilir.
- Kral ölürse → anında oyundan çıkarsın.
- Zafer: Tüm düşman Kral'larını öldür.
- Kral görece zayıf bir birimdir; Garnizon edilebilir (TC, Castle).
- Wonder/Relic zafer koşulları bu modda geçerli değil.
- AoA'da: yok.

### Nomad (Göçebe)
- Oyuncuların sabit başlangıç noktası yok; haritada dağınık başlanır.
- Town Center yok; Villager'larla ilk TC'yi inşa etmek gerekir.
- İlk TC konumu kritik: kaynak ve savunma hesabı.
- AoA'da: yok.

### Sudden Death
- Town Center'ın yıkılması = anında elenme (birim/bina kalmasa bile).
- Standart Conquest'e benzer ama TC koruması daha kritik.
- AoA'da fiilen bu mod uygulanıyor (MatchSystem TC taraması).

### Turbo Random Map
- Kaynak toplama ve üretim hızları 2-3× artırılır.
- Daha hızlı ilerleme, kısa maçlar.
- AoA'da: oyun hızı mevcuttur (O22 QOL; []/Space ile hız değiştirme).

---

## Diplomasi Sistemi

| Durum | Açıklama |
|---|---|
| **Allied (Müttefik)** | Birbirine saldıramaz; kaynak paylaşımı mümkün |
| **Neutral (Nötr)** | Saldırmaz ama kaynakları savunmaz |
| **Enemy (Düşman)** | Birbirine saldırır |

**Ticaret:** Müttefiklerle, nötrler ve hatta düşmanlarla ticaret yapılabilir (riskli ama mümkün).

**Tribute (Haraç):** Müttefiklerine kaynak gönderebilirsin; %30 transfer vergisi alınır (Banking tech ile %25'e düşer).

**Resign (Teslim):** Oyundan ayrılabilirsin; birimlerin/binaların yıkılır.

AoA'da aktif: VIC2 (O22); Resign + Esc pause menüsü.

---

## Kazanma Koşulları Özet

| Mod | Askeri | Wonder | Relic | Score | Kral |
|---|---|---|---|---|---|
| Random Map | ✅ | ✅ | ✅ | ✅ | — |
| Deathmatch | ✅ | ✅ | ✅ | ✅ | — |
| Regicide | ✅ | — | — | — | ✅ |
| Nomad | ✅ | ✅ | ✅ | ✅ | — |

---

## AoA Zafer Koşulları Durumu

| Koşul | Durum |
|---|---|
| Conquest (TC eliminasyonu) | ✅ Aktif (MatchSystem) |
| Wonder zaferi | ✅ Aktif (O18, 60s countdown) |
| Relic win | ✅ Aktif (O18, tüm relic 60s tut) |
| Score win | ✅ Aktif (O18, sonuç ekranı) |
| Regicide | ⬜ Yok |
| Resign/teslim | ✅ Aktif (O22 VIC2) |
