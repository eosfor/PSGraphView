# Architecture Decision Log

## 2026-04-19 15:09:05 PDT

Решение:
Подготовить pinned native Graphviz baseline layer до первого `PSGallery` publish и выбрать два отдельных representative scenario вне fixture-suite:
- `wiki-vote-seed20`
- `ngk10-4-sfdp`

Причины:
- Для будущего post-publish сценария через `Install-Module` нужны не только входные `.gv/.dot`, но и уже зафиксированные эталонные `Svg/Png/Jpg`.
- Генерировать baseline внутри gallery-installed workflow невыгодно:
  - это потребует системного Graphviz на чистом runner-е;
  - такой contour перестанет проверять именно автономный installed-module path.
- Значит baseline нужно хранить в репозитории и уметь воспроизводимо пересобирать отдельным скриптом.
- Помимо малых fixture-ов нужен хотя бы один-два более крупных representative scenario, чтобы post-publish contour не ограничивался только базовыми примерами.
- `wiki-vote-seed20` уже доказал свою полезность в raster quality gate.
- `ngk10_4.gv` из graphviz corpus добавляет отдельный network-style сценарий под `sfdp`, ближе к force/network layout, чем обычные `dot`-fixture-ы.

Телеметрия / наблюдения:
- Добавлены baseline inputs и tooling:
  - [tests/Baselines/Graphviz/manifest.json](/Users/andrei/repo/PSGraphView/tests/Baselines/Graphviz/manifest.json)
  - [eng/Export-GraphvizBaselineAssets.ps1](/Users/andrei/repo/PSGraphView/eng/Export-GraphvizBaselineAssets.ps1)
  - [eng/Test-GraphvizBaselineSuite.ps1](/Users/andrei/repo/PSGraphView/eng/Test-GraphvizBaselineSuite.ps1)
  - [tests/Fixtures/Graphviz/representative/ngk10_4.gv](/Users/andrei/repo/PSGraphView/tests/Fixtures/Graphviz/representative/ngk10_4.gv)
- Baseline manifest сейчас включает `6` сценариев:
  - `clust1`
  - `records`
  - `arrows`
  - `table`
  - `wiki-vote-seed20`
  - `ngk10-4-sfdp`
- Локальная генерация baseline:
  - summary:
    - `/Users/andrei/repo/PSGraphView/tests/Baselines/Graphviz/baseline-summary.json`
  - итог:
    - `scenarioCount=6`
    - `durationMs=1508.59`
    - `dotCommandPath=/opt/homebrew/bin/dot`
- Локальный compare-suite against current module:
  - summary:
    - `/Users/andrei/repo/PSGraphView/artifacts/local-baseline-suite/baseline-suite-results.json`
  - итог:
    - `scenarioCount=6`
    - `failureCount=0`
    - `durationMs=5725.65`
- Representative raster telemetry:
  - `wiki-vote-seed20 / png`:
    - `rmse=46.537`
    - `diff%=13.3837`
    - `ssim=0.366526`
  - `wiki-vote-seed20 / jpg`:
    - `rmse=44.1982`
    - `diff%=13.9694`
    - `ssim=0.389072`
  - `ngk10-4-sfdp / png`:
    - `rmse=48.5329`
    - `diff%=12.455`
    - `ssim=0.077591`
  - `ngk10-4-sfdp / jpg`:
    - `rmse=47.64`
    - `diff%=12.5054`
    - `ssim=0.080927`
- Малые fixture-ы тоже уже промерены:
  - `clust1 / png`: `rmse=53.4261`, `diff%=18.5185`, `ssim=0.34222`
  - `records / png`: `rmse=54.4717`, `diff%=15.0149`, `ssim=0.288235`
  - `arrows / png`: `rmse=78.6472`, `diff%=36.7504`, `ssim=0.306772`
  - `table / png`: `rmse=59.4827`, `diff%=19.9823`, `ssim=0.592146`
- Важное наблюдение:
  - локальная compare telemetry уже полезна для калибровки,
  - но фиксировать жесткие cross-platform thresholds в baseline manifest пока рано:
    - сначала нужен хотя бы один gallery-installed hosted прогон на `Linux`, `macOS` и `Windows`.

Следствие:
- baseline layer можно считать подготовленной базой для post-publish `Install-Module` contour.
- Следующий практический шаг уже не про генерацию baseline, а про:
  - первый controlled non-dry-run publish в `PSGallery`
  - подключение `eng/Test-GraphvizBaselineSuite.ps1` в gallery-installed workflow
  - калибровку cross-platform thresholds по telemetry первого hosted gallery run

## 2026-04-19 14:50:19 PDT

Решение:
Считать hosted cross-platform fixture coverage закрытой не только для `core`, но и для `extended`: оба режима уже подтверждены реальными `GitHub Actions` run-ами на этой ветке.

Причины:
- До этого `extended` был подтвержден локально и описан как nightly/manual слой, но hosted run на текущей ветке еще не был зафиксирован в decision log.
- После зеленого ручного `workflow_dispatch` прогона этот этап больше не является следующим шагом по плану.
- Значит активный фокус можно сместить с самого fixture contour на baseline-артефакты и будущие gallery-installed e2e проверки.

