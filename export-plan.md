# План сближения export pipeline `PSGraphView` с `graphviz` для `svg/png/jpg`

## Цель

Сблизить managed-export в `PSGraphView` с `graphviz` максимально близко к `1:1` по:

- ходу export pipeline после layout
- семантике viewport / dpi / scale / translation
- структуре `svg`
- поведению raster-export для `png` и `jpg`
- итоговому визуальному результату на реальных графах

Важно:

- эта работа должна быть изолирована от layout parity
- export-патчи не должны смешиваться с новыми algorithmic-правками в `Sfdp`
- где возможно, сравнение надо делать на одинаковой геометрии, чтобы layout noise не маскировал export mismatch
- мы не портируем cairo renderer; мы портируем graphviz semantics, используя managed backend.
- production-код нового export pipeline нужно вести в отдельном проекте `PSGraphView.GVExport`, а не раздувать `PSGraphView.Sfdp`.

Основные сравнительные сценарии:

- `demos/Compare-WikiVote-Export.ps1`
- `demos/Compare-Export-SmallGraphs.ps1`

Примечание:

- для быстрого smoke-run `Compare-WikiVote-Export.ps1` можно запускать на маленьком `SubgraphSeedCount`
- `Compare-WikiVote-Export.ps1` теперь должен поддерживать как минимум два subgraph mode:
  - `InducedTopDegree` для простого smoke-run
  - `ExpandedTopDegree` для более плотного large-graph compare-case
- для содержательного сравнения edge/path structure лучше использовать `ExpandedTopDegree`, потому что он даёт более насыщенный подграф по рёбрам

---

## Статус на сегодня

Сделано:

- managed `Sfdp` уже умеет экспортировать `svg`, однако это, скорее, кастомная реализация, нежели часть `graphviz`. ее можно заменить на порт, полностью, ее не нужно сохранять.
- есть текущий diagnostic path в `Sfdp`:
  - `layout.geometry`
  - `component.geometry`
  - `svg.geometry`
- есть рабочий compare harness для layout parity:
  - `demos/Compare-WikiVote-Sfdp.ps1`

Фактическое текущее состояние export surface:

- `Export-GraphView` для `Sfdp` и `MSAGL` сейчас поддерживает только `Svg`
- `ViewOutputKind` сейчас знает только:
  - `Json`
  - `Html`
  - `Svg`
- `CmdletOutputHelpers` умеет выводить и определять по пути только `.json/.html/.svg`
- docs/help прямо говорят, что `Sfdp` и `MSAGL` сейчас поддерживают только `SVG`
- отдельного export-проекта (`PSGraphView.GVExport`) в solution пока нет

Критическое уточнение после повторной визуальной проверки артефактов:

- `Patch 3` действительно сблизил структуру `svg`, но не закрыл visual parity
- managed `svg` из прогонов `Patch 3b`, `Patch 4`, `Patch 4b`, `Patch 5`, `Patch 5b`, `Patch 5c` для одних и тех же case-ов оказался одинаковым byte-for-byte
- значит дальнейшие patch-и улучшали diagnostics и raster path, но не исправляли проблемную `svg` geometry
- на реальных preview это видно явно:
  - `WikiVote 30 / 99`: graphviz показывает весь граф, managed почти пустой и уехавший вниз
  - `self-loop`: у managed теряется сама петля
  - `triangle-cycle`: у managed клипается почти весь граф
- из этого следует новый обязательный corrective step:
  - `Patch 3c` должен починить `svg` geometry / transform / bounds parity
  - до этого нельзя считать результаты `Patch 4+` честной оценкой export parity

Главные текущие ограничения managed-side:

- нет `png/jpg` export path
- нет общего render-scene между `svg` и будущими raster-форматами
- `SfdpSvgExporter` пишет `svg` напрямую, без graphviz-like render/device split
- текущий `svg` ближе к "свой аккуратный SVG", чем к буквальному `graphviz svg`
- текущая label-геометрия приблизительная:
  - ширина текста оценивается через `label.Length * fontSize * 0.56`
  - font family жёстко задан как `sans-serif`
- текущий exporter всегда рисует background `<rect>`, а у `graphviz` это определяется render/device semantics и graph attrs

Ключевые файлы текущей managed-реализации:

