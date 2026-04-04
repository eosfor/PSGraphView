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

## 2026-03-30 09:59 PDT - Patch 3 закрываем через graphviz-like SVG structure

Решение: считаем `Patch 3` выполненным. Дальше `svg` надо сравнивать уже как graphviz-like output с остаточными геометрическими деталями, а не как другой exporter.

Причины:

- root `svg` теперь ближе к `graphviz` по единицам и структуре
- появился graph-level container:
  - `id=\"graph0\"`
  - `class=\"graph\"`
  - `transform=\"scale(...) rotate(...) translate(...)\"`
- graph title и background теперь оформлены по graphviz-like схеме:
  - `<title>G</title>`
  - background polygon вместо background rect
- node geometry переведена на `ellipse`, а не `circle`
- после удаления лишних контейнеров `g#edges` / `g#nodes` структурный diff по числу групп ушёл в `0`

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRenderScene.cs` now models graph canvas metadata plus separate node radii
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphSvgRenderSceneWriter.cs` now writes:
  - root `width/height` in `pt`
  - graph-level `<g id=\"graph0\" class=\"graph\" ...>`
  - graph title
  - background polygon
  - `ellipse` nodes
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs` now emits graph-group coordinates and graph transform instead of fully flattened page coordinates
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch3b`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `GroupCountDelta = 0`, `TitleDelta = 0`, `RectDelta = 0`
- result: on all `6` small-graph cases `GraphGroupId = graph0`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3b`
- result: `30 vertices / 99 edges`
- result: `GroupCountDelta = 0`, `TitleDelta = 0`, `RectDelta = 0`
- result: `GraphGroupId = graph0`
- result: `GraphGroupTransform = scale(1 1) rotate(0) translate(4 118.982)`
- result: `WidthMatch = false`, `HeightMatch = false`, `ViewBoxMatch = false`

Следствие:

- остаточные `svg`-расхождения после `Patch 3` больше похожи на geometry / edge-shape / text details
- это подтверждает порядок дальнейшей работы:
  - `Patch 3a` по выбору raster backend
  - затем `Patch 4+` по raster path

## 2026-03-30 10:13 PDT - Raster backend фиксируем как SkiaSharp, без Svg.Skia в production path

Решение: `Patch 3a` закрываем выбором `SkiaSharp` как raster backend для будущих `png/jpg`. `Svg.Skia` в production export path не используем.

Причины:

- `SkiaSharp` уже присутствует в соседнем `/Users/andrei/repo/libsixel`, вместе с тем же набором native-assets packages
- он даёт нужный нам low-level surface/encoder слой:
  - создание bitmap surface
  - draw path
  - `png` encode
  - `jpg` encode
- по локальному probe видно, что transparency можно явно flatten-ить на заданный background до JPEG encode
- это позволяет держать правильное разделение ответственности:
  - graphviz-like semantics задаём мы
  - `SkiaSharp` остаётся только backend-ом рисования и кодирования
- `Svg.Skia` нам здесь вреден как основной путь, потому что он слишком легко толкает к rasterize собственного `svg` вместо прямого scene render

Телеметрия:

- source reference: `/Users/andrei/repo/libsixel/src/LibSixel.PowerShell/LibSixel.PowerShell.csproj`
- source reference: `/Users/andrei/repo/libsixel/src/LibSixel.PowerShell/Internal/ImageDecoder.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/PSGraphView.GVExport.csproj` now pins:
  - `SkiaSharp 2.88.9`
  - `SkiaSharp.NativeAssets.Linux.NoDependencies 2.88.9`
  - `SkiaSharp.NativeAssets.macOS 2.88.9`
  - `SkiaSharp.NativeAssets.Win32 2.88.9`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterBackendSelection.cs` fixes the selected backend as `SkiaSharp`
- code change: added `/Users/andrei/repo/PSGraphView/tests/PSGraphView.GVExport.Tests/SkiaSharpRasterBackendProbeTests.cs`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `2/2` passed
- result: probe confirmed `SKSurface.Create(...)`, PNG encode, and explicit flatten + JPEG encode on current macOS environment
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `9/9` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed

Ограничения, которые принимаем:

- этот patch не закрывает text parity
- этот patch не означает, что мы принимаем Skia drawing quirks как эталон
- точная policy для JPEG background будет ещё отдельно зафиксирована на `Patch 6`

## 2026-03-30 10:36 PDT - Patch 4 закрываем первым working raster path на общей scene

Решение: считаем `Patch 4` выполненным. В репе теперь есть первый production-like managed raster path для `png/jpg`, построенный поверх той же scene, что и `svg`.

Причины:

- `png/jpg` теперь реально выдаются из `Export-GraphView`, а не остаются unsupported
- raster renderer рисует scene напрямую через `SkiaSharp`, без rasterize собственного `svg`
- `Sfdp` теперь использует общий render-scene и для `svg`, и для raster path
- packaging-блокер для published module закрыт:
  - `libSkiaSharp*` копируется в корень `PSGraphView.PowerShell`
  - значит `-UseLocalModules` сценарий совпадает с unit/in-proc сценариями
- compare harness теперь видит managed `png/jpg` как реальные outputs и может сравнивать хотя бы размеры и bytes

Телеметрия:

- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRasterExporter.cs`
- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderScenePipeline.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ViewOutputKind.cs` now exposes `Png` and `Jpg`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj` now copies `libSkiaSharp*` to module root during build/publish
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1` now reports raster compare summary instead of unsupported placeholder
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `4/4` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter "SfdpSvgExporterTests|SfdpRasterExporterTests"`
- result: `11/11` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- smoke: published-module `Export-GraphView -Renderer Sfdp -As Png` returned `PNG_BYTES=894`
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch4b`
- result: `6/6` cases completed successfully with managed `svg/png/jpg`
- result: on all `6` small-graph cases `RasterBackend = SkiaSharp`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch4b`
- result: `30 vertices / 99 edges`
- result: managed `png/jpg` both produced successfully
- result: `WikiVote` raster deltas are currently `WidthDelta = 31`, `HeightDelta = 21` for both `png` and `jpg`

Следствие:

- `Patch 5` и `Patch 6` теперь можно делать уже на реальном managed raster output
- текущие raster deltas пока естественно наследуют ещё не закрытую parity-разницу по geometry/viewport/text
- это уже не проблема "нет raster surface", а именно следующий слой parity-работы

## 2026-03-30 12:37 PDT - Patch 5 закрываем после выравнивания PNG clear policy и raw raster sizing

Решение: считаем `Patch 5` выполненным. `png` теперь сравнивается не только по размерам, но и по базовым pixel-metrics, а transparent-background mismatch для основного сценария больше не маскирует остальные расхождения.

Причины:

- raster pixel size теперь считается из raw page size, а не из уже округлённого `svg` размера
- `png` surface при включённом background теперь очищается сразу в цвет фона, а не в transparent black
- из-за этого `TransparentPixelDelta` ушёл в `0` и больше не искажает итоговую картину
- после этого оставшийся mismatch уже читается как:
  - geometry / viewport difference, унаследованная от ещё не закрытой `svg` parity
  - density / dark-pixel coverage difference

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRenderScene.cs` now stores raw raster page size in viewport
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpViewportCalculator.cs` now preserves raw raster width/height before `svg` rounding
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs` now:
  - computes pixel size from raw raster page size
  - clears PNG to graph background color when background is enabled
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1` now loads `SkiaSharp.dll` early enough to decode both graphviz and managed raster outputs and compute pixel metrics
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `4/4` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch5c`
- result: on all `6` small-graph cases `PngTransparentPixelDelta = 0`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch5c`
- result: `30 vertices / 99 edges`
- result: `PngWidthDelta = 30`
- result: `PngHeightDelta = 21`
- result: `PngTransparentPixelDelta = 0`
- result: `PngDarkPixelDelta = -3008`
- result: `PngNonWhitePixelDelta = -3008`

Следствие:

- `Patch 6` теперь можно делать уже на фоне более честной PNG telemetry
- оставшийся крупный raster mismatch уже нельзя списать на transparent clear policy
- следующий meaningful шаг для raster parity идёт в JPEG flatten policy и дальше в общую geometry/text parity

## 2026-03-30 14:17 PDT - Patch 3 признаём structure-only, вводим обязательный Patch 3c

Решение: прежнюю трактовку `Patch 3` как фактически закрытого SVG parity сужаем. `Patch 3` остаётся принятым только как structural parity patch. Для visual SVG parity вводится отдельный corrective `Patch 3c`, который теперь обязателен до новых содержательных выводов по `svg/png/jpg`.

Причины:

- повторная visual-проверка артефактов показала, что managed `svg` остаётся плохим во всех прогонах начиная с `Patch 3b`
- это не локальная проблема одного case-а:
  - `WikiVote` почти пустой и уехавший вниз
  - `self-loop` теряет саму петлю
  - `triangle-cycle` клипается почти целиком
  - `bidirectional-edge` схлопывается по высоте
- managed `svg` для одинаковых case-ов оказался одинаковым byte-for-byte во всех прогонах:
  - `Patch 3b`
  - `Patch 4`
  - `Patch 4b`
  - `Patch 5`
  - `Patch 5b`
  - `Patch 5c`
- значит последующие patch-и улучшали diagnostics и raster path, но не исправляли базовую `svg` geometry
- code inspection показал правдоподобную причину:
  - viewport calculator считает `TranslationX / TranslationY`
  - но scene builder по-прежнему собирает graph transform и local coordinates через `PaddingX` и `contentBounds.MaxY`
  - из-за этого `viewBox` и scene geometry оказываются в разных coordinate systems

Телеметрия:

- artifact check: SHA-256 `wiki-vote-expanded-30-s3-managed.svg` одинаковый для:
  - `/tmp/psgraphview-export-wikivote-patch3b/wiki-vote-expanded-30-s3/svg/wiki-vote-expanded-30-s3-managed.svg`
  - `/tmp/psgraphview-export-wikivote-patch4/wiki-vote-expanded-30-s3/svg/wiki-vote-expanded-30-s3-managed.svg`
  - `/tmp/psgraphview-export-wikivote-patch4b/wiki-vote-expanded-30-s3/svg/wiki-vote-expanded-30-s3-managed.svg`
  - `/tmp/psgraphview-export-wikivote-patch5/wiki-vote-expanded-30-s3/svg/wiki-vote-expanded-30-s3-managed.svg`
  - `/tmp/psgraphview-export-wikivote-patch5b/wiki-vote-expanded-30-s3/svg/wiki-vote-expanded-30-s3-managed.svg`
  - `/tmp/psgraphview-export-wikivote-patch5c/wiki-vote-expanded-30-s3/svg/wiki-vote-expanded-30-s3-managed.svg`
- artifact check: SHA-256 `single-edge-managed.svg` одинаковый для `Patch 3b/4/4b/5/5b/5c`
- preview render:
  - graphviz `WikiVote` preview показывает весь граф
  - managed `WikiVote` preview показывает только несколько точек у нижней границы
  - graphviz `self-loop` preview показывает петлю
  - managed `self-loop` preview показывает только узел
- summary metrics:
  - `WikiVote`: graphviz `123pt x 108pt`, managed `146pt x 123pt`, `ViewBoxMatch = false`
  - `self-loop`: graphviz `27pt x 22pt`, managed `10pt x 10pt`
  - `triangle-cycle`: graphviz `54pt x 48pt`, managed `27pt x 29pt`
- code inspection:
  - `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpViewportCalculator.cs`
  - `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
  - `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphSvgRenderSceneWriter.cs`

Следствие:

- `Patch 3` больше нельзя использовать как основание для вывода "SVG уже визуально близок к graphviz"
- `Patch 4/5/6` нужно трактовать осторожно, потому что они пока наследуют неустранённую `svg` geometry проблему
- следующий корректный шаг:
  - выполнить `Patch 3c`
  - перепроверить `svg`
  - и только потом возобновлять новые parity-решения по raster

## 2026-03-30 15:08 PDT - Patch 3c закрывает visibility blocker, но не natural-size parity

Решение: считаем `Patch 3c` выполненным. Базовая visual-поломка `svg` устранена: managed `svg` больше не теряет граф за кадром и не даёт ложный сигнал "почти пустой export". При этом natural-size parity ещё не закрыта и должна рассматриваться как отдельный следующий слой работы.

Причины:

