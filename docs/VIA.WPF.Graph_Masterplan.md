# VIA.WPF.Graph – Masterplan für allgemeine Graph-Visualisierung und spätere UserFlow-Integration

**Status:** geprüfter Architektur- und Entwicklungsplan, noch keine Implementierungsfreigabe je Phase  
**Ziel:** Allgemein nutzbare Graph-Visualisierung für WPF in der Solution `VIA.WPF.Graph`, zunächst als unabhängiges Test-/Prototyp-Projekt, danach schrittweise Integration in VIA UserFlow.  
**Layout-Engine:** `Rubjerg.Graphviz` **3.0.5** / Graphviz `dot`.  
**MVVM-Standard:** überall CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `[AsyncRelayCommand]`, bei Bedarf `ObservableRecipient` und `WeakReferenceMessenger`).  
**Arbeitsregel:** Jede Phase endet mit lokaler Build-/Funktionsprüfung und explizitem Step-Gate.

**Revision 4 – Vollständigkeitsprüfung, Übergabe und strikte Host-Isolation:** Dieser Stand enthält zusätzlich die bisher besprochenen Punkte zu Tree/Graph-Hybridansicht, Karten-Badges für Rückwege, Popup-Darstellung, allgemeinen Knoten/Links/Gruppen, überlappenden Gruppen, Collapse/Expand, Bearbeitung über ActionAreas, CommunityToolkit.Mvvm, Persistenz, Altprojekt-Migration, der aktuellen Einschränkung des ActionArea-Editors sowie einen vollständigen Übergabeauftrag für einen neuen Chat. Die frühere Graphviz-Demo ist ausdrücklich keine technische Baseline.

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
   Graph-Canvas und Tree senden nur klar typisierte Interaktions- beziehungsweise Änderungsanfragen. Ausschließlich der Host verarbeitet diese und ändert seine Modelle/Collections.

3. **Single Source of Truth.**  
   Bei UserFlow bleiben `Project.Screens`, `Project.Popups`, `ActionArea.Actions` und deren ActionDefinitions die alleinigen Quellen für Navigation. Es entstehen keine synchron gehaltenen Navigationscollections.

4. **Lesbarkeit vor Vollständigkeit.**  
   Ein Gesamtgraph mit 100–150 Screens ist Diagnoseansicht, nicht Standardarbeitsansicht. Die Standardansicht kombiniert Tree, Bereichsfluss und fokussierte Graphdarstellung.

5. **Graphviz nur als Layout-Engine.**  
   Graphviz bestimmt Knotenpositionen, Cluster-Bounds und Kantenverläufe. WPF rendert Karten, Labels, Badges, Auswahl, Zoom, Pan, Hit-Testing und Interaktionen vollständig selbst.

6. **WPF-spezifische Teile bleiben getrennt.**  
   Das neutrale Graphmodell darf keine WPF-, SkiaSharp-, Graphviz- oder UserFlow-Abhängigkeit enthalten.

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
| Anordnen, Zoomen und Gruppen sichtbar machen | GraphCanvas, Graphviz-Layout, Bereichs- und Gruppenprojektionen |
| Auswahl einzelner/mehrerer Gruppen | Gruppenselektion, Mehrfachauswahl, Fokus und Filter |
| Collapse/Expand | ausschließlich für disjunkte/hierarchische Container-Gruppen |
| Tree links, Gesamtgraph rechts | Hybridansicht mit synchroner Auswahl |
| Oder-Verzweigungen | Tree-Siblings als Alternativen; kein doppeltes Rendern von Rückzielen |
| Back-Pfeil und Badge in der Karte | Back-Footer/Badge, bei UserFlow nur mit fachlich ehrlicher Zielangabe |
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
  GraphCanvas, Zoom/Pan, Knoten- und Kanten-Templates,
  Tree-/Pfadansicht, Auswahl, Suche, Fokus, Interaktions-Requests

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
VIA.WPF.Graph.Wpf       -> VIA.WPF.Graph.Core
VIA.WPF.Graph.Demo      -> VIA.WPF.Graph.Core, VIA.WPF.Graph.Graphviz, VIA.WPF.Graph.Wpf

VIA.WPF.Graph.Graphviz <-> VIA.WPF.Graph.Wpf                 verboten
Alle VIA.WPF.Graph.*-Projekte -> UserFlow / Mockup           verboten
```

Die Demo ist ausschließlich der Composition Root. Sie darf Graphviz-Layout und WPF-Renderer zusammensetzen. Der WPF-Renderer erhält weder eine Projekt- noch eine Paketabhängigkeit zu Graphviz; Graphviz bleibt ausschließlich Layoutadapter.

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
    -> WPF-Renderer
```