Телеметрия / наблюдения:
- Push-triggered `core` run на ветке:
  - workflow: [graphviz-fixture-suite.yml](/Users/andrei/repo/PSGraphView/.github/workflows/graphviz-fixture-suite.yml)
  - run: `24639546416`
  - ссылка: [GitHub Actions run 24639546416](https://github.com/eosfor/PSGraphView/actions/runs/24639546416)
  - итог:
    - `ubuntu-24.04 / linux-x64`: `success`
    - `macos-14 / osx-arm64`: `success`
    - `windows-2022 / win-x64`: `success`
- Manual `extended` run на той же ветке:
  - workflow: [graphviz-fixture-suite.yml](/Users/andrei/repo/PSGraphView/.github/workflows/graphviz-fixture-suite.yml)
  - run: `24639548864`
  - ссылка: [GitHub Actions run 24639548864](https://github.com/eosfor/PSGraphView/actions/runs/24639548864)
  - итог:
    - `ubuntu-24.04 / linux-x64`: `success`
    - `macos-14 / osx-arm64`: `success`
    - `windows-2022 / win-x64`: `success`
- Во всех job прошли шаги:
  - `Restore PowerShell module`
  - `Publish module with bundled Graphviz runtime`
  - `Run Graphviz fixture suite`
  - `Upload fixture artifacts`
- Наблюдение по CI осталось прежним:
  - GitHub продолжает показывать warning про будущую deprecation `Node.js 20` для standard actions.

Следствие:
- `extended` fixture coverage можно считать закрытой на hosted CI уровне.
- Следующий шаг теперь уже не про fixture workflow, а про:
  - pinned baseline assets
  - representative large graph scenarios
  - gallery-installed e2e pipeline-ы
  - controlled publish из `PSGallery`

## 2026-04-19 13:54:06 PDT

Решение:
Поднять `extended` integration coverage как отдельный nightly/manual слой в уже существующем fixture workflow, не смешивая его с быстрым `core` PR gate.

Причины:
- `core` уже доказал свою полезность как короткий обязательный regression contour.
- `extended` нужен, чтобы проверять более сложные входы, но его невыгодно вешать на каждый push и каждый PR.
- Дублировать второй почти такой же workflow ради `extended` не нужно:
  - логика publish и runner уже есть;
  - достаточно дать тому же workflow отдельный режим для schedule/manual запуска.
- Такой вариант дает больше покрытия без лишнего шума и без удлинения основного PR path.

Телеметрия / наблюдения:
- Локальный `extended` прогон уже подтвержден:
  - summary:
    - `/Users/andrei/repo/PSGraphView/artifacts/local-graphviz-fixture-suite-extended/fixture-suite-results.json`
  - итог:
    - `totalFixtureCount=4`
    - `failureCount=0`
    - `successCount=4`
    - `durationMs=2103.3`
- Пройденные fixture-ы:
  - `states`
  - `table`
  - `heawood`
  - `structs`
- Обновлен workflow:
  - [graphviz-fixture-suite.yml](/Users/andrei/repo/PSGraphView/.github/workflows/graphviz-fixture-suite.yml)
  - что изменено:
    - добавлен `schedule`
    - `schedule` запускает tier `extended`
    - `workflow_dispatch` по-прежнему может запускать `core`, `extended` или `all`
    - artifact name теперь включает tier для лучшей читаемости

Следствие:
- `extended` integration coverage можно считать поднятым на уровне CI-контура.
- Следующий шаг уже не про fixture tiers, а про:
  - выбор больших representative graph scenario вне fixture-suite
  - подготовку pinned baseline assets для gallery-installed e2e

## 2026-04-19 13:21:14 PDT

Решение:
Считать базовый raster quality gate введенным: thresholds для `RMSE`, `different-pixel %` и `SSIM` зафиксированы в репозитории, а repeatable regression-step оформлен отдельным `eng`-скриптом поверх pinned DOT-входов.

Причины:
- До этого raster compare уже существовал как manual инструмент, но не как реальный gate.
- Без thresholds compare не мог отличать приемлемое текущее расхождение от реального регресса.
- Для первого рабочего quality gate нужен был небольшой, но representative набор:
  - один простой smoke graph
  - один pinned `WikiVote` subgraph
- Эти два сценария дают полезное покрытие:
  - маленький граф с простыми label/arrow path
  - более крупный реальный граф с большим количеством узлов и ребер
- Gate должен был валить именно native raster path, а не случайно уходить во внешний `dot`, поэтому script специально ломает `PSGRAPHVIEW_GRAPHVIZ_DOT_PATH` для candidate render-а и использует `dot` только как explicit baseline renderer.

Телеметрия / наблюдения:
- Добавлены pinned inputs:
  - [raster-smoke.dot](/Users/andrei/repo/PSGraphView/tests/Fixtures/Graphviz/quality/raster-smoke.dot)
  - [wiki-vote-seed20.dot](/Users/andrei/repo/PSGraphView/tests/Fixtures/Graphviz/quality/wiki-vote-seed20.dot)
- Добавлен threshold manifest:
  - [raster-quality-gate.json](/Users/andrei/repo/PSGraphView/tests/Fixtures/Graphviz/quality/raster-quality-gate.json)
- Добавлен repeatable gate script:
  - [Test-GraphvizRasterQualityGate.ps1](/Users/andrei/repo/PSGraphView/eng/Test-GraphvizRasterQualityGate.ps1)
- Локальная проверка gate:
  - summary:
    - `/Users/andrei/repo/PSGraphView/artifacts/local-raster-quality-gate/raster-quality-gate-results.json`
  - итог:
    - `scenarioCount=2`
    - `failureCount=0`
    - `succeeded=true`
    - `durationMs=1415.78`
- Зафиксированные текущие метрики и thresholds:
  - `smoke-basic / png`:
    - observed: `rmse=57.4447`, `diff%=23.4077`, `ssim=0.493517`
    - threshold: `rmse<=65.0`, `diff%<=25.0`, `ssim>=0.40`
  - `smoke-basic / jpg`:
    - observed: `rmse=56.1938`, `diff%=23.8140`, `ssim=0.488233`
    - threshold: `rmse<=62.0`, `diff%<=25.0`, `ssim>=0.40`
  - `wiki-vote-seed20 / png`:
    - observed: `rmse=31.6704`, `diff%=17.0028`, `ssim=0.568433`
    - threshold: `rmse<=38.0`, `diff%<=20.0`, `ssim>=0.52`
  - `wiki-vote-seed20 / jpg`:
    - observed: `rmse=38.2482`, `diff%=15.3318`, `ssim=0.459697`
    - threshold: `rmse<=45.0`, `diff%<=18.0`, `ssim>=0.43`
- Дополнительная узкая проверка:
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --filter RasterImageComparerTests --no-restore`
  - итог: `4 passed`

Следствие:
- Active task `Raster Quality Gate` можно считать закрытой на базовом уровне.
- Следующий шаг теперь уже не про сами thresholds, а про:
  - `extended` integration coverage
  - pinned baseline assets для gallery-installed e2e pipeline-ов
  - последующий post-publish contour через `Install-Module`

## 2026-04-19 13:07:54 PDT

Решение:
Добавить в план отдельный слой post-publish e2e pipeline-ов на чистых runner-ах, который проверяет реальный пользовательский сценарий через `Install-Module` из `PSGallery`, а не только source-built publish output.

Причины:
- Текущие `no-system-graphviz` и fixture workflow уже хорошо проверяют код и bundled publish layout, но они стартуют из исходников этого репозитория.
- Это не то же самое, что реальная установка модуля пользователем:
  - `Install-Module`
  - `Import-Module`
  - использование уже опубликованного пакета на пустом агенте
- Для `PSGraphView` это особенно важно из-за native runtime и raster path:
  - packaging может быть корректным в локальном `dotnet publish`, но сломаться в реальном gallery package
  - import path и native dependency resolution нужно проверять именно после установки из `PSGallery`
- Проверка только на факт появления файлов недостаточна; нужен еще compare с эталонными output-ами.
- Эталоны логично строить не вручную, а на основе pinned native Graphviz baseline для curated `.gv/.dot` входов.

Телеметрия / наблюдения:
- Это пока плановый шаг; нового hosted run для него еще нет.
- Уже существующие слои, на которые он будет опираться:
  - [no-system-graphviz.yml](/Users/andrei/repo/PSGraphView/.github/workflows/no-system-graphviz.yml)
  - [graphviz-fixture-suite.yml](/Users/andrei/repo/PSGraphView/.github/workflows/graphviz-fixture-suite.yml)
  - [publish.yml](/Users/andrei/repo/PSGraphView/.github/workflows/publish.yml)
- Уже существующая база для compare:
  - [RasterImageComparer.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/RasterImageComparer.cs)
  - [Compare-WikiVote-GraphvizRaster.ps1](/Users/andrei/repo/PSGraphView/demos/Compare-WikiVote-GraphvizRaster.ps1)
  - [tests/Fixtures/Graphviz/manifest.json](/Users/andrei/repo/PSGraphView/tests/Fixtures/Graphviz/manifest.json)
- Предполагаемый сценарий pipeline:
  - чистый runner
  - `Install-Module PSGraphView`
  - `Import-Module PSGraphView`
  - прогон curated e2e fixture-набора
  - сохранение `Svg/Png/Jpg`
  - compare с pinned native Graphviz baseline

Следствие:
- Новый слой проверки не должен заменять текущие source-built workflow.
- Его нужно держать отдельно как post-publish или release-validation contour.
- Перед внедрением этого слоя нужно:
  - определить pinned baseline inputs и output-ы
  - зафиксировать thresholds для compare
  - провести первый controlled non-dry-run publish в `PSGallery`

## 2026-04-11 14:14:58 PDT

Решение:
Считать publish dry-run подтвержденным: `PSGallery` publish workflow теперь проверен реальным `workflow_dispatch` прогоном, который проходит весь release contour до `Publish-Module`, но не публикует модуль наружу.

Причины:
- После добавления `dry_run` режим был нужен не как абстрактная опция в YAML, а как реально подтвержденный safe path перед первым настоящим publish.
- Для `PSGraphView` особенно важно было проверить не только build и tests, но и:
  - `dotnet publish`
  - обновление manifest в publish output
  - bundled smoke через `PSGraphView.psd1`
  - корректный skip шага `Publish-Module`
- Это и есть минимально достаточная репетиция релиза без внешнего side effect.

Телеметрия / наблюдения:
- Workflow:
  - [publish.yml](/Users/andrei/repo/PSGraphView/.github/workflows/publish.yml)
  - run: `24291734993`
  - ссылка: [GitHub Actions run 24291734993](https://github.com/eosfor/PSGraphView/actions/runs/24291734993)
- Параметры dry-run:
  - `version=0.1.0`
  - `prerelease=beta1`
  - `graphviz_runtime_version=0.1.0-beta.12`
  - `dry_run=true`
- Итог:
  - job `publish (9.0.x)` -> `success`
  - длительность: `1m0s`
  - `Publish PowerShell module to PSGallery` был пропущен
  - опубликован artifact:
    - `psgraphview-publish-dry-run`
- Подтвержденные шаги внутри run:
  - `Restore dependencies`
  - `Build`
  - `Run tests`
  - `Publish PowerShell module`
  - `Update module manifest version`
  - `Smoke published module bundle`
  - `Upload dry-run artifacts`
  - `Skip PSGallery publish in dry-run`
- Наблюдения по warning-ам:
  - GitHub снова показал warning про будущую deprecation `Node.js 20` для standard actions
  - build/tests прошли с уже существующими C# warning-ами по nullability и unused event
  - эти warning-и не заблокировали dry-run, но остались техническим долгом

Следствие:
- Publish pipeline теперь можно считать не просто настроенным, а реально проверенным в безопасном режиме.
- Следующий release-engineering шаг уже предельно конкретный:
  - controlled non-dry-run publish в `PSGallery`
- Это отдельный шаг от основного Graphviz-плана; основной план по-прежнему должен идти дальше в сторону raster thresholds и `extended` integration contour.

## 2026-04-11 12:09:54 PDT

Решение:
Усилить `PSGallery` publish workflow минимальным, но практическим патчем:
- перевести bundled Graphviz runtime по умолчанию на актуальный `0.1.0-beta.12`
- после `dotnet publish` и обновления manifest запускать узкий bundled smoke
- явно проверять, что итоговый manifest в publish output содержит ожидаемые `ModuleVersion` и `Prerelease`

Причины:
- Текущий publish workflow уже умел публиковать модуль, но был слабее `PSGraph` по защищенности от release-сюрпризов.
- Самый заметный технический риск был в устаревшем default runtime:
  - workflow по умолчанию все еще ссылался на `0.1.0-beta.3`
  - при этом реальная кроссплатформенная проверка уже давно подтверждена на `0.1.0-beta.12`
- Для `PSGraphView` одной сборки перед `Publish-Module` недостаточно:
  - publish output включает bundled `libpsgv`
  - значит перед отправкой в `PSGallery` полезно проверить именно уже опубликованный layout через `psd1` import path
- Проверка итогового manifest после `Update-ModuleManifest` снижает риск тихого расхождения между вычисленной версией release и тем, что реально попадет в галерею.

Телеметрия / наблюдения:
- Обновлен workflow:
  - [publish.yml](/Users/andrei/repo/PSGraphView/.github/workflows/publish.yml)
- Что изменено:
  - default `graphviz_runtime_version`:
    - было: `0.1.0-beta.3`
    - стало: `0.1.0-beta.12`
  - fallback `graphviz_runtime_version` внутри shell step:
    - было: `0.1.0-beta.3`
    - стало: `0.1.0-beta.12`
  - после `Update-ModuleManifest` workflow теперь явно проверяет:
    - `ModuleVersion`
    - `PrivateData.PSData.Prerelease`
  - перед `Publish-Module` добавлен шаг:
    - `Smoke published module bundle`
    - использует [Test-GraphvizNoSystemSmoke.ps1](/Users/andrei/repo/PSGraphView/eng/Test-GraphvizNoSystemSmoke.ps1)
    - проверяет published module через `PSGraphView.psd1` и `-RequireBundledGraphvizRuntime`
- Локальная проверка workflow syntax:
  - `ruby -e 'require "yaml"; YAML.load_file(".../publish.yml"); puts "YAML OK"'`
  - результат ожидается как достаточная узкая проверка для workflow-only патча

Следствие:
- Publish pipeline в `PSGraphView` по-прежнему проще, чем в `PSGraph`, но теперь он лучше защищен именно на тех местах, где у `PSGraphView` есть свой дополнительный риск: bundled native runtime.
- Следующий естественный шаг уже не про базовую publish-механику, а про реальный dry-run или первый controlled release прогон.

## 2026-04-11 12:02:45 PDT

Решение:
Считать `Патч 6a` закрытым: curated `core` fixture-suite уже подтвержден не только локально, но и реальным hosted `GitHub Actions` matrix run на трех платформах.

Причины:
- До этого момента fixture-suite был реализован по коду, но его еще не было смысла считать завершенным, пока не пройдет настоящий hosted прогон с bundled runtime.
- Первый push-triggered run уже дал именно эту проверку:
  - тот же publish path, что будет у пользователя в CI
  - тот же `psd1` import path
  - тот же bundled `libpsgv`
  - три реальные платформы
- Значит этап “подготовить curated cross-platform regression contour” по основному сценарию можно считать закрытым.

Телеметрия / наблюдения:
- Hosted workflow:
  - [graphviz-fixture-suite.yml](/Users/andrei/repo/PSGraphView/.github/workflows/graphviz-fixture-suite.yml)
  - run: `24273975962`
  - ссылка: [GitHub Actions run 24273975962](https://github.com/eosfor/PSGraphView/actions/runs/24273975962)
- Итог matrix:
  - `macos-14 / osx-arm64`: `success`, `36s`
  - `ubuntu-24.04 / linux-x64`: `success`, `41s`
  - `windows-2022 / win-x64`: `success`, `4m12s`
- Артефакты опубликованы для всех трех платформ:
  - `graphviz-fixture-suite-osx-arm64`
  - `graphviz-fixture-suite-linux-x64`
  - `graphviz-fixture-suite-win-x64`
- В каждом artifact есть:
  - `fixture-suite-results.json`
  - per-fixture `graph.json`
  - per-fixture `graph.svg`
  - per-fixture `graph.png`
  - per-fixture `graph.jpg`
  - per-fixture `smoke.log`
  - per-fixture `smoke-results.json`
- Дополнительное наблюдение по CI:
  - GitHub показал предупреждение про будущую deprecation `Node.js 20` для:
    - `actions/checkout@v4`
    - `actions/setup-dotnet@v4`
    - `actions/upload-artifact@v4`
  - это не сломало текущий прогон, но это уже отдельный небольшой CI follow-up, а не blocker по Graphviz path

Следствие:
- Следующий шаг теперь уже не про саму кроссплатформенную работоспособность.
- Новый практический фокус:
  - thresholds и regression-step для raster compare
  - `extended` tier и/или большие representative graph scenario
  - мелкий CI follow-up по Node 20 warning
- Предыдущая запись от `2026-04-09 22:57:03 PDT`, где hosted run еще считался будущим шагом, теперь исторически верна, но уже устарела по статусу.

## 2026-04-09 22:57:03 PDT

Решение:
Вынести `core` fixture-suite в отдельный `GitHub Actions` workflow, а не встраивать его в уже существующий `no-system-graphviz` smoke workflow.

Причины:
- `no-system-graphviz` уже выполняет роль короткого smoke на одной строке DOT.
- Fixture-suite по природе тяжелее:
  - несколько входов
  - per-fixture артефакты
  - отдельная suite summary
- Значит смешивать его с базовым smoke невыгодно:
  - сложнее читать падения
  - сложнее управлять tier-ами `core/extended`
  - обычный быстрый smoke теряет простоту

Телеметрия / наблюдения:
- Добавлен workflow:
  - [graphviz-fixture-suite.yml](/Users/andrei/repo/PSGraphView/.github/workflows/graphviz-fixture-suite.yml)
- Workflow использует:
  - тот же publish path с bundled runtime
  - тот же matrix:
    - `ubuntu-24.04 / linux-x64`
    - `windows-2022 / win-x64`
    - `macos-14 / osx-arm64`
- Workflow вызывает:
  - [Invoke-GraphvizFixtureSuite.ps1](/Users/andrei/repo/PSGraphView/eng/Invoke-GraphvizFixtureSuite.ps1)
  - с `-RequireBundledGraphvizRuntime`
- Локальная синтаксическая проверка YAML пройдена:
  - `ruby -e 'require "yaml"; YAML.load_file(".../graphviz-fixture-suite.yml"); puts "YAML OK"'`
  - результат: `YAML OK`
- README обновлен:
  - [README.md](/Users/andrei/repo/PSGraphView/README.md)

Следствие:
- Следующий шаг теперь уже внешний:
  - первый реальный `GitHub Actions` прогон нового workflow
  - разбор platform-specific падений, если они появятся
- До этого момента `Патч 6a` можно считать реализованным по коду, но еще не подтвержденным в hosted CI.

## 2026-04-09 22:54:15 PDT

Решение:
Сделать fixture runner отдельным `eng`-скриптом поверх уже существующего `Test-GraphvizNoSystemSmoke.ps1`, а не дублировать format-specific проверки заново.

Причины:
- В `Test-GraphvizNoSystemSmoke.ps1` уже была правильная базовая проверка нужного нам пользовательского сценария:
  - `Import-Module ...psd1`
  - сломанный внешний `dot`
  - прогон `Json|Svg|Png|Jpg`
- Значит новый runner должен был заниматься только orchestration:
  - пройти по manifest
  - запускать smoke по каждому fixture-у
  - собирать per-fixture summary и артефакты
- Такой подход уменьшает риск расхождения между одиночным smoke и fixture-suite.

Телеметрия / наблюдения:
- Добавлен runner:
  - [Invoke-GraphvizFixtureSuite.ps1](/Users/andrei/repo/PSGraphView/eng/Invoke-GraphvizFixtureSuite.ps1)
- Runner делает:
  - tier/filter selection по `manifest.json`
  - отдельный `pwsh -NoProfile` запуск smoke на каждый fixture
  - `smoke.log` и `smoke-results.json` на каждый fixture
  - общий summary:
    - `fixture-suite-results.json`
- Локальная проверка пройдена:
  - `dotnet build src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj`
  - `pwsh -NoLogo -NoProfile -File ./eng/Invoke-GraphvizFixtureSuite.ps1 -ModuleManifestPath ./src/PSGraphView.PowerShell/bin/Debug/net9.0/PSGraphView.psd1 -GraphvizNativeLibraryPath /var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/psgraphview-graphviz-native/libpsgv.dylib -Tier core -OutputDirectory ./artifacts/local-graphviz-fixture-suite-core`
  - итог:
    - `8/8` fixture-ов `core` прошли
    - summary: `/Users/andrei/repo/PSGraphView/artifacts/local-graphviz-fixture-suite-core/fixture-suite-results.json`
- README обновлен:
  - [README.md](/Users/andrei/repo/PSGraphView/README.md)

Следствие:
- Следующий шаг уже не про локальную orchestration-логику.
- Следующий патч этого этапа:
  - новый `GitHub Actions` workflow для `core` fixture-suite
  - reuse того же runner-а на `Linux`, `Windows`, `macOS`
  - загрузка suite summary и per-fixture артефактов при падении

## 2026-04-09 22:53:45 PDT

Решение:
Оставить `records.gv` в `core` fixture-наборе и считать пустые text draw-команды допустимым Graphviz-сценарием, который должен проходить через managed scene path без исключения.

Причины:
- Первый локальный прогон `core` fixture-suite показал, что `records.gv` падает не из-за runner-а, а из-за слишком строгой валидации `TextCommand`.
- Для record labels Graphviz может отдавать `T`-операции с пустым `text`, например для пустых ячеек.
- Понижать такой fixture из `core` в `extended` было бы неверно:
  - `record`-формы уже входят в целевой coverage
  - ошибка была в нашем scene model, а не в случайности upstream sample

Телеметрия / наблюдения:
- Первый failing run:
  - fixture: `records`
  - log: `/Users/andrei/repo/PSGraphView/artifacts/local-graphviz-fixture-suite-core/records/smoke.log`
  - ошибка:
    - `Text is required. (Parameter 'text')`
- Корень проблемы:
  - [SceneCommands.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/SceneCommands.cs)
  - `TextCommand` запрещал пустую строку через `string.IsNullOrWhiteSpace(text)`
- Regression test добавлен в:
  - [GraphvizXdotJsonSceneInterpreterTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizXdotJsonSceneInterpreterTests.cs)
  - локально пройдено:
    - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --filter GraphvizXdotJsonSceneInterpreterTests`
    - результат: `7 passed`
- После фикса локальный `core` suite прошел полностью:
  - summary: `/Users/andrei/repo/PSGraphView/artifacts/local-graphviz-fixture-suite-core/fixture-suite-results.json`

Следствие:
- Scene model должен сохранять `""` как корректное значение текста.
- `records.gv` остается в `core` наборе и продолжает быть полезным regression fixture-ом.

## 2026-04-09 22:47:56 PDT

Решение:
Начать fixture-suite этап с локального manifest-а и sync-скрипта, а не сразу с runner-а или workflow.

Причины:
- Без локально зафиксированного набора входов runner и CI были бы завязаны либо на соседний checkout `graphviz`, либо на неявный список файлов в коде.
- Manifest нужен как единый источник правды:
  - какие upstream файлы входят в `core`
  - какие входят в `extended`
  - каким renderer-ом их предполагается гонять
- Sync-скрипт делает curated набор воспроизводимым:
  - можно быстро обновить локальный fixture-каталог из related `graphviz`
  - можно явно увидеть diff, если upstream sample изменился

Телеметрия / наблюдения:
- Добавлен manifest:
  - [manifest.json](/Users/andrei/repo/PSGraphView/tests/Fixtures/Graphviz/manifest.json)
- Добавлен sync-скрипт:
  - [Sync-GraphvizFixtures.ps1](/Users/andrei/repo/PSGraphView/eng/Sync-GraphvizFixtures.ps1)
- В первый curated набор вошли:
  - `core`: `8` fixture-ов
  - `extended`: `4` fixture-а
- Локальная синхронизация пройдена:
  - `pwsh -NoLogo -NoProfile -File ./eng/Sync-GraphvizFixtures.ps1 -GraphvizSourceRoot /Users/andrei/.codex/worktrees/f5d8/graphviz -Clean -Tier all`
  - результат:
    - `core/clust1.gv`
    - `core/clust2.gv`
    - `core/records.gv`
    - `core/record2.gv`
    - `core/arrows.gv`
    - `core/fsm.gv`
    - `core/Petersen.gv`
    - `core/poly.dot`
    - `extended/states.gv`
    - `extended/table.gv`
    - `extended/Heawood.gv`
    - `extended/structs.dot`
- README обновлен:
  - [README.md](/Users/andrei/repo/PSGraphView/README.md)

Следствие:
- Следующий патч этого этапа должен уже строиться поверх локального каталога `tests/Fixtures/Graphviz`, а не читать исходники напрямую из related repo.
- Следующий практический шаг:
  - единый runner, который проходит по manifest
  - рендерит `Json|Svg|Png|Jpg`
  - пишет per-fixture summary и артефакты для будущего workflow

## 2026-04-09 22:45:14 PDT

Решение:
Следующим этапом после уже зеленого `no-system-graphviz` smoke сделать curated cross-platform fixture-suite на базе существующих `.gv/.dot` из related `graphviz` repo, а не пытаться сразу прогонять весь upstream набор без отбора.

Причины:
- Текущий smoke уже подтверждает основной пользовательский сценарий, но он слишком маленький, чтобы уверенно ловить регрессии в более широком наборе Graphviz-примитивов.
- При этом "просто взять все `.gv/.dot` из graphviz" для обязательного PR-gate было бы слишком шумно:
  - часть входов завязана на шрифты, locale и platform-specific text layout
  - часть входов использует внешние image asset-ы
  - часть входов просто слишком тяжела для короткого обязательного CI
- Поэтому нужен curated набор с явным manifest:
  - `core` для обязательного быстрого прогона на трех ОС
  - `extended` для ручного или nightly прогона
- Fixture-ы лучше синхронизировать в `PSGraphView`, а не читать из соседнего checkout в CI:
  - так workflow остается самодостаточным
  - входы версиионируются вместе с потребляющими их тестами и runner-ами

Телеметрия / наблюдения:
- Проверенный текущий CI baseline:
  - [no-system-graphviz.yml](/Users/andrei/repo/PSGraphView/.github/workflows/no-system-graphviz.yml)
  - matrix:
    - `ubuntu-24.04 / linux-x64`
    - `windows-2022 / win-x64`
    - `macos-14 / osx-arm64`
- Просмотренный upstream pool `.gv/.dot` в related repo:
  - [graphviz](/Users/andrei/.codex/worktrees/f5d8/graphviz)
  - примеры подходящих кандидатов:
    - [clust1.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/clust1.gv)
    - [clust2.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/clust2.gv)
    - [records.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/records.gv)
    - [record2.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/record2.gv)
    - [arrows.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/arrows.gv)
    - [fsm.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/fsm.gv)
    - [Petersen.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/undirected/Petersen.gv)
    - [poly.dot](/Users/andrei/.codex/worktrees/f5d8/graphviz/doc/dotguide/poly.dot)
- Примеры, которые пока решено не включать в обязательный `core`:
  - [japanese.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/japanese.gv)
  - [russian.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/russian.gv)
  - [Latin1.gv](/Users/andrei/.codex/worktrees/f5d8/graphviz/graphs/directed/Latin1.gv)
  - [usershape.dot](/Users/andrei/.codex/worktrees/f5d8/graphviz/tests/usershape.dot)

Следствие:
- Первый патч этого этапа должен быть не про workflow, а про данные:
  - manifest выбранных fixture-ов
  - sync-скрипт из related `graphviz`
  - локальный fixture-каталог в этом репозитории
- Только после этого имеет смысл добавлять единый runner и новый fixture workflow в `GitHub Actions`.

## 2026-04-09 22:04:27 PDT

Решение:
Перевести raster compare из идеи в рабочий manual step: добавить metric helper в `PSGraphView.Graphviz`, отдельный demo-скрипт для `WikiVote` raster compare и начать собирать телеметрию для последующих thresholds.

Причины:
- После закрытия native path и cross-platform no-system smoke главный оставшийся вопрос уже не "работает ли путь вообще", а "насколько managed raster расходится с оригинальным `dot`".
- Для этого мало визуального сравнения: нужен повторяемый численный compare-step, который можно запускать на одном и том же DOT входе.
- Отдельный helper внутри `PSGraphView.Graphviz` удобнее, чем внешний tool-chain:
  - не нужен ImageMagick или другой внешний diff tool
  - одни и те же метрики доступны и из tests, и из demo-script

Телеметрия / наблюдения:
- Добавлен helper:
  - [RasterImageComparer.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/RasterImageComparer.cs)
  - текущие метрики:
    - `MeanAbsoluteDifference`
    - `RootMeanSquareDifference`
    - `MaxAbsoluteDifference`
    - `DifferentPixelRatio`
    - `GlobalStructuralSimilarity`
- Добавлены tests:
  - [RasterImageComparerTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/RasterImageComparerTests.cs)
  - локально пройдено:
    - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --filter RasterImageComparerTests --no-restore`
    - результат: `4 passed`
    - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --filter GraphSceneRasterRendererTests --no-restore`
    - результат: `2 passed`
- Добавлен demo-script:
  - [Compare-WikiVote-GraphvizRaster.ps1](/Users/andrei/repo/PSGraphView/demos/Compare-WikiVote-GraphvizRaster.ps1)
- README обновлен:
  - [README.md](/Users/andrei/repo/PSGraphView/README.md)
- Smoke-проверка на простом DOT через локальный `libpsgv.dylib`:
  - `PNG`: `rmse=48.0042`, `diff%=19.6615`, `ssim=0.501966`
  - `JPG`: `rmse=44.2855`, `diff%=19.8103`, `ssim=0.559741`
  - compare JSON:
    - `/var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/tmp.DQmhNvKZX3/out/wiki-vote-raster-compare.json`
- Первый реальный `WikiVote` subgraph compare:
  - параметры:
    - `SubgraphSeedCount=20`
    - `MaxComparisonDimension=1024`
  - graph telemetry:
    - `FullGraphVertexCount=7115`
    - `FullGraphEdgeCount=103689`
    - `SubgraphVertexCount=20`
    - `SubgraphEdgeCount=103`
  - `PNG`: `rmse=31.6704`, `diff%=17.0028`, `ssim=0.568433`
  - `JPG`: `rmse=38.2482`, `diff%=15.3318`, `ssim=0.459697`
  - compare JSON:
    - `/var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/psgv-raster-compare-wikivote.qdGnZV/wiki-vote-raster-compare.json`

Следствие:
- Compare-step теперь уже существует и дает первые реальные числа.
- Следующий шаг уже не "сделать compare", а:
  - решить, какие baseline/thresholds считать приемлемыми;
  - расширить compare на еще один-два representative graph scenario;
  - только потом думать про автоматизацию этого compare в regression contour.

## 2026-04-09 21:21:09 PDT

Решение:
Считать текущую ветку находящейся уже не на этапе внедрения native path, а на этапе его измерения и расширения: основной Graphviz native path и cross-platform no-system smoke подтверждены, следующий шаг теперь про raster compare и более крупные integration-сценарии.

Причины:
- Ветка уже прошла дальше, чем старые формулировки в плане:
  - `Json`, `Svg`, `Png`, `Jpg` идут по native path
  - standalone module path через `psd1` закрыт
  - no-system-graphviz matrix уже зеленый на трех ОС
- Значит оставлять в плане старый `Следующий шаг` про выбор raster backend или про незакрытый no-system contour было бы неверно и вводило бы в заблуждение.
- После закрытия packaging/runtime вопросов основной оставшийся риск уже не в работоспособности пути, а в качестве и воспроизводимости raster output по сравнению с оригинальным `dot`.

Телеметрия / наблюдения:
- Рабочая ветка:
  - `feature/direct-graphviz-integration`
- Текущее состояние рабочего дерева:
  - чисто, кроме не тронутого `AGENTS.md`
- Подтверждающий CI run:
  - repo: `eosfor/PSGraphView`
  - run: `24070711886`
  - итог:
    - `ubuntu-24.04 / linux-x64`: success
    - `macos-14 / osx-arm64`: success
    - `windows-2022 / win-x64`: success
    - workflow conclusion: `success`
- Уже есть база для следующего этапа:
  - [Compare-WikiVote-GraphvizSvg.ps1](/Users/andrei/repo/PSGraphView/demos/Compare-WikiVote-GraphvizSvg.ps1)
  - [Test-GraphvizNoSystemSmoke.ps1](/Users/andrei/repo/PSGraphView/eng/Test-GraphvizNoSystemSmoke.ps1)

Следствие:
- План нужно синхронизировать так:
  - `Патч 5` считать частично выполненным
  - `Патч 6` считать выполненным по основному сценарию
  - следующим практическим шагом считать compare-step для raster divergence, а не runtime/package fixes

## 2026-04-07 00:59:39 PDT

Решение:
Считать Linux raster packaging gap закрытым: явная зависимость на `SkiaSharp.NativeAssets.Linux.NoDependencies` и перевод workflow default на `0.1.0-beta.12` дали полностью зеленый `no-system-graphviz` matrix в `PSGraphView`.

Причины:
- Предыдущий remaining failure уже не был связан с `graphviz runtime` и сводился к Linux raster path в `SkiaSharp`.
- После явного включения Linux native assets publish на Linux runner начал реально раскладывать `libSkiaSharp.so` в модульный layout.
- Повторный `GitHub Actions` прогон подтвердил, что bundled-path теперь одинаково работает на всех трех целевых ОС.

Телеметрия / наблюдения:
- Кодовый патч:
  - [PSGraphView.Graphviz.csproj](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/PSGraphView.Graphviz.csproj)
  - добавлен `SkiaSharp.NativeAssets.Linux.NoDependencies`
- CI workflow:
  - [no-system-graphviz.yml](/Users/andrei/repo/PSGraphView/.github/workflows/no-system-graphviz.yml)
  - default `graphviz_runtime_version` переведен на `0.1.0-beta.12`
- Локальная проверка publish в Linux container:
  - `dotnet publish ./src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj -c Release --no-restore -o /tmp/psgv-publish`
  - в publish output появились:
    - `SkiaSharp.dll`
    - `runtimes/linux-x64/native/libSkiaSharp.so`
- Downstream workflow:
  - repo: `eosfor/PSGraphView`
  - run: `24070711886`
  - итог:
    - `ubuntu-24.04 / linux-x64`: success
    - `macos-14 / osx-arm64`: success
    - `windows-2022 / win-x64`: success
    - workflow conclusion: `success`

Следствие:
- Этап cross-platform no-system smoke теперь можно считать подтвержденным по основному сценарию.
- Следующий оставшийся кусок плана уже не про packaging/runtime, а про compare-step и численную телеметрию расхождения raster output.

## 2026-04-07 00:52:06 PDT

Решение:
Явно добавить Linux native asset package для `SkiaSharp` в `PSGraphView.Graphviz`, а оставшийся cross-platform gap считать уже не проблемой `graphviz runtime`, а проблемой Linux raster packaging в `PSGraphView`.

Причины:
- Повторный downstream `no-system-graphviz` прогон на новом `graphviz runtime 0.1.0-beta.12` уже закрыл upstream часть проблемы:
  - `macOS`: success
  - `Windows`: success
  - `Linux`: единственный remaining failure
- Linux failure происходит уже после успешного bundled Graphviz publish/smoke path:
  - `Json` и `Svg` на Linux создаются
  - падение начинается на `Export-GraphvizView -As Png`
- `SkiaSharp 2.88.9` по своему `nuspec` тянет native assets только для `Win32` и `macOS`; Linux native assets туда по умолчанию не входят.
- Значит Linux publish module layout нельзя считать надежным без явной package-зависимости на Linux native assets.

Телеметрия / наблюдения:
- Upstream release:
  - repo: `eosfor/graphviz-psgv`
  - tag: `psgv-runtime-v0.1.0-beta.12`
  - release run: `24070196448`
  - результат:
    - `linux-x64`: success
    - `osx-arm64`: success
    - `win-x64`: success
- Downstream workflow:
  - repo: `eosfor/PSGraphView`
  - run: `24070383134`
  - результат:
    - `macos-14 / osx-arm64`: success
    - `windows-2022 / win-x64`: success
    - `ubuntu-24.04 / linux-x64`: failure
- Точный Linux failure:
  - `Export-GraphvizView -As Png`
  - `The type initializer for 'SkiaSharp.SKImageInfo' threw an exception.`
- Linux smoke artifact уже содержит:
  - `graph.json`
  - `graph.svg`
  - но не содержит `graph.png` и `graph.jpg`
- Проверка `SkiaSharp` package metadata:
  - в `SkiaSharp.nuspec` для `net6.0/.NETStandard` заявлены только:
    - `SkiaSharp.NativeAssets.Win32`
    - `SkiaSharp.NativeAssets.macOS`
  - Linux native package туда по умолчанию не входит
- Дополнительная CI-правка:
  - workflow default для `graphviz_runtime_version` тоже переведен на `0.1.0-beta.12`
  - иначе push-triggered run-ы продолжали бы смотреть на устаревший `0.1.0-beta.3` и давали бы ложный шум

Следствие:
- Следующий кодовый патч в `PSGraphView` должен быть минимальным:
  - явная зависимость на `SkiaSharp.NativeAssets.Linux.NoDependencies`
- После этого нужно заново прогнать `no-system-graphviz` workflow и проверить, ушел ли Linux raster failure.

## 2026-04-07 00:29:38 PDT

Решение:
Чинить оставшийся Linux failure в `PSGraphView`, а не в published `graphviz` bundle: preload bundled native libraries должен сначала поднимать обычные shared libraries, и только потом `libgvplugin_*`.

Причины:
- Первый downstream `no-system-graphviz` прогон на `graphviz runtime 0.1.0-beta.11` уже показал, что новый bundle сам по себе достаточно хороший:
  - `macOS`: success
  - `Windows`: success
  - `Linux`: единственный failure
- Linux log упал не на отсутствии файла в archive, а на порядке загрузки:
  - `libgvplugin_neato_layout.so` пытался подняться раньше, чем процесс увидел `libgts-0.7.so.5`
- Сам release archive `graphviz-psgv-runtime-0.1.0-beta.11-linux-x64.tar.gz` уже содержит `libgts-0.7.so.5`, значит ошибка не в публикации asset-а, а в consumer preload logic.

Телеметрия / наблюдения:
- Downstream workflow:
  - `eosfor/PSGraphView`
  - run `24068945588`
  - `macos-14 / osx-arm64`: success
  - `windows-2022 / win-x64`: success
  - `ubuntu-24.04 / linux-x64`: failure
- Точный Linux failure:
  - `Unable to load shared library '.../libgvplugin_neato_layout.so' ... libgts-0.7.so.5: cannot open shared object file`
- Проверка archive:
  - `gh release download psgv-runtime-v0.1.0-beta.11 --repo eosfor/graphviz-psgv --pattern 'graphviz-psgv-runtime-0.1.0-beta.11-linux-x64.tar.gz'`
  - внутри archive уже есть `lib/libgts-0.7.so.5`
- Локальная regression-проверка:
  - добавлен test на порядок preload-а:
    - [GraphvizNativeApiTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphvizNativeApiTests.cs)
  - локально пройдено:
    - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --filter GraphvizNativeApiTests --no-restore`
    - `PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR=/Users/andrei/.codex/worktrees/f5d8/graphviz dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphvizView --no-restore`

Следствие:
- Следующий внешний шаг:
  - закоммитить consumer-side fix в `PSGraphView`
  - повторно прогнать `no-system-graphviz` workflow на `0.1.0-beta.11`
- Если Linux после этого все еще будет падать, тогда уже возвращаться к upstream bundle и смотреть на следующий отсутствующий transitive dependency.

## 2026-04-06 23:03:06 PDT

Решение:
Добавить локальный helper для staged bundled-layout smoke, чтобы можно было прогонять publish-like сценарий без приватного runtime token и без ожидания `GitHub Actions`.

Причины:
- В текущем локальном окружении нет `PSGRAPHVIEW_GITHUB_TOKEN` и нет `GITHUB_TOKEN`, поэтому отсюда нельзя воспроизвести полный `dotnet publish` с загрузкой приватного Graphviz runtime bundle.
- При этом перед реальным CI-прогоном полезно иметь локальную проверку именно bundled-path, а не только сценарий с явным `PSGRAPHVIEW_PSGV_LIBRARY_PATH`.
- Временный staged module layout с `libpsgv` в `runtimes/<rid>/native` достаточно близок к publish-like раскладке, чтобы поймать ошибки поиска native runtime на стороне `psd1`-импорта.

Телеметрия / наблюдения:
- Добавлен helper:
  - [Invoke-LocalNoSystemGraphvizSmoke.ps1](/Users/andrei/repo/PSGraphView/eng/Invoke-LocalNoSystemGraphvizSmoke.ps1)
- Добавлена документация:
  - [README.md](/Users/andrei/repo/PSGraphView/README.md)
- Добавлен ignore для локальных smoke-артефактов:
  - [.gitignore](/Users/andrei/repo/PSGraphView/.gitignore)
- Локальная bundled-layout проверка пройдена:
  - `pwsh -NoLogo -NoProfile -File ./eng/Invoke-LocalNoSystemGraphvizSmoke.ps1 -GraphvizNativeLibraryPath /var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/psgraphview-graphviz-native/libpsgv.dylib -OutputDirectory ./artifacts/local-bundled-no-system-graphviz-smoke`
  - результат: `Graphviz no-system smoke passed`
  - результат: `Local bundled no-system-graphviz smoke passed`
  - артефакт результата: `artifacts/local-bundled-no-system-graphviz-smoke/smoke-results.json`
- Наблюдение по окружению:
  - `PSGRAPHVIEW_GITHUB_TOKEN`: не задан
  - `GITHUB_TOKEN`: не задан

Следствие:
- Теперь у нас есть два локальных уровня проверки `Патча 6`:
  - explicit native library path через `Test-GraphvizNoSystemSmoke.ps1`
  - bundled-layout emulation через `Invoke-LocalNoSystemGraphvizSmoke.ps1`
- Следующий внешний шаг все еще тот же:
  - реальный прогон `GitHub Actions` workflow
  - затем разбор cross-platform результатов

## 2026-04-06 22:54:10 PDT

Решение:
Начать `Патч 6` с одного общего smoke-скрипта и отдельного `GitHub Actions` workflow, а не пытаться сразу покрыть весь этап только через xUnit tests.

Причины:
- Для сценария "без системного Graphviz" важнее проверить реальный модульный запуск через `pwsh -NoProfile` и `Import-Module ...psd1`, чем добавлять еще один слой unit tests.
- Один общий smoke-скрипт удобнее использовать и локально, и в CI, не дублируя логику проверки `Json|Svg|Png|Jpg`.
- Cross-platform workflow нужен именно на уровне publish-like layout, потому что следующий основной риск уже в packaging/runtime behavior, а не в scene/interpreter логике.
- Matrix runner-ы выбраны так, чтобы соответствовать текущим bundled `RID`:
  - `ubuntu-24.04` -> `linux-x64`
  - `windows-2022` -> `win-x64`
  - `macos-14` -> `osx-arm64`

Телеметрия / наблюдения:
- Добавлен общий smoke-скрипт:
  - [Test-GraphvizNoSystemSmoke.ps1](/Users/andrei/repo/PSGraphView/eng/Test-GraphvizNoSystemSmoke.ps1)
- Добавлен отдельный workflow:
  - [no-system-graphviz.yml](/Users/andrei/repo/PSGraphView/.github/workflows/no-system-graphviz.yml)
- Скрипт делает следующее:
  - импортирует модуль по `psd1`
  - подсовывает невалидный `PSGRAPHVIEW_GRAPHVIZ_DOT_PATH`
  - подменяет `dot` в `PATH` на заведомо падающий shim
  - проверяет `Export-GraphvizView -As Json|Svg|Png|Jpg`
  - валидирует `_draw_`, `<svg>`, сигнатуры `PNG` и `JPG`
- Локальная проверка пройдена:
  - `pwsh -NoLogo -NoProfile -File ./eng/Test-GraphvizNoSystemSmoke.ps1 -ModuleManifestPath ./src/PSGraphView.PowerShell/bin/Debug/net9.0/PSGraphView.psd1 -GraphvizNativeLibraryPath /var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/psgraphview-graphviz-native/libpsgv.dylib -OutputDirectory ./artifacts/local-no-system-graphviz-smoke`
  - результат: `Graphviz no-system smoke passed`
  - артефакт результата: `artifacts/local-no-system-graphviz-smoke/smoke-results.json`

Следствие:
- Теперь у `Патча 6` есть единый executable smoke path, который можно запускать и вручную, и в `GitHub Actions`.
- Следующий незакрытый кусок этого этапа:
  - дождаться реального прогона workflow в CI
  - отдельно добавить telemetry compare для raster divergence против оригинального `dot`

## 2026-04-06 22:46:23 PDT

Решение:
Для следующего этапа автономных проверок не ограничиваться локальными тестами и сразу планировать publish-like smoke в `GitHub Actions` matrix на трех платформах:
- `macOS`
- `Linux`
- `Windows`

Причины:
- Локальные tests уже подтверждают кодовый path, но не дают такой же уверенности по реальному module/runtime layout на всех целевых ОС.
- Главная цель следующего этапа не просто "еще один тест", а доказательство того, что `PSGraphView` работает без системного `dot` и без установленного системного Graphviz.
- Именно cross-platform `GitHub Actions` smoke лучше всего ловит ошибки в:
  - поиске native runtime assets
  - layout модуля вокруг `psd1`
  - различиях `RID` и загрузки зависимостей между `macOS`, `Linux` и `Windows`
- При этом тяжелые compare/benchmark сценарии не стоит тащить в обычный PR pipeline, чтобы не сделать его медленным и шумным.

Телеметрия / наблюдения:
- Уже закрыт standalone module path через `psd1` для raster на локальной машине:
  - [PSGraphView.PowerShell.csproj](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj)
  - [SkiaSharpNativeLoader.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/SkiaSharpNativeLoader.cs)
- Уже есть локальные проверки, подтверждающие native path:
  - `Export-GraphvizView` tests: `7 passed`
  - `GraphSceneRasterRenderer` tests: `2 passed`
- Следующий незакрытый риск уже не в логике scene/interpreter, а в cross-platform packaging/runtime поведении.

Следствие:
- `Патч 6` нужно строить вокруг одного общего smoke-сценария `pwsh -NoProfile -> Import-Module psd1 -> Export-GraphvizView -As Json|Svg|Png|Jpg`.
- Этот сценарий нужно запускать и локально, и в `GitHub Actions` matrix.
- В CI нельзя ставить системный `graphviz`; наоборот, нужно явно подтверждать, что процессный fallback недоступен.

## 2026-04-06 21:47:48 PDT

Решение:
Закрыть `Патч 4a` через два изменения:
- включить копирование lockfile assemblies в output модуля PowerShell;
- добавить явный native loader для `SkiaSharp`, который ищет `libSkiaSharp` в root module path и в `runtimes/<rid>/native`.

Причины:
- Для реального PowerShell-модуля было недостаточно того, что raster renderer работает в test host и direct `.NET` runner.
- Standalone import через `PSGraphView.psd1` требует, чтобы модуль сам подтягивал и managed `SkiaSharp.dll`, и native `libSkiaSharp`.
- Без этого `Png/Jpg` оставались формально реализованными, но не закрывали настоящий пользовательский сценарий.

Телеметрия / наблюдения:
- Добавлен loader:
  - [SkiaSharpNativeLoader.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/SkiaSharpNativeLoader.cs)
- Обновлен PowerShell module build output:
  - [PSGraphView.PowerShell.csproj](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj)
- Обновлены standalone tests:
  - [ExportGraphvizViewCmdletTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/ExportGraphvizViewCmdletTests.cs)
- После исправления в build output появились:
  - `SkiaSharp.dll`
  - `libSkiaSharp.dylib`
  - `runtimes/osx/native/libSkiaSharp.dylib`
- Проверки:
  - `PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR=/Users/andrei/.codex/worktrees/f5d8/graphviz dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphvizView`
  - результат: `7 passed`, `0 failed`, `0 skipped`
  - `dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj --filter GraphSceneRasterRenderer`
  - результат: `2 passed`, `0 failed`, `0 skipped`
  - отдельная standalone-проверка:
    - `pwsh -NoProfile`
    - `Import-Module /Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/bin/Debug/net9.0/PSGraphView.psd1`
    - `Export-GraphvizView -As Png|Jpg`
    - успешно созданы `wiki-vote-standalone-module.png` и `wiki-vote-standalone-module.jpg` даже при битом `PSGRAPHVIEW_GRAPHVIZ_DOT_PATH`

Следствие:
- Native raster path теперь закрыт не только по коду и unit/cmdlet tests, но и по standalone PowerShell module path через `psd1`.
- Следующий незакрытый шаг уже действительно про end-to-end сценарий без системного `dot` и без установленного системного Graphviz.

## 2026-04-06 21:41:30 PDT

Решение:
Добавить в план отдельный ближний patch на module/runtime loading для raster path через `Import-Module ...psd1`.

Причины:
- Сам managed raster renderer уже работает, но этого недостаточно для реального PowerShell-модуля.
- Сейчас есть разница между сценариями:
  - direct `.NET` runner может собрать `Png/Jpg`
  - а standalone `pwsh` через импорт модуля по `psd1` упирается в загрузку `SkiaSharp`
- Значит это нужно считать не абстрактной e2e-полировкой, а отдельным техническим gap в module packaging/runtime layout.

Телеметрия / наблюдения:
- При запуске через временный direct `.NET` runner `WikiVote` raster уже собирается:
  - [wiki-vote-psgraphview-native.png](/var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/PSGraphView-wikivote-compare/wiki-vote-psgraphview-native.png)
  - [wiki-vote-psgraphview-native.jpg](/var/folders/1j/j11fjyg16ys35ssgq1kz8d6m0000gn/T/PSGraphView-wikivote-compare/wiki-vote-psgraphview-native.jpg)
- При standalone `pwsh` через импорт модуля по `psd1` всплывает runtime error:
  - `The type initializer for 'SkiaSharp.SKImageInfo' threw an exception`
  - inner error: `Unable to load shared library 'libSkiaSharp'`
- Наблюдение по layout output:
  - это не похоже на проблему scene/interpreter-а
  - это похоже именно на проблему раскладки native dependency для модульного сценария

Следствие:
- Перед полными end-to-end проверками нужно отдельно закрыть module/runtime loading для `SkiaSharp`.
- Целевой сценарий проверки должен быть именно таким:
  - `pwsh -NoProfile`
  - `Import-Module <path-to-PSGraphView.psd1>`
  - `Export-GraphvizView -As Png|Jpg`

## 2026-04-06 21:29:35 PDT

Решение:
Добавить в дальнейший план отдельное сравнение между оригинальным raster output от `dot` и managed raster output от `PSGraphView`, причем не ограничиваться визуальной проверкой, а снять численную метрику расхождения.

Причины:
- После перехода `Png/Jpg` на managed renderer важно понимать не только то, что картинки "похожи", но и насколько они реально расходятся.
- Для raster path визуального сравнения недостаточно: различия могут быть в масштабе, толщине линий, антиалиасинге и канве даже при одинаковом layout.
- Такая телеметрия пригодится и для регрессий, и для дальнейших решений, если придется подкручивать raster backend или packaging.

Телеметрия / наблюдения:
- На `WikiVote` subgraph визуально layout выглядит очень близко к `dot`.
- При этом уже видно расхождение по raster output size:
  - `dot -Tpng`: `4373x1595`, `1,583,898` байт
  - managed `png`: `3272x1188`, `988,627` байт
  - `dot -Tjpg`: `3280x1196`, `760,916` байт
  - managed `jpg`: `3272x1188`, `668,937` байт
- Значит совпадение по геометрии высокое, но pixel-level output точно не является 1:1 и требует отдельной метрики.

Следствие:
- В следующих этапах нужно добавить compare-step и сохранить его результаты в decision log как телеметрию.
- Для первого среза достаточно сравнения на `WikiVote` subgraph и на одном маленьком smoke graph.

## 2026-04-06 21:12:32 PDT

Решение:
Закрыть `Патч 4` через отдельный managed raster renderer в `PSGraphView.Graphviz` и перевести `Export-GraphvizView -As Png|Jpg` на тот же native path, что уже используется для `Svg`.

Причины:
- После стабилизации `Svg` уже не было смысла держать `Png/Jpg` на process fallback.
- Scene model уже покрывает нужный набор фигур, линий, bezier и текста, поэтому raster path логично строить поверх нее, а не вводить второй независимый decode/render слой.
- Для первого raster-среза `SkiaSharp` дает достаточно прямой путь к `Png/Jpg` без привязки к системному Graphviz.

Телеметрия / наблюдения:
- Добавлен renderer:
  - [GraphSceneRasterRenderer.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.Graphviz/GraphSceneRasterRenderer.cs)
- Добавлены unit tests:
  - [GraphSceneRasterRendererTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.Graphviz.Tests/GraphSceneRasterRendererTests.cs)
- Обновлен cmdlet:
  - [ExportGraphvizViewCmdlet.cs](/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/ExportGraphvizViewCmdlet.cs)
- Обновлены cmdlet tests:
  - [ExportGraphvizViewCmdletTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/ExportGraphvizViewCmdletTests.cs)
  - `Png` и `Jpg` tests теперь тоже задают битый `PSGRAPHVIEW_GRAPHVIZ_DOT_PATH` и ожидают успешный render
- Проверки:
  - `PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR=/Users/andrei/.codex/worktrees/f5d8/graphviz dotnet test tests/PSGraphView.Graphviz.Tests/PSGraphView.Graphviz.Tests.csproj`
  - результат: `16 passed`, `0 failed`, `0 skipped`
  - `PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR=/Users/andrei/.codex/worktrees/f5d8/graphviz dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --filter ExportGraphvizView`
  - результат: `6 passed`, `0 failed`, `0 skipped`

Следствие:
- `Json`, `Svg`, `Png` и `Jpg` теперь идут через общий native path `DOT -> libpsgv -> Graphviz JSON -> scene -> renderer`.
- Следующий незакрытый технический шаг уже не про process fallback, а про более жесткие end-to-end проверки для сценария без системного `dot` и без установленного системного Graphviz.

## 2026-04-06 21:07:30 PDT

Решение:
Закрыть `Патч 3a` через узкий cmdlet-level regression-test, который специально ломает путь к `dot`, но все равно требует успешный `Export-GraphvizView -As Svg`.

Причины:
- Нам нужно было закрепить уже работающий native `Svg` path до перехода к `Png/Jpg`.
- Такой тест срабатывает ровно на нужной границе: если `Svg` снова начнет пользоваться `GraphvizProcessRenderer`, он упадет сразу.
- Для этого шага не нужен полный сценарий "машина без Graphviz"; достаточно намеренно сломанного `dot`, чтобы поймать откат к process renderer.

Телеметрия / наблюдения:
- Добавлен regression-test:
  - [ExportGraphvizViewCmdletTests.cs](/Users/andrei/repo/PSGraphView/tests/PSGraphView.PowerShell.Tests/ExportGraphvizViewCmdletTests.cs)
  - тест задает битый `PSGRAPHVIEW_GRAPHVIZ_DOT_PATH` и проверяет, что `Export-GraphvizView -As Svg` все равно возвращает валидный `Svg`
- Для изоляции используется локальный scope, который восстанавливает env var после теста.
- Проверка:
  - `PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR=/Users/andrei/.codex/worktrees/f5d8/graphviz dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore --filter ExportGraphvizView`
  - результат: `5 passed`, `0 failed`, `0 skipped`

Следствие:
- Native `Svg` path теперь закреплен отдельным regression-test.
- Следующий практический шаг уже можно брать как `scene -> Png/Jpg`, не смешивая его с защитой уже работающего `Svg`.

## 2026-04-06 20:52:08 PDT

Решение:
Выделить короткий защитный шаг для `Svg` в отдельный `Патч 3a` и поставить его перед `scene -> Png/Jpg`.

Причины:
- `Svg` уже переведен на native path, но это поведение пока нужно явно закрепить тестом, а не считать самоочевидным.
- Если сразу перейти к raster path, то можно незаметно вернуть `Svg` на process fallback и заметить это слишком поздно.
- Отдельный маленький патч проще проверить и проще читать в истории, чем смешанный этап `Svg guard + Png/Jpg`.

Телеметрия / наблюдения:
- Текущее состояние ветки:
  - `Export-GraphvizView -As Svg` уже работает через native path
  - `Export-GraphvizView -As Png|Jpg` все еще идут через process fallback
- На `wiki-vote` compare/benchmark path уже подтверждено, что native `Svg` реально работает без вызова `dot` для самого рендера.

Следствие:
- Следующий технический шаг в плане должен быть не `Png/Jpg`, а regression-test на независимость `Svg` от внешнего `dot`.
- Этот шаг нужно считать только локальной страховкой для `Svg`, а не финальным доказательством автономности всей системы.
- Конечный критерий должен быть шире:
  - решение должно работать без системного `dot`
  - и без установленного системного Graphviz
  - отдельные end-to-end проверки должны подтверждать это уже для всего native path, а не только для `Svg`.

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
