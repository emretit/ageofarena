# 11 — Medeniyetler & Balance

## 1. Durum Özeti

Tüm takımlar **tek, jenerik medeniyet** oynuyor — aynı birim/bina/tech setine sahipler; tek
fark `EnemyAI` kişiliği ([GameTypes.cs:20](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L20)).
Medeniyet katmanı (civ bonusları, unique unit/tech, civ'e özel kapalı tech ağacı) **yok**.
Bu, oyunun tekrar oynanabilirliğini (replayability) belirleyen büyük P2 kategori; ayrıca
mevcut sabit sayıların gözden geçirildiği **balance pass** burada toplanır.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Jenerik ortak tech/bina/birim seti | ✅ | [TechDefs.cs](../AgeOfArenaUnity/Assets/Scripts/TechDefs.cs), [BuildingDefs.cs](../AgeOfArenaUnity/Assets/Scripts/BuildingDefs.cs) | Core |
| AI kişilik çeşidi (oynanış farkı) | ✅ | [GameTypes.cs:20](../AgeOfArenaUnity/Assets/Scripts/GameTypes.cs#L20) | Derinlik |
| **Civ tanım veri yapısı** | ❌ | — | Derinlik |
| **Civ bonusları** (eko/askeri/tech hız) | ❌ | — | Derinlik |
| **Unique unit** (civ'e özel birim) | ❌ | — | Derinlik |
| **Unique tech** (civ'e özel araştırma) | ❌ | — | Derinlik |
| **Civ'e özel kısıtlı tech ağacı** (bazı tech kapalı) | ❌ | herkes her şeyi açar | Derinlik |
| **Civ seçim UI** (maç öncesi) | ❌ | — | Derinlik |
| **Balance pass** (sayı dengeleme) | 🟡 | sabitler elle ayarlı | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P2] Civ tanım veri yapısı (temel)
- **Neden:** Tüm civ özelliklerinin asıldığı omurga; data-driven tasarım sonradan civ eklemeyi ucuzlatır.
- **Yaklaşım:** `CivDef` statik tablosu (mevcut `TechDefs`/`BuildingDefs` deseni): bonus listesi (örn. "+%10 gather", "askeri bina −%15 maliyet"), unique unit tipi, unique tech listesi, kapalı tech kümesi. `TechState`/`ResourceManager`/`BuildingDefs` okumaları civ bonusunu çarpan olarak uygular. Takım başına `civId`.
- **Dokunulacak dosyalar:** yeni `CivDefs.cs`, `GameTypes.cs` (CivId enum + bonus tipleri), `TechState.cs` (bonus enjekte), `ResourceManager.cs`/`GatherSystem.cs` (eko bonus), `BuildingDefs.cs`/`TechDefs.cs` (maliyet/kilit), `WorldRoot.cs` (takıma civ ata).
- **Kabul Kriteri:** En az 2 civ tanımlı; civ A eko-bonuslu (gather ölçülebilir hızlı), civ B askeri-bonuslu (birim ucuz/güçlü); civ B'nin kapalı bir tech'i tech kartında çıkmaz.
- **Doğrulama:** `Unity_ManageEditor(Play)` → iki takıma farklı civ ata → gather hızı/birim maliyetini kıyasla → `Unity_GetConsoleLogs(Error)` temiz; kapalı tech'in kartta olmadığını gör.

### [P2] Unique unit + unique tech
- **Neden:** Civ kimliğinin "his" tarafı; her civ'e imza birim/araştırma.
- **Yaklaşım:** Unique unit `UnitType`'a eklenir ama sadece o civ'in Castle/özel binasında üretilebilir ([02](02-units.md), [03](03-buildings.md)); unique tech `TechDefs`'e civ-kısıtlı satır ([05](05-tech-ages.md)).
- **Dokunulacak dosyalar:** `GameTypes.cs`, `UnitFactory.cs`, `TechDefs.cs`, `CivDefs.cs`, `BuildingEntity.cs` (civ filtreli üretim).
- **Kabul Kriteri:** Civ A oyuncusu kendi unique unit'ini üretebilir, civ B üretemez; unique tech sadece sahibi civ'de araştırılır.
- **Doğrulama:** Play → civ A ile unique unit üret (başarılı), civ B ile dene (kart kapalı); unique tech benzer kontrol.

### [P2] Civ seçim UI + balance pass
- **Civ seçim:** maç öncesi (lobby/senaryo, bkz [10](10-multiplayer.md)/[12](12-maps-scenario-campaign.md)) civ seçimi. **Kabul:** seçilen civ maça doğru bonuslarla başlar.
- **Balance pass:** mevcut sabitleri (birim maliyet/hasar/hp, tech bonus) bir tabloda toplayıp gözden geçir; counter matrisi ([04](04-combat.md)) ile tutarlılık. **Kabul:** belgelenmiş hedef "time-to-kill" aralıklarına uyan dengelenmiş sayı seti.

## 4. Referans Repolardan Notlar

- **openage**: nyan DSL ile civ/bonus/unique tamamen data-driven ve moddable — `CivDefs` tablo yaklaşımının ilham kaynağı; ileride dış dosyadan (JSON/ScriptableObject) yüklemeye açık tut.

## 5. Bağımlılıklar

- Civ bonusları → [01-economy.md](01-economy.md) (eko bonus), [04-combat.md](04-combat.md)/[05-tech-ages.md](05-tech-ages.md) (askeri/tech bonus).
- Unique unit/tech → [02-units.md](02-units.md), [03-buildings.md](03-buildings.md), [05-tech-ages.md](05-tech-ages.md).
- Civ seçim → [10-multiplayer.md](10-multiplayer.md) (lobby), [12-maps-scenario-campaign.md](12-maps-scenario-campaign.md) (senaryo).