Graphviz liefert:

- Knoten-Bounding-Boxes,
- Cluster-Bounds,
- Kanten-Splines/Point-Folgen,
- Richtung und Layering,
- Kreuzungsreduktion innerhalb der verfügbaren Layoutregeln.

WPF liefert:

- Knoten-Karten,
- Popups als eigene Templates,
- Icons, Badges und Action-Labels,
- Kantenstile,
- Auswahl/Hover/Fokus,
- TreeView/Pfadansicht,
- Zoom, Pan, Fit, Suche,
- hit-testbare Kanten und Karten.

### 6.1.1 Größen- und Koordinatenvertrag

Graphviz benötigt Knotengrößen vor dem Layout. Daher arbeitet jede Ansicht zunächst mit festen, je Template definierten Größenprofilen (`Compact`, `Standard`, `Detail`, `Popup`, `Stub`). Dynamisch gemessene WPF-Karten dürfen erst nach einem kontrollierten Rebuild Einfluss auf das Layout nehmen; sie dürfen nicht stillschweigend andere Graphviz-Maße verwenden.

Der Graphviz-Adapter kapselt die Umrechnung zwischen DOT-/Point-Koordinaten und WPF-DIPs an genau einer Stelle. Alle Bounds, Splines, Zoom- und Hit-Test-Berechnungen verwenden danach nur noch dieses gemeinsame WPF-Koordinatensystem.

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

### 6.5 Fehler- und Fallbackverhalten

- Layoutfehler dürfen die Anwendung nicht schließen.
- Der Adapter liefert einen kontrollierten Fehlerzustand mit ursprünglichem Graphdokument und diagnostischer Meldung.
- Fehlt eine Kanten-Geometrie, zeichnet der WPF-Renderer eine gerade Fallback-Kante zwischen den Knotenzentren.
- Nicht auflösbare Ziele erscheinen als markierte externe oder fehlerhafte Stubs; sie werden nicht stillschweigend entfernt.

---

## 7. WPF-Darstellung und Interaktion

### 7.1 Rechter Bereich: GraphCanvas

Der GraphCanvas wird ein eigenes WPF-Control, kein fremder Graphviewer.

Funktionen der ersten produktiven Ausbaustufe:

- Zoom per Mausrad und Zoom-Slider,
- Pan über freie Fläche,
- Fit-to-Graph,
- Knoten-Auswahl,
- Kanten-Auswahl,
- Mehrfachauswahl per Modifier,
- Gruppenauswahl,
- Suche und Zentrieren,
- Fokusmodus mit ausgegrautem Restgraph,
- Auswahl synchron zum Tree,
- Kontextmenü über hostseitige Commands.

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
- Popup-/Overlay-Badge,
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
| Screen | Standardkarte mit Titel, optional Description und Actionanzahl |
| Popup | kleinere Overlay-Karte |
| Bereich | Cluster-/Bereichskarte in Übersicht |
| Extern/Stub | reduzierte Karte für Übergang außerhalb der aktuellen Projektion |
| Referenz | Tree-only oder reduzierte Graphmarkierung |

Screen-Vorschauen aus UserFlow werden erst nach einer stabilen Basis ergänzt. Sie sind kein Blocker für Architektur, Navigation oder Skalierung.

### 7.6 Kantenlabels und visuelle Dichte

- Detail- und Bereichsansichten zeigen Actionlabels direkt an der Kante oder als nahes Label-Chip.
- Gesamt- und Diagnoseansichten reduzieren Labels abhängig von Zoom und Dichte.
- Bei Parallelkanten wird im kompakten Modus ein Bündel-Badge gezeigt; die Einzellinks bleiben auswählbar.
- Back-, Cancel-, Popup- und External-Links bleiben auch ohne volles Label an Pfeilform, Badge und Linienstil erkennbar.
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

Der allgemeine Graph sendet nur Requests wie:

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

Der Host entscheidet, ob der Request unterstützt wird. Ein generischer Graph muss keinerlei fachliche Mutation kennen.

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

### Phase 3 – VIA.WPF.Graph.Wpf: Canvas-Grundlage

**Ziel:** Vollständig eigener WPF-Renderer auf Basis des Layout-Ergebnisses.

**Umfang:**

