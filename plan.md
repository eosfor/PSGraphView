# Plan: Native Graphviz Path In PSGraphView

## Цель

Довести `PSGraphView` до полноценного native Graphviz path:
- вход: DOT из pipeline (`Export-Graph -Format Graphviz | Export-GraphvizView`) или DOT-файл;
- layout и draw payload: `libpsgv`;
- рендер: `PSGraphView` (`Graphviz JSON -> normalized scene -> Svg/Png/Jpg`);
- без обязательной установки системного `dot` или полного системного Graphviz на машине пользователя.

Подробная хронология вынесена в [history.md](/Users/andrei/repo/PSGraphView/history.md).
Причины решений и телеметрия по ним остаются в [decisions.md](/Users/andrei/repo/PSGraphView/decisions.md).

## Scope

- `MSAGL` остается на текущем собственном renderer path и не переводится на `SkiaSharp`.
- `DSM` можно рассматривать как отдельный кандидат на будущий переход к `SkiaSharp`, но это не часть текущего Graphviz-плана.
- Для draw-команд в текущем native path ориентируемся на Graphviz `json`, а не на `xdot_json`.

## Текущий статус

Завершено:
- `Патчи 0-4a` закрыты.
- Native path для `Json`, `Svg`, `Png` и `Jpg` реализован.
- `PSGraphView.Graphviz` умеет грузить bundled `libpsgv`.
- `Export-GraphvizView -As Svg` не зависит от внешнего `dot`.
- `Export-GraphvizView -As Png|Jpg` работает в обычном `pwsh` через `Import-Module ...psd1`.
- Cross-platform `no-system-graphviz` smoke подтвержден на `Linux`, `macOS` и `Windows`.
- Curated `core` fixture-suite подтвержден на `Linux`, `macOS` и `Windows`.
- Publish workflow усилен:
  - используется bundled runtime `0.1.0-beta.12` по умолчанию;
  - перед `Publish-Module` есть bundled smoke;
  - есть безопасный `dry_run`;
  - dry-run уже подтвержден.

Частично завершено:
- `Патч 5` закрыт частично:
  - есть manual compare/benchmark для `WikiVote`;
  - есть `RasterImageComparer`;
  - есть demo-скрипт для raster compare;
  - базовые численные метрики уже снимаются, но thresholds и regression gate еще не введены.

Осталось:
- ввести repeatable baseline и thresholds для raster compare;
- расширить integration contour за пределы текущего smoke и `core` fixture-tier;
- добавить отдельные post-publish e2e pipeline-ы на чистых runner-ах с установкой модуля из `PSGallery`;
- обновить CI action-ы под будущее снятие `Node.js 20`;
- отдельно провести первый controlled non-dry-run publish в `PSGallery`;
- вернуть в работу image-операции `I` только если они реально начнут приходить из Graphviz JSON.

## Архитектура

Поток данных:
- `PSGraph` строит граф и экспортирует DOT.
- `Export-GraphvizView` принимает DOT.
- `PSGraphView.Graphviz` вызывает `libpsgv`.
- `libpsgv` возвращает Graphviz JSON draw payload.
- interpreter в `PSGraphView` превращает Graphviz JSON в managed scene model.
- renderer backend превращает scene model в `Svg/Png/Jpg`.

Граница ответственности:
- `PSGraph`: доменная модель графа и DOT-export.
- `libpsgv`: `DOT -> layout -> Graphviz JSON draw payload`.
- `PSGraphView.Graphviz`: native interop, runtime bundle, interpreter, scene model, managed renderers.
- `PSGraphView.Msagl`: остается на своем текущем renderer path.
- `PSGraphView.Dsm`: не входит в этот этап.

## Статус по патчам

- `Патч 0`: выполнен. Native test harness стабилизирован.
- `Патч 1`: выполнен. Scene model добавлена.
- `Патч 2a-2c`: выполнены. Interpreter закрывает основной draw coverage для текущего payload.
- `Патч 3`: выполнен. Managed `scene -> Svg`.
- `Патч 3a`: выполнен. Regression-защита от возврата к внешнему `dot` для `Svg`.
- `Патч 4`: выполнен. Managed `scene -> Png/Jpg` на `SkiaSharp`.
- `Патч 4a`: выполнен. Модульный runtime path для raster через `psd1`.
- `Патч 5`: частично выполнен. Compare и benchmark есть, но acceptance-gate еще нет.
- `Патч 6`: выполнен. Автономный bundled path без системного Graphviz подтвержден.
- `Патч 6a`: выполнен. `core` fixture-suite работает в hosted CI на трех ОС.
- `Патч 7`: backlog. Возможный отдельный follow-up по `DSM`.

