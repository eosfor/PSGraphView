# Plan: Native Graphviz Path In PSGraphView

## Цель

Довести `PSGraphView` до полноценного native Graphviz renderer path:
- вход: DOT из pipeline (`Export-Graph -Format Graphviz | Export-GraphvizView`) или DOT-файл;
- layout и scene: `libpsgv` (`DOT -> Graphviz JSON draw payload`);
- рендер: `PSGraphView` (`xdot_json -> normalized scene -> Svg/Png/Jpg`);
- без обязательной установки системного `dot` или полного системного Graphviz на машине пользователя.
- целевой конечный сценарий:
  - все поддержанные native output-path должны работать только на bundled/runtime `libpsgv` и нашем managed renderer-е
  - ключевые проверки должны уметь подтверждать это на машине без системного `dot` и без установленного системного Graphviz

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
- `Патч 3a` закрыт:
  - добавлен cmdlet-level regression-test, который подтверждает, что `Export-GraphvizView -As Svg` не зависит от внешнего `dot`
- `Патч 4` закрыт:
  - добавлен managed `scene -> Png/Jpg` renderer на `SkiaSharp`
  - `Export-GraphvizView -As Png|Jpg` переключен на native path `DOT -> libpsgv -> xdot_json -> scene -> raster`
  - cmdlet-level tests на `Png/Jpg` теперь тоже ломают путь к `dot`, но ожидают успешный native render
- runtime bundle на стороне `graphviz` подтвержден на:
  - `linux-x64`
  - `osx-arm64`
  - `win-x64`
  - preview release run `24019580372`
- есть повторяемый manual compare/benchmark script:
  - `demos/Compare-WikiVote-GraphvizSvg.ps1`
  - он строит общий `wiki-vote.dot` или берет готовый `-DotPath`
  - пишет оба `Svg` и `wiki-vote-svg-benchmark.json` с warm/cold замерами

Частично завершено:
- `Патч 5` уже закрыт частично:
  - есть manual compare/benchmark script для `WikiVote`
  - `WikiVote` можно прогонять до финального native `Svg/Png/Jpg`
  - для raster compare теперь есть отдельный metric helper и demo-скрипт
  - первые численные метрики уже сняты на простом DOT smoke graph и на `WikiVote` subgraph
  - но отдельный automated integration contour для более крупных графов и acceptance-thresholds для raster divergence еще не закрыты
- Для `Патча 6` уже добавлены:
  - общий smoke-скрипт для `pwsh -NoProfile -> Import-Module psd1 -> Export-GraphvizView -As Json|Svg|Png|Jpg`
  - локальный helper для staged bundled-layout smoke без приватного runtime token
  - отдельный `GitHub Actions` workflow с matrix на `Linux`, `Windows` и `macOS`
  - фактический CI прогон на `graphviz runtime 0.1.0-beta.12` уже подтвержден полностью:
    - `macOS`: green
    - `Windows`: green
    - `Linux`: green
  - no-system smoke теперь подтверждает и bundled `libpsgv`, и managed `Svg/Png/Jpg` path на всех трех runner-ах
  - численное сравнение raster output и дальнейшая телеметрия еще остаются частью незавершенного этапа

Не завершено:
- отдельный follow-up на image-операции, если Graphviz JSON plugin начнет выдавать `xd_image`
- отдельный thresholded regression-step для raster compare, чтобы численные метрики можно было использовать не только вручную
- отдельный более крупный integration contour для `WikiVote` и других больших графов поверх уже работающего no-system smoke
- curated fixture-suite из существующих `.gv/.dot` входов `graphviz` для repeatable cross-platform regression в `GitHub Actions`

## Архитектура

Поток данных:
- `PSGraph` строит граф и экспортирует DOT.
- `Export-GraphvizView` принимает DOT.
- `PSGraphView.Graphviz` вызывает `libpsgv`.
- `libpsgv` возвращает Graphviz JSON draw payload.
- interpreter в `PSGraphView` превращает Graphviz JSON draw payload в managed scene model.
- renderer backend превращает scene model в `Svg/Png/Jpg`.

