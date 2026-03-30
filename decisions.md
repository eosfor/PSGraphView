# Architecture Decisions

## 2026-03-30 00:08 PDT - Эталонные export targets фиксируем явно

Решение: для baseline compare и последующего parity используем явные target-spec из локального `graphviz`:

- `-Tsvg:svg`
- `-Tpng:cairo`
- `-Tjpg:cairo`

Причины:

- для `svg` эталонный path в текущем локальном `graphviz` идёт через core SVG plugin, а не через cairo path
- для `png` и `jpg` на локальной сборке reference path идёт через cairo-backed renderer/device chain
- explicit target-spec убирает двусмысленность от plugin priority и различий между сборками
- compare harness должен воспроизводимо сравнивать один и тот же backend, а не то, что выбралось по умолчанию

Телеметрия:

- code inspection: `/Users/andrei/repo/graphviz/plugin/core/gvrender_core_svg.c`
- code inspection: `/Users/andrei/repo/graphviz/plugin/pango/gvrender_pango.c`
- code inspection: `/Users/andrei/repo/graphviz/plugin/gd/gvdevice_gd.c`
- code inspection: `/Users/andrei/repo/graphviz/lib/common/emit.c`

## 2026-03-30 00:08 PDT - Новый export pipeline выносим в отдельный проект

Решение: production-код нового export pipeline планируем в отдельном проекте `PSGraphView.GVExport`.

Причины:

- это отделяет export parity от layout parity и не раздувает `PSGraphView.Sfdp`
- в `PSGraphView.Sfdp` разумно оставить layout, post-processing и layout-specific adapters
- `PSGraphView.PowerShell` должен оставаться тонким user-facing слоем, а не местом для scene/raster/device логики
- общий scene/viewport/raster слой потенциально пригодится не только для `Sfdp`, но и для других renderer-ов

Телеметрия:

- repo structure inspection: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/`
- repo structure inspection: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/`
- user direction: отдельный проект для нового export functionality

## 2026-03-30 00:11 PDT - Patch 0 baseline compare harness принят

Решение: считаем `Patch 0` закрытым и используем новый harness как рабочий baseline для следующих export patch-ей.

Причины:

- оба planned compare-script теперь реально запускаются в локальной репе
- small-graph harness даёт быстрый повторяемый baseline, где mismatch читается без шума от большого layout
- WikiVote harness тоже рабочий и уже сохраняет те же артефакты рядом с output-ами
- managed `png/jpg` сейчас честно помечаются как unsupported, а не маскируются под успешный результат

Телеметрия:

- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-smoke`
- result: 6/6 cases completed successfully
- result: graphviz `svg/png/jpg` produced for all 6 cases
- result: managed `svg` produced for all 6 cases
- result: managed `png/jpg` unsupported for all 6 cases, как и ожидалось до `Patch 4`
- result: во всех 6 small-graph cases `NodeGroupDelta = 0` и `EdgeGroupDelta = 0`
- result: во всех 6 small-graph cases `WidthMatch = false`, `HeightMatch = false`, `ViewBoxMatch = false`
- result: во всех 6 small-graph cases `TitleDelta = -1` и `RectDelta = 1`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 10 -OutputDir /tmp/psgraphview-export-wikivote-smoke`
- result: script completed successfully and produced `svg/png/jpg` reference outputs plus managed `svg`
- result: `SubgraphSeedCount = 10` yielded `10 vertices / 0 edges`, so this size is good for smoke only, not for edge-path parity work

## 2026-03-30 00:20 PDT - Patch 0a diagnostics layer принят

Решение: считаем `Patch 0a` закрытым. Новый compare loop теперь поднимает diagnostics с managed-стороны и verbose-диагностику из локального `graphviz`.

Причины:

- managed exporter теперь пишет явные события:
  - `render.scene`
  - `render.viewport`
  - `svg.structure`
- локальный `graphviz` теперь пишет нормализованные `GVEXPORT_*` строки для:
  - viewport
  - scene
  - svg transform
  - cairo surface
  - gd device
