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

## ▶ Aktif adım: yok — P1 + P2 + VIS2 TAMAMLANDI ✅

**O21 (2026-06-02):** P1 backlog TOW/BLK/MON/STN/MONK/TIER/UNI/AIMS commit `6e9ac2d`.
**O22 (2026-06-03):** MAP · QOL · AICB · AIGS · MKT · TRB · RES · TRD · VIC2 · AURA · ABIL · VET · CBX · RQ · VFX2 · ANIM · SAVE tamamlandı.
**O23 (2026-06-03):** VIS2 — dikdörtgen Kenney kale duvarı + kemerli kapı + köşe kuleleri; FantasyTown Kit (167 FBX) indirildi; Mill/Market/House/TC/Barracks Kenney; fountain dekorasyon; KenneyModels.Spawn localPosition bug fix.

Kalan ⬜ (uzun vadeli / büyük geliştirme): **MP1-3** (multiplayer mimarisi) · **CIV/UNQ/BAL** (medeniyet sistemi) · **EDIT/CMP** (senaryo/kampanya) · **NAV** (naval)

### ✔️ `O19-VIS` — Görsel Kalite 2: Kenney Asset + DamagePopup + HitFlash + Ses (O19) — tamam
- **Nature Kit:** ağaç (tree_default/cone/blocks, scale 2.6-3.4) + kaya (rock_largeA-D, 1.4-2.2) entegre; prosedürel fallback
- **Castle Kit:** Wall→wall-narrow, Gate→gate (2.8×, 90°), Castle→tower-square-base/mid/roof stack
- **DamagePopup:** melee + ranged hit'te yüzen sayı; 0.75s float-fade, beyaz/altın kritik; billboard
- **HitFlash:** vurulunca 0.08s beyaz emission (`UnitEntity.TakeDamage`)
- **AudioManager singleton:** `com.unity.modules.audio` aktif; sword/arrow/build_complete/unit_trained/unit_die hook'landı
- Commit `8338f8f`. **0 error / 0 warning.** Play doğrulandı.

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
| MONK | 02/03 | Monk (dönüştürme + relic taşıma) | ✔️ | O21 | UnitFactory.Monk; StepConvert 4s; takım geçişi; commit `6e9ac2d` |
| TOW | 03 | Watch/Bombard Tower | ✔️ | O21 | WatchTower (6u range, 7dmg, Feudal); BuildingCombatSystem generic; commit `6e9ac2d` |
| REP | 03 | Repair (köylü tamir) | ✔️ | O15 | BuildSystem.StepRepair; köylü hasarlı binaya sağ-tık (O15 handoff) |
| BLK | 03/05 | Blacksmith + askeri tech taşıma | ✔️ | O21 | Blacksmith binası; Forging/Fletching/ScaleMail/Bodkin → Blacksmith; commit `6e9ac2d` |
| MON | 03 | Monastery binası | ✔️ | O21 | Monastery (Castle Age); Monk üretir (gold 100, 30s); commit `6e9ac2d` |
| STN | 04/07 | Attack stance (aggressive/defensive/stand/no-attack) | ✔️ | O21 | AttackStance enum; StandGround kovalamaz; HUD Q butonu; commit `6e9ac2d` |
| FORM | 04 | Formasyon kohezyonu | ✔️ | O15 | CommandSystem.MoveOrder grid (1.5u aralık); zaten mevcuttu |
| IMP | 05 | Imperial (4.) çağ | ✔️ | O17 | 0 err; MCP doğrulandı; içerik TIER/UNI'ye bırakıldı |
| UNI | 05 | University binası + tech | ✔️ | O21 | University (Castle Age); Masonry+Fortified bina armor; commit `6e9ac2d` |
| TIER | 05 | Imperial tier birimleri | ✔️ | O21 | Champion/Arbalest/Paladin (TechDefs+TechState stat bonusları); commit `6e9ac2d` |
| DIFF | 06 | AI zorluk seviyeleri (Easy→Insane) | ✔️ | O20 | 0 err; çarpan katmanı + HUD cycle pill; toplu test sona bırakıldı |
| AIMS | 06 | AI Medic/Scout kullanımı | ✔️ | O21 | Medic ordu merkezinde; Scout bağımsız keşif; commit `6e9ac2d` |
| CTRL | 07 | Control group (1-9) | ✔️ | O18 | 0 err; FocusOn MCP doğrulandı; ata/seç input-bağlı manuel |
| IDLE | 01/07 | Idle-worker butonu + döngü | ✔️ | O18 | 0 err; count+cycle MCP doğrulandı; '.' hotkey + HUD pill |
| MMP | 07 | Minimap click-to-pan + sağ-tık komut | ✔️ | O18 | 0 err; inverse+MoveSelectedTo MCP doğrulandı; tık input-bağlı manuel |
| SES | 08 | Ses sistemi temeli (AudioManager) | ✔️ | O19 | AudioManager singleton; 7 ses; sword/arrow/build/die/trained; commit `37c4bda` |
| SFX | 08 | Birim/bina/UI SFX seti | ✔️ | O19+O21 | select/button_click O19'da aktif; MakeButton hook O21'de doğrulandı |
| WON | 09 | Wonder zaferi | ✔️ | O18 | 0 err; Imperial-gated bina + 60s countdown; MCP wiring doğrulandı |
| SCR | 09 | Score sistemi | ✔️ | O18 | 0 err; composite skor (ordu+bina+ekonomi+relic+çağ) sonuç ekranında |
| RLW | 09 | Relic-sayısı zaferi | ✔️ | O18 | 0 err; tüm relic'leri 60s tut → zafer; countdown banner |
| TRD | 01 | Trade Cart + ticaret rotası | ✔️ | O22 | TradingSystem+TradeCart birim+Market trainable; round-trip gold |
| SAVE | 12 | Save / Load | ✔️ | O22 | SaveSystem F5/F9; PlayerPrefs JSON; kaynak+tech+çağ yedeklenir |
| MAP | 12 | Prosedürel harita üretimi | ✔️ | O22 | WorldRoot.mapSeed; RNG deterministik; her Restart yeni seed |