- fixed scene теперь использует graph-local `Y` sign conventions, совместимые с `graphviz svg`
- viewport больше не считается только по node/label bounds: routed edges теперь участвуют в расчёте content bounds до final page size
- compare harness теперь умеет проверять не только DOM/structure, но и реальную геометрическую видимость элементов внутри `viewBox`
- после этих изменений видимость всех graphviz-visible `node/edge` совпала на `WikiVote` и на всех small-cases

Телеметрия:

- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `11/11` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `4/4` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch3c`
- result: all `6/6` small-graph cases now report:
  - `VisibleNodeCoverageRatio = 1.0`
  - `VisibleEdgeCoverageRatio = 1.0`
  - empty `VisibleNodeIdsMissingInManaged`
  - empty `VisibleEdgeTitlesMissingInManaged`
- result: `triangle-cycle` is no longer clipped out of frame; all `3` nodes and `3` edges are visible
- result: `self-loop` is now visible in managed preview; width improved from the previous `10pt` baseline to `16pt`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3c`
- result: `30 vertices / 99 edges`
- result: `VisibleNodeCoverageRatio = 1.0`
- result: `VisibleEdgeCoverageRatio = 1.0`
- result: empty `VisibleNodeIdsMissingInManaged`
- result: empty `VisibleEdgeTitlesMissingInManaged`
- result: managed `WikiVote` preview now shows the whole graph instead of a few points near the lower edge
- result: raster telemetry improved after the shared geometry fix:
  - `PngDarkPixelDelta`: `-3008 -> -1723`
  - `JpgDarkPixelDelta`: `-2489 -> -966`

Остаточный mismatch:

- `svg` size parity remains open:
  - `WikiVote`: managed `146pt x 123pt`, graphviz `123pt x 108pt`
  - `triangle-cycle`: managed `27pt x 29pt`, graphviz `54pt x 48pt`
  - `self-loop`: managed `16pt x 12pt`, graphviz `27pt x 22pt`
- это уже не проблема потери видимости
- это следующий слой:
  - route envelope
  - natural edge bounds
  - graphviz-like extent semantics for small graphs and loops

Следствие:

- после `Patch 3c` compare harness снова можно использовать для честной оценки `svg/png/jpg`
- визуальный blocker на `svg` снят
- если нужна более близкая size parity, следующий кандидат — отдельный follow-up patch на natural-size / curve-envelope alignment

## 2026-03-30 15:15 PDT - Patch 6 закрываем как JPEG semantics patch, а не как полный raster parity fix

Решение: считаем `Patch 6` выполненным в узком, но важном смысле: managed `jpg` теперь использует graphviz-like opaque conversion policy, а compare harness умеет отдельно мерить fallback behavior. При этом полный `jpg` parity пока не закрыт, потому что основной remaining mismatch остаётся выше по pipeline.

Причины:

- в `graphviz` `jpg:cairo` идёт не через "сразу рисовать на белый фон", а через ARGB buffer + `gd` device
- `gd` branch использует off-white transparent fallback и threshold-подобную логику для слабо непрозрачных пикселей
- прежний managed path был слишком сглаженным:
  - сразу очищал surface в fallback color
  - потом смешивал всю полупрозрачность поверх этого фона
- новый path ближе к observed `graphviz` semantics:
  - сначала transparent scene
  - потом отдельный opaque-conversion step
  - fallback color `#fffffe`
  - 8-bit alpha threshold `64`, соответствующий порогу из `gd`-ветки после пересчёта из 7-bit alpha
- telemetry после этого показывает, что `jpg` policy действительно стала измеримой отдельно, но большой `WikiVote` mismatch почти не сдвинулся

Телеметрия:

- source reference: `/Users/andrei/repo/graphviz/plugin/gd/gvdevice_gd.c`
- source reference: `/Users/andrei/repo/graphviz/plugin/pango/gvrender_pango.c`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `6/6` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch6`
- result: all `6/6` cases completed successfully
- result: `self-loop` improved from `JpgDarkPixelDelta = -4` to `0`
- result: `self-loop` improved from `JpgNonWhitePixelDelta = -4` to `0`
- result: `single-edge` now has `JpgNearFallbackPixelDelta = 67`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch6`
- result: `30 vertices / 99 edges`
- result: `JpgWidthDelta` stayed `30`
- result: `JpgHeightDelta` stayed `21`
- result: `JpgDarkPixelDelta` moved from `-966` to `-975`
- result: `JpgNonWhitePixelDelta` moved from `-962` to `-975`
- result: `JpgNearFallbackPixelDelta = 9125`

Следствие:

- `Patch 6` не нужно дальше пытаться "дожать" вслепую через ещё один local tweak во flattening
- текущий remaining mismatch прежде всего наследуется от:
  - geometry / natural-size parity
  - scene density
  - text / label parity
- значит следующий meaningful шаг остаётся тем же:
  - `Patch 3d`
  - затем `Patch 7`

## 2026-03-30 15:40 PDT - Patch 3d улучшил export-side route envelope, но выявил layout-limited residuals

Решение: считаем `Patch 3d` выполненным как export-side corrective patch для route envelope и compare baseline. При этом полный natural-size parity не закрыт: после исправления self-loop и fair arrows baseline стало видно, что часть оставшегося mismatch уже не export-only, а layout-limited.

Причины:

- compare harness до `Patch 3d` был нечестным:
  - `graphviz` рендерили с arrows
  - managed export сравнивали без `ShowArrows`
- `self-loop` у managed был принципиально беднее `graphviz`:
  - одна cubic curve вместо двухсегментной петли
  - bounds и viewport из-за этого были занижены
- после починки этих вещей `self-loop` почти выровнялся, а `triangle-cycle` и `WikiVote` почти нет
- это важный сигнал: оставшийся большой mismatch нельзя дальше автоматически считать export bug

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpEdgeRouter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `11/11` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch3d`
- result: `self-loop svg` moved from `16pt x 12pt` to `28pt x 22pt`, graphviz is `27pt x 22pt`
- result: `self-loop png` moved from `WidthDelta = -16, HeightDelta = -14` to `WidthDelta = 0, HeightDelta = 0`
- result: `self-loop jpg` moved from `WidthDelta = -16, HeightDelta = -14` to `WidthDelta = 0, HeightDelta = 0`
- result: `bidirectional-edge svg` moved from `48pt x 11pt` to `48pt x 16pt`, graphviz is `43pt x 22pt`
- result: `bidirectional-edge png HeightDelta` moved from `-14` to `-8`
- result: `bidirectional-edge jpg HeightDelta` moved from `-14` to `-8`
- result: `PathDelta = 0` again on all small cases after excluding marker paths in `<defs>`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3d`
- result: `WikiVote svg` stayed `146pt x 123pt` vs `123pt x 108pt`
- result: `WikiVote png` stayed `WidthDelta = 30`, `HeightDelta = 21`
- result: `WikiVote jpg` stayed `WidthDelta = 30`, `HeightDelta = 21`
- graphviz verbose reference for `triangle-cycle` after overlap removal:
  - width `0.612462`
  - height `0.539291`
