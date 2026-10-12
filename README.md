# CrimeSim0

Unity-Spielprojekt. **Unity-Version:** 6000.3.25f1

## Projekt öffnen
1. Git LFS einmalig installieren: `git lfs install` ([git-lfs.com](https://git-lfs.com))
2. Repo klonen: `git clone https://github.com/sensista/CrimeSim0`
3. In Unity Hub „Add project from disk“ → den geklonten Ordner wählen und mit **6000.3.25f1** öffnen.
   Unity erzeugt beim ersten Öffnen die fehlenden `ProjectSettings`, `Packages` und `.meta`-Dateien – diese danach committen.

## Ordnerstruktur
```
Assets/_Project/
  Scripts/  Core, Player, AI, UI, Systems
  Scenes/  Prefabs/  Audio/  Settings/
  Art/  Models, Materials, Textures
docs/ROADMAP.md   Aufgaben & Meilensteine
```

## Einrichtung Spieler und Kamera
1. **Player:** leeres GameObject mit `CharacterController` und `PlayerController`. Das Modell (z. B. eine Capsule) als Kind, Layer `PlayerBody` (unter Tags & Layers anlegen).
2. **HeadAnchor:** leeres Kind-Objekt des Players auf Augenhöhe (ca. Y = 1,6).
3. **Main Camera:** `CameraController` hinzufügen und die Felder setzen: `Target` = Player, `Head Anchor` = HeadAnchor, `Collision Mask` = alles außer `PlayerBody`. `Player Body Layer` steht bereits auf `PlayerBody`.
4. Optional bei Player im Feld `Camera Controller` die Kamera eintragen, sonst wird sie automatisch gesucht.
5. Steuerung: WASD bewegen, Maus umsehen, **V** schaltet zwischen Third und First Person.

## Testlevel erstellen
Im Unity-Menü **CrimeSim → Testlevel erstellen** wählen. Das Skript:
- legt bei Bedarf den Layer `PlayerBody` an,
- erstellt Materialien unter `Assets/_Project/Art/Materials` (URP/Lit, sonst Standard),
- baut eine Szene mit Licht, Boden, Gebäudeblöcken, Rampe, Treppe, Kisten, Player (inkl. HeadAnchor) und fertig verdrahteter Kamera,
- speichert sie unter `Assets/_Project/Scenes/TestLevel.unity`.

Erneut ausführen überschreibt die Szene. Danach Play drücken: WASD, Maus, **V** zum Umschalten.

## Character Creator
1. Menü **CrimeSim → Character Creator Szene erstellen** (erzeugt `Scenes/CharacterCreator.unity`) und **CrimeSim → Testlevel erstellen** erneut ausführen (Player bekommt das neue Charakter-Rig). Beide Menüs tragen die Szenen in die Build Settings ein (Creator zuerst).
2. `CharacterCreator` öffnen und Play drücken: Name, Größe, Statur, Kopfgröße, Körperfett, Muskeln, Hautfarbe, Frisur, Kleidung und Farben einstellen, **Zufall** würfelt. Linke Maustaste ziehen dreht die Vorschau.
3. **Fertig** speichert nach `Application.persistentDataPath/character.json` und lädt `TestLevel`. Dort lädt der Player den Charakter automatisch.

**Feste Merkmale** (aus dem Creator): Größe, Statur, Kopf, Haut, Haare, Kleidung.
**Veränderlicher Körper** (`BodyCondition` am Player): Körperfett und Muskeln (0–1). Werte ändern sich weich sichtbar und werden gespeichert. Später gekoppelt an Essen, Training und Aktivität.
**Debug im TestLevel:** F1/F2 mehr/weniger Fett, F3/F4 mehr/weniger Muskeln.

Technik: `CharacterAppearanceApplier` baut einen Platzhalter aus Primitives (nur Fallback). Austauschbar über `bodyModelPrefab` (echtes Modell) und `CharacterPartLibrary` (Create → CrimeSim → Character Part Library: Prefabs pro Slot Hair/Top/Bottom/Shoes). Körperform läuft über `IBodyShapeTarget`, sodass Blendshapes die Skalierung ersetzen.

## Charaktermodelle: MPFB → Blender → Unity
1. In Blender das Add-on **MPFB** (MakeHuman für Blender) installieren, einen Menschen erzeugen und ein **Rigify**-Rig hinzufügen (Rigify generieren).
2. Für Körperformen Shape Keys anlegen bzw. aus MPFB-Zielen backen, z. B. `fat`, `muscle`, `build` (Namen frei, aber in Unity gleich eintragen). Modifier vorher anwenden, damit die Shape Keys erhalten bleiben.
3. Export als **FBX**: Armature + Mesh, „Apply Transform“ / Skalierung 1, „Shape Keys“ aktiv, Deform-Bones only (Rigify: nur `DEF-`-Knochen), keine Leaf Bones.
4. In Unity unter `Art/Models` ablegen. Import: Rig → **Animation Type: Humanoid**, Avatar prüfen; Model → **Import BlendShapes** an.
5. Prefab aus dem Modell erstellen, `BlendShapeBodyTarget` hinzufügen und die Zuordnung pflegen (Blendshape-Name → Parameter BodyFat/Muscle/Build/Height/HeadSize, Kurve Wert 0–1 → Gewicht 0–100).
6. Das Prefab beim `CharacterAppearanceApplier` als `Body Model Prefab` eintragen. Dann werden keine Primitives mehr erzeugt. Hinweis: Höhe wird bereits über die Rig-Skalierung umgesetzt, daher `Height` als Blendshape nur nutzen, wenn `heightScale` 1 bleibt.

## Spielkonzept
3D-Open-World-Crime-Sim in Unity 6, eine Mischung aus bekannten Vorbildern plus eigenen, neuen Mechaniken:
- **GTA:** offene Stadt, Fahrzeuge, Polizei und Fahndungslevel
- **Drug Dealer Simulator / Schedule 1:** ein illegales Geschäft aufbauen (Produktion, Verkauf, Kunden, Dealer, Geldwäsche)
- **Die Sims:** Lebenssimulation mit Bedürfnissen, eigenem Zuhause und Beziehungen
- **Eigene Ideen:** bestehende Mechaniken verbinden und neue erfinden

**Perspektive:** 3D. Eine umschaltbare Third-/First-Person-Kamera (Taste V) bleibt während der ganzen Entwicklung im Projekt, auch um Animationen zu prüfen. Die finale Standard-Perspektive ist _noch offen_.

**Noch offen:**
- Finale Standard-Perspektive (Third oder First Person)
- Setting und Stadt
- Grafikstil
- Umfang des ersten Prototyps
- Eigene neue Kernmechaniken
