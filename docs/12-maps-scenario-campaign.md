# 12 — Harita · Senaryo · Kampanya · Save/Load

## 1. Durum Özeti

Sahne **kod ile sabit kuruluyor**: `WorldRoot.Build()` zemini, 4 üssü, ormanı/madenleri,
relic'leri ve NavMesh'i runtime bake ediyor ([CLAUDE.md](../CLAUDE.md) — elle `.unity` sahnesi
yok). Tek, sabit bir arena var; **prosedürel harita üretimi, senaryo/harita editörü, kampanya
ve save/load yok**. Bu kategori single-player içeriğinin uzun-vade omurgası: tekrar oynanır
haritalar + kayıt + senaryo.

## 2. Ne Var / Ne Eksik

| Özellik | Durum | Codebase referansı | Çekirdek? |
|---|---|---|---|
| Kod-güdümlü sabit arena (zemin/üs/kaynak) | ✅ | `WorldRoot.Build()` ([WorldRoot.cs](../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs)) | Core |
| Runtime NavMesh bake (wall carving) | ✅ | [WorldRoot.cs](../AgeOfArenaUnity/Assets/Scripts/WorldRoot.cs), [CLAUDE.md](../CLAUDE.md) | Core |
| Restart (NavMesh temizle + yeniden kur) | ✅ | [GameBootstrap.cs](../AgeOfArenaUnity/Assets/Scripts/GameBootstrap.cs) | Core |
| **Save / Load** (oyun durumu serileştirme) | ❌ | — | Core (SP) |
| **Prosedürel harita üretimi** (rastgele dengeli) | ❌ | tek sabit arena | Core (SP) |
| **Harita/senaryo editörü** | ❌ | — | Derinlik |
| **Kampanya** (bağlı görevler + hikaye) | ❌ | — | Derinlik |
| **Zafer-tetikli senaryo mantığı** (script/trigger) | ❌ | — | Derinlik |
| **Terrain çeşidi** (su/dağ/orman/çöl) | ❌ | düz zemin | Derinlik |
| **Başlangıç ayarları** (kaynak/çağ/AI sayısı) | 🟡 | sabit init | Derinlik |

## 3. Eksikler — Öncelikli Backlog

### [P1] Save / Load (oyun durumu serileştirme)
- **Neden:** Single-player'ın temel beklentisi; uzun maçları sürdürmeyi sağlar.
- **Yaklaşım:** Tüm runtime durumunu (her takım `ResourceManager`/`TechState`, birim listesi [tip/konum/hp/state], bina listesi [tip/konum/hp/inşa/rally/garnizon], relic sahiplik, çağ, kamera) serileştir. `WorldRoot.Build()` zaten "veriden sahne kur" yapıyor — **Load = sabit arena yerine kayıttan kur**; aynı kurulum yolu yeniden kullanılır. JSON (okunur) veya binary (UnityTutorials-RTS deseni).
- **Dokunulacak dosyalar:** yeni `SaveSystem.cs`, `WorldRoot.cs` (veriden kurulum dalı), `GameManager.cs` (state toplama), tüm entity'lerde serileştirilebilir alanlar.
- **Kabul Kriteri:** Kaydet → çık → yükle ile maç aynen geri gelir: kaynaklar, birim/bina sayısı ve konumları, çağ, araştırılan tech'ler, relic sahipliği korunur.
- **Doğrulama:** `Unity_ManageEditor(Play)` → birkaç birim/bina/tech ilerlet → Kaydet → Restart → Yükle → `Unity_Camera_Capture` + HUD ile durumun birebir aynı olduğunu doğrula; `Unity_GetConsoleLogs(Error)` temiz.

### [P1] Prosedürel harita üretimi
- **Neden:** Tek arena tekrar oynanışı öldürür; dengeli rastgele harita çeşitliliği şart.
- **Yaklaşım:** `WorldRoot`'u parametrize et: seed'li yerleşim (simetrik başlangıç üsleri, dengeli kaynak/altın/relic dağıtımı, orman kümeleri). Determinizm gerekirse seed sabit ([10](10-multiplayer.md) ile uyumlu). NavMesh bake mevcut akışla aynı kalır.
- **Dokunulacak dosyalar:** `WorldRoot.cs` (seed + yerleştirme algoritması), `ResourceFactory.cs` (dağıtım), opsiyonel yeni `MapGenerator.cs`.
- **Kabul Kriteri:** Farklı seed farklı ama dengeli harita üretir (her takım eşit yakınlıkta kaynak); aynı seed aynı haritayı verir; NavMesh doğru bake olur.
- **Doğrulama:** Play → 2 farklı seed → `Unity_SceneView_Capture2DScene` ile farklı düzenleri gör; aynı seed tekrar → özdeş düzen; birimler her yere path bulur.

### [P2] Senaryo/harita editörü + trigger
- **Yaklaşım:** Basit editör modu (bina/birim/terrain yerleştir, başlangıç kaynağı/AI/zafer tipi ayarla, kaydet) — Save/Load formatını yeniden kullanır. Trigger: "X olursa Y" basit koşul-aksiyon listesi.
- **Kabul Kriteri:** Editörde harita kurup kaydet → o haritadan maç başlat; tanımlı trigger maç içinde ateşlenir (örn. "tüm relic toplanınca mesaj").
- **Doğrulama:** Editörde küçük harita kur → kaydet → oyna → trigger'ı tetikle, gözle.

### [P2] Kampanya + terrain çeşidi + başlangıç ayarları
- **Kampanya:** sıralı senaryolar + brifing/hikaye + ilerleme kaydı. **Kabul:** görev tamamlanınca sıradaki açılır.
- **Terrain çeşidi:** su (naval [02](02-units.md)), dağ (geçilmez), orman (görüş engeli) — NavMesh + FOW ile entegre. **Kabul:** su NavMesh dışı, dağ engel, orman görüşü keser.
- **Başlangıç ayarları:** maç öncesi kaynak/başlangıç çağı/AI sayısı/zafer tipi ([09](09-victory-objectives.md)). **Kabul:** seçilen ayarlarla maç başlar.

## 4. Referans Repolardan Notlar

- **UnityTutorials-RTS**: binary serialization ile save/load — doğrudan referans desen.
- **unity-rts**: level generation eksikliği bilinen sınır olarak not edilmiş; prosedürel üretim sıfırdan tasarlanmalı.
- **openage**: senaryo/trigger ve harita formatı (AoE2 .scx benzeri) — veri-güdümlü senaryo ilhamı.

## 5. Bağımlılıklar

- Save/Load → tüm sistemler (durum toplama); özellikle [03-buildings.md](03-buildings.md) (garnizon state), [05-tech-ages.md](05-tech-ages.md) (tech state).
- Prosedürel harita → [01-economy.md](01-economy.md) (kaynak dağıtımı), [10-multiplayer.md](10-multiplayer.md) (seed determinizmi).
- Terrain çeşidi → [02-units.md](02-units.md) (naval), Fog of War.
- Başlangıç/senaryo ayarları → [09-victory-objectives.md](09-victory-objectives.md), [11-civilizations-balance.md](11-civilizations-balance.md).
