# 04 — Savaş & Kuşatma Mekaniği

## 1. Durum Özeti

`CombatSystem` her birim için hedefe yürü→menzilde dur→vur döngüsünü işletiyor; melee ve
ranged (Projectile) ayrımı, cavalry charge (ilk vuruş 2.5×) ve Trebuchet anti-yapı bonusu var.
`BuildingCombatSystem` Castle otomatik atışını yönetiyor. Hasar şu an **düz** (`AttackDamage`,
[CombatSystem.cs:114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L114)) — **zırh tipi,
attack stance, formasyon kohezyonu ve okçu balistiği (gecikme/yay) yok.** Savaşın taktik
derinliği buradan gelir.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Melee döngü (chase→stop→hit) | ✅ | [CombatSystem.cs:23](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L23) | Core |
| Otomatik en yakın düşman bulma (aggro) | ✅ | [CombatSystem.cs:139](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L139) | Core |
| Ranged projectile (homing ok/bolt) | ✅ | [Projectile.cs:34](../AgeOfArenaUnity/Assets/Scripts/Projectile.cs#L34) | Core |
| Cavalry charge (2.5× ilk vuruş) | ✅ | [CombatSystem.cs:123](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L123) | Derinlik |
| Trebuchet anti-yapı (3×) | ✅ | [CombatSystem.cs:113-114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L113) | Core |
| Castle yapı-atışı | ✅ | [BuildingCombatSystem.cs:16](../AgeOfArenaUnity/Assets/Scripts/BuildingCombatSystem.cs#L16) | Core |
| HP barları (3D floating) | ✅ | [CombatSystem.cs](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs) | Core |
| `IDamageable` (birim+bina aynı arayüz) | ✅ | [IDamageable.cs](../AgeOfArenaUnity/Assets/Scripts/IDamageable.cs) | Core |
| **Hasar↔zırh tip matrisi** (melee/pierce/siege) | ❌ | düz dmg [CombatSystem.cs:114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L114) | Core |
| **Attack stance** (aggressive/defensive/stand/no-attack) | ❌ | hep otomatik aggro | Core |
| **Formasyon kohezyonu** (birlik halinde hareket) | ❌ | grid spread var, kohezyon yok | Derinlik |
| **Okçu balistiği** (atış gecikmesi + yay + ıskalama) | ❌ | homing+anında | Derinlik |
| **Flanking / arkadan bonus** | ❌ | — | Derinlik |
| **Morale / panik** | ❌ | — | Derinlik |
| **Friendly fire** (ok dostu vurur) | ❌ | — | Derinlik |
| **Kuşatma çeşidi** (Battering Ram, Mangonel splash) | ❌ | sadece Trebuchet | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Hasar↔zırh tip matrisi
- **Neden:** Counter sisteminin ([02](02-units.md)) motor tarafı; "okçu piyadeyi yener, mızrak süvariyi yener" buradan ölçülür.
- **Yaklaşım:** Saldırıya `damageType ∈ {Melee,Pierce,Siege}`, hedefe `meleeArmor/pierceArmor`. Hasar formülünü [CombatSystem.cs:114](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L114): `effectiveDmg = max(1, atk - armor[type])`. Mevcut anti-yapı çarpanı ve charge bu hesabın *üstüne* uygulanır (sıra korunur). Trebuchet `damageType=Siege`, yapı pierce-zırhı yüksek → okçu binaya zayıf, Trebuchet güçlü doğal olarak çıkar.
- **Dokunulacak dosyalar:** `GameTypes.cs` (DamageType enum), `UnitEntity.cs`/`BuildingDefs.cs` (armor alanları), `CombatSystem.cs:113-125`, `BuildingCombatSystem.cs`.
- **Kabul Kriteri:** Aynı saldırı, yüksek pierce-zırhlı hedefe okla az, melee ile çok hasar verir; min 1 hasar garantisi korunur; charge/anti-yapı çarpanları hâlâ çalışır.
- **Doğrulama:** Play → okçu vs yüksek-pierce hedef ve melee vs aynı hedef → `Unity_Camera_Capture`'da ölüm sürelerini kıyasla; cavalry charge ilk vuruşu hâlâ 2.5×.

### [P1] Attack stance (aggressive / defensive / stand ground / no-attack)
- **Neden:** Birim kontrolünün temel taktik katmanı; köylü/okçu yanlışlıkla dalmaz.
- **Yaklaşım:** `UnitEntity.stance` enum; `CombatSystem` aggro yarıçapını ([CombatSystem.cs:139](../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs#L139)) stance'a göre ayarla: Aggressive=düşmanı kovala, Defensive=yerinde karşılık ver, Stand ground=hareket etme sadece menzildekine vur, No-attack=hiç saldırma. UI ikonları [07](07-ui-ux-qol.md).
- **Dokunulacak dosyalar:** `GameTypes.cs`, `UnitEntity.cs`, `CombatSystem.cs:139`, `CommandSystem.cs` (stance hotkey), `HUD.cs`.
- **Kabul Kriteri:** No-attack köylü düşmanı görse de toplamaya devam eder; Stand-ground birim yerinden kıpırdamadan menzildekine ateş eder; Aggressive eski davranış.
- **Doğrulama:** Play → köylüyü No-attack yap, yanına düşman getir → toplamaya devam eder (Camera.Capture); okçuyu Stand-ground yap → ilerlemez ama atar.

### [P1] Formasyon kohezyonu
- **Neden:** Şu an grid'e dağılıyor ama birlikte yürürken hız/şekil korunmuyor; karışık ordu dağılıyor.
- **Yaklaşım:** Grup hareketinde ([CommandSystem.cs](../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs) formasyon grid'i) en yavaş birimin hızına senkron + line/box formasyon seçimi; melee önde, ranged arkada dizilimi.
- **Dokunulacak dosyalar:** `CommandSystem.cs`, `UnitEntity.cs` (formasyon offset/hız), opsiyonel `HUD.cs` (formasyon butonu).
- **Kabul Kriteri:** Karışık grup birlikte ve şekli koruyarak yürür; melee önde, okçu arkada; en yavaş birime senkron.
- **Doğrulama:** Play → 10 karışık birim seç+yürüt → `Unity_Camera_Capture`'da formasyonun korunduğunu gözle.

### [P2] Okçu balistiği, kuşatma çeşidi, flanking, morale, friendly fire
- **Balistik:** Projectile'a uçuş süresi + hedef kaçarsa ıskalama. **Kabul:** hareketli hedefe atılan ok bazen ıskalar.
- **Kuşatma çeşidi:** Battering Ram (anti-yapı melee), Mangonel (alan hasarı). **Kabul:** Mangonel tek atışta kümedeki birden çok birime hasar.
- **Flanking/morale/friendly-fire:** derinlik polish; her biri `CombatSystem` hasar dalına opsiyonel modifier.

## 4. Referans Repolardan Notlar

- **unity-rts**: ranged projectile hız matematiği + melee mesafe kontrolü.
- **LockstepRTSEngine**: deterministik 2D fizik (X-Z), influence map — formasyon/aggro için referans; deterministlik [10](10-multiplayer.md) ile örtüşür.
- **openage**: hasar/zırh tipleri tamamen veri-güdümlü (nyan) — matris tablo olarak tutulmalı.

## 5. Bağımlılıklar

- Hasar matrisi → [02-units.md](02-units.md) (counter) — aynı armor alanlarını paylaşır.
- Stance → [06-ai.md](06-ai.md) (AI stance bilinci) + [07-ui-ux-qol.md](07-ui-ux-qol.md) (ikon).
- Garnizonlu atış → [03-buildings.md](03-buildings.md).
- Determinizm → [10-multiplayer.md](10-multiplayer.md).
