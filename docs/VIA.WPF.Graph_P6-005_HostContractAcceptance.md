# VIA.WPF.Graph – P6-005 Phase-6-Hostvertrag-Abnahme

Status: angenommen  
Phase: 6 – Allgemeiner Hostvertrag für Bearbeitung  
Issue: P6-005 / #40  
Datum: 2026-07-08

## Grundlage

Diese Abnahme basiert auf dem gemeldeten Stand nach P6-004:

- P6-001 wurde gebaut, getestet und gepusht.
- P6-002 wurde gebaut, getestet und gepusht.
- P6-003 inklusive Fix wurde gebaut, getestet und gepusht.
- P6-004 wurde gebaut, getestet und gepusht.
- Der bearbeitbare Demo-Host wurde im Demo-Kontext geprüft.
- Die allgemeine Library bleibt weiterhin UserFlow-frei.

## Ziel der Phase 6

Phase 6 sollte einen allgemeinen Hostvertrag schaffen, über den Graph-Controls Änderungsanfragen stellen können, ohne Domänenwissen zu besitzen und ohne Hostmodelle direkt zu mutieren.

Der Host bleibt Eigentümer von:

- fachlichem Zustand,
- Persistenz,
- Undo/Redo,
- tatsächlichen Modellmutationen,
- Entscheidung über unterstützte Aktionen,
- Validierungs- und Fehlerlogik.

Die Graph-Library liefert ausschließlich neutrale Requests, neutrale Results, Capability-Beschreibung, Validierungsfeedback und WPF-Rendering.

## Abgenommener Umfang

### P6-001 – Request-/Result-Vertrag

Abgenommen:

- neutraler Request-Vertrag,
- neutrales Result-Modell,
- Statuswerte für Erfolg, Ablehnung, Nichtunterstützung und Fehler,
- hostseitige Request-Handler-Boundary.

### P6-002 – Host-Capabilities und Editierbarkeit

Abgenommen:

- Read-only- und Editable-Modus,
- hostseitige Capability-Angaben,
- Prüfung, ob Request-Arten grundsätzlich unterstützt werden,
- keine WPF-, Graphviz- oder UserFlow-Abhängigkeit im Core.

### P6-003 – Validierungs- und Fehlerfeedback

Abgenommen:

- neutrales Request-Feedback,
- Validierung von Requests gegen Host-Capabilities,
- kontrollierte Rückgabe von Rejected, NotSupported und Failed,
- Fehlerfeedback ohne UI- oder Domänenabhängigkeit.

### P6-004 – Bearbeitbarer Demo-Host

Abgenommen:

- Demo-Host besitzt ein eigenes Hostmodell,
- Demo-Host liefert unveränderliche GraphDocument-Snapshots,
- GraphCanvas bleibt Renderer und Request-Sender,
- Read-only lehnt Mutationsrequests ab,
- Editable erlaubt beispielhaftes Anlegen, Retargeting und Löschen,
- nach akzeptierter Demo-Mutation wird neu gelayoutet,
- RequestResult und Feedback sind in der Demo sichtbar.

## Architekturentscheidung

Phase 6 bestätigt die Trennung zwischen allgemeiner Graph-Library und Host:

1. `VIA.WPF.Graph.Core` enthält neutrale Graph-, Request-, Result-, Capability- und Validierungsverträge.
2. `VIA.WPF.Graph.Wpf` rendert und erzeugt neutrale Requests, mutiert aber kein Hostmodell direkt.
3. `VIA.WPF.Graph.Graphviz` bleibt ausschließlich Layoutadapter.
4. `VIA.WPF.Graph.Demo` darf beispielhafte Hostlogik enthalten, bleibt aber Test- und Demonstrationshost.
5. Fachliche Mutation bleibt ausschließlich Aufgabe des jeweiligen Hosts.
6. UserFlow-Integration beginnt erst nach dieser Abnahme und ausschließlich auf UserFlow-Seite.

## Nicht enthalten

Folgende Punkte sind bewusst nicht Bestandteil der Phase-6-Abnahme:

- UserFlow-Adapter,
- Analyse aktueller UserFlow-Quellen,
- direkte Bearbeitung von ActionDefinitions,
- Persistenz von UserFlow-Graphzustand,
- Undo/Redo-Integration in UserFlow,
- neue UserFlow-Properties oder JSON-Änderungen,
- manuelle Knotenpositionen.

## Offene bewusste Folgeentscheidungen für Phase 7+

Für die UserFlow-Integration müssen später gesondert geprüft und entschieden werden:

- aktuelle UserFlow-Solution-Struktur,
- stabile Identität von ActionDefinitions,
- JSON-, Clone- und Snapshot-Verhalten,
- read-only FlowView-Integration,
- Mapping von Screens, Popups, ActionAreas und ActionDefinitions,
- Umgang mit NavigateBack,
- Öffnen des bestehenden ActionArea-Editors aus Graph-/Tree-Auswahl,
- Undo/Redo und Persistenz für tatsächliche UserFlow-Mutationen.

## Step-Gate

Phase 6 ist mit dieser Abnahme abgeschlossen.

Die allgemeine `VIA.WPF.Graph`-Library ist für die nächste Stufe freigegeben:

- Phase 7 darf erst in einem Integrationskontext mit aktuellem `VIA.WPF.Graph`-Quellstand und aktuellem UserFlow-Export starten.
- Die Abhängigkeitsrichtung bleibt ausschließlich: UserFlow-Adapter / FlowView-Host → `VIA.WPF.Graph`.
- `VIA.WPF.Graph` bleibt vollständig frei von UserFlow-, Mockup-, Screen-, Popup-, ActionArea- und ActionDefinition-Typen.
