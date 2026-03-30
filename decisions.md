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
