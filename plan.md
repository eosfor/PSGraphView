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
- Curated `extended` fixture-suite подтвержден на `Linux`, `macOS` и `Windows`.
- Pinned native Graphviz baseline assets подготовлены для будущего gallery-installed contour.
- Representative scenario вне fixture-suite уже выбраны:
  - `wiki-vote-seed20`
  - `ngk10-4-sfdp`
- Publish workflow усилен:
  - используется bundled runtime `0.1.0-beta.12` по умолчанию;
  - перед `Publish-Module` есть bundled smoke;
  - есть безопасный `dry_run`;
  - dry-run уже подтвержден.
- Первый controlled publish уже выполнен:
  - `PSGraphView 0.1.0-beta2` опубликован в `PSGallery`.
- Gallery-installed baseline suite уже подтвержден на `Linux`, `macOS` и `Windows` через `Install-Module` и `Import-Module` с чистого runner-а.

Частично завершено:
- `Патч 5` закрыт частично:
  - есть manual compare/benchmark для `WikiVote`;
  - есть `RasterImageComparer`;
  - есть demo-скрипт для raster compare;
  - raster quality gate уже введен для pinned `WikiVote` subgraph и простого smoke graph;
  - pinned baseline layer уже добавлен;
  - gallery-installed acceptance contour уже включен и прошел первый hosted прогон на трех ОС;
  - cross-platform raster thresholds для baseline manifest еще не зафиксированы как обязательный release gate.

Осталось:
- откалибровать и зафиксировать cross-platform thresholds для gallery-installed baseline compare;
- решить, какие из этих thresholds станут обязательным release gate;
- обновить CI action-ы под будущее снятие `Node.js 20`;
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
- `Патч 5`: частично выполнен. Quality gate, pinned baseline compare и gallery-installed acceptance contour готовы, но финальные cross-platform thresholds еще не зафиксированы.
- `Патч 6`: выполнен. Автономный bundled path без системного Graphviz подтвержден.
- `Патч 6a`: выполнен. `core` fixture-suite работает в hosted CI на трех ОС.
- `Патч 6b`: выполнен. `extended` fixture-suite работает в hosted CI на трех ОС.
- `Патч 7`: backlog. Возможный отдельный follow-up по `DSM`.

## Активные задачи

### 1. Cross-Platform Baseline Calibration
- Первый hosted gallery-installed прогон уже получен:
  - `PSGallery`: `PSGraphView 0.1.0-beta2`
  - workflow: `psgraphview-gallery-installed-e2e`
  - run: `24642173313`
- Следующий шаг на этих данных:
  - снять telemetry из artifacts по `Linux`, `macOS` и `Windows`
  - определить рабочие thresholds для raster compare в baseline manifest
  - решить, какие thresholds делать обязательным release gate

### 2. CI Cleanup
- Обновить workflow/action-ы, чтобы убрать предупреждение про будущую deprecation `Node.js 20`.

### 3. Release Follow-up
- Решить, как использовать уже готовый `psgraphview-gallery-installed-e2e` дальше:
  - только как post-publish validation
  - или как обязательный release gate для каждого prerelease/stable publish
- Если следующий шаг будет релизным:
  - выбрать политику версионирования после `0.1.0-beta2`
  - определить момент для `0.1.0` stable

## Основные проверки

Обязательные проверки, которые уже должны оставаться зелеными:
- `Export-GraphvizView -As Json` работает через native path.
- `Export-GraphvizView -As Svg` не зависит от внешнего `dot`.
- `Export-GraphvizView -As Png` и `-As Jpg` создают валидные файлы.
- `Export-GraphvizView -As Png` и `-As Jpg` работают через `psd1` в обычном `pwsh`.
- Bundled runtime реально используется без системного Graphviz.
- `no-system-graphviz` smoke проходит на `Linux`, `macOS` и `Windows`.
- `core` fixture-suite проходит на `Linux`, `macOS` и `Windows`.
- gallery-installed baseline suite проходит на `Linux`, `macOS` и `Windows` через `Install-Module` и `Import-Module`.
- Для raster path остается доступной телеметрия расхождения с оригинальным `dot`.

Проверки, уже добавленные для gallery-installed baseline layer:
- установленный из `PSGallery` модуль успешно ставится через `Install-Module` на чистом runner-е;
- установленный из `PSGallery` модуль успешно импортируется через `Import-Module`;
- gallery-installed e2e набор успешно отдает `Svg`, `Png` и `Jpg` на `Linux`, `macOS` и `Windows`;
- gallery-installed output-ы сравниваются с pinned native Graphviz baseline;
- gallery-installed output-ы не пустые и содержат ожидаемые артефакты рендера.
- Следующий слой поверх этого:
  - зафиксировать agreed thresholds для hosted cross-platform compare telemetry.

Проверки, уже добавленные для raster quality gate:
- `eng/Test-GraphvizRasterQualityGate.ps1` сравнивает native `Png/Jpg` с нативным Graphviz baseline;
- gate использует pinned DOT-входы:
  - `tests/Fixtures/Graphviz/quality/raster-smoke.dot`
  - `tests/Fixtures/Graphviz/quality/wiki-vote-seed20.dot`
- текущие thresholds зафиксированы в:
  - `tests/Fixtures/Graphviz/quality/raster-quality-gate.json`

Проверки, уже подготовленные для gallery-installed baseline layer:
- `eng/Export-GraphvizBaselineAssets.ps1` генерирует pinned native Graphviz `Svg/Png/Jpg` baseline-артефакты;
- `eng/Test-GraphvizBaselineSuite.ps1` умеет сравнивать локальный или установленный модуль с pinned baseline-набором;
- baseline manifest сейчас включает:
  - `clust1`
  - `records`
  - `arrows`
  - `table`
  - `wiki-vote-seed20`
  - `ngk10-4-sfdp`

## Риски

- Текст и шрифты могут визуально отличаться между платформами, даже если layout уже посчитан.
- Graphviz library path нельзя считать thread-safe без отдельного доказательства.
- Не все upstream `.gv/.dot` подходят для обязательного cross-platform gate; расширять fixture-suite нужно выборочно.
- Не нужно смешивать в одну серию патчей Graphviz path и возможную миграцию `DSM`.

## Следующий шаг

Следующий практический шаг:
- снять и разобрать telemetry из artifacts gallery-installed run `24642173313`;
- по этим данным зафиксировать cross-platform thresholds в baseline manifest;
- после этого решить, делать ли `psgraphview-gallery-installed-e2e` обязательным release gate;
- отдельно закрыть CI cleanup по `Node.js 20`.
