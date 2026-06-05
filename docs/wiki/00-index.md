# AoA Wiki — İndeks

> Age of Arena'nın **şu an gerçekte ne yaptığını** belgeleyen kullanıcı/geliştirici
> wiki'sinin giriş kapısı. Sayfa haritası, katman ayrımı, okuma sırası ve bakım
> sözleşmesi burada. Tek doğruluk kaynağı **kod**: her stat, formül ve mekanik
> doğrudan `AgeOfArenaUnity/Assets/Scripts/` içindeki `dosya:satır` referansına bağlıdır.

---

## (a) Wiki nedir?

Bu wiki, **AoA'nın bugünkü davranışını** tarif eder — gelecekteki planları, AoE2 ideal
hâlini ya da "olması gerekeni" değil. Her sayfa şu prensiplere uyar:

- **Tek kaynak = kod.** Yazılan her sayı (HP, hasar, maliyet, menzil, hız, cooldown,
  çarpan, çağ eşiği vb.) doğrudan koddan türetilir ve clickable bir
  [dosya:satır](../../AgeOfArenaUnity/Assets/Scripts/) bağlantısı taşır. Sabit kodlanmış
  bir değer için kaynak gösterilmiyorsa o satır eksiktir.
- **Tarif eder, önermez.** Wiki neyin **çalıştığını** anlatır. Eksikler ve "şöyle olmalı"
  fikirleri her sayfanın **§8 (eksikler / backlog)** bölümünde toplanır ve oradan
  [99-backlog.md](99-backlog.md) içinde birleştirilir.
- **Sayfa yapısı tutarlıdır.** Her konu sayfası kabaca: §1 ne olduğu → §2 nasıl çalışır
  (mekanik + formül) → §3 gerçek statlar (tablo) → … → strateji/counter → §8 eksikler.
- **Stat değişince wiki bayatlar.** Koddaki bir sayı değişirse, ilgili wiki tablosu da
  güncellenmelidir (bkz. **(e) Bakım sözleşmesi**).

Kısaca: bir oyuncu "Spearman süvariye karşı ne kadar bonus alıyor?" diye sorduğunda,
cevap wiki'de bir tablo satırıdır ve o satır `UnitEntity.cs#L…` bağlantısıyla
doğrulanabilir.

---

## (b) Dört katman ayrımı

Bu repo'da oyunla ilgili bilgi **dört farklı katmana** ayrılmıştır. Karıştırılmaması
kritik: her birinin sorusu, doğruluk kaynağı ve değişme ritmi farklıdır.

| Katman | Konum | Yanıtladığı soru | Doğruluk kaynağı | Üslup |
|---|---|---|---|---|
| **Wiki** | `docs/wiki/` (bu klasör) | "AoA **şu an** gerçekte ne yapıyor?" | **Kod** (`dosya:satır`) | Tarif eder; öneri sadece §8 |
| **AoE2 Referans** | `docs/reference/` | "**AoE2**'de gerçekte ne var?" | AoE2:DE (2019) gerçek statları | Harici gerçek, kıyas tabanı |
| **Gap & Roadmap** | `docs/01-12.md` | "AoA'da ne **var / eksik**, sırada ne?" | Wiki (var) ↔ Reference (ideal) farkı | Planlar; P0/P1/P2 önceliği |
| **HANDOFF** | `HANDOFF.md` | "Oturumda **ne yapıldı**, mimari nasıl, sırada ne?" | Oturum geçmişi + mimari kararlar | Karar günlüğü; P3 kararları |

### İlişki diyagramı

```
                          ┌──────────────────────────┐
                          │   KOD (Assets/Scripts/)   │
                          │  tek gerçek davranış       │
                          └────────────┬─────────────┘
                                       │ dosya:satır türetir
                                       ▼
   ┌──────────────────┐    fark    ┌──────────────────┐
   │  docs/wiki/      │◄──────────►│ docs/reference/  │
   │  "AoA ne yapıyor"│  analizi   │ "AoE2'de ne var" │
   └────────┬─────────┘            └─────────┬────────┘
            │                                │
            │      her ikisi besler          │
            └───────────────┬────────────────┘
                            ▼
                  ┌──────────────────┐
                  │  docs/01-12.md   │   var ↔ ideal farkı
                  │  Gap & Roadmap   │   → P0/P1/P2 plan
                  └────────┬─────────┘
                           │ planlanan iş kararı
                           ▼
                  ┌──────────────────┐
                  │   HANDOFF.md     │   yapıldı + mimari + P3 karar
                  └──────────────────┘
```