- `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- `src/PSGraphView.PowerShell/ViewOutputKind.cs`
- `src/PSGraphView.PowerShell/CmdletOutputHelpers.cs`
- `src/PSGraphView.Sfdp/SfdpSvgExporter.cs`
- `src/PSGraphView.Sfdp/SfdpLabelLayouter.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpSvgExporterTests.cs`

---

## Как это делает `graphviz` сейчас

### Общий pipeline

Опорный pipeline в `graphviz` выглядит так:

1. layout готовит geometry и `bb`
2. `lib/common/emit.c` вычисляет:
   - `pad`
   - `dpi`
   - `zoom`
   - `view`
   - `focus`
   - `translation`
   - `pageBoundingBox`
   - `width/height`
   - `clip`
3. `lib/gvc/gvrender.c` и plugin system выбирают renderer/device pair
4. renderer рисует примитивы
5. device финализирует bytes / file format

Ключевые reference-файлы:

- `../graphviz/lib/common/emit.c`
- `../graphviz/lib/gvc/gvrender.c`
- `../graphviz/lib/gvc/gvdevice.c`

### `svg`

Для `svg` опорный path не cairo-based, а core SVG plugin:

- renderer:
  - `../graphviz/plugin/core/gvrender_core_svg.c`
- device:
  - `svg:svg`

На текущем локальном коде это выглядит так:

- `gvrender_svg_types[]`:
  - `svg` с priority `1`
- `gvdevice_svg_types[]`:
  - `svg:svg` с priority `1`
- `svg:cairo` в pango plugin есть, но идёт с priority `-10`

Практический вывод:

- для vector parity эталоном надо считать именно `-Tsvg:svg`
- не надо смешивать это с `svg:cairo`

Характерные особенности `graphviz svg`:

- root `<svg>` пишет `width` и `height` в `pt`
- `viewBox` задаётся из `pageBoundingBox`
- весь граф кладётся в `<g class="graph" ... transform="scale(...) rotate(...) translate(...)">`
- y-инверсия делается через transform / sign conventions renderer-а, а не ручной перестановкой во всём output
- есть graph/node/edge/cluster groups с `id` и `class`
- есть `<title>` для graph/node/edge
- есть anchor / tooltip / target path через `<a>` и отдельный `<g>`
- text style идёт через font mapping из graphviz font alias-ов
- есть поддержка gradient/fill/stroke-opacity/dash semantics

### `png`

Для `png` на типичном cairo/pango build опорный path такой:

- renderer:
  - `../graphviz/plugin/pango/gvrender_pango.c`
- device:
  - `png:cairo`

На текущем локальном коде:

- `png:cairo` в pango plugin имеет priority `10`
- `png:gd` в gd plugin имеет priority `1`
- значит для обычного `-Tpng` эталон на локальном build, скорее всего, именно cairo path

Характерные особенности:

- default dpi `96`
- рисование идёт на `cairo_image_surface`
- `scale/rotate/translate/clip` ставятся в cairo context в `begin_page`
- PNG bytes пишутся самим cairo
- device semantics и width/height уже зафиксированы на уровне `emit.c`

### `jpg`

Для `jpg/jpeg` на текущем локальном коде наиболее вероятный reference path такой:

- renderer:
  - `../graphviz/plugin/pango/gvrender_pango.c`
- device:
  - `jpg:cairo` / `jpeg:cairo` через `../graphviz/plugin/gd/gvdevice_gd.c`

Характерные особенности:

- render идёт в ARGB buffer
- затем gd делает JPEG encoding
- прозрачный фон при JPEG не сохраняется:
  - в коде есть явная логика с off-white transparent fallback
- quality у gd оставлен library-default (`JPEG_QUALITY = -1`)

Важно:

- на других сборках теоретически может победить другой backend
- compare harness должен не полагаться на "что само выбралось"
- reference format надо пиновать явно:
  - `-Tsvg:svg`
  - `-Tpng:cairo`
  - `-Tjpg:cairo`

---

## Что именно хотим портировать

Цель не просто "добавить PNG/JPG", а приблизить managed-export к graphviz по следующим слоям:

1. Viewport semantics.
   Хотим:
   - graphviz-like `pad`
   - graphviz-like `dpi`
   - graphviz-like `zoom`
   - graphviz-like `translation`
   - graphviz-like `pageBoundingBox`
   - graphviz-like width/height and clipping

2. Scene semantics.
   Хотим:
   - устойчивую промежуточную render-scene
   - одинаковый input для `svg/png/jpg`
   - отсутствие дублирования export-логики по форматам

3. SVG semantics.
   Хотим:
   - graphviz-like root `<svg>`
   - graphviz-like graph/node/edge groups
   - graphviz-like `<title>`
   - units, transform и style ближе к `graphviz svg`
   - позже, если понадобится:
     - anchors/tooltips/targets
     - font mapping
     - dash/opacity/gradient parity

4. Raster semantics.
   Хотим:
   - единый ARGB render surface
   - graphviz-like default dpi для raster
   - предсказуемый `png`
   - graphviz-like `jpg` flattening behavior

5. Compare tooling.
   Хотим:
   - такой же patch-by-patch цикл, как в `plan.md`
   - диагностические логи в обеих репах
   - сравнение не только "на глаз", а по структуре, числам и пикселям

---

## Принципы работы

Чтобы не смешать export parity с layout parity, работа должна идти по таким правилам:

- сначала фиксируем reference backend и compare harness
- потом выносим managed export в отдельный слой scene / viewport
- только после этого меняем `svg`
- raster path строим уже поверх scene, а не поверх парсинга собственного `svg`
- если надо сравнивать чисто export, добавляем fixed-geometry mode:
  - либо через scene dump/load
  - либо через импорт заранее зафиксированной geometry

Отдельно:

- `svg` и raster не должны расходиться по node/edge/label geometry
- docs/help меняем только после появления реального support
- layout-specific код остаётся в `PSGraphView.Sfdp`, а новый scene/viewport/raster/surface код заводим в `PSGraphView.GVExport`

---

## Что можно делать параллельно, а что нет

### Строго последовательно

Эти куски зависят друг от друга и должны идти по порядку:

1. `Patch 0`
   - сначала нужен reference compare harness
   - без него дальше нечем нормально мерить результат

2. `Patch 0a`
   - после harness нужен baseline diagnostics layer
   - без него трудно локализовать mismatch в viewport / scene / raster

3. `Patch 1`
   - общий render scene должен появиться раньше серьезной переработки `svg/png/jpg`

4. `Patch 2`
   - viewport semantics надо стабилизировать до fine-tuning `svg` и до raster

5. `Patch 3`
   - `svg` structure надо приблизить к `graphviz` до начала полной raster parity

6. `Patch 3c`
   - после structural parity нужен отдельный corrective patch на visual SVG geometry
   - без него raster-сравнение продолжает наследовать базовую поломку `svg`

7. `Patch 3d`
   - после закрытия visibility нужен отдельный patch на natural-size / curve-envelope parity

8. `Patch 3a`
   - backend decision должен быть зафиксирован до реальной реализации `png/jpg`

9. `Patch 4`
   - только после этого есть смысл вводить production raster surface

Почему это последовательная цепочка:

- `Patch 1` без `Patch 0/0a` даст код без нормальной измеримости
- `Patch 3c` нужен потому, что post-`Patch 3` visual review показал:
  - structure близка к `graphviz`
  - geometry всё ещё сломана
- `Patch 4` без `Patch 3a` слишком легко приведёт к случайному backend lock-in
- `Patch 5/6` без `Patch 2` будут шуметь из-за viewport mismatch, а без `Patch 3c` — ещё и из-за уже сломанной `svg` geometry

### Можно делать параллельно с низким риском

Эти направления можно запускать рядом, если основной последовательный путь уже не блокируется:

- внутри `Patch 0`:
  - `Compare-WikiVote-Export.ps1`
  - `Compare-Export-SmallGraphs.ps1`
  могут делаться параллельно, потому что это два независимых harness-а

- внутри `Patch 0a`:
  - managed diagnostics
  - local graphviz verbose patches
  можно делать параллельно, потому что это две стороны одного compare loop

- внутри `Patch 3`:
  - SVG structure tests
  - docs notes для внутреннего плана
  можно делать параллельно с кодом exporter-а

- внутри `Patch 3c`:
  - visual compare на small-cases
  - visual compare на `WikiVote`
  - дополнительные diagnostics по scene / viewport / transform
  можно вести параллельно, но сам фикс geometry должен собираться последовательно в одном месте

- внутри `Patch 3a`:
  - исследование `SkiaSharp`
  - проверка packaging / native assets
  - проверка text / antialiasing risk
  можно вести параллельно как независимые validation tasks

- после `Patch 4`:
  - `Patch 5` (`png` parity)
  - подготовка части compare tooling для raster diff
  можно вести рядом

- после `Patch 4`:
  - `Patch 6` (`jpg` parity)
  - отдельная работа по `jpg` flattening diagnostics
  тоже можно вести рядом с `png`, если у них не пересекается write-scope

### Частично параллельные куски

Есть задачи, которые можно начать заранее, но завершать только после базовых patch-ей:

- работа по `Patch 7`:
  - исследование text / font parity можно начать рано
  - но фиксировать поведение лучше уже после `Patch 3` и `Patch 4`

- docs/help из `Patch 8`:
  - черновики можно готовить заранее
  - финализировать только после стабилизации public surface

- pixel diff helpers:
  - можно писать вместе с `Patch 0`
  - но реальные baseline thresholds лучше утверждать после первых результатов `Patch 4/5/6`

### Что нельзя делать слишком рано

Не стоит начинать это до нужной базы:

- production `png/jpg` implementation до завершения `Patch 3a`
- продолжение parity-оценки `svg/png/jpg` как будто `Patch 3` уже закрыл visual SVG parity
- настройку pixel thresholds до появления стабильного raster output
- подробный font parity до фиксации viewport и scene
- публичные docs про `png/jpg` до рабочего user-facing surface
- попытки лечить `jpg/png` mismatch через post-processing собственного `svg`, если цель — graphviz-like raster path

### Практическая матрица зависимостей

- `Patch 0` -> блокирует почти всё
- `Patch 0a` -> блокирует осмысленную локализацию всех следующих patch-ей
- `Patch 1` -> блокирует чистую реализацию `Patch 4`
- `Patch 2` -> блокирует точную оценку `Patch 3/5/6`
- `Patch 3` -> закрывает structure-only слой, но не даёт ещё честной visual parity
- `Patch 3c` -> блокирует дальнейшую честную оценку `Patch 4/5/6` и должен быть сделан раньше новых parity-выводов по raster
- `Patch 3d` -> не блокирует саму visibility-проверку, но нужен для size parity и для сближения raster dimensions
- `Patch 3a` -> блокирует production-реализацию `Patch 4`
- `Patch 4` -> блокирует полноценные `Patch 5` и `Patch 6`
- `Patch 5` и `Patch 6` -> могут идти параллельно после `Patch 4`, но с аккуратным разделением файлов
- `Patch 7` -> лучше добивать после первых результатов `Patch 5/6`
- `Patch 8` -> в основном финальный cleanup

---

## Метрики сравнения

### Базовые метрики для viewport и scene

Сравнивать:

- `dpiX/dpiY`
- `padX/padY`
- `zoom`
- `translationX/translationY`
- `clip`
- `pageBoundingBox`
- `outputWidth/outputHeight`
- `viewBox`
- `nodeCount`
- `edgeCount`
- `labelCount`

Критерий:

- для числовых viewport-метрик цель — exact match или отличие не больше `0.01 pt`

### SVG-метрики

Сравнивать:

- root `width/height/viewBox`
- наличие и порядок основных групп:
  - `graph`
  - `node`
  - `edge`
- наличие `<title>` для graph/node/edge
- количество:
  - `<g>`
  - `<path>`
  - `<ellipse>` / `<circle>`
  - `<text>`
  - `<a>`
- style-поля:
  - `stroke`
  - `fill`
  - `stroke-width`
  - `stroke-dasharray`
  - `fill-opacity`
  - `stroke-opacity`
  - `font-family`
  - `font-size`
  - `text-anchor`

Геометрические SVG-метрики:

- `MaxNodeCenterDelta`
- `MeanNodeCenterDelta`
- `MaxLabelAnchorDelta`
- `MeanLabelAnchorDelta`
- `MaxPathEndpointDelta`
- `MeanPathEndpointDelta`
- `BoundingBoxDelta`

### Raster-метрики

Сравнивать:

- pixel width / height
- effective dpi
- background alpha / flattening behavior
- mean absolute pixel error
- max channel error
- diff pixel ratio
- SSIM

Для `jpg` отдельно сравнивать:

- background flattening color
- отсутствие alpha
- размер файла как вспомогательную метрику, не как основной критерий

### Метрики для реальных графов

На больших графах типа `WikiVote`:

- scene metrics должны быть основными
- pixel metrics нужны как итоговая контрольная проверка
- raw byte equality не использовать как основной критерий

---

## Диагностика, которую надо добавить

### Managed diagnostics

Нужны новые события:

- `render.viewport`
  - `dpiX`
  - `dpiY`
  - `padX`
  - `padY`
  - `zoom`
  - `translationX`
  - `translationY`
  - `clipMinX`
  - `clipMinY`
  - `clipMaxX`
  - `clipMaxY`
  - `pageBoxMinX`
  - `pageBoxMinY`
  - `pageBoxMaxX`
  - `pageBoxMaxY`
  - `outputWidth`
  - `outputHeight`

- `render.scene`
  - `nodeCount`
  - `edgeCount`
  - `labelCount`
  - `nodeShape`
  - `hasArrows`
  - `backgroundMode`

- `svg.structure`
  - `rootWidth`
  - `rootHeight`
  - `viewBox`
  - `graphGroupTransform`
  - `groupCount`
  - `titleCount`
  - `pathCount`
  - `textCount`
  - `anchorCount`

- `raster.surface`
  - `format`
  - `width`
  - `height`
  - `dpiX`
  - `dpiY`
  - `pixelFormat`
  - `backgroundFlattened`

- `raster.encode`
  - `format`
  - `byteCount`
  - `jpegQualityMode`
  - `transparentFallbackColor`

### Local graphviz verbose patches

Нужны локальные verbose-патчи в:

- `../graphviz/lib/common/emit.c`
  - логировать итоговые:
    - `dpi`
    - `pad`
    - `zoom`
    - `translation`
    - `clip`
    - `pageBoundingBox`
    - `width/height`
    - `output format`

- `../graphviz/plugin/core/gvrender_core_svg.c`
  - логировать:
    - root `width/height/viewBox`
    - graph group `transform`
    - counts по node/edge/text/path/title

- `../graphviz/plugin/pango/gvrender_pango.c`
  - логировать:
    - chosen cairo surface type
    - raster width/height
    - effective scale
    - clip
    - translation

- `../graphviz/plugin/gd/gvdevice_gd.c`
  - логировать:
    - target format
    - jpeg path vs png path
    - resolution
    - transparency handling
    - output byte count

Вывод этих verbose-логов должен складываться рядом с managed diagnostics так же, как это уже сделано в `Compare-WikiVote-Sfdp.ps1`.

---

## Compare harness, который нужен

### Основной сценарий

Новый основной demo-скрипт:

- `demos/Compare-WikiVote-Export.ps1`

Он должен:

1. Строить один и тот же входной граф.
2. Готовить output dir с подпапками:
   - `svg`
   - `png`
   - `jpg`
   - `logs`
   - `diff`
3. Явно вызывать graphviz с pinned format:
   - `-Tsvg:svg`
   - `-Tpng:cairo`
   - `-Tjpg:cairo`
4. Явно вызывать managed `Export-GraphView` с:
   - `-As Svg`
   - `-As Png`
   - `-As Jpg`
5. Сохранять:
   - итоговые файлы
   - graphviz verbose logs
   - managed diagnostics
   - comparison summary json

### Что должен считать compare-скрипт

Для `svg`:

- structure summary
- viewport summary
- node/edge/text counts
- geometry deltas

Для `png/jpg`:

- dimensions
- pixel diff
- SSIM
- mean absolute error
- heatmap / diff image

### Второй compare-скрипт

Нужен ещё маленький deterministic harness:

- `demos/Compare-Export-SmallGraphs.ps1`

Он должен прогонять набор маленьких графов, где легче локализовать mismatch:

- одиночное ребро
- bidirectional edge
- self-loop
- graph with labels
- graph with arrows
- graph with disconnected components
- graph with transparent / non-white background

Причина:

- `WikiVote` хорош для итоговой проверки
- но неудобен для локализации первых SVG/raster mismatches

---

## Порядок patch-ов

### Patch 0. Зафиксировать reference export-path и baseline compare harness

Статус:

- сделано

Цель:

- зафиксировать, что именно считаем эталоном в `graphviz`
- получить первый repeatable baseline для `svg/png/jpg`

Фокус:

- `demos/Compare-WikiVote-Export.ps1`
- `demos/Compare-Export-SmallGraphs.ps1`
- при необходимости helpers рядом с ними

План:

1. Сделать новый compare-script по образцу `Compare-WikiVote-Sfdp.ps1`.
2. Пиновать reference formats явно:
   - `svg:svg`
   - `png:cairo`
   - `jpg:cairo`
3. Сохранять рядом:
   - `graphviz.verbose.log`
   - `managed.diagnostics.jsonl`
   - `comparison.json`
4. Пока managed `png/jpg` не готовы:
   - скрипт должен уметь работать в partial mode
   - честно писать, что raster compare пока недоступен

Критерий готовности:

- один скрипт воспроизводимо собирает reference outputs и summary
- baseline проверен на `Compare-Export-SmallGraphs.ps1`
- `Compare-WikiVote-Export.ps1` проверен smoke-run на subgraph

### Patch 0a. Добавить export diagnostics в обе стороны без смены поведения

Статус:

- сделано

Цель:

- получить сравнимые viewport/render логи ещё до первых поведенческих правок

Фокус:

- managed:
  - `src/PSGraphView.Sfdp/SfdpSvgExporter.cs`
  - новый render diagnostics helper
- graphviz:
  - `../graphviz/lib/common/emit.c`
  - `../graphviz/plugin/core/gvrender_core_svg.c`
  - `../graphviz/plugin/pango/gvrender_pango.c`
  - `../graphviz/plugin/gd/gvdevice_gd.c`

План:

1. Добавить `render.viewport`, `render.scene`, `svg.structure`.
2. В локальном `graphviz` добавить сопоставимый verbose.
3. Научить compare-script парсить эти события в summary.

Критерий готовности:

- видно, где mismatch:
  - в viewport
  - в scene
  - в svg structure
  - в raster surface
- compare summary уже поднимает:
  - `Graphviz.Outputs.*.VerboseSummary`
  - `Managed.Outputs.*.DiagnosticsSummary`
  - `Comparisons.Diagnostics`

### Patch 0b. Добавить edge-preserving large-graph compare mode

Статус:

- сделано

Цель:

- получить большой reference-case, где на `WikiVote` реально есть рёбра и path structure, а не только вершины без дуг

Почему это понадобилось:

- нужен явный большой compare-case, где рёбер больше, чем в простом induced smoke-run
- такой сценарий лучше подходит для:
  - edge path parity
  - arrow parity
  - raster edge density checks

Важно:

- по факту прогона выяснилось, что прежний вывод про `0` рёбер был следствием бага в script:
  он не разворачивал `EdgeList` из `Get-OutEdge` / `Get-InEdge`
- после исправления:
  - `InducedTopDegree` тоже даёт рёбра
  - но `ExpandedTopDegree` всё равно полезен как более плотный compare-case

Фокус:

- `demos/Compare-WikiVote-Export.ps1`

План:

1. Добавить альтернативный subgraph mode, который сохраняет рёбра:
   - BFS/expansion from top-degree start vertices
2. Явно писать в summary, какой subgraph mode использовался.
3. Сохранить induced mode как smoke-вариант.
4. Исправить обход `Get-OutEdge` / `Get-InEdge`, чтобы script реально видел отдельные рёбра, а не `EdgeList` как единый объект.

Критерий готовности:

- есть повторяемый large-graph scenario, где `WikiVote` даёт достаточно плотный edge-case и пригоден для export parity
- в summary пишется `Selection.Mode`

### Patch 1. Развязать export от layout и ввести общий render scene

Статус:

- сделано

Цель:

- прекратить прямую генерацию `svg` из layout-результата
- подготовить одну общую scene для `svg/png/jpg`

Фокус:

- `src/PSGraphView.GVExport/`
  - новый render-scene model
  - viewport / device helpers
- `src/PSGraphView.Sfdp/`
  - layout-to-scene adapter
  - `SfdpSvgExporter`
- `src/PSGraphView.PowerShell/`
  - wiring нового export-layer

План:

1. Завести новый проект `PSGraphView.GVExport` и подключить его в solution.
2. Ввести internal render scene:
   - nodes
   - routed edges
   - labels
   - viewport info
   - style info
3. Оставить текущий public surface без breaking change.
4. Перевести `SfdpSvgExporter` на scene, не меняя итоговый output.

Результат:

1. Добавлен новый проект:
   - `src/PSGraphView.GVExport/`
2. В `PSGraphView.GVExport` заведены:
   - `GraphRenderScene`
   - `GraphRenderViewport`
   - `GraphRenderNode`
   - `GraphRenderEdge`
   - `GraphRenderLabel`
   - `GraphSvgRenderSceneWriter`
3. В `PSGraphView.Sfdp` добавлен layout-to-scene adapter:
   - `SfdpRenderSceneBuilder`
4. `SfdpSvgExporter` теперь:
   - считает layout как раньше
   - строит общий scene
   - пишет diagnostics уже от scene / viewport
   - генерирует `svg` через `PSGraphView.GVExport`
5. `PSGraphView.PowerShell` в этом patch не менялся:
   - direct reference на `GVExport` пока не нужен
   - cmdlet продолжает работать через `PSGraphView.Sfdp`

Телеметрия:

- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `7/7` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch1`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `NodeGroupDelta = 0` and `EdgeGroupDelta = 0`
- result: on all `6` small-graph cases `TitleDelta = -1` and `RectDelta = 1`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch1`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: `NodeGroupDelta = 0`, `EdgeGroupDelta = 0`, `TitleDelta = -1`, `RectDelta = 1`
- result: `Diagnostics.Available = true`

Критерий готовности:

- поведение то же
- scene есть
- можно строить один и тот же scene для `svg/png/jpg`

### Patch 2. Сблизить viewport semantics с `graphviz`

Статус:

- сделано

Цель:

- сделать managed viewport ближе к `emit.c`

Фокус:

- новый viewport helper в `src/PSGraphView.Sfdp/`
- `SfdpSvgExporter`

План:

1. Явно ввести:
   - `dpi`
   - `pad`
   - `zoom`
   - `translation`
   - `pageBoundingBox`
2. Для `svg` принять `72 dpi` semantics.
3. Для будущего raster-path заложить `96 dpi` default.
4. Перевести current width/height/viewBox logic на graphviz-like расчёт.

Результат:

1. Добавлен новый helper:
   - `src/PSGraphView.Sfdp/SfdpViewportCalculator.cs`
2. В export path теперь явно заведены и пишутся в diagnostics:
   - `dpiX`
   - `dpiY`
   - `rasterDefaultDpiX`
   - `rasterDefaultDpiY`
   - `zoom`
   - `rotation`
   - `padX`
   - `padY`
   - `translationX`
   - `translationY`
   - `pageBoundingBox*`
   - `layoutScale`
3. Для `GraphvizPoints` exporter теперь:
   - переводит layout coordinates в output-space через scale `72`
   - считает `pageBoundingBox` уже в output units
   - делает zero-based `viewBox`
   - применяет graphviz-like `pad = 4`
4. `SfdpSvgExporter` больше не использует старую схему:
   - `padding = max(nodeRadius * 2, 12)`
   - `viewBoxMinX = contentMinX - padding`
   - `viewBoxMinY = contentMinY - padding`
5. Добавлен regression test на zero-based viewport в `GraphvizPoints` mode.

Телеметрия:

- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `8/8` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch2b`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `ViewBoxMinXDelta = 0` and `ViewBoxMinYDelta = 0`
- result: `single-edge` improved from `OutputWidthDelta = -17`, `OutputHeightDelta = 17` to `OutputWidthDelta = 5`, `OutputHeightDelta = 1`
- result: `disconnected-components` improved from `OutputWidthDelta = -57`, `OutputHeightDelta = -5` to `OutputWidthDelta = 4`, `OutputHeightDelta = 0`
- result: `star` improved from `OutputWidthDelta = -57`, `OutputHeightDelta = -58` to `OutputWidthDelta = -2`, `OutputHeightDelta = -4`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch2b`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: `ViewBoxMinXDelta = 0`, `ViewBoxMinYDelta = 0`
- result: `OutputWidthDelta` improved from `-95` to `23`
- result: `OutputHeightDelta` improved from `-80` to `15`

Критерий готовности:

- viewport-метрики у managed и graphviz почти совпадают
- главный mismatch, если останется, уже сидит не в единицах и не в рамке страницы

### Patch 2a. Дотянуть natural export scale для больших графов

Статус:

- сделано

Зачем нужен follow-up:

- после `Patch 2` рамка страницы, `pad`, `dpi` и zero-based `viewBox` уже стали graphviz-like
- но на больших cases всё ещё остаётся остаточный natural-size mismatch
- на `WikiVote 30 / 99` managed `pageBoundingBox` всё ещё шире и выше эталона на `23 / 15`

Фокус:

- `src/PSGraphView.Sfdp/SfdpViewportCalculator.cs`
- compare telemetry из:
  - `demos/Compare-Export-SmallGraphs.ps1`
  - `demos/Compare-WikiVote-Export.ps1`

План:

1. Проверить, можно ли вычислять natural export scale ближе к `graphviz` из:
   - current content geometry
   - average edge length
   - packing / overlap-removal telemetry
2. Не смешивать эту работу с `Patch 3` по SVG structure.
3. Если устойчивого правила не найдётся, явно зафиксировать, что остаточный mismatch относится к layout scale, а не к viewport math.

Результат:

1. В export path добавлена подготовка для учёта routed edge geometry:
   - `SfdpEdgeRouter.RoutedEdge` теперь хранит routed points
   - добавлен cubic bezier bounds helper
2. В tests добавлен regression test на routed self-loop bounds.
3. По telemetry подтверждено:
   - zero-based `viewBox` и `pageBoundingBox` уже не проблема
   - residual large-graph mismatch не уходит через дополнительные viewport tweaks
4. Вывод:
   - остаточный `23 / 15` на `WikiVote 30 / 99` относится не к viewport math
   - дальше это надо разбирать как:
     - layout scale mismatch
     - edge routing / curve style mismatch
     - SVG transform / structure mismatch в следующих patch-ах

Телеметрия:

- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `9/9` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch2a-final`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `ViewBoxMinXDelta = 0` and `ViewBoxMinYDelta = 0`
- result: `single-edge` stayed at `OutputWidthDelta = 5`, `OutputHeightDelta = 1`
- result: `self-loop` stayed at `OutputWidthDelta = -17`, `OutputHeightDelta = -12`
- result: `triangle-cycle` stayed at `OutputWidthDelta = -27`, `OutputHeightDelta = -19`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch2a-final`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: `ViewBoxMinXDelta = 0`, `ViewBoxMinYDelta = 0`
- result: `OutputWidthDelta = 23`, `OutputHeightDelta = 15`

Критерий готовности:

- либо large-graph width/height delta уменьшается ещё заметно
- либо в `decisions.md` жёстко зафиксировано, почему дальше это уже не viewport problem

### Patch 3. Перевести SVG output на graphviz-like structure

Статус:

- сделано

Цель:

- приблизить managed SVG к `gvrender_core_svg.c`

Фокус:

- `src/PSGraphView.GVExport/GraphSvgRenderSceneWriter.cs`
- `src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
- тесты:
  - `tests/PSGraphView.Sfdp.Tests/SfdpSvgExporterTests.cs`

