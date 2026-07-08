# VIA.WPF.Graph – P0-004 Minimaler neutraler Vertrag, Sizing und View-State

**Status:** Entscheidungsvorlage / Phase-0-Arbeitsergebnis  
**Issue:** P0-004 – Minimalen neutralen Vertrag, Sizing und View-State entscheiden  
**Phase:** 0 – Baseline, Entscheidungen und technische Verifikation  
**Gültigkeit:** verbindlich für den Start von Phase 1, ohne öffentliche API-Festlegung  

## 1. Ziel von P0-004

P0-004 legt den minimalen fachlichen Rahmen fest, den `VIA.WPF.Graph.Core`, `VIA.WPF.Graph.Graphviz`, `VIA.WPF.Graph.Wpf` und `VIA.WPF.Graph.Demo` ab Phase 1 verwenden dürfen.

Dieses Dokument ist eine aus dem Masterplan abgeleitete Entscheidung für die konkrete Umsetzung. Der Masterplan bleibt die fachliche und architektonische Single Source of Truth.

## 2. Nicht-Ziele

P0-004 führt keine Implementierung ein.

Nicht enthalten sind:

- C#-Modelle, Records, Klassen, Interfaces oder Enums,
- XAML-Controls oder Templates,
- neue NuGet-Pakete,
- UserFlow-, Mockup-, Screen-, Popup-, ActionArea- oder ActionDefinition-Typen,
- Graphbearbeitung,
- Persistenzformat,
- Undo/Redo,
- manuelle Knotenpositionen,
- endgültige öffentliche API-Signaturen.

Exakte Typnamen, Namespaces, Validierungsdetails und öffentliche API werden erst in Phase 1 festgelegt.

## 3. Minimaler neutraler Graphvertrag

### 3.1 Grundsatz

Der neutrale Vertrag beschreibt einen Graph-Snapshot. Er ist keine zweite fachliche Wahrheit und keine Host-Collection. Der Host erzeugt den Snapshot aus seinem eigenen Modell und bleibt Besitzer von Domänenzustand, Persistenz und Mutationen.

Alle IDs sind hoststabile, opaque IDs innerhalb eines Graphdokuments. Die allgemeine Library interpretiert keine Host-ID-Semantik.

### 3.2 Graphdokument

Ein Graphdokument benötigt mindestens:

| Bestandteil | Zweck |
|---|---|
| Dokument-ID oder Snapshot-ID | technische Identifikation eines Graph-Snapshots |
| Knoten | darzustellende Einheiten |
| Links | gerichtete oder ungerichtete Verbindungen |
| Gruppen | Container- und Markierungsgruppen |
| optionale Metadaten | hosteigene Zusatzdaten, vom Core nicht fachlich interpretiert |

Das Graphdokument enthält keine WPF-, Graphviz- oder UserFlow-Objekte.

### 3.3 Knoten

Ein Knoten benötigt mindestens:

| Bestandteil | Entscheidung |
|---|---|
| `Id` | stabile, opaque Knoten-ID |
| `Title` | sichtbarer Haupttitel |
| `Description` | optionale Kurzbeschreibung |
| `Kind` | neutrale Kategorie, z. B. Standard, Popup, Bereichs-Stellvertreter, Extern, Referenz |
| `SizeProfile` | feste Größenklasse vor Layout |
| `VisualStyleKey` | neutrale Template-Auswahl, keine Brush-/WPF-Logik |
| `GroupMemberships` | Zugehörigkeit zu null, einer oder mehreren Gruppen |
| `Metadata` | hosteigene Zusatzdaten, nicht vom Core interpretiert |

Knoten speichern keine WPF-Brushes, Controls, Screens, Popups oder ActionAreas.

### 3.4 Links

Ein Link benötigt mindestens:

| Bestandteil | Entscheidung |
|---|---|
| `Id` | stabile, opaque Link-ID |
| `SourceNodeId` | Quellknoten |
| `TargetNodeId` | Zielknoten |
| `Direction` | gerichtet oder ungerichtet |
| `Kind` | fachliche Link-Kategorie |
| `Label` | sichtbarer Link-/Action-Text |
| `LineStyle` | neutraler Linienstil, keine WPF-Pen-Definition |
| `Weight/Priority` | Layout- oder Darstellungspriorität |
| `IsLayoutConstraint` | steuert, ob der Link den Hauptfluss beeinflusst |
| `Metadata` | hosteigene Zusatzdaten, nicht vom Core interpretiert |

