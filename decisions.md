# Architecture Decision Log

## 2026-04-06 19:50:50 PDT

Решение:
Вынести сравнение `dot -Tsvg` и native `Export-GraphvizView -As Svg` в отдельный повторяемый demo-скрипт:
- [Compare-WikiVote-GraphvizSvg.ps1](/Users/andrei/repo/PSGraphView/demos/Compare-WikiVote-GraphvizSvg.ps1)
- скрипт должен уметь как сам построить `wiki-vote` subgraph DOT, так и принять уже готовый `-DotPath`
- на выходе он должен сохранять оба `Svg` и JSON с warm/cold timing-ами

Причины:
- После перехода `Svg` на native path нужен быстрый способ повторять и визуальное сравнение, и сравнение по скорости на одном и том же входе.
- Разовые ad-hoc команды в shell неудобно переиспользовать после каждого следующего патча.
- Для этой серии изменений важно сравнивать оба пути на общем DOT, иначе выводы по скорости и размеру выходных файлов легко искажаются.

Телеметрия / наблюдения:
- Базовый замер для `wiki-vote` subgraph с `SubgraphSeedCount = 30`:
  - warm `Export-GraphvizView -As Svg`: `35.71 ms` avg, `35.70 ms` median
  - warm `dot -Kdot -Tsvg`: `90.83 ms` avg, `90.47 ms` median
  - cold `pwsh + Import-Module + Export-GraphvizView`: `358.41 ms` avg, `356.30 ms` median
  - cold `dot -Kdot -Tsvg`: `90.13 ms` avg, `89.95 ms` median
- Вывод по текущему состоянию:
  - в уже живой PowerShell-сессии native `Svg` path быстрее внешнего `dot`
  - в разовом запуске доминирует startup overhead `pwsh` и импорта модуля, поэтому direct `dot` быстрее
- Скрипт сохраняет baseline-файлы:
  - общий `wiki-vote.dot`
  - `wiki-vote-graphviz-dot.svg`
  - `wiki-vote-psgraphview-native.svg`
  - `wiki-vote-svg-benchmark.json`

Следствие:
- После патчей, затрагивающих Graphviz JSON payload, interpreter или `scene -> Svg`, этот demo-скрипт нужно использовать как ручную проверку регрессий.
- Результаты сравнения из этого скрипта нужно при необходимости дописывать в decision log как телеметрию для следующих решений.

## 2026-04-06 19:00:55 PDT

Решение:
Исправить ранее принятое предположение про входной payload для interpreter-а:
- для текущего `libpsgv` draw-команды нужно читать не из `xdot_json`, а из Graphviz `json`;
- managed имена `XdotJson` в `PSGraphView` пока оставить как технический legacy-name, чтобы не смешивать bug fix и массовое переименование.

Причины:
- На практике `xdot_json` из текущего native path не содержит `_draw_` и `_ldraw_`, из-за чего `scene -> Svg` дает почти пустой документ.
- Тот же graph через `dot -Tjson` содержит draw-операции, которые как раз и нужны interpreter-у.
- Это и есть ошибка предыдущего решения: мы слишком рано зафиксировали именно `xdot_json` как обязательный layout payload, не проверив его на реальном large graph сценарии.

Телеметрия / наблюдения:
- На WikiVote subgraph native `Svg` выглядел как белый лист.
- Диагностика показала:
  - `Export-GraphvizView -As Json` через текущий `libpsgv` возвращал graph/object/edge metadata без `_draw_`;
  - из-за этого generated `Svg` был около `8.3K`, тогда как родной `dot -Tsvg` был около `68K`.
- После переключения `libpsgv` на Graphviz `json`:
  - native `Svg` для того же WikiVote subgraph стал около `64K`;
  - `ExportGraphvizView` tests снова проходят и уже проверяют наличие `_draw_` и реальных SVG primitives.
- Источник различия в related project:
  - в [psgv.c](/Users/andrei/.codex/worktrees/f5d8/graphviz/lib/psgv/psgv.c) `DEFAULT_LAYOUT_FORMAT` был `xdot_json` и был изменен на `json`.

