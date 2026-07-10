# VIA.WPF.Graph – Masterplan für allgemeine Graph-Visualisierung und spätere UserFlow-Integration

**Status:** geprüfter Architektur- und Entwicklungsplan, fortgeschrieben bis Skia-R7-Workspace-Entscheidungsstand  
**Ziel:** Allgemein nutzbare Graph-Visualisierung für WPF in der Solution `VIA.WPF.Graph`, zunächst als unabhängiges Test-/Prototyp-Projekt, danach schrittweise Integration in VIA UserFlow.  
**Layout-Engine:** `Rubjerg.Graphviz` **3.0.5** / Graphviz `dot`.  
**Renderbasis:** WPF-Host mit `SkiaGraphSurface` auf `SkiaSharp.Views.WPF` für die aktive Product-Demo-Graphfläche. Die frühere WPF-`GraphCanvas` ist nur noch Legacy-Bestand und wird im nächsten freigegebenen Code-Patch entfernt, sofern keine kompilierten Referenzen verbleiben.  
**MVVM-Standard:** überall CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `[AsyncRelayCommand]`, bei Bedarf `ObservableRecipient` und `WeakReferenceMessenger`).  
**Arbeitsregel:** Jede Phase endet mit lokaler Build-/Funktionsprüfung und explizitem Step-Gate.

**Revision 4 – Vollständigkeitsprüfung, Übergabe und strikte Host-Isolation:** Dieser Stand enthält zusätzlich die bisher besprochenen Punkte zu Tree/Graph-Hybridansicht, Karten-Badges für Rückwege, Popup-Darstellung, allgemeinen Knoten/Links/Gruppen, überlappenden Gruppen, Collapse/Expand, Bearbeitung über ActionAreas, CommunityToolkit.Mvvm, Persistenz, Altprojekt-Migration, der aktuellen Einschränkung des ActionArea-Editors sowie einen vollständigen Übergabeauftrag für einen neuen Chat. Die frühere Graphviz-Demo ist ausdrücklich keine technische Baseline.

**Revision 7 – Skia-R7-Fortschreibung:** Nach R7-001/R7-002 ist `SkiaGraphSurface` die aktive Renderfläche der Product Demo. Skia übernimmt Rendering, Hit-Testing, Pan/Zoom, Selection-Overlays und Präsentationsdarstellung innerhalb von `VIA.WPF.Graph.Wpf`; Graphviz bleibt ausschließlich für Layout und Routing zuständig. Die alte WPF-`GraphCanvas` wird nicht weiter optisch ausgebaut und darf erst nach einem eigenen API-/Kompatibilitäts-Step-Gate entfernt werden. Vor der UserFlow-Integration werden Scope-Policy, Link-Routing, Viewport/Scrollbars und Interaktionsrequests auf Skia-Basis gehärtet.

**Revision 8 – Größen-/Wrap-Vertrag und Layout-Tool-Grenze:** Der Größenvertrag wird um eine spätere `GraphCardMeasurePolicy` ergänzt. Titel, Subtitle, Typzeile und optionale Host-Metadaten müssen vor dem Layout mit definierten Wrap-/Ellipsis-Regeln in eine finale `GraphSize` überführt werden. `GraphLayoutNode.Bounds` werden nicht heimlich nach dem Graphviz-Layout vergrößert, weil sonst Kanten, Gruppen, Hit-Tests und Scroll-Extents nicht mehr zum Layout passen. Graphviz/`dot` bleibt die Standard-Layoutengine. Alternative Layoutadapter wie MSAGL dürfen später nur nach Step-Gate evaluiert werden; reine Graphalgorithmus-Bibliotheken wie QuikGraph ersetzen kein visuelles Layout.

**Revision 9 – GraphWorkspace als Host-Onboarding-Schicht:** Die Library soll später nicht nur einzelne Primitive wie Tree und Skia-Fläche bereitstellen, sondern eine wiederverwendbare Workspace-/Navigator-Schicht für Standard-Hosts. Hosts wie UserFlow sollen nicht die komplette Product-Demo-Logik für Tree/Graph-Koordination, Scope-Bildung, ScrollIntoView, Fit/Center, Selection-Sync, ViewMode-Wechsel und Request-Dispatch nachbauen müssen. `GraphWorkspace` bleibt trotzdem hostneutral: Der Host liefert `GraphDocument`, hostbesessenen `GraphViewState`, Capabilities, Request-Handler und optionale Policies; die Library orchestriert die neutrale Arbeitsansicht, mutiert aber kein Hostmodell.

**Revision 10 – GraphWorkspace-Zielentscheidungen:** `GraphWorkspace` wird als fertiges hostneutrales WPF-Control mit integriertem Navigation Tree und `SkiaGraphSurface` geplant. Der Host soll nicht `VisibleDocument`, `VisibleLayout`, Tree/Graph-Sync, ScrollIntoView, Focus, Fit/Center oder Scope-Bildung selbst nachbauen müssen. Standard für Gruppenklicks ist `Group Compact`; vollständige Gruppen und Gesamtgraphen sind explizite Nutzeraktionen. Die Library erzeugt sichtbare Teildokumente und Layouts aus dem vollständigen `GraphDocument`. Der Inspector bleibt hostseitig. Layout-/Routing-Umschaltung wird vorgesehen, aber erst nach ScopePolicy-Stabilisierung umgesetzt.

---

## 1. Ausgangspunkt und Zielbild

UserFlow soll Navigation nicht nur als Sammlung von Screens und ActionAreas anzeigen, sondern auf zwei sich ergänzenden Ebenen verständlich machen:

```text
┌───────────────────────────────┬──────────────────────────────────────────────┐
│ Navigation Path Tree          │ Graph / Bereichsübersicht                   │
│                               │                                              │
│ Einstieg                      │  [Login] ───► [Registrieren]                │
│ └─ Login                      │      └────► [MainView]                      │
│    ├─ Registrieren            │                                              │
│    │  └─ ↩ Login              │  Gruppen/Bereiche, Querverbindungen,         │
│    └─ MainView                │  Popups und Bereichswechsel                  │
│       └─ ⛔ kein Back          │                                              │
└───────────────────────────────┴──────────────────────────────────────────────┘
```

Der linke Bereich beantwortet: **„Welche möglichen Wege und Alternativen gibt es ab hier?“**  
Der rechte Bereich beantwortet: **„Wie hängt die Navigation insgesamt zusammen?“**

Die Lösung wird so aufgebaut, dass UserFlow nur ein erster Host ist. Andere Anwendungen sollen neutrale Knoten, Links und Gruppen liefern können, ohne UserFlow-Typen oder -Collections zu kennen.

---

## 2. Verbindliche Architekturgrundsätze

1. **Keine fachliche Kopie im Renderer.**  
   Der Graph-Renderer erhält eine Projektion beziehungsweise einen unveränderlichen Render-Snapshot. Die fachliche Wahrheit bleibt im jeweiligen Host-Model/ViewModel.

2. **Keine Mutation durch Graph-UI.**  
   Graph-Surface und Tree senden nur klar typisierte Interaktions- beziehungsweise Änderungsanfragen. Ausschließlich der Host verarbeitet diese und ändert seine Modelle/Collections.

3. **Single Source of Truth.**  
   Bei UserFlow bleiben `Project.Screens`, `Project.Popups`, `ActionArea.Actions` und deren ActionDefinitions die alleinigen Quellen für Navigation. Es entstehen keine synchron gehaltenen Navigationscollections.

4. **Lesbarkeit vor Vollständigkeit.**  
   Ein Gesamtgraph mit 100–150 Screens ist Diagnoseansicht, nicht Standardarbeitsansicht. Die Standardansicht kombiniert Tree, Bereichsfluss und fokussierte Graphdarstellung.

5. **Graphviz nur als Layout-Engine.**  
   Graphviz bestimmt Knotenpositionen, Cluster-Bounds und Kantenverläufe. WPF/Skia rendert Karten, Labels, Badges, Auswahl, Zoom, Pan, Hit-Testing und Interaktionen vollständig selbst.

6. **WPF-spezifische Teile bleiben getrennt.**  
   Das neutrale Graphmodell darf keine WPF-, SkiaSharp-, Graphviz- oder UserFlow-Abhängigkeit enthalten. SkiaSharp bleibt ausschließlich eine WPF-Renderer-Abhängigkeit.

7. **CommunityToolkit.Mvvm als Standard.**  
   Neue ViewModels, UI-Zustände und Commands verwenden ausschließlich Toolkit-Muster. Klassische handgeschriebene `INotifyPropertyChanged`-Implementierungen werden nicht neu eingeführt.

8. **Keine DependencyProperty-Shadowing.**  
   Eine WPF-DependencyProperty wird nur in einer Klasse registriert. Abgeleitete Controls verwenden sie, registrieren sie aber nicht erneut.

9. **Persistente UI-Zustände gehören dem Host.**  
   Expand-/Collapse-Zustände, aktive Ansicht, Zoom, Pan, Filter und spätere manuelle Positionen liegen im Host-ViewModel beziehungsweise im vom Host persistierten State – nie nur in einem visuellen Control.

10. **Schrittweise Umsetzung.**  
    Kein Big-Bang-Umbau in UserFlow. Erst allgemeiner Prototyp, dann read-only UserFlow-Integration, danach Bearbeitung und Persistenz.

### 2.1 Vollständigkeitscheck der besprochenen Zielpunkte

| Besprochener Punkt | Im Plan verbindlich abgedeckt durch |
|---|---|
| Allgemeines Tool statt reiner UserFlow-Speziallösung | neutrale Projekte `VIA.WPF.Graph.Core`, `.Graphviz`, `.Wpf` und `.Demo` |
| Knoten mit Titel, Defaultgröße und Farbe/Style | neutrale Knotenbeschreibung plus Style-Key und WPF-Templates |
| Links mit Quelle, Ziel, Richtung, Typ und Linienstil | Linkmodell, semantische Link-Kategorien und Renderingregeln |
| beliebige, auch überlappende Gruppen | Trennung in Container- und Markierungsgruppen |
| Anordnen, Zoomen und Gruppen sichtbar machen | SkiaGraphSurface, Graphviz-Layout, Bereichs- und Gruppenprojektionen; legacy GraphCanvas nur noch als Altbestand |
| Auswahl einzelner/mehrerer Gruppen | Gruppenselektion, Mehrfachauswahl, Fokus und Filter |
| Collapse/Expand | ausschließlich für disjunkte/hierarchische Container-Gruppen |
| Tree links, Gesamtgraph rechts | Hybridansicht mit synchroner Auswahl |
| Host muss Tree/Graph-Koordination nicht selbst nachbauen | `GraphWorkspace` als wiederverwendbare Onboarding-Schicht über Tree, Skia-Fläche, Scope-Policy, View-State-Bindings und Requests |
| Oder-Verzweigungen | Tree-Siblings als Alternativen; kein doppeltes Rendern von Rückzielen |
| Back-/Sondertyp-Hinweise in der Karte | neutrale Typ-/Linkdarstellung ohne UserFlow-Annahmen; bei UserFlow nur mit fachlich ehrlicher Zielangabe |
| Popups | kleinere Overlay-Karten, PopupOpen-Kanten und Tree-Knoten |
| bestehende Navigation ändern / neue anlegen | getrennte Phasen über Host-Requests und ActionArea/ActionDefinition |
| Wiederverwendbarkeit in anderen Anwendungen | hostneutrale Modelle, Capabilities und Adaptergrenze |
| CommunityToolkit.Mvvm / WeakReferenceMessenger | verbindliche Toolkit-Regeln und klar eingegrenzte Messenger-Nutzung |
| UserFlow-SSOT, Persistenz und Undo/Redo | Host-Commands, Snapshot vor Mutation, Rebuild aus Originaldaten |

---

## 3. Aktueller UserFlow-Bestand als Integrationsgrundlage

Die aktuelle Lösung bietet bereits die wesentlichen fachlichen Anker:

| Bestehender Bestandteil | Relevanz für die Graphlösung |
|---|---|
| `Screen.Id`, `Screen.Name`, `Screen.Descr`, `Screen.GroupName`, `Screen.IsHomeScreen` | Screen-Knoten, Gruppierung, Root des Navigation Path Tree |
| `ScreenPopup.Id`, `Name`, `Title`, `Description`, `GroupName` | Popup-Knoten und Popup-Gruppen |
| `ActionArea` als `DesignControl` mit eigener Control-ID | eindeutiger Ursprung einer Navigationsaktion |
| `ActionArea.Actions` | Quelle aller von einer ActionArea ausgelösten Actions |
| `ActionDefinition.Type`, `Trigger`, `TargetScreenId`, `PopupId`, `Parameters` | gerichtete Links und deren Semantik |
| `Project.Screens`, `Project.Popups` | alleinige Collection-Quellen für die UserFlow-Projektion |
| `MockupViewModel.CurrentScreen`, `CurrentPopup`, `HomeScreen`, Preview-Navigation | Selektion, Fokus und Navigation zwischen Graph und bestehender UI |
| Gruppierte Screen-/Popup-Views im ViewModel | vorhandene Basis für Bereiche und Expand-Zustände |
| `FlowView` | vorhandener, aktuell noch leerer Integrationspunkt |
| Snapshot-/Undo-Redo-Infrastruktur | spätere sichere Bearbeitung von Actions und Navigation |

### 3.1 Wesentliche fachliche Besonderheiten

- Ein `ActionArea` kann mehrere Actions enthalten; der Trigger ist Teil der semantischen Identität einer dargestellten Kante.
- `Navigate` besitzt ein explizites Ziel über `TargetScreenId`.
- `ShowPopup` besitzt ein explizites Ziel über `PopupId`.
- `NavigateHome` verweist implizit auf den aktuellen Home-Screen.
- `NavigateBack` besitzt derzeit **kein statisch gespeichertes Ziel**. Eine Darstellung wie „Zurück zu Login“ kann deshalb nur aus dem aktuell gewählten Pfad abgeleitet werden. Sie darf nicht als fester globaler Zielscreen behauptet werden.
- `OpenURL` und `OpenFile` sind keine internen Navigationsziele; sie werden als externe/terminale Aktion dargestellt.
- Ein `ActionDefinition` besitzt derzeit keine eigene persistierte ID. Für zuverlässige Link-Auswahl und Bearbeitung ist das ein kritischer Entscheidungspunkt.

### 3.2 Aktuelle Einschränkungen des ActionArea-Editors

Der aktuelle Editor bildet eine ActionArea als feste Liste von Trigger-Zeilen ab. Pro `ActionTrigger` gibt es dabei genau eine `ActionRow`; beim Speichern wird `ActionArea.Actions` aus diesen Zeilen neu aufgebaut. Für die erste UserFlow-Bearbeitungsstufe gelten deshalb folgende Regeln:

