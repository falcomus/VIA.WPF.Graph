# P0-002 – Rubjerg.Graphviz 3.0.5 auf Zielrechner verifizieren

## Zweck dieses Arbeitspakets

P0-002 prüft mit einer sehr kleinen WPF-Anwendung, ob die für VIA.WPF.Graph vorgesehene
Graphviz-Technik auf dem tatsächlichen Windows-Zielrechner funktioniert.

Die Prüfung legt noch keinen allgemeinen Graphvertrag und keinen späteren WPF-Renderer an.
Sie enthält nur:

- die leeren Zielprojekte aus der in P0-001 freigegebenen Struktur,
- das Paket `Rubjerg.Graphviz` 3.0.5 ausschließlich im Graphviz-Projekt,
- eine technische Probe mit 6 Knoten, 2 Clustern, einer Rückkante sowie TB- und LR-Layout,
- eine kleine WPF-Testoberfläche als alleinigen Composition Root,
- einen MVVM-Testzustand mit `CommunityToolkit.Mvvm`.

## Verbindliche Referenzgrenzen der Probe

```text
VIA.WPF.Graph.Core      -> keine externen Abhängigkeiten
VIA.WPF.Graph.Graphviz  -> Core, Rubjerg.Graphviz 3.0.5
VIA.WPF.Graph.Wpf       -> Core
VIA.WPF.Graph.Demo      -> Core, Graphviz, Wpf; alleiniger Composition Root
```

`VIA.WPF.Graph.Wpf` referenziert weder `Rubjerg.Graphviz` noch
`VIA.WPF.Graph.Graphviz`. Nur die Demo setzt die beiden getrennten Schichten zusammen.

## Architektur des technischen Tests

`Rubjerg.Graphviz` 3.0.5 stellt in dieser Prüfung eine AMD64-Assembly bereit. Deshalb
bauen `VIA.WPF.Graph.Graphviz` und `VIA.WPF.Graph.Demo` explizit mit
`PlatformTarget` `x64`; die Demo verwendet zusätzlich `RuntimeIdentifier` `win-x64`.
`Core` und `Wpf` bleiben architekturneutral. Diese Einstellung dient nur der technischen
P0-002-Verifikation und ist noch keine allgemeine Produkt-Runtime-Freigabe.

## Vorab geprüfte technische Grundlage

Die Paketdokumentation für `Rubjerg.Graphviz` 3.0.5 beschreibt:

- mitgelieferte, vorkompilierte 64-Bit-Graphviz-Binaries für Windows,
- Graphviz-Version 11.0.0 innerhalb des Pakets,
- Unterstützung für .NET 5 und höher,
- die Voraussetzung Microsoft Visual C++ Redistributable 2015–2022 auf Windows,
- die API `RootGraph.CreateNew`, `GetOrAddNode`, `GetOrAddEdge`, `GetOrAddSubgraph`,
  `CreateLayout`, `GetBoundingBox` und `GetFirstSpline`.

Die technische Probe verwendet genau diese dokumentierte API. Ein echter Build und Lauf auf
Deinem Windows-Rechner bleiben dennoch zwingend, weil nur dort die native Windows-Runtime,
Visual-C++-Voraussetzung und Publish-Ausgabe verifiziert werden können.

## MVVM-Regel

Die Testoberfläche verwendet `CommunityToolkit.Mvvm` 8.4.2:

- `GraphvizVerificationViewModel : ObservableObject`,
- `[ObservableProperty]` für Ergebnis- und Laufstatus,
- `[RelayCommand]` für die wiederholbare technische Prüfung.

Der Code-behind von `MainWindow` setzt ausschließlich den ViewModel-DataContext. Er enthält
keinen UI-Zustand und keine Prüfungslogik.

## Prüffall

Der technische Minimalgraph prüft zur Laufzeit:

- 6 Knoten,
- 6 benannte gerichtete Kanten einschließlich einer Rückkante,
- 2 Graphviz-Cluster,
- Top → Bottom und Left → Right über `rankdir`,
- `CreateLayout` mit `CoordinateSystem.TopLeft`,
- Bounds von Graph, Clustern und Knoten,
- Spline-Geometrie der benannten Rückkante.

Ein erfolgreicher Start zeigt `P0-002 PASSED`. Ein Fehlerfenster enthält die vollständige
Ausnahme und soll unverändert in den Chat kopiert werden.

## Test auf Deinem Rechner

1. Die ZIP-Datei im Repository-Root entpacken und vorhandene Dateien ersetzen.
2. `VIA.WPF.Graph/VIA.WPF.Graph.slnx` in Visual Studio öffnen.
3. Bei `VIA.WPF.Graph.Demo` mit Rechtsklick **Als Startprojekt festlegen**.
4. Oben **Erstellen → Projektmappe erstellen** wählen.
5. Mit `F5` starten.
6. Im Fenster muss `P0-002 PASSED` stehen. Danach einmal auf **Prüfung erneut ausführen** klicken.

Danach den Publish-Test in PowerShell im Ordner `VIA.WPF.Graph` ausführen:

```powershell
$publishPath = Join-Path $env:TEMP ("VIA.WPF.Graph-P0-002-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
dotnet publish .\VIA.WPF.Graph.Demo\VIA.WPF.Graph.Demo.csproj -c Release -r win-x64 --self-contained false -o $publishPath
Start-Process (Join-Path $publishPath "VIA.WPF.Graph.Demo.exe")
```

Der Publish-Ordner liegt nur temporär außerhalb des Repositorys. Auch dort muss
`P0-002 PASSED` erscheinen.

## Ergebnisprotokoll – erst nach dem echten Test ausfüllen

| Prüfschritt | Ergebnis |
|---|---|
| Paketwiederherstellung | offen |
| Lokaler Build | offen |
| Start aus Visual Studio | offen |
| Wiederholung über die Schaltfläche | offen |
| Native Graphviz-Runtime | offen |
| Frameworkabhängiger Publish `win-x64` | offen |
| Start aus Publish-Ordner | offen |
| Unterstützte Runtime | offen |
| Deployment-/Lizenzentscheidung | offen |

## Vorläufige, noch nicht bestätigte Entscheidung

Wenn alle Prüfungen erfolgreich sind, wird `win-x64` zunächst als einzige unterstützte
Runtime dokumentiert. x86, ARM64, Linux und Self-contained Deployment sind nicht Gegenstand
dieses Arbeitspakets und bleiben ausdrücklich ungeprüft.

Der Wrapper ist laut Upstream-Repository unter EPL-2.0 lizenziert. Die Graphviz-Lizenzseite
nennt die Eclipse Public License. Vor einer externen Produktverteilung müssen die mit dem
Paket ausgelieferten Lizenzhinweise nochmals verbindlich geprüft und in der Produktdokumentation
aufgenommen werden.
