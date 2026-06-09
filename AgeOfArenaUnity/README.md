# Age of Arena — Unity (C#)

Three.js sürümünden Unity'ye taşınan AoE2-tarzı izometrik RTS'in iskeleti.
Built-in Render Pipeline kullanır (URP/HDRP paketi gerekmez), tüm sahne
çalışma anında kod ile kurulur — elle `.unity` sahnesi gerektirmez.

## Açma

1. **Unity Hub** → `Add` → `Add project from disk`
2. Bu klasörü seç: `AgeOfArenaUnity/`
3. Editor sürümü **6000.4.1f1** ile açılır (kurulu).
4. Üstteki **Play** ▶ tuşuna bas.

İlk Play'de `GameBootstrap` otomatik çalışır ve sahneyi kurar: izometrik kamera,
güneş + sis, zemin, surlu üs (Town Center + evler + kışla), orman halkası,
altın/taş madenleri.

## Kontroller

| Tuş | İşlev |
|---|---|
| Ok tuşları / ekran kenarı | Kamera pan |
| Fare tekerleği | Zoom |
| S / A / P | Dur / saldır-yürü / devriye |
| Q / F / H | Duruş / formasyon / kule çanı |
| Esc | Pause menüsü |

## Yapı

```
Assets/Scripts/
  GameBootstrap.cs      Play'de otomatik çalışan giriş noktası
  WorldRoot.cs          Tüm sahneyi kurar (ışık, zemin, kamera, üs, orman, maden)
  IsometricCameraRig.cs Ortografik izometrik kamera + pan/zoom
  BuildingFactory.cs    TownCenter / House / Barracks (prosedürel)
  ResourceFactory.cs    Tree / GoldMine / StoneMine
  Prims.cs              Box/Cylinder/Cone/Sphere + materyal yardımcıları
```

## Yol Haritası

Güncel backlog ve DoD için [../docs/PLAN.md](../docs/PLAN.md) dosyasını kullan.
