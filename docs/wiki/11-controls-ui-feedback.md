# Kontroller & UI / Ses / VFX — AoA Wiki

> ⚙️ **Güncel HUD (2026-06-03):** Bu sayfa orijinal HUD'u anlatır. AoE2-sadık yeniden tasarım
> (dokulu üst/alt bar + kalıcı 4-bölge alt bar + kamera-RTT **diamond minimap** + tıkla-navigasyon)
> `UiSkin.cs`'te uygulanmıştır (9-slice + Kenney CC0).
> Aşağıdaki üst/alt bar ve minimap bölümleri o rework'le güncellenmiştir.

> Age of Arena'nın **oyuncu arayüzü, girdi (input) ve geri-bildirim katmanı**: oyun-başı
> medeniyet/zorluk/mod seçim ekranı, dokulu üst bar + 4-bölge alt bar + kamera-RTT diamond
> minimap, AoE2-tarzı komut barı, fare/klavye seçimi ve emirleri (attack-move/patrol/garnizon/
> duruş), diplomasi paneli, isometric kamera, ses havuzu, ölüm partikülleri + havada-yükselen
> hasar sayıları + kamera sarsıntısı, fog of war ve quick-save. Tüm UI **kod ile runtime'da**
> kurulur (elle `.unity` sahnesi yok). Bu sayfadaki **her sayı doğrudan koddan** alınmıştır;
> tek doğruluk kaynağı verilen stat JSON'u boş geldiği için yalnızca koddan teyit edilen
> değerler listelenmiştir, teyit edilemeyenler "kodda tanımlı değil" diye işaretlenir.
>
> Kod kaynağı:
> [CivSelectScreen.cs](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs),
> [HUD.cs](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs),
> [CommandSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs),
> [SelectionSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs),
> [IsometricCameraRig.cs](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs),
> [AudioManager.cs](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs),
> [VisualEffectSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs),
> [DamagePopup.cs](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs),
> [MinimapSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs),
> [FogOfWarSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs),
> [MatchSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs),
> [SaveSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs).

---

## 1. Ne olduğu

Bu katman oyuncunun oyunla konuştuğu her şeyi kapsar:

- **Medeniyet seçim ekranı (CIVS)** — Oyun açılışında arena üzerine binen overlay: "Yok" (None) +
  10 oynanabilir medeniyet 4 sütunlu grid'de; seçim team 0'a **anında** uygulanır (HP/hız/toplama
  bonusları canlı). Ekranda ayrıca **STRT** zorluk satırı (6 seviye: Kolay→Orta→Normal→Zor→Acımasız
  →Efsanevi) ve **oyun modu** satırı (Rastgele/Ölüm Maçı/Regicide/Göçebe) tıkla-döngü pille bulunur.
- **HUD / Komut barı** — Üstte **dokulu** kaynak/çağ/relic/zorluk pili barı; altta **4-bölge** dokulu
  alt bar: sol bilgi paneli (isim + HP + garnizon/duruş + üretim ilerleme çubuğu + kuyruk şeridi),
  ortada **5×3 sabit slot** komut kartı, sağda kamera-RTT **diamond minimap** bölgesi. Butonlar klavye
  hotkey'leriyle aynı sistemleri çağırır; karşılanamayan aksiyonlar kararır (dim). Bina kit'i varsa
  bar/buton/slot çerçeveleri `UiSkin` 9-slice (Kenney CC0) ile dokulanır.
- **Seçim** — Sol-tık tek birim, Shift toggle, sürükleme kutusu (drag-box), çift-tık ekrandaki
  aynı tip, kontrol grupları (Ctrl+1..9 / 1..9), boşta köylü döngüsü (`.`).
- **Emirler** — Sağ-tık bağlama duyarlı (move / gather / attack / build-repair / garrison /
  rally), attack-move (A), patrol (P), dur (S), duruş döngüsü (Q, 4 durum).
- **Diplomasi (DIPL)** — D tuşu sol kenarda paneli açar/kapatır; her AI takımı (Kırmızı/Yeşil/Sarı)
  için Düşman↔Tarafsız↔İttifak durumu tıkla-döngü.
- **Kamera** — Orthographic isometric rig: WASD/ok/kenar-kaydırma pan, scroll zoom, Q/E döndürme,
  hasar sarsıntısı, gruba odaklanma.