- managed diagnostics for `triangle-cycle` after overlap removal:
  - width `0.230212`
  - height `0.263516`

Следствие:

- export-side work на loops и reverse edges дала measurable improvement
- но для `triangle-cycle` и `WikiVote` оставшийся большой размерный разъезд уже нельзя честно называть только export parity issue
- следующий шаг должен быть отдельным:
  - `Patch 3e`
  - развести layout-limited residuals и export-limited residuals

## 2026-03-30 16:12 PDT - Patch 3e развёл layout-limited и export-limited residuals

Решение: считаем `Patch 3e` выполненным. Compare harness теперь явно отделяет layout-side вклад в итоговый size mismatch от export-side вклада, и для `triangle-cycle` и `WikiVote` остаток признан `layout_limited`.

Причины:

- после `Patch 3d` было уже видно, что на `triangle-cycle` и `WikiVote` размеры расходятся сильно, но было непонятно, сколько в этом реального exporter-а
- raw layout telemetry уже была в обеих системах, но harness её не сводил в одну decomposition-модель
- без этого любые дальнейшие export patch-и рисковали лечить layout mismatch как будто это проблема `svg/png/jpg` writer-а

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- change: `graphviz` verbose parser now reads:
  - `pre overlap geometry`
  - `overlap geometry`
- change: managed diagnostics parser now reads:
  - `postprocess.geometry`
  - `overlap.geometry`
  - `layout.geometry`
  - `component.geometry`
- change: compare summary now exposes `Comparisons.Diagnostics.LayoutResiduals`
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch3e`
- result: `6/6` cases completed successfully
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3e`
- result: `30 vertices / 99 edges`
- result: `triangle-cycle`:
  - classification `layout_limited`
  - `OutputWidthDelta = -27`
  - `LayoutWidthDelta = -28.71`
  - `ExportWidthDelta = 1.71`
  - `OutputHeightDelta = -19`
  - `LayoutHeightDelta = -20.45`
  - `ExportHeightDelta = 1.45`
- result: `WikiVote 30 / 99`:
  - classification `layout_limited`
  - `OutputWidthDelta = 23`
  - `LayoutWidthDelta = 23.28`
  - `ExportWidthDelta = -0.28`
  - `OutputHeightDelta = 15`
  - `LayoutHeightDelta = 15.66`
  - `ExportHeightDelta = -0.66`
- result: `bidirectional-edge` classified as `mixed`
- result: `self-loop` classified as `mixed`
- result: `single-edge` classified as `layout_limited`

Следствие:

- `triangle-cycle` и `WikiVote` больше не нужно использовать как аргумент, что exporter сам по себе всё ещё сильно ломает размер
- на этих case-ах export residual уже маленький, а основной вклад сидит в layout geometry
- следующие export patch-и нужно выбирать по тем case-ам, где classification остаётся `mixed` или `export_limited`
- если понадобится чистый export experiment без layout noise, следующий правильный шаг:
  - fixed-geometry / scene-dump compare mode

## 2026-03-30 16:58 PDT - Patch 7a зафиксировал text baseline до реального font/label fix

Решение: считаем `Patch 7a` выполненным как baseline-патч для text/font/label parity. Содержимое label-ов между `graphviz` и managed уже совпадает, но основной remaining text mismatch теперь измерен явно: font family, font size, anchor и relative label placement.

Причины:

- до этого compare harness вообще не имел отдельного labelled mode, поэтому text parity обсуждалась в основном на глаз
- без отдельного `Text` summary было трудно отличить:
  - mismatch по содержимому label-ов
  - mismatch по font family / size
  - mismatch по anchor и размещению относительно node center