План:

1. Сделать root `<svg>` ближе к graphviz:
   - `width="...pt"`
   - `height="...pt"`
   - graphviz-like `viewBox`
2. Завести `<g class="graph" ... transform="...">`.
3. Сблизить:
   - node groups
   - edge groups
   - titles
   - classes / ids
4. Убрать явную ручную геометрию там, где graphviz полагается на group transform.
5. Добавить недостающие SVG-структурные тесты.

Результат:

1. Root `<svg>` теперь ближе к `graphviz svg`:
   - `width` и `height` пишутся в `pt`
   - `viewBox` пишется в zero-based graphviz-like формате
   - добавлен `xmlns:xlink`
2. Граф теперь оборачивается в graphviz-like group:
   - `<g id="graph0" class="graph" transform="...">`
   - `<title>G</title>`
   - background рисуется через `<polygon>`, а не через `<rect>`
3. Node shape переведён на `<ellipse rx="..." ry="...">`, чтобы форма была ближе к `graphviz`.
4. Scene builder теперь готовит graph-group coordinates и graph-group transform вместо полностью "развёрнутой" page-space геометрии.
5. Убраны лишние контейнеры `g#edges` / `g#nodes`, из-за которых было расхождение по `GroupCount`.

Телеметрия:

- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch3b`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `GroupCountDelta = 0`, `TitleDelta = 0`, `RectDelta = 0`
- result: on all `6` small-graph cases `GraphGroupId = graph0`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3b`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: `GroupCountDelta = 0`, `TitleDelta = 0`, `RectDelta = 0`
- result: `GraphGroupId = graph0`
- result: `GraphGroupTransform = scale(1 1) rotate(0) translate(4 118.982)`
- result: `WidthMatch = false`, `HeightMatch = false`, `ViewBoxMatch = false`

