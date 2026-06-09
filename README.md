# Age of Arena

AoE2 Arena tarzı 3D izometrik RTS — **Unity (C#)**.

## Teknoloji

- **Unity 6000.4.1f1** — Built-in Render Pipeline (URP/HDRP gerekmez)
- **C#** — tüm oyun mantığı `AgeOfArenaUnity/Assets/Scripts/`
- Sahne çalışma anında kod ile kurulur (elle `.unity` sahnesi yok)

## Çalıştırma

1. **Unity Hub** → `Add` → `Add project from disk`
2. Klasörü seç: `AgeOfArenaUnity/`
3. Editor **6000.4.1f1** ile açılır → **Play** ▶

İlk Play'de `GameBootstrap` sahneyi otomatik kurar: izometrik kamera, zemin + NavMesh,
4 üs, orman, altın/taş madenleri, başlangıç birimleri.

## Temel Kontroller

- Kamera: ok tuşları veya ekran kenarı; fare tekerleği zoom.
- Birim komutları: `S` dur, `A` saldır-yürü, `P` devriye, `Q` duruş, `F` formasyon.
- Köylü inşa kısayolları bağlama duyarlıdır; örn. `H` ev, `F` tarla. Aynı tuşlar bu sırada global
  komut tetiklemez.
- `H` savaş ekranında kule çanı; `Esc` pause menüsü.

## Belgeler

- [CLAUDE.md](CLAUDE.md) — proje talimatları ve teknik notlar
- [HANDOFF.md](HANDOFF.md) — güncel mimari, sistemler ve yapılacaklar
- [AgeOfArenaUnity/README.md](AgeOfArenaUnity/README.md) — Unity kurulum + kontroller

> Not: Proje başlangıçta Three.js/TypeScript web prototipiydi; Unity'ye taşındıktan sonra
> eski web kaynağı kaldırıldı (git geçmişinde mevcut).