- compare summary уже сводит эти данные в отдельный блок `Comparisons.Diagnostics`

Телеметрия:

- managed verification: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- managed verification: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- graphviz rebuild: `PATH=\"/opt/homebrew/opt/bison/bin:$PATH\" cmake -S /Users/andrei/repo/graphviz -B /tmp/graphviz-build-export-port -GNinja -DCMAKE_BUILD_TYPE=Debug -DCMAKE_INSTALL_PREFIX=/tmp/graphviz-prefix -DBISON_EXECUTABLE=/opt/homebrew/opt/bison/bin/bison`
- graphviz rebuild: `PATH=\"/opt/homebrew/opt/bison/bin:$PATH\" cmake --build /tmp/graphviz-build-export-port --target install -j4`
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch0a`
- result: 6/6 small-graph cases completed successfully with `Comparisons.Diagnostics.Available = true`
- result: on `single-edge`, `Scene.NodeCountDelta = 0` and `Scene.EdgeCountDelta = 0`
- result: on `single-edge`, `Viewport.OutputWidthDelta = -17`, `Viewport.OutputHeightDelta = 17`
- result: on `single-edge`, `SvgStructure.TitleCountDelta = -1`, `SvgStructure.RectCountDelta = 1`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -OutputDir /tmp/psgraphview-export-wikivote-patch0a`
- result: diagnostics summary produced successfully for `wiki-vote-subgraph-30`

## 2026-03-30 00:20 PDT - Нужен отдельный edge-preserving WikiVote mode

Решение: в план добавлен follow-up `Patch 0b` для большого compare-case с реальными рёбрами.

Причины:

- текущий способ брать induced subgraph по top-degree вершинам оказался плохим для export parity
- без рёбер бесполезно сравнивать:
  - edge path geometry
  - arrows
  - raster edge density

Телеметрия:

- `Compare-WikiVote-Export.ps1 -UseSubgraph -SubgraphSeedCount 10` -> `10 vertices / 0 edges`
- `Compare-WikiVote-Export.ps1 -UseSubgraph -SubgraphSeedCount 30` -> `30 vertices / 0 edges`

## 2026-03-30 00:34 PDT - Пересмотр решения про WikiVote induced mode

Решение: прежний вывод о том, что induced top-degree mode на `WikiVote` даёт `0` рёбер, признан ошибочным.

В чём была ошибка:

- в `Compare-WikiVote-Export.ps1` script некорректно обходил результат `Get-OutEdge` / `Get-InEdge`
- вместо отдельных рёбер он фактически получал `EdgeList` как единый объект
- из-за этого subgraph builder почти не добавлял рёбра и telemetry была неверной

Обновлённое решение:

- `InducedTopDegree` сохраняем как простой smoke-mode
- добавленный `ExpandedTopDegree` используем как основной large-graph compare-case для export parity

Причины:

- после исправления edge enumeration induced mode уже не пустой
- но expanded mode всё равно даёт более насыщенный граф и лучше годится для сравнения edge/path behavior

Телеметрия:

- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphMode InducedTopDegree -SubgraphSeedCount 10 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch0b-induced-smoke`
- result: `Selection.Mode = InducedTopDegree`
- result: `10 vertices / 32 edges`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch0b-expanded`
- result: `Selection.Mode = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: on expanded case `NodeGroupDelta = 0`, `EdgeGroupDelta = 0`
- result: on expanded case `TitleDelta = -1`, `RectDelta = 1`
- result: on expanded case `Viewport.OutputWidthDelta = -95`, `Viewport.OutputHeightDelta = -80`

## 2026-03-30 00:34 PDT - Patch 0b закрыт

Решение: считаем `Patch 0b` выполненным.

Причины:

- в `Compare-WikiVote-Export.ps1` теперь есть два режима выборки подграфа
- summary теперь явно пишет `Selection.Mode`
- появился repeatable large-graph case с большим числом рёбер для export parity

Телеметрия:

- `InducedTopDegree` smoke-case: `10 / 32`
- `ExpandedTopDegree` compare-case: `30 / 99`
