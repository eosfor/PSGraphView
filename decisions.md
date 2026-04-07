# Architecture Decision Log

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