- V1 behandelt maximal **eine** Action pro Kombination aus ActionArea und Trigger als unterstützten Fall.
- Werden persistente Action-IDs eingeführt, muss der Editor die vorhandene ID in der zugehörigen `ActionRow` halten und beim Speichern wieder übernehmen. Andernfalls würden Link-Identitäten bei jeder Bearbeitung verloren gehen.
- Mehrere parallele Kanten zwischen denselben Screen-Knoten bleiben im allgemeinen Graphmodell erlaubt; UserFlow unterstützt sie erst, wenn der ActionArea-Editor und das Datenmodell dies bewusst zulassen.
- `OpenURL` ist im ActionRow-Modell vorhanden, aber nicht in der aktuellen Auswahl der Editor-ActionTypes enthalten. Der Adapter darf vorhandene URL-Links anzeigen, sie jedoch nicht als „voll bearbeitbar“ deklarieren, bevor der Editor dafür freigegeben und geprüft wurde.
- Der bestehende Editor wird aus der Anwendung bereits über einen hostseitigen `ActionAreaEditMessage`-/Command-Weg geöffnet. Die Graphintegration soll diesen Weg nutzen oder gleichwertig über den Host kapseln, niemals den Editor direkt aus dem allgemeinen Graph-Control aufrufen.

### 3.3 Konsequenz: stabile Link-Identität

Für eine spätere Bearbeitung im Graph muss jede Kante zuverlässig auf genau eine ActionDefinition zurückgeführt werden können.

**Empfehlung:** eine persistente `ActionDefinition.Id` ergänzen, bevor Graphbearbeitung aktiviert wird. Bis zur expliziten Freigabe darf keine neue Property eingeführt werden.

Übergangsweise kann ein Link intern über folgende Kombination adressiert werden:

```text
OwnerKind + OwnerId + ActionAreaControlId + Trigger + ActionsIndex
```

Diese Kombination ist jedoch nicht robust gegen Umordnen oder mehrere gleichartige Actions. Sie ist deshalb nur für eine read-only Übergangsphase akzeptabel.

---

## 4. Zielprojektstruktur

Die Namen sind Zielnamen. Die bereits vorhandene `VIA.WPF.Graph/VIA.WPF.Graph.slnx` ist die verbindliche eigenständige Test-/Prototyp-Solution. Sie wird weder durch eine `.sln` ergänzt noch migriert. Phase 0 legt Repository-Ort, Namespaces und die spätere Übernahme in eine allgemeine Bibliotheksstruktur verbindlich fest.

```text
VIA.WPF.Graph.Core
  Neutrales Modell, Projektionen, Selektion, Layoutvertrag,
  Gruppenregeln, Commands/Requests, View-State-Daten

VIA.WPF.Graph.Graphviz
  Rubjerg.Graphviz 3.0.5-Adapter,
  DOT-Erzeugung, Layoutauswertung, Kanten-/Cluster-Geometrie

VIA.WPF.Graph.Wpf
  SkiaGraphSurface als primärer Renderer, GraphWorkspace als Standard-Onboarding-Control,
  Zoom/Pan, Hit-Testing, Knoten-/Kanten-/Gruppenrendering,
  Tree-/Pfadansicht, Auswahl, Suche, Fokus, Interaktions-Requests;
  GraphCanvas wird nach R7-006 im folgenden Code-Patch entfernt

VIA.WPF.Graph.Demo
  Eigenständige WPF-Testanwendung mit Testdaten,
  Vergleichs- und Belastungsszenarien

später ausschließlich auf Host-Seite:
UserFlow-Graph-Adapter
  liegt außerhalb der VIA.WPF.Graph-Solution und referenziert die allgemeine Library;
  übersetzt Screens, Popups, ActionAreas und ActionDefinitions auf das neutrale Modell.
  Der konkrete Projektname wird erst in der UserFlow-Integrationsphase entschieden.
```

### 4.1 Abhängigkeitsrichtung

```text
VIA.WPF.Graph.Core
        ▲
        ├──────── VIA.WPF.Graph.Graphviz
        └──────── VIA.WPF.Graph.Wpf
                         ▲
                         └──────── VIA.WPF.Graph.Demo

später, ausschließlich außerhalb der VIA.WPF.Graph-Solution:
UserFlow-Adapter / FlowView ─────► VIA.WPF.Graph.Core, .Graphviz und .Wpf
```

Verboten:

```text
VIA.WPF.Graph.Core -> WPF
VIA.WPF.Graph.Core -> SkiaSharp
VIA.WPF.Graph.Core -> Rubjerg.Graphviz
VIA.WPF.Graph.* -> Mockup / UserFlow
Graph-Renderer -> UserFlow-Collections direkt
```

### 4.2 Verbindliche P0-001-Entscheidung: Repository, Projekte und Isolationsgrenzen

**Repository- und Ordnerkonvention**

```text
Repository-Root/
  README.md
  docs/
    VIA.WPF.Graph_Masterplan.md
    VIA.WPF.Graph_ManagementOverview.md
  VIA.WPF.Graph/
    .gitignore
    VIA.WPF.Graph.slnx
    VIA.WPF.Graph.Core/       (späteres Projektverzeichnis)
    VIA.WPF.Graph.Graphviz/   (späteres Projektverzeichnis)
    VIA.WPF.Graph.Wpf/        (späteres Projektverzeichnis)
    VIA.WPF.Graph.Demo/       (späteres Projektverzeichnis)
```

- `VIA.WPF.Graph/VIA.WPF.Graph.slnx` bleibt das einzige Solution-Format dieser Library.
- Dokumentation liegt im Repository-Root unter `docs/`; die README liegt im Repository-Root.
- Die vier späteren Projektverzeichnisse werden direkte Kinder des Solution-Ordners `VIA.WPF.Graph/`.
- Assembly- und Root-Namespace eines späteren Projekts entsprechen genau seinem Projektnamen: `VIA.WPF.Graph.Core`, `VIA.WPF.Graph.Graphviz`, `VIA.WPF.Graph.Wpf` und `VIA.WPF.Graph.Demo`.
- Es wird weder ein `VIA.WPF.Graph.UserFlow`- noch ein `VIA.WPF.Graph.Mockup`-Projekt oder -Namespace in dieser Solution angelegt.

**Verbindliche Referenzmatrix**

```text
VIA.WPF.Graph.Core      -> keine Projekt-, WPF-, Graphviz-, UserFlow- oder Host-Abhängigkeit
VIA.WPF.Graph.Graphviz  -> VIA.WPF.Graph.Core
VIA.WPF.Graph.Wpf       -> VIA.WPF.Graph.Core, SkiaSharp.Views.WPF
VIA.WPF.Graph.Demo      -> VIA.WPF.Graph.Core, VIA.WPF.Graph.Graphviz, VIA.WPF.Graph.Wpf

VIA.WPF.Graph.Graphviz <-> VIA.WPF.Graph.Wpf                 verboten
Alle VIA.WPF.Graph.*-Projekte -> UserFlow / Mockup           verboten
```

Die Demo ist ausschließlich der Composition Root. Sie darf Graphviz-Layout und WPF-/Skia-Renderer zusammensetzen. Der WPF-Renderer erhält weder eine Projekt- noch eine Paketabhängigkeit zu Graphviz; Graphviz bleibt ausschließlich Layoutadapter. `SkiaSharp.Views.WPF` ist als reine Renderer-Abhängigkeit in `VIA.WPF.Graph.Wpf` zulässig und darf nicht in den Core wandern.

P0-001 dokumentiert diese Struktur, erzeugt aber keine Projekt-, NuGet-, C#- oder XAML-Dateien. Solche Dateien gehören erst in einen danach ausdrücklich freigegebenen Umsetzungsschritt.

---

## 5. Neutrales fachliches Modell

Die unten stehenden Begriffe definieren die fachliche Verantwortung. Exakte Typnamen, Namespace und öffentliche API werden erst in Phase 1 festgelegt.

### 5.1 Knoten

Ein Knoten benötigt mindestens:

| Eigenschaft | Zweck |
|---|---|
| `Id` | hostübergreifend stabile Identität innerhalb eines Graphdokuments |
| `Title` | sichtbarer Name |
| `Description` | optionale Kurzbeschreibung |
| `Kind` | semantischer Knotentyp, z. B. Standard, Popup, Bereichs-Stellvertreter, Extern, Referenz |
| `DefaultSize` | gewünschte Kartengröße vor Layout |
| `VisualStyleKey` | Template-/Darstellungswahl, nicht fachliche Logik |
| `GroupMemberships` | Zugehörigkeit zu null, einer oder mehreren Gruppen |
| `Metadata` | optionale hosteigene, nicht vom Kern interpretierte Zusatzdaten |

**Wichtig:** Hintergrundfarbe, Badge-Farben und konkrete WPF-Brushes gehören nicht in die fachliche Kernlogik. Der Kern kann einen neutralen Style-Key oder eine semantische Kategorie liefern; die WPF-Schicht entscheidet das tatsächliche Template.

### 5.2 Links

Ein Link benötigt mindestens:

| Eigenschaft | Zweck |
|---|---|
| `Id` | stabile Link-Identität |
| `SourceNodeId`, `TargetNodeId` | Verbindung |
| `Direction` | gerichtet oder ungerichtet |
| `Kind` | fachliche Kategorie |
| `Label` | sichtbarer Action-/Linkname |
| `LineStyle` | durchgezogen, gestrichelt, punktiert etc. |
| `Weight/Priority` | Einfluss auf Layout oder visuelle Priorität |
| `IsLayoutConstraint` | steuert, ob Link den Hauptfluss beeinflusst |
| `Metadata` | hosteigene Referenzinformationen |

Empfohlene neutrale Link-Kategorien:

```text
Primary      Hauptpfad
Secondary    regulärer Nebenpfad
Back         Rückweg
Cancel       Abbrechen / expliziter Abbruchpfad
PopupOpen    Öffnen eines Popups
PopupClose   nur falls ein Host eine explizite Close-Aktion kennt
External     URL, Datei oder Systemübergang
Reference    Baum-Referenz auf bereits besuchten Knoten
Diagnostic   technische/optionale Verbindung
```

Der **Link-Kind** ist fachlich. Der Linientyp wird daraus standardmäßig abgeleitet, kann aber vom Host bewusst überschrieben werden.

Zusätzliche Regeln:

- Parallelkanten sind zulässig: Zwei unterschiedliche Actions dürfen dieselben Quell- und Zielknoten haben. Sie werden nie anhand von Source/Target zusammengeführt.
- In kompakten Ansichten dürfen Parallelkanten zu einer sichtbaren Bündel-Kante mit Anzahl-Badge zusammengefasst werden. Die Einzelactions bleiben bei Auswahl oder im Detailmodus erreichbar.
- Selbstkanten sind zulässig, werden aber als explizite Schleife dargestellt und als Diagnosefall markiert.
- Die allgemeine Kategorie `Cancel` ist vorgesehen. UserFlow ordnet erst dann zu, wenn eine fachliche Cancel-Semantik existiert; sie wird nicht aus einem beliebigen Back-Link erraten.

### 5.3 Gruppen

Es gibt zwei ausdrücklich unterschiedliche Gruppenarten.

#### A. Container-Gruppen

Eigenschaften:

- disjunkt oder sauber hierarchisch,
- geeignet für Bereichsrahmen/Cluster,
- können expandiert oder kollabiert werden,
- dürfen ausschließlich dann als Graphviz-Cluster gerendert werden, wenn ihre Mitgliedschaft eindeutig ist.

Beispiele:

```text
Einstieg
Aufträge
Administration
```

#### B. Markierungs-Gruppen

Eigenschaften:

- dürfen überlappen,
- werden nicht als kollabierbarer Container behandelt,
- erscheinen als Auswahl, Filter, Umrandung, Schraffur oder Hervorhebung,
- ändern die Knotenpositionen nicht zwingend.

Beispiele:

```text
„Onboarding-Flow“
„Kritische Screens“
„Screens mit Freigabe“
„Mobile Einstiegspfade“
```

### 5.4 Graphdokument und Projektionen

Das neutrale Graphdokument enthält Knoten, Links und Gruppen. Es ist keine UI-Collection und keine zweite fachliche Wahrheit.

Darauf aufbauende Projektionen:

| Projektion | Zweck |
|---|---|
| Gesamtgraph | technischer Gesamtüberblick |
| Bereichsübersicht | nur Container-Gruppen plus gebündelte Übergänge |
| Bereichsgraph | Knoten eines Bereichs plus externe Stubs |
| Screen-Fokus | ausgewählter Screen/Pupup plus direkte Nachbarn |
| Navigation Path Tree | lesbarer Pfadbaum ab Root oder Auswahl |
| Gruppenfokus | Auswahl einer oder mehrerer Markierungsgruppen |
| Diagnoseansicht | alle Knoten/Links mit reduzierten Labels |

Eine Tree-Projektion ist optional und nur für gerichtete, vom Host als navigierbar markierte Links sinnvoll. Der Host bestimmt Root-Knoten, einbezogene Link-Kategorien und die Behandlung mehrerer Einstiege. Nicht erreichbare Komponenten erscheinen separat unter „Weitere Einstiege / nicht erreichbar“ und dürfen nicht unsichtbar werden.

---

## 6. Layout-Architektur mit Graphviz

### 6.1 Verantwortungsteilung

```text
Neutrales Graphdokument
    -> Layout-Projektion
    -> DOT-Modell
    -> Rubjerg.Graphviz / dot
    -> Layout-Ergebnis
    -> WPF-/Skia-Renderer
```

Graphviz liefert:

- Knoten-Bounding-Boxes,
- Cluster-Bounds,
- Kanten-Splines/Point-Folgen,
- Richtung und Layering,
- Kreuzungsreduktion innerhalb der verfügbaren Layoutregeln.

WPF/Skia liefert:

- Knoten-Karten,
- Popups als eigene Templates,
- Icons, Badges und Action-Labels,
- Kantenstile,
- Auswahl/Hover/Fokus,
- TreeView/Pfadansicht,
- Zoom, Pan, Fit, Suche,
- hit-testbare Kanten und Karten.

### 6.1.1 Größen- und Koordinatenvertrag

Graphviz benötigt die endgültigen Knotengrößen vor dem Layout. Daher arbeitet jede Ansicht zunächst mit festen, je Template definierten Größenprofilen (`Compact`, `Standard`, `Detail`, `Popup`, `Stub`). Diese Profile sind aber nur ein erster Vertrag. Für die produktive Skia-Library wird daraus eine explizite Mess- und Wrap-Policy.