Граница ответственности:
- `PSGraph`: доменная модель графа и DOT-export.
- `libpsgv`: `DOT -> layout -> Graphviz JSON draw payload`.
- `PSGraphView.Graphviz`: native interop и чтение runtime bundle.
- первый renderer слой для Graphviz логично держать в `PSGraphView.Graphviz`, а не в отдельном новом проекте.
- новый renderer слой в `PSGraphView`: `Graphviz JSON -> scene -> Svg/Png/Jpg`.
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

Патч 3a. Защитная проверка Svg
- Статус:
  - выполнен в текущей ветке
- Добавить узкий regression-test, который подтверждает, что `Export-GraphvizView -As Svg` не зависит от внешнего `dot`.
- Проверка должна ломаться, если `Svg` path снова начнет уходить в `GraphvizProcessRenderer`.
- Практический сценарий:
  - сломанный `PATH` для `dot`
  - или намеренно невалидный путь к `dot`
  - при этом native `Svg` должен оставаться рабочим
- Это локальная страховка для уже готового `Svg`, а не финальная проверка полной автономности решения.

Патч 4. Raster renderer
- Статус:
  - выполнен в текущей ветке
- Выбрать raster backend после появления стабильного `Svg` path.
- Базовый кандидат: `SkiaSharp`.
- Реализовать `scene -> Png/Jpg`.
- Переключить `Export-GraphvizView -As Png|Jpg` на тот же native path.

Патч 4a. Модульный runtime path для raster
- Статус:
  - выполнен в текущей ветке
- Закрыть standalone PowerShell-сценарий через `Import-Module ...psd1` для `Png/Jpg`.
- Добиться, чтобы `SkiaSharp.dll` и native `libSkiaSharp` корректно подхватывались в модульном layout, а не только в test host или direct `.NET` runner.
- После этого повторно проверить:
  - `Export-GraphvizView -As Png`
  - `Export-GraphvizView -As Jpg`
  - запуск из обычного `pwsh -NoProfile` через импорт модуля по `psd1`

Патч 5. End-to-end сценарии
- Статус:
  - выполнен частично в текущей ветке
  - есть общий smoke path и manual compare/benchmark script для `WikiVote`
  - добавлен отдельный raster compare path:
    - `RasterImageComparer`
    - `demos/Compare-WikiVote-GraphvizRaster.ps1`
  - первые численные raster metrics уже сняты
  - automated contour для больших графов и acceptance-thresholds еще не завершены
- Добавить быстрый smoke path:
  - inline DOT
  - `Json`
  - `Svg`
- Добавить более крупный интеграционный сценарий:
  - `WikiVote`
  - сначала subgraph
  - затем full graph
- Держать ручной compare/benchmark script для `WikiVote`, чтобы визуальные и временные регрессии проверялись на одном и том же DOT входе.

Патч 6. Автономные проверки без системного Graphviz
- Статус:
  - основной сценарий выполнен в текущей ветке
  - общий smoke-скрипт, локальный bundled-layout helper и cross-platform CI workflow уже добавлены
  - первый downstream workflow на `graphviz runtime 0.1.0-beta.11` уже дал полезный срез:
    - `macOS`: green
    - `Windows`: green
    - `Linux`: нашел bug в consumer preload order
  - bug в Linux preload order уже исправлен локально, повторный CI прогон обязателен
  - Linux native asset packaging для `SkiaSharp` уже закрыт в текущей ветке
  - default runtime version в workflow уже переведен на актуальный preview, чтобы push-run-ы не уходили обратно на устаревший `0.1.0-beta.3`
  - downstream workflow на `graphviz runtime 0.1.0-beta.12` теперь полностью green:
    - `Linux`: green
    - `macOS`: green
    - `Windows`: green
  - run: `24070711886`
  - compare-step для raster и дальнейшая телеметрия остаются отдельным следующим этапом, а не blocker для автономного native path