- перед реальным text fix нужен был repeatable baseline, как раньше для viewport и raster

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-LabeledGraphs.ps1`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderScenePipeline.cs`
- code change: `/Users/andrei/repo/PSGraphView/tests/PSGraphView.Sfdp.Tests/SfdpSvgExporterTests.cs`
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `12/12` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7a`
- result: `3/3` labeled cases completed successfully
- result: for all labeled cases:
  - `NodeLabelCountDelta = 0`
  - `MissingNodeLabelsInManaged = []`
  - `UnexpectedNodeLabelsInManaged = []`
- result: for all labeled cases:
  - `GraphvizFontFamilies = ["Times,serif"]`
  - `ManagedFontFamilies = ["sans-serif"]`
  - `GraphvizTextAnchors = ["middle"]`
  - `ManagedTextAnchors = []`
  - `AverageFontSizeDelta = -6.0`
- result: `triangle-cycle-labeled`:
  - `AverageOffsetXDelta = 8.93`
  - `AverageBaselineOffsetYDelta = -3.65`
  - managed diagnostics `render.labels.averageOffsetX = 8.93`
- result: `single-edge-labeled`:
  - `AverageOffsetXDelta = -9.42`
  - `AverageBaselineOffsetYDelta = 3.57`
- result: `star-labeled`:
  - `AverageOffsetXDelta = 2.04`
  - `AverageBaselineOffsetYDelta = 1.13`

Следствие:

- проблема text parity теперь локализована не в потере label-ов, а именно в semantics:
  - graphviz рендерит node labels centered with `text-anchor="middle"`
  - managed сейчас рисует внешний label placement c `sans-serif` и `8pt`
- следующий правильный шаг внутри `Patch 7`:
  - `Patch 7b`
  - править label placement, font defaults и `svg` text attributes уже под измеримый baseline

## 2026-03-30 17:14 PDT - Patch 7b переводит Sfdp labels на graphviz-like centered semantics

Решение: для `Sfdp` принимаем graphviz-like semantics для node labels как новый baseline:

- centered label inside node
- `font-family="Times,serif"`
- `text-anchor="middle"`
- default `LabelFontSize = 14.0`
- default `LabelOffsetX = 0.0`
- default `LabelOffsetY = 0.0`

Отдельно фиксируем, что label bounds больше не должны раздувать viewport. Для PowerShell-path эти defaults применяются только когда пользователь явно не передал свои значения.

Причины:

- `Patch 7a` показал, что remaining text mismatch сидел не в содержимом label-ов, а в semantics:
  - font family
  - font size
  - anchor
  - relative placement относительно центра node
- у `graphviz` node labels в наших reference `svg` рисуются centered с `Times,serif` и `text-anchor="middle"`
- старый managed path с внешним label placement и `8pt sans-serif` давал заметный визуальный шум и pixel diff даже при правильной geometry узлов и рёбер
- label bounds не должны влиять на page size, иначе text parity начинает ломать viewport parity

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpLabelLayouter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpOptions.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderScenePipeline.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRenderScene.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphSvgRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-LabeledGraphs.ps1`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `6/6` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `13/13` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7b`
- result: `3/3` labeled cases completed successfully
- result: for all labeled cases:
  - `NodeLabelCountDelta = 0`
  - `MissingNodeLabelsInManaged = []`
  - `UnexpectedNodeLabelsInManaged = []`
  - `GraphvizFontFamilies = ["Times,serif"]`
  - `ManagedFontFamilies = ["Times,serif"]`
  - `GraphvizTextAnchors = ["middle"]`
  - `ManagedTextAnchors = ["middle"]`
  - `AverageFontSizeDelta = 0.0`
  - `AverageOffsetXDelta = 0.0`
- result: `single-edge-labeled`:
  - `AverageBaselineOffsetYDelta = 0.0`
- result: `triangle-cycle-labeled`:
  - `AverageBaselineOffsetYDelta = 8.881784197001252E-16`
- result: `star-labeled`:
  - `AverageBaselineOffsetYDelta = -8.881784197001252E-16`

Следствие:

- `Patch 7b` закрывает крупный semantic mismatch по text/font/anchor для `svg`
- remaining text differences теперь стоит искать уже в renderer-specific деталях:
  - glyph shaping
  - font availability
  - raster anti-aliasing
- следующие compare-прогоны по `png/jpg` надо трактовать уже без прежней скидки на неправильные defaults текста

## 2026-03-30 20:24 PDT - Patch 7c добавляет raster text telemetry и лёгкий Skia text tuning

Решение: следующий шаг внутри `Patch 7` делаем не как большой слепой rewrite raster text path, а как измеряемый подпатч:

- добавляем explicit `SkiaSharp` text flags для raster path
- пишем эти flags в managed diagnostics
- добавляем в compare harness нормализованные raster density metrics, чтобы отделить text weight от общего размера кадра

Принятые настройки для текущего baseline:

- `HintingLevel = Slight`
- `SubpixelText = true`
- `LcdRenderText = true` для opaque background path
- `IsAutohinted = true`

Причины:

- после `Patch 7b` `svg`-semantics по тексту уже совпали, но `png/jpg` всё ещё расходились и raw pixel counts были плохо интерпретируемы без нормализации
- labelled cases показали, что raw `DarkPixelDelta` может сильно зависеть от размера output-а, а не только от веса glyph-ов
- нужен был следующий слой telemetry, который позволит говорить не только “пикселей больше/меньше”, но и “текст плотнее/легче при той же opaque area”
- небольшой `SkiaSharp` text tuning имеет смысл только если его можно сразу измерить и потом либо оставить, либо откатить

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-LabeledGraphs.ps1`
- code change: `/Users/andrei/repo/PSGraphView/tests/PSGraphView.GVExport.Tests/GraphRasterRenderSceneWriterTests.cs`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `6/6` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7c`
- result: `3/3` labeled cases completed successfully
- result: managed diagnostics now report:
  - `textHintingLevel = "Slight"`
  - `subpixelText = true`
  - `lcdRenderText = true`
  - `autohintedText = true`
- result: `single-edge-labeled`:
  - `PngDarkPixelDelta` improved from `24` to `19`
  - `JpgDarkPixelDelta` improved from `34` to `29`
  - `PngDarkPixelDensityDelta = -0.091`
  - `JpgDarkPixelDensityDelta = -0.021`
- result: `star-labeled`:
  - `PngDarkPixelDelta` improved from `618` to `612`
  - `JpgDarkPixelDelta` improved from `788` to `777`
  - `PngDarkPixelDensityDelta = 0.062`
  - `JpgDarkPixelDensityDelta = 0.083`
- result: `triangle-cycle-labeled`:
  - `PngDarkPixelDelta = -350`
  - `JpgDarkPixelDelta = -289`
  - `PngDarkPixelDensityDelta = 0.284`
  - case remains layout-limited and is not a good final judge of raster text tuning

Следствие:

- у нас появился usable raster text baseline, который уже не сводится только к raw pixel count
- текущий `SkiaSharp` tuning даёт небольшой плюс на labeled cases с близким output size, но не закрывает весь remaining raster text mismatch
- следующий meaningful шаг для `Patch 7` уже не в “ещё одном флаге antialiasing”, а в:
  - same-geometry compare
  - font availability / fallback inspection
  - возможно, отдельном label-only raster experiment

## 2026-03-30 20:42 PDT - Patch 7d показывает, что current raster text mismatch не объясняется простым font fallback

Решение: добавляем explicit font resolution telemetry в compare loop и принимаем вывод, что на текущей машине `graphviz` и managed фактически сходятся на одной и той же resolved family для node labels: `Times New Roman`.

Причины:

- после `Patch 7c` всё ещё оставался вопрос, не уходит ли `SkiaSharp` в другой fallback font, из-за чего и расходятся raster glyph-ы
- без этого любая следующая попытка чинить raster text parity была бы гаданием
- `graphviz -v` уже пишет строку `fontname: ... resolved to ...`, а managed raster path теперь может писать фактически выбранные `SKTypeface.FamilyName`

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-LabeledGraphs.ps1`
- code change: `/Users/andrei/repo/PSGraphView/tests/PSGraphView.GVExport.Tests/GraphRasterRenderSceneWriterTests.cs`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `7/7` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7d`
- result: `3/3` labeled cases completed successfully
- result: for all labeled cases:
  - `GraphvizResolvedFamilies = ["Times New Roman"]`
  - `ManagedResolvedFamilies = ["Times New Roman"]`
  - `MissingResolvedFamiliesInManaged = []`
  - `UnexpectedResolvedFamiliesInManaged = []`
- result: `single-edge-labeled` detailed comparison:
  - graphviz requested `Times-Roman`
  - graphviz resolved family `Times New Roman`
  - managed diagnostics `resolvedLabelFontFamilies = "Times New Roman"`

Следствие:

- на этой машине remaining raster text mismatch уже нельзя списать на другой resolved font family
- значит следующий remaining слой сидит глубже:
  - glyph shaping / hinting differences между Pango/Cairo и Skia
  - либо общий scene-density noise
- следующий meaningful шаг внутри `Patch 7`:
  - same-geometry compare
  - или label-only raster experiment, где geometry фиксирована и видна только разница рендера текста

## 2026-03-30 21:02 PDT - Patch 7e подтверждает local raster text mismatch через label ROI telemetry

Решение: добавляем в labelled compare отдельный `label ROI` слой метрик и принимаем его как следующий рабочий baseline для raster text parity.

Причины:

- после `Patch 7d` стало ясно, что проблема не в простом font fallback
- whole-image raster metrics всё ещё смешивали в одну кучу:
  - text weight
  - scene size
  - node/edge density
- нужен был локальный metric именно по области label-а, чтобы увидеть, расходится ли сам raster text после вычитания общего scene noise

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-LabeledGraphs.ps1`
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7e`
- result: `3/3` labeled cases completed successfully
- result: `single-edge-labeled`:
  - `PngLabelDarkDelta = 38`
  - `PngLabelDarkDensityDelta = -0.091`
  - `JpgLabelDarkDelta = 58`
  - `JpgLabelDarkDensityDelta = -0.021`
- result: `triangle-cycle-labeled`:
  - `PngLabelDarkDelta = -197`
  - `PngLabelDarkDensityDelta = 0.141`
  - `JpgLabelDarkDelta = -148`
  - `JpgLabelDarkDensityDelta = 0.164`
- result: `star-labeled`:
  - `PngLabelDarkDelta = 743`
  - `PngLabelDarkDensityDelta = 0.027`
  - `JpgLabelDarkDelta = 933`
  - `JpgLabelDarkDensityDelta = 0.046`

Следствие:

- local text region действительно расходится и после вычитания общего размера сцены
- это усиливает вывод, что remaining mismatch сидит уже в:
  - glyph shaping / hinting
  - либо в том, как graphviz и managed по-разному rasterize-ят один и тот же text box
- следующий meaningful шаг:
  - same-geometry compare
  - либо отдельный label-only / scene-only raster experiment уже без layout path вообще

## 2026-03-30 21:16 PDT - Patch 7f выделяет именно вклад текста через labeled vs unlabeled baseline

Решение: следующий baseline внутри `Patch 7` делаем как `label-only` contribution experiment:

- каждый labeled case прогоняется дважды:
  - с labels
  - без labels
- затем считаем contribution самого текста как разницу `labeled - unlabeled`
- и сравниваем уже contribution между `graphviz` и managed

Причины:

- `Patch 7e` уже показал, что local ROI по label-ам расходится
- но всё ещё оставался вопрос, какая часть этого ROI идёт от самого текста, а какая от фона node/scene внутри box
- baseline `labeled - unlabeled` убирает большую часть этого шума и даёт более чистую оценку raster contribution именно от текста

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-LabeledGraphs.ps1`
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7f`
- result: `3/3` labeled cases completed successfully
- result: `single-edge-labeled`:
  - `PngLabelOnly.DarkPixelContributionDelta = 46`
  - `PngLabelOnly.DarkPixelDensityContributionDelta = -0.070`
  - `JpgLabelOnly.DarkPixelContributionDelta = 34`
  - `JpgLabelOnly.DarkPixelDensityContributionDelta = -0.028`
- result: `triangle-cycle-labeled`:
  - `PngLabelOnly.DarkPixelContributionDelta = -104`
  - `PngLabelOnly.DarkPixelDensityContributionDelta = 0.154`
  - `JpgLabelOnly.DarkPixelContributionDelta = -126`
  - `JpgLabelOnly.DarkPixelDensityContributionDelta = 0.150`
- result: `star-labeled`:
  - `PngLabelOnly.DarkPixelContributionDelta = 787`
  - `PngLabelOnly.DarkPixelDensityContributionDelta = 0.035`
  - `JpgLabelOnly.DarkPixelContributionDelta = 906`
  - `JpgLabelOnly.DarkPixelDensityContributionDelta = 0.045`

Следствие:

- remaining mismatch теперь подтверждён уже не только whole-image и не только local ROI, а именно на уровне raster contribution самого текста
- следующий шаг внутри `Patch 7` уже просится как:
  - same-geometry compare
  - или synthetic label scene / text-only experiment

## 2026-03-30 20:53 PDT - Patch 7g отделяет raster text mismatch от layout и canvas-size noise

Решение: добавляем отдельный same-geometry baseline `demos/Compare-Export-FixedTextScene.ps1` и принимаем его как основной reference для следующих text-raster экспериментов внутри `Patch 7`.

Причины:

- после `Patch 7f` было уже понятно, что mismatch сидит в raster contribution текста, но ещё оставался шум от:
  - layout geometry
  - общего scene composition
  - разных raster dimensions
- нужен был baseline, где:
  - geometry берётся из `graphviz`
  - `graphviz` рендерит fixed-text scene через `neato -n`
  - managed рендерит тот же text-only scene напрямую через `GVExport`
- отдельный шаг с принудительным выравниванием managed raster canvas под pixel size reference-вывода `graphviz` нужен был, чтобы не спутать glyph mismatch с mismatch по размеру картинки

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-FixedTextScene.ps1`
- run: `pwsh -NoProfile -File demos/Compare-Export-FixedTextScene.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-fixed-text-patch7g`
- result: `2/2` fixed-text cases completed successfully
- result: `single-edge-fixed-text`
  - `GraphvizResolvedFamilies = ["Times New Roman"]`
  - `ManagedResolvedFamilies = ["Times New Roman"]`
  - `Png WidthDelta = 0`
  - `Png HeightDelta = 0`
  - `PngDarkPixelDelta = 73`
  - `PngDarkPixelDensityDelta = 4.0677`
  - `Jpg WidthDelta = 0`
  - `Jpg HeightDelta = 0`
  - `JpgDarkPixelDelta = -73`
  - `JpgDarkPixelDensityDelta = -0.2173`