Следствие:
- Для текущего этапа `scene` нужно считать интерпретацией Graphviz JSON draw payload.
- Переименование public/managed символов (`XdotJson`, `GraphvizXdotJsonSceneInterpreter`) можно вынести в отдельный cleanup, если это действительно понадобится.

## 2026-04-06 18:49:46 PDT

Решение:
Закрыть `Патч 3` через собственный managed `scene -> Svg` renderer в `PSGraphView.Graphviz` и перевести `Export-GraphvizView -As Svg` на native path.

Причины:
- После завершения interpreter-а `Svg` уже можно строить внутри `PSGraphView`, не дергая внешний `dot`.
- Для текущего набора примитивов достаточно обычного `XDocument`; новый пакет для SVG здесь не нужен.
- Это дает первый полноценный пользовательский результат без зависимости от системного Graphviz для `Svg`, при этом `Png/Jpg` можно пока оставить на process fallback.

Телеметрия / наблюдения:
- Добавлен renderer:
  - [GraphSceneSvgRenderer.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/GraphSceneSvgRenderer.cs)
- Добавлены tests:
  - [GraphSceneSvgRendererTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphSceneSvgRendererTests.cs)
- Переключен cmdlet:
  - [ExportGraphvizViewCmdlet.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphvizViewCmdlet.cs)
- Обновлены PowerShell tests на native `Svg` path:
  - [ExportGraphvizViewCmdletTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/ExportGraphvizViewCmdletTests.cs)
- Проверки:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `14 passed`, `0 failed`, `0 skipped`
  - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `4 passed`, `0 failed`, `0 skipped`

Следствие:
- `Svg` больше не должен идти через `GraphvizProcessRenderer`.
- Следующий отдельный этап уже про `scene -> Png/Jpg`, а не про повторное чтение `xdot_json`.

## 2026-04-06 18:44:03 PDT

Решение:
Закрыть текущий `Патч 2c` через поддержку `t` и mixed label draw-операций, но не добавлять `I`, пока его нет в фактическом `xdot_json` output.

Причины:
- `libguide.pdf` перечисляет и `t`, и `I`, но реальный `xdot_json` plugin сейчас расходится с полным xdot-набором.
- `t` уже имеет явное представление в JSON schema через `fontchar`, и это влияет на рендер текста.
- Для record/HTML labels и `decorate=true` критично не только уметь читать `T`, но и не ломаться на нетекстовых операциях внутри label draw-атрибутов.
- Добавлять `ImageCommand` сейчас преждевременно: это усложнит scene model без подтвержденного входного payload-а.