Критерий готовности:

- structure summary становится близким к `graphviz svg`
- diff по `svg` уже читается как fine details, а не как другой exporter

Уточнение после повторной visual-проверки:

- `Patch 3` нельзя считать закрытием visual SVG parity
- он закрыл только structure-layer:
  - `GroupCountDelta = 0`
  - `TitleDelta = 0`
  - `RectDelta = 0`
- по geometry этот patch оставил критическую проблему:
  - `managed.svg` визуально плохой на `self-loop`, `triangle-cycle`, `bidirectional-edge` и `WikiVote`
  - артефакты `managed.svg` от `Patch 3b` до `Patch 5c` для тех же case-ов остались одинаковыми byte-for-byte
- поэтому ниже добавлен отдельный `Patch 3c`

### Patch 3c. Исправить SVG geometry / transform / bounds parity

Статус:

- сделано

Цель:

- устранить базовую visual-поломку managed `svg`, которая сохранилась после `Patch 3`
- добиться того, чтобы `svg` перестал терять контент, клипать петли и уводить граф за `viewBox`
- сделать `svg` пригодной опорой для дальнейшей честной оценки `png/jpg`

Почему это отдельный corrective patch:

- `Patch 3` закрыл только structural parity, но не visual parity
- дальнейшие patch-и (`4/5`) уже улучшали raster pipeline, однако сравнивали его с `svg` geometry, которая всё ещё сломана
- без отдельного исправления `svg` geometry невозможно уверенно отделить:
  - ошибку transform / page bounds
  - ошибку raster surface
  - ошибку encoder / flattening policy

Фокус:

- `src/PSGraphView.Sfdp/SfdpRenderSceneBuilder.cs`
- `src/PSGraphView.Sfdp/SfdpViewportCalculator.cs`
- `src/PSGraphView.GVExport/GraphSvgRenderSceneWriter.cs`
- при необходимости:
  - `src/PSGraphView.Sfdp/SfdpEdgeRouter.cs`
  - `src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs`
- тесты:
  - `tests/PSGraphView.Sfdp.Tests/SfdpSvgExporterTests.cs`
  - compare scripts и их summary

Основная гипотеза:

- current scene builder неправильно применяет graphviz-like page semantics:
  - использует `PaddingX`, а не рассчитанный `TranslationX`
  - использует `contentBounds.MaxY`, а не page/viewport-derived translate semantics
  - из-за этого scene geometry и `viewBox` оказываются в разных coordinate systems