- result: `star-fixed-text`
  - `GraphvizResolvedFamilies = ["Times New Roman"]`
  - `ManagedResolvedFamilies = ["Times New Roman"]`
  - `Png WidthDelta = 0`
  - `Png HeightDelta = 0`
  - `PngDarkPixelDelta = 2889`
  - `PngDarkPixelDensityDelta = 17.1272`
  - `Jpg WidthDelta = 0`
  - `Jpg HeightDelta = 0`
  - `JpgDarkPixelDelta = -240`
  - `JpgDarkPixelDensityDelta = -0.0605`

Следствие:

- resolved font family и canvas size уже не объясняют remaining mismatch
- после вычитания layout-noise и size-noise расхождение остаётся именно в rasterization text-only scene
- дальше внутри `Patch 7` нужно копать уже не `font-family`, а:
  - alpha / clear policy
  - premultiplied-alpha behavior
  - различия `Pango/Cairo/CoreText` против `SkiaSharp/CoreText` на text-only render path

## 2026-03-30 21:12 PDT - Patch 7h показывает, что fixed-text PNG mismatch в основном сидел в background semantics

Решение: для same-geometry fixed-text baseline managed-сторону переводим на graphviz-like opaque white raster background и принимаем вывод, что основной крупный mismatch в `png` был вызван не glyph shaping, а различием device background semantics.