## Активные задачи

### 1. Raster Quality Gate
- Зафиксировать baseline/thresholds для:
  - `RMSE`
  - `different-pixel %`
  - `SSIM`
- Начать с:
  - `WikiVote` subgraph
  - одного простого smoke graph
- После этого оформить repeatable regression-step поверх уже существующего compare helper.

### 2. Extended Integration Coverage
- Добавить `extended` fixture tier как отдельный необязательный или nightly слой.
- Решить, какие большие representative graphs стоит держать отдельно от fixture-suite:
  - например, `WikiVote`.

### 3. CI Cleanup
- Обновить workflow/action-ы, чтобы убрать предупреждение про будущую deprecation `Node.js 20`.

### 4. Gallery E2E Pipelines
- Добавить отдельные workflow на `Linux`, `macOS` и `Windows` для чистого runner-а.
- Сценарий каждого workflow:
  - `Install-Module PSGraphView`
  - `Import-Module PSGraphView`
  - прогон curated набора e2e тестов
  - запись `Svg`, `Png` и `Jpg` результатов
  - проверка, что файлы существуют и не пустые
  - сравнение с эталонными output-ами
- Эти pipeline-ы должны проверять именно установленный из `PSGallery` модуль, а не локально собранный publish output.
- Эталонные output-ы нужно готовить на основе pinned native Graphviz baseline:
  - брать curated `.gv/.dot` входы
  - рендерить их нативным Graphviz
  - сохранять как эталоны для `Svg` и raster compare
- Этот contour логично запускать как отдельный post-publish или release-validation слой, а не смешивать с обычным PR gate.

### 5. Release Follow-up
- После завершения quality gate использовать уже подтвержденный `dry_run` как основу для первого controlled non-dry-run publish в `PSGallery`.
- После первого publish включить gallery-installed e2e pipeline-ы как обязательную release-validation проверку.

## Основные проверки

Обязательные проверки, которые уже должны оставаться зелеными:
- `Export-GraphvizView -As Json` работает через native path.
- `Export-GraphvizView -As Svg` не зависит от внешнего `dot`.
- `Export-GraphvizView -As Png` и `-As Jpg` создают валидные файлы.
- `Export-GraphvizView -As Png` и `-As Jpg` работают через `psd1` в обычном `pwsh`.
- Bundled runtime реально используется без системного Graphviz.
- `no-system-graphviz` smoke проходит на `Linux`, `macOS` и `Windows`.
- `core` fixture-suite проходит на `Linux`, `macOS` и `Windows`.
- Для raster path остается доступной телеметрия расхождения с оригинальным `dot`.

Дополнительные проверки, которые нужно добавить следующим слоем:
- установленный из `PSGallery` модуль успешно ставится через `Install-Module` на чистом runner-е;
- установленный из `PSGallery` модуль успешно импортируется через `Import-Module`;
- gallery-installed e2e набор успешно отдает `Svg`, `Png` и `Jpg` на `Linux`, `macOS` и `Windows`;
- gallery-installed output-ы сравниваются с pinned native Graphviz baseline и проходят agreed thresholds;
- gallery-installed output-ы не пустые и содержат ожидаемые артефакты рендера.

## Риски

- Текст и шрифты могут визуально отличаться между платформами, даже если layout уже посчитан.
- Graphviz library path нельзя считать thread-safe без отдельного доказательства.
- Не все upstream `.gv/.dot` подходят для обязательного cross-platform gate; расширять fixture-suite нужно выборочно.
- Не нужно смешивать в одну серию патчей Graphviz path и возможную миграцию `DSM`.

## Следующий шаг

Следующий практический шаг:
- ввести baseline и thresholds для raster compare;
- затем поднять `extended` fixture tier или отдельные большие integration scenario;
- потом подготовить pinned baseline-артефакты для gallery-installed e2e pipeline-ов;
- после этого готовить первый controlled publish в `PSGallery` и включать post-publish gallery-installed проверки;
- отдельно закрыть CI cleanup по `Node.js 20`.