- результат:
  - часть графа оказывается вне видимой области
  - self-loop / curved-edge bounds учитываются недостаточно
  - raster path потом наследует ту же базовую ошибку

План:

1. Зафиксировать regression как explicit goal в telemetry и tests:
   - `self-loop`
   - `triangle-cycle`
   - `bidirectional-edge`
   - `wiki-vote-expanded-30-s3`
2. Перевести scene builder на использование рассчитанных viewport translation values:
   - не `PaddingX`
   - а `TranslationX / TranslationY` или эквивалентную graphviz-like page transform semantics
3. Выровнять систему координат между:
   - `viewBox`
   - graph-group `transform`
   - node coordinates
   - edge path coordinates
   - label coordinates
4. Пересчитать background polygon из page-bounds / graph transform semantics, а не из случайного локального bounds mix.
5. Расширить edge-bounds учёт там, где сейчас клипается geometry:
   - self-loop
   - curved edges
   - arrow-adjacent geometry, если она влияет на page bounds
6. Добавить дополнительные diagnostics, которые покажут:
   - scene local bounds
   - final page bounds
   - graph transform
   - max visible node/edge extents внутри `viewBox`
7. Добавить в compare harness новый слой `svg visibility`:
   - одинаковые ли `node` / `edge` вообще присутствуют в обоих `svg`
   - какие из них геометрически пересекают `viewBox`
   - сколько вершин и рёбер видимо у `graphviz`
   - сколько вершин и рёбер видимо у managed
   - какие visible-in-graphviz элементы отсутствуют или уехали за кадр в managed
   - для node:
     - visibility по `ellipse` vs `viewBox`
   - для edge:
     - visibility по `path` bounds vs `viewBox`
     - для начала допустим diagnostics-level bbox/sampling approach, если точная bezier math избыточна
8. Перепрогнать:
   - `Compare-Export-SmallGraphs.ps1`
   - `Compare-WikiVote-Export.ps1`
   и только после этого возвращаться к новым parity-выводам по `png/jpg`

Ожидаемый результат:

1. Managed `svg` перестаёт быть визуально "почти пустым" на `WikiVote`.
2. На `self-loop` петля снова полностью видна.
3. На `triangle-cycle` и `bidirectional-edge` не остаётся клипа и схлопывания geometry.
4. `svg` и raster начинают расходиться уже в более локальных деталях, а не в базовом placement.

Телеметрия, по которой принимаем patch:

- visual preview:
  - `WikiVote` managed preview показывает весь граф внутри кадра, а не несколько точек у нижней границы
  - `self-loop` managed preview показывает саму петлю
  - `triangle-cycle` managed preview показывает все три вершины и три ребра
- summary metrics:
  - `WidthMatch` и `HeightMatch` должны заметно улучшиться хотя бы на small-cases
  - `ViewBoxMatch` должен либо совпасть, либо стать объяснимо близким
  - `self-loop` больше не должен иметь деградацию уровня `27pt x 22pt` против `10pt x 10pt`
  - `triangle-cycle` больше не должен иметь деградацию уровня `54pt x 48pt` против `27pt x 29pt`
  - `WikiVote` больше не должен оставаться на визуально пустом managed preview
- `svg visibility` metrics:
  - для small-cases и `WikiVote` managed должен показывать те же видимые вершины, что и `graphviz`, либо отличаться только в явно объяснимых fine details
  - `VisibleNodeCount` у managed не должен резко проседать относительно `graphviz`
  - `VisibleEdgeCount` у managed не должен резко проседать относительно `graphviz`
  - должны появиться списки:
    - `VisibleNodeIdsMissingInManaged`
    - `VisibleEdgeTitlesMissingInManaged`
  - на целевых regression-case-ах эти списки должны стать пустыми или радикально уменьшиться
- code-level check:
  - новые `svg` outputs для одних и тех же case-ов должны измениться относительно baseline `Patch 3b/5c`
  - иначе patch не считается состоявшимся

Критерий готовности:

- visual SVG mismatch перестаёт быть блокирующей проблемой
- после `Patch 3c` можно снова доверять сравнению `Patch 4/5/6` как сравнению export layers, а не сравнению уже сломанной geometry

Результат:

1. Scene builder переведён на graph-local `Y` sign conventions, совместимые с `graphviz svg`:
   - local `y` и edge path `y` больше не пишутся с ошибочной двойной инверсией
2. Viewport теперь считается по content-bounds, в которые входят и routed edges, а не только node/label bounds.
3. В pipeline добавлен edge-aware translate flow:
   - bounds для viewport считаются до final scene build
   - routed edges затем переводятся в ту же output/page coordinate system
4. В compare harness добавлен новый `svg visibility` слой:
   - `VisibleNodeCount`
   - `VisibleEdgeCount`
   - `VisibleNodeCoverageRatio`
   - `VisibleEdgeCoverageRatio`
   - `VisibleNodeIdsMissingInManaged`
   - `VisibleEdgeTitlesMissingInManaged`
5. Self-loop routing усилен, чтобы петля была реально заметна в managed preview, а не почти исчезала около вершины.

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
- result: `triangle-cycle` managed preview now shows all `3` vertices and all `3` edges inside frame
- result: `bidirectional-edge` managed preview no longer collapses to an almost flat strip
- result: `self-loop` managed preview now shows the loop as visible geometry
- result: `self-loop` width improved from previous `10pt` baseline to `16pt`, though still below graphviz `27pt`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3c`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: `VisibleNodeCoverageRatio = 1.0`
- result: `VisibleEdgeCoverageRatio = 1.0`
- result: empty `VisibleNodeIdsMissingInManaged`
- result: empty `VisibleEdgeTitlesMissingInManaged`
- result: managed `WikiVote` preview перестал быть почти пустым; весь граф теперь видим внутри кадра
- result: raster telemetry после исправления geometry тоже улучшилась:
  - `PngDarkPixelDelta`: from `-3008` to `-1723`
  - `JpgDarkPixelDelta`: from `-2489` to `-966`

Остаток после Patch 3c:

- visual blocker закрыт, но full natural-size parity ещё не закрыта
- size mismatch всё ещё заметен:
  - `WikiVote svg`: `146pt x 123pt` vs `123pt x 108pt`
  - `triangle-cycle svg`: `27pt x 29pt` vs `54pt x 48pt`
  - `self-loop svg`: `16pt x 12pt` vs `27pt x 22pt`
- это уже другой слой проблемы:
  - curve envelope / route geometry
  - natural content bounds
  - graphviz-like edge extent semantics

Следующий осмысленный follow-up:

- `Patch 3c` уже достаточен, чтобы прекратить ложные выводы вида "managed svg пустой / контент уехал за viewBox"
- следующий отдельный слой работы фиксируем как `Patch 3d`

### Patch 3d. Сблизить natural size и curve-envelope с graphviz

Статус:

- сделано частично

Цель:

- уменьшить оставшийся `svg` size mismatch после того, как visibility blocker уже закрыт
- сблизить managed bounds с graphviz по:
  - self-loop envelope
  - bidirectional-edge curvature envelope
  - natural page size на small-cases и `WikiVote`

Фокус:

- `src/PSGraphView.Sfdp/SfdpEdgeRouter.cs`
- `src/PSGraphView.Sfdp/SfdpViewportCalculator.cs`
- compare scripts / visibility + size telemetry
- compare harness parity по arrows

Телеметрия, которая толкает к этому patch-у:

- `WikiVote svg`: managed `146pt x 123pt` vs graphviz `123pt x 108pt`
- `triangle-cycle svg`: managed `27pt x 29pt` vs graphviz `54pt x 48pt`
- `self-loop svg`: managed `16pt x 12pt` vs graphviz `27pt x 22pt`

Результат:

1. Compare harness теперь сравнивает одинаковую arrow semantics:
   - managed export запускается с `ShowArrows = true`
   - `ArrowSize = 0.08`, как и у `graphviz`
2. `self-loop` переведён на двухсегментный cubic path, ближе к `graphviz`.
3. Routed edge path/bounds больше не зашиты на ровно `4` точки:
   - scene теперь умеет one-or-more cubic segments
   - bounds считаются по всем сегментам
4. Для bidirectional edges увеличена curvature heuristic, чтобы уменьшить vertical-envelope mismatch.
5. `PathDelta` в compare harness снова стал честной метрикой:
   - marker path из `<defs>` больше не считается как edge path

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
- result: `self-loop svg` improved from `16pt x 12pt` to `28pt x 22pt` versus graphviz `27pt x 22pt`
- result: `self-loop png` improved from `WidthDelta = -16, HeightDelta = -14` to `WidthDelta = 0, HeightDelta = 0`
- result: `self-loop jpg` improved from `WidthDelta = -16, HeightDelta = -14` to `WidthDelta = 0, HeightDelta = 0`
- result: `bidirectional-edge svg` improved from `48pt x 11pt` to `48pt x 16pt` versus graphviz `43pt x 22pt`
- result: `bidirectional-edge png` improved `HeightDelta` from `-14` to `-8`
- result: `bidirectional-edge jpg` improved `HeightDelta` from `-14` to `-8`
- result: `PathDelta = 0` again on all small cases
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3d`
- result: `WikiVote svg` still `146pt x 123pt` vs `123pt x 108pt`
- result: `WikiVote png` still `WidthDelta = 30`, `HeightDelta = 21`
- result: `WikiVote jpg` still `WidthDelta = 30`, `HeightDelta = 21`