`GraphLayoutNode.Bounds` sind das Ergebnis des Layouts. Sie dürfen nicht stillschweigend nachträglich vergrößert werden, nur weil ein gerenderter Titel oder Subtitle nicht passt. Eine solche Korrektur würde Graphviz-Kanten, Pfeilenden, Cluster-Bounds, Hit-Tests, Fit-to-Graph und Scroll-Extents verfälschen. Zulässig ist nur ein klar benannter visueller Innenabstand innerhalb der bereits berechneten Bounds oder ein eigener, dokumentierter Postprocessor für rein optische Gruppenflächen.

Der saubere Ablauf lautet:

```text
GraphDocument / Projektion
    -> CardMeasurePolicy mit Template, ViewMode, Texten und Max-/Min-Regeln
    -> finale GraphSize je Node
    -> Graphviz-Layout mit echten Node-Größen
    -> GraphLayoutResult mit Node-/Group-/Edge-Geometrie
    -> SkiaGraphSurface rendert exakt in diesen Bounds
```

Der Graphviz-Adapter kapselt die Umrechnung zwischen DOT-/Point-Koordinaten und WPF-DIPs an genau einer Stelle. Alle Bounds, Splines, Zoom- und Hit-Test-Berechnungen verwenden danach nur noch dieses gemeinsame DIP-Koordinatensystem. `SkiaGraphSurface` rendert diese Koordinaten direkt über Skia und hält seine hit-testbaren Node-/Link-/Group-Bounds aus dem aktuellen Render-Snapshot vor.

### 6.1.2 Textmessung, Wrap und CardMeasurePolicy

Für Skia muss Textlayout planbar sein. Deshalb wird eine neutrale, hostfreie `GraphCardMeasurePolicy` vorgesehen. Sie liegt nicht im Core als Skia-/WPF-Abhängigkeit, sondern als WPF-/Renderer-Policy oder als vom Host vor dem Layout gelieferte Größenentscheidung. Der Core kennt weiterhin nur `GraphSize`.

Die Policy berechnet pro Node vor dem Layout:

- Mindest- und Maximalbreite,
- Mindest- und Maximalhöhe,
- Zeilenregeln für Titel, Subtitle, Typzeile und optionale Metazeilen,
- Wrap oder Ellipsis pro ViewMode und NodeKind,
- Innenabstände und reservierte Bereiche für Selection, Labels oder Statushinweise,
- Fallback-Regeln bei sehr langen Texten.

Empfohlene Standardregeln:

| View/Template | Titel | Subtitle | Typ-/Metazeile | Verhalten bei Überlauf |
|---|---|---|---|---|
| Compact | 1 Zeile | ausblenden | ausblenden | Ellipsis |
| Standard | bis 2 Zeilen | optional 1 Zeile | nur Spezialtypen | Ellipsis nach erlaubten Zeilen |
| Detail | bis 3 Zeilen | bis 2 Zeilen | erlaubt | Höhe bis Maximalwert wachsen lassen |
| Popup | 1–2 Zeilen | optional | Popup-Typzeile erlaubt | kompakt halten |
| Stub/External | 1 Zeile | optional | External/Reference erlaubt | Ellipsis |

Ändert sich Text, Template, VisualDensity oder ViewMode so, dass sich die gemessene Größe ändert, wird ein kontrollierter Layout-Rebuild ausgelöst. Es gibt keine stille Größenabweichung zwischen Graphviz-Input und Skia-Rendering.

### 6.1.3 Layout-Engine-Grenze und optionale Alternativen

Graphviz/`dot` bleibt die Standard-Layoutengine für gerichtete Navigations- und Bereichsgraphen. Andere Werkzeuge werden nicht vermischt, sondern ausschließlich über eigene Adapter hinter demselben neutralen Layoutvertrag evaluiert.

- Graphviz/`dot`: Standard für hierarchische Flows, Gruppen/Cluster und Diagnosegraphen.
- MSAGL: später als optionaler alternativer Layoutadapter prüfbar, aber nur nach eigenem Step-Gate, API-Prüfung und Vergleichstest.
- QuikGraph/QuickGraph: nur für Graphalgorithmen wie Analyse, Traversal oder Pfadsuche interessant; kein Ersatz für visuelles Card-/Cluster-/Edge-Layout.
- Eigene Routen: für Focus-/Branch-Ansichten zulässig, wenn Graphviz-Splines visuell zu unruhig sind. Dann bleiben Node-Positionen aus dem Layout, aber Kanten werden view-spezifisch als gerade, polyline oder orthogonale Kurzrouten berechnet.

Kein neues Layoutpaket wird eingeführt, bevor der tatsächliche API-, Lizenz-, Deployment- und Qualitätsnutzen gegen den aktuellen Graphviz-Adapter geprüft wurde.

### 6.2 Standardlayoutregeln

| Ansicht | Standardrichtung | Begründung |
|---|---|---|
| Bereichsübersicht | Top → Bottom | kompakter bei vielen Bereichen |
| Bereichsfluss | Left → Right | entspricht Lesefluss und klassischen UserFlows |
| Screen-Fokus | Left → Right | Alternativen sind unmittelbar sichtbar |
| Navigation Path Tree | Top → Bottom | natürliche TreeView-Lesart |
| Vollgraph | Top → Bottom | Diagnoseansicht, begrenzte Breite |

### 6.3 Haupt-, Neben- und Rückwege

- `Primary`: beeinflusst Layout und wird stärker angezeigt.
- `Secondary`: beeinflusst Layout, aber visueller zurückgenommen.
- `Back`: `constraint=false`; darf den Hauptfluss nicht umsortieren.
- `PopupOpen`: kurze, dezente Verbindung zu einem kleineren Popup-Knoten.
- `External`: führt zu einem Terminal- oder Stub-Knoten; keine künstliche Screen-Verbindung.

### 6.4 Kantenrouting

Zu testen und je Ansicht separat einstellbar:

- Spline,
- Polyline,
- orthogonal.

Die Wahl wird nicht als globale technische Einstellung erzwungen. Sie gehört in die jeweilige Ansichtskonfiguration, weil ein Bereichsfluss andere Anforderungen hat als eine Diagnoseansicht.

Für Focus- und Branch-Ansichten ist ausdrücklich erlaubt, Graphviz nur für Node- und Group-Positionen zu verwenden und die sichtbaren Kanten anschließend mit einer eigenen, vereinfachten Routing-Policy zu zeichnen. Das ist kein Bruch der Architektur, solange die Policy hostneutral bleibt und keine Domänenbegriffe kennt. Ziel ist weniger Zickzack und bessere Lesbarkeit in kleinen Ausschnitten.

### 6.5 Fehler- und Fallbackverhalten

- Layoutfehler dürfen die Anwendung nicht schließen.
- Der Adapter liefert einen kontrollierten Fehlerzustand mit ursprünglichem Graphdokument und diagnostischer Meldung.
- Fehlt eine Kanten-Geometrie, zeichnet der WPF-Renderer eine gerade Fallback-Kante zwischen den Knotenzentren.
- Nicht auflösbare Ziele erscheinen als markierte externe oder fehlerhafte Stubs; sie werden nicht stillschweigend entfernt.

---

## 7. WPF-/Skia-Darstellung und Interaktion

### 7.1 Rechter Bereich: SkiaGraphSurface

`SkiaGraphSurface` ist die primäre Graphfläche. Sie ist ein eigenes WPF-Control auf Basis von `SKElement`, kein fremder Graphviewer. Die frühere WPF-`GraphCanvas` bleibt nur noch als Legacy-/Vergleichsbestand, bis ein eigenes Step-Gate über Entfernung oder API-Kompatibilität entscheidet.

Funktionen der ersten produktiven Ausbaustufe:

- Zoom per Mausrad und Zoom-Slider,
- Pan über freie Fläche,
- Fit-to-Graph,
- Knoten-Auswahl,
- Kanten-Auswahl,
- Mehrfachauswahl per Modifier,
- Gruppenauswahl,
- Suche und Zentrieren,
- Fokusmodus mit konfigurierbarer Scope- und Dichte-Policy,
- Auswahl synchron zum Tree,
- Kontextmenü über hostseitige Commands,
- optionaler Viewport-/Scrollbar-Vertrag oberhalb der Skia-Fläche, ohne Hostmodell-Mutation.

### 7.1.1 Scope-Policy für Tree-Auswahl und Graphfläche

Die Product Demo hat gezeigt, dass ein Gesamtgraph zwar mit Skia performant renderbar ist, aber nicht als Standardarbeitsansicht taugt. Deshalb wird die sichtbare Graphfläche über eine neutrale Scope-Policy aus dem vollständigen Graphdokument abgeleitet.

Mögliche Scope-Modi:

| Modus | Zweck |
|---|---|
| Node Focus | ausgewählter Knoten plus direkte Vorgänger/Nachfolger |
| Branch | lokaler Navigationspfad mit begrenzter Tiefe |
| Group Compact | Gruppe als Bereich mit Einstiegspunkten, wichtigen Knoten und gebündelten Übergängen |
| Group Full | alle Knoten einer Containergruppe plus relevante Randübergänge |
| Overview | kompletter Graph als Diagnose-/Map-Ansicht |

Die Scope-Policy ist zunächst Host-/Demo-Logik. Sie wird erst in einen allgemeinen Library-Vertrag überführt, wenn die Regeln ausreichend stabil sind. Weder Core noch Renderer dürfen daraus UserFlow-, Screen- oder ActionArea-Annahmen ableiten.

Konfigurationspunkte:

```text
NeighborDepth
MaxVisibleNodes
IncludedLinkKinds
GroupDisplayMode
ShowExternalLinks
ShowBackAndCancelLinks
ShowReferenceLinks
```

### 7.1.2 GraphWorkspace als Host-Onboarding-Schicht

Neben den Einzelcontrols wird eine wiederverwendbare Workspace-Schicht geplant. Ziel ist, dass ein Host nicht die Product-Demo als Integrationsvorlage kopieren muss.

`GraphWorkspace` beziehungsweise eine gleichwertige Workspace-/Navigator-Schicht übernimmt die neutrale Koordination von:

- NavigationPathTree und SkiaGraphSurface,
- Tree-Auswahl → sichtbarer Graph-Scope,
- Graph-Klick → Tree-Pfad öffnen und sichtbaren Tree-Eintrag scrollen,
- Selection-Sync für Nodes, Links und Gruppen,
- Focus, Branch, Group Compact, Group Full, Overview und Diagnostic,
- Fit-to-Graph, Center selected, 100 %, Free Pan/Zoom und optionalen Scrollbars,
- Layout-/Routingoptionen je ViewMode,
- Request-Dispatch an den Host über `IGraphRequestHandler` oder gebundene Commands,
- Anzeige neutraler Request-/Validation-Feedbacks.

Der Host liefert dafür nur die allgemeinen Eingaben:

```text
GraphDocument
GraphViewState beziehungsweise bindbare View-State-Properties
GraphHostCapabilities
IGraphRequestHandler oder äquivalente Commands
optionale GraphWorkspaceOptions / ScopePolicy / LayoutPolicy
```

Der Host bleibt Besitzer von Persistenz, Undo/Redo, fachlicher Mutation und domänenspezifischer Projektion. Die Workspace-Schicht darf keine UserFlow-, Screen-, Popup-, ActionArea- oder ActionDefinition-Typen kennen. Sie darf auch keine Hostcollections direkt verändern.

Für UserFlow bedeutet das spätere Ziel: Der UserFlow-Adapter erzeugt ein neutrales `GraphDocument` und stellt Host-Commands bereit. Tree-/Graph-Synchronisierung, ScrollIntoView, sichtbare Teilgraphen, Fit/Center und allgemeine UX-Orchestrierung werden von der Library bereitgestellt und nicht in UserFlow dupliziert.

Mögliche spätere Einbindungsformen:

```xml
<graph:GraphWorkspace
    Document="{Binding FlowGraphDocument}"
    ViewState="{Binding FlowGraphViewState, Mode=TwoWay}"
    HostCapabilities="{Binding GraphCapabilities}"
    RequestHandler="{Binding GraphRequestHandler}" />
```

oder:

```xml
<graph:GraphWorkspace
    Workspace="{Binding FlowGraphWorkspace}" />
```

Die genaue öffentliche API wird erst nach einem Step-Gate festgelegt. Bis dahin bleibt Product-Demo-Code ein Erprobungsort, aber kein Host-spezifisches Muster, das später kopiert werden soll.

#### 7.1.2.1 Verbindliche Zielentscheidungen für GraphWorkspace

Für die weitere Phase-7-Umsetzung gelten folgende Entscheidungen:

| Entscheidung | Festlegung | Konsequenz |
|---|---|---|
| Workspace-Form | `GraphWorkspace` wird als fertiges WPF-Control geplant. | Standardhosts müssen nicht selbst Tree, Graphfläche, Scope, ScrollIntoView und Viewport-Orchestrierung verdrahten. |
| Tree-Zugehörigkeit | Der Navigation Tree ist Teil des Standard-Workspace und optional abschaltbar. | Tree/Graph-Sync, Tree-Pfadöffnung und Tree-ScrollIntoView sind Library-Mechanik. |
| Gruppen-Klick | Standard ist `Group Compact`. | Ein Gruppenklick zeigt nicht automatisch alle enthaltenen Screens/Nodes. |
| Vollständige Gruppe | `Group Full` ist eine explizite Nutzeraktion. | Große Bereiche überfluten den Graph nicht versehentlich. |
| Sichtbarer Graph | Die Library erzeugt `VisibleDocument` und `VisibleLayout` aus dem vollständigen `GraphDocument`. | Hosts wie UserFlow müssen die Demo-Scoping-Logik nicht nachbauen. |
| Layout/Routing-Umschaltung | Wird vorgesehen, aber erst nach stabiler ScopePolicy umgesetzt. | Erst wird reduziert, welche Nodes sichtbar sind; danach werden Linien/Routing optimiert. |
| Legacy GraphCanvas | Wird im nächsten freigegebenen Code-Patch entfernt. | SkiaGraphSurface und GraphWorkspace werden die einzige aktive WPF-Renderbasis. |
| Inspector | Bleibt hostseitig. | Die Library liefert Auswahl, Requests und neutrale Feedbackdaten, aber keine domänenspezifische Detailansicht. |

#### 7.1.2.2 Standard-Scope-Regeln

Die erste verbindliche ScopePolicy für den Workspace lautet:

| Auslöser | Standardmodus | Sichtbarer Inhalt | Grenze |
|---|---|---|---|
| Klick auf Node/Card | `Focus` | aktiver Node plus direkte Vorgänger und Nachfolger | typischerweise 8 bis 15 Karten |
| Klick auf Tree-Zweig | `Branch` | aktiver Tree-Pfad plus direkte Alternativen | Tiefe 1 bis 2 |
| Klick auf Containergruppe | `Group Compact` | Einstiegsknoten, aktive Auswahl, wichtige Übergänge und Randknoten | maximal 20 sichtbare Nodes |
| Button „Full Group“ | `Group Full` | alle Nodes der Gruppe plus relevante Randübergänge | explizite Nutzeraktion |
| Button „Overview“ | `Overview` | kompletter Graph | Diagnose-/Map-Ansicht, nicht Standard |
| Diagnose/Test | `Diagnostic` | vollständiger Graph mit Fehler-/Sonderfällen | explizit |

Wenn ein Scope mehr als die erlaubte Node-Anzahl enthält, reduziert die Library nicht stillschweigend fachliche Daten. Sie wählt eine reproduzierbare kompakte Darstellung: aktive Auswahl zuerst, dann Einstiegspunkte, direkte Nachbarn, strukturell wichtige Übergänge und zuletzt neutrale Referenz-/Randknoten. Die vollständigen Daten bleiben im Host-`GraphDocument` erhalten.

#### 7.1.2.3 Host- und Library-Verantwortung

Die Library übernimmt:

- Aufbau der Tree-Projektion aus dem neutralen Graph,
- Aufbau des sichtbaren Graph-Scope,
- Layout des sichtbaren Scope,
- Synchronisierung von Tree-Auswahl, Graph-Auswahl und aktiven IDs,
- ScrollIntoView im Tree,
- Fit/Center/100 %/Free Pan-Zoom,
- Auswahl- und Open-Requests an den Host,
- neutrale Anzeige von Request-/Validation-Feedback.

Der Host übernimmt weiterhin:

- Erstellung des vollständigen `GraphDocument`,
- Besitz und Persistenz von `GraphViewState`,
- fachliche Mutation und Undo/Redo,
- Verarbeitung neutraler Requests,
- domänenspezifische Inspektoren, Dialoge und Editoren,
- Mapping von Hostobjekten auf neutrale Graph-IDs.

`GraphWorkspace` darf dadurch hilfreich sein, ohne die zentrale Grenze zu verletzen: Die Library orchestriert neutrale UI-Mechanik, aber sie besitzt und mutiert kein Hostmodell.

#### 7.1.2.4 Geplante Standard-Bindings

Die bevorzugte einfache Host-Anbindung bleibt:

```xml
<graph:GraphWorkspace
    Document="{Binding FlowGraphDocument}"
    ViewState="{Binding FlowGraphViewState, Mode=TwoWay}"
    HostCapabilities="{Binding GraphCapabilities}"
    RequestHandler="{Binding GraphRequestHandler}" />
```

Alternativ darf eine spätere gebündelte Workspace-VM angeboten werden:

```xml
<graph:GraphWorkspace
    Workspace="{Binding FlowGraphWorkspace}" />
```

Die erste Umsetzungsvariante bevorzugt die einfache Property-Bindung, weil sie den Host-Vertrag klar sichtbar macht und keine Host-spezifische ViewModel-Basisklasse erzwingt.

### 7.2 Linker Bereich: Navigation Path Tree

Der Tree ist keine Spiegelung aller Graphknoten, sondern eine zyklensichere Pfadprojektion.

Regeln:

1. Ein ausgehender struktureller Link erzeugt einen neuen Ast. Geschwisteräste bedeuten fachlich **ODER-Alternativen**, nicht eine zwingende Reihenfolge.
2. Ein bereits im aktuellen Vorfahrenpfad erreichter Knoten wird nicht rekursiv erneut expandiert.
3. Stattdessen wird ein Referenzknoten angezeigt.
4. Rückwege werden bevorzugt als Karten-/Tree-Hinweis dargestellt, nicht als vollständige neue Unterstruktur.
5. Mehrfach erreichbare Knoten außerhalb des aktuellen Vorfahrenpfads werden konfigurierbar behandelt: Referenz oder erneuter Ast.
6. Der Root ist entweder der Home-Knoten oder ein bewusst gewählter Startknoten.
7. Mehrere Roots und nicht erreichbare Komponenten werden in eigenen Root-Abschnitten sichtbar gehalten.
8. Unterschiedliche Actions zum selben Ziel bleiben als getrennte Tree-Zeilen sichtbar, weil ihr Trigger/Label fachlich verschieden sein kann.

Beispiel:

```text
Login
├─ Registrieren
│  └─ ↩ Login [Referenz]
└─ MainView
   └─ ⛔ Kein expliziter Rückweg
```

### 7.3 Rückwege in Karten und Tree

Rückwege werden als kompakte Karteninformation dargestellt:

```text
↩ Zurück
[Back]
```

Bei statisch bekanntem Ziel:

```text
↩ Zurück zu Login
[Back]
```

Für UserFlow gilt: Bei `NavigateBack` ist ein konkretes Ziel nicht persistiert. Daher ist „Zurück zu Login“ nur zulässig, wenn es aus dem aktuell sichtbaren Tree-Pfad eindeutig abgeleitet wird. Sonst lautet die Anzeige neutral „Zurück“.

### 7.4 Popups

Popups sind keine normalen Screens.

**Im Graph:**

- kleineres eigenes Template,
- Popup-/Overlay-Hinweis,
- räumlich nahe beim auslösenden Screen,
- PopupOpen-Link dezent oder gestrichelt,
- ausgehende Navigation des Popups normal sichtbar, sofern sie fachlich existiert.

**Im Tree:**

```text
▣ Hilfe öffnen [Popup]
└─ Popup: Hilfe
```

Ein Schließen-Hinweis wird nur dargestellt, wenn der Host eine fachlich eindeutige Close-/Back-Semantik bereitstellt. Die aktuelle ActionDefinition kennt keine eigene `ClosePopup`-Action; diese darf nicht ohne Freigabe erfunden werden.

### 7.5 Knoten-Templates

Mindestens drei Templates:

| Template | Einsatz |
|---|---|
| Compact | Gesamtgraph / Diagnose |
| Standard | Bereichsfluss |
| Detail | Screen-Fokus / Auswahl |

Zusätzlich:

| Typ | Darstellung |
|---|---|
| Screen | Standardkarte mit Titel, optional Description und Actionanzahl; keine erzwungenen Standard-Badges |
| Popup | kleinere Overlay-Karte |
| Bereich | Cluster-/Bereichskarte in Übersicht |
| Extern/Stub | reduzierte Karte für Übergang außerhalb der aktuellen Projektion |
| Referenz | Tree-only oder reduzierte Graphmarkierung |

Screen-Vorschauen aus UserFlow werden erst nach einer stabilen Basis ergänzt. Sie sind kein Blocker für Architektur, Navigation oder Skalierung.

### 7.6 Kantenlabels und visuelle Dichte

- Detail- und Bereichsansichten zeigen Actionlabels direkt an der Kante oder als nahes Label-Chip; Labels werden als eigene Render-Schicht nach Linien und Karten gezeichnet.
- Gesamt- und Diagnoseansichten reduzieren Labels abhängig von Zoom und Dichte.
- Bei Parallelkanten wird im kompakten Modus ein Bündel-Badge gezeigt; die Einzellinks bleiben auswählbar.
- Back-, Cancel-, Popup- und External-Links bleiben auch ohne volles Label an Pfeilform, Typ-Hinweis und Linienstil erkennbar.
- Kanten-Hit-Tests bleiben unabhängig davon möglich, ob das Label bei kleiner Zoomstufe ausgeblendet ist.

---

## 8. Selektion, Commands und Messaging

### 8.1 Zustandsbesitz

Der Zustand gehört nicht dem WPF-Control, sondern einem hostseitigen ViewModel:

```text
Aktive Ansicht
Aktiver Bereich
Ausgewählte Knoten
Ausgewählte Links
Ausgewählte Gruppen
Suchtext
Fokusmodus
Zoom/Pan
Tree-Expand-Zustand
Kollabierte Container-Gruppen
Layoutoptionen
```

Das Control bindet an diesen State beziehungsweise erhält ihn per Binding/Command. Ein Viewwechsel darf den Zustand nicht verlieren.

### 8.2 CommunityToolkit.Mvvm-Verwendung

| Bedarf | Toolkit-Mittel |
|---|---|
| ViewModel-Zustand | `ObservableObject`, `[ObservableProperty]` |
| abgeleitete Anzeigezustände | `[NotifyPropertyChangedFor]` oder partielle Change-Methoden |
| synchrone Nutzeraktionen | `[RelayCommand]` |
| Layoutberechnung / Suche mit Cancel-Logik | `[AsyncRelayCommand]` |
| hostseitige Reaktion auf Nachrichten | `ObservableRecipient` nur dort, wo ein Empfänger tatsächlich nötig ist |
| lose Synchronisation Tree ↔ Graph ↔ Host | `WeakReferenceMessenger` |

### 8.3 Messaging-Regel

`WeakReferenceMessenger` wird nur für lose UI- und Navigationssignale eingesetzt, beispielsweise:

- Auswahl eines Knotens,
- Zentrierwunsch auf einen Knoten,
- Tree-Pfad öffnen,
- Bereich auswählen,
- Layout neu berechnen.

Nicht über Messages:

- direkte Mutation von UserFlow-Collections,
- Persistenz,
- Undo/Redo,
- fachliche Validierung.

Diese Vorgänge laufen über hostseitige Commands und Services.

### 8.4 Änderungsrequests

Die allgemeine Graph-Surface sendet nur Requests wie:

```text
OpenNode
OpenLink
OpenGroupRequested
ReturnToOverviewRequested
CreateLinkRequested
RetargetLinkRequested
DeleteLinkRequested
SetGroupCollapsedRequested
SetManualNodePositionRequested
```

Der Host entscheidet, ob der Request unterstützt wird. Eine generische Graph-Surface muss keinerlei fachliche Mutation kennen.

---

## 9. UserFlow-Adapter und fachliche Abbildung

### 9.1 Knotenabbildung

| UserFlow-Quelle | Graphknoten |
|---|---|
| `Screen` | `Screen`-Knoten |
| `ScreenPopup` | `Popup`-Knoten |
| `Screen.GroupName` | primäre Container-Gruppe für Screens |
| `ScreenPopup.GroupName` | Popup-Gruppe oder zugeordnete Container-Gruppe |
| HomeScreen | Tree-Root- und Start-Badge |
| externer Übergang | Stub-/Terminalknoten, nicht künstlicher Screen |

### 9.2 Linkabbildung

Der UserFlow-Adapter durchläuft die Controls eines Screens beziehungsweise Popups und filtert `ActionArea`-Controls. Für jede ActionDefinition entsteht eine Graphkante.

| ActionType | Darstellung |
|---|---|
| `Navigate` | gerichteter Screen→Screen-Link |
| `ShowPopup` | gerichteter Screen/Popup→Popup-Link |
| `NavigateHome` | Link zum aktuell ermittelten HomeScreen |
| `NavigateBack` | Back-Semantik; Ziel nur im gewählten Pfad sicher ableitbar |
| `OpenURL` | externer Terminal-Link |
| `OpenFile` | externer Terminal-Link |
| `None` | wird nicht als Navigation dargestellt; optional Diagnose-Hinweis |

### 9.3 Link-Metadaten

Für die spätere Bearbeitung führt der Adapter intern mindestens:

```text
OwnerKind              Screen | Popup
OwnerId                Screen.Id oder ScreenPopup.Id
ActionAreaControlId    DesignControl.Id der ActionArea
ActionIdentity         zukünftige ActionDefinition.Id oder Übergangsschlüssel
ActionType             ActionDefinition.Type
Trigger                ActionDefinition.Trigger
```

Die Metadaten sind keine neue UserFlow-Collection. Sie sind Teil der transienten Projektion und dienen dazu, UI-Anfragen eindeutig zurück zum Host zu führen.

### 9.4 Gruppenabbildung

Die bestehenden `GroupName`-Werte sind die Ausgangsbasis für Bereiche.

Regeln für die erste UserFlow-Stufe:

- Jeder Screen gehört genau einer Screen-Container-Gruppe an.
- Ein leerer oder fehlender `GroupName` wird einer stabil benannten, synthetischen Container-Gruppe „Ohne Gruppe“ zugeordnet. Dieser Wert wird nicht zurück in UserFlow geschrieben.
- Popups bleiben zunächst in eigenen Popup-Gruppen.
- Eine optische Zuordnung eines Popups zu einem Screen erfolgt über die auslösende PopupOpen-Kante, nicht durch eine künstliche Gruppenzuweisung.
- Überlappende fachliche Gruppen kommen erst als allgemeine Markierungsgruppen hinzu, sobald ein Host dafür Daten liefern kann.

---

## 10. Bearbeiten und Erstellen von Navigation

### 10.1 Grundsatz

Die Graphansicht bearbeitet niemals `ActionArea.Actions` direkt. Sie fordert Änderungen beim UserFlow-ViewModel an.

```text
Graph/Tree
  -> Änderungsrequest
  -> UserFlow FlowViewModel / Host-Command
  -> Snapshot vor Mutation
  -> ActionArea / ActionDefinition ändern
  -> persistieren
  -> Projektion neu aufbauen
```

### 10.2 Bestehende Navigation ändern

Mögliche spätere Aktionen:

- Kante auswählen → zugehörige ActionArea/ActionDefinition im bestehenden Editor öffnen.
- Ziel eines `Navigate`-Links ändern.
- Popup-Ziel eines `ShowPopup`-Links ändern.
- Trigger oder Linklabel im Action-Editor ändern.
- Link löschen → zugrunde liegende ActionDefinition löschen.

Die erste produktive Bearbeitungsstufe nutzt den bestehenden ActionArea-Editor, statt im Graph sofort einen zweiten vollständigen Action-Editor zu bauen.

Vor dieser Stufe wird der Editor so erweitert, dass vorhandene Action-Identitäten beim Laden und Speichern erhalten bleiben. Außerdem wird getestet, ob die aktuell verfügbaren Editor-Controls alle angezeigten ActionTypes tatsächlich bearbeiten können. Nicht unterstützte Typen bleiben im Graph sichtbar, aber nur lesbar.

### 10.3 Neue Navigation erstellen

Möglicher Ablauf nach der read-only Phase:

