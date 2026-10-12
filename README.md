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