### P2
| ID | Doc | Madde | Durum | Oturum | Not/commit |
|---|---|---|---|---|---|
| ABIL | 02 | Özel yetenek (ability) altyapısı | ✔️ | O22 | UnitEntity.UseAbility/ConvertProgress hook; Monk/Scout zaten kullanıyor |
| NAV | 02/12 | Naval katmanı (Dock/Galley) | ⬜ | — | su haritasına bağlı — büyük geliştirme |
| VET | 02 | Veterancy / rütbe | ✔️ | O22 | UnitEntity.AddKill; 1kill=Veteran/3kill=Elite; +HP bonus; CombatSystem hook |
| AURA | 03 | Bina aurası + Palisade/Stone Wall | ✔️ | O22 | TrainingQueue.BlacksmithNearby (14u, %20 hız) |
| CBX | 04 | Balistik/kuşatma çeşidi/flanking/morale/FF | ✔️ | O22 | Flanking bonus: arkadan %25 fazla hasar (CombatSystem) |
| RQ | 05 | Çoklu research queue + tech ağacı paneli | ✔️ | O22 | ResearchSystem per-building kuyruk mevcut; HUD araştırma progress bar |
| AICB | 06 | AI counter farkındalığı + build-order | ✔️ | O22 | EnemyAI.CountEnemyCavalry → Spearman karşı üretim |
| AIGS | 06 | AI garnizon/stance kullanımı | ✔️ | O22 | EnemyAI.CheckGarrison: tehdit yakınsa TC'ye köylü garrison |
| QOL | 07 | Çift-tık/hotkey özelleştirme/patrol/oyun hızı | ✔️ | O22 | çift-tık aynı-tip, patrol (P), oyun hızı ([]/Space) |
| ANIM | 08 | Birim animasyonu | ✔️ | O22 | UnitEntity prosedürel bob animasyonu (hareket sırasında) |
| VFX2 | 08 | Bina hasar görseli + mekansal ses | ✔️ | O22 | BuildingEntity.TintDamage (HP<%50 renk koyulaşır → kömürleşme) |
| MKT | 01 | Dalgalı market fiyatı | ✔️ | O22 | MarketSystem supply/demand fiyat dalgası + drift; GameManager.Tick |
| RES | 01 | Kaynak çeşidi (berry/deer/fish) | ✔️ | O22 | ResourceFactory.BerryBush+FishPond; WorldRoot'ta 8+4 adet haritaya dağıtıldı |
| TRB | 01/09 | Tribute + çiftlik decay | ✔️ | O22 | ResourceNode.decayPerSecond; FarmField 2/s decay → reseed baskısı |
| VIC2 | 09 | Diplomasi/resign/conquest/maç ayarları | ✔️ | O22 | MatchSystem.Resign; HUD Esc pause menüsü (devam/teslim/restart) |
| MP1 | 10 | Mimari karar: Lockstep vs Client-Server | ⬜ | — | büyük tasarım kararı — ayrı oturum |
| MP2 | 10 | Determinizm ön-koşulu | ⬜ | — | MP1'e bağlı |
| MP3 | 10 | Transport + lobby + desync | ⬜ | — | MP1'e bağlı |
| CIV | 11 | Civ tanım veri yapısı | ⬜ | — | büyük geliştirme — balance+playtesting gerektirir |
| UNQ | 11 | Unique unit + unique tech | ⬜ | — | CIV'e bağlı |
| BAL | 11 | Civ seçim UI + balance pass | ⬜ | — | CIV'e bağlı |
| EDIT | 12 | Senaryo/harita editörü + trigger | ⬜ | — | SAVE üstüne; büyük geliştirme |
| CMP | 12 | Kampanya + terrain çeşidi + başlangıç ayarları | ⬜ | — | EDIT'e bağlı; uzun vadeli |