1. Quell-Screen oder Quell-Popup auswählen.
2. Vorhandene ActionArea wählen oder „ActionArea erzeugen“ anfordern.
3. Zielscreen oder Popup im Graph/Tree wählen.
4. Aktionstyp und Trigger im hostseitigen Dialog wählen.
5. Host erzeugt/aktualisiert die ActionDefinition.
6. Snapshot wird vor Mutation gespeichert.
7. Graphprojektion und Tree werden neu berechnet.

### 10.4 Editierbarkeit nur nach Datenmodell-Freigabe

Folgende Punkte benötigen vor Code eine explizite Entscheidung:

- persistente `ActionDefinition.Id`,
- erlaubte Mehrfachaktionen pro Trigger,
- Behandlung von `NavigateBack`,
- Popup schließen als eigene Action oder nur Preview-Verhalten,
- ob das Anlegen einer neuen ActionArea im Graph erlaubt sein soll,
- Undo-/Redo-Kontext für Änderungen an Actions eines Screens oder Popups.

---

## 11. Persistenz und Undo/Redo

### 11.1 Fachliche Navigation

Die fachliche Navigation bleibt in UserFlow:

```text
Screen / ScreenPopup
  -> Bands / Pages
  -> ActionArea
  -> ActionDefinition
```

Änderungen passieren nur dort und werden über die bestehende Projektpersistenz gespeichert.

### 11.2 Graphansichtszustand

Der allgemeine Graph benötigt einen separaten, hostbesessenen View-State.

Vorgesehene State-Bereiche:

| State | In erster Stufe | Später persistierbar |
|---|---|---|
| aktive Ansicht | ja | ja |
| ausgewählter Bereich | ja | optional |
| Tree-Expand-Zustand | ja | ja |
| kollabierte Container-Gruppen | ja | ja |
| Zoom/Pan | ja | ja |
| Suche/Filter | ja | optional |
| manuelle Knotenpositionen | nein | nach eigener Freigabe |

Empfehlung für UserFlow: View-State wird als eigener Projektzustand modelliert und mit dem Projekt gespeichert. Er darf nicht an `FlowView`-Instanzen hängen.

### 11.2.1 Abwärtskompatibilität und Migration

- Bestehende `.ufp`-Projekte ohne GraphViewState müssen ohne Sonderbehandlung laden; der View-State startet dann mit Defaults.
- Wird eine persistente `ActionDefinition.Id` freigegeben, brauchen alte Actions eine einmalige, deterministische oder zentral erzeugte ID-Migration. Die ID darf weder bei jedem Rendern noch bei jedem Editor-Öffnen neu entstehen.
- Die Migration wird zusammen mit JSON-Serialisierung, Clone-/Snapshot-Pfaden und bestehendem ActionArea-Editor getestet.
- Das Speichern eines alten Projekts ohne Änderungen darf keine unbeabsichtigte fachliche Navigation ändern.

### 11.3 Manuelle Positionen

Manuelle Knotenverschiebung wird ausdrücklich nicht in die erste Integrationsstufe aufgenommen.

Grund:

- sie kollidiert mit automatischem Graphviz-Layout,
- sie verlangt klare Regeln für Layout-Neuberechnung,
- sie verlangt persistente, stabile Knoten-IDs,
- sie muss bei gelöschten/umbenannten Knoten bereinigt werden.

Erst wenn die automatische Darstellung akzeptiert ist, folgt ein gesonderter Plan für Layout-Overrides.

### 11.4 Undo/Redo

Bei jeder Navigation-Mutation:

1. Snapshot vor der Mutation,
2. Mutation im originalen UserFlow-Modell,
3. Rebuild der Projektion,
4. Wiederherstellung nach Undo/Redo über das normale UserFlow-Verhalten.

Graph-UI erhält danach nur einen neuen Render-Snapshot. Kein eigenes zweites Undo-System in UserFlow.

---

## 12. Entwicklungsphasen mit Step-Gates

### Phase 0 – Baseline, Entscheidungen und technische Verifikation

**Ziel:** Risiken entfernen, bevor neue Bibliotheksarchitektur entsteht.

**Umfang:**

- die bestehende `VIA.WPF.Graph.slnx` sowie Namespace-/Projektkonventionen verbindlich festlegen und dokumentieren,
- **keinen Code aus der früheren Graphviz-Demo übernehmen**; die neue Lösung entsteht gemäß diesem Plan als eigenständige Solution,
- die frühere Demo höchstens als unverbindliche visuelle Referenz betrachten, aber nicht an einen neuen Chat übergeben und nicht kompilieren,
- Rubjerg.Graphviz 3.0.5 gegen den tatsächlichen Zielrechner validieren,
- native Voraussetzungen, Lizenz-/Deployment-Fragen und Ziel-Runtime dokumentieren,
- Minimalgraph testen: 6 Knoten, 1 Popup, 1 Rückkante, 2 Gruppen,
- keine UserFlow-Quellen, ActionArea-Editoren oder Host-Datenmodelle analysieren; diese Informationen gehören bewusst erst in die spätere Integrationsphase.

**Entscheidungen vor Phase 1:**

- P0-001: Die konkrete Solution-/Projektstruktur unter `VIA.WPF.Graph` einschließlich Referenzmatrix ist in Abschnitt 4.2 verbindlich festgelegt.
- Welcher minimale neutrale Graphvertrag wird in Phase 1 umgesetzt?
- Sollen Popups im allgemeinen Modell als eigener NodeKind oder nur als Host-Metadatum geführt werden?
- Welche View-State-Daten gehören in den allgemeinen, hostneutralen Vertrag und welche bleiben vollständig beim jeweiligen Host?
- Welche Rubjerg-/Deployment-Variante funktioniert auf dem Zielrechner?

**Abnahme:**

- lokale Solution baut,
- Test-App startet,
- Links→Rechts und Oben→Unten funktionieren,
- kein MSAGL/GraphX im neuen Kern,
- allgemeine Projekt- und API-Entscheidungen sind dokumentiert,
- es besteht keine Projekt-, Namespace-, Paket- oder Laufzeitabhängigkeit zu UserFlow.

**Step-Gate:** Freigabe von Projektstruktur, neutralem API-Rahmen und Rubjerg-Abhängigkeit.

---

### Phase 1 – VIA.WPF.Graph.Core

**Ziel:** Neutrales, testbares Graphmodell ohne WPF und ohne Graphviz.

**Umfang:**

- Knoten-, Link-, Gruppen- und Graphdokumentmodell,
- Container- versus Markierungsgruppen,
- Auswahl- und View-State-Datenmodelle,
- Projektionstypen: Gesamt, Bereich, Fokus, Tree,
- Validierung: eindeutige IDs, vorhandene Linkziele, parallele Links, Selbstkanten und zulässige Gruppenzuordnung,
- zyklensichere Tree-Projektion,
- Unit-Tests für Graphprojekte, Parallelkanten, Mehrfachroots und Tree-Rekursion.

**Nicht enthalten:** WPF, Graphviz, UserFlow-Referenzen, Mutation.

**Abnahme:**

- Core baut unabhängig,
- Tree endet bei Zyklen sicher,
- Container-Gruppen und überlappende Markierungsgruppen sind unterscheidbar,
- 150 Knoten lassen sich als Modell validieren.

**Step-Gate:** Freigabe des neutralen API-Entwurfs.

---

### Phase 2 – VIA.WPF.Graph.Graphviz

**Ziel:** Layoutadapter mit klarer Fehlerbehandlung und reproduzierbaren Ergebnissen.

**Umfang:**

- Graphdokument → DOT-Projektion,
- Container-Gruppen → Graphviz-Cluster,
- Hauptpfad-/Nebenpfad-/Rückweg-Regeln,
- Layoutoptionen `LR` und `TB`,
- Routingoptionen Spline, Polyline, Orthogonal,
- Auswertung von Node-Bounds, Cluster-Bounds und Edge-Geometrie, inklusive zentraler DOT→WPF-Koordinatenumrechnung,
- Fehler-/Fallbackmodell,
- Layout-Caching und Abbruch veralteter Berechnungen.
- Tests für parallele Links, Selbstkanten und Label-Bündelung.

**Abnahme:**

- 15-, 30- und 150-Knoten-Referenzgraphen layoutbar,
- Rückkanten stören Hauptfluss nicht,
- fehlende Ziele erzeugen keine Abstürze,
- Layoutfehler werden als Ergebnis gemeldet, nicht geworfen bis zur UI.

**Step-Gate:** Freigabe der Layoutqualität für Bereichsfluss und Gesamtübersicht.

---

### Phase 3 – VIA.WPF.Graph.Wpf: Renderer-Grundlage

**Ziel:** Vollständig eigener WPF-/Skia-Renderer auf Basis des Layout-Ergebnisses.

**Umfang:**

- SkiaGraphSurface mit Knoten-, Kanten-, Gruppen- und Overlay-Layern,
- Standard-, Compact-, Detail-, Popup- und Stub-Templates,
- Zoom, Pan, Fit-to-Graph,
- Knoten-/Kanten-Hit-Test,
- Einzel- und Mehrfachauswahl,
- Hover, Fokus, Suchzentrierung, Drill-down in Bereiche und Rückkehr zur Übersicht,
- Commands und hostseitig gebundener View-State,
- keine direkte Hostmutation,
- Legacy-GraphCanvas nicht weiter optisch ausbauen.

**Abnahme:**

- Mouse Wheel zoomt, freie Fläche verschiebt,
- Auswahl bleibt beim Rebuild logisch erhalten, sofern Id noch vorhanden,
- Surface-/Viewport-Größe wird aus Layoutbounds abgeleitet,
- WPF-/Skia-Control enthält keinen Graphviz-Code.

**Step-Gate:** visuelle Freigabe des Basisdesigns.

---

### Phase 4 – Path Tree, Synchronisierung und Gruppen-UX

**Ziel:** Die Hybridansicht wird nutzbar.

**Umfang:**

- Tree-/Pfadprojektion mit Mini-Karten,
- Referenzknoten bei Rückkanten und Zyklen,
- Tree ↔ Graph Synchronisierung,
- Container-Gruppen Collapse/Expand,
- Auswahl einer oder mehrerer Markierungsgruppen,
- Fokusmodus und Filter,
- gruppenbezogene Bereichsübersicht.
- kollabierte Containergruppen mit gebündelten externen Übergängen und Anzahl-Badges.

**Abnahme:**

- Klick im Tree zentriert den Graph,
- Klick im Graph selektiert/öffnet den Tree-Pfad, ohne implizit neu zu zentrieren,
- Collapse nur bei Container-Gruppen,
- überlappende Markierungsgruppen bleiben gleichzeitig auswählbar,
- Rückkanten erzeugen keine Tree-Endlosschleife.

**Step-Gate:** UX-Freigabe für die Standardarbeitsansicht.

---

### Phase 5 – VIA.WPF.Graph.Demo und Belastungstest

**Ziel:** allgemeine Lösung vor UserFlow-Integration beweisen.

**Testsets:**

| Testset | Inhalt |
|---|---|
| Small | 8–12 Knoten, 1 Popup, 1 Rückweg |
| Medium | 25–35 Knoten, mehrere Bereiche, externe Übergänge |
| Large | 100–150 Knoten, 10–20 Bereiche, Zyklen und Popups |
| Groups | überlappende Markierungsgruppen plus Containergruppen |
| Error | fehlende Linkziele, leere Gruppen, isolierte Knoten, Parallelkanten, Selbstkanten |

**Messpunkte:**

- Layoutzeit,
- UI-Reaktionsfähigkeit,
- Lesbarkeit des Bereichsflusses,
- Speicherverhalten bei wiederholtem Rebuild,
- korrektes Auflösen/Erhalten von Selektion,
- robuste Fehlerdarstellung.

**Abnahme:**

- Large-Test stabil,
- keine WPF-/Native-Crashes,
- Bereichsfluss bleibt die klarste Standardansicht,
- Gesamtgraph ist als Diagnoseansicht brauchbar,
- Product Demo nutzt SkiaGraphSurface als aktive Graphfläche.

**Step-Gate:** Entscheidung zur Extraktion/Übernahme in VIA.WPF.Graph.

---

### Phase 6 – Allgemeiner Hostvertrag für Bearbeitung

**Ziel:** Der Graph kann allgemein Änderungsanfragen stellen, ohne Domänenwissen zu besitzen.

**Umfang:**

- Request-/Result-Vertrag für Öffnen, Anlegen, Retargeting, Löschen,
- hostseitige Capability-Angaben: read-only, editierbar, neue Links erlaubt etc.,
- Command-/Message-Grenzen,
- Validierungs- und Fehlerdarstellung,
- Demo-Host mit beispielhafter bearbeitbarer Navigation.

**Abnahme:**

- read-only und editable Hosts nutzen dieselbe WPF-Komponente,
- abgelehnte Requests ändern nichts,
- UI zeigt eindeutige Rückmeldung,
- Core bleibt UserFlow-frei.

**Step-Gate:** Freigabe des Bearbeitungsvertrags.

---


### Spätere UserFlow-read-only-Integration – Adapter und FlowView

**Ziel:** Aktuelle UserFlow-Daten werden ohne Mutation visualisiert. Diese Integrationsstufe ist durch Revision 6/7 nicht mehr die nächste Arbeitsphase, sondern folgt erst nach Library-Finalisierung, allgemeiner Demo-Abnahme, Dokumentation und Packaging.

**Voraussetzungen:**

- Die allgemeinen Library-/Demo-/Dokumentationsphasen bis einschließlich Phase 9 sind abgenommen.
- Der aktuelle UserFlow-VS2AI-Export liegt jetzt vor.
- Die allgemeine `VIA.WPF.Graph`-Solution bleibt unverändert UserFlow-frei.
- Der Adapter entsteht ausschließlich auf UserFlow-Seite und referenziert `VIA.WPF.Graph`; die umgekehrte Richtung ist verboten.
- Vor dem ersten Code: ActionDefinition-Identität, JSON/Clone/Snapshot, Editor-Identitätserhalt, Altprojekt-Migration und UserFlow-View-State gegen den aktuellen Bestand prüfen und als Integrationsentscheidungen freigeben.

**Umfang:**

- UserFlow-Adapter aus Screens, Popups, ActionAreas und Actions,
- ActionArea-Quellermittlung aus bestehenden Controls, einschließlich expliziter Rebuild-/Invalidierungsstrategie nach relevanten Modelländerungen,
- Auflösen von `TargetScreenId`, `PopupId`, HomeScreen und externen Aktionen,
- Flow-spezifisches ViewModel mit Toolkit-Patterns,
- Einbau der Hybridansicht in die vorhandene `FlowView`,
- Synchronisierung mit `CurrentScreen`, `CurrentPopup`, `HomeScreen` und Preview-Navigation.

**Abnahme:**