Остаток после Patch 3d:

- export-side route envelope для self-loop заметно сблизили
- reverse-edge curvature тоже стала ближе
- но `triangle-cycle` и `WikiVote` почти не сдвинулись по size parity
- это уже сильный сигнал, что оставшийся mismatch в этих case-ах в основном layout-limited, а не чисто export-limited

Критерий готовности:

- width/height deltas сокращаются заметно, но уже без потери `VisibleNodeCoverageRatio = 1.0` и `VisibleEdgeCoverageRatio = 1.0`

Следующий шаг:

- добавить отдельный `Patch 3e`, чтобы развести:
  - export-side mismatch
  - layout-side geometry mismatch

### Patch 3e. Зафиксировать layout-limited residuals отдельно от export parity

Статус:

- сделано

Цель:

- перестать лечить остаточный layout mismatch как будто это чисто export bug
- получить честный следующий baseline для export parity после `Patch 3d`

Фокус:

- compare tooling / diagnostics
- возможно fixed-geometry или scene-dump compare mode
- явное сравнение:
  - graphviz layout bounds
  - managed layout bounds
  - export-only viewport / raster deltas поверх одной и той же geometry

Реализация:

- compare harness теперь отдельно парсит layout geometry:
  - `graphviz` verbose:
    - `pre overlap geometry stage=...`
    - `overlap geometry stage=...`
  - managed diagnostics:
    - `postprocess.geometry`
    - `overlap.geometry`
    - `layout.geometry`
- в summary добавлен новый блок:
  - `Comparisons.Diagnostics.LayoutResiduals`
- этот блок раскладывает итоговый output delta на:
  - `LayoutWidthDelta` / `LayoutHeightDelta`
  - `ExportWidthDelta` / `ExportHeightDelta`
- и ставит classification:
  - `layout_limited`
  - `export_limited`
  - `mixed`

Телеметрия:

- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch3e`
- result: `6/6` cases completed successfully
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch3e`
- result: `30 vertices / 99 edges`
- result: `triangle-cycle` classified as `layout_limited`
- result: `triangle-cycle OutputWidthDelta = -27`, `LayoutWidthDelta = -28.71`, `ExportWidthDelta = 1.71`
- result: `triangle-cycle OutputHeightDelta = -19`, `LayoutHeightDelta = -20.45`, `ExportHeightDelta = 1.45`
- result: `WikiVote` classified as `layout_limited`
- result: `WikiVote OutputWidthDelta = 23`, `LayoutWidthDelta = 23.28`, `ExportWidthDelta = -0.28`
- result: `WikiVote OutputHeightDelta = 15`, `LayoutHeightDelta = 15.66`, `ExportHeightDelta = -0.66`
- result: `bidirectional-edge` classified as `mixed`
- result: `self-loop` classified as `mixed`

Критерий готовности:

- `triangle-cycle` и `WikiVote` residuals классифицированы:
  - что идёт из layout
  - что остаётся реально export-side

Вывод:

- основной remaining mismatch для `triangle-cycle` и `WikiVote` теперь доказуемо layout-limited
- значит следующие export patch-и не должны пытаться "лечить" эти два case-а как будто проблема сидит в `svg/png/jpg` writer-е
- для export parity дальше разумно фокусироваться на:
  - export-limited и mixed case-ах
  - text / label parity
  - при необходимости fixed-geometry compare mode

### Patch 3a. Отдельно выбрать и зафиксировать raster backend

Статус:

- сделано

Цель:

- принять техническое решение по raster backend до реальной реализации `png/jpg`
- не допустить, чтобы удобство `SkiaSharp` незаметно подменило цель parity с `graphviz`

Почему это отдельный patch:

- выбор backend-а здесь влияет на:
  - текст
  - stroke/fill behavior
  - alpha / compositing
  - anti-aliasing
  - jpeg flattening
  - переносимость тестов между платформами
- если этот выбор не зафиксировать заранее, потом будет трудно понять:
  - mismatch идёт из нашей логики
  - или из особенностей backend-а

Фокус:

- исследовательский документированный выбор между:
  - `SkiaSharp` как основным raster backend
  - альтернативой через другой managed encoder / surface path
- при необходимости:
  - маленький prototype в `tests` или `scratch`-ветке
  - без выхода на public surface

План:

1. Явно зафиксировать backend candidates.
2. Для каждого кандидата проверить:
   - можно ли рисовать на ARGB surface с контролем alpha
   - можно ли стабильно кодировать `png`
   - можно ли стабильно кодировать `jpg`
   - можно ли явно контролировать background flattening
   - можно ли получить достаточно предсказуемый pixel output для compare
3. Для `SkiaSharp` отдельно проверить:
   - anti-aliasing defaults
   - text rendering variability
   - зависимость от native assets
   - поведение на macOS/Linux/Windows
4. Принять backend не по удобству API, а по критериям ниже.

Результат:

1. Выбранный raster backend:
   - `SkiaSharp`
2. Зафиксированный package surface в `PSGraphView.GVExport`:
   - `SkiaSharp`
   - `SkiaSharp.NativeAssets.Linux.NoDependencies`
   - `SkiaSharp.NativeAssets.macOS`
   - `SkiaSharp.NativeAssets.Win32`
3. `Svg.Skia` осознанно не выбран для production export path:
   - `Patch 4+` должен рисовать scene напрямую
   - rasterize собственного `svg` в production path не допускается
4. В коде выбор backend-а зафиксирован отдельно, до реализации `png/jpg`.
5. Добавлен отдельный probe-test проект `tests/PSGraphView.GVExport.Tests`, который подтверждает:
   - создание Skia surface
   - PNG encode
   - JPEG encode после явного flatten transparency на заданный background

Почему выбран `SkiaSharp`:

- он уже используется в соседнем `libsixel`, так что packaging и native-assets path для репозитория не новые
- он даёт прямой low-level drawing surface и encode path для `png/jpg`
- нам не нужно подменять им export semantics:
  - viewport
  - scene order
  - background policy
  - jpeg flattening
  всё это остаётся на стороне `PSGraphView`
- по локальному probe он работает в текущей macOS-среде без дополнительного native-bootstrap вне NuGet package set

Принятые ограничения:

- `SkiaSharp` не рассматривается как analog `graphviz device/plugin system`
- text parity остаётся отдельным риском и не считается закрытой этим patch-ем
- точный `jpg` background color policy ещё будет формализован в `Patch 6`

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/PSGraphView.GVExport.csproj` now pins `SkiaSharp` + native assets packages and copies native libraries after build
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterBackendSelection.cs` now fixes backend choice as `SkiaSharp`
- code change: added `/Users/andrei/repo/PSGraphView/tests/PSGraphView.GVExport.Tests/`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `2/2` passed
- result: probe confirmed:
  - `SKSurface.Create(...)` works
  - PNG encode works
  - explicit transparency flatten + JPEG encode works
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `9/9` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `10/10` passed

Критерии принятия `SkiaSharp`:

- он используется только как низкоуровневый drawing/encoding backend
- viewport, scene, background semantics и order задаём мы сами
- output можно сделать достаточно стабильным для compare harness
- pixel diff с `graphviz` не уходит в системный шум из-за backend-specific особенностей
- packaging и native assets реалистичны для этого репозитория

Красные флаги для `SkiaSharp`:

- если text rendering даёт большой нерегулируемый разброс
- если `jpg/png` требуют слишком много backend-specific обходов
- если придётся rasterize-ить собственный `svg`, а не рисовать scene напрямую
- если итоговая реализация станет semantically closer к `SkiaSharp`, чем к `graphviz`

Критерий готовности:

- в плане и в коде явно зафиксировано:
  - какой backend выбран
  - почему он выбран
  - какие ограничения мы принимаем

### Patch 4. Ввести managed raster surface поверх общей scene

Статус:

- сделано

Цель:

- построить единый внутренний raster path для `png/jpg`

Фокус:

- новый raster renderer в `src/PSGraphView.GVExport/`
- `ExportGraphViewCmdlet`
- `ViewOutputKind`
- `CmdletOutputHelpers`

План:

1. Расширить `ViewOutputKind`:
   - `Png`
   - `Jpg`
2. Расширить path inference:
   - `.png`
   - `.jpg`
   - `.jpeg`
3. Добавить internal ARGB surface renderer.
4. Подключить backend, который выбран и зафиксирован на `Patch 3a`.

Результат:

1. В `PSGraphView.GVExport` добавлен прямой scene-to-raster path на `SkiaSharp`:
   - без rasterize собственного `svg`
   - с отдельными выходами для `png` и `jpg`
2. В `Sfdp` добавлен отдельный `SfdpRasterExporter`, который использует тот же render-scene, что и `svg`.
3. `Export-GraphView` теперь умеет:
   - `-As Png`
   - `-As Jpg`
   - inference по `.png/.jpg/.jpeg`