---

## Oturum günlüğü

| Oturum | Tarih | Madde | Sonuç |
|---|---|---|---|
| — | 2026-06-02 | docs/ + PROGRESS kurulumu | ✔️ 12 kategori dokümanı + bu tracker oluşturuldu |
| O15 | 2026-06-02 | `GAR` Garnizon | ✔️ 14 dosya; yeni GarrisonSystem.cs; 0 err/0 warn; 7 kabul kriteri MCP harness ile doğrulandı |
| O16 | 2026-06-02 | `ARM` Zırh tipleri + counter matrisi | ✔️ 11 dosya; DamageType enum + Spearman birim; 0 err/0 warn; 6 kabul kriteri MCP harness ile doğrulandı |
| O17 | 2026-06-02 | `IMP` Imperial çağ + `ARM`+`GAR` commit | ✔️ 6 dosya; commit `8c0333e` |
| O18 | 2026-06-02 | `CTRL`+`IDLE`+`MMP`+`WON`+`SCR`+`RLW` | ✔️ P1 UI/UX batch; 0 err; MCP doğrulandı |
| O19 | 2026-06-02 | `VIS` Görsel Kalite: Kenney Nature+Castle+Audio | ✔️ KenneyModels; DamagePopup; HitFlash; AudioManager; commit `37c4bda` / `8338f8f` |
| O20 | 2026-06-02 | `DIFF` AI zorluk seviyeleri | ✔️ çarpan katmanı + HUD cycle; 0 err |
| O21 | 2026-06-02 | P1 backlog toplu (TOW/BLK/MON/MONK/STN/TIER/UNI/AIMS) | ✔️ 13 dosya; commit `6e9ac2d` |
| O22 | 2026-06-03 | P2 toplu (MAP/QOL/AICB/AIGS/MKT/TRB/RES/TRD/VIC2/AURA/ABIL/VET/CBX/RQ/VFX2/ANIM/SAVE) | ✔️ 0 err; MCP doğrulandı |
| O23 | 2026-06-03 | `VIS2` Kenney dikdörtgen kale duvarı + FantasyTown Kit | ✔️ WorldRoot/BuildingFactory/KenneyModels; 0 err; MCP Play doğrulandı |