- Добавить более жесткие тесты и smoke-сценарии для режима, где на машине нет системного `dot` и нет установленного системного Graphviz.
- Эти проверки должны подтверждать, что решение опирается только на bundled/runtime `libpsgv` и managed renderer-ы.
- Минимум один тест должен падать, если реализация снова начнет звать process renderer или требовать системный Graphviz.
- Сначала подготовить один общий smoke-скрипт для сценария:
  - `pwsh -NoProfile`
  - `Import-Module <path-to-PSGraphView.psd1>`
  - `Export-GraphvizView -As Json|Svg|Png|Jpg`
  - сломанный `PSGRAPHVIEW_GRAPHVIZ_DOT_PATH`
  - отсутствие `dot` в `PATH`
- Затем вынести этот сценарий в `GitHub Actions` matrix:
  - `macos`
  - `linux`
  - `windows`
  - без установки системного `graphviz`
  - с publish-like или module-like layout, максимально близким к реальному пользовательскому запуску
- Для Linux bundled path отдельно зафиксировать regression:
  - versioned shared libraries из bundle (`libgts`, `libpango`, и т.д.) должны preloaded раньше `libgvplugin_*`
  - иначе `NativeLibrary.Load(...)` может падать на plugin-е, хотя нужная зависимость уже лежит рядом в том же bundle
- Для Linux raster path отдельно зафиксировать regression:
  - publish/module layout должен реально содержать Linux native asset для `SkiaSharp`
  - smoke-сценарий должен проверять не только `Json/Svg`, но и `Png/Jpg` на Linux runner
- PR-level CI лучше держать на коротком smoke-наборе.
- Более тяжелые compare/benchmark сценарии лучше оставить отдельно, чтобы не раздувать обычный pipeline.
- Отдельно добавить compare-step для raster output:
  - базовый manual compare-step уже есть в текущей ветке
  - сейчас нужно расширить его до repeatable regression-step:
    - сравнить оригинальный raster от `dot` и managed raster от `PSGraphView`
    - использовать уже введенные численные метрики, а не только визуальное сравнение
    - определить рабочие baseline/thresholds минимум для `WikiVote` subgraph и одного простого smoke graph
- Практический критерий:
  - отсутствие `dot` в `PATH` не должно ломать native `Svg/Png/Jpg` path
  - отсутствие установленного системного Graphviz не должно ломать native `Svg/Png/Jpg` path
  - `Export-GraphvizView` должен оставаться работоспособным при наличии только bundled/runtime native assets

Патч 7. Отдельный follow-up по DSM
- После стабилизации Graphviz path оценить, есть ли смысл перевести `PSGraphView.Dsm` на `SkiaSharp`.
- Рассматривать это как отдельную задачу с отдельными тестами и без затрагивания `MSAGL`.

Патч 6a. Curated Graphviz fixture-suite для cross-platform CI
Статус:
- этап начат
- manifest и локальный fixture-каталог уже добавлены в текущей ветке
- единый runner поверх manifest уже добавлен в текущей ветке
- workflow для `core` suite уже добавлен в текущей ветке
- следующий подшаг: первый реальный `GitHub Actions` прогон и разбор platform-specific падений, если они будут
- Подготовить небольшой curated fixture-набор на основе уже существующих `.gv/.dot` из related `graphviz` repo.
- Не читать fixture-ы из соседнего checkout прямо в CI:
  - выбранные входы должны быть синхронизированы в этот репозиторий
  - источник каждого fixture-а должен оставаться записан в manifest
- Первый срез разделить на два уровня:
  - `core`: короткий, стабильный, обязательный для PR на `Linux`, `Windows`, `macOS`
  - `extended`: более широкий и потенциально более шумный, для `workflow_dispatch` и/или nightly
- Для первого `core` набора брать графы, которые закрывают разные draw-сценарии, но не вносят лишнюю платформенную случайность:
  - clusters/subgraphs
  - records
  - arrow styles
  - обычные text labels
  - простой undirected graph