- **Minimap** — RenderTexture top-down kamera, 45° döndürülerek **diamond** olarak çizilen RawImage
  + nokta (blip) katmanı; sol-tık/sürükle pan, sağ-tık move.
- **Ses** — `AudioSource` havuzu üzerinden tek-atış (one-shot) SFX (kılıç, ok, inşa, eğitim,
  ölüm, buton, birim/köylü seçimi, hareket onayı, çağ atlama fanfarı).
- **VFX** — Ölüm partikül patlaması (küçük/büyük), büyük bina yıkımında kamera sarsıntısı ve hasar
  başına **havada yükselen hasar sayısı** (DamagePopup; crit'te altın renk).
- **Fog of War** — 128×128 CPU dokulu üç-kademeli görüş (varsayılan **kapalı**).
- **Save/Load** — F5/F9 ile PlayerPrefs JSON snapshot (kaynak + çağ + tech). Oyun bittiğinde **R** ile
  yeniden başlat (civ/zorluk/mod korunur).

---

## 2. Nasıl çalışır (mekanik + formül)

### Medeniyet/zorluk/mod seçim ekranı (CIVS + STRT)
`WorldRoot.Build()` sonunda bir `CivSelectScreen` spawn edilir; kendi `Canvas`'ını
(ScreenSpaceOverlay, `sortingOrder=5000` → HUD'un üstünde) ve gerekirse bir `EventSystem`
kurar ([CivSelectScreen.cs:28](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L28)).
Tam ekran karartma üzerine "MEDENİYETİNİ SEÇ" başlığı, ardından **None + her oynanabilir civ**
(`CivilizationDefs.Playable()`) 4 sütunlu grid'de (buton 260×90, boşluk 28×26) dizilir
([CivSelectScreen.cs:52](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L52)). Her butonda
civ adı + tek satır bonus ipucu var ([CivSelectScreen.cs:180](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L180)).
Tıklayınca `Choose` `GameManager.playerCiv`'i set eder, **mevcut team-0 birimlerinin** max-HP ve
hızını yeniden hesaplar (bonus anında etkin), seçimi `GameBootstrap.PlayerCiv`'e kalıcılaştırır ve
overlay'i kapatır ([CivSelectScreen.cs:101](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L101)).
**STRT zorluk satırı** tıkla-döngüyle 6 seviyeyi gezer (`%6` modülo) ve canlı `EnemyAI`'lere anında
uygular ([CivSelectScreen.cs:124](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L124));
**oyun modu satırı** 4 modu (`%4`) gezer ([CivSelectScreen.cs:143](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L143)).
Her iki seçim de `GameBootstrap.Next*` alanlarına yazıldığından **restart sonrası korunur**.

### HUD kurulumu ve yenileme döngüsü
`HUD.Init` runtime'da `Canvas` (ScreenSpaceOverlay, sortingOrder=100, referans çözünürlük
1920×1080, `MatchWidthOrHeight=0.5`) kurar, **dokulu üst barı** (`BuildTopBar`) ve **4-bölge alt
komut barını** oluşturur, `ResourceManager.OnChanged` ve `GameEvents.OnAgeAdvanced` olaylarına abone
olur ([HUD.cs:83](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L83), [HUD.cs:194](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L194)).
Üst bar girişleri: Food / Wood / Gold / Stone / Pop / Relic + tıklanabilir **zorluk pili**
([HUD.cs:207](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L207), [HUD.cs:232](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L232)).
Bar/pil/slot çerçeveleri kit varsa `UiSkin.SkinPanel`/`Slice` 9-slice ile dokulanır
([HUD.cs:204](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L204)). Alt bar 4 bölgeye ayrılır: sol
**bilgi paneli** (`LeftW=240`), orta **komut grid bölgesi** (`CmdZoneW=352`) ve sağ **minimap bölgesi**
(`MinW=230`, diamond burada parent'lanır) ([HUD.cs:72](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L72),
[HUD.cs:519](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L519)).
Her `Update`'te seçim imzası (bina / birim sayısı / villager var mı / tech versiyonu) değişmişse
kart yeniden kurulur; aksi halde sadece slot durumları ve ilerleme barı tazelenir
([HUD.cs:691](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L691)). Komut barı yalnızca bir bina
veya ≥1 birim seçiliyse görünür ([HUD.cs:678](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L678)).
Seçim bir bina ise bilgi paneli **canlı garnizon sayacı** (`Garnizon N/cap`), birim ise **duruş**
satırını (`Duruş: <ad> [Q]`) gösterir ([HUD.cs:784](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L784),
[HUD.cs:814](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L814)).

**Karşılanabilirlik (affordability):** her slotun bir `Func<bool>` predicate'i vardır; her frame
değerlendirilir, durum değişince buton `interactable` ve rengi güncellenir. Kararma rengi
`Color.Lerp(c, DisabledBase, 0.72f)` ile hesaplanır ([HUD.cs:1188](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1188)).
Eğitim butonu için ek koşul: `pop < popCap` ([HUD.cs:895](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L895)).

### Seçim mantığı
Sürüklemenin kutu sayılması için fare en az `DragThreshold = 5` px hareket etmeli
([SelectionSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L13)). Çift-tık
penceresi `DblClickWindow = 0.35 sn` ([SelectionSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L14));
aynı tipte ve pencere içindeyse `SelectSameTypeOnScreen` ekrandaki (viewport 0..1) tüm aynı tip
birimleri seçer ([SelectionSystem.cs:264](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L264)).
Kontrol grubu çift-bas penceresi `DoubleTapWindow = 0.4 sn` — ikinci basışta kamera gruba odaklanır
([SelectionSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L21)).
HUD üstündeyse tıklama uGUI'ye bırakılır (`IsPointerOverGameObject`).

### Sağ-tık emir önceliği
`CommandSystem.Update` sağ-tık hedefini şu sırayla çözer
([CommandSystem.cs:60](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L60)):
seçili kendi binası → rally point; düşman → attack; inşa/hasarlı kendi binası → villager build/repair;
garnizon boşluğu olan kendi binası → garrison; kaynak düğümü → gather; "Ground" → move.
Çoklu birimde formasyon `cols = ceil(sqrt(n))`, `rows = ceil(n/cols)`, hücre aralığı
`FormationSpacing = 1.5` ([CommandSystem.cs:356](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L356)).
Her move/gather/attack için 0.5 sn ömürlü halka marker (`LineRenderer`, yarıçap 0.7, 20 segment)
düşürülür ([CommandSystem.cs:398](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L398)).

**Oyun hızı:** `[` ile `timeScale` 0.5 azalır (min 0.5), `]` ile 0.5 artar (max 4), Space ile
0 ↔ 1 toggle ([CommandSystem.cs:232](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L232)).

### Kamera rig
Sabit pitch **30°**, başlangıç yaw **45°**, mesafe **60** birim
([IsometricCameraRig.cs:21](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L21)).
Pan `panSpeed=25`, kenar-kaydırma marjı `edgeMargin=10` px, döndürme `rotateSpeed=90°/sn`,
zoom `zoomSpeed=4` orthographic birim/notch, ölçek `minSize=6 / maxSize=30` (başlangıç 11)
([IsometricCameraRig.cs:11](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L11),
[IsometricCameraRig.cs:55](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L55)).
Pan, odak noktasını `bounds = (60, 60)` yarı-genişliğine kelepçeler. Sarsıntı `Random.insideUnitSphere
* magnitude` (Y=0) ofseti uygular; süre/şiddet birikimsel max alır.

### Diplomasi paneli (DIPL)
`HUD.Update`'te **D** tuşu `ToggleDiplomacyPanel`'i çağırır ([HUD.cs:694](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L694)).
Panel sol kenarda (220×160) açılır; her AI takımı (Kırmızı/Yeşil/Sarı) için `gm.diplomacy[0, team]`
durumunu Düşman→Tarafsız→İttifak sırasıyla döndüren bir buton vardır; etiket rengi duruma göre değişir
([HUD.cs:1573](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1573),
[HUD.cs:1581](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1581)).

### Diamond minimap
`MapSize=140` dünya birimi, `TexSize=256`, RawImage karesi `Side=130` px (45° döndürülünce
diamond ≈184px) ([MinimapSystem.cs:22](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L22)).
İkincil ortografik kamera dümdüz aşağı (90°) RenderTexture'a render eder; RawImage HUD'un
**minimap bölgesine** parent'lanır ve `Quaternion.Euler(0,0,45)` ile döndürülür → kare harita
diamond okunur ([MinimapSystem.cs:46](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L46),
[MinimapSystem.cs:87](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L87)). Nokta (blip)
katmanı dönen RawImage altında uGUI Image olarak parent'lanır, böylece rotasyonu miras alır. Sol-tık/
sürükle kamerayı yeniden ortalar, sağ-tık seçimi oraya yönlendirir; tıklama `ScreenPointToLocalPointInRectangle`
ile 45° rotasyona duyarlı çözülür ([MinimapSystem.cs:187](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L187)).

### Ses
`AudioManager` singleton, `PoolSize=10` AudioSource havuzu, klipler `Resources/Audio/`'tan önceden
yüklenir ([AudioManager.cs:20](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L20)). `PlaySound`
havuzda round-robin ilerler ve `vol * 0.7` global zayıflatma ile `PlayOneShot` çağırır
([AudioManager.cs:68](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L68)). **10 `SoundId`** tanımlı:
Sword, Arrow, BuildComplete, UnitTrained, UnitDie, ButtonClick, UnitSelect, UnitMove, UnitVillager, AgeUp
([AudioManager.cs:11](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L11)). UnitMove/UnitVillager/AgeUp
şu an mevcut klipleri **yeniden kullanır** (placeholder; benzersiz klip gelince değiştirilecek)
([AudioManager.cs:28](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L28)). Seçim sesi kullanıcı
aksiyonu başına bir kez (vol 0.5), buton tıklaması vol 0.6 ile çalar.

### VFX
Ölüm partikülü `ParticleSystem` patlaması: ömür `0.8 / 0.5 sn` (büyük/küçük)
([VisualEffectSystem.cs:56](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L56)),
hız `5 / 3`, boyut `0.5 / 0.28`, burst `28 / 12` partikül, maxParticles `30 / 14`, küresel şekil
yarıçapı `0.6 / 0.25`. Obje `ömür + 0.2 sn` sonra yok edilir. `ShakeBuildings` listesindeki büyük
binalar yıkılınca kamera `Shake(0.35 sn, 0.4 şiddet)` ([VisualEffectSystem.cs:42](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L42)).

### Hasar sayıları (DamagePopup)
Her vuruşta `DamagePopup.Show` hedefin üstünde bir `TextMesh` doğurur (Canvas yükü yok); hasar
miktarını yazar, `RiseSpeed=2.2` ile yukarı süzülür, `Duration=0.75 sn` boyunca alfa 1→0 fade olup
kendini yok eder ([DamagePopup.cs:11](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs#L11),
[DamagePopup.cs:22](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs#L22)). Crit'te yazı **altın
sarısı + kalın + fontSize 32** (normalde beyaz, 24); her frame `Camera.main`'e billboard döner. Çağıran:
[CombatSystem.cs](../../AgeOfArenaUnity/Assets/Scripts/CombatSystem.cs),
[Projectile.cs](../../AgeOfArenaUnity/Assets/Scripts/Projectile.cs).

### Fog of War
Varsayılan **kapalı** (`fogEnabled=false`); açıldığında 128×128 RGB24 doku, görüş kademesi
`Black=0 / Shroud=70 / Lit=255` (R kanalı), enemy renderer toggle aralığı `VisCheckInterval=0.5 sn`
([FogOfWarSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L21)).
Birim/bina görüş yarıçapları tabloda.

### Save/Load
F5 kaydeder, F9 yükler ([SaveSystem.cs:26](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs#L26)).
Snapshot yalnızca kaynak (food/wood/gold/stone), `ageIndex`, araştırılan tech ID listesi ve `popCap`
içerir; sahne yeniden kurulmaz, değerler mevcut oyuna uygulanır ([SaveSystem.cs:32](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs#L32)).

---

## 3. Gerçek statlar (koddan)

> Not: Bu sistem için ayrı stat JSON verilmediği için (girdi boş `[]`) tüm değerler doğrudan
> ilgili C# dosyasından teyit edilmiştir. Her satır kaynağına bağlıdır.

| Stat | Değer | Kaynak |
|---|---|---|
| Civ seçim grid (sütun, buton W×H) | 4, 260×90 | [CivSelectScreen.cs:55](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L55) |
| Civ seçim seçenekleri | None + 10 civ | [CivSelectScreen.cs:52](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L52) |
| Zorluk seviyeleri (döngü) | 6 (Kolay→…→Efsanevi) | [CivSelectScreen.cs:134](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L134) |
| Oyun modları (döngü) | 4 (Rastgele/Ölüm Maçı/Regicide/Göçebe) | [CivSelectScreen.cs:153](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L153) |
| Komut kartı grid (sütun × satır) | 5 × 3 (15 slot/sayfa) | [HUD.cs:67](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L67) |
| Buton boyutu (W × H), boşluk | 60 × 60, 6 | [HUD.cs:70](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L70) |
| Alt komut barı yüksekliği | 220 | [HUD.cs:72](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L72) |
| Sol bilgi paneli / komut grid / minimap bölge genişliği | 240 / 352 / 230 | [HUD.cs:73](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L73) |
| Canvas referans çözünürlük | 1920 × 1080 | [HUD.cs:160](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L160) |
| Çağ popup süre / fade | 2.5 / 0.5 sn | [HUD.cs:125](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L125) |
| Kararma lerp oranı (DisabledBase'e) | 0.72 | [HUD.cs:1188](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1188) |
| Drag-box eşiği | 5 px | [SelectionSystem.cs:13](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L13) |
| Çift-tık penceresi | 0.35 sn | [SelectionSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L14) |
| Kontrol grubu çift-bas penceresi | 0.4 sn | [SelectionSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L21) |
| Kontrol grubu aralığı | 1–9 | [SelectionSystem.cs:181](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L181) |
| Formasyon birim aralığı | 1.5 | [CommandSystem.cs:12](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L12) |
| Move marker ömrü / yarıçap / segment | 0.5 sn / 0.7 / 20 | [CommandSystem.cs:411](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L411) |
| Oyun hızı min / max / adım | 0.5 / 4 / 0.5 | [CommandSystem.cs:233](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L233) |
| Kamera pitch (sabit) | 30° | [IsometricCameraRig.cs:22](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L22) |
| Kamera başlangıç yaw / mesafe | 45° / 60 | [IsometricCameraRig.cs:21](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L21) |
| Pan hızı | 25 | [IsometricCameraRig.cs:11](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L11) |
| Zoom hızı / min / max ölçek | 4 / 6 / 30 | [IsometricCameraRig.cs:12](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L12) |
| Başlangıç ortho ölçek | 11 | [IsometricCameraRig.cs:55](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L55) |
| Döndürme hızı / kenar marjı | 90°/sn / 10 px | [IsometricCameraRig.cs:15](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L15) |
| Pan sınırı (yarı-genişlik) | 60 × 60 | [IsometricCameraRig.cs:17](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L17) |
| Minimap dünya kapsamı / doku / RawImage karesi | 140 / 256 / 130 (45° → diamond ≈184px) | [MinimapSystem.cs:22](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L22) |
| Minimap diamond rotasyonu | Z+45° | [MinimapSystem.cs:87](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L87) |
| Ses havuzu boyutu | 10 | [AudioManager.cs:20](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L20) |
| Global ses zayıflatma | ×0.7 | [AudioManager.cs:68](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L68) |
| Seçim / buton sesi vol | 0.5 / 0.6 | [SelectionSystem.cs:162](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L162), [HUD.cs:572](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L572) |
| Tanımlı SFX (SoundId) sayısı | 10 | [AudioManager.cs:11](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L11) |
| Hasar sayısı ömrü / yükseliş hızı | 0.75 sn / 2.2 | [DamagePopup.cs:11](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs#L11) |
| Hasar sayısı fontSize (crit / normal) | 32 / 24 | [DamagePopup.cs:30](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs#L30) |
| Partikül ömrü (büyük / küçük) | 0.8 / 0.5 sn | [VisualEffectSystem.cs:56](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L56) |
| Partikül hız / boyut (büyük / küçük) | 5/3 · 0.5/0.28 | [VisualEffectSystem.cs:57](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L57) |
| Partikül burst / max (büyük / küçük) | 28/12 · 30/14 | [VisualEffectSystem.cs:62](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L62) |
| Kamera sarsıntı (süre / şiddet) | 0.35 sn / 0.4 | [VisualEffectSystem.cs:45](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L45) |
| Fog varsayılan | kapalı (false) | [FogOfWarSystem.cs:31](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L31) |
| Fog doku / dünya yarı-genişlik | 128 / 60 | [FogOfWarSystem.cs:21](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L21) |
| Fog kademe (siyah/gölge/aydınlık R) | 0 / 70 / 255 | [FogOfWarSystem.cs:33](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L33) |
| Fog enemy-görünürlük aralığı | 0.5 sn | [FogOfWarSystem.cs:26](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L26) |
| Birim görüş (Scout/Cav/Archer/Militia/Vil/Treb) | 13/9/8/7/5/4 | [FogOfWarSystem.cs:199](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L199) |
| Bina görüş (TC/Castle/askeri/diğer) | 10/8/7/5 | [FogOfWarSystem.cs:210](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L210) |
| Save anahtarı / slot | "AoA_SaveSlot_0" | [SaveSystem.cs:14](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs#L14) |

**Klavye/fare kısayolları (koddan):**

| Tuş / girdi | Aksiyon | Kaynak |
|---|---|---|
| Sol-tık / Shift+sol-tık | Seç / toggle ekle-çıkar | [SelectionSystem.cs:55](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L55) |
| Sol-tık sürükle | Drag-box seçim | [SelectionSystem.cs:71](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L71) |
| Çift sol-tık | Ekrandaki aynı tip | [SelectionSystem.cs:104](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L104) |
| Ctrl+1..9 / 1..9 | Grup ata / çağır | [SelectionSystem.cs:181](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L181) |
| `.` (Period) | Sıradaki boşta köylü | [SelectionSystem.cs:53](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L53) |
| Sağ-tık | Bağlama duyarlı emir | [CommandSystem.cs:60](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L60) |
| S / A / P | Dur / attack-move / patrol | [CommandSystem.cs:245](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L245) |
| Q | Duruş döngüsü (Saldırgan→Savunmacı→Yerinde Dur→Saldırma) | [HUD.cs:698](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L698) |
| U | Garnizon boşalt | [CommandSystem.cs:289](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L289) |
| D | Diplomasi panelini aç/kapat | [HUD.cs:694](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L694) |
| `[` / `]` / Space | Yavaşlat / hızlandır / duraklat | [CommandSystem.cs:234](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L234) |
| WASD / Ok / kenar | Kamera pan | [IsometricCameraRig.cs:34](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L34) |
| Q / E | Kamera döndür | [IsometricCameraRig.cs:86](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L86) |
| Scroll | Zoom | [IsometricCameraRig.cs:81](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L81) |
| R (bina yerleştirme) | Footprint'i 90° döndür | [BuildingPlacement.cs:85](../../AgeOfArenaUnity/Assets/Scripts/BuildingPlacement.cs#L85) |
| R (oyun bitti ekranı) | Yeniden başlat (civ/zorluk/mod korunur) | [MatchSystem.cs:57](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L57) |
| Esc | Duraklama menüsü | [HUD.cs:637](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L637) |
| F5 / F9 | Quick-save / load | [SaveSystem.cs:28](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs#L28) |

> Not: **Q tuşu iki işlev paylaşır** — birim seçiliyken duruş döngüsü, kamera için sol-döndürme.
> **R tuşu** bina-yerleştirme modunda footprint döndürür, oyun-bitti ekranında restart yapar; oyun
> içinde serbest restart yalnızca duraklama menüsündeki "Yeniden Başlat" butonuyla
> ([HUD.cs:1293](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1293)).
> Özelleştirilebilir hotkey haritası **kodda tanımlı değil** — tüm tuşlar `KeyCode` ile sabit.

---

## 4. Strateji & counter

- **Kontrol grupları**, AoA'da mikro-yönetimin bel kemiği. Orduyu Ctrl+1, köylüleri Ctrl+2 yapıp
  sürekli üretim için TC'yi bir gruba almak APM kazandırır; çift-bas kamerayı gruba ışınlar
  ([SelectionSystem.cs:204](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L204)).
- **Boşta köylü (`.`)** ekonomik kaçağı kapatır; HUD pili "Boşta köylü: N (.)" sayar
  ([HUD.cs:658](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L658)). Düzenli `.` basmak,
  rakipten kaynak avantajı sağlar.
- **Oyun hızı** tek-oyunculu pratik için: zor anlarda `[` ile yavaşlatıp mikro yap, ekonomik
  döngülerde `]` ile hızlandır. (Çok-oyunculuda determinizm garantisi **yok**.)
- **Çift-tık ile kitle seç**, bir okçuya çift-tıklayıp ekrandaki tüm okçuları toplamak ani
  focus-fire için idealdir.
- **Görüş counter'ı:** Scout 13 birim görüşle haritanın en geniş alanını açar; rakip Scout'unu
  öldürmek onu kör eder. Ancak fog **varsayılan kapalı** olduğundan bu avantaj yalnızca fog
  açıkken devreye girer ([FogOfWarSystem.cs:31](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L31)).
- **Sağ-tık önceliğini** bilmek hatalı emirleri önler: hasarlı kendi binası varken villager
  seçiliyse sağ-tık inşa/onarım verir; saldırı için düşmana tıklamak gerekir.

---

## 5. Çapraz bağlantılar

- Komut kartındaki **eğitim/araştırma butonları** birim ve tech maliyetleriyle çalışır →
  [./02-units.md](./02-units.md), [./05-tech-tree.md](./05-tech-tree.md).
- **Bina + garnizon** paneli (U boşalt, N/cap sayaç) → [./04-buildings.md](./04-buildings.md).
- **Pazar al/sat** butonları ve ekonomi geri-bildirimi → [./08-economy-trade.md](./08-economy-trade.md).
- **Duruş (stance)** ve attack-move savaş davranışını sürer → [./07-combat-counters.md](./07-combat-counters.md).
- **Çağ popup / barı**, **civ seçim ekranı** ve zorluk/medeniyet pilleri → [./01-game-flow-ages.md](./01-game-flow-ages.md),
  [./06-civilizations.md](./06-civilizations.md), [./09-ai-difficulty.md](./09-ai-difficulty.md).
- **Diplomasi paneli** (D, takım ilişkileri) → [./09-ai-difficulty.md](./09-ai-difficulty.md).
- **Zafer countdown banner'ı** (Wonder/relic) → [./10-victory-objectives.md](./10-victory-objectives.md).

---

## 6. Kod referansları (file:line, derivation)

- Civ/zorluk/mod seçim ekranı: [CivSelectScreen.cs:18](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L18),
  Choose (canlı uygulama) [CivSelectScreen.cs:101](../../AgeOfArenaUnity/Assets/Scripts/CivSelectScreen.cs#L101).
- HUD canvas + abonelikler: [HUD.cs:83](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L83);
  grid/bölge sabitleri (5×3, 60px, BarH=220, 240/352/230): [HUD.cs:67](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L67);
  dokulu üst bar + zorluk pili: [HUD.cs:194](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L194),
  [HUD.cs:232](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L232).
- Diplomasi paneli (D toggle + durum döngüsü): [HUD.cs:1573](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1573).
- Komut barı görünürlük + rebuild imzası: [HUD.cs:678](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L678),
  [HUD.cs:691](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L691).
- Affordability predicate + kararma: [HUD.cs:703](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L703),
  Dim formülü [HUD.cs:1188](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1188).
- Tooltip / hotkey badge / cost satırı: [HUD.cs:485](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L485),
  [HUD.cs:548](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L548), CostLine [HUD.cs:1320](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1320).
- Üretim kuyruğu şeridi (tıkla-iptal): [HUD.cs:799](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L799).
- Esc duraklama menüsü + game over: [HUD.cs:1100](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1100),
  [HUD.cs:1140](../../AgeOfArenaUnity/Assets/Scripts/HUD.cs#L1140).
- Seçim (tek/shift/box/çift-tık/grup/idle): [SelectionSystem.cs:80](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L80),
  [SelectionSystem.cs:133](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L133),
  [SelectionSystem.cs:178](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L178),
  [SelectionSystem.cs:245](../../AgeOfArenaUnity/Assets/Scripts/SelectionSystem.cs#L245).
- Sağ-tık emir akışı + marker + formasyon: [CommandSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L36),
  [CommandSystem.cs:345](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L345),
  [CommandSystem.cs:398](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L398).
- Attack-move / patrol pick: [CommandSystem.cs:308](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L308),
  [CommandSystem.cs:259](../../AgeOfArenaUnity/Assets/Scripts/CommandSystem.cs#L259).
- Kamera pan/zoom/rotate/shake: [IsometricCameraRig.cs:62](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L62),
  [IsometricCameraRig.cs:110](../../AgeOfArenaUnity/Assets/Scripts/IsometricCameraRig.cs#L110).
- Diamond minimap kamera + rotasyon + click-to-world: [MinimapSystem.cs:46](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L46),
  [MinimapSystem.cs:87](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L87),
  [MinimapSystem.cs:187](../../AgeOfArenaUnity/Assets/Scripts/MinimapSystem.cs#L187).
- Ses havuzu + one-shot + 10 SoundId: [AudioManager.cs:11](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L11),
  [AudioManager.cs:68](../../AgeOfArenaUnity/Assets/Scripts/AudioManager.cs#L68).
- VFX partikül + sarsıntı: [VisualEffectSystem.cs:49](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L49),
  [VisualEffectSystem.cs:36](../../AgeOfArenaUnity/Assets/Scripts/VisualEffectSystem.cs#L36).
- Hasar sayısı (rise/fade/crit + billboard): [DamagePopup.cs:22](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs#L22),
  [DamagePopup.cs:43](../../AgeOfArenaUnity/Assets/Scripts/DamagePopup.cs#L43).
- Oyun-bitti R restart (civ/zorluk/mod koru): [MatchSystem.cs:57](../../AgeOfArenaUnity/Assets/Scripts/MatchSystem.cs#L57).
- Fog doku + görüş + enemy toggle: [FogOfWarSystem.cs:105](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L105),
  [FogOfWarSystem.cs:165](../../AgeOfArenaUnity/Assets/Scripts/FogOfWarSystem.cs#L165).
- Save/Load snapshot: [SaveSystem.cs:32](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs#L32),
  [SaveSystem.cs:54](../../AgeOfArenaUnity/Assets/Scripts/SaveSystem.cs#L54).

---

## 7. AoE2 farkı (reference köprü)

Detaylı QoL backlog'u için: [docs/PLAN.md](../PLAN.md) (Açık İşler).

AoA artık AoE2 hissini geniş ölçüde yakalar: oyun-başı medeniyet/zorluk/mod seçimi, dokulu üst bar +
4-bölge alt bar + diamond minimap, sol bilgi + orta komut kartı (hotkey + buton paritesi, üretim
kuyruğu, tooltip), diplomasi paneli, duruş/attack-move/patrol/garnizon ve havada yükselen hasar
sayıları. Ayrıca AoE2'de olmayan ya da geç gelen birkaç QoL'u erken sunar (idle-worker pili, çift-tık
aynı-tip, kontrol grubu, minimap pan/komut). Başlıca **AoE2'de var, AoA'da eksik/farklı** noktalar:

- **Özelleştirilebilir hotkey profili** yok (AoE2'nin tam tuş haritası karşısında sabit `KeyCode`).
- **Akıllı/grid formasyonlar** (Line/Box/Staggered/Flank) yok; AoA tek `ceil(sqrt(n))` kare grid.
- **Fog of War varsayılan kapalı** — AoE2'de fog ve "explored shroud" temel kuraldır; AoA'da görsel
  ve opsiyonel.
- **Shift ile çoklu-waypoint emir kuyruğu** (devriye dışı) yok.
- **Detaylı birim istatistik paneli** (saldırı/zırh/menzil sayıları) ve ikon-zırh sınıfı rozetleri
  HUD'da gösterilmez; bilgi paneli isim + HP + garnizon/duruş/üretim ile sınırlı.
- **Minimap ölçekleme/zoom** ve **sinyal (flare)** yok — diamond minimap sabit boyut; sürükle-pan ve
  sağ-tık komut var.

---

## 8. Eksikler/Yapılacaklar

| ID-aday | sınıf | eksik | AoE2-ref | efor |
|---|---|---|---|---|
| HOTK | Core (QoL) | Özelleştirilebilir hotkey haritası (kayıt/yükle) | AoE2 tam tuş profili | Orta |
| QUEUE | Derinlik | Shift ile çoklu-waypoint emir kuyruğu | AoE2 shift-queue | Orta |
| FORM | Derinlik | Formasyon tipleri (Line/Box/Staggered/Flank) | AoE2 formasyon butonları | Orta |
| STAT | Core | Bilgi panelinde birim saldırı/zırh/menzil göstergesi | AoE2 detay paneli | Düşük |
| FLARE | Derinlik | Minimap sinyal (flare) ping'i | AoE2 map ping | Düşük |
| FOGUI | Core | Fog of War'ı varsayılan açma + UI toggle | AoE2 temel fog | Düşük |
| MMZOOM | Derinlik | Minimap'te ölçek/zoom (sürükle-pan zaten var) | AoE2 minimap | Düşük |
| SAVEF | Core | Tam sahne save/load (birim/bina pozisyonları) | AoE2 tam kayıt | Yüksek |
| VOL | Core (QoL) | Ses seviyesi ayarı (master/SFX/müzik) | AoE2 ses ayarları | Düşük |
| MUSIC | Derinlik | Arka plan müzik katmanı | AoE2 müzik | Orta |
