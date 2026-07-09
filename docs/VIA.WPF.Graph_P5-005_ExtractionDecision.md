# VIA.WPF.Graph – P5-005 Extraktions-/Übernahmeentscheidung

Status: angenommen  
Phase: 5 – Demo und Belastungstest  
Issue: P5-005 / #35  
Datum: 2026-07-08

## Grundlage

Diese Entscheidung basiert auf dem nach P5-004 gemeldeten Stand:

- Build und Tests wurden vom Host erfolgreich ausgeführt.
- P5-004 wurde gepusht.
- Die Demo-Testsets Small, Medium, Large, Groups und Error sind vorhanden.
- Large und wiederholter Rebuild wurden als technische Belastungsabnahme akzeptiert.
- Die beobachtete sehr kleine Darstellung bei Large im Fit-Modus ist für die Belastungsabnahme zulässig und kein Blocker.

## Entscheidung

Die allgemeine Graph-Library ist für die nächste Ausbaustufe freigegeben.

Die Phase-5-Demo bleibt ein technischer Nachweis und Testhost. Sie wird nicht als fachliche Hostschicht behandelt und nicht in Domänenlogik überführt.

Die Übernahmeentscheidung lautet:

1. `VIA.WPF.Graph.Core`, `VIA.WPF.Graph.Graphviz` und `VIA.WPF.Graph.Wpf` bleiben die allgemeine, hostneutrale Library-Basis.
2. `VIA.WPF.Graph.Demo` bleibt ein eigenständiger Test- und Belastungshost.
3. Es wird kein Code aus der Demo in den Core verschoben, solange kein neutraler Library-Vertrag dafür besteht.
4. Es wird kein UserFlow-, Mockup-, Screen-, Popup-, ActionArea- oder ActionDefinition-Typ in die Library aufgenommen.
5. Die nächste Phase ist Phase 6: allgemeiner Hostvertrag für Bearbeitung und Änderungsrequests.

## Akzeptierter Funktionsstand

Akzeptiert für die allgemeine Library-Basis:

- neutrales Graphmodell mit Nodes, Links und Gruppen,
- Containergruppen und überlappende Markierungsgruppen,
- Graphviz-Layoutadapter mit kontrollierter Fehlerbehandlung,
- eigenes WPF-Rendering ohne Graphviz-WPF-Abhängigkeit,
- Zoom, Pan, Fit, Auswahl, Fokus und Bereichsübersicht,
- Navigation Path Tree mit zyklensicherer Projektion,
- Tree-/Graph-Synchronisierung,
- Collapse/Expand für Containergruppen,
- Demo-Testsets Small, Medium, Large, Groups und Error,
- Large-/Rebuild-Stabilitätsprüfung als technische Phase-5-Abnahme.

## Nicht als Blocker gewertet

Folgende Punkte bleiben bewusst außerhalb der P5-005-Abnahme:

- Large ist im Gesamtgraph-Fit-Modus nicht präsentationsfähig lesbar.
- Für große Graphen ist die Bereichsübersicht, Fokus, Drilldown und Free Pan/Zoom die vorgesehene UX.
- Scrollbars sind kein Standardmodus. Sie können später optional als technischer Debugmodus geprüft werden.
- Eine schönere Präsentationsdemo ist ein UX-Thema, kein Architektur- oder Belastungsblocker.

## Konsequenz für Phase 6

Phase 6 darf starten, aber weiterhin ohne Domänenwissen.

Ziel von Phase 6 ist ein allgemeiner Hostvertrag für Requests, Capabilities, Ergebnisse und Fehlerdarstellung. Der Host bleibt Besitzer von Zustand, Persistenz, Undo/Redo und fachlicher Mutation.

Zulässig in Phase 6:

- neutrale Request-/Result-Typen,
- Capability-Angaben des Hosts,
- Demo-Host zur Simulation bearbeitbarer Navigation,
- Validierung und Fehlerdarstellung über neutrale Verträge.

Nicht zulässig in Phase 6:

- UserFlow-Adapter,
- UserFlow-Quellenanalyse,
- direkte Domänenmutation durch GraphCanvas oder Navigation Path Tree,
- neue Hostmodell-Annahmen,
- persistente UserFlow-spezifische View-State-Entscheidungen.

## Step-Gate

Phase 5 ist mit dieser Entscheidung abgeschlossen.

Freigabe für Phase 6 erfolgt nach Commit und Push dieser Entscheidung.
