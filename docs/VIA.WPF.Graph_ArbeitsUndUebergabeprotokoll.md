# VIA.WPF.Graph – Arbeits- und Übergabeprotokoll

**Status:** verbindliches Arbeitsprotokoll für Phase 0  
**Issue:** P0-005 – Arbeits- und Übergabeprotokoll im Repository verankern  
**Phase:** 0 – Baseline, Entscheidungen und technische Verifikation  
**Stand:** nach P0-004, vor P0-006  

## 1. Zweck

Dieses Dokument hält den aktuellen Arbeitsstand der allgemeinen `VIA.WPF.Graph`-Library fest. Es dient als Übergabegrundlage zwischen Chat, Repository und GitHub-Issues.

Der Masterplan bleibt die fachliche und architektonische Single Source of Truth. Dieses Protokoll dokumentiert nur, welche Phase-0-Arbeitspakete bereits erledigt wurden, welche technischen Ergebnisse gelten und welche Grenzen weiterhin verbindlich sind.

## 2. Verbindliche Arbeitsgrenzen

Für die allgemeine Library gelten weiterhin diese Grenzen:

- `VIA.WPF.Graph` bleibt bis einschließlich Phase 6 vollständig UserFlow-frei.
- Keine Referenzen auf Mockup-, Screen-, Popup-, ActionArea- oder ActionDefinition-Typen.
- Keine Hostmodell-Mutation durch Graph-UI oder Demo-Code.
- `VIA.WPF.Graph.Core` bleibt ohne Projekt-, WPF-, Graphviz-, UserFlow- oder Host-Abhängigkeit.
- `VIA.WPF.Graph.Graphviz` referenziert nur `Core`.
- `VIA.WPF.Graph.Wpf` referenziert nur `Core`.
- `VIA.WPF.Graph.Demo` ist die alleinige Composition Root und darf `Core`, `Graphviz` und `Wpf` zusammensetzen.
- `Graphviz` und `Wpf` dürfen einander nicht referenzieren.
- CommunityToolkit.Mvvm wird überall verwendet, wo es technisch sinnvoll ist.

## 3. Phase-0-Arbeitspakete

| Issue | Ergebnis | Status |
|---|---|---|
| P0-001 | Solution-Struktur und Isolationsgrenzen dokumentiert. `.slnx` bleibt verbindlich; keine `.sln`-Migration. | erledigt |
| P0-002 | Rubjerg.Graphviz 3.0.5 auf Windows x64 technisch verifiziert. | erledigt |
| P0-003 | Minimalgraph als technische Referenz sichtbar ausgeführt: TB und LR, 6 Knoten, Rückkante, Popup, 2 Gruppen. | erledigt |
| P0-004 | Minimaler neutraler Vertrag, feste Größenprofile und View-State-Grenzen dokumentiert. | erledigt |
| P0-005 | Dieses Arbeits- und Übergabeprotokoll wird im Repository verankert. | aktuell |
| P0-006 | Phase-0-Abnahme und Step-Gate für Phase 1. | offen |

## 4. Technischer Stand nach P0-003

Der aktuelle technische Referenzstand bestätigt:

- `Rubjerg.Graphviz` Paketversion `3.0.5`,
- Prozessarchitektur `X64`,
- Runtime Identifier `win-x64`,
- Erzeugung und Layout von 6 Knoten,
- 6 gerichtete Kanten einschließlich Rückkante,
- 2 Graphviz-Cluster,
- TB- und LR-Layout,
- lesbare Graph-, Node-, Cluster- und Spline-Geometrie.

Die offizielle Zielruntime für die erste Entwicklungsstufe ist daher:

```text
win-x64
```

Andere Runtimes wie x86, ARM64, Linux oder macOS sind nicht verifiziert und nicht zugesagt.

## 5. Dokumentierte Entscheidungsbasis

Folgende Dokumente bilden den aktuellen Phase-0-Stand:

| Datei | Zweck |
|---|---|
| `docs/VIA.WPF.Graph_Masterplan.md` | fachliche und architektonische Single Source of Truth |
| `docs/VIA.WPF.Graph_ManagementOverview.md` | Management-/Projektüberblick aus P0-001 |
| `docs/VIA.WPF.Graph_P0-002_RubjergVerification.md` | Rubjerg-/Runtime-Verifikation |
| `docs/VIA.WPF.Graph_P0-003_MinimalGraphReference.md` | technische Minimalgraph-Referenz |
| `docs/VIA.WPF.Graph_P0-004_NeutralContractSizingViewState.md` | neutraler Vertrag, Sizing und View-State |
| `docs/VIA.WPF.Graph_ArbeitsUndUebergabeprotokoll.md` | aktuelles Arbeits- und Übergabeprotokoll |

## 6. Aktuelle Projektstruktur

Die Solution-Struktur nach P0-003/P0-004 lautet:

```text
VIA.WPF.Graph.slnx

VIA.WPF.Graph.Core
VIA.WPF.Graph.Graphviz
VIA.WPF.Graph.Wpf
VIA.WPF.Graph.Demo

docs
```

Die Projekte existieren nur im für Phase 0 freigegebenen Minimalumfang. Fachliche C#-Modelle, öffentliche Core-APIs, WPF-Renderer und Host-Requests werden erst in späteren Phasen umgesetzt.

## 7. Bekannte Nicht-Ziele bis einschließlich Phase 0

Nicht Bestandteil des bisherigen Stands sind:

- produktives Core-Graphmodell,
- endgültige öffentliche API,
- GraphCanvas,
- Navigation Path Tree,
- Graphviz-DOT-Produktionsadapter,
- Layout-Caching,
- WPF-Templates der späteren Library,
- UserFlow-Adapter,
- Bearbeitungsrequests,
- Persistenz,
- Undo/Redo,
- ActionDefinition-ID,
- manuelle Knotenpositionen.

## 8. Offener nächster Schritt

Nach P0-005 folgt P0-006.

P0-006 muss prüfen und dokumentieren, ob Phase 0 vollständig abgenommen werden kann. Erst danach darf Phase 1 starten.

Phase 1 beginnt ausschließlich nach ausdrücklicher Freigabe. Der Inhalt von Phase 1 ist `VIA.WPF.Graph.Core`: neutrales, testbares Graphmodell ohne WPF, Graphviz und UserFlow.

## 9. Übergaberegel für den nächsten Chat

Bei einem Chatwechsel genügt folgender Stand als Grundlage:

1. aktueller Repository-Stand nach P0-005 oder P0-006,
2. `docs/VIA.WPF.Graph_Masterplan.md`,
3. dieses Arbeits- und Übergabeprotokoll,
4. Hinweis, dass UserFlow bis einschließlich Phase 6 kein Eingabeartefakt ist.

Keine frühere Graphviz-Demo, kein UserFlow-Export und kein Hostmodell dürfen als technische Baseline verwendet werden.

## 10. Prüfpunkte zur Abnahme von P0-005

P0-005 gilt als erledigt, wenn:

- dieses Dokument im Repository liegt,
- P0-001 bis P0-004 nachvollziehbar zusammengefasst sind,
- die aktuellen technischen Ergebnisse und Grenzen dokumentiert sind,
- P0-006 als nächster Step-Gate-Schritt klar benannt ist,
- keine Code-, Projekt-, Paket- oder Architekturänderung entstanden ist.