- Graph wird aus Originalcollections erzeugt,
- keine UserFlow-Collection wird durch Graph/Tree geändert,
- Auswahl springt sauber zwischen FlowView und bestehender Screen-/Popup-UI,
- Screen-/Popup-Gruppen erscheinen als Bereiche,
- Wechsel von Screen/Template/Popup zerstört Graphzustand nicht.

**Step-Gate:** fachliche Freigabe der read-only UserFlow-Navigationsansicht.

---

### Phase 8 – UserFlow-Bearbeitung bestehender Actions

**Ziel:** Vorhandene Navigation aus Graph/Tree öffnen und ändern.

**Voraussetzungen:**

- stabile Action-Identität freigegeben, in JSON/Clone/Snapshot/Editor erhalten und umgesetzt,
- Snapshot-Strategie bestätigt,
- Ziel-/Back-/Popup-Semantik entschieden.

**Umfang:**

- Link auswählen → über Host-Command/Message den bestehenden ActionArea-Editor mit richtiger Action öffnen,
- Retargeting vorhandener `Navigate`-/`ShowPopup`-Actions,
- Löschen bestehender Actions,
- Rebuild nach Mutation,
- Undo/Redo mit bestehender Snapshot-Infrastruktur.

**Abnahme:**

- Änderung bleibt nach Speichern/Laden erhalten,
- Undo/Redo stellt die Navigation korrekt wieder her,
- Graph zeigt nach jeder Mutation den Modellzustand,
- keine Action-Kopien/Synchronisationslisten.

**Step-Gate:** Freigabe, neue Navigation aus Graph abzuleiten.

---

### Phase 9 – Neue Navigation aus der Hybridansicht

**Ziel:** Neue Actions kontrolliert im Graph/Tree erstellen.

**Umfang:**

- Quellknoten wählen,
- Zielknoten wählen,
- ActionArea wählen oder bewusst erzeugen,
- Trigger und ActionType festlegen,
- Dialog für fehlende Pflichtdaten,
- Snapshot vor Mutation,
- Validierung gegen ungültige Ziele, doppelte Actions und den V1-Grundsatz „maximal eine Action je ActionArea/Trigger“.

**Abnahme:**

- neu angelegte Action ist im bestehenden ActionArea-Editor sichtbar,
- nach Projekt-Neuladen identisch vorhanden,
- Graph/Tree aktualisieren sich aus Originaldaten,
- nicht unterstützte Cases bleiben gesperrt statt halb implementiert.

**Step-Gate:** Entscheidung über manuelles Graphlayout.

---

### Phase 10 – Optionale Erweiterungen

Nur nach stabiler produktiver Nutzung.

Mögliche Themen:

- persistierte manuelle Knotenpositionen,
- Layout-Overrides pro Bereich,
- Screenshot-/SVG/PDF-Export,
- Druckansicht,
- Link- und Knoten-Badges aus Hostmetadaten,
- Vergleich zweier Navigationsstände,
- Analyse: unerreichbare Screens, Sackgassen, Zyklen, fehlende Ziele,
- automatische Hauptpfadvorschläge,
- mehrere Graphquellen in einer Anwendung,
- globale Markierungsgruppen aus Fachdomänen.

---

## 13. Teststrategie

### 13.1 Core-Tests

- eindeutige Knoten-/Link-IDs,
- Links mit fehlenden Targets,
- Gruppenkollisionen,
- Container-/Markierungsgruppen,
- Tree-Zyklen,
- Mehrfachpfade,
- Linkklassifikation,
- Parallelkanten und Selbstkanten,
- mehrere Roots und nicht erreichbare Komponenten.

### 13.2 Graphviz-Integrationstests

- LR/TB pro Referenzgraph,
- Cluster-Bounds vorhanden,
- Spline-/Polyline-/Orthogonalgeometrie plausibel,
- Rückkanten als `constraint=false`,
- Layout-Fehler ohne Prozessabbruch,
- Wiederholung gleicher Daten ohne Geometry-Leaks,
- konsistente DOT→WPF-Koordinaten bei Zoom, Fit und Hit-Test.

### 13.3 WPF-/Skia-Funktionstests

- Zoom/Pan/Fit,
- Viewport-/Scrollbar-Verhalten,
- Knoten-/Kantenauswahl,
- Skia-Hit-Testing für Karten, Kanten und Gruppen,
- Tree/Graph-Synchronisierung,
- Search/Fokus,
- Collapse/Expand,
- Mehrfachgruppen-Auswahl,
- Tastaturfokus und Accessibility-Basis,
- Label-Ausblendung/Bündelung ohne Verlust der Link-Auswahl,
- Render-Layer-Reihenfolge: Gruppen, Linien, Karten, Pfeile/Labels/Overlays.
- Text-Wrap und Ellipsis pro NodeKind/ViewMode ohne Abschneiden außerhalb der Card-Bounds.
- gemessene Card-Größen stimmen mit Graphviz-Input und Skia-Rendering überein.

### 13.4 UserFlow-Integrationstests

- Screen mit `Navigate`,
- Screen mit `NavigateHome`,
- Screen mit `NavigateBack`,
- `ShowPopup`,
- Popup mit ausgehender Navigation,
- ungültige `TargetScreenId`/`PopupId`,
- Screen- und Popup-Gruppen,
- Projekt speichern/laden,
- Screen-/Template-/Popup-Wechsel,
- Undo/Redo nach Linkänderung,
- Altprojekt ohne GraphViewState/Action-ID laden, speichern und erneut laden,
- Action-ID bleibt nach Öffnen und Speichern im bestehenden ActionArea-Editor stabil.

### 13.5 Belastungsabnahme

| Szenario | Mindestziel |
|---|---|
| 15 Screens / 1 Bereich | sofort lesbar |
| 30 Screens / 3 Bereiche | Bereichsfluss ohne relevante Überlagerung |
| 100–150 Screens / 10–20 Bereiche | Übersicht, Drill-down und Diagnose stabil |
| wiederholtes Rebuild | keine sichtbare UI-Degradation |
| beschädigte Referenz | Hinweis statt Absturz |

---

## 14. Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Graphviz-native Runtime auf Zielsystem | Phase-0-Installations-/Deploymenttest, zunächst win-x64 festlegen |
| ActionDefinition ohne stabile Id | Bearbeitung erst nach expliziter Datenmodellentscheidung |
| `NavigateBack` ohne Ziel-ID | neutral darstellen oder Ziel nur aus aktivem Pfad ableiten |
| überlappende Gruppen als Cluster | nur disjunkte Containergruppen clustern; Überlappungen markieren |
| 150 Detailkarten auf einmal | Bereichs-/Fokusansichten als Standard, Scope-Policy und Compact-Modus im Vollgraph |
| Graphviz-Layout ändert sich nach Datenänderung | Selection an stabile IDs binden, nicht an Koordinaten |
| manueller Layoutwunsch zu früh | erst nach akzeptierter automatischer Darstellung separat planen |
| UI-Control mutiert Fachmodell | ausschließlich Requests/Commands, keine Collection-Referenzen im Renderer |
| jeder Host muss Demo-Orchestrierung kopieren | GraphWorkspace-/Policy-Schicht als wiederverwendbare Host-Onboarding-Schicht; Host liefert Daten und Commands, Library koordiniert Tree, Scope, Viewport und Requests |
| parallele Rebuilds | generation-id/cancellation und atomarer Austausch des Layout-Ergebnisses |
| Parallelkanten verschwinden oder Labels überlagern sich | Links immer über eigene Link-ID behandeln; Bündelung nur als reine Darstellung |
| neue Action-ID wird vom bestehenden Editor verloren | ID durch ActionRow laden/speichern; Clone/Snapshot/JSON testen |
| Altprojekte brechen nach Modellergänzung | rückwärtskompatibler Default und definierte Migrationsregel |
| ungruppierte Screens verschwinden aus Bereichsansichten | stabile synthetische Gruppe „Ohne Gruppe“ nur in der Projektion |
| früherer Demo-Code wird als technische Baseline missverstanden | Demo nicht übergeben und nicht übernehmen; neue Solution entsteht ausschließlich nach diesem Plan |
| Text wird erst nach dem Layout größer gerendert | CardMeasurePolicy vor Graphviz ausführen; Bounds nicht stillschweigend nachträglich vergrößern |
| Wrap-Regeln machen Layout unruhig | Min-/Max-Größen und ViewMode-spezifische Zeilenlimits definieren; Rebuild nur kontrolliert auslösen |
| Layout-Toolwechsel erzeugt neue Abhängigkeiten ohne Nutzen | Graphviz bleibt Standard; MSAGL/andere Tools nur nach Adapter-Step-Gate und Vergleichstest |
| Legacy-GraphCanvas bleibt nach Skia-Pivot als falsche Zielrichtung liegen | nicht weiter ausbauen; Entfernung/Archivierung nur nach eigenem API-Kompatibilitäts-Step-Gate |
| Skia-Hit-Testing und WPF-ScrollViewer divergieren | ein zentraler Viewport-/Extent-Vertrag und Tests für Pan/Zoom/Scrollbar-Synchronisierung |

---

## 15. Offene Entscheidungen vor Umsetzung

1. **ActionDefinition-ID:** Darf eine persistente ID ergänzt werden? Empfehlung: ja.
2. **Popup schließen:** Wird eine explizite `ClosePopup`-Semantik benötigt oder zunächst nicht dargestellt?
3. **Rücknavigation:** Soll `NavigateBack` ausschließlich generisch „Zurück“ anzeigen oder im ausgewählten Tree-Pfad den abgeleiteten Zielnamen nennen?
4. **Bereichsmodell:** Werden Screen- und Popup-Gruppen getrennt geführt oder gibt es eine fachliche Zuordnung zwischen ihnen?
5. **Persistenz des View-State:** Welche States sind pro Projekt zu speichern?
6. **`VIA.WPF.Graph`-Struktur:** In welche bestehende Solution/Repository-Struktur werden `VIA.WPF.Graph.*`-Projekte aufgenommen?
7. **Graphdesign:** Welche neutralen Product-Demo-Styles werden später in wiederverwendbare Skia-Renderer-Konfigurationen überführt?
8. **Bearbeitung:** Darf die Graphansicht später neue ActionAreas anlegen oder zunächst nur bestehende Actions ändern?
9. **Deployment:** Welche Runtime-Architektur wird offiziell unterstützt: `win-x64` nur oder zusätzlich andere Runtimes?
10. **Bereichscollapse:** Soll ein kollabierter Bereich nur Übergangszahlen oder auch die wichtigsten ausgehenden Actionlabels zeigen?
11. **ActionArea-Editor:** Bleibt V1 bei exakt einer Action pro Trigger, und wird die ID-Übernahme dort verbindlich umgesetzt?
12. **Mehrfachkanten:** Sollen sie im Standardgraph als Einzellinien oder ab einer Dichte als Bündel mit Anzahl angezeigt werden?
13. **Ungruppierte Screens:** Ist „Ohne Gruppe“ als reine Projektion fachlich akzeptiert?
14. **Größenvertrag:** Welche festen Kartenmaße gelten je Template, bevor dynamische Größenmessung später zugelassen wird?
15. **Legacy-GraphCanvas:** Entfernen, archivieren oder als Kompatibilitätscontrol behalten?
16. **Scope-Policy:** Welche Defaults gelten für Node Focus, Branch, Group Compact, Group Full und Overview?
17. **Link-Routing:** Wann nutzt die Ansicht Graphviz-Splines, gerade Linien, Polyline oder orthogonale Kurzrouten?
18. **CardMeasurePolicy:** Welche Wrap-/Ellipsis-Regeln gelten je ViewMode, NodeKind und VisualDensity?
19. **Layout-Alternativen:** Soll MSAGL später als optionaler Adapter evaluiert werden, oder bleibt Graphviz alleinige Layoutengine?
20. **Graph-Algorithmen:** Werden QuikGraph/QuickGraph-ähnliche Bibliotheken später für Analysefunktionen benötigt, ohne sie als Layoutengine zu verwenden?

---

## 16. Empfohlene Reihenfolge der nächsten konkreten Schritte

1. Aktuellen Skia-R7-Stand als offizielle Renderbasis dokumentieren und Product-Demo-Leichen bereinigen.
2. GraphWorkspace als Host-Onboarding-Schicht planen, damit Tree/Graph-Koordination, Scope, ScrollIntoView, Fit/Center und Request-Dispatch nicht von jedem Host nachgebaut werden müssen.
3. CardMeasurePolicy, Text-Wrap, Ellipsis und Größenvertrag vor Graphviz verbindlich planen und danach prototypisch umsetzen.
4. Scope-Policy, Link-Noise, Link-Routing und Viewport/Scrollbars aus der Product Demo in neutrale Workspace-/Policy-Regeln überführen.
5. SkiaGraphSurface-API/Bindings prüfen und entscheiden, was generischer Library-Vertrag wird und was Demo-/Host-Policy bleibt.
6. Layout-Alternativen nur bei Bedarf als Adapter-Vergleich prüfen; kein Toolwechsel ohne Step-Gate.
7. Legacy-GraphCanvas separat bewerten: behalten, archivieren oder nach Step-Gate entfernen.
8. Den allgemeinen Hostvertrag für Requests und Capabilities auf Skia-Hit-Testing, Interaktionsoverlays und GraphWorkspace anwenden.
9. Erst vor UserFlow-read-only den aktuellen UserFlow-Export liefern und dort ActionDefinition-Identität, Altprojekt-Migration, Editor-Identitätserhalt und UserFlow-View-State verbindlich entscheiden.
10. Erst danach die vorhandene `FlowView` read-only füllen.
11. Bearbeitung und neue Navigation erst nach dem read-only Abnahmetest aktivieren.

---


## 17. Übergabe an einen neuen Chat

### 17.1 Grundregel: Zwei getrennte Arbeitskontexte

`VIA.WPF.Graph` ist eine allgemeine Library. Sie kennt weder UserFlow-Typen noch UserFlow-Projekte, Collections, ViewModels, JSON-Formate oder ActionAreas. Diese Trennung gilt nicht nur für den fertigen Code, sondern auch für die Entwicklungsphasen 0 bis 9.

Deshalb wird die Informationsübergabe bewusst zweistufig durchgeführt:

| Arbeitsstufe | Phasen | Benötigte Artefakte | Nicht mitgeben |
|---|---:|---|---|
| Allgemeine Graph-Library | 0–9 | Masterplan und aktueller `VIA.WPF.Graph`-Quellstand | UserFlow-Export, UserFlow-Zip, alte Graphviz-Demo, VIALib-Quellen |
| UserFlow-Integration | nach Phase 9 | Masterplan, aktueller Stand der neuen `VIA.WPF.Graph`-Solution, aktueller UserFlow-VS2AI-Export | alte Graphviz-Demo; veraltete UserFlow-Exporte |
| spätere VIALib-Übernahme | nach produktiver UserFlow-Abnahme | zusätzlich aktueller VIALib-Export oder aktuelle VIALib-Solution | keine Annahmen über VIALib-Struktur |

Der frühere `GraphvizWpfDemo.zip` wird nie als technische Baseline übergeben. Er war ein explorativer Vorversuch; die neue Solution entsteht von Grund auf nach diesem Plan.

### 17.2 Startauftrag für Phasen 0 bis 6

Diesen Text **nur mit dem Masterplan** übergeben:

```text
Arbeite ausschließlich nach VIA.WPF.Graph_Masterplan.md.

Arbeite am aktuellen Skia-R7-Stand der allgemeinen VIA.WPF.Graph-Solution weiter. UserFlow ist zu diesem Zeitpunkt ausdrücklich kein Eingabeartefakt und darf weder referenziert noch als implizites Datenmodell angenommen werden.

Wichtig:
- Die neue VIA.WPF.Graph-Solution wird von Grund auf nach dem Masterplan aufgebaut.
- Das frühere Graphviz-Demo-Projekt ist keine technische Baseline und wird nicht verwendet.
- Verwende überall CommunityToolkit.Mvvm: ObservableObject, ObservableProperty, RelayCommand/AsyncRelayCommand; WeakReferenceMessenger nur für echte bereichsübergreifende Ereignisse.
- VIA.WPF.Graph.Core darf keine WPF-, Graphviz-, UserFlow- oder Host-Abhängigkeit erhalten.
- WPF-/Skia-Controls rendern, hit-testen und erzeugen nur neutrale Requests; sie mutieren keine Host-Daten.
- Keine neuen Datenmodell-Properties, IDs, Projekte, Packages oder Architekturentscheidungen ohne vorheriges Step-Gate.
- Jede Codeänderung nur auf Basis der aktuell bereitgestellten Dateien; komplette Dateien liefern, sofern kein Patch verlangt wird.
- Nach jeder Phase: lokal bauen, relevante Tests ausführen, Ergebnis und offene Punkte berichten und auf Freigabe warten.

Ziel der aktuellen Fortsetzung: SkiaGraphSurface, CardMeasurePolicy/Text-Wrap, Scope-Policy, Viewport/Scrollbars, Link-Routing und Cleanup gemäß Phase 7 stabilisieren. Erst danach UserFlow-read-only vorbereiten.
```

### 17.3 Übergang in die UserFlow-Integration

Erst nach Abschluss und Freigabe von Phase 9 wird ein **neuer Integrationskontext** eröffnet oder dem bestehenden Chat die folgenden Artefakte nachgereicht:

1. `VIA.WPF.Graph_Masterplan.md`.
2. Der aktuelle Quellstand der bis dahin erstellten `VIA.WPF.Graph`-Solution.
3. Der aktuelle `VIA UserFlow … .vs2ai.context.md`-Export.
4. Nur bei Bedarf ein aktueller VIALib-Export; dies ist nicht Teil der ersten UserFlow-Integration.

Dann lautet der Integrationsauftrag:

```text
Beginne mit Phase 7 aus VIA.WPF.Graph_Masterplan.md.

Die allgemeine VIA.WPF.Graph-Library bleibt vollständig UserFlow-frei. Implementiere jede UserFlow-Abbildung ausschließlich auf UserFlow-Seite in einem Adapter bzw. FlowView-Host. UserFlow referenziert VIA.WPF.Graph; die Abhängigkeitsrichtung darf niemals umgekehrt sein.

Prüfe zuerst den aktuellen UserFlow-Export gegen die Integrationsannahmen des Masterplans. Vor jeder Änderung an ActionDefinition-Identität, JSON/Clone/Snapshot, ActionArea-Editor, View-State oder Projektstruktur: Entscheidung dokumentieren und Step-Gate einholen.
```

### 17.4 Reihenfolge der Informationsübergabe

1. Für die allgemeine Library: ausschließlich den Masterplan und den Startauftrag aus Abschnitt 17.2 geben.
2. Nach jeder abgenommenen allgemeinen Library-/Demo-/Dokumentationsphase: ausschließlich den aktuellen Quellstand der neuen `VIA.WPF.Graph`-Solution zusätzlich geben.
3. Erst nach Phase-9-Abnahme: den aktuellen UserFlow-VS2AI-Export und den Auftrag aus Abschnitt 17.3 geben.
4. Bei UserFlow-Änderungen vor oder während der UserFlow-Integrationsphase: einen neuen Export bereitstellen, bevor der Adapter geändert wird.
5. Bei einer späteren Übernahme in VIALib: aktuellen VIALib-Export geben und die Zielstruktur ausdrücklich freigeben.

### 17.5 Was der neue Chat nicht annehmen darf

- Die frühere Demo sei kompilierbar, architektonisch passend oder zu übernehmen.
- Die allgemeine Library dürfe UserFlow referenzieren oder einen `VIA.WPF.Graph.UserFlow`-Bestandteil enthalten.
- `NavigateBack` habe eine persistierte Ziel-ID.
- Eine `ActionDefinition.Id` existiere bereits oder dürfe ohne Freigabe ergänzt werden.
- Der ActionArea-Editor unterstütze bereits mehrere Actions pro Trigger oder alle vorhandenen ActionTypes vollständig.
- Gruppen seien immer disjunkt oder für Collapse/Expand geeignet.
- Die Graphansicht dürfe direkt UserFlow-Collections ändern.
- Ein visueller Zustand sei persistent, nur weil er im WPF-Control aktuell sichtbar ist.


## 18. Definition of Done für die erste produktive UserFlow-Stufe

Die erste produktive Stufe gilt als fertig, wenn:

- FlowView links einen zyklensicheren Navigation Path Tree und rechts einen Graph zeigt.
- Screens und Popups aus den bestehenden UserFlow-Collections kommen.
- `GroupName`-Gruppen als Bereiche funktionieren.
- `Navigate`, `NavigateHome`, `NavigateBack`, `ShowPopup`, externe Actions und fehlende Ziele korrekt und ehrlich dargestellt werden.
- Auswahl Tree ↔ Graph ↔ bestehende UserFlow-Selektion synchronisiert ist.
- Zoom, Pan, Fit, Suche, Fokus und Bereichswechsel funktionieren.
- 100–150 Screens im Bereichs-/Drill-down-Modell nutzbar bleiben.
- keine Graph-UI UserFlow-Collections direkt verändert.
- jeder persistente UI-Zustand im Model/ViewModel liegt und nach Viewwechsel erhalten bleibt.
- Build, Referenztests, Altprojekt-Migration und Belastungstest dokumentiert sind.
- Parallelkanten, mehrere Roots und ungruppierte Screens sichtbar und fachlich nachvollziehbar bleiben.
- der aktuelle ActionArea-Editor bei editierbaren Linktypen die stabile Link-Identität nicht verliert.

---

## 19. Historische Revision 5 – frühere UserFlow-Read-only-Konkretisierung

Diese Revision bleibt als fachlicher Zielrahmen für die spätere UserFlow-read-only-Integration erhalten. Durch Revision 6 und Revision 7 ist sie jedoch nicht mehr die nächste Arbeitsphase. Die dort verwendeten P7-Bezeichnungen sind historische Bezeichnungen und dürfen für die aktuelle Skia-/Library-Finalisierung nicht als aktueller Arbeitsauftrag gelesen werden. Alle Architekturregeln, Abnahmebedingungen und Step-Gates bleiben fachlich maßgeblich.

### 19.1 Zweck von P7-001

P7-001 ist ein Integrationsentscheid vor Code. Er ist kein Implementierungsschritt und keine Freigabe für Datenmodell-, Persistenz-, Editor- oder Migrationsänderungen.

Ziel von P7-001 ist, den read-only Rahmen für die erste UserFlow-Integration verbindlich festzulegen:

- Adapter ausschließlich auf UserFlow-Seite,
- `VIA.WPF.Graph` bleibt unverändert UserFlow-frei,
- UserFlow referenziert `VIA.WPF.Graph.Core`, `VIA.WPF.Graph.Graphviz` und `VIA.WPF.Graph.Wpf`, niemals umgekehrt,
- keine neue `ActionDefinition.Id` in dieser späteren UserFlow-read-only-Stufe,
- keine JSON-, Clone-, Snapshot- oder Altprojekt-Migration in dieser späteren UserFlow-read-only-Stufe,
- keine Änderung am bestehenden ActionArea-Editor in dieser späteren UserFlow-read-only-Stufe,
- keine Action-Bearbeitung und keine neue Navigation in dieser späteren UserFlow-read-only-Stufe,
- `NavigateBack` wird read-only zunächst neutral als Rücknavigation dargestellt; ein konkretes Ziel darf nur angezeigt werden, wenn es aus dem aktuell sichtbaren Pfad eindeutig ableitbar ist,
- alle UserFlow-Daten bleiben Single Source of Truth in den bestehenden Collections.

### 19.2 Historische Teilissues für die spätere UserFlow-read-only-Integration

#### P7-001 – Integrationsentscheid und Schnitt festlegen

Umfang:

- aktuellen Masterplan, aktuellen `VIA.WPF.Graph`-Stand und aktuellen UserFlow-Export gegen die spätere UserFlow-read-only-Stufe prüfen,
- konkreten Adapterort in der UserFlow-Solution benennen,
- erlaubte Projektverweise festlegen,
- read-only Capability-Profil festlegen,
- Abbildungsregeln für Screens, Popups, ActionAreas und ActionDefinitions festlegen,
- Rebuild-/Invalidierungsstrategie grob festlegen,
- Nicht-Ziele und Stopppunkte dokumentieren.

Nicht enthalten:

- keine Implementierung,
- keine neuen UserFlow-Properties,
- keine neuen persistenten IDs,
- keine neuen Datenmodelle für Persistenz,
- keine Editoränderung,
- keine Änderung an `VIA.WPF.Graph` wegen UserFlow.

Step-Gate:

- Freigabe des read-only Integrationsschnitts vor P7-002.

#### P7-002 – Read-only UserFlow-Graphprojektion

Umfang:

- UserFlow-seitiger Adapter erzeugt ein neutrales `GraphDocument`,
- Screens werden zu Standard-Knoten,
- Popups werden zu Popup-Knoten,
- Screen-`GroupName` wird zu Screen-Container-Gruppen,
- Popup-`GroupName` wird zu Popup-Container-Gruppen oder neutralen Popup-Bereichen gemäß P7-001,
- fehlende Gruppen werden nur in der Projektion als stabile synthetische Gruppe behandelt,
- `ActionArea.Actions` erzeugen Links,
- `Navigate`, `NavigateHome`, `NavigateBack`, `ShowPopup`, `OpenURL`, `OpenFile` und ungültige Ziele werden sichtbar und fachlich ehrlich abgebildet,
- Link-Metadaten enthalten nur transiente Rückverweise für Auswahl und spätere Zuordnung, keine zweite fachliche Collection.

Nicht enthalten:

- keine Mutation von `Project.Screens`, `Project.Popups`, `ActionArea.Actions` oder `ActionDefinition`,
- keine persistente Link-ID-Migration,
- keine Action-Bearbeitung.

Step-Gate:

- GraphDocument wird aus Originalcollections erzeugt und bleibt read-only.

#### P7-003 – Layout, Tree und Flow-Host-ViewModel

Umfang:

- Flow-spezifisches ViewModel nach CommunityToolkit.Mvvm-Mustern,
- hostseitiger Besitz von `GraphDocument`, `GraphLayoutResult`, `GraphTreeProjection` und View-State,
- Layout über `VIA.WPF.Graph.Graphviz`,
- Tree-Projektion über `VIA.WPF.Graph.Core`,
- read-only `GraphRequestCommand` für Auswahl, Öffnen, Fokus, Collapse/Expand und Rückkehr zur Übersicht,
- abgelehnte oder nicht unterstützte Requests ändern keine UserFlow-Daten.

Nicht enthalten:

- keine Graphmutation,
- keine Undo-/Redo-Anbindung,
- keine Persistenz des GraphViewState, sofern nicht separat freigegeben.

Step-Gate:

- Host-ViewModel kann aus dem aktuellen Projekt einen stabilen read-only Graph- und Tree-Zustand erzeugen.

#### P7-004 – FlowView-Hybridansicht

Umfang:

- bestehende `FlowView` wird als Integrationspunkt genutzt,
- links `GraphNavigationPathTree`, rechts `SkiaGraphSurface`,
- beide Controls binden ausschließlich an hostseitigen Zustand,
- Zoom, Pan, Fit, Auswahl, aktiver Bereich und Tree-Expand-Zustand bleiben im Flow-Host-ViewModel,
- Viewwechsel darf den Graphzustand nicht zerstören.

Nicht enthalten:

- kein neuer allgemeiner Graph-Renderer,
- keine UserFlow-Typen in `VIA.WPF.Graph.Wpf`,
- keine direkte Mutation aus Tree oder SkiaGraphSurface.

Step-Gate:

- Hybridansicht zeigt UserFlow read-only und bleibt beim Viewwechsel stabil.

#### P7-005 – Read-only Synchronisierung mit bestehender UserFlow-UI

Umfang:

- Auswahl im Tree setzt hostseitig den passenden Screen-/Popup-Fokus,
- Auswahl im Graph setzt hostseitig den passenden Screen-/Popup-Fokus,
- bestehende Screen-/Popup-Auswahl aktualisiert Graph- und Tree-Selektion,
- `HomeScreen` wird als Root-/Startinformation genutzt,
- Preview-Navigation darf als Fokus-/Öffnungswunsch genutzt werden, aber nicht als Datenmutation,
- fehlende Ziele und externe Aktionen bleiben sichtbar statt entfernt zu werden.

Nicht enthalten:

- keine Bearbeitung bestehender Actions,
- keine neue Navigation,
- keine neue Preview-Semantik.

Step-Gate:

- Tree, Graph und bestehende UserFlow-Auswahl sind read-only synchron.

#### P7-006 – Abnahme und Tests

Umfang:

- Build der betroffenen Solution,
- Smoke-Test mit Screens, Popups und Gruppen,
- Testfälle für `Navigate`, `NavigateHome`, `NavigateBack`, `ShowPopup`, `OpenURL`, `OpenFile`, ungültige `TargetScreenId` und ungültige `PopupId`,
- Prüfung: keine UserFlow-Collection wurde durch Graph/Tree geändert,
- Prüfung: Viewwechsel zerstört Graphzustand nicht,
- Prüfung: `VIA.WPF.Graph` bleibt UserFlow-frei.