Akış: **Kod** gerçeği üretir → **Wiki** onu belgeler, **Reference** AoE2 idealini tutar
→ ikisinin farkı **Gap/Roadmap** planını doğurur → planın hayata geçişi ve kararları
**HANDOFF**'a düşer → kod tekrar değişir, döngü başa döner.

---

## (c) Sayfa haritası

| # | Sayfa | Başlık | Kapsam |
|---|---|---|---|
| 00 | [00-index.md](00-index.md) | İndeks | Bu sayfa: wiki nedir, katmanlar, harita, okuma sırası, bakım |
| 01 | [01-game-flow-ages.md](01-game-flow-ages.md) | Oyun Akışı & Çağlar | Runtime sahne kurulumu, oyun döngüsü, 4 çağ ve çağ atlama |
| 02 | [02-units.md](02-units.md) | Birimler | ~24 birim türü (temel + counter + deniz + civ unique): rol, statlar, mekanik |
| 03 | [03-unit-upgrades.md](03-unit-upgrades.md) | Birim Yükseltme Zincirleri | Man-at-Arms / Crossbowman / Paladin vb. upgrade hatları |
| 04 | [04-buildings.md](04-buildings.md) | Binalar & Garnizon | Bina tablosu, maliyet/HP/çağ, drop-off, garnizon mekaniği |
| 05 | [05-tech-tree.md](05-tech-tree.md) | Teknoloji Ağacı | `TechDefs.Table`, araştırma, çağ kilidi, bonus uygulaması |
| 06 | [06-civilizations.md](06-civilizations.md) | Medeniyetler | `Civilization` bonusları, unique unit/tech, takım seçimi |
| 07 | [07-combat-counters.md](07-combat-counters.md) | Savaş & Counter Sistemi | ArmorClass + additive bonus-damage modeli (M7), hasar↔zırh, projectile |
| 08 | [08-economy-trade.md](08-economy-trade.md) | Ekonomi & Ticaret | 4 kaynak, toplama, depo, Market, Trade Cart, Relik |
| 09 | [09-ai-difficulty.md](09-ai-difficulty.md) | AI & Zorluk | AIPersonality, üretim rotasyonu, zorluk parametreleri |
| 10 | [10-victory-objectives.md](10-victory-objectives.md) | Zafer Koşulları | Fetih / Wonder / Relic / Score zafer yolları |
| 11 | [11-controls-ui-feedback.md](11-controls-ui-feedback.md) | Kontroller & UI / Ses / VFX | Girdi, seçim, HUD, minimap, ses, animasyon, VFX |
| 99 | [99-backlog.md](99-backlog.md) | Birleşik Backlog | Tüm sayfaların §8 eksikleri tek tekilleştirilmiş liste |

---

## (d) Okuma sırası

Hedefe göre üç farklı yol önerilir.

### Oyuncu olarak (nasıl oynanır)
1. [01 Oyun Akışı & Çağlar](01-game-flow-ages.md) — oyun nasıl başlar, çağlar nasıl atlanır
2. [08 Ekonomi & Ticaret](08-economy-trade.md) — kaynakları toplamayı öğren
3. [04 Binalar & Garnizon](04-buildings.md) — ne inşa edilir, ne işe yarar
4. [02 Birimler](02-units.md) + [03 Birim Yükseltmeleri](03-unit-upgrades.md) — ordunu kur
5. [07 Savaş & Counter](07-combat-counters.md) — neyle neyi yeneceğini öğren
6. [06 Medeniyetler](06-civilizations.md) + [10 Zafer Koşulları](10-victory-objectives.md) — kazanma planı
7. [11 Kontroller & UI](11-controls-ui-feedback.md) — tuşlar, minimap, geri-bildirim