Parallelkanten bleiben eigenständige Links mit eigener ID. Links werden niemals nur anhand von Quelle und Ziel zusammengeführt.

Minimal vorgesehene Link-Kategorien:

```text
Primary
Secondary
Back
Cancel
PopupOpen
PopupClose
External
Reference
Diagnostic
```

`Cancel` und `PopupClose` sind allgemeine neutrale Möglichkeiten. Ein Host darf sie nur verwenden, wenn er eine fachlich eindeutige Semantik besitzt.

### 3.5 Gruppen

Gruppen werden in zwei Arten getrennt:

| Art | Entscheidung |
|---|---|
| Container-Gruppe | disjunkt oder sauber hierarchisch; darf als Cluster, Bereich oder kollabierbarer Container dienen |
| Markierungs-Gruppe | darf überlappen; dient Filter, Auswahl oder Hervorhebung; kein Collapse/Expand |

Eine Gruppe benötigt mindestens:

| Bestandteil | Entscheidung |
|---|---|
| `Id` | stabile, opaque Gruppen-ID |
| `Title` | sichtbarer Gruppenname |
| `Kind` | Container oder Markierung |
| `ParentGroupId` | optional, nur für hierarchische Container-Gruppen |
| `VisualStyleKey` | neutrale Darstellungswahl |
| `Metadata` | hosteigene Zusatzdaten |

Die Mitgliedschaft liegt primär am Knoten über `GroupMemberships`. Dadurch entstehen keine doppelten synchron zu haltenden Mitgliedslisten.

### 3.6 Projektionen

Phase 1 darf den neutralen Vertrag auf folgende Projektionsarten vorbereiten:

```text
FullGraph
AreaOverview
AreaGraph
FocusGraph
NavigationPathTree
GroupFocus
DiagnosticGraph
```

Die konkrete Projektions-API wird in Phase 1 entschieden. P0-004 legt nur fest, dass diese Sichten fachlich zulässig sind.

## 4. Sizing-Vertrag

### 4.1 Grundsatz

Graphviz benötigt Knotengrößen vor dem Layout. Daher verwendet die Library zunächst feste Größenprofile in WPF-DIPs. Dynamische WPF-Messung darf erst nach einem gesonderten Step-Gate eingeführt werden.

Der Graphviz-Adapter ist die einzige Stelle, die zwischen WPF-DIPs, DOT-Inches und Graphviz-Points umrechnet.

### 4.2 Initiale Größenprofile

Die folgenden Profile gelten als Phase-1-Ausgangswerte:

| Profil | Breite | Höhe | Zweck |
|---|---:|---:|---|
| `Compact` | 120 DIP | 48 DIP | Gesamtgraph / Diagnose |
| `Standard` | 180 DIP | 72 DIP | normale Knotenkarte |
| `Detail` | 240 DIP | 120 DIP | Fokus / Detaildarstellung |
| `Popup` | 160 DIP | 56 DIP | Popup-/Overlay-Knoten |
| `Stub` | 140 DIP | 48 DIP | externe oder fehlerhafte Ziele |
| `GroupProxy` | 200 DIP | 80 DIP | Bereichs-Stellvertreter in Übersichten |

Diese Werte sind keine endgültige UI-Gestaltung. Sie sind der feste technische Vertrag für reproduzierbares Layout in den ersten Phasen.

### 4.3 Konsequenzen

- WPF-Templates müssen in Phase 3 innerhalb des gewählten Profils rendern.
- Texte werden zunächst gekürzt oder umgebrochen, statt die Layoutgröße stillschweigend zu ändern.
- Graphviz erhält die Größen aus dem gleichen Profil, das WPF später rendert.
- Nachträgliche dynamische Größenmessung benötigt einen kontrollierten Layout-Rebuild und eine eigene Freigabe.

