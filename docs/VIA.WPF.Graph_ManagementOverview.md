# VIA.WPF.Graph – Managementübersicht

## 1. Ziel
- Eine wiederverwendbare WPF-Komponente zur Visualisierung komplexer Navigations- und Prozessgraphen.
  - Knoten: Seiten, Dialoge, Bereiche oder andere fachliche Objekte.
  - Verbindungen: Wege, Aktionen, Rückwege, Popups und externe Übergänge.
  - Gruppen: Bereiche und fachliche Markierungen.
- Erster späterer Anwendungsfall: Darstellung der Navigation in VIA UserFlow.
- Grundsatz: Die Bibliothek bleibt allgemein und unabhängig von UserFlow.

## 2. Nutzen
- Navigation wird nicht nur als Liste, sondern als verständliche Übersicht sichtbar.
  - Linke Seite: mögliche Wege und Alternativen als Baum.
  - Rechte Seite: Gesamtzusammenhang, Bereiche und Querverbindungen als Graph.
- Schnellere Analyse großer Projekte.
  - Erkennen von Sackgassen, Zyklen, fehlenden Zielen und unübersichtlichen Bereichen.
  - Fokus auf einzelne Bereiche statt unlesbarer Gesamtansicht.
- Spätere Wiederverwendung auch außerhalb von UserFlow.

## 3. Architektur und Sicherheitsgrenzen
- Verbindliche Solution: `VIA.WPF.Graph/VIA.WPF.Graph.slnx`.
  - Keine `.sln` ergänzen oder migrieren.
  - Dokumentation liegt im Repository-Root unter `docs/`; die README liegt im Repository-Root.
  - Die späteren Projektordner liegen direkt im Solution-Ordner `VIA.WPF.Graph/`.
- Zielprojekte und gleichlautende Assembly-/Root-Namespaces.
  - `VIA.WPF.Graph.Core`: neutrales Graphmodell und Regeln.
  - `VIA.WPF.Graph.Graphviz`: automatische Anordnung und Kantenführung.
  - `VIA.WPF.Graph.Wpf`: Darstellung, Zoom, Auswahl und Interaktion.
  - `VIA.WPF.Graph.Demo`: unabhängige Testanwendung und einziger Composition Root.
- Verbindliche Referenzen.
  - Core → keine Projekt-, WPF-, Graphviz-, UserFlow- oder Host-Abhängigkeit.
  - Graphviz → Core.
  - Wpf → Core.
  - Demo → Core, Graphviz, Wpf.
  - Graphviz ↔ Wpf ist verboten.
  - Alle Graph-Projekte → UserFlow oder Mockup sind verboten.
- Der WPF-Renderer erhält keine Graphviz-Abhängigkeit. Die Demo setzt beide Schichten ausschließlich als Composition Root zusammen.
- UserFlow verwendet die Graph-Bibliothek erst später über einen Adapter; die Graph-UI ändert keine UserFlow-Daten direkt.
- Fachliche Daten bleiben immer beim jeweiligen Host-System.

## 4. Zielumfang
- Graphdarstellung.
  - Karten für Knoten, verschiedene Verbindungstypen, Popup-Darstellung.
  - Haupt-, Neben- und Rückwege.
  - Parallelverbindungen und Selbstverbindungen.
- Navigation und Bedienung.
  - Zoom, Pan, Suche, Fokus, Auswahl und Mehrfachauswahl.
  - Baum- und Graphansicht sind miteinander synchronisiert.
- Gruppen.
  - Bereiche können ein- und ausgeklappt werden.
  - Überlappende fachliche Markierungen bleiben zusätzlich möglich.
- Qualität.
  - Robuste Fehlerdarstellung statt Absturz.
  - Belastungstest bis 100–150 Knoten.

## 5. Vorgehen in Etappen
- Phase 0: Architektur, Projektstruktur und technische Machbarkeit absichern.
  - Graphviz-/Runtime-/Deployment-Prüfung.
  - Keine Übernahme alter Demo-Implementierungen.
- Phasen 1–2: Neutrales Modell und automatische Graphanordnung erstellen.
- Phasen 3–5: WPF-Oberfläche, Baum-/Graph-Hybridansicht und Belastungstest erstellen.
- Phase 6: Allgemeine Schnittstelle für spätere Bearbeitungsanfragen definieren.
- Phasen 7–9: Erst danach schrittweise UserFlow anbinden.
  - Zuerst nur lesen und visualisieren.
  - Danach bestehende Navigation bearbeiten.
  - Zuletzt neue Navigation erstellen.
- Phase 10: Optionale Erweiterungen, etwa Export, Analyse und manuelle Layoutanpassung.

## 6. Qualitäts- und Freigaberegeln
- Jede Phase endet mit Build, Funktionsprüfung und ausdrücklicher Freigabe.
- Kein Big-Bang-Umbau von UserFlow.
- Änderungen werden zuerst allgemein in der Demo geprüft.
- Persistente Änderungen müssen im Host-Modell landen und nach Laden, Wechseln und Undo/Redo identisch bleiben.

## 7. Wesentliche Risiken und Absicherung
- Native Graphviz-Runtime.
  - Wird vor dem Kernaufbau auf dem Zielrechner validiert.
- Große oder zyklische Graphen.
  - Bereichs- und Fokusansichten sind Standard; der Vollgraph ist Diagnoseansicht.
- Bearbeitung von UserFlow-Navigation.
  - Erst nach stabiler Identität, Persistenz, Editor- und Undo/Redo-Prüfung.
- Vermischung von Bibliothek und UserFlow.
  - Durch strikt getrennte Projekte und Adaptergrenze verhindert.

## 8. Aktueller Status
- Masterplan und GitHub-Backlog sind angelegt.
- Eigenständiges Repository und die verbindliche leere `VIA.WPF.Graph.slnx` existieren.
- P0-001 dokumentiert Solution-Format, Repository-Konvention, Zielprojekte, Root-Namespaces und Isolationsgrenzen.
- P0-001 erzeugt keine Projekt-, NuGet-, C#- oder XAML-Dateien.
- Nächster Schritt nach ausdrücklichem Step-Gate: technische Phase-0-Verifikation von Rubjerg.Graphviz, Runtime, Deployment und neutralem Datenmodellrahmen.