4. `CmdletOutputHelpers` теперь умеет писать как text, так и binary output.
5. В diagnostics добавлено событие `render.raster`.
6. Compare harness теперь не пишет placeholder для `png/jpg`, а даёт базовую summary по width/height/bytes.
7. Отдельно закрыт packaging-аспект:
   - published `PSGraphView.PowerShell` теперь копирует `libSkiaSharp*` в корень модуля
   - из-за этого `png/jpg` работают не только в unit tests, но и в `-UseLocalModules` publish path

Телеметрия:

- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs`
- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRasterExporter.cs`
- code change: added `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderScenePipeline.cs`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ViewOutputKind.cs` now includes `Png` and `Jpg`
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/CmdletOutputHelpers.cs` now infers `.png/.jpg/.jpeg` and writes binary results
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj` now copies `libSkiaSharp*` to module root on build/publish
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1` now reports raster compare summary
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `4/4` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter "SfdpSvgExporterTests|SfdpRasterExporterTests"`
- result: `11/11` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -Command '. ./demos/Import-DemoModules.ps1; Import-PSGraphViewDemoModules -UseLocalModules; ... Export-GraphView -As Png ...'`
- result: published module returned PNG bytes successfully
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch4b`
- result: `6/6` cases completed successfully with managed `svg/png/jpg`
- result: on all `6` small-graph cases `RasterBackend = SkiaSharp`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch4b`
- result: `Selection = ExpandedTopDegree`
- result: `30 vertices / 99 edges`
- result: managed `svg/png/jpg` all produced successfully
- result: `WikiVote` raster deltas are currently `Png/Jpg WidthDelta = 31`, `HeightDelta = 21`

Предпочтительный вариант:

- один кроссплатформенный managed encoder для `png/jpg`

С учётом соседнего `libsixel`, речь здесь именно про уже используемый там `SkiaSharp`-stack:

- надо отдельно посмотреть на:
  - `../libsixel/src/LibSixel.PowerShell/Internal/ImageDecoder.cs`
  - `../libsixel/src/LibSixel/Internal/PixelConverter.cs`
- и на package surface:
  - `SkiaSharp`
  - `SkiaSharp.NativeAssets.*`
  - `Svg.Skia`
- там уже есть полезные куски для:
  - decode `png/jpg/svg`
  - перевод в `RGBA8888`
  - resize / pixel-buffer handling
- но использовать их нужно очень аккуратно:
  - это может быть helper для compare tooling
  - это может быть helper для внутренних pixel-buffer conversion
  - это может быть кандидат для managed raster backend, если он не ломает parity-цели
  - это не должно становиться источником эталонной export-семантики
- семантику `png/jpg` всё равно надо снимать с `graphviz`:
  - dpi
  - background flattening
  - alpha behavior
  - viewport scaling
  - text/render ordering

Чего делать не надо:

- не принимать текущее поведение `libsixel` за reference для `jpg/png`
- не принимать текущее поведение `SkiaSharp` за reference для `jpg/png`
- не тащить в export plan лишнюю sixel-specific логику
- не подменять прямой managed render растеризацией собственного `svg`, если цель — parity с `graphviz` raster path

Замечание:

- в репе сейчас нет готовой raster dependency
- это, вероятно, потребует одного нового пакета
- если идём на это, dependency должна быть узкой:
  - только surface/encoding
  - без переноса layout/render-логики наружу
- если это будет `SkiaSharp`, решение должно уже быть обосновано и зафиксировано на `Patch 3a`

Критерий готовности:

- managed умеет отдать raster bytes из той же scene, что и `svg`

### Patch 5. Добить `png` до graphviz-like semantics

Статус:

- сделано

Цель:

- приблизить managed `png` к `png:cairo`

Фокус:

- raster renderer
- `Export-GraphView` PowerShell surface
- tests + compare script

План:

1. Зафиксировать default `96 dpi`.
2. Сблизить background/alpha behavior с `png:cairo`.
3. Добавить diagnostics:
   - `raster.surface`
   - `raster.encode`
4. Добавить raster compare в script.

Результат:

1. Raster sizing теперь считает `png` из raw page size, а не из уже округлённого `svg width/height`.
2. `png` clear policy приведена ближе к `png:cairo`:
   - если graph background включён, surface сразу очищается в background color
   - из-за этого ушёл artificial transparent halo по краям
3. Compare harness теперь умеет для `png`:
   - width/height deltas
   - byte delta
   - `DarkPixelDelta`
   - `NonWhitePixelDelta`
   - `TransparentPixelDelta`
4. Через telemetry стало видно, что текущий крупный `png` mismatch уже сидит не в transparent background policy, а в:
   - общей geometry/viewBox разнице
   - density / coverage разнице

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRenderScene.cs` now carries raw raster page size in viewport
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs` now:
  - uses raw raster page size for pixel dimensions
  - clears PNG surface to background color when background is enabled
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpViewportCalculator.cs` now keeps raw raster page width/height
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1` now computes raster pixel metrics via `SkiaSharp`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `4/4` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch5c`
- result: `6/6` cases completed successfully
- result: on all `6` small-graph cases `PngTransparentPixelDelta = 0`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch5c`
- result: `30 vertices / 99 edges`
- result: `PngWidthDelta = 30`, `PngHeightDelta = 21`
- result: `PngTransparentPixelDelta = 0`
- result: `PngDarkPixelDelta = -3008`
- result: `PngNonWhitePixelDelta = -3008`

Критерий готовности:

- `png` сравнивается по размерам и pixel metrics
- крупный mismatch локализован

### Patch 6. Добить `jpg` до graphviz-like semantics

Статус:

- сделано

Цель:

- приблизить managed `jpg` к `jpg:cairo` + gd device behavior

Фокус:

- jpeg encode path
- compare script

План:

1. Повторить graphviz-like background flattening.
2. Явно зафиксировать transparent fallback color.
3. Добавить сравнение по pixel metrics уже без alpha.
4. Проверить размер / quality behavior как вспомогательную метрику.

Результат:

1. `jpg` path больше не рисуется сразу на fallback background.
   Теперь `SkiaSharp` сначала получает прозрачный ARGB scene, а затем отдельный opaque-conversion step повторяет `graphviz/gd`-подобную policy:
   - fallback color `#fffffe`
   - threshold по 8-bit alpha `64`
   - пиксели ниже порога заменяются fallback color
   - пиксели на пороге и выше сохраняют исходный RGB без ручного смешивания с белым фоном
2. Managed diagnostics для raster теперь пишут:
   - `encodeQuality`
   - `opaqueOutputPolicy`
   - `opaqueFallbackColor`
   - `opaqueAlphaThreshold`
3. Compare harness теперь считает ещё одну `jpg`-метрику:
   - `NearFallbackPixelDelta`
4. `Patch 6` закрыл именно `jpeg flattening semantics`, но не снял общий большой raster mismatch:
   - на `WikiVote 30 / 99` размеры по-прежнему `194x164` vs `164x143`
   - значит главный remaining blocker уже не в `jpg` policy, а в тех же geometry / density / text расхождениях, которые сидят выше по pipeline

Телеметрия:

- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/GraphRasterRenderSceneWriter.cs` now:
  - renders `jpg` from transparent scene first
  - applies graphviz-like gd threshold flatten step before JPEG encode
- code change: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/SfdpRenderDiagnostics.cs` now logs JPEG policy fields
- code change: `/Users/andrei/repo/PSGraphView/demos/Compare-Export.Common.ps1` now reports `NearFallbackPixelDelta`
- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `6/6` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-patch6`
- result: all `6/6` cases completed successfully and now expose `NearFallbackPixelCount`
- result: on `self-loop`, `JpgDarkPixelDelta` improved from `-4` to `0`
- result: on `self-loop`, `JpgNonWhitePixelDelta` improved from `-4` to `0`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 30 -SubgraphStartVertexCount 3 -OutputDir /tmp/psgraphview-export-wikivote-patch6`
- result: `30 vertices / 99 edges`
- result: `JpgWidthDelta = 30`
- result: `JpgHeightDelta = 21`
- result: `JpgDarkPixelDelta = -975`
- result: `JpgNonWhitePixelDelta = -975`
- result: `JpgNearFallbackPixelDelta = 9125`

Критерий готовности:

- `jpg` использует graphviz-like opaque conversion policy
- compare harness умеет отдельно мерить `jpg` fallback behavior
- зафиксировано, что remaining большой mismatch уже сидит выше, а не в самом `jpeg flattening`

### Patch 7. Добивка text / font / label parity

Статус:

- сделано частично

Цель:

- убрать самый вероятный remaining mismatch после viewport/scene/raster

Почему это отдельный patch:

- text metrics и font mapping — один из самых рискованных разъездов
- current managed label sizing приблизительное
- это может давать сильный pixel diff даже при почти правильной геометрии узлов и рёбер

Фокус:

- `src/PSGraphView.Sfdp/SfdpLabelLayouter.cs`
- managed raster text path
- `svg` text attributes

Patch 7a:

- сделать baseline и telemetry до реального text-fix:
  - labelled compare harness
  - text/font summary в `svg` compare
  - managed label diagnostics

Сделано в Patch 7a:

- добавлен labelled compare script:
  - `demos/Compare-Export-LabeledGraphs.ps1`
