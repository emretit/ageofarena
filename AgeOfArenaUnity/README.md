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
| WASD / ok tuşları | Kamera pan |
| Fare tekerleği | Zoom |
| Q / E | Döndür |

## Yapı

```
Assets/Scripts/
  GameBootstrap.cs      Play'de otomatik çalışan giriş noktası
  WorldRoot.cs          Tüm sahneyi kurar (ışık, zemin, kamera, üs, orman, maden)
  IsometricCameraRig.cs Ortografik izometrik kamera + pan/zoom/rotate
  BuildingFactory.cs    TownCenter / House / Barracks (prosedürel)
  ResourceFactory.cs    Tree / GoldMine / StoneMine
  Prims.cs              Box/Cylinder/Cone/Sphere + materyal yardımcıları
```

## Sıradaki adımlar (port yol haritası)

- [ ] Birim sistemi (villager/militia/archer) + seçim & sağ-tık komut
- [ ] Kaynak toplama (GatherSystem) + kaynak HUD
- [ ] Eğitim kuyruğu + bina inşası
- [ ] Pathfinding (flow field)
- [ ] Minimap + tam HUD
- [ ] GLTF/FBX asset'leri ile gerçek modeller
