# VIA.WPF.Graph – P0-006 Phase-0-Abnahme und Step-Gate

**Status:** Phase-0-Abschlussdokumentation  
**Phase:** 0 – Baseline, Entscheidungen und technische Verifikation  
**Issue:** P0-006 – Phase-0-Abnahme und Step-Gate dokumentieren  
**Geltungsbereich:** allgemeine `VIA.WPF.Graph`-Library ohne UserFlow-/Mockup-/Host-Abhängigkeit

---

## 1. Zweck

Dieses Dokument fasst den Abschlussstand von Phase 0 zusammen und bildet die Entscheidungsgrundlage für das Step-Gate in Richtung Phase 1.

Phase 0 hat keine fachliche Graphmodell-API und keinen produktiven WPF-Renderer eingeführt. Sie hat ausschließlich die Solution-Grenzen, technische Graphviz-Verifikation, Minimalreferenz, Entscheidungsrahmen und Übergabedokumentation festgelegt.

---

## 2. Abgeschlossene Phase-0-Issues

| Issue | Ergebnis |
|---|---|
| P0-001 – Solution-Struktur und Isolationsgrenzen festlegen | Struktur- und Abhängigkeitsgrenzen dokumentiert; `.slnx` bleibt verbindlich; keine `.sln`-Migration. |
| P0-002 – Rubjerg.Graphviz 3.0.5 auf Zielrechner verifizieren | Rubjerg.Graphviz 3.0.5 auf Windows x64 / `win-x64` erfolgreich ausgeführt. |
| P0-003 – Minimalgraph als technische Referenz ausführen | Minimalgraph mit 6 Knoten, 6 gerichteten Kanten, Rückkante, 2 Clustern sowie TB-/LR-Layout sichtbar ausgeführt. |
| P0-004 – Minimalen neutralen Vertrag, Sizing und View-State entscheiden | Minimaler neutraler Vertragsrahmen, initiale Größenprofile und View-State-Grenzen dokumentiert; keine öffentliche API festgelegt. |
| P0-005 – Arbeits- und Übergabeprotokoll im Repository verankern | Arbeits-/Übergabeprotokoll für weitere Phasen dokumentiert. |

---

## 3. Verbindliche Isolationsgrenzen

Die in P0-001 festgelegte Referenzmatrix bleibt verbindlich:

```text
Core      -> keine Projekt-, WPF-, Graphviz-, UserFlow- oder Host-Abhängigkeit
Graphviz  -> Core
Wpf       -> Core
Demo      -> Core, Graphviz, Wpf

Graphviz <-> Wpf                         verboten
Alle Graph-Projekte -> UserFlow/Mockup   verboten
```

Die Demo ist ausschließlich Composition Root. Der WPF-Renderer darf keine Graphviz-Abhängigkeit erhalten.

---

## 4. Technische Verifikation

### 4.1 Rubjerg.Graphviz

Verifizierter Stand:

```text
Rubjerg.Graphviz package version: 3.0.5
Process architecture: X64
Runtime identifier: win-x64
```

Verifiziert wurden:

- Erzeugen und Layouten eines Graphen,
- 6 Knoten,
- 6 gerichtete Kanten einschließlich Rückkante,
- 2 Graphviz-Cluster,
- Layouts in Top-to-Bottom und Left-to-Right,
- Lesen von Graph-, Node-, Cluster-Bounds,
- Lesen der Rückkanten-Spline-Geometrie.

### 4.2 Unterstützte Runtime

Für die nächste Entwicklungsstufe gilt zunächst:

```text
Windows x64 / win-x64
```

Nicht zugesagt und nicht verifiziert:

- x86,
- ARM64,
- Linux,
- macOS,
- Deployment-Szenarien außerhalb des lokal geprüften Windows-x64-Kontexts.

---

## 5. Minimalgraph-Referenz

P0-003 liefert eine technische Referenzansicht in der Demo. Diese Ansicht ist kein finales Graph-UI-Design.

Sie dient ausschließlich dazu, spätere Änderungen an Graphviz-Anbindung, Layoutauswertung oder Runtime schnell gegen eine bekannte technische Referenz zu prüfen.

Bestandteile der Referenz:

- 6 Knoten,
- Popup-ähnlicher Knoten,
- Rückkante,
- 2 Gruppen/Cluster,
- TB-Layout,
- LR-Layout,
- sichtbare Bounds und Kantenführung in der Demo.

---

## 6. Entscheidungen für Phase 1

Für Phase 1 ist freigegeben, den neutralen Kern fachlich zu entwerfen und zu implementieren, sofern das Step-Gate ausdrücklich bestätigt wird.

Phase 1 darf enthalten:

- neutrales Knotenmodell,
- neutrales Linkmodell,
- Container- und Markierungsgruppen,
- Graphdokument,
- Validierung,
- Projektionen,
- zyklensichere Tree-Projektion,
- neutrale Auswahl- und View-State-Datenmodelle,
- Core-Tests.

Phase 1 darf nicht enthalten:

- WPF-Controls,
- Graphviz-API-Aufrufe,
- UserFlow-/Mockup-Typen,
- Hostmodell-Mutationen,
- persistente UserFlow-IDs,
- ActionArea-/ActionDefinition-Änderungen,
- finale UI-Designentscheidungen.

---

## 7. Offene Punkte nach Phase 0

| Punkt | Behandlung |
|---|---|
| Öffentliche Core-API | Erst in Phase 1 konkret festlegen und nach Step-Gate prüfen. |
| Exakte Typ-/Property-Namen | Erst in Phase 1 festlegen. |
| Graphviz-DOT-Erzeugung aus Core-Modell | Erst Phase 2. |
| Produktiver WPF-Renderer | Erst Phase 3. |
| Navigation Path Tree im UI | Erst Phase 4. |
| Belastungstest 100–150 Knoten | Erst Phase 5. |
| Bearbeitungsvertrag | Erst Phase 6. |
| UserFlow-Integration | Frühestens Phase 7 und ausschließlich hostseitig. |
| Andere Runtime-Architekturen | Nicht Teil der Phase-0-Freigabe. |

---

## 8. Phase-0-Abnahme

Phase 0 gilt aus technischer Sicht als abnahmebereit, wenn folgende Punkte im Repository vorhanden und geprüft sind:

- P0-001 bis P0-005 sind eingespielt, committed und gepusht,
- P0-002 wurde lokal erfolgreich ausgeführt,
- P0-003 wurde lokal sichtbar korrekt ausgeführt,
- diese P0-006-Dokumentation ist eingespielt, committed und gepusht,
- der Nutzer erteilt danach ausdrücklich das Step-Gate für Phase 1.

Ohne ausdrückliche Freigabe beginnt Phase 1 nicht.

---

## 9. Step-Gate-Formulierung

Die folgende Formulierung kann nach Prüfung für die Freigabe verwendet werden:

```text
Phase 0 ist abgenommen. Phase 1 starten.
```

Bis diese Freigabe erfolgt, bleibt die Arbeit nach P0-006 gestoppt.