- compare harness теперь умеет отдельный `Comparisons.Text` блок:
  - `NodeLabelCountDelta`
  - `GraphvizFontFamilies` / `ManagedFontFamilies`
  - `GraphvizTextAnchors` / `ManagedTextAnchors`
  - `AverageFontSizeDelta`
  - `AverageOffsetXDelta`
  - `AverageBaselineOffsetYDelta`
- managed diagnostics теперь пишут `render.labels`

Телеметрия Patch 7a:

- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpSvgExporterTests`
- result: `12/12` passed
- test: `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphViewCmdletTests`
- result: `13/13` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7a`
- result: `3/3` labeled cases completed successfully
- result: text content already совпадает:
  - `NodeLabelCountDelta = 0`
  - `MissingNodeLabelsInManaged = []`
- result: главные remaining text mismatches теперь измерены явно:
  - `GraphvizFontFamilies = ["Times,serif"]`
  - `ManagedFontFamilies = ["sans-serif"]`
  - `GraphvizTextAnchors = ["middle"]`
  - `ManagedTextAnchors = []`
  - `AverageFontSizeDelta = -6.0` на всех labeled cases
- result: `triangle-cycle-labeled`:
  - `AverageOffsetXDelta = 8.93`
  - `AverageBaselineOffsetYDelta = -3.65`
- result: `single-edge-labeled`:
  - `AverageOffsetXDelta = -9.42`
  - `AverageBaselineOffsetYDelta = 3.57`

Следующий шаг внутри Patch 7:

- `Patch 7b`: реальная правка label placement / font defaults / `svg` text attributes под graphviz-like baseline

Сделано в Patch 7b:

- `Sfdp` label semantics переведены на graphviz-like centered mode:
  - label рисуется по центру node
  - `text-anchor="middle"`
  - default `font-family="Times,serif"`
- default `LabelFontSize` для `Sfdp` поднят до `14.0`
- default `LabelOffsetX/Y` для `Sfdp` выровнен к `0.0 / 0.0`
- PowerShell-path для `Sfdp` теперь применяет эти defaults только когда пользователь не передал свои значения явно
- label bounds больше не расширяют viewport / page size
- raster text path теперь уважает `text-anchor` и пытается брать `Times New Roman` как practical match для `Times,serif`

Телеметрия Patch 7b:

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
- result: `triangle-cycle-labeled`:
  - `AverageBaselineOffsetYDelta = 8.881784197001252E-16`
- result: `star-labeled`:
  - `AverageBaselineOffsetYDelta = -8.881784197001252E-16`

Следующий шаг внутри Patch 7:

- оценить remaining raster text parity уже не по font defaults, а по фактическому `SkiaSharp` glyph shaping и anti-aliasing

Сделано в Patch 7c:

- `SkiaSharp` raster text path теперь использует явные text flags:
  - `HintingLevel = Slight`
  - `SubpixelText = true`
  - `LcdRenderText = true` для opaque background path
  - `IsAutohinted = true`
- managed raster diagnostics теперь пишут реальный text rendering mode:
  - `textHintingLevel`
  - `subpixelText`
  - `lcdRenderText`
  - `autohintedText`
- compare harness теперь считает не только raw pixel delta, но и нормализованные raster метрики:
  - `OpaqueCoverageDelta`
  - `DarkPixelDensityDelta`
  - `NonWhitePixelDensityDelta`
  - `NearFallbackPixelDensityDelta`
- labelled compare overview теперь сохраняет отдельные `PngComparison` и `JpgComparison`

Телеметрия Patch 7c:

- test: `dotnet test tests/PSGraphView.GVExport.Tests/PSGraphView.GVExport.Tests.csproj`
- result: `6/6` passed
- test: `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --filter SfdpRasterExporterTests`
- result: `2/2` passed
- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7c`
- result: `3/3` labeled cases completed successfully
- result: managed raster diagnostics now report:
  - `textHintingLevel = "Slight"`
  - `subpixelText = true`
  - `lcdRenderText = true`
- result: on `single-edge-labeled`:
  - `PngDarkPixelDelta` improved from `24` to `19`
  - `JpgDarkPixelDelta` improved from `34` to `29`
  - `PngDarkPixelDensityDelta = -0.091`
  - `JpgDarkPixelDensityDelta = -0.021`
- result: on `star-labeled`:
  - `PngDarkPixelDelta` improved from `618` to `612`
  - `JpgDarkPixelDelta` improved from `788` to `777`
  - `PngDarkPixelDensityDelta = 0.062`
  - `JpgDarkPixelDensityDelta = 0.083`
- result: `triangle-cycle-labeled` remains dominated by layout-size mismatch:
  - `PngDarkPixelDelta = -350`
  - `JpgDarkPixelDelta = -289`
  - `PngDarkPixelDensityDelta = 0.284`

Следующий шаг внутри Patch 7:

- для remaining raster text parity нужен уже не общий default-tuning, а отдельное решение по font availability / glyph shaping / same-geometry compare

Сделано в Patch 7d:

- compare harness теперь собирает font resolution telemetry из `graphviz -v`:
  - `fontname: ... resolved to ...`
- managed raster diagnostics теперь пишут фактически выбранные `SkiaSharp` label font families:
  - `resolvedLabelFontFamilies`
- labelled compare overview теперь сохраняет `FontResolution` summary рядом с `TextComparison`

Телеметрия Patch 7d:

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
- result: `single-edge-labeled` detailed summary now shows:
  - `Requested = "Times-Roman"`
  - `ResolvedFamily = "Times New Roman"`
  - managed `resolvedLabelFontFamilies = "Times New Roman"`

Следующий шаг внутри Patch 7:

- на текущей машине remaining raster text mismatch уже нельзя объяснить простым font fallback
- следующий meaningful шаг:
  - same-geometry compare
  - или label-only raster experiment для отделения glyph shaping от общей scene density

Сделано в Patch 7e:

- compare harness теперь считает отдельные `label ROI` raster metrics для `png/jpg`
- ROI строится из `svg` text geometry и потом маппится в локальные raster rectangles
- labelled compare overview теперь сохраняет:
  - `PngLabelRaster`
  - `JpgLabelRaster`

Телеметрия Patch 7e:

- run: `pwsh -NoProfile -File demos/Compare-Export-LabeledGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-labeled-compare-patch7e`
- result: `3/3` labeled cases completed successfully
- result: label ROI metrics теперь отделяют local text region от whole-image size noise
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

Следующий шаг внутри Patch 7:

- label ROI telemetry показала, что local raster text mismatch действительно существует даже после вычитания общего scene size
- следующий meaningful шаг:
  - same-geometry compare
  - либо отдельный scene/label-only renderer experiment, где geometry фиксирована и сравнивается только glyph output

Критерий готовности:

- label anchors, label size и визуальная плотность становятся заметно ближе к graphviz

### Patch 8. Cleanup, docs, help и финальный summary

Статус:

- не сделано

Цель:

- после стабилизации surface довести пользовательскую часть

Фокус:

- `README.md`
- `docs/powershell/Export-GraphView.md`
- generated help
- demos

---

## Первые необходимые patch-и

Если идти строго по приоритету, первые patch-и я считаю такими:

1. `Patch 0`
   - новый compare harness для `svg/png/jpg`
   - pinned graphviz backends
   - baseline summary

2. `Patch 0a`
   - export diagnostics в managed и local graphviz
   - локализация mismatch по viewport / structure / raster

3. `Patch 1`
   - общий render scene
   - развязка export от layout

4. `Patch 2`
   - graphviz-like viewport semantics
   - это база для всех форматов

5. `Patch 3`
   - graphviz-like SVG structure
   - главный vector parity patch

6. `Patch 3c`
   - corrective patch на visual SVG geometry / transform / bounds parity
   - обязателен перед новыми выводами по raster parity

7. `Patch 3a`
   - зафиксированный выбор raster backend
   - отдельная проверка `SkiaSharp` против parity-целей

8. `Patch 4`
   - общий raster surface + новый output surface в PowerShell

После обнаружения visual SVG regression двигаться дальше в `png/jpg` без `Patch 3c` уже нельзя: выводы будут шумными и частично ложными.

---

## Ожидаемые риски

Главные риски:

- text metrics и font parity
- выбор raster encoder / drawing dependency
- различия между backend-ами graphviz на разных сборках
- смешивание layout mismatch и export mismatch
- backend-driven drift:
  - когда решение начинает повторять `SkiaSharp`, а не `graphviz`
- native-assets риск:
  - когда зависимость технически работает, но неудобна для поставки и тестов

Как снижаем риск:

- pinned backend names в compare-script
- scene/viewport diagnostics
- small-graph harness для локализации
- отдельные patch-и на text parity
- `SkiaSharp` из соседнего `libsixel` рассматривать только как возможный технический backend, а не как источник правильной export-семантики
- вынести выбор backend-а в отдельный `Patch 3a`, а не прятать его внутрь первой реализации `png/jpg`

---

## Следующий практический шаг после текущего состояния

- начать с `Patch 0`
- сразу же параллельно подготовить `Patch 0a`

Причина:

- до появления compare harness и export diagnostics любые правки в `svg/png/jpg` будут плохо измеримы
- здесь нужен тот же цикл, что и в `plan.md`:
  - маленький patch
  - новый run
  - новые логи
  - локализация следующего mismatch
