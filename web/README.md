# AGEOFARENA – Web (AoE2 Arena tarzı mobil RTS)

Web tabanlı, mobil öncelikli Age of Empires 2 Arena tarzı RTS. Phaser 3 + TypeScript + Vite.

## Kurulum

```bash
npm install
```

## Geliştirme

```bash
npm run dev
```

`http://localhost:5173` adresinde açılır. Mobilde test için bilgisayarın yerel IP’si ile aynı portu kullan (örn. `http://192.168.1.x:5173`).

## Build

```bash
npm run build
```

Çıktı: `dist/` — statik dosyaları istediğin web sunucusuna atabilirsin.

## Proje yapısı

- `src/main.ts` – Phaser başlangıç ve scale
- `src/config.ts` – Oyun sabitleri (boyut, kaynaklar, grid)
- `src/data/` – Birim ve bina verileri (UnitData, BuildingData)
- `src/scenes/GameScene.ts` – Ana sahne (harita, UI, kamera)

Detaylı plan: repo kökündeki `WEB_GELISTIRME_PLANI.md`.
