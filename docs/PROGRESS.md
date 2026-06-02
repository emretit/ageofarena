# Age of Arena — Roadmap İlerleme Takibi (Handoff)

> Bu dosya **çok-oturumlu yürütmenin canlı handoff'udur.** Roadmap kategorileri
> [docs/00-overview.md](00-overview.md)'de; bu tablo **hangi maddenin hangi durumda** olduğunu izler.

## Kullanım ritüeli (her oturum)

1. **"Sıradaki" işaretçisine** veya master tablodaki en yüksek öncelikli ⬜ maddeye bak.
2. O maddenin **doc dosyasındaki spec'ini** oku (ör. `GAR` → [03-buildings.md](03-buildings.md) §3 [P0]).
3. Kodla → **MCP/Play ile doğrula** (her spec'in "Doğrulama" adımı).
4. **Bu tabloyu güncelle** (Durum + Oturum + Not/commit) ve gerekirse "Aktif adım" / "Sıradaki"ni değiştir.
5. Commit at → **yeni oturuma geç.** Bir oturum = bir madde (mümkünse).

**Durum lejantı:** ⬜ todo · 🟡 devam ediyor · ✅ kod bitti (0 error derleniyor) · ✔️ runtime doğrulandı

---

## ▶ Aktif adım: yok — sıradaki maddeyi seç

`CTRL` (Control group 1-9) **tamamlandı** (O18). Çekirdek ergonomi bloğunun ilk maddesi;
sıradaki ergonomi adayları `IDLE` (idle-worker) ve `MMP` (minimap pan).

### ✔️ `CTRL` — Control group (1-9) (P1) — tamam
Spec: [07-ui-ux-qol.md](07-ui-ux-qol.md) §[P1]. 3 dosya: SelectionSystem (grup ata/seç
+ çift-bas odak), IsometricCameraRig (`FocusOn`), CommandSystem (Ctrl+digit guard).
- **Ctrl+1..9** → seçili birimleri gruba ata (boş seçim no-op, grubu silmez)
- **1..9** → grubu yeniden seç (yalnız bina seçili değilken — rakamlar bina seçiliyken
  araştırma/ticarete ait); ölü/yanlış-takım üyeler otomatik temizlenir, garrisonlu üye
  grupta kalır ama seçilmez
- **Çift-bas (0.4s)** → kamerayı grup merkezine taşı (`IsometricCameraRig.FocusOn`, bounds clamp)
- `CommandSystem` research+market hotkey'leri `CtrlHeld` iken atlanır (çakışma yok)
- **0 error/0 warning**. MCP: `FocusOn` kamerayı taşıdı + out-of-bounds clamp finite;
  ata/seç input-bağlı (klavye) olduğu için manuel Play doğrulaması gerekir.

### ✔️ `IMP` — Imperial (4.) çağ (P1) — tamam
Spec: [05-tech-ages.md](05-tech-ages.md) §3 [P1]. Çağ ilerlemesi Castle → **Imperial**
olarak uzatıldı; mevcut `FeudalAge`/`CastleAge` tech deseni birebir izlendi.
6 dosya: GameTypes (Age + ImperialAge), TechDefs (tablo satırı + IsAvailable),
ResearchSystem (age advance dalı), HUD (AgeName/isAge/tooltip), EnemyAI (Imperial'a
yükselme + case). **0 error/0 warning**. MCP RunCommand ile doğrulandı:
- Castle çağında ImperialAge TC'de araştırılabilir = **True**
- Apply sonrası `tech.age` = **Imperial** (1000Y+600A, 50s)
- Barracks Castle-tech'leri (Forging/ScaleMail/ManAtArms) hâlâ açık → **regresyon yok**
- ImperialAge tek seferlik (tekrar teklif edilmiyor)
- HUD popup "İMPARATORLUK ÇAĞI!" + "Çağ: İmparatorluk" etiketi (generic ShowAgePopup)
- İçerik (Imperial-gated tier birim/tech) ayrı maddeler: `TIER`, `UNI`
- ✅ Commit bekliyor (O17).

### ✔️ `ARM` — Zırh tipleri + counter matrisi (P1) — tamam
Spec: [02-units.md](02-units.md) §3 [P1] + [04-combat.md](04-combat.md) §3 [P1].
11 dosya değişti. **0 error/0 warning**. MCP RunCommand ile 6 kabul kriteri doğrulandı:
- Spearman vs Cavalry: 10/vuruş → 8 vuruşta öldürür (Militia: 25 vuruş — 3.1× fark)
- Archer vs Spearman (pierceArmor=3): min-1/vuruş (pierce zırhı çalışıyor)
- Archer vs Wall (pierceArmor=10): min-1 (zemin koruması)
- Cavalry charge: 20 hasar (2.5× onaylandı)
- Trebuchet vs bina: 105 hasar (3× anti-structure, Siege bypass)
- 0 compile error
- ✅ **Commit edildi** (`8c0333e`, O17 başında — GAR + O16 görsel ile birlikte).

### ✔️ `GAR` — Garnizon (P0) — tamam
Spec: [03-buildings.md](03-buildings.md) §3 [P0]. Tüm dosyalar yazıldı, **0 error/0 warning**,
MCP RunCommand harness'ı ile 7 kabul kriteri doğrulandı. ✅ Commit edildi (`8c0333e`).

## ⏭ Sıradaki (öneri)
`SES` (ses temeli — izole, yüksek etki) veya `MON`+`MONK` (Monastery + Monk) veya
`TIER`/`UNI` (Imperial içeriğini doldurur — IMP artık hazır).

---

## Master tablo

### P0
| ID | Doc | Madde | Durum | Oturum | Not/commit |
|---|---|---|---|---|---|
| GAR | 03 | Garnizon (gir/iyileş/çık + savunma oku) | ✔️ | O15 | 0 err; 7 kriter MCP; commit `8c0333e` |

### P1
| ID | Doc | Madde | Durum | Oturum | Not/commit |
|---|---|---|---|---|---|
| ARM | 02/04 | Zırh tipleri + counter matrisi (spear>cav>archer) | ✔️ | O16 | 0 err; 6 kriter MCP; commit `8c0333e` |
| MONK | 02/03 | Monk (dönüştürme + relic taşıma) | ⬜ | — | Monastery'ye bağlı |
| TOW | 03 | Watch/Bombard Tower | ⬜ | — | |
| REP | 03 | Repair (köylü tamir) | ⬜ | — | |
| BLK | 03/05 | Blacksmith + askeri tech taşıma | ⬜ | — | |
| MON | 03 | Monastery binası | ⬜ | — | |
| STN | 04/07 | Attack stance (aggressive/defensive/stand/no-attack) | ⬜ | — | |
| FORM | 04 | Formasyon kohezyonu | ⬜ | — | |
| IMP | 05 | Imperial (4.) çağ | ✔️ | O17 | 0 err; MCP doğrulandı; içerik TIER/UNI'ye bırakıldı |
| UNI | 05 | University binası + tech | ⬜ | — | |
| TIER | 05 | Imperial tier birimleri | ⬜ | — | |
| DIFF | 06 | AI zorluk seviyeleri (Easy→Insane) | ⬜ | — | |
| AIMS | 06 | AI Medic/Scout kullanımı | ⬜ | — | |
| CTRL | 07 | Control group (1-9) | ✔️ | O18 | 0 err; FocusOn MCP doğrulandı; ata/seç input-bağlı manuel |
| IDLE | 01/07 | Idle-worker butonu + döngü | ⬜ | — | |
| MMP | 07 | Minimap click-to-pan + sağ-tık komut | ⬜ | — | |
| SES | 08 | Ses sistemi temeli (AudioManager) | ⬜ | — | yüksek etki/izole |
| SFX | 08 | Birim/bina/UI SFX seti | ⬜ | — | SES'e bağlı |
| WON | 09 | Wonder zaferi | ⬜ | — | |
| SCR | 09 | Score sistemi | ⬜ | — | |
| RLW | 09 | Relic-sayısı zaferi | ⬜ | — | |
| TRD | 01 | Trade Cart + ticaret rotası | ⬜ | — | Market rolüne bağlı |
| SAVE | 12 | Save / Load | ⬜ | — | |
| MAP | 12 | Prosedürel harita üretimi | ⬜ | — | |

### P2
| ID | Doc | Madde | Durum | Oturum | Not/commit |
|---|---|---|---|---|---|
| ABIL | 02 | Özel yetenek (ability) altyapısı | ⬜ | — | |
| NAV | 02/12 | Naval katmanı (Dock/Galley) | ⬜ | — | su haritasına bağlı |
| VET | 02 | Veterancy / rütbe | ⬜ | — | |
| AURA | 03 | Bina aurası + Palisade/Stone Wall | ⬜ | — | |
| CBX | 04 | Balistik/kuşatma çeşidi/flanking/morale/FF | ⬜ | — | |
| RQ | 05 | Çoklu research queue + tech ağacı paneli | ⬜ | — | |
| AICB | 06 | AI counter farkındalığı + build-order | ⬜ | — | |
| AIGS | 06 | AI garnizon/stance kullanımı | ⬜ | — | GAR/STN'e bağlı |
| QOL | 07 | Çift-tık/hotkey özelleştirme/patrol/oyun hızı | ⬜ | — | |
| ANIM | 08 | Birim animasyonu | ⬜ | — | |
| VFX2 | 08 | Bina hasar görseli + mekansal ses | ⬜ | — | |
| MKT | 01 | Dalgalı market fiyatı | ⬜ | — | |
| RES | 01 | Kaynak çeşidi (berry/deer/fish) | ⬜ | — | |
| TRB | 01/09 | Tribute + çiftlik decay | ⬜ | — | |
| VIC2 | 09 | Diplomasi/resign/conquest/maç ayarları | ⬜ | — | |
| MP1 | 10 | Mimari karar: Lockstep vs Client-Server | ⬜ | — | erken karar |
| MP2 | 10 | Determinizm ön-koşulu | ⬜ | — | MP1'e bağlı |
| MP3 | 10 | Transport + lobby + desync | ⬜ | — | |
| CIV | 11 | Civ tanım veri yapısı | ⬜ | — | |
| UNQ | 11 | Unique unit + unique tech | ⬜ | — | CIV'e bağlı |
| BAL | 11 | Civ seçim UI + balance pass | ⬜ | — | |
| EDIT | 12 | Senaryo/harita editörü + trigger | ⬜ | — | SAVE'e bağlı |
| CMP | 12 | Kampanya + terrain çeşidi + başlangıç ayarları | ⬜ | — | |

---

## Oturum günlüğü

| Oturum | Tarih | Madde | Sonuç |
|---|---|---|---|
| — | 2026-06-02 | docs/ + PROGRESS kurulumu | ✔️ 12 kategori dokümanı + bu tracker oluşturuldu |
| O15 | 2026-06-02 | `GAR` Garnizon | ✔️ 14 dosya; yeni GarrisonSystem.cs; 0 err/0 warn; 7 kabul kriteri MCP harness ile doğrulandı |
| O16 | 2026-06-02 | `ARM` Zırh tipleri + counter matrisi | ✔️ 11 dosya; DamageType enum + Spearman birim; 0 err/0 warn; 6 kabul kriteri MCP harness ile doğrulandı |
