# Plan: Native Graphviz Path In PSGraphView

## Цель

Довести `PSGraphView` до полноценного native Graphviz renderer path:
- вход: DOT из pipeline (`Export-Graph -Format Graphviz | Export-GraphvizView`) или DOT-файл;
- layout и scene: `libpsgv` (`DOT -> Graphviz JSON draw payload`);
- рендер: `PSGraphView` (`xdot_json -> normalized scene -> Svg/Png/Jpg`);
- без обязательной установки полного Graphviz на машине пользователя.

Для первого полноценного результата:
- `Svg` считать отдельным milestone и не завязывать его жестко на `SkiaSharp`.
- backend для `Svg` оставить implementation detail.
- `SkiaSharp` рассматривать как основной кандидат для raster path и как допустимый backend для будущих renderer-ов, но не как обязательное условие для первого `Svg`.

Ограничение scope для этого плана:
- `MSAGL` остается на текущем собственном renderer path и не переводится на `SkiaSharp`.
- `DSM` можно рассматривать как отдельный кандидат на будущий переход к `SkiaSharp`, но это не должно смешиваться с первым Graphviz-срезом.

## Текущее состояние

Завершено:
- есть `Export-GraphvizView` с нужным UX:
  - `-InputObject`
  - `-DotPath`
  - `-Renderer`
  - `-As`
  - `-OutputPath`
- `Export-GraphvizView -As Json` уже идет по native пути `DOT -> libpsgv -> xdot_json`.
- native layout payload скорректирован:
  - для draw-команд используется Graphviz `json`, а не `xdot_json`
  - текущие managed имена `XdotJson` пока оставлены ради совместимости, но фактический payload нужно считать Graphviz JSON с `_draw_`/`_ldraw_`
- publish уже умеет скачивать bundled runtime из private release-ов `eosfor/graphviz-psgv` и раскладывать его в `runtimes/<rid>/native`.
- `PSGraphView.Graphviz` уже умеет грузить bundled `libpsgv`.
- `Патч 0` закрыт:
  - native test harness для `PSGraphView.Graphviz.Tests` работает
  - cmdlet-level native test для `Export-GraphvizView -As Json` больше не в skip
- базовый `Патч 1` закрыт:
  - scene model введена в `PSGraphView.Graphviz`
  - добавлены tests на форму модели и полиморфную сериализацию
- базовый `Патч 2a` закрыт:
  - добавлен первый interpreter `xdot_json -> scene` в `PSGraphView.Graphviz`
  - покрыты top-level graph, `objects`, `edges` и draw-атрибуты `_draw_`, `_ldraw_`, `_hdraw_`, `_tdraw_`, `_hldraw_`, `_tldraw_`
  - для MVP поддержаны `E/e`, `P/p`, `B/b`, `L`, `T`, `c/C`, `F`, `S`
  - добавлены tests на синтетические и native `xdot_json` payload-ы
- `Патч 2b` закрыт:
  - interpreter теперь реально обходит membership-ссылки `subgraphs`, `nodes`, `edges`
  - nested cluster/subgraph payload-ы не зависят от сырого порядка объектов в JSON
  - добавлена явная проверка на битые membership-индексы
  - добавлены tests на nested subgraph traversal
- `Патч 2c` закрыт:
  - добавлена поддержка `t` через font-style flags в scene model
  - подтверждено, что label draw-атрибуты могут содержать не только `T`, и этот path покрыт tests
  - `I` оставлен отдельным follow-up, потому что текущий `xdot_json` plugin `xd_image` не сериализует
- `Патч 3` закрыт:
  - добавлен managed `scene -> Svg` renderer
  - `Export-GraphvizView -As Svg` переключен на native path `DOT -> libpsgv -> xdot_json -> scene -> Svg`
  - process renderer оставлен только для `Png/Jpg`
  - `MSAGL` не затронут
- runtime bundle на стороне `graphviz` подтвержден на:
  - `linux-x64`
  - `osx-arm64`
  - `win-x64`
  - preview release run `24019580372`

Частично завершено:
- `Export-GraphvizView -As Svg|Png|Jpg` существует, но пока использует process-based Graphviz fallback.
- `WikiVote` можно прогонять до `xdot_json`, но не до финального native `Svg/Png/Jpg`.

Не завершено:
- полный interpreter `xdot_json -> scene`, включая рекурсивный обход вложенных `subgraphs`
- расширенный interpreter coverage для `t`, `I`, record/HTML labels и `decorate=true`
- отдельный follow-up на image-операции, если `xdot_json` plugin начнет выдавать `xd_image`
- native `scene -> Png/Jpg`
- переключение `Export-GraphvizView -As Png|Jpg` на native path
- явная проверка, что после переключения путь не зависит от системного `dot`

