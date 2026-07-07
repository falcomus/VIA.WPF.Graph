# P0-003 – Minimalgraph als technische Referenz ausführen

## Zweck

P0-003 ergänzt die erfolgreiche P0-002-Runtime-Prüfung um eine sichtbare, isolierte WPF-Referenz.
Die Demo zeigt denselben festen Minimalgraphen als technische Darstellung in zwei Tabs:

- Oben → Unten (`TB`)
- Links → Rechts (`LR`)

## Fester Prüffall

- 6 Knoten
- 1 Popup-Knoten (`Help popup`)
- 6 gerichtete Kanten
- 1 gestrichelte Rückkante (`Confirmation` → `Start`)
- 2 Gruppen (`Entry`, `Work`)

Die Positionen, Gruppengrenzen und Kantenverläufe stammen direkt aus dem von
`Rubjerg.Graphviz` berechneten Layout. Die Demo übersetzt diese Daten nur in einfache
WPF-Formen für den technischen Nachweis.

## Abgrenzung

Dies ist ausdrücklich **kein** allgemeiner `GraphCanvas` und keine Vorwegnahme von Phase 3.

- Kein neuer Typ im `VIA.WPF.Graph.Wpf`-Projekt.
- Keine Abhängigkeit von `VIA.WPF.Graph.Wpf` zu Graphviz.
- Kein neutraler Graphvertrag im Core.
- Keine UserFlow-/Mockup-Referenz.
- Keine Übernahme der früheren Graphviz-Demo.

Die Demo bleibt der einzige Composition Root und referenziert `Core`, `Graphviz` und `Wpf`.

## Lokaler Test

1. `VIA.WPF.Graph.slnx` in Visual Studio öffnen.
2. `VIA.WPF.Graph.Demo` als Startprojekt festlegen.
3. **Erstellen → Projektmappe neu erstellen**.
4. Mit `F5` starten.
5. Prüfen:
   - Oben steht `P0-003 PASSED`.
   - Der Tab **Oben → Unten (TB)** zeigt alle 6 Knoten, beide Gruppen und die Rückkante.
   - Der Tab **Links → Rechts (LR)** zeigt dieselben Elemente in horizontaler Anordnung.
   - Die Schaltfläche **Referenz erneut ausführen** erzeugt beide Ansichten erneut.

Bei einem Fehler den vollständigen Inhalt unter **Technische Details** senden.
