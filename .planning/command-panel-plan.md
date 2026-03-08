# Sol Sidebar Command Panel (AoE2 Tarzı)

## Context
AoE2'de alt panelin sol/orta kısmında, seçilen varlığa göre bina inşa etme veya ünite eğitme butonları gösterilir. Şu an projede bu fonksiyon sadece alt HUD'un orta panelinde (`#action-buttons`) var. Kullanıcı, ekranın sol tarafında AoE2 tarzı ayrı bir **Command Panel** istiyor — seçime göre bina/ünite üretim butonları gösteren bir grid panel.

## Plan

### 1. `types.ts` — Köylünün inşa edebileceği binaları tanımla
- `VILLAGER_BUILDABLE: BuildingId[]` sabiti ekle: `['house', 'barracks', 'archeryRange', 'stable', 'blacksmith', 'market', 'castle']`
- Dosya: `src/entities/types.ts`

### 2. `icons.ts` — İkon fonksiyonlarını paylaşımlı modüle taşı
- `src/ui/HUD.ts` dosyasının altındaki `getBuildingIcon()` ve `getUnitIcon()` fonksiyonlarını yeni `src/ui/icons.ts` dosyasına taşı
- Her iki fonksiyonu export et
- HUD.ts'de import ile kullan
- Dosya: Yeni `src/ui/icons.ts`, düzenleme `src/ui/HUD.ts`

### 3. `index.html` — Sidebar HTML ve CSS ekle
- `#command-panel` div'i ekle (bottom-hud'un üstünde, sol tarafta konumlanacak)
- İçinde `#command-grid` (3 sütun x 5 satır grid) ve `#command-tooltip` (hover bilgi kutusu)
- CSS: Mevcut ortaçağ temasına uygun koyu kahve/altın tonları
- Panel genişliği ~160px, bottom-hud'un hemen üstüne oturacak (`bottom: 180px`)
- `.command-slot` (44x44px butonlar), `.command-slot.active`, `.command-slot.disabled` stilleri
- Tooltip: panelin sağında görünecek, isim + maliyet + açıklama
- `#command-panel`'a click-through filtresi için pointer-events ayarı
- Dosya: `index.html`

### 4. `CommandPanel.ts` — Ana panel sınıfı (YENİ DOSYA)
- Dosya: `src/ui/CommandPanel.ts`
- Sınıf yapısı:
  ```
  class CommandPanel {
    onSelectionChanged(entities: GameEntity[])  // Seçim değişince çağrılır
    updateButtonStates(resources: PlayerResources)  // Kaynak durumuna göre buton aktif/pasif
  }
  ```
- **Bağlam mantığı:**
  - Boş seçim → paneli temizle (boş slotlar göster)
  - Köylü seçili → `VILLAGER_BUILDABLE` listesinden bina butonları göster
  - Player 0 binası seçili + trainable var → eğitilebilir ünite butonları göster
  - Askeri ünite seçili → boş (ya da stance butonları, ileride)
  - Düşman varlığı → paneli temizle
- **Callback'ler:**
  - `onTrainClick: (building, unitId) => void` — TrainingQueue.enqueue'ye bağlanacak
  - `onBuildClick: (buildingId) => void` — Şimdilik console.log (bina yerleştirme sistemi henüz yok)
- **Tooltip:** Hover'da butonun sağında isim, maliyet (Food/Wood/Gold), eğitim süresi göster
- **Buton durumu:** Kaynak yetersizse `.disabled` class'ı ekle, yeterliyse kaldır
- Mevcut `icons.ts`'den `getBuildingIcon()` / `getUnitIcon()` import et

### 5. `Selection.ts` — CommandPanel entegrasyonu
- Constructor'a `commandPanel: CommandPanel` parametresi ekle
- `updateSelectionDisplay()` içinde, HUD güncellemesinden sonra `commandPanel.onSelectionChanged(this.selected)` çağır
- `onLeftClick` filtresine `#command-panel` ekle (tıklamaların oyun alanına geçmemesi için)
- Dosya: `src/systems/Selection.ts` — satır 30-36 (constructor), satır 243-249 (updateSelectionDisplay), satır 154 (click filter)

### 6. `main.ts` — Bağlantı ve game loop
- `CommandPanel` import et ve oluştur
- Callback'leri bağla:
  - `commandPanel.onTrainClick = (b, uid) => trainingQueue.enqueue(b, uid)`
  - `commandPanel.onBuildClick = (id) => console.log('Build:', id)` (ileride bina yerleştirme)
- `SelectionSystem` constructor'ına `commandPanel` geç
- Game loop'ta (frameCount % 6 bloğunda) `commandPanel.updateButtonStates(resourceManager.getResources(0))` çağır
- Dosya: `src/main.ts` — satır 136-139 (init), satır 265-277 (game loop)

## Değiştirilecek Dosyalar
1. `src/entities/types.ts` — VILLAGER_BUILDABLE ekleme
2. `src/ui/icons.ts` — **YENİ** paylaşımlı ikon modülü
3. `src/ui/HUD.ts` — ikon fonksiyonlarını import'a çevir
4. `index.html` — HTML + CSS ekleme
5. `src/ui/CommandPanel.ts` — **YENİ** ana panel sınıfı
6. `src/systems/Selection.ts` — CommandPanel entegrasyonu
7. `src/main.ts` — Başlatma ve game loop bağlantısı

## Doğrulama
1. `npm run dev` ile oyunu başlat
2. Ekranın sol tarafında koyu panel görünmeli (boş slotlar)
3. Köylü seç → panelde bina ikonları (House, Barracks, vb.) görünmeli
4. Bina seç (ör. Barracks) → panelde eğitilebilir üniteler (Militia, Spearman) görünmeli
5. Hover'da tooltip ile isim + maliyet gösterilmeli
6. Kaynak yetersizse buton soluk/disabled görünmeli
7. Ünite eğitim butonuna tıkla → TrainingQueue'ya eklenmeli (alt paneldeki queue'da da görünmeli)
8. Boş alana tıkla → panel temizlenmeli