## Архитектура

Поток данных:
- `PSGraph` строит граф и экспортирует DOT.
- `Export-GraphvizView` принимает DOT.
- `PSGraphView.Graphviz` вызывает `libpsgv`.
- `libpsgv` возвращает `xdot_json`.
- interpreter в `PSGraphView` превращает Graphviz JSON draw payload в managed scene model.
- renderer backend превращает scene model в `Svg/Png/Jpg`.

Граница ответственности:
- `PSGraph`: доменная модель графа и DOT-export.
- `libpsgv`: `DOT -> layout -> xdot_json`.
- `PSGraphView.Graphviz`: native interop и чтение runtime bundle.
- первый renderer слой для Graphviz логично держать в `PSGraphView.Graphviz`, а не в отдельном новом проекте.
- новый renderer слой в `PSGraphView`: `xdot_json -> scene -> Svg/Png/Jpg`.
- `PSGraphView.Msagl`: остается на своем текущем SVG renderer path.
- `PSGraphView.Dsm`: текущий SVG exporter остается рабочим; возможный `SkiaSharp` path идет отдельным этапом после стабилизации Graphviz.

## Scene Model

- Основная справка по семантике xdot для этого плана:
  - [Graphviz Library Manual, section 1.1.2 xdot](https://graphviz.org/pdf/libguide.pdf)
- Практическое замечание для текущего `libpsgv`:
  - для draw-операций в этом проекте нужно ориентироваться на Graphviz `json`, потому что `xdot_json` в текущем native path не несет `_draw_`/`_ldraw_`.
- Между `xdot_json` и renderer backend вводим normalized scene model.
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

Структура `xdot_json`, которую нужно учитывать сразу:
- draw-команды живут не только как набор op-кодов, но и внутри атрибутов:
  - `_draw_`
  - `_ldraw_`
  - `_hdraw_`
  - `_tdraw_`
  - `_hldraw_`
  - `_tldraw_`
- у top-level graph есть `objects`, у вложенных graph/subgraph есть `subgraphs`.
- interpreter должен уметь обходить эту структуру и собирать scene по graph/node/edge/subgraph объектам, а не только читать один список операций.
- по `libguide.pdf`:
  - все координаты и размеры в xdot задаются в points;
  - `T` обычно живет в label-атрибутах;
  - при `shape=record`, HTML-like label и `decorate=true` в label-атрибутах могут появляться и нетекстовые draw-операции;
  - кроме MVP-набора есть как минимум `t` и `I`, их нужно учитывать как отдельные follow-up операции, а не забытые edge-case.

## Последовательность патчей

Правило завершения патча:
- после каждого патча нужно делать узкую, но реальную проверку именно затронутого поведения;
- патч считается удачным только если эта проверка прошла и не выявила новый blocker в измененном срезе;
- удачный патч нужно завершать отдельным commit, а не копить несколько уже проверенных патчей в рабочем дереве;
- если проверка не прошла, патч не считается завершенным и не должен фиксироваться как завершенный commit.

Патч 0. Стабилизация проверки и тестового контура
Статус:
- выполнен в текущей ветке
- Починить локальную сборку тестовой `libpsgv` из исходников `graphviz` для `PSGraphView.Graphviz.Tests`.
- Закрыть текущий blocker с include-path до `lib/cdt` и соседних include-dir.
- Добиться, чтобы native tests падали только по реальным проблемам interop/layout, а не по проблемам test harness.

Патч 1. Scene model
Статус:
- базовый срез выполнен в текущей ветке
- Добавить базовые scene types прямо в `src/PSGraphView.Graphviz`.
- Не заводить отдельный renderer-проект на первом шаге без явной необходимости.
- Добавить unit tests на ожидаемую модель команд и state.

Патч 2a. Первый xdot interpreter
Статус:
- выполнен в текущей ветке
- Реализовать первый `xdot_json -> normalized scene`.
- Покрыть базовую структуру документа:
  - top-level graph
  - `objects`
  - `edges`
  - draw-атрибуты `_draw_`, `_ldraw_`, `_hdraw_`, `_tdraw_`, `_hldraw_`, `_tldraw_`
- Начать с поддержки:
  - ellipse
  - polygon
  - bezier
  - polyline
  - text
  - color/font/style state
- Добавить unit tests на маленьких synthetic и native `xdot_json` payload-ах.

Патч 2b. Доработка interpreter-а
Статус:
- выполнен в текущей ветке
- Добрать рекурсивный обход вложенных `subgraphs`, если они присутствуют в payload.
- Добавить tests на graph с cluster/subgraph, чтобы scene не теряла group-level draw-команды.

Патч 2c. Расширенный xdot coverage
Статус:
- выполнен в текущей ветке
- Добавить поддержку `t`.
- Проверить payload-ы с record/HTML labels и `decorate=true`, где label draw-атрибуты могут содержать не только `T`.
- `I` оставить отдельным follow-up, пока текущий `xdot_json` plugin не сериализует `xd_image`.
- После этого считать interpreter слой достаточно полным для перехода к `Svg`.

Патч 3. Svg renderer
Статус:
- выполнен в текущей ветке
- Реализовать `scene -> Svg`.
- Не считать `SkiaSharp` обязательным для этого патча.
- Подключить его в `Export-GraphvizView -As Svg`.
- Process fallback оставить временно только для `Png/Jpg`, если это ускоряет доставку первого результата.

Патч 4. Raster renderer
- Выбрать raster backend после появления стабильного `Svg` path.
- Базовый кандидат: `SkiaSharp`.
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

Патч 6. Проверка независимости от системного Graphviz
- Добавить тесты, подтверждающие, что после переключения `Svg/Png/Jpg` идут не через внешний `dot`.
- Минимум один тест должен падать, если реализация снова начнет звать process renderer.
- Практический критерий:
  - отсутствие `dot` в `PATH` или намеренно сломанный путь к `dot` не должны ломать native `Svg/Png/Jpg` path.

Патч 7. Отдельный follow-up по DSM
- После стабилизации Graphviz path оценить, есть ли смысл перевести `PSGraphView.Dsm` на `SkiaSharp`.
- Рассматривать это как отдельную задачу с отдельными тестами и без затрагивания `MSAGL`.

## Основные проверки

Минимальные обязательные проверки:
- `Export-GraphvizView -As Json` не ломается после введения interpreter-а
- `Export-GraphvizView -As Svg` отдает валидный SVG на простом DOT
- `Export-GraphvizView -As Png` и `-As Jpg` создают не пустые бинарные файлы
- bundled runtime реально используется, без системного Graphviz
- `Svg/Png/Jpg` после переключения не требуют системный `dot`
- native tests на `PSGraphView.Graphviz.Tests` проходят через рабочий test harness, а не падают на сборке вспомогательной библиотеки
- native Graphviz tests не запускаются параллельно, если upstream path падает на assert при одновременных сессиях

## Резервный путь

В `graphviz` уже есть отдельный native ABI для render path из нейтрального DTO:
- `psgv_render_graph`
- `psgv_render_graph_once`
- `svg/png/jpg` как primary format

Это не основной путь для текущего этапа.

Его стоит рассматривать только как запасной вариант, если:
- interpreter `xdot_json -> scene` начнет резко разрастаться;
- точность или стоимость поддержки окажутся хуже ожидаемого;
- понадобится временно получить native `Svg/Png/Jpg` быстрее, чем будет готов полноценный managed scene path.

## Риски

- `xdot_json` содержит state-операции, поэтому нельзя строить renderer прямо поверх raw JSON.
- Текст и шрифты могут визуально отличаться между платформами, даже если layout уже посчитан.
- На первом шаге не нужно пытаться закрыть весь визуальный язык Graphviz; лучше сначала стабилизировать основной набор примитивов.
- Не нужно смешивать в одну серию патчей Graphviz renderer path и возможную миграцию `DSM` на `SkiaSharp`.
- Не нужно трогать `MSAGL`, пока для него нет отдельной причины и отдельного плана миграции.
- Если пропустить стабилизацию test harness, можно долго чинить не продуктовый код, а окружение тестов.
- Если жестко привязать первый `Svg` milestone к `SkiaSharp`, можно искусственно увеличить объем первого рабочего среза.
- Upstream Graphviz native path в тестах сейчас нельзя считать безопасным для параллельного запуска нескольких сессий в одном процессе.
- По `libguide.pdf` Graphviz as a library не thread-safe, значит parallel native usage нужно считать отдельным риском до явного доказательства обратного.

## Следующий шаг

Следующий практический шаг в этом репозитории:
- переходить к `Патчу 4`: выбрать и реализовать raster backend для `scene -> Png/Jpg`;
- image-операции `I` возвращать в план только если они реально начнут приходить из `xdot_json`;
- держать `MSAGL` вне этого изменения.