## 5. View-State-Entscheidung

### 5.1 Grundsatz

Persistenter View-State gehört dem Host. Die allgemeine Library darf neutrale State-Datenformen definieren und binden, besitzt aber nicht dauerhaft den Zustand.

Das WPF-Control darf transienten UI-Zustand für laufende Interaktionen halten, zum Beispiel Drag-Startpunkt oder Hover. Dauerhafte Auswahl, Zoom, Pan, aktive Ansicht und Collapse-Zustand bleiben im Host-ViewModel.

### 5.2 Allgemein zulässiger neutraler View-State

Folgende State-Bereiche dürfen ab Phase 1/3 als neutraler Vertrag vorbereitet werden:

| State | Entscheidung |
|---|---|
| aktive Projektion / Ansicht | allgemein neutral |
| ausgewählte Knoten-IDs | allgemein neutral |
| ausgewählte Link-IDs | allgemein neutral |
| ausgewählte Gruppen-IDs | allgemein neutral |
| Fokus-Knoten oder Fokus-Gruppe | allgemein neutral |
| Suchtext | allgemein neutral, Persistenz optional hostseitig |
| Zoom | allgemein neutral |
| Pan / Viewport-Origin | allgemein neutral |
| Tree-Expand-Zustand | allgemein neutral, Besitz hostseitig |
| kollabierte Container-Gruppen | allgemein neutral, nur Container-Gruppen |
| Layoutoptionen | allgemein neutral, z. B. Richtung und Routingstil |

### 5.3 Host-eigener Zustand

Folgende Punkte bleiben vollständig beim Host:

- Persistenzort und Persistenzformat,
- Undo/Redo,
- fachliche Mutationen,
- Domänenauswahl außerhalb des Graphs,
- Mapping zwischen Hostmodell und Graph-IDs,
- UserFlow-spezifische Auswahl wie aktueller Screen oder aktuelles Popup,
- Berechtigungen und Editierbarkeit,
- manuelle Knotenpositionen bis zu gesonderter Freigabe.

### 5.4 Kein versteckter Control-Besitz

Ein Wechsel der View, ein Rebuild oder ein neuer Layoutlauf darf Selektion, Zoom, Pan und Collapse-Zustand nicht nur deshalb verlieren, weil ein WPF-Control neu erzeugt wurde. Der Host muss den dauerhaften Zustand erneut binden können.

## 6. Validierungsleitplanken für Phase 1

Phase 1 soll mindestens folgende Validierungen ermöglichen:

- eindeutige Knoten-IDs,
- eindeutige Link-IDs,
- eindeutige Gruppen-IDs,
- vorhandene Linkziele oder klarer Fehlerzustand,
- parallele Links bleiben erhalten,
- Selbstkanten sind zulässig, aber erkennbar,
- Container-Gruppen sind disjunkt oder sauber hierarchisch,
- Markierungsgruppen dürfen überlappen,
- Collapse/Expand nur für Container-Gruppen,
- keine WPF-/Graphviz-/UserFlow-Abhängigkeit im Core.

## 7. Offene Punkte für spätere Phasen

Diese Punkte werden ausdrücklich nicht in P0-004 entschieden:

- konkrete C#-Typnamen und API-Signaturen,
- konkrete Serialisierung,
- konkrete WPF-Templates,
- Graphviz-DOT-Erzeugungsdetails,
- Layout-Caching,
- Messenger- oder Command-Vertrag,
- Host-Requests für Bearbeitung,
- UserFlow-Adapter,
- ActionDefinition-ID,
- Popup-Schließen-Semantik,
- NavigateBack-Zielableitung,
- manuelle Positionen.

## 8. Prüfpunkte zur Abnahme von P0-004

P0-004 gilt als erledigt, wenn:

- der minimale neutrale Vertrag dokumentiert ist,
- feste initiale Größenprofile dokumentiert sind,
- hostbesessener View-State von transientem Control-State getrennt ist,
- spätere Phasen nicht vorweggenommen werden,
- keine Implementierungsdateien angelegt oder verändert wurden,
- keine neuen Paket- oder Projektabhängigkeiten entstanden sind.