Телеметрия / наблюдения:
- По [Graphviz Library Manual, section 1.1.2 xdot](https://graphviz.org/pdf/libguide.pdf):
  - `t` задает font characteristics;
  - `I` существует в полном xdot-формате;
  - label attrs при `record`, HTML-like label и `decorate=true` могут содержать не только `T`.
- По `graphviz` JSON schema:
  - в [graphviz_json_schema.json](/Users/andrei/.codex/worktrees/f5d8/graphviz/doc/infosrc/graphviz_json_schema.json) есть `font_style` с `op: "t"` и `fontchar`.
- По текущей реализации JSON plugin:
  - в [gvrender_core_json.c](/Users/andrei/.codex/worktrees/f5d8/graphviz/plugin/core/gvrender_core_json.c#L294) `xd_image` сейчас не сериализуется, там стоит `break;`
  - `xd_fontchar` сериализуется как `op: "t"` и `fontchar`.
- Проверки:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `12 passed`, `0 failed`, `0 skipped`
  - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `4 passed`, `0 failed`, `0 skipped`

Следствие:
- Interpreter слой для текущего `xdot_json` можно считать достаточно полным, чтобы переходить к `scene -> Svg`.
- `I` нужно возвращать в план только если upstream JSON plugin начнет реально его отдавать.

## 2026-04-06 18:40:36 PDT

Решение:
Закрыть `Патч 2b` через рекурсивную сборку scene по membership-ссылкам `subgraphs`, `nodes`, `edges`, а не по одному только сырому порядку `objects`.

Причины:
- В `xdot_json` subgraph-структура задается не вложенными объектами, а индексными ссылками.
- Если ориентироваться только на сырой порядок `objects`, scene будет случайно зависеть от текущего layout output, а не от самой структуры graph/subgraph.
- Явная проверка membership-индексов делает ошибки payload-а видимыми сразу, а не превращает их в тихую потерю объектов.

Телеметрия / наблюдения:
- На cluster-примере `dot -Tjson` видно:
  - root содержит `_subgraph_cnt: 2`;
  - `objects[0]` для `cluster_outer` ссылается на child subgraph через `subgraphs: [1]`, на nodes через `nodes: [2, 3]` и на edge через `edges: [0]`;
  - nested структура реально задается именно ссылками по индексам, а не JSON-вложенностью.
- Добавлены tests:
  - nested traversal c проверкой порядка `cluster_outer -> cluster_inner -> A -> B -> edge:0`
  - failure path на битый membership-индекс
- Проверки:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `11 passed`, `0 failed`, `0 skipped`
  - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `4 passed`, `0 failed`, `0 skipped`

Следствие:
- Interpreter слой больше не зависит от случайного порядка объектов в payload.
- Следующий отдельный срез должен быть уже не про subgraph traversal, а про расширенный xdot coverage: `t`, `I`, record/HTML labels и `decorate=true`.

## 2026-04-06 18:38:16 PDT

Решение:
Считать [Graphviz Library Manual, section 1.1.2 xdot](https://graphviz.org/pdf/libguide.pdf) основной внешней справкой по семантике xdot для interpreter-а в `PSGraphView`.

Причины:
- Документ прямо фиксирует набор xdot draw-операций и их смысл.
- Он подтверждает, что все координаты и размеры в xdot задаются в points, что важно для нашего `scene -> Svg` этапа.
- Он отдельно предупреждает, что Graphviz as a library не thread-safe; это совпало с нашим реальным падением native tests при параллельном запуске.
- Документ также уточняет, что label-атрибуты не всегда ограничиваются чистым `T`: для `record`, HTML-like label и `decorate=true` там могут появляться и другие draw-операции.

Телеметрия / наблюдения:
- По `libguide.pdf` section `1.1.2 xdot`:
  - xdot расширяет dot draw-атрибутами `draw`, `ldraw`, `hdraw`, `tdraw`, `hldraw`, `tldraw`;
  - среди операций кроме нашего MVP-набора есть `t` и `I`;
  - все coordinates and sizes use points.
- По `libguide.pdf` section `1.2`:
  - есть прямое предупреждение: `Using Graphviz as a library is not thread-safe.`
- Это подтверждает уже пойманное нами реальное падение:
  - `Assertion failed: (sym->id >= 0 && sym->id < topdictsize(obj)), function agxget, file attr.c, line 460`
  - которое возникало при параллельном прогоне native Graphviz tests.

Следствие:
- В `plan.md` нужно держать прямую ссылку на `libguide.pdf`.
- После закрытия текущего `2b` нужно отдельно решить, когда добавлять поддержку `t` и `I`.
- Payload-ы с record/HTML labels и `decorate=true` нужно считать отдельным обязательным coverage-срезом перед завершением interpreter слоя.

## 2026-04-06 18:24:49 PDT

Решение:
Считать базовый interpreter-срез отдельным завершенным `Патчем 2a`:
- добавить `GraphvizXdotJsonSceneInterpreter` прямо в `PSGraphView.Graphviz`;
- поддержать первый полезный набор `xdot_json -> scene` для top-level graph, `objects`, `edges`;
- включить draw-атрибуты `_draw_`, `_ldraw_`, `_hdraw_`, `_tdraw_`, `_hldraw_`, `_tldraw_`;
- для MVP поддержать `E/e`, `P/p`, `B/b`, `L`, `T`, `c/C`, `F`, `S`.

Причины:
- Это уже дает рабочий bridge между native `xdot_json` и введенной scene model без преждевременного перехода к `Svg`.
- Такой срез достаточно мал, чтобы проверить его отдельно и закончить отдельным commit-ом.
- При этом он уже покрывает реальные node/edge/label сценарии, которые нужны для следующего этапа.

Телеметрия / наблюдения:
- Добавлены файлы:
  - [GraphvizXdotJsonSceneInterpreter.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/GraphvizXdotJsonSceneInterpreter.cs)
  - [GraphvizXdotJsonSceneInterpreterTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizXdotJsonSceneInterpreterTests.cs)
  - [GraphvizNativeCollection.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeCollection.cs)
- Дополнительно скорректированы test helpers:
  - [GraphvizNativeSessionTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeSessionTests.cs)
  - [GraphvizNativeFactAttribute.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/GraphvizNativeFactAttribute.cs)
- Проверки:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `9 passed`, `0 failed`, `0 skipped`
  - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `4 passed`, `0 failed`, `0 skipped`

Следствие:
- Полный `Патч 2` нужно считать разделенным на `2a` и `2b`.
- Следующий шаг перед `Svg` renderer-ом: добрать рекурсивный обход вложенных `subgraphs` и tests на cluster/subgraph payload-ы.

## 2026-04-06 18:24:49 PDT

Решение:
Native Graphviz tests запускать без параллельного доступа к upstream Graphviz path:
- сериализовать native tests через отдельную xUnit collection;
- собирать временную `libpsgv.dylib` под локом и сначала во временный файл, а не сразу в финальный путь.

Причины:
- Поштучно native tests проходят, а параллельный запуск валит test host уже внутри upstream Graphviz.
- Без этого любой следующий патч будет давать ложные падения, не связанные с managed-логикой interpreter-а или renderer-а.
- Временный output path для `libpsgv` убирает риск подхватить частично созданную dylib при гонке.

Телеметрия / наблюдения:
- До ограничения параллелизма полный прогон `PSGraphView.Graphviz.Tests` падал с assert:
  - `Assertion failed: (sym->id >= 0 && sym->id < topdictsize(obj)), function agxget, file attr.c, line 460`
- После сериализации native tests и безопасной сборки временной dylib проходит:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `9 passed`, `0 failed`, `0 skipped`
- Основные файлы:
  - [GraphvizNativeCollection.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeCollection.cs)
  - [GraphvizNativeSessionTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeSessionTests.cs)
  - [GraphvizNativeFactAttribute.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/GraphvizNativeFactAttribute.cs)

Следствие:
- Native Graphviz path в тестах пока нельзя считать thread-safe внутри одного test host.
- Для следующих патчей нужно сохранять это ограничение, пока не появится явное подтверждение, что upstream path безопасен для параллельных сессий.

## 2026-04-06 17:52:05 PDT

Решение:
Считать обязательным operational rule для следующих патчей:
- каждый патч нужно проверять отдельной целевой проверкой;
- успешный патч нужно завершать отдельным commit сразу после успешной проверки.

Причины:
- Иначе легко потерять границу между уже подтвержденным изменением и следующим экспериментом.
- Для текущего плана это особенно важно, потому что путь идет маленькими техническими срезами: harness, scene model, interpreter, svg, raster.
- Такой порядок упрощает откат, review и чтение истории изменений.

Телеметрия / наблюдения:
- Для `Патча 0` уже есть отдельная проверка native harness.
- Для `Патча 1` уже есть отдельная проверка scene model и Graphviz tests.
- Значит текущая серия изменений уже естественно раскладывается на отдельные commit-ы.

Следствие:
- После успешной проверки `Патч 0` и `Патч 1` нужно оформить отдельными commit-ами.
- Дальнейшие патчи в этом плане тоже нужно вести по тому же правилу.

## 2026-04-06 17:52:05 PDT

Решение:
Первый scene model срез для Graphviz ввести прямо в `PSGraphView.Graphviz` в виде:
- `GraphScene` с graph-level `Commands` и `Objects`;
- `SceneObject` с `Kind`, `Name`, `Bounds`, `Commands`;
- отдельного набора geometry/style/command types для `Ellipse`, `Polygon`, `Bezier`, `Polyline`, `Text`;
- полиморфной JSON-сериализации для `SceneCommand` и `ScenePaint` как тестового и отладочного контракта.

Причины:
- Это минимальный набор, который уже соответствует плану и не требует отдельного renderer-проекта.
- Модель уже готова для следующего шага `xdot_json -> scene`, но пока не перегружена лишними abstraction-слоями.
- Полиморфная сериализация упрощает тесты, снимки состояния и будущую диагностику interpreter-а.

Телеметрия / наблюдения:
- Добавлены файлы scene model:
  - [GraphScene.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/GraphScene.cs)
  - [SceneGeometry.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/SceneGeometry.cs)
  - [SceneStyle.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/SceneStyle.cs)
  - [SceneCommands.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/SceneCommands.cs)
- Добавлены тесты:
  - [SceneModelTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/SceneModelTests.cs)
- Проверка:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `5 passed`, `0 failed`, `0 skipped`

Следствие:
- Следующий шаг можно вести уже как реализацию interpreter-а поверх готовой scene model.
- `MSAGL` и `DSM` эта модель пока не затрагивает.

## 2026-04-06 17:48:08 PDT

Решение:
Считать `Патч 0` по native test harness практически закрытым через два изменения:
- расширить ad-hoc include set при локальной сборке временной `libpsgv.dylib` из исходников `graphviz`;
- поднять такой же provisioning path и для cmdlet-level native tests, чтобы `Export-GraphvizView -As Json` не оставался в skip.

Причины:
- Проблема была не в interop-коде `PSGraphView.Graphviz`, а в слишком узком compile command внутри тестов.
- Одного `-I lib` и `-I lib/psgv` недостаточно для цепочки include-ов `gvc/common/cgraph/pathplan`.
- Native-path нужно проверять не только unit/integration тестами interop-слоя, но и через PowerShell cmdlet surface.

Телеметрия / наблюдения:
- После расширения include-dir проходят `PSGraphView.Graphviz.Tests`:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `3 passed`, `0 failed`, `0 skipped`
- После добавления provisioning path в PowerShell tests проходят `ExportGraphvizView` tests без skip:
  - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `4 passed`, `0 failed`, `0 skipped`
- Основные файлы изменения:
  - [GraphvizNativeSessionTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeSessionTests.cs)
  - [GraphvizNativeFactAttribute.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/GraphvizNativeFactAttribute.cs)
  - [ExportGraphvizViewCmdletTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/ExportGraphvizViewCmdletTests.cs)

Следствие:
- Следующий практический шаг можно делать уже поверх рабочего native test harness.
- Дальше можно переходить к `Scene model` и `xdot_json -> scene`, не тратя цикл на ложные падения harness.

## 2026-04-06 17:37:58 PDT

Решение:
Обновить план Graphviz path следующими ограничениями и этапами:
- начинать с `Патча 0`: стабилизация native test harness;
- первый Graphviz renderer-срез держать внутри `PSGraphView.Graphviz`, без нового отдельного renderer-проекта;
- первый milestone формулировать как `scene -> Svg`, а не как обязательный `scene -> SkiaSharp -> Svg`;
- в interpreter сразу включать обход структуры `xdot_json`, а не только список op-кодов;
- зафиксировать отдельный критерий готовности: `Svg/Png/Jpg` после переключения не должны зависеть от системного `dot`;
- ABI `psgv_render_graph(... -> svg/png/jpg)` считать запасным вариантом, а не основным планом.

Причины:
- Сейчас native test loop ненадежен: сначала нужно получить рабочий контур проверки, иначе дальнейшие изменения будет трудно оценивать.
- `PSGraphView.Graphviz` уже владеет Graphviz interop и runtime loading, поэтому первый renderer-срез логичнее размещать там же.
- В репозитории уже есть рабочие SVG path без `SkiaSharp`, поэтому не нужно искусственно связывать первый `Svg` результат с выбором raster backend.
- Реальная сложность `xdot_json` выше, чем просто набор op-кодов: там есть draw-атрибуты и вложенная graph/subgraph структура.
- Основной незакрытый пользовательский долг сейчас не в наличии `Svg` как такового, а в том, что `Svg|Png|Jpg` все еще зависят от внешнего `dot`.
- В `graphviz` уже существует другой native render ABI, но он ведет к другому архитектурному пути и не должен незаметно подменить текущий план.
- Ошибка предыдущей формулировки плана была в том, что она слишком рано зафиксировала `SkiaSharp` как обязательную часть первого `Svg` milestone.

Телеметрия / наблюдения:
- `ExportGraphvizView` cmdlet tests:
  - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `3 passed`, `1 skipped`
- `PSGraphView.Graphviz` native tests:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --no-restore`
  - результат: `3 failed`
  - причина падения: test harness не собирает временную `libpsgv.dylib`, потому что не находит `cdt.h`
- Источник проблемы в test harness:
  - [GraphvizNativeSessionTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeSessionTests.cs)
- В `graphviz` есть запасной render ABI:
  - [psgv.h](/Users/andrei/.codex/worktrees/f5d8/graphviz/lib/psgv/psgv.h)
  - [test_psgv_smoke.cpp](/Users/andrei/.codex/worktrees/f5d8/graphviz/tests/test_psgv_smoke.cpp)

Следствие:
- `plan.md` должен начинаться с починки test harness.
- Первый Graphviz `Svg` milestone не должен блокироваться выбором `SkiaSharp`.
- `MSAGL` остается вне этого плана.

## 2026-04-06 17:36:23 PDT

Решение:
`MSAGL` оставить на текущем собственном renderer path, а `SkiaSharp` рассматривать для:
- Graphviz native render path как основной текущий этап;
- `DSM` как возможный отдельный следующий этап.

Причины:
- В `MSAGL` уже есть рабочий и покрытый тестами SVG path, и его замена сейчас только расширит scope.
- Текущая задача про native Graphviz path и не требует пересборки уже рабочего `MSAGL` renderer-а.
- `DSM` визуально проще и потенциально лучше подходит для отдельной последующей унификации на `SkiaSharp`, если после Graphviz это даст реальную пользу.

Телеметрия / наблюдения:
- В `MSAGL` уже есть собственный SVG renderer:
  - [MsaglSvgRenderer.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Msagl/MsaglSvgRenderer.cs)
- В `DSM` уже есть отдельный SVG exporter:
  - [DsmSvgExporter.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Dsm/DsmSvgExporter.cs)
- Текущий план Graphviz path уже изолирован от `MSAGL` по cmdlet surface:
  - [ExportGraphvizViewCmdlet.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphvizViewCmdlet.cs)
- Количественной телеметрии по выгоде миграции `DSM` на `SkiaSharp` пока нет.

Следствие:
- План по Graphviz не должен включать миграцию `MSAGL`.
- Возможную миграцию `DSM` нужно вести отдельным follow-up после стабилизации Graphviz path.

## 2026-04-06 16:07:44 PDT

Решение:
Основной native Graphviz path в `PSGraphView` строить как:
- `DOT -> libpsgv -> xdot_json -> normalized scene -> SkiaSharp -> Svg/Png/Jpg`

Причины:
- `PSGraphView` должен быть потребителем scene/layout, а не владельцем layout-алгоритмов.
- `libpsgv` уже закрывает `DOT -> xdot_json`.
- Это позволяет убрать зависимость от внешнего `dot` для финального render path после завершения renderer-а.

Телеметрия / наблюдения:
- В `PSGraphView` уже есть:
  - [ExportGraphvizViewCmdlet.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphvizViewCmdlet.cs)
  - [GraphvizNativeSession.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/GraphvizNativeSession.cs)
  - [GraphvizNativeLayoutRenderer.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/GraphvizNativeLayoutRenderer.cs)
- `Export-GraphvizView -As Json` уже использует native path.

Следствие:
- Следующий основной этап разработки находится уже в `PSGraphView`, а не в `graphviz`.

## 2026-04-06 16:07:44 PDT

Решение:
Границу между `PSGraph` и `PSGraphView` держать текстовой через DOT, а не через общий managed DTO.

Причины:
- `PSGraph` уже умеет `Export-Graph -Format Graphviz`.
- Такой поток позволяет `PSGraphView` не зависеть напрямую от внутренней доменной модели `PSGraph`.
- Это хорошо совпадает с выбранным UX:
  - `Export-Graph -Graph $g -Format Graphviz | Export-GraphvizView ...`
  - `Export-GraphvizView -DotPath file.dot ...`

Телеметрия / наблюдения:
- В `PSGraph` prerequisite по полноте DOT boundary уже закрыт:
  - vertex attrs
  - edge attrs
  - graph-level attrs
- Это решение пришло из предыдущего этапа в `graphviz` и `PSGraph`.

Следствие:
- `PSGraphView` не должен проектировать новый DTO-вход для этого renderer path.

## 2026-04-06 16:07:44 PDT

Решение:
Между `xdot_json` и `SkiaSharp` использовать normalized scene model, а не сырые `xdot` state-операции.

Причины:
- `xdot_json` содержит и геометрию, и state (`c`, `C`, `F`, `S`).
- Если отдать это напрямую renderer-у, слой `SkiaSharp` превратится во вторую реализацию `xdot` state-machine.
- Проще и надежнее держать state только в interpreter-е, а renderer-у отдавать уже готовые draw-команды.

Телеметрия / наблюдения:
- По [gvrender_core_json.c](/Users/andrei/.codex/worktrees/f5d8/graphviz/plugin/core/gvrender_core_json.c) в `xdot_json` уже есть операции:
  - `E/e`
  - `P/p`
  - `B/b`
  - `L`
  - `T`
  - `c/C`
  - `F`
  - `S`
- Это дает естественный MVP-набор команд:
  - `EllipseCommand`
  - `PolygonCommand`
  - `BezierCommand`
  - `PolylineCommand`
  - `TextCommand`

Следствие:
- Следующий практический патч должен вводить scene types и interpreter, а не сразу `SkiaSharp` drawing code.

## 2026-04-06 15:56:16 PDT

Решение:
Считать bundled native runtime из `graphviz` подтвержденной базой для `PSGraphView` publish path.

Причины:
- Runtime bundle уже проверен на всех трех целевых платформах.
- Это снимает риск “доделать renderer, а потом снова упереться в native packaging”.

Телеметрия / наблюдения:
- GitHub repo: `eosfor/graphviz-psgv`
- Preview release run: `24019580372`
- Результат:
  - `linux-x64`: success
  - `osx-arm64`: success
  - `win-x64`: success
  - `release`: success
- `PSGraphView` уже умеет скачивать runtime bundle в publish:
  - [PSGraphView.PowerShell.csproj](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj)
  - [Install-GraphvizRuntimeBundle.ps1](/Users/andrei/repo/PSGraphView/eng/Install-GraphvizRuntimeBundle.ps1)

Следствие:
- Следующий этап можно вести как обычную managed-разработку поверх уже подтвержденного native baseline.

## 2026-04-06 16:07:44 PDT

Решение:
Первый renderer-срез ограничить минимальным набором примитивов и форматов:
- примитивы:
  - ellipse
  - polygon
  - bezier
  - polyline
  - text
- форматы:
  - сначала `Svg`
  - затем `Png/Jpg`

Причины:
- Это покрывает основной полезный Graphviz output для наших сценариев.
- Так проще быстрее перевести `Export-GraphvizView -As Svg` на native path и получить первый полноценный пользовательский результат.
- Не нужно пытаться сразу повторить весь визуальный язык Graphviz.

Телеметрия / наблюдения:
- Текущий основной тестовый сценарий:
  - `WikiVote`
- Текущий уже рабочий промежуточный сценарий:
  - `Export-GraphvizView -As Json`

Следствие:
- Следующий план патчей:
  - scene model
  - interpreter
  - Svg renderer
  - затем raster formats