### Geliştirici olarak (kodu anlamak)
1. [01 Oyun Akışı & Çağlar](01-game-flow-ages.md) — `WorldRoot.Build()` ve sahne kurulumu giriş noktasıdır
2. [05 Teknoloji Ağacı](05-tech-tree.md) + [06 Medeniyetler](06-civilizations.md) — veri tabloları (`TechDefs`, `CivilizationDefs`)
3. [02 Birimler](02-units.md) + [04 Binalar](04-buildings.md) — factory + defs + entity üçlüsü deseni
4. [07 Savaş & Counter](07-combat-counters.md) — `CombatSystem`, `Projectile`, `IDamageable`
5. [08 Ekonomi](08-economy-trade.md) + [09 AI](09-ai-difficulty.md) + [10 Zafer](10-victory-objectives.md) — sistem katmanı
6. [11 Kontroller & UI / Ses / VFX](11-controls-ui-feedback.md) — sunum katmanı
7. [99 Backlog](99-backlog.md) — ne eksik, sırada ne var

### AoE2 ile karşılaştırma yapan olarak
1. İlgili wiki sayfasını oku (AoA'da ne var) → ör. [02 Birimler](02-units.md)
2. Eşleşen reference sayfasını aç (AoE2'de ne var) → ör. [../reference/02-units-upgrade-chains.md](../reference/02-units-upgrade-chains.md)
3. Farkın planını gör → [../PLAN.md](../PLAN.md) (Açık İşler + DoD)
4. Her wiki sayfasının **§8** bölümü AoA-tarafı eksikleri zaten kıyaslar; toplu hâli [99-backlog.md](99-backlog.md)

---

## (e) Bakım sözleşmesi

Bu wiki ancak güncel tutulduğu sürece güvenilirdir. Değişiklik türüne göre kurallar:

- **Bir stat (sayı) değişirse:** Kodda HP/hasar/maliyet/menzil/hız/çarpan/çağ eşiği gibi
  bir değer değiştiğinde, **ilgili wiki tablosu aynı commit'te güncellenir**. Stat
  değişikliği en çok **§3 (gerçek statlar)** ve birim/upgrade ilişkisi nedeniyle
  **02-units.md ↔ 03-unit-upgrades.md** ile, civ bonusu söz konusuysa **06-civilizations.md**
  ile çapraz tutarlı kalmalıdır. Pratik kural: *stat değişti → §3 + §6 (civ) gözden geçir.*
- **Yeni mekanik / sistem eklenirse:** İlgili sayfanın §1–§2'sine eklenir; yeni
  `dosya:satır` bağlantısı verilir.
- **Eksik / iyileştirme önerisi:** Wiki gövdesine **karar gibi yazılmaz**; ilgili sayfanın
  **§8'ine "öneri" olarak** girilir ve [99-backlog.md](99-backlog.md) ile senkron tutulur.
  Hayata geçirme **kararı** wiki'nin değil **[HANDOFF.md](../../HANDOFF.md)** P3 listesinindir.
- **Bağlantılar:** Eklenen her `dosya:satır` linki gerçek koda işaret etmeli. Satır
  numaraları kaydığında ölü link bırakmamak için periyodik kontrol yapılır.

> Özet sözleşme: **Stat değiştir → §3 + §6 güncelle. Öneri → §8 yaz, HANDOFF P3'e taşı.
> Karar wiki'nin değil, HANDOFF'un işidir.**

---

## Ayrıca bakınız

- [HANDOFF.md](../../HANDOFF.md) — oturum geçmişi + mimari + P3 kararları
- [CLAUDE.md](../../CLAUDE.md) — Unity teknik notları (sahne kod ile kurulur, MCP, Input)
- [docs/PLAN.md](../PLAN.md) — plan · backlog · DoD tek kaynağı
- [docs/reference/README.md](../reference/README.md) — AoE2 gerçek statları kaynağı
- [AgeOfArenaUnity/README.md](../../AgeOfArenaUnity/README.md) — Unity proje mimarisi