- GraphCanvas mit Knoten- und Kantenlayer,
- Standard-, Compact-, Detail-, Popup- und Stub-Templates,
- Zoom, Pan, Fit-to-Graph,
- Knoten-/Kanten-Hit-Test,
- Einzel- und Mehrfachauswahl,
- Hover, Fokus, Suchzentrierung, Drill-down in Bereiche und Rückkehr zur Übersicht,
- Commands und hostseitig gebundener View-State,
- keine direkte Hostmutation.

**Abnahme:**

- Mouse Wheel zoomt, freie Fläche verschiebt,
- Auswahl bleibt beim Rebuild logisch erhalten, sofern Id noch vorhanden,
- Canvas-Größe wird aus Layoutbounds abgeleitet,
- WPF-Control enthält keinen Graphviz-Code.

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
- Klick im Graph öffnet/selektiert den Tree-Pfad,
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
- Gesamtgraph ist als Diagnoseansicht brauchbar.

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

### Phase 7 – UserFlow read-only Adapter und FlowView

**Ziel:** Aktuelle UserFlow-Daten werden ohne Mutation visualisiert.

**Voraussetzungen:**

- Phasen 0 bis 6 sind abgenommen.
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

### 13.3 WPF-Funktionstests

- Zoom/Pan/Fit,
- Knoten-/Kantenauswahl,
- Tree/Graph-Synchronisierung,
- Search/Fokus,
- Collapse/Expand,
- Mehrfachgruppen-Auswahl,
- Tastaturfokus und Accessibility-Basis,
- Label-Ausblendung/Bündelung ohne Verlust der Link-Auswahl.

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
| 150 Detailkarten auf einmal | Bereichs-/Fokusansichten als Standard, Compact-Modus im Vollgraph |
| Graphviz-Layout ändert sich nach Datenänderung | Selection an stabile IDs binden, nicht an Koordinaten |
| manueller Layoutwunsch zu früh | erst nach akzeptierter automatischer Darstellung separat planen |
| UI-Control mutiert Fachmodell | ausschließlich Requests/Commands, keine Collection-Referenzen im Renderer |
| parallele Rebuilds | generation-id/cancellation und atomarer Austausch des Layout-Ergebnisses |
| Parallelkanten verschwinden oder Labels überlagern sich | Links immer über eigene Link-ID behandeln; Bündelung nur als reine Darstellung |
| neue Action-ID wird vom bestehenden Editor verloren | ID durch ActionRow laden/speichern; Clone/Snapshot/JSON testen |
| Altprojekte brechen nach Modellergänzung | rückwärtskompatibler Default und definierte Migrationsregel |
| ungruppierte Screens verschwinden aus Bereichsansichten | stabile synthetische Gruppe „Ohne Gruppe“ nur in der Projektion |
| früherer Demo-Code wird als technische Baseline missverstanden | Demo nicht übergeben und nicht übernehmen; neue Solution entsteht ausschließlich nach diesem Plan |

---

## 15. Offene Entscheidungen vor Umsetzung

1. **ActionDefinition-ID:** Darf eine persistente ID ergänzt werden? Empfehlung: ja.
2. **Popup schließen:** Wird eine explizite `ClosePopup`-Semantik benötigt oder zunächst nicht dargestellt?
3. **Rücknavigation:** Soll `NavigateBack` ausschließlich generisch „Zurück“ anzeigen oder im ausgewählten Tree-Pfad den abgeleiteten Zielnamen nennen?
4. **Bereichsmodell:** Werden Screen- und Popup-Gruppen getrennt geführt oder gibt es eine fachliche Zuordnung zwischen ihnen?
5. **Persistenz des View-State:** Welche States sind pro Projekt zu speichern?
6. **`VIA.WPF.Graph`-Struktur:** In welche bestehende Solution/Repository-Struktur werden `VIA.WPF.Graph.*`-Projekte aufgenommen?
7. **Graphdesign:** Welche bestehenden `VIA.WPF.Graph`-/UserFlow-Styles, Brushes und Iconpacks sollen die neuen Templates verwenden?
8. **Bearbeitung:** Darf die Graphansicht später neue ActionAreas anlegen oder zunächst nur bestehende Actions ändern?
9. **Deployment:** Welche Runtime-Architektur wird offiziell unterstützt: `win-x64` nur oder zusätzlich andere Runtimes?
10. **Bereichscollapse:** Soll ein kollabierter Bereich nur Übergangszahlen oder auch die wichtigsten ausgehenden Actionlabels zeigen?
11. **ActionArea-Editor:** Bleibt V1 bei exakt einer Action pro Trigger, und wird die ID-Übernahme dort verbindlich umgesetzt?
12. **Mehrfachkanten:** Sollen sie im Standardgraph als Einzellinien oder ab einer Dichte als Bündel mit Anzahl angezeigt werden?
13. **Ungruppierte Screens:** Ist „Ohne Gruppe“ als reine Projektion fachlich akzeptiert?
14. **Größenvertrag:** Welche festen Kartenmaße gelten je Template, bevor dynamische Größenmessung später zugelassen wird?

