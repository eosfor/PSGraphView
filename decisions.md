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

## 2026-03-30 00:38 PDT - Для graph helpers сначала используем PSGraph/QuikGraph API

Решение: при дальнейшей работе с графами в compare scripts и tooling сначала опираться на готовые API из `PSGraph` и `QuikGraph`, и только потом добавлять локальную вспомогательную логику.

Причины:

- графы в `PSGraph` уже построены поверх `QuikGraph`
- там уже есть готовые механизмы для:
  - получения `in/out` рёбер
  - обходов графа
  - подграфов и graph algorithms
- это снижает риск повторно допустить ошибки в script-логике, как это было с некорректным обходом `EdgeList`
- это уменьшает дублирование и держит compare tooling ближе к реальному graph surface проекта

Телеметрия:

- source reference: `/Users/andrei/repo/PSGraph`
- source reference: `QuikGraph`-based graph model used by `PSGraph` / `PSQuickGraph`
- trigger: user direction to учитывать исходники `PSGraph` и не пере-реализовывать графовые механизмы в тестовых скриптах без необходимости

## 2026-03-30 10:44 PDT - Patch 1 делаем через отдельный managed scene project

Решение: `Patch 1` закрываем через новый проект `PSGraphView.GVExport`, а не через дальнейшее разрастание `SfdpSvgExporter`.

Причины:

- это соответствует ранее принятому направлению на отдельный export-layer
- scene-модель теперь отделена от layout-specific кода
- `Sfdp` остаётся местом для layout, routing, label placement и adapter-логики
- это открывает прямой путь к `png/jpg` поверх того же scene, без парсинга собственного `svg`

Телеметрия:

- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport`
- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpSvgExporter.cs` now renders via `GraphSvgRenderSceneWriter`
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `7/7` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed

## 2026-03-30 10:44 PDT - PowerShell пока не ссылаем напрямую на GVExport

Решение: на `Patch 1` не добавляем прямой `ProjectReference` из `PSGraphView.PowerShell` в `PSGraphView.GVExport`.

Причины:

- у cmdlet-слоя пока нет прямого использования scene/device API
- реальная зависимость сейчас идёт через `PSGraphView.Sfdp`
- прямую ссылку лучше добавлять только тогда, когда `PowerShell` начнёт напрямую выбирать format/device/export backend
- это держит diff меньше и не создаёт искусственную связанность раньше времени

Телеметрия:

- code inspection: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- code inspection: `PSGraphView.PowerShell` после `Patch 1` продолжает работать без прямого обращения к `GVExport`

## 2026-03-30 10:44 PDT - Patch 1 не изменил текущую SVG parity baseline

Решение: считаем, что вынесение scene/writer в `Patch 1` не внесло новой визуальной регрессии в текущий baseline.

Причины:

- small-graph compare остался на тех же дельтах, что и до `Patch 1`
- large-graph `WikiVote` compare-case тоже сохранил те же структурные расхождения
- значит `Patch 1` действительно изолировал архитектуру, а не поменял измеряемое поведение

Телеметрия:

- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch1`
- result: `6/6` cases completed successfully
- result: on all `6` cases `NodeGroupDelta = 0` and `EdgeGroupDelta = 0`
- result: on all `6` cases `TitleDelta = -1` and `RectDelta = 1`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch1`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: `NodeGroupDelta = 0`
- result: `EdgeGroupDelta = 0`
- result: `TitleDelta = -1`
- result: `RectDelta = 1`
- result: `Diagnostics.Available = true`

## 2026-03-30 01:00 PDT - Patch 2 переводит viewport в graphviz-like output space

Решение: `Patch 2` закрываем через явный viewport helper и отдельный export-space conversion step перед построением scene.

Причины:

- до этого exporter смешивал layout units и output units
- из-за этого `viewBox` был смещён в отрицательные координаты, padding был случайным (`max(nodeRadius * 2, 12)`), а page size почти не зависел от реальной graph geometry
- для `GraphvizPoints` нужен явный переход из layout-space в output-space с `72 dpi` semantics
- `png/jpg` позже тоже потребуют тех же чисел: `dpi`, `pad`, `translation`, `pageBoundingBox`

Телеметрия:

- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpViewportCalculator.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpSvgExporter.cs` now:
  - scales layout coordinates into output-space
  - computes zero-based page bounding box
  - applies translation before scene rendering
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs` now writes `dpi/pad/translation/pageBoundingBox/layoutScale`
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `8/8` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed

## 2026-03-30 01:00 PDT - После Patch 2 page-box math стал правильнее, но остался residual natural-size mismatch

Решение: считаем `Patch 2` выполненным, но добавляем follow-up `Patch 2a` на исследование residual natural-size mismatch для больших графов.

Причины:

- после `Patch 2` zero-based `viewBox` и graphviz-like `pad = 4` уже совпадают по смыслу и по telemetry
- оставшаяся разница теперь выглядит как mismatch natural export scale, а не как ошибка в `pageBoundingBox` math
- смешивать это с `Patch 3` нельзя, потому что `Patch 3` про SVG structure, а не про scale calibration

Телеметрия:

- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch2b`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `ViewBoxMinXDelta = 0` and `ViewBoxMinYDelta = 0`
- result: `single-edge` improved from `OutputWidthDelta = -17`, `OutputHeightDelta = 17` to `OutputWidthDelta = 5`, `OutputHeightDelta = 1`
- result: `disconnected-components` improved from `OutputWidthDelta = -57`, `OutputHeightDelta = -5` to `OutputWidthDelta = 4`, `OutputHeightDelta = 0`
- result: `star` improved from `OutputWidthDelta = -57`, `OutputHeightDelta = -58` to `OutputWidthDelta = -2`, `OutputHeightDelta = -4`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch2b`
- result: `30 vertices / 99 edges`
- result: `ViewBoxMinXDelta = 0`
- result: `ViewBoxMinYDelta = 0`
- result: `OutputWidthDelta` improved from `-95` to `23`
- result: `OutputHeightDelta` improved from `-80` to `15`

## 2026-03-30 01:09 PDT - Patch 2a подтвердил, что остаточный mismatch больше не сидит в viewport math

Решение: считаем `Patch 2a` закрытым. Остаточный mismatch после `Patch 2` не пытаемся дальше чинить через `pageBoundingBox` / `dpi` / `translation`.

Причины:

- `Patch 2` уже посадил `viewBox` в zero-based coordinates и сделал graphviz-like `pad = 4`
- дополнительные viewport-side правки не изменили residual deltas на cases, где mismatch связан с shape/layout behavior
- значит следующий meaningful шаг должен идти не в viewport math, а в:
  - edge routing / curve shape
  - SVG transform structure
  - или более глубокую layout-scale parity

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpEdgeRouter.cs` now stores routed points and can compute cubic bezier bounds
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs` can expand content bounds with edge geometry
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `9/9` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch2a-final`
- result: on all `6` small-graph cases `ViewBoxMinXDelta = 0` and `ViewBoxMinYDelta = 0`
- result: `single-edge` remained `5 / 1`
- result: `self-loop` remained `-17 / -12`
- result: `triangle-cycle` remained `-27 / -19`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch2a-final`
- result: `30 vertices / 99 edges`
- result: `OutputWidthDelta = 23`
- result: `OutputHeightDelta = 15`
