# PlayerBase – MPFB-Spielerfigur

- `PlayerBase.fbx`: Body (13 380 Vertices) + Eyes, Eyebrows, Eyelashes, Teeth, Underwear_Panties, Underwear_Top (weiblich), Underwear_Briefs (männlich, CC-BY-Platzhalter) auf dem 53-Knochen-Game-Engine-Skelett (Root → pelvis …). Nur Deform-Knochen, keine Leaf-Bones, −Z vorne / Y oben, Scale 1, max. 4 Gewichte pro Vertex, normalisiert.
- Basis = **geschlechtsneutral** (alle MPFB-Makros 0.5, Rassen je 1/3, Alter 25 J.).
- `PlayerBase.shapes.json`: alle Shape Keys mit Kategorie, deutschem Anzeigenamen, Richtung/Paar (incr/decr), betroffenen Proxies, `jointOffsets` (Knochenkopf-Verschiebung in m bei Gewicht 1) und `heightJointOffsets` für die Größe.
- Neu erzeugen: `Tools/MPFB/build_player.py` (Blender 4.2 + MPFB 2.0.18, `blender -b --python build_player.py`).

## Shape-Key-Typen
- `macro_g{0,05,1}_m{..}_w{..}`: 3×3×3-Gitter Geschlecht × Muskel × Gewicht (26 Shapes, Mitte = Basis). Unity mischt trilinear (Phase 2).
- `macro_age75_*`, `macro_age100_*`: Alter (~57 / 90 J.) pro Geschlecht 0/0.5/1.
- `macro_race_{african,asian,caucasian}_g{0,1}`: Herkunft je Geschlecht.
- `macro_breast_c*_f*`: Körbchengröße × Festigkeit (bei Frau), 3×3-Gitter.
- `macro_proportions_{0,1}`.
- Alle übrigen: MPFB-Modifier-Targets (Gesicht/Körper, je Richtung ein Shape).
- **Größe ist kein Blendshape**: per Root-/Knochenskalierung (`heightJointOffsets`, `heightTotal_m`).

## Genitalien später ergänzen (optional)
Basis enthält keine Genital-Targets/-Geometrie. Ein optionales Genital-Mesh wird wie die anderen Proxies gebaut: MPFB-Proxy (.mhclo) auf dem neutralen Menschen fitten, Gewichte vom selben `game_engine`-Rig (pelvis/thigh) übernehmen, und in `build_player.py` in `prox` eintragen – das Skript überträgt dann alle Shape Keys über die Body-Vertex-Referenzen der .mhclo. Als eigenes SkinnedMesh unter `PlayerRig` exportieren und per Option ein-/ausblenden; die Unterwäsche bleibt Standard.

## Unity (Phase 2)
- `MPFBPlayerImporter` (Editor) stellt das FBX automatisch ein: Humanoid, Create From This Model, Hips = `pelvis`, explizite Knochenzuordnung (Unreal-Namen), Blendshapes an, Normalen importieren, Blendshape-Normalen berechnen, 4 Gewichte, keine Materialien/Animation. Beim allerersten Import läuft erst die Auto-Zuordnung, danach importiert das Skript einmal neu mit der expliziten Zuordnung.
- Ruhepose ist eine A-Pose. Humanoid funktioniert damit; sehen Animationen schief aus: FBX auswählen → Rig → Configure → Pose → Enforce T-Pose → Apply.
- Menü **CrimeSim → Spielerfigur Prefab erstellen** erzeugt `Prefabs/Player/PlayerMPFB.prefab` (MPFBShapeDriver, URP-Materialien unter `Art/Materials/Player`).
- `MPFBShapeDriver` liest dieses Manifest zur Laufzeit (TextAsset) und mischt die Shapes:
  - Gitter Geschlecht × Muskel × Gewicht: trilinear (Achsenpunkte 0 / 0,5 / 1, Mitte = Basis).
  - Alter 25 → 57 → 90 J. (Makro 0,5 / 0,75 / 1) je Geschlecht; jünger als 25 wird nicht unterstützt.
  - Herkunft: Gewichte normalisiert, je Geschlecht 0/1 linear gemischt.
  - Brust: Körbchen × Festigkeit bilinear, skaliert mit „Weiblichkeit“ (1 − gender).
  - Modifier: Paar decr/incr als Regler −1..1, einzelne Targets 0..1.
  - Unterwäsche: gender < 0,5 → Panties + Top, sonst Briefs (Namen im Inspector änderbar, Kleidung später einfach austauschbar).
- **Koordinaten:** Blender (x, y, z) → Unity (−x, z, −y). Export mit −Z vorne / Y oben dreht −90° um X: (x, y, z) → FBX (x, z, −y); Unity spiegelt beim Import X. Der Driver prüft das beim Start gegen die echten Knochen (`boneHeadsNeutral`) und meldet die Abweichung in der Konsole.
- **Skelett folgt:** Knochenköpfe werden um die gewichteten `jointOffsets` verschoben und die Bindposes der Mesh-Kopien neu berechnet (keine Doppelverformung). Größe = Uniform-Scale (`heightTotal_m`), CharacterController passt sich an.

## Optimierung (später)
Im Spiel könnten alle festen Creator-Shapes in ein eigenes Mesh gebacken werden (nur Fett/Muskel-Gitter bleibt dynamisch). Das spart Speicher (~630 Blendshapes je Mesh-Kopie) und CPU. Siehe docs/ROADMAP.md.
