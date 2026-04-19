# History: Native Graphviz Path In PSGraphView

Этот файл хранит краткую хронологию этапов.
Актуальный статус и оставшиеся шаги находятся в [plan.md](/Users/andrei/repo/PSGraphView/plan.md).
Причины решений, метрики и ссылки на проверки находятся в [decisions.md](/Users/andrei/repo/PSGraphView/decisions.md).

## Этап 1. Базовый native path

- `Патч 0`: стабилизирован native test harness.
- `Патч 1`: введена scene model.
- `Патч 2a-2c`: добавлен interpreter для Graphviz draw payload и добрана основная draw coverage.
- `Патч 3`: добавлен managed `scene -> Svg`.
- `Патч 3a`: добавлена защита от возврата `Svg` path к внешнему `dot`.
- `Патч 4`: добавлен managed `scene -> Png/Jpg` на `SkiaSharp`.
- `Патч 4a`: закрыт standalone PowerShell-сценарий через `Import-Module ...psd1` для raster path.

Итог этапа:
- `Json`, `Svg`, `Png`, `Jpg` уже идут через bundled native path и managed renderer-ы.

## Этап 2. Автономность без системного Graphviz

- Добавлен общий smoke-скрипт для сценария без системного `dot`.
- Добавлен bundled-layout helper для локальной проверки publish-like layout.
- Добавлен cross-platform workflow `psgraphview-no-system-graphviz`.
- Hosted run `24070711886` подтвердил green matrix на:
  - `linux-x64`
  - `osx-arm64`
  - `win-x64`

Итог этапа:
- основной bundled path подтвержден на трех платформах без системного Graphviz.

## Этап 3. Curated fixture-suite

- Добавлен локальный fixture catalog и manifest.
- Добавлен sync-скрипт для fixture-ов из related `graphviz`.
- Добавлен runner для fixture-suite.
- Добавлен hosted workflow `psgraphview-graphviz-fixture-suite`.
- Hosted run `24273975962` подтвердил green `core` suite на:
  - `linux-x64`
  - `osx-arm64`
  - `win-x64`

Итог этапа:
- есть отдельный кроссплатформенный regression contour поверх curated Graphviz fixture-ов.

## Этап 4. Release hardening

- Усилен workflow публикации в `PSGallery`.
- По умолчанию используется bundled runtime `0.1.0-beta.12`.
- Перед публикацией добавлен bundled smoke уже на published module layout.
- Добавлен безопасный `dry_run`.
- Hosted dry-run `24291734993` завершился успешно.

Итог этапа:
- publish-контур проверен в безопасном режиме и готов к первому controlled release.

## Этап 5. Compare и метрики качества

- Добавлен SVG compare/benchmark для `WikiVote`.
- Добавлен raster compare helper:
  - `RasterImageComparer`
- Добавлен demo-скрипт:
  - `demos/Compare-WikiVote-GraphvizRaster.ps1`
- Добавлен repeatable quality gate:
  - `eng/Test-GraphvizRasterQualityGate.ps1`
  - pinned `raster-smoke.dot`
  - pinned `wiki-vote-seed20.dot`
  - threshold manifest `raster-quality-gate.json`
- Зафиксированы первые рабочие thresholds для:
  - `RMSE`
  - `different-pixel %`
  - `SSIM`

Итог этапа:
- базовый raster quality gate уже введен;
- дальше остается расширять integration contour и готовить pinned gallery baseline layer.
