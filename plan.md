# Plan: Native Graphviz Path In PSGraphView

## Цель

Довести `PSGraphView` до полноценного native Graphviz renderer path:
- вход: DOT из pipeline (`Export-Graph -Format Graphviz | Export-GraphvizView`) или DOT-файл;
- layout и scene: `libpsgv` (`DOT -> xdot_json`);
- рендер: `PSGraphView` (`xdot_json -> normalized scene -> SkiaSharp -> Svg/Png/Jpg`);
- без обязательной установки полного Graphviz на машине пользователя.

## Текущее состояние

Завершено:
- есть `Export-GraphvizView` с нужным UX:
  - `-InputObject`
  - `-DotPath`
  - `-Renderer`
  - `-As`
  - `-OutputPath`
- `Export-GraphvizView -As Json` уже идет по native пути `DOT -> libpsgv -> xdot_json`.
- publish уже умеет скачивать bundled runtime из private release-ов `eosfor/graphviz-psgv` и раскладывать его в `runtimes/<rid>/native`.
- `PSGraphView.Graphviz` уже умеет грузить bundled `libpsgv`.
- runtime bundle на стороне `graphviz` подтвержден на:
  - `linux-x64`
  - `osx-arm64`
  - `win-x64`
  - preview release run `24019580372`

Частично завершено:
- `Export-GraphvizView -As Svg|Png|Jpg` существует, но пока использует process-based Graphviz fallback.
- `WikiVote` можно прогонять до `xdot_json`, но не до финального native `Svg/Png/Jpg`.

Не завершено:
- interpreter `xdot_json -> scene`
- `SkiaSharp` renderer `scene -> Svg/Png/Jpg`
- переключение `Export-GraphvizView -As Svg|Png|Jpg` на native path

## Архитектура

Поток данных:
- `PSGraph` строит граф и экспортирует DOT.
- `Export-GraphvizView` принимает DOT.
- `PSGraphView.Graphviz` вызывает `libpsgv`.
- `libpsgv` возвращает `xdot_json`.
- interpreter в `PSGraphView` превращает `xdot_json` в managed scene model.
- renderer на `SkiaSharp` рисует scene model в `Svg/Png/Jpg`.

Граница ответственности:
- `PSGraph`: доменная модель графа и DOT-export.
- `libpsgv`: `DOT -> layout -> xdot_json`.
- `PSGraphView.Graphviz`: native interop и чтение runtime bundle.
- новый renderer слой в `PSGraphView`: `xdot_json -> scene -> SkiaSharp`.

## Scene Model

- Между `xdot_json` и `SkiaSharp` вводим normalized scene model.
- Renderer не должен исполнять сырую `xdot` state-machine.
- Interpreter может держать текущий state (`pen`, `fill`, `font`, `style`), но наружу должен отдавать уже нормализованные draw-команды.

Базовые типы:
- `GraphScene`
- `SceneObject`
- `ScenePoint`
- `SceneRect`
- `SceneColor`
- `SceneGradient`
- `SceneFont`
- `SceneStrokeStyle`

Базовые draw-команды для MVP:
- `EllipseCommand`
- `PolygonCommand`
- `BezierCommand`
- `PolylineCommand`
- `TextCommand`

Маппинг `xdot_json` для MVP:
- `E/e` -> ellipse
- `P/p` -> polygon
- `B/b` -> bezier
- `L` -> polyline
- `T` -> text
- `c/C/F/S` меняют текущий interpreter state, который вшивается в следующую draw-команду

Для первого renderer-среза:
- обязательно:
  - solid colors
  - plain text labels
  - `solid/dashed/dotted`
- можно оставить на второй шаг:
  - gradients
  - более экзотические стили

## Последовательность патчей

Патч 1. Scene model
- Добавить в `PSGraphView.Graphviz` или отдельный renderer-проект базовые scene types.
- Добавить unit tests на сериализованную/ожидаемую модель команд.

Патч 2. Xdot interpreter
- Реализовать `xdot_json -> normalized scene`.
- Начать с поддержки:
  - ellipse
  - polygon
  - bezier
  - polyline
  - text
  - color/font/style state
- Добавить unit tests на маленьких реальных `xdot_json` payload-ах.

Патч 3. Svg renderer
- Реализовать `scene -> Svg` через `SkiaSharp`.
- Подключить его в `Export-GraphvizView -As Svg`.
- Process fallback оставить временно только для `Png/Jpg`, если это ускоряет доставку первого результата.

Патч 4. Raster renderer
- Реализовать `scene -> Png/Jpg`.
- Переключить `Export-GraphvizView -As Png|Jpg` на тот же native path.

Патч 5. End-to-end сценарии
- Добавить быстрый smoke path:
  - inline DOT
  - `Json`
  - `Svg`
- Добавить более крупный интеграционный сценарий:
  - `WikiVote`
  - сначала subgraph
  - затем full graph

## Основные проверки

Минимальные обязательные проверки:
- `Export-GraphvizView -As Json` не ломается после введения interpreter-а
- `Export-GraphvizView -As Svg` отдает валидный SVG на простом DOT
- `Export-GraphvizView -As Png` и `-As Jpg` создают не пустые бинарные файлы
- bundled runtime реально используется, без системного Graphviz

## Риски

- `xdot_json` содержит state-операции, поэтому нельзя строить renderer прямо поверх raw JSON.
- Текст и шрифты могут визуально отличаться между платформами, даже если layout уже посчитан.
- На первом шаге не нужно пытаться закрыть весь визуальный язык Graphviz; лучше сначала стабилизировать основной набор примитивов.

## Следующий шаг

Следующий практический шаг в этом репозитории:
- ввести normalized scene model;
- затем сделать первый interpreter `xdot_json -> scene`.
