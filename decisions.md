# Architecture Decision Log

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
