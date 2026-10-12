# Roadmap CrimeSim0

> Diese Datei ist die gemeinsame Anweisungsquelle für Cursor und Grok Bot. Aufgaben und Entscheidungen hier festhalten und aktuell halten.

## Offene Entscheidungen
- [ ] Finale Standard-Perspektive (Third oder First Person; Umschaltung bleibt dauerhaft drin)
- [ ] Setting und Stadt
- [ ] Grafikstil
- [ ] Umfang des ersten Prototyps

## Phase 0: Grundlagen
- [x] Grundgerüst (Ordnerstruktur, .gitignore, Git LFS, Starter-Skripte)
- [ ] Projekt in Unity 6000.3.25f1 öffnen, generierte Dateien committen

## Phase 1: Vertical Slice (Prototyp)
- [x] Umstieg auf URP vorbereitet (Paket + Menü CrimeSim → URP einrichten)
- [ ] URP in Unity einrichten, Szenen neu erzeugen und committen
- [x] Umschaltbare Kamera Third/First Person (CameraController, Taste V)
- [x] Testlevel per Editor-Menü (CrimeSim → Testlevel erstellen)
- [ ] Bewegung und Kamera im Testlevel testen und feinjustieren
- [ ] Kleine Stadtzone als Blockout (ein paar Straßen, Gebäude, Wohnung)
- [ ] Interaktionssystem (Objekte und NPCs ansprechen oder benutzen)
- [ ] Inventar (Gegenstände aufnehmen, lagern, abgeben)
- [ ] Wirtschaft und Geld (Kaufen, Verkaufen, Kontostand, Speichern)
- [ ] Einfache NPC-Kunden (Anfrage, Treffpunkt, Bezahlung)
- [x] Character Creator Grundsystem (Aussehen, Speichern, Platzhalter-Rig, Creator-Szene)
- [x] Veränderlicher Körper: BodyCondition (Fett/Muskel), weiche Anpassung, Debug-Tasten F1–F4
- [x] BlendShapeBodyTarget für echte Modelle (MPFB/Blender, Humanoid)
- [ ] Erstes MPFB-Charaktermodell mit Rigify und Shape Keys (fat, muscle, build) importieren
- [ ] Bedürfnisse (z. B. Hunger, Schlaf, Energie) und Zuhause
- [ ] Körper koppeln an Essen, Training und Aktivität
- [ ] UI: HUD, Inventar, Pausemenü

## Phase 2: Erweiterung
- [ ] Produktion und Ausbau des Geschäfts
- [ ] Dealer anheuern und verwalten
- [ ] Geldwäsche über legale Fassaden-Geschäfte
- [ ] Beziehungen zu NPCs
- [ ] Fahrzeuge
- [ ] Polizei und Fahndungslevel
- [ ] Eigene neue Mechaniken ausarbeiten und einbauen

## Phase 3: Inhalte und Feinschliff
- [ ] Weitere Stadtteile
- [ ] Modelle aus Blender (FBX), Sound, Effekte
- [ ] Balancing, Speichern und Laden, Menüs