Причины:

- `Patch 7g` всё ещё показывал огромный разъезд по `png`, хотя:
  - canvas size уже совпадал
  - resolved font family уже совпадала
- визуальный preview при этом показывал, что managed `png` выглядит как почти чёрный прямоугольник, а reference `graphviz png:cairo` выглядит как нормальный текст на белом фоне
- это указывало, что следующий честный шаг нужно делать не в production renderer, а в synthetic baseline:
  - сначала выровнять background semantics text-only device path
  - и только потом смотреть, что осталось от собственно glyph mismatch

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-FixedTextScene.ps1`
- run: `pwsh -NoProfile -File demos/Compare-Export-FixedTextScene.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-fixed-text-patch7h`
- result: `2/2` fixed-text cases completed successfully
- result: `single-edge-fixed-text`
  - `GraphvizResolvedFamilies = ["Times New Roman"]`
  - `ManagedResolvedFamilies = ["Times New Roman"]`
  - `Png WidthDelta = 0`
  - `Png HeightDelta = 0`
  - `TransparentPixelDelta = 0`
  - `PngDarkPixelDelta = -21`
  - `PngDarkPixelDensityDelta = -0.0816`
  - previously on `Patch 7g`: `PngDarkPixelDelta = 73`, `PngDarkPixelDensityDelta = 4.0677`
- result: `star-fixed-text`
  - `GraphvizResolvedFamilies = ["Times New Roman"]`
  - `ManagedResolvedFamilies = ["Times New Roman"]`
  - `Png WidthDelta = 0`
  - `Png HeightDelta = 0`
  - `TransparentPixelDelta = 0`
  - `PngDarkPixelDelta = -123`
  - `PngDarkPixelDensityDelta = -0.0395`
  - previously on `Patch 7g`: `PngDarkPixelDelta = 2889`, `PngDarkPixelDensityDelta = 17.1272`
- result: `jpg` residual remains more noticeable:
  - `single-edge-fixed-text`: `JpgDarkPixelDelta = -73`, `JpgNearFallbackPixelDelta = 74`
  - `star-fixed-text`: `JpgDarkPixelDelta = -287`, `JpgNearFallbackPixelDelta = 293`

Следствие:

- крупный text-only `png` mismatch из `Patch 7g` оказался в основном artefact-ом от разной background policy
- после выравнивания white background `png` уже близок к reference даже без новых font/glyph changes
- следующий meaningful шаг внутри `Patch 7` теперь уже более узкий:
  - micro-baseline на один label
  - и отдельная доводка `jpg` opaque conversion / text weight

## 2026-04-02 09:18 PDT - Patch 7i показывает, что на одном label remaining text mismatch уже маленький

Решение: добавляем в fixed-text baseline отдельный micro-case `single-label-fixed-text` и принимаем вывод, что после `Patch 7h` большого per-glyph mismatch уже не видно.

Причины:

- после `Patch 7h` всё ещё оставался вопрос, не маскируют ли multi-label cases маленький, но системный glyph mismatch
- для честного ответа нужен был самый маленький text-only case:
  - один label
  - тот же resolved font family
  - те же pixel dimensions
  - тот же compare loop для `png/jpg`

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export-FixedTextScene.ps1`
- run: `pwsh -NoProfile -File demos/Compare-Export-FixedTextScene.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-fixed-text-patch7i`
- result: `3/3` fixed-text cases completed successfully
- result: `single-label-fixed-text`
  - `GraphvizResolvedFamilies = ["Times New Roman"]`
  - `ManagedResolvedFamilies = ["Times New Roman"]`
  - `Png WidthDelta = 0`
  - `Png HeightDelta = 0`
  - `PngDarkPixelDelta = -4`
  - `PngDarkPixelDensityDelta = -0.0625`
  - `Jpg WidthDelta = 0`
  - `Jpg HeightDelta = 0`
  - `JpgDarkPixelDelta = -7`
  - `JpgDarkPixelDensityDelta = -0.1094`
  - `JpgNearFallbackPixelDelta = 7`
- visual preview:
  - `png` managed и `graphviz` почти совпадают
  - `jpg` тоже близок, но managed остаётся немного светлее reference

Следствие:

- после выравнивания background semantics и перехода к micro-case большого per-glyph mismatch уже не видно
- remaining residual теперь выглядит узким и, скорее всего, сидит в:
  - `jpg` opaque conversion
  - compression / quality
  - небольшом различии text weight после flattening
- это уже не похоже на крупную ошибку text pipeline

## 2026-04-04 09:07 PDT - Patch 8 синхронизирует docs/help с текущим Sfdp export surface

Решение: закрываем `Patch 8` обновлением пользовательской документации и help-файлов под реальное текущее состояние cmdlet surface:

- managed `Sfdp` экспортирует `Svg`, `Png`, `Jpg`
- `README`, markdown-help и `dll-Help.xml` должны говорить об одном и том же

Причины:

- после `Patch 4+` и последующих parity-патчей код уже давно умеет direct raster export, но docs/help продолжали утверждать, что `Sfdp` умеет только `Svg`
- это уже стало user-visible несоответствием между:
  - кодом
  - тестами
  - README
  - platyPS markdown
  - external help XML
- финальный cleanup имеет смысл только после стабилизации export surface, и к этому моменту мы как раз пришли

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/README.md`
- code change: `/Users/andrei/repo/PSGraphView/docs/powershell/Export-GraphView.md`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/en-US/PSGraphView.PowerShell.dll-Help.xml`
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed

Следствие:

- docs/help больше не отстают от текущего `Sfdp` export surface
- пользователь теперь видит корректную картину:
  - direct `Svg`
  - direct `Png`
  - direct `Jpg`
- remaining open work после этого уже не про docs, а только про optional доводку узкого `jpg` residual внутри `Patch 7`

## 2026-04-04 12:31 PDT - Patch 7j переводит compare-run на graphviz-like node style для плотных графов

Решение: добавляем в `Sfdp` отдельный graphviz-like node style mode и включаем его по умолчанию в parity compare harness.

Что именно принято:

- у graphviz-like режима узлы рисуются как:
  - transparent fill / `fill="none"`
  - black stroke / `stroke="#000000"`
- в raster path полностью прозрачный fill не рисуется вообще
- в cmdlet surface это доступно через:
  - `-SfdpGraphvizNodeStyle`
- compare scripts используют этот режим по умолчанию, потому что он лучше соответствует reference output `graphviz`

Причины:

- на полном `WikiVote` визуальный разъезд по вершинам был не только в размерах, но и в самом node style
- в reference `graphviz` dense nodes выглядели почти чёрными точками, потому что:
  - node interior прозрачный
  - через него виден накопленный тёмный слой рёбер
- в managed exporter nodes были с непрозрачной серой заливкой и серой обводкой
- из-за этого managed path перекрывал тёмные edge bundles под node и визуально давал светлые точки вместо graphviz-like тёмных

Телеметрия:

- full-graph artifact inspection:
  - graphviz full SVG: `/tmp/psgraphview-export-wikivote-full-gvplugins/wiki-vote-full/svg/wiki-vote-full-graphviz.svg`
  - managed full SVG before patch: `/tmp/psgraphview-export-wikivote-full/wiki-vote-full/svg/wiki-vote-full-managed.svg`
- observed reference node style in graphviz full SVG:
  - `fill="none"`
  - `stroke="black"`
  - `rx="0.72" ry="0.72"`
- observed pre-patch managed node style in SVG:
  - `fill="#696969"`
  - `stroke="#555555"`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpOptions.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphSvgRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `14/14` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `14/14` passed
- run: `GV_PLUGIN_PATH=/tmp/graphviz-prefix/lib/graphviz pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch7j`
- result: `30 vertices / 99 edges`
- result: managed `svg` node elements after patch:
  - `fill="none"`
  - `stroke="#000000"`
- result: raster compare after patch:
  - `PngWidthDelta = 30`
  - `PngHeightDelta = 21`
  - `PngDarkPixelDelta = -1833`
  - `JpgWidthDelta = 30`
  - `JpgHeightDelta = 21`
  - `JpgDarkPixelDelta = -1108`

Следствие:

- visual mismatch по dense node markers теперь не маскируется непрозрачной node fill на managed-стороне
- compare harness стал ближе к реальному graphviz node semantics
- remaining residual после этого уже надо трактовать как size / density / raster-weight mismatch, а не как ошибку node fill policy

## 2026-04-04 13:04 PDT - Patch 9 переводит full-case compare harness на fast SVG summary mode

Решение: для очень больших `svg` compare harness больше не пытается всегда считать полный detailed edge visibility summary. На full-case он автоматически переключается в fast mode и всё равно дописывает итоговый `comparison.json`.

Что именно принято:

- для больших графов detailed edge visibility и длинные edge title lists больше не считаются обязательными
- вместо этого harness сохраняет:
  - structural `svg` counters
  - node visibility
  - raster metrics
  - diagnostics comparison
- в summary это явно помечается как:
  - `SummaryMode = "Fast"`
  - `EdgeVisibilityAvailable = false`
  - `EdgeTitleListsAvailable = false`
- отдельный bug в `Get-AverageOrNull` на пустом массиве label metrics тоже исправлен

Причины:

- полный `Compare-WikiVote-Export.ps1` на `7115 / 103689` до этого не доходил до финального `comparison.json`
- exporter сам по себе работал, но harness зависал или падал на summary-stage
- основная cost-проблема была в detailed edge visibility:
  - для каждого edge path считался sampled bezier bbox
  - это было слишком дорого для full `WikiVote`
- дополнительно `Get-AverageOrNull` падал на graphs без label-ов, что делало full SVG summary partial даже после fast-path

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1`
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `14/14` passed
- run: `GV_PLUGIN_PATH=/tmp/graphviz-prefix/lib/graphviz pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch9-subgraph`
- result: subgraph case completed successfully
- run: `GV_PLUGIN_PATH=/tmp/graphviz-prefix/lib/graphviz pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-wikivote-patch9-full-2`
- result: full case completed successfully and wrote `/tmp/psgraphview-export-wikivote-patch9-full-2/wiki-vote-full/wiki-vote-full-comparison.json`
- result: full-case `Svg` comparison:
  - `Available = true`
  - `SummaryMode = "Fast"`
  - `EdgeVisibilityAvailable = false`
  - `EdgeTitleListsAvailable = false`
  - `VisibleNodeCoverageRatio = 1.0`
- result: full-case residuals remain measurable:
  - `PNG WidthDelta = 61`
  - `PNG HeightDelta = -30`
  - `PNG DarkPixelDelta = -213220`
  - `JPG WidthDelta = 61`
  - `JPG HeightDelta = -30`
  - `JPG DarkPixelDelta = -135830`

Следствие:

- полный `WikiVote` теперь можно использовать как repeatable compare-case без ручного обхода
- harness больше не смешивает “экспортер сломан” и “summary слишком тяжёлый”
- detailed edge visibility остаётся на small-case и subgraph-case, где она действительно полезна и недорога