---

## 16. Empfohlene Reihenfolge der nächsten konkreten Schritte

1. Phase 0 durchführen: die bestehende `VIA.WPF.Graph.slnx`, Rubjerg-Runtime und den neutralen Datenmodellrahmen prüfen; keine technische Alt-Demo übernehmen und keine Host-spezifischen Entscheidungen treffen.
2. VIA.WPF.Graph.Core und VIA.WPF.Graph.Graphviz ohne UserFlow-Abhängigkeit aufbauen.
3. WPF-Canvas und Navigation Path Tree im Demo-Projekt validieren.
4. Den allgemeinen Hostvertrag für Requests und Capabilities abnehmen.
5. Erst vor Phase 7 den aktuellen UserFlow-Export liefern und dort ActionDefinition-Identität, Altprojekt-Migration, Editor-Identitätserhalt und UserFlow-View-State verbindlich entscheiden.
6. Erst danach die vorhandene `FlowView` read-only füllen.
7. Bearbeitung und neue Navigation erst nach dem read-only Abnahmetest aktivieren.

---


## 17. Übergabe an einen neuen Chat

### 17.1 Grundregel: Zwei getrennte Arbeitskontexte

`VIA.WPF.Graph` ist eine allgemeine Library. Sie kennt weder UserFlow-Typen noch UserFlow-Projekte, Collections, ViewModels, JSON-Formate oder ActionAreas. Diese Trennung gilt nicht nur für den fertigen Code, sondern auch für die Entwicklungsphasen 0 bis 6.

Deshalb wird die Informationsübergabe bewusst zweistufig durchgeführt:

| Arbeitsstufe | Phasen | Benötigte Artefakte | Nicht mitgeben |
|---|---:|---|---|
| Allgemeine Graph-Library | 0–6 | nur `VIA.WPF.Graph_Masterplan.md` | UserFlow-Export, UserFlow-Zip, alte Graphviz-Demo, VIALib-Quellen |
| UserFlow-Integration | ab 7 | Masterplan, aktueller Stand der neuen `VIA.WPF.Graph`-Solution, aktueller UserFlow-VS2AI-Export | alte Graphviz-Demo; veraltete UserFlow-Exporte |
| spätere VIALib-Übernahme | nach produktiver UserFlow-Abnahme | zusätzlich aktueller VIALib-Export oder aktuelle VIALib-Solution | keine Annahmen über VIALib-Struktur |

Der frühere `GraphvizWpfDemo.zip` wird nie als technische Baseline übergeben. Er war ein explorativer Vorversuch; die neue Solution entsteht von Grund auf nach diesem Plan.

### 17.2 Startauftrag für Phasen 0 bis 6

Diesen Text **nur mit dem Masterplan** übergeben:

```text
Arbeite ausschließlich nach VIA.WPF.Graph_Masterplan.md.

Starte mit Phase 0 der allgemeinen VIA.WPF.Graph-Solution. UserFlow ist zu diesem Zeitpunkt ausdrücklich kein Eingabeartefakt und darf weder referenziert noch als implizites Datenmodell angenommen werden.

Wichtig:
- Die neue VIA.WPF.Graph-Solution wird von Grund auf nach dem Masterplan aufgebaut.
- Das frühere Graphviz-Demo-Projekt ist keine technische Baseline und wird nicht verwendet.
- Verwende überall CommunityToolkit.Mvvm: ObservableObject, ObservableProperty, RelayCommand/AsyncRelayCommand; WeakReferenceMessenger nur für echte bereichsübergreifende Ereignisse.
- VIA.WPF.Graph.Core darf keine WPF-, Graphviz-, UserFlow- oder Host-Abhängigkeit erhalten.
- WPF-Controls rendern, hit-testen und erzeugen nur neutrale Requests; sie mutieren keine Host-Daten.
- Keine neuen Datenmodell-Properties, IDs, Projekte, Packages oder Architekturentscheidungen ohne vorheriges Step-Gate.
- Jede Codeänderung nur auf Basis der aktuell bereitgestellten Dateien; komplette Dateien liefern, sofern kein Patch verlangt wird.
- Nach jeder Phase: lokal bauen, relevante Tests ausführen, Ergebnis und offene Punkte berichten und auf Freigabe warten.

Ziel von Phase 0: allgemeine Projektstruktur, echte Rubjerg.Graphviz-API/Runtime, Deployment und neutralen Datenmodellrahmen verifizieren. Erst danach mit Phase 1 fortfahren.
```

