# PlayerBase – MPFB-Spielerfigur

- `PlayerBase.fbx`: Body (13 380 Vertices) + Eyes, Eyebrows, Eyelashes, Teeth, Underwear_Panties, Underwear_Top auf dem 53-Knochen-Game-Engine-Skelett (Root → pelvis …). Nur Deform-Knochen, keine Leaf-Bones, −Z vorne / Y oben, Scale 1, max. 4 Gewichte pro Vertex, normalisiert.
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
