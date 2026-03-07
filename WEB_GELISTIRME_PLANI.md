# AGEOFARENA – Three.js ile AoE2 Arena (Mobil Web)

## 1. Teknoloji

| Bileşen        | Seçim           | Not                              |
|----------------|-----------------|----------------------------------|
| 3D Motor       | **Three.js**    | İzometrik 3D, WebGL, mobil uyumlu |
| Dil            | **TypeScript**  | Tip güvenliği                    |
| Build          | **Vite**        | Hızlı dev server                 |
| Dağıtım        | Web (PWA)       | Mobil öncelikli, tarayıcıdan     |

## 2. Klasör Yapısı

```
web/
├── index.html
├── package.json
├── vite.config.ts
├── tsconfig.json
└── src/
    ├── main.ts                 # Three.js giriş, sahne, render döngüsü
    ├── config.ts               # Oyun sabitleri
    ├── camera/
    │   ├── IsometricCamera.ts  # OrthographicCamera (AoE2 açısı)
    │   └── CameraControls.ts   # Pan (touch/mouse) + zoom (pinch/scroll)
    └── world/
        ├── Ground.ts           # Zemin, grid, dama deseni
        └── Bases.ts            # Oyuncu ve düşman üsleri, şehir merkezleri
```

## 3. Geliştirme Aşamaları

### Faz 1 – MVP
- [x] Three.js proje kurulumu (Vite + TS)
- [x] İzometrik kamera (OrthographicCamera, 30° açı)
- [x] Zemin + grid + dama deseni
- [x] Oyuncu/düşman üsleri + şehir merkezleri (low-poly)
- [x] Mobil kontroller (pan + pinch zoom)
- [x] Kaynak UI (Food/Wood/Gold)
- [ ] Birim oluşturma (Köylü, Piyade, Okçu)
- [ ] Seçim sistemi (raycasting ile tıklama/dokunma)
- [ ] Hareket sistemi (pathfinding)
- [ ] Kaynak toplama (köylü → ağaç/maden/çiftlik)
- [ ] Bina yerleştirme (Kışla, Okçu Evi)
- [ ] Birim üretimi (kuyruk sistemi)
- [ ] Basit AI
- [ ] Kazanma/Kaybetme

### Faz 2 – Arena Hissi
- [ ] Duvarlar ve kapılar
- [ ] Ortadaki kaynaklar
- [ ] Daha fazla birim/bina

### Faz 3 – Çok Oyunculu
- [ ] PvP (WebSocket)
- [ ] PWA desteği

## 4. Çalıştırma

```bash
cd web
npm install
npm run dev
```