### 17.3 Übergang in die UserFlow-Integration

Erst nach Abschluss und Freigabe von Phase 6 wird ein **neuer Integrationskontext** eröffnet oder dem bestehenden Chat die folgenden Artefakte nachgereicht:

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
2. Nach Phase 0 und jeder weiteren abgenommenen Phase: ausschließlich den aktuellen Quellstand der neuen `VIA.WPF.Graph`-Solution zusätzlich geben.
3. Erst nach Phase-6-Abnahme: den aktuellen UserFlow-VS2AI-Export und den Auftrag aus Abschnitt 17.3 geben.
4. Bei UserFlow-Änderungen vor oder während Phase 7: einen neuen Export bereitstellen, bevor der Adapter geändert wird.
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

## 19. Revision 5 – Phase-7-Konkretisierung ohne Änderung der bisherigen Masterplan-Inhalte

Diese Revision ergänzt den bestehenden Masterplan ausschließlich um eine präzisere Schrittfolge für Phase 7. Alle vorherigen Abschnitte bleiben fachlich maßgeblich. Diese Ergänzung ersetzt keine bestehenden Architekturregeln, Abnahmebedingungen oder Step-Gates.

### 19.1 Zweck von P7-001

P7-001 ist ein Integrationsentscheid vor Code. Er ist kein Implementierungsschritt und keine Freigabe für Datenmodell-, Persistenz-, Editor- oder Migrationsänderungen.

Ziel von P7-001 ist, den read-only Rahmen für die erste UserFlow-Integration verbindlich festzulegen:

- Adapter ausschließlich auf UserFlow-Seite,
- `VIA.WPF.Graph` bleibt unverändert UserFlow-frei,
- UserFlow referenziert `VIA.WPF.Graph.Core`, `VIA.WPF.Graph.Graphviz` und `VIA.WPF.Graph.Wpf`, niemals umgekehrt,
- keine neue `ActionDefinition.Id` in Phase 7,
- keine JSON-, Clone-, Snapshot- oder Altprojekt-Migration in Phase 7,
- keine Änderung am bestehenden ActionArea-Editor in Phase 7,
- keine Action-Bearbeitung und keine neue Navigation in Phase 7,
- `NavigateBack` wird read-only zunächst neutral als Rücknavigation dargestellt; ein konkretes Ziel darf nur angezeigt werden, wenn es aus dem aktuell sichtbaren Pfad eindeutig ableitbar ist,
- alle UserFlow-Daten bleiben Single Source of Truth in den bestehenden Collections.

### 19.2 Vorgeschlagene Teilissues für Phase 7

#### P7-001 – Integrationsentscheid und Schnitt festlegen

Umfang:

- aktuellen Masterplan, aktuellen `VIA.WPF.Graph`-Stand und aktuellen UserFlow-Export gegen Phase 7 prüfen,
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
- links `GraphNavigationPathTree`, rechts `GraphCanvas`,
- beide Controls binden ausschließlich an hostseitigen Zustand,
- Zoom, Pan, Fit, Auswahl, aktiver Bereich und Tree-Expand-Zustand bleiben im Flow-Host-ViewModel,
- Viewwechsel darf den Graphzustand nicht zerstören.

Nicht enthalten:

- kein neuer allgemeiner Graph-Renderer,
- keine UserFlow-Typen in `VIA.WPF.Graph.Wpf`,
- keine direkte Mutation aus Tree oder GraphCanvas.

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

### 19.3 Stopppunkte innerhalb Phase 7

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

### 19.4 Phase-7-Nichtziele

Nicht Bestandteil der read-only Phase 7:

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

Phase 7 liefert nur die read-only Navigationsansicht. Erst nach ihrer Abnahme werden Phase 8 und Phase 9 betrachtet.

Phase 8 benötigt vor Code weiterhin gesonderte Entscheidungen zu:

- stabiler `ActionDefinition.Id`,
- JSON-/Clone-/Snapshot-Erhalt,
- ActionArea-Editor-Identitätserhalt,
- Altprojekt-Migration,
- Undo-/Redo-Verhalten,
- erlaubten editierbaren ActionTypes,
- Behandlung von `NavigateBack`, Popup-Schließen und Mehrfachaktionen pro Trigger.

Phase 9 bleibt weiterhin die spätere Stufe für neue Navigation aus Graph oder Tree.