Step-Gate:

- fachliche Freigabe der read-only UserFlow-Navigationsansicht. Erst danach darf Phase 8 geplant werden.

### 19.3 Stopppunkte innerhalb der späteren UserFlow-read-only-Integration

Vor Umsetzung ist ausdrücklich anzuhalten, wenn einer der folgenden Punkte notwendig erscheint:

- neue UserFlow-Property,
- neue persistente ID,
- JSON-/Clone-/Snapshot-Anpassung,
- Altprojekt-Migration,
- Änderung am ActionArea-Editor,
- neue UserFlow-Datei oder neues Projekt mit architektonischer Relevanz,
- neue Dependency,
- Änderung an `VIA.WPF.Graph.Core`, `.Graphviz` oder `.Wpf` wegen UserFlow,
- direkte Mutation von `Project.Screens`, `Project.Popups`, `ActionArea.Actions` oder `ActionDefinition` aus Graph oder Tree.

### 19.4 Nichtziele der späteren UserFlow-read-only-Integration

Nicht Bestandteil der read-only diese spätere UserFlow-read-only-Stufe:

- bestehende Actions öffnen, ändern oder löschen,
- neue Navigation anlegen,
- neue ActionAreas erzeugen,
- `ActionDefinition.Id` einführen,
- GraphViewState persistieren,
- Altprojekte migrieren,
- manuelle Knotenpositionen speichern,
- ActionArea-Editor erweitern,
- mehrere Actions pro Trigger fachlich freigeben,
- `NavigateBack` als statischen Zielscreen behaupten.

### 19.5 Verhältnis zu Phase 8 und Phase 9

Die spätere UserFlow-read-only-Integration liefert nur die read-only Navigationsansicht. Erst nach ihrer Abnahme werden UserFlow-Bearbeitung und neue Navigation betrachtet.

Phase 8 benötigt vor Code weiterhin gesonderte Entscheidungen zu:

- stabiler `ActionDefinition.Id`,
- JSON-/Clone-/Snapshot-Erhalt,
- ActionArea-Editor-Identitätserhalt,
- Altprojekt-Migration,
- Undo-/Redo-Verhalten,
- erlaubten editierbaren ActionTypes,
- Behandlung von `NavigateBack`, Popup-Schließen und Mehrfachaktionen pro Trigger.

Phase 9 bleibt weiterhin die spätere Stufe für neue Navigation aus Graph oder Tree.

---

# 20. Revision 6 – Library-first-Reihenfolge ab Phase 7

## 20.1 Anlass

Nach Abschluss der allgemeinen Library-Phasen 0 bis 6 wurde die UserFlow-Integration bewusst ans Ende verschoben. Der Skia-R7-Stand konkretisiert nun die technische Renderbasis für die Library-Finalisierung.

Begründung:

- `VIA.WPF.Graph` soll zuerst als vollständig autarke, allgemeine WPF-/Skia-Graph-Library finalisiert werden.
- UserFlow darf die Library-Architektur nicht zu früh prägen.
- Die Library soll vor der ersten Domänenintegration API-, Host-, Demo-, Dokumentations- und Packaging-reif sein.
- Das GitHub Project Board ist nicht mehr verbindliche Steuerung. Der Masterplan bleibt Single Source of Truth.

Diese Revision ersetzt nicht den bisherigen Masterplantext, sondern legt ab jetzt eine neue verbindliche Arbeitsreihenfolge fest. Frühere Phase-7-/8-/9-/10-Beschreibungen bleiben als fachliche Inhalte erhalten, werden aber durch diese Revision zeitlich nach hinten verschoben.

## 20.2 Neue verbindliche Phasenreihenfolge ab Phase 7

| Neue Phase | Titel | Inhalt |
|---|---|---|
| Phase 7 | Library-Finalisierung und Host-Integrationsreife | SkiaGraphSurface als aktive Renderbasis, GraphWorkspace als Host-Onboarding-Schicht, öffentliche API, Hostvertrag, Request-/Result-Verhalten, View-State-Bindings, Scope-Policy, CardMeasurePolicy, Text-Wrap, allgemeine Synchronisierung und Host-Neutralität härten. |
| Phase 8 | Allgemeine UX-/Demo-Abnahme | Product Demo ohne UserFlow verbessern, Skia-Hybridansicht absichern, Navigation, Fokus, Zoom/Pan/Scrollbars, Tree+Graph-Verhalten und größere Demo-Abnahme finalisieren. |
| Phase 9 | Dokumentation, Packaging und Host-Onboarding | Integrationsdokumentation, Paket-/Referenzstruktur, Host-Beispiele, Übergabehinweise und Release-Vorbereitung erstellen. |
| Phase 10 | UserFlow read-only Adapter und FlowView | Bisherige UserFlow-read-only-Phase. Visualisierung aktueller UserFlow-Daten ohne Mutation. |
| Phase 11 | UserFlow-Bearbeitung bestehender Actions | Bisherige Phase zur Bearbeitung vorhandener UserFlow-Actions. |
| Phase 12 | UserFlow neue Navigation | Bisherige Phase zur Erstellung neuer Navigation aus Graph/Tree. |
| Phase 13 | Optionale Erweiterungen | Bisherige optionale Erweiterungen. |

## 20.3 Neue Phase 7 – Library-Finalisierung und Host-Integrationsreife

### P7-001 – Masterplan auf Library-first- und Skia-Reihenfolge umstellen

Ziel:

- Masterplan verbindlich auf Library-first-Reihenfolge umstellen.
- SkiaGraphSurface als aktive Renderbasis dokumentieren.
- GitHub Project Board aus der verbindlichen Steuerung entfernen.
- UserFlow an das Ende verschieben.

Abnahme:

- Masterplan enthält Revision 7.
- Frühere Inhalte bleiben erhalten.
- Neue Reihenfolge und Skia-Rolle sind eindeutig.
- Codeänderungen beschränken sich auf bereinigende Demo-/Dokumentationsanpassungen.

### P7-002 – Public API und Hostvertrag prüfen

Ziel:

- Öffentliche API und Hostvertrag auf Integrationsreife prüfen.
- Neutralität von Core, Graphviz und WPF sicherstellen.
- Request-/Result-Vertrag, Host-Capabilities und Editierbarkeitsmodell fachlich prüfen.
- Keine UserFlow-Begriffe oder UserFlow-Abhängigkeiten einführen.

Abnahme:

- API-Oberfläche ist nachvollziehbar.
- Hostpflichten sind klar.
- Mutationsgrenze bleibt beim Host.
- Keine domänenspezifischen Typen in der Library.

### P7-003 – SkiaGraphSurface/View-State-Bindings härten

Ziel:

- Allgemeine Bindings für Zoom, Pan, Auswahl, Gruppen, ViewMode, LayoutBounds und RequestCommand prüfen.
- Rebuild-Stabilität allgemein absichern.
- View-State weiterhin host-owned halten.
- Scope-Policy und Viewport/Scrollbar-Verhalten von Persistenz und Hostmodell trennen.
- Keine projektspezifische Persistenz einführen.
- Größen-, Wrap- und Card-Messregeln als eigene Policy von View-State und Hostpersistenz trennen.

Abnahme:

- SkiaGraphSurface bleibt hostneutral.
- View-State kann vom Host gehalten und wieder angebunden werden.
- Keine Spiegelkopien oder verdeckte Synchronisationslisten entstehen.
- Legacy-GraphCanvas wird nicht weiter optisch ausgebaut.

### P7-003a – GraphCardMeasurePolicy, Text-Wrap und Größenvertrag

Ziel:

- Einen verbindlichen Größenvertrag vor dem Graphviz-Layout festlegen.
- Titel, Subtitle, Typzeile und optionale Metadaten mit Wrap-/Ellipsis-Regeln messen.
- Pro ViewMode und NodeKind Mindest-/Maximalgrößen definieren.
- `GraphLayoutNode.Bounds` als Layout-Ergebnis behandeln und nicht nachträglich wegen Textüberlauf vergrößern.
- Core bleibt frei von WPF-, Skia- und Font-Messlogik.
- Keine neue Layoutengine einführen.

Abnahme:

- Standard-, Popup-, Stub- und Detail-Karten schneiden Text nicht hart ab.
- Wrap-Regeln sind reproduzierbar und lösen bei Größenänderung einen kontrollierten Rebuild aus.
- Graphviz erhält dieselben Card-Größen, die Skia anschließend rendert.
- Gruppen-, Link-, Hit-Test- und Scroll-Extents bleiben konsistent.
- MSAGL/QuikGraph bleiben nur dokumentierte spätere Prüfoptionen, keine neue Dependency.

### P7-003b – GraphWorkspace-Zielentscheidungen festlegen

Ziel:

- Eine wiederverwendbare Workspace-/Navigator-Schicht als Standardintegration für Hosts verbindlich planen.
- Festlegen, dass `GraphWorkspace` als fertiges WPF-Control mit integriertem, optional abschaltbarem Navigation Tree und `SkiaGraphSurface` umgesetzt wird.
- Festlegen, dass die Library sichtbare Scopes und Layouts aus dem vollständigen `GraphDocument` erzeugt.
- Festlegen, dass `Group Compact` Standard bei Gruppenauswahl ist und `Group Full` nur explizit aktiviert wird.
- Festlegen, dass der Inspector hostseitig bleibt.
- Festlegen, dass Layout-/Routing-Umschaltung erst nach stabiler ScopePolicy umgesetzt wird.
- Festlegen, dass Legacy-`GraphCanvas` im nächsten Code-Patch entfernt wird.
- Keine UserFlow-Typen, keine Hostmodell-Mutation und keine Persistenzentscheidung in die Library ziehen.

Abnahme:

- Masterplan beschreibt die Workspace-Verantwortung eindeutig.
- Hostpflichten und Librarypflichten sind getrennt.
- Standard-Scope-Regeln sind konkret definiert.
- `MaxVisibleNodes` für `Group Compact` ist auf 20 festgelegt.
- Die Product Demo bleibt Erprobungsort, aber nicht das nachzubauende Integrationsmuster.
- Kein Code-/API-Commit führt neue öffentliche `GraphWorkspace`-Typen ohne eigenes Step-Gate ein.

### P7-003c – Legacy-GraphCanvas entfernen

Ziel:

- Alte WPF-`GraphCanvas`-Bestände entfernen, nachdem `SkiaGraphSurface` als aktive Renderbasis und GraphWorkspace-Zielrichtung festgelegt sind.
- Entfernen der nicht mehr aktiven Legacy-Control-Dateien und zugehörigen Tests, sofern keine kompilierten Referenzen mehr bestehen.
- Keine Änderung am Skia-Rendering, an Demo-Scopes oder an öffentlichem Hostvertrag in diesem Cleanup.

Abnahme:

- Solution baut nach Entfernung.
- Tests sind grün oder bewusst angepasste Legacy-Tests wurden entfernt.
- Product Demo nutzt weiterhin ausschließlich `SkiaGraphSurface`.
- Keine `GraphCanvas`-Referenzen verbleiben außerhalb historischer Dokumentation.

### P7-004 – NavigationPathTree/Graph-Sync allgemein absichern

Ziel:

- Tree/Graph-Synchronisierung ohne Domänenannahmen prüfen.
- GraphWorkspace-/Policy-Regeln für Tree-Auswahl, sichtbaren Graph-Scope und ScrollIntoView auf Basis der festgelegten Standard-Scope-Regeln vorbereiten.
- Selection, OpenNode, OpenGroup, ClearSelection und ReturnToOverview allgemein absichern.
- Zyklensichere Projektion und Referenzknoten-Verhalten prüfen.

Abnahme:

- Tree und Graph arbeiten über neutrale IDs und Requests.
- Standard-Hosts müssen ScrollIntoView, Focus-Graph und Tree-Pfadöffnung nicht selbst nachbauen.
- Kein Hostmodell wird direkt mutiert.
- Zyklische Graphen bleiben sicher darstellbar.

### P7-005 – Product Demo als allgemeine Skia-Referenz verbessern

Ziel:

- Product Demo als allgemeine Referenz für spätere Hosts verbessern.
- Read-only/editable-Modus, Requestfeedback, Hybridansicht, Skia-Scopes und allgemeine Testdaten klar darstellen.
- Keine UserFlow-Daten verwenden.
- Link-Routing, Link-Noise, Gruppenflächen, Kartenlayout, Wrap und Textdichte weiter stabilisieren.
- Bei Bedarf dokumentierten Vergleichspunkt für alternative Layoutadapter vorbereiten, aber keine neue Dependency ohne Step-Gate einführen.

Abnahme:

- Demo zeigt typische Host-Integration ohne Domäne.
- Demo erklärt die Grenzen zwischen Library und Host.
- Skia-Graphfläche ist die aktive Product-Demo-GraphArea.
- Allgemeine UX ist belastbar genug für spätere Integration.

### P7-006 – Phase-7-Abnahme: Build, Tests, Host-Neutralität

Ziel:

- Phase 7 abschließen.
- Build und Tests ausführen.
- Host-Neutralität prüfen.
- Keine UserFlow-Vorwegnahme.

Abnahme:

- `dotnet test` ist erfolgreich.
- Keine neuen UserFlow-Abhängigkeiten existieren.
- Öffentliche API, SkiaGraphSurface und Demo sind konsistent.
- Phase 8 wird erst nach ausdrücklicher Freigabe gestartet.

## 20.4 Verbindliche Nicht-Ziele bis einschließlich Phase 9

Bis Phase 9 werden nicht umgesetzt:

- UserFlow-Adapter
- FlowView-Integration
- UserFlow-spezifische ViewModels
- ActionDefinition-ID
- JSON-/Clone-/Snapshot-Migrationen
- UserFlow-Editoränderungen
- Retargeting von UserFlow-Actions
- neue UserFlow-Navigation
- neue UserFlow-ActionAreas
- UserFlow-spezifische Persistenz
- ein `VIA.WPF.Graph.UserFlow`-Projekt innerhalb der allgemeinen Graph-Library

## 20.5 Arbeitssteuerung ohne GitHub Project Board

Ab Revision 6 gilt:

- Der Masterplan ist die fachliche und architektonische Single Source of Truth.
- GitHub Issues und GitHub Project Board sind nicht mehr verbindliche Arbeitssteuerung.
- Arbeitsschritte werden direkt aus dem Masterplan abgeleitet.
- Git bleibt weiterhin technische Historie.
- Nach jeder Phase bzw. jedem freigegebenen Arbeitspaket wird gestoppt.

