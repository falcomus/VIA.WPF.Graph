# VIA.WPF.Graph – P2-006 Graphviz-Referenzgraphs und Phase-2-Gate

## Status

Phase 2 wird durch dieses Gate technisch abgesichert, sobald die lokalen Tests erfolgreich ausgeführt wurden.

## Geprüfter Umfang

- `VIA.WPF.Graph.Graphviz` bleibt der einzige Projektbereich mit `Rubjerg.Graphviz`-Abhängigkeit.
- `VIA.WPF.Graph.Wpf` referenziert weiterhin nur `VIA.WPF.Graph.Core`.
- Der Graphviz-Adapter erzeugt neutrale `GraphLayoutResult`-Daten aus `GraphDocument`.
- `GraphGroupKind.Container` wird als Graphviz-Cluster abgebildet.
- `GraphGroupKind.Marker` wird nicht als Cluster verwendet.
- Top-to-bottom- und left-to-right-Layouts werden über Referenzgraphen geprüft.
- Spline-, Polyline- und Orthogonal-Routing werden technisch geprüft.
- Fehlerfälle werden als `GraphLayoutResult.Error` zurückgegeben.
- Vorab abgebrochene Layoutanforderungen liefern ein kontrolliertes Cancellation-Ergebnis.
- Erfolgreiche identische Layoutanforderungen werden aus dem Cache wiederverwendet.

## Referenzgraph

Der Phase-2-Referenzgraph enthält:

- 6 Knoten,
- 6 gerichtete Links,
- 1 Popup-Knoten,
- 1 Rückkante,
- 2 Containergruppen,
- 1 Markergruppe, die nicht als Cluster erscheinen darf.

## Lokaler Gate-Befehl

```powershell
dotnet test .\VIA.WPF.Graph\VIA.WPF.Graph.slnx
```

## Abnahmebedingung

Phase 2 gilt erst als abgeschlossen, wenn der lokale Testlauf ohne Fehler beendet wurde und der Stand committed sowie gepusht ist.

## Nicht Teil von Phase 2

- kein WPF-Renderer,
- keine GraphCanvas-Implementierung,
- keine Demo-UI-Erweiterung,
- keine UserFlow-Integration,
- keine Hostmutation.