- Кандидаты для первого `core` набора:
  - `graphs/directed/clust1.gv`
  - `graphs/directed/clust2.gv`
  - `graphs/directed/records.gv`
  - `graphs/directed/record2.gv`
  - `graphs/directed/arrows.gv`
  - `graphs/directed/fsm.gv` или `graphs/directed/states.gv`
  - `graphs/undirected/Petersen.gv`
  - `doc/dotguide/poly.dot` или `doc/dotguide/structs.dot`
- Что пока не включать в обязательный `core` gate:
  - графы с нестабильной межплатформенной font-зависимостью:
    - `graphs/directed/japanese.gv`
    - `graphs/directed/russian.gv`
    - `graphs/directed/Latin1.gv`
  - графы с внешними image asset-ами:
    - `tests/usershape.dot`
- Первый практический патч этого этапа:
  - добавить manifest со списком fixture-ов и их upstream source path
  - добавить sync-скрипт, который копирует выбранные `.gv/.dot` из related `graphviz` repo в локальный fixture-каталог
  - завести локальный `core` fixture-каталог в этом репозитории
- Второй патч этого этапа:
  - добавить единый runner, который проходит по manifest и прогоняет fixture-ы через `PSGraphView.psd1`
  - сохраняет per-fixture summary JSON и артефакты
- Третий патч этого этапа:
  - вынести `core` suite в отдельный `GitHub Actions` workflow на трех платформах
  - `extended` suite оставить необязательным и более тяжелым

## Основные проверки

Минимальные обязательные проверки:
- `Export-GraphvizView -As Json` не ломается после введения interpreter-а
- `Export-GraphvizView -As Svg` отдает валидный SVG на простом DOT
- `Export-GraphvizView -As Svg` не зависит от внешнего `dot`
- `Export-GraphvizView -As Png` и `-As Jpg` создают валидные бинарные файлы
- `Export-GraphvizView -As Png` и `-As Jpg` работают в обычном standalone PowerShell module path через `psd1`
- bundled runtime реально используется, без системного Graphviz
- целевой end-to-end сценарий проходит на машине без системного `dot` и без установленного системного Graphviz
- этот end-to-end сценарий проходит в `GitHub Actions` matrix минимум на `macOS`, `Linux` и `Windows`
- curated `core` fixture-suite проходит в `GitHub Actions` на `macOS`, `Linux` и `Windows`
- каждый fixture из `core` набора успешно отдает `Json|Svg|Png|Jpg` без внешнего `dot`
- fixture-suite публикует summary JSON и артефакты так, чтобы падение можно было разобрать без локального воспроизведения
- для raster path есть отдельная телеметрия по расхождению между оригинальным `dot` output и managed raster output
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
- Не все upstream `.gv/.dot` одинаково подходят для обязательного cross-platform gate:
  - font-heavy и locale-heavy примеры могут шуметь между ОС даже при корректной работе renderer-а
  - fixture-suite нужно начинать с curated stable subset, а не со случайного массового прогона всего upstream набора

## Следующий шаг

Следующий практический шаг в этом репозитории:
- начать curated fixture-suite для cross-platform CI:
  - manifest выбранных upstream `.gv/.dot` уже добавлен
  - sync-скрипт и локальный `core` fixture-каталог уже добавлены
  - единый runner уже добавлен и локально прогнан на `core`
  - workflow для `core` fixture-набора уже добавлен
  - следующий подшаг: прогнать его на трех платформах и разобрать результаты
- затем вернуться к repeatable baseline для raster compare:
  - зафиксировать рабочие baseline/thresholds для `RMSE`, `different-pixel %` и `SSIM`
  - начать с уже снятых чисел для `WikiVote` subgraph и простого smoke graph
- image-операции `I` возвращать в план только если они реально начнут приходить из `xdot_json`;
- `MSAGL` по-прежнему держать вне этого изменения.
