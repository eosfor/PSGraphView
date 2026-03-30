# План сближения `PSGraphView.Sfdp` с `graphviz sfdp`

## Цель

Сблизить managed-реализацию `PSGraphView.Sfdp` с `graphviz sfdp` максимально близко к 1:1 по:

- ходу алгоритма
- ключевым промежуточным состояниям
- итоговой раскладке
- поведению на реальных графах

Основной сравнительный сценарий:

- `demos/Compare-WikiVote-Sfdp.ps1`

---

## Статус на сегодня

Сделано:

- Шаг 0 реализован:
  - добавлен diagnostic API в `PSGraphView.Sfdp`
  - diagnostics выведены в `Export-GraphView`
  - demo сохраняет рядом `graphviz.verbose.log` и `managed.diagnostics.jsonl`
- Есть подтверждённые прогоны:
  - `subgraph-30`
  - полный `WikiVote`
- По полному `WikiVote` уже разобраны логи и закрыты главные multilevel-разъезды:
  - `seed` и overlap budget в compare harness выровнены
  - `K`, component-level `p`, `max_depth` и default `threshold` доведены до близкой к Graphviz семантики
  - canonical triangulation parity улучшена на `Patch 6b`
- Текущее состояние после `Patch 12` на full `WikiVote`:
  - full graph:
    - Graphviz `MeanEdgeToDiagonal = 0.094565`
    - Managed `MeanEdgeToDiagonal = 0.077753`
    - Graphviz `EdgeLengthCv = 0.555466`
    - Managed `EdgeLengthCv = 0.555135`
  - main component:
    - Graphviz `MeanEdgeToDiagonal = 0.094585`
    - Managed `MeanEdgeToDiagonal = 0.093877`
    - Graphviz `EdgeLengthCv = 0.555271`
    - Managed `EdgeLengthCv = 0.554953`
  - handoff checkpoints:
    - `component.geometry stage=layout_component_return`: width `20.099584`
    - `layout.geometry stage=after_packing`: width `24.811667`
    - `svg.geometry stage=export_input`: width `24.811667`
  - вывод:
    - main component уже близок к Graphviz
    - renderer / `SfdpSvgExporter` не даёт главный разъезд
    - `Patch 12` закрыл главный mismatch по packing scale / step
    - remaining mismatch уже не выглядит как грубая проблема единиц или cell scale
- `Patch 7` реализован:
  - в managed добавлен явный `SfdpQuadtreeMode` с режимами `None/Normal/Fast/Hybrid`
  - default path теперь явно соответствует Graphviz `NORMAL`
  - старый `DisableSfdpBarnesHut` сохранён как совместимый alias к `quadtreeMode=None`
  - в diagnostics теперь пишутся `quadtreeMode`, `effectiveQuadtreeMode`, `quadtreeHybridThreshold`, `quadtreeMaxDepth`, `quadtreeMaxDepthUsed`
  - в managed добавлен batched single-level path для `Fast`
  - добавлены тесты на `Hybrid -> Normal/Fast` и на PowerShell surface
- `Patch 7a` реализован:
  - в `Normal` path добавлен graphviz-like repulsion через `GetSupernodes(...)`
  - добавлен optimizer для `quadtreeLevel`, близкий к Graphviz `max_qtree_level`
  - в diagnostics теперь пишутся `quadtreeLevel`, `quadtreeWork`, `nsuperAverage`, `countsAverage`
  - full `WikiVote` показал, что главный quadtree-разъезд почти закрыт:
    - `mode_switch` на главной компоненте стал `49 -> 48`
    - runtime managed заметно упал относительно `Patch 7`
- `Patch 11` реализован:
  - `SfdpComponentPacker` переведён с packing по bounding rectangles на graphviz-like packing по polyomino cells
  - polyomino теперь строится по node boxes и local edge lines, а не по целому bbox компоненты
  - default `ComponentGap` уменьшен с `40` до `16`, чтобы соответствовать graphviz-scale packing намного ближе
  - `LayoutEngine` теперь отдаёт packer-у full `SfdpCsrGraph`, чтобы packing мог учитывать локальные рёбра компоненты
  - полный `WikiVote` показал заметный сдвиг:
    - `Managed MeanEdgeToDiagonal = 0.008147 -> 0.016874`
    - `after_packing width = 220.524444 -> 110.524444`
  - main component при этом осталась близкой к Graphviz
- `Patch 12` реализован:
  - packing переведён на отдельную point-scale семантику с `scale = 72`
  - `ComponentGap` снова трактуется в layout units, а packing margin вычисляется в Graphviz-like point space
  - в diagnostics добавлены shape-level packing поля:
    - `scale`
    - `cellCount`
    - `gridWidth/gridHeight`
    - `perimeter`
    - `componentWidthScaled/componentHeightScaled`
    - `roundedMinXScaled/roundedMinYScaled`
  - полный `WikiVote` показал:
    - `step = 35` у managed против Graphviz `29`
    - `after_packing width = 24.811667`
    - `Managed MeanEdgeToDiagonal = 0.077753`
  - main component по-прежнему близка к Graphviz

Не сделано:

- добить остаточный full-graph mismatch после `Patch 12`
  - `Patch 12` уже убрал разъезд по packing scale / step:
    - Graphviz `step size = 29`
    - Managed `step = 35`
  - но на full `WikiVote` ещё остаётся остаточный разъезд:
    - Graphviz `MeanEdgeToDiagonal = 0.094565`
    - Managed `MeanEdgeToDiagonal = 0.077753`
  - следующий конкретный шаг:
    - проверить финальный shift / normalize path после packing
    - при необходимости сравнить managed post-pack handoff с Graphviz `dotneato_postprocess`
- добивка quadtree parity по fine details
  - surface и основная `Normal`-траектория уже близки
  - если возвращаться сюда, то только ради более точного повторения Graphviz internals
- выравнивание smoothing / optional parity features

Частично сделано:

- `Patch 0b` реализован:
  - в managed добавлены missing diagnostics для `singlelevel`, `multilevel.refine` и `prism overlap`
  - в локальном `graphviz` исходнике добавлен verbose для effective `p`, multilevel levels и `refine setup`
  - локальный `graphviz` собран в `/tmp/graphviz-prefix`
  - полный `WikiVote` прогнан через локальный `sfdp`
  - managed сборка и тесты зелёные
- `Patch 1` реализован:
  - compare-скрипт теперь передаёт Graphviz `-Gstart=$SfdpSeed`
  - compare-скрипт теперь явно фиксирует `overlap=prism1000`
  - managed export теперь идёт с `-SfdpOverlapRemovalIterations 1000`
  - полный `WikiVote` подтверждает, что `seed` выровнен и managed для `ComponentId=0` доходит до `mode_switch` и `shrink`
- `Patch 2` реализован:
  - refinement `K` больше не пересчитывается из fine-layout
  - `K` теперь спускается вниз по уровням как `previousNaturalLength * 0.75`
  - jitter при `prolongate` теперь тоже берётся из `previousNaturalLength`
  - добавлен тест на цепочку `previous -> expectedGraphviz -> actualManaged`
  - полный `WikiVote` перепрогнан на свежей сборке модуля
- `Patch 3` реализован:
  - effective `p` теперь вычисляется один раз на компоненту
  - это значение протягивается через весь multilevel path
  - в diagnostics добавлен `component.control` для effective `p`
  - добавлен регрессионный тест на component-level `p`
  - полный `WikiVote` перепрогнан на свежей сборке модуля
- `Patch 4` реализован:
  - дефолтный `MaxMultilevelDepth` больше не ограничен значением `8`
  - PowerShell default тоже больше не режет multilevel на `8`
  - явный user override по глубине сохранён
  - добавлены тесты на default без `max_depth` и на явный лимит
  - полный `WikiVote` перепрогнан на свежей сборке модуля
- `Patch 5` реализован:
  - дефолтный `MultilevelThreshold` больше не равен `64`
  - default threshold теперь `4`, ближе к Graphviz `minsize`
  - явный user override по threshold сохранён
  - добавлены тесты на новый default threshold и на явный threshold override
  - полный `WikiVote` перепрогнан на свежей сборке модуля
- `Patch 6` реализован:
  - в `prism` relaxation path добавлен graphviz-like perturbation для `dist == 0`
  - этот path теперь использует общий `seed`, а не скрытый `Random.Shared`
  - в overlap diagnostics добавлен `zeroDistancePerturbations`
  - добавлен регрессионный тест на exact-coincident pair
  - полный `WikiVote` перепрогнан на свежей сборке модуля
  - по full `WikiVote` это не изменило метрики и не сдвинуло `mode_switch`
  - вывод: для главной компоненты текущий разъезд сидит не в `dist == 0` branch
- `Patch 6a` частично реализован:
  - в managed добавлена явная поддержка `OverlapRemovalHalfWidth/OverlapRemovalHalfHeight`
  - эти override'ы выведены в `Export-GraphView`
  - в diagnostics теперь пишутся `boxSource`, `boxHalfWidth`, `boxHalfHeight`
  - compare-скрипт теперь парсит `Graphviz avg_label-size` и сохраняет suggested managed half-size в summary
  - эксперимент с автоприменением этого override в default compare-path не улучшил overlap trace и ухудшил итоговую packed-метрику
  - вывод: support нужен, но включать это поведение по умолчанию в compare harness пока нельзя
- `Patch 6b` реализован:
  - edge set `SfdpPrismTriangulationBuilder` сверен с Graphviz `delaunay_tri` на canonical point sets
  - найден и исправлен tie-break для co-circular cases
  - для этого points on circumcircle теперь не считаются `inside`
  - добавлены differential tests на square / roof shape и tightened test на proximity graph
  - полный `WikiVote` перепрогнан на свежей сборке модуля
  - по full `WikiVote` это не изменило итоговые метрики, но canonical triangulation path теперь ближе к Graphviz
- `Patch 8` частично реализован:
  - в `SfdpPrismOverlapRemover` добавлены подробные shrink/scaling diagnostics
  - shrink теперь входит по факту отсутствия overlap-ребер, а не только по численному `maxOverlap < 1`
  - добавлен тест на shrink diagnostics
  - полный `WikiVote` перепрогнан на свежей сборке модуля
  - по full `WikiVote` итоговые метрики не изменились
  - но теперь видно главное:
    - у `ComponentId=0` shrink действительно начинается
    - `scaleStart = 1`, `scaleBest = 1`, `scalingIterations = 0`
    - `returnedEarlyAtScaleStart = true`
  - вывод: remaining mismatch сидит раньше самого bisection, в post-switch `prism` model/graph semantics
- `Patch 8a` реализован:
  - в managed diagnostics добавлен `overlap.model` с агрегатами по графу и модели:
    - `proximityEdgeCount`
    - `overlapEdgeCount`
    - `combinedEdgeCount`
    - `proximityOnlyEdgeCount`
    - `overlapOnlyEdgeCount`
    - `sharedEdgeCount`
    - `expandEdgeCount`
    - `shrinkEdgeCount`
    - `maxOverlapFactor`
    - `minOverlapFactor`
  - в `SfdpPrismStressSystem` добавлены счётчики для этих агрегатов
  - в локальном `graphviz/lib/neatogen/overlap.c` добавлен сопоставимый verbose `overlap model ...`
  - полный `WikiVote` перепрогнан через локально собранный `sfdp`
  - по full `WikiVote` итоговые метрики не изменились:
    - Graphviz `MeanEdgeToDiagonal = 0.094565`
    - Managed `MeanEdgeToDiagonal = 0.088738`
    - Graphviz `EdgeLengthCv = 0.555466`
    - Managed `EdgeLengthCv = 0.555218`
  - но теперь локализация разъезда уже жёсткая:
    - на первой non-neighbor итерации главной компоненты Graphviz строит большой overlap graph:
      - `proximity_edges = 21170`
      - `overlap_edges = 428537`
      - `combined_edges = 440752`
      - `shared_edges = 8955`
      - `expand_edges = 0`
      - `shrink_edges = 440752`
      - `min_overlap = 0.00684641`
    - managed в той же точке строит только proximity-path:
      - `proximityEdgeCount = 21169`
      - `overlapEdgeCount = 0`
      - `combinedEdgeCount = 21169`
      - `sharedEdgeCount = 0`
      - `expandEdgeCount = 0`
      - `shrinkEdgeCount = 21169`
      - `minOverlapFactor = 0.0271810908276002`
  - вывод:
    - разъезд уже не в shrink bisection
    - и не только в первой non-neighbor итерации
    - mismatch сидит в `prism` overlap-graph / ideal-distance semantics
    - следующий шаг должен идти в `SfdpPrismOverlapGraphBuilder` и `SfdpPrismModelBuilder`
- `Patch 8b` частично реализован:
  - `SfdpPrismOverlapGraphBuilder` переведён на graphviz-like overlap path
    - overlap graph теперь строится не как точный 2D overlap graph
    - а ближе к `get_overlap_graph()` из Graphviz
  - shrink-entry в `SfdpPrismOverlapRemover` возвращён к graphviz-like правилу:
    - вход по `maxOverlap < 1`
    - а не по `overlapEdgeCount == 0`
  - обновлены тесты на overlap-graph semantics
  - затем patch был расширен новыми подшагами:
    - добавлена узкая pre-overlap geometry diagnostics в managed и в локальный Graphviz verbose
    - полный `WikiVote` перепрогнан на:
      - `/tmp/psgraphview-sfdp-full-patch8b-geometry-localgv/wiki-vote-full-graphviz.verbose.log`
      - `/tmp/psgraphview-sfdp-full-patch8b-geometry-localgv/wiki-vote-full-managed.diagnostics.jsonl`
      - `/tmp/psgraphview-sfdp-full-patch8b-geometry-localgv/wiki-vote-full-comparison.json`
  - затем выяснилось, что `-UseLocalModules` мог поднимать stale `PSGraphView` из `tests/PSGraphView.PowerShell.Tests/bin/...`
    - позже этот обходной путь был заменён на fresh `dotnet publish` перед import
  - после пересборки test-проекта и свежего полного прогона:
    - `/tmp/psgraphview-sfdp-full-patch8b-freshmodule-localgv/wiki-vote-full-graphviz.verbose.log`
    - `/tmp/psgraphview-sfdp-full-patch8b-freshmodule-localgv/wiki-vote-full-managed.diagnostics.jsonl`
    - `/tmp/psgraphview-sfdp-full-patch8b-freshmodule-localgv/wiki-vote-full-comparison.json`
  - новый подтверждённый результат после расширения `Patch 8b`:
    - на первой non-neighbor итерации `ComponentId=0` managed уже почти совпадает с Graphviz по overlap-модели:
      - `proximityEdgeCount = 21169`
      - `overlapEdgeCount = 436378`
      - `combinedEdgeCount = 448589`
      - `sharedEdgeCount = 8958`
      - `minOverlapFactor = 0.006829398306334229`
    - Graphviz в той же точке:
      - `proximity_edges = 21170`
      - `overlap_edges = 428537`
      - `combined_edges = 440752`
      - `shared_edges = 8955`
      - `min_overlap = 0.00684641`
  - вывод:
    - overlap-graph path и shared-edge merge теперь почти совпали с Graphviz
    - geometry diagnostics на свежем модуле тоже подтверждены
    - главный remaining mismatch теперь уже не в `sharedEdgeCount`, а в pre-overlap geometry / scaling path
  - внутри `Patch 8b.2` уже проверены две важные гипотезы:
    - в compare-скрипт добавлены явные override'ы:
      - `ManagedOverlapHalfWidth`
      - `ManagedOverlapHalfHeight`
      - `UseSuggestedManagedOverlapHalfSize`
    - найдено, что первый вариант suggestion был неверным:
      - он брал `avg edge len=... avg_label-size=...` из Graphviz verbose
      - это target edge length для `scale_to_edge_length`, а не base `avg_label_size`
    - compare-скрипт исправлен:
      - теперь он отдельно парсит `Graphviz BaseLabelSize`
      - и отдельно `Graphviz TargetEdgeLength`
      - suggestion для managed half-size теперь строится из base `avg_label_size / 2`
    - corrected experiment:
      - `/tmp/psgraphview-sfdp-full-patch8b2-correctedhalf-localgv/wiki-vote-full-comparison.json`
      - `/tmp/psgraphview-sfdp-full-patch8b2-correctedhalf-localgv/wiki-vote-full-managed.diagnostics.jsonl`
      - `/tmp/psgraphview-sfdp-full-patch8b2-correctedhalf-localgv/wiki-vote-full-graphviz.verbose.log`
    - что он показал:
      - на `post_scale` managed теперь почти 1:1 с Graphviz:
        - `averageLabelSize = 0.131111`
        - `averageEdgeLength = 0.524444`
      - `mode_switch` geometry тоже стала заметно ближе
      - но итоговая packed-метрика стала хуже:
        - managed `MeanEdgeToDiagonal = 0.008147`
    - вывод:
      - box-size / scaling semantics действительно влияют на overlap-loop geometry
      - но одного исправления half-size недостаточно, чтобы улучшить финальный layout
      - следующий шаг должен смотреть на `overlap finish` geometry и то, что происходит после overlap
    - это не закрывает `Patch 8b.2`
      - corrected half-size был полезным диагностическим экспериментом
      - но решение надо принимать по default path
      - следующий обязательный шаг: свежий full run с `overlap.geometry stage=finish` у managed и Graphviz
  - затем import local module был доведён до более надёжной схемы:
    - `demos/Import-DemoModules.ps1` теперь при `-UseLocalModules` делает fresh `dotnet publish` для `src/PSGraphView.PowerShell`
    - demo импортирует `PSGraphView` прямо из publish output
    - это даёт полный набор зависимостей и убирает stale-module эффект
  - свежий baseline run после перехода на publish-based import:
    - `/tmp/psgraphview-sfdp-full-patch8b2-finish3-localgv/wiki-vote-full-comparison.json`
    - `/tmp/psgraphview-sfdp-full-patch8b2-finish3-localgv/wiki-vote-full-managed.diagnostics.jsonl`
    - `/tmp/psgraphview-sfdp-full-patch8b2-finish3-localgv/wiki-vote-full-graphviz.verbose.log`
  - что он показал на default path:
    - managed `overlap.finish` теперь несёт финальную геометрию прямо в event:
      - `finishWidth = 833.9624029331408`
      - `finishHeight = 735.2162938856841`
      - `finishAverageEdgeLength = 105.45773696954491`
      - `finishAverageLabelSize = 5.439999999999856`
    - эта `finish`-геометрия для `ComponentId=0` совпадает с `mode_switch`-геометрией
      - то есть на default path shrink у managed фактически не меняет раскладку после перехода в `neighborhoodOnly = false`
    - итоговые метрики при этом остаются:
      - Graphviz `MeanEdgeToDiagonal = 0.094565`
      - Managed `MeanEdgeToDiagonal = 0.083519`
      - Graphviz `EdgeLengthCv = 0.555466`
      - Managed `EdgeLengthCv = 0.555134`
  - затем локальный Graphviz был пересобран, и default-path сравнение по `finish`-геометрии удалось закрыть:
    - свежий run:
      - `/tmp/psgraphview-sfdp-full-patch8b2-finish4-localgv/wiki-vote-full-comparison.json`
      - `/tmp/psgraphview-sfdp-full-patch8b2-finish4-localgv/wiki-vote-full-managed.diagnostics.jsonl`
      - `/tmp/psgraphview-sfdp-full-patch8b2-finish4-localgv/wiki-vote-full-graphviz.verbose.log`
      - `/tmp/psgraphview-sfdp-full-patch8b2-finish4-localgv/wiki-vote-full-graphviz.reverbose.log`
    - что показал default path для `ComponentId=0`:
      - Graphviz:
        - `post_scale width = 5.374888`, `avg_edge_len = 0.524444`, `avg_label_size = 0.131111`
        - `mode_switch == finish == after_overlap_removal`
        - `mode_switch width = 19.462161`, `avg_edge_len = 3.496258`
      - managed:
        - `pre_scale averageLabelSize = 5.439999999999856`
        - `post_scale width = 384.20624979012763`, `avg_edge_len = 21.759999999999255`
        - `mode_switch == finish == after_overlap_removal`
        - `mode_switch width = 833.9624029331408`, `avg_edge_len = 105.45773696954491`
    - вывод:
      - `Patch 8b.2` закрыт
      - post-switch / finish / after-overlap path больше не является слепой зоной
      - remaining mismatch сидит в default box-size / scaling semantics до входа в post-switch phase
  - compare-скрипт дополнительно расширен:
    - он теперь автоматически пишет `MainComponentGeometry` в `comparison.json`
    - по Graphviz и managed для checkpoint-ов:
      - `after_pcp_rotate`
      - `before_overlap_removal`
      - `pre_scale`
      - `post_scale`
      - `mode_switch`
      - `finish`
      - `after_overlap_removal`
    - верифицирующий run с этой автосводкой:
      - `/tmp/psgraphview-sfdp-full-patch8b2-finish5-localgv/wiki-vote-full-comparison.json`

Текущие артефакты анализа:

- `/tmp/psgraphview-sfdp-step0-check/wiki-vote-subgraph-30-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-step0-check/wiki-vote-subgraph-30-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-step0/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-step0/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch0b-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch1-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch1-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch2b-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch2b-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch2b-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch3-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch3-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch3-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch4-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch4-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch4-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch5-threshold-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch5-threshold-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch5-threshold-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch6-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch6-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch6-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch6ab-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch6ab-final-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch6ab-final-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch6ab-final-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch7-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch7-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch7-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch7a-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch7a-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch8a-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch8a-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch8a-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch8b-freshmodule-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch8b-freshmodule-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch8b-freshmodule-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch7a-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-graphviz.verbose.log`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-comparison.json`

### Что уже видно по логам

Это уже не гипотезы, а то, что подтверждается логами и/или исходником Graphviz:

- compare harness по основным управляющим параметрам уже выровнен:
  - Graphviz и managed идут с одним `seed`
  - overlap budget выровнен до `1000`
- для главной компоненты полного `WikiVote` главный remaining mismatch сейчас уже не в multilevel, а в `prism` и/или single-level path:
  - Graphviz делает `mode_switch` на `iter 49`
  - managed делает `mode_switch` на `iter 61`
  - обе реализации доходят до shrink stage
- у managed сейчас не 1:1 `K`-path в multilevel:
  - Graphviz после `prolongate` делает `K *= 0.75`
  - managed фактически пересчитывает `K` заново по fine-layout
- после `Patch 2` этот разъезд закрыт:
  - у `ComponentId=0` `actualManagedNaturalLength` совпадает с `expectedGraphvizNaturalLength` на всех logged levels
  - цепочка `K` монотонно убывает до finest level
- у managed сейчас не 1:1 `p`-path:
  - managed меняет `p` по уровням одной и той же компоненты
  - в Graphviz auto-`p` вычисляется один раз на компоненту до multilevel loop
- после `Patch 3` этот разъезд закрыт:
  - у `ComponentId=0` `repulsiveExponent = -1.8` и `repulsiveExponentSource = component_auto` на всех уровнях
  - прыжок `-1.8 -> -1 -> -1.8` исчез
- managed жёстко упирается в `MaxMultilevelDepth = 8`
  - на полном `WikiVote` это видно как `base_case=max_depth` на `437` узлах
  - у Graphviz `levels` по умолчанию фактически не ограничен таким порогом
- после `Patch 4` этот разъезд закрыт:
  - у `ComponentId=0` больше нет `base_case=max_depth`
  - managed теперь доходит до `Level=14` и останавливается по `threshold` на `39` узлах
  - следующий multilevel-разъезд теперь уже не depth, а `MultilevelThreshold = 64`
- после `Patch 5` этот разъезд тоже закрыт:
  - у `ComponentId=0` managed теперь доходит до `Level=20` и coarsest `n=4`
  - `base_case` теперь `coarsening_not_smaller`, а не `threshold`
  - coarsest path стал почти на одном уровне с Graphviz
- после `Patch 6a`:
  - explicit `OverlapRemovalHalfWidth/Height` добавлены как инструмент анализа
  - `Graphviz avg_label-size -> managed half-size` нельзя безопасно включать по умолчанию
  - этот шаг не сдвинул `mode_switch` для главной компоненты
- после `Patch 6b`:
  - canonical triangulation parity стала ближе к Graphviz
  - на full `WikiVote` это почти не изменило trace и итоговые метрики
- после `Patch 7`:
  - managed явно пишет `quadtreeMode` и `effectiveQuadtreeMode`
  - на главной компоненте full `WikiVote` default path реально идёт как `Normal`
  - coarsest levels ниже threshold корректно пишут `effectiveQuadtreeMode=None`
  - `MeanEdgeToDiagonal` и `EdgeLengthCv` слегка сдвинулись, но `mode_switch` ушёл дальше от Graphviz: `49 -> 77`
- после `Patch 7a`:
  - в `Normal` path появились graphviz-like `supernodes`
  - `quadtreeLevel` теперь адаптируется по итерациям, а не остаётся фиксированным
  - diagnostics показывают `quadtreeWork`, `nsuperAverage` и `countsAverage`
  - на главной компоненте full `WikiVote` основной quadtree mismatch почти закрыт:
    - Graphviz `mode_switch` на `iter 49`
    - managed `mode_switch` на `iter 48`
  - итоговые метрики тоже сдвинулись ближе к Graphviz:
    - `MeanEdgeToDiagonal = 0.088738`
    - `EdgeLengthCv = 0.555218`
  - следующий remaining mismatch уже сидит в `prism` после `mode_switch`:
    - Graphviz для `ComponentId=0` делает `no overlap, rescale to shrink`
    - managed для `ComponentId=0` тоже входит в shrink, но сразу возвращается с `scaleStart = 1`
    - значит текущий разъезд уже не в самом входе в shrink, а в post-switch `prism` semantics
- после `Patch 8`:
  - shrink/scaling diagnostics для managed стали достаточно подробными
  - на `ComponentId=0` теперь видно, что mismatch не в отсутствующем shrink-entry:
    - `enteredShrinkStage = true`
    - `postSwitchMaxOverlap = 0.9999983870275831`
    - `postSwitchMinOverlap = 0.0271810908276002`
    - `scaleStart = 1`
    - `scaleBest = 1`
    - `scalingIterations = 0`
    - `returnedEarlyAtScaleStart = true`
  - значит следующий remaining mismatch сидит раньше scaling search:
    - в post-switch `minOverlap`
    - и/или в `prism` model/graph semantics при `neighborhoodOnly = false`
- маленькие компоненты по `prism` уже выглядят близко к Graphviz
  - главный разъезд сидит в большой компоненте

---

## Соответствие шагов 0-7 и patch-ов

Старые секции `Шаг 0-7` ниже больше не являются рабочим планом.

Они полезны только как архив того, с какой high-level разбивки всё начиналось.
Актуальным планом считаются секции `Patch ...`.

Карта соответствия:

- `Шаг 0. Диагностика и сравнение с verbose Graphviz`
  - реализован базовой диагностикой, `Patch 0b` и подготовкой compare baseline из `Patch 1`
- `Шаг 1. Выровнять multilevel-поведение`
  - соответствует `Patch 3`, `Patch 4`, `Patch 5`
- `Шаг 2. Выровнять quadtree path`
  - соответствует `Patch 7` и `Patch 7a`
- `Шаг 3. Довести refinement / prolongation путь`
  - соответствует `Patch 2`
- `Шаг 4. Сблизить smoothing`
  - пока не начат отдельными patch-ами
- `Шаг 5. Сблизить геометрию call_tri / Delaunay / RNG`
  - соответствует `Patch 6b`
- `Шаг 6. Довести prism overlap removal`
  - соответствует `Patch 6`, `Patch 6a`, `Patch 8`
  - текущий активный шаг внутри него это `Patch 8`
- `Шаг 7. Опциональные режимы Graphviz`
  - пока не начат отдельными patch-ами

Вывод:

- как рабочие секции старые `Шаг 0-7` уже не нужны;
- как краткий архив они ещё терпимы;
- актуализировать дальше нужно только:
  - `Статус на сегодня`
  - `Что уже видно по логам`
  - `Patch ...`

---

## Архивный roadmap шагов 0-7

### Шаг 0. Добавить диагностику и сравнение с verbose Graphviz

Соответствующие patch-и:

- базовая диагностика
- `Patch 0b`
- `Patch 1` как подготовка baseline сравнения

Статус:

- выполнено
- subgraph и full прогнаны
- логов уже достаточно, чтобы переставить приоритеты следующих правок

Это первый приоритет. До правок алгоритма нужно увидеть, где именно начинается расхождение.

### 0.1. Добавить диагностический API в `PSGraphView.Sfdp`

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs`
- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpPostProcessor.cs`
- `src/PSGraphView.Sfdp/SfdpStressMajorizationSmoother.cs`
- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`

Новые файлы:

- `src/PSGraphView.Sfdp/SfdpDiagnosticsOptions.cs`
- `src/PSGraphView.Sfdp/SfdpDiagnosticEvent.cs`
- `src/PSGraphView.Sfdp/SfdpDiagnosticsWriter.cs`

Идея:

- не использовать `ILogger` внутри алгоритма
- сделать лёгкий внутренний diagnostic sink
- по умолчанию всё выключено
- при включении писать либо `jsonl`, либо текст в стиле Graphviz

Минимальный контракт:

- `Path`
- `Format`
- `IncludeIterations`
- `IncludeCoordinates`

### 0.1.1. Точный shape диагностических типов

Ниже целевой shape, который можно брать в реализацию почти без додумывания.

#### `SfdpDiagnosticFormat`

Файл:

- `src/PSGraphView.Sfdp/SfdpDiagnosticFormat.cs`

```csharp
namespace PSGraphView.Sfdp;

public enum SfdpDiagnosticFormat
{
    JsonLines,
    GraphvizLikeText
}
```

#### `SfdpDiagnosticsOptions`

Файл:

- `src/PSGraphView.Sfdp/SfdpDiagnosticsOptions.cs`

```csharp
namespace PSGraphView.Sfdp;

public sealed class SfdpDiagnosticsOptions
{
    public string? Path { get; init; }
    public SfdpDiagnosticFormat Format { get; init; } = SfdpDiagnosticFormat.JsonLines;
    public bool IncludeIterations { get; init; } = true;
    public bool IncludeCoordinates { get; init; }
    public bool FlushOnWrite { get; init; }
}
```

#### `SfdpDiagnosticEvent`

Файл:

- `src/PSGraphView.Sfdp/SfdpDiagnosticEvent.cs`

```csharp
namespace PSGraphView.Sfdp;

internal sealed record SfdpDiagnosticEvent(
    string Phase,
    string Name,
    int? ComponentId,
    int? Level,
    int? Iteration,
    IReadOnlyDictionary<string, object?> Data);
```

#### `SfdpDiagnosticsWriter`

Файл:

- `src/PSGraphView.Sfdp/SfdpDiagnosticsWriter.cs`

```csharp
using System.Globalization;
using System.Text.Json;

namespace PSGraphView.Sfdp;

internal sealed class SfdpDiagnosticsWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly StreamWriter? _writer;
    private readonly SfdpDiagnosticsOptions? _options;

    private SfdpDiagnosticsWriter(SfdpDiagnosticsOptions? options, StreamWriter? writer)
    {
        _options = options;
        _writer = writer;
    }

    public bool IsEnabled => _writer is not null && _options is not null;

    public static SfdpDiagnosticsWriter Create(SfdpDiagnosticsOptions? options)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.Path))
        {
            return new SfdpDiagnosticsWriter(options, null);
        }

        var directory = System.IO.Path.GetDirectoryName(options.Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(options.Path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream);
        return new SfdpDiagnosticsWriter(options, writer);
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    public void Write(
        string phase,
        string name,
        int? componentId = null,
        int? level = null,
        int? iteration = null,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (!_options!.IncludeIterations)
        {
            iteration = null;
        }

        var evt = new SfdpDiagnosticEvent(
            phase,
            name,
            componentId,
            level,
            iteration,
            data ?? EmptyData);

        WriteEvent(evt);
    }

    public void WriteCoordinates(
        string phase,
        string name,
        double[] x,
        double[] y,
        int? componentId = null,
        int? level = null,
        int? iteration = null)
    {
        if (!IsEnabled || !_options!.IncludeCoordinates)
        {
            return;
        }

        Write(
            phase,
            name,
            componentId,
            level,
            iteration,
            new Dictionary<string, object?>
            {
                ["x"] = x,
                ["y"] = y
            });
    }

    private void WriteEvent(SfdpDiagnosticEvent evt)
    {
        if (_options!.Format == SfdpDiagnosticFormat.JsonLines)
        {
            _writer!.WriteLine(JsonSerializer.Serialize(evt, JsonOptions));
        }
        else
        {
            _writer!.WriteLine(FormatGraphvizLike(evt));
        }

        if (_options.FlushOnWrite)
        {
            _writer.Flush();
        }
    }

    private static string FormatGraphvizLike(SfdpDiagnosticEvent evt)
    {
        var parts = new List<string>
        {
            $"phase={evt.Phase}",
            $"name={evt.Name}"
        };

        if (evt.ComponentId.HasValue)
        {
            parts.Add($"component={evt.ComponentId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (evt.Level.HasValue)
        {
            parts.Add($"level={evt.Level.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (evt.Iteration.HasValue)
        {
            parts.Add($"iter={evt.Iteration.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        foreach (var pair in evt.Data)
        {
            parts.Add($"{pair.Key}={FormatValue(pair.Value)}");
        }

        return string.Join(" ", parts);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyData =
        new Dictionary<string, object?>();
}
```

#### Изменение `SfdpOptions`

Файл:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`

Добавить:

```csharp
public SfdpDiagnosticsOptions? Diagnostics { get; init; }
```

### 0.1.2. Целевые точки использования writer

Рекомендуемый минимальный паттерн:

```csharp
using var diagnostics = SfdpDiagnosticsWriter.Create(options.Diagnostics);
diagnostics.Write(
    phase: "layout",
    name: "start",
    data: new Dictionary<string, object?>
    {
        ["nodeCount"] = indexedGraph.NodeCount,
        ["componentCount"] = components.ComponentCount,
        ["seed"] = options.Seed
    });
```

Внутри pipeline передавать writer явно параметром, а не через static / ambient state.

Целевой принцип:

- верхний слой создаёт writer
- нижние слои только вызывают `Write(...)`
- если diagnostics выключены, cost должен быть близок к нулю

### 0.2. Добавить PowerShell-параметры для диагностики

Файл:

- `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`

Параметры:

- `-SfdpDiagnosticsPath`
- `-SfdpDiagnosticsFormat`
- `-SfdpDiagnosticsIncludeIterations`

### 0.3. Научить demo сохранять verbose Graphviz и managed diagnostics рядом

Файл:

- `demos/Compare-WikiVote-Sfdp.ps1`

Нужно:

- запускать Graphviz как `sfdp -v ... 2> graphviz.verbose.log`
- передавать `SfdpDiagnosticsPath` в managed export
- сохранять пути к логам в `comparison.json`

### 0.4. Что логировать в первой версии

Минимально:

- `layout.start`
- `layout.finish`
- `multilevel.enter`
- `multilevel.coarsen`
- `multilevel.prolongate`
- `singlelevel.start`
- `singlelevel.finish`
- `overlap.start`
- `overlap.iter`
- `overlap.finish`

Ключевые поля:

- `nodeCount`
- `edgeCount`
- `componentCount`
- `seed`
- `p`
- `K`
- `step`
- `adaptiveCooling`
- `maxIterations`
- `level`
- `coarsenedTo`
- `coarsenFactor`
- `refinementNaturalLength`
- `neighborhoodOnly`
- `maxOverlap`
- `minOverlap`
- `residual`
- `shrink`

### 0.5. Что добавить во второй версии логов

- per-iteration лог в `SfdpSingleLevelLayouter`
- per-iteration лог в `SfdpStressMajorizationSmoother`

Дополнительно:

- `Fnorm`
- `iteration`
- `diff`
- `outerTolerance`
- quadtree stats, если будут доступны

### 0.6. Как использовать это для анализа

После внедрения шага 0 сравнивать рядом:

- `graphviz.verbose.log`
- `managed.diagnostics.jsonl`

Сначала искать первое место, где расходятся:

- effective control
- включение multilevel
- число уровней
- `K`
- refinement path
- prism scaling
- overlap removal

---

### Шаг 1. Выровнять multilevel-поведение с Graphviz

Соответствующие patch-и:

- `Patch 3`
- `Patch 4`
- `Patch 5`

Сейчас это главный источник расхождения на малых графах, особенно на `subgraph-30`.

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`

Что сделать:

- убрать или сильно ослабить `MultilevelThreshold = 64`
- приблизить поведение к graphviz `levels`
- проверить, что малые графы тоже идут через coarsening, если graphviz делает то же самое

Проверка:

- `Compare-WikiVote-Sfdp.ps1 -UseSubgraph -SubgraphSeedCount 30`
- сравнить verbose / diagnostics и итоговые метрики

---

### Шаг 2. Выровнять quadtree path

Соответствующие patch-и:

- `Patch 7`
- `Patch 7a`

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpQuadTree.cs`

Что сделать:

- вместо одного пути `UseBarnesHut` добавить режимы ближе к Graphviz:
  - `none`
  - `normal`
  - `fast`
  - `hybrid`
- по возможности приблизить выбор режима к размеру графа
- затем добавить более близкий подбор параметров quadtree

Проверка:

- полный WikiVote
- сравнение `Fnorm`, `step`, времени и итоговых метрик

---

### Шаг 3. Довести refinement / prolongation путь до Graphviz

Соответствующие patch-и:

- `Patch 2`

Файлы:

- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpTransferOperator.cs`

Проверить и выровнять:

- `random_start = false` после подъёма
- `K *= 0.75`
- `adaptiveCooling = false`
- `step = 0.1`
- jitter после prolongation
- порядок применения prolongation и refinement

Проверка:

- diagnostics по каждому уровню
- сравнение с verbose Graphviz на одинаковом seed

---

### Шаг 4. Сблизить smoothing

Соответствующие patch-и:

- пока нет отдельных patch-ей

Файлы:

- `src/PSGraphView.Sfdp/SfdpPostProcessor.cs`
- `src/PSGraphView.Sfdp/SfdpStressMajorizationSmoother.cs`

Что сделать:

- заменить текущий `Spring` smoothing на отдельный path, близкий к Graphviz
- затем добавить:
  - `Triangle`
  - `Rng`

Сейчас в managed:

- `Spring` это повторный запуск single-level layout

В Graphviz:

- `Spring` это другой post-process solver

Проверка:

- unit tests на deterministic behavior
- differential comparison на нескольких фиксированных графах

---

### Шаг 5. Сблизить геометрию `call_tri` / Delaunay / RNG

Соответствующие patch-и:

- `Patch 6b`

Файлы:

- `src/PSGraphView.Sfdp/Prism/SfdpPrismTriangulationBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpCallTri.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpUgGraphBuilder.cs`

Что сделать:

- проверить расхождения edge set между текущей реализацией и Graphviz
- по возможности приблизить поведение к:
  - `call_tri`
  - `call_tri2`
  - `UG_graph`

Проверка:

- тесты на одинаковый набор рёбер для фиксированных наборов точек
- отдельные кейсы:
  - дубликаты
  - коллинеарность
  - почти вырожденные конфигурации

---

### Шаг 6. Довести prism overlap removal

Соответствующие patch-и:

- `Patch 6`
- `Patch 6a`
- `Patch 8`

Файлы:

- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismModelBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismProximityGraphBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismBoxBuilder.cs`

Что сделать:

- выровнять scaling перед overlap removal
- выровнять `avgEdgeLength` и `avgLabelSize` path
- проверить neighborhood-only phase
- проверить shrink path
- затем приблизить входные размеры узлов к реальным размерам Graphviz, а не только к `NodeRadius`

Проверка:

- сравнение по verbose / diagnostics
- сравнение overlap-итераций по `maxOverlap`, `minOverlap`, `residual`

---

### Шаг 7. Опциональные режимы Graphviz, если нужна реально полная parity

Соответствующие patch-и:

- пока нет отдельных patch-ей

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- overlap / smoothing / layout pipeline по необходимости

Что сюда относится:

- `label_scheme`
- edge-label nodes
- special penalty paths
- дополнительные режимы, которые не участвуют в текущем demo, но есть в Graphviz

Это не первый приоритет, пока не будет сближения на основном path.

---

## Исторический порядок внедрения

1. Шаг 0: диагностика и сравнение с `sfdp -v`
2. Patch 0b: дологировать missing states, которые всё ещё не видны
3. Patch 1: выровнять compare harness и runtime control
4. Patch 2: исправить `K`-path в multilevel/refinement
5. Patch 3: исправить auto-`p` semantics
6. Patch 4: снять искусственный `max_depth` как stop condition
7. Patch 5: снять искусственный `MultilevelThreshold = 64`
8. Patch 6a: выровнять box/padding semantics на входе в `prism`
9. Patch 6b: выровнять proximity / triangulation path для `prism`
10. Patch 7: выровнять quadtree mode/path
11. Шаг 4: smoothing
12. Шаг 7: optional parity features

---

## Минимальный первый patch

Первый patch должен быть только про диагностику, без изменения алгоритма.

Включить:

- новый diagnostic API
- логирование в layout / multilevel / single-level / overlap
- PowerShell-параметры
- запись `graphviz.verbose.log` и `managed.diagnostics.jsonl` из demo

Это даст базу для точечного сравнения дальше.

---

## Проверка после шага 0

Прогонять:

1. `Compare-WikiVote-Sfdp.ps1 -UseSubgraph -SubgraphSeedCount 30`
2. `Compare-WikiVote-Sfdp.ps1` на полном WikiVote

Сравнивать сначала:

- effective `p`
- включился ли multilevel
- число уровней
- `K` на coarse/fine уровнях
- refinement path
- overlap scaling
- `maxOverlap`

Если расхождение видно уже там, дальше правки алгоритма будут намного точнее и дешевле.

---

## Очень конкретный patch-plan после разбора логов

Ниже не абстрактные этапы, а прямой рабочий порядок: что менять первым, где именно, и что должно поменяться в логах после каждого patch.

### Patch 0b. Дологировать missing states

Статус:

- выполнено в коде
- проверено для managed
- локальный `graphviz` собран и прогнан через compare-скрипт

Цель:

- перестать гадать по косвенным признакам там, где текущих логов ещё не хватает

Файлы:

- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismRelaxationSolver.cs`
- локально для анализа: `../graphviz/lib/sfdpgen/spring_electrical.c`

Что добавить:

- в managed:
  - `singlelevel.finish.terminationReason`
  - `singlelevel.control.requestedP`, `effectiveP`, `pSource`
  - `multilevel.refine.previousK`, `expectedGraphvizK`, `actualManagedK`
  - `overlap.finish.terminationReason`
  - `overlap.finish.finalMaxOverlap`, `finalMinOverlap`
  - реальный `overlapResidual`, а не placeholder `0` / `100000`
- в локальном Graphviz verbose:
  - effective `p` после AUTOP resolve
  - реальный `level / n` на каждом уровне
  - `K`, `step`, `adaptive_cooling`, `random_start` после каждого `prolongate`

Что должно измениться в логах после patch:

- в managed исчезнут бессмысленные `residual = 100000`
- у `ComponentId=0` будет прямо видно `terminationReason = max_iterations`
- в Graphviz появятся реальные уровни, а не только `max levels`
- станет возможным честно сравнить final/effective `p` и `K`, а не только их запрос

Что уже изменилось после patch:

- в managed `singlelevel.finish` теперь пишет `terminationReason`
- в managed `singlelevel.control` теперь пишет `requestedRepulsiveExponent`, `repulsiveExponent`, `repulsiveExponentSource`
- в managed `multilevel.prolongate` и `multilevel.refine` теперь видны `previousNaturalLength`, `expectedGraphvizNaturalLength`, `actualManagedNaturalLength`
- `SfdpPrismRelaxationSolver` теперь возвращает `Changed + Residual`
- в managed `overlap.iter` и `overlap.finish` теперь пишутся реальный `residual`, `terminationReason`, `finalMaxOverlap`, `finalMinOverlap`
- в локальном `spring_electrical.c` добавлен verbose для:
  - effective `p` после AUTOP resolve
  - `multilevel level=...`
  - `level result ...`
  - `refine setup next_level=...`

Что ещё нужно сделать после patch:

- использовать эти verbose-логи как baseline для следующих algorithmic patches

### Patch 1. Выровнять compare harness и runtime control

Статус:

- выполнено
- полный `WikiVote` перепрогнан через локальный `sfdp`

Цель:

- убрать шум от заведомо разных runtime-параметров до правок алгоритма

Файлы:

- `demos/Compare-WikiVote-Sfdp.ps1`

Что править:

- передавать Graphviz тот же `seed`, что и managed:
  - через `-Gstart=$SfdpSeed` или эквивалентный `start=<seed>`
- сделать budget overlap removal сравнимым:
  - явно передавать managed `-SfdpOverlapRemovalIterations 1000`
  - не оставлять implicit default `32`, когда Graphviz реально идёт с `overlap=1000`
- явно фиксировать режимы сравнения в одном месте скрипта, а не надеяться на дефолты

Что должно измениться в логах после patch:

- в Graphviz verbose строка с seed должна совпасть с managed `layout.start.seed`
- у managed для `ComponentId=0` overlap больше не должен заканчиваться на `32`
- в managed должен появиться `mode_switch` и потом `shrink` на большой компоненте
- если даже при `1000` итерациях shrink не появится, значит проблема уже в алгоритме prism, а не в budget

Что уже изменилось после patch:

- в локальном Graphviz verbose теперь `random start 1 seed 42`
- в summary `Graphviz.Seed` и `Managed.Seed` совпадают
- в summary `Graphviz.OverlapRemovalIterations` и `Managed.OverlapRemovalIterations` совпадают и равны `1000`
- у managed для `ComponentId=0` теперь:
  - `overlap.start.maxIterations = 1000`
  - есть `mode_switch` на `Iteration = 55`
  - есть `shrink` на `Iteration = 56`
  - overlap больше не обрывается на старых `32` итерациях

Вывод после patch:

- главный разъезд по overlap budget убран
- текущий следующий реальный разъезд уже не в harness, а в алгоритме:
  - managed всё ещё останавливается на `max_depth = 8`
  - managed `K` path всё ещё не как в Graphviz
  - managed `p` path всё ещё надо довести до single effective value per component

### Patch 2. Исправить `K`-path в multilevel/refinement

Статус:

- выполнено
- полный `WikiVote` перепрогнан на свежей сборке
- unit-тесты зелёные

Цель:

- сделать refinement по `K` таким же, как в Graphviz

Файлы:

- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`

Что править:

- перестать вычислять refinement `K` как `EstimateNaturalLength(fineGraph, fineX, fineY) * 0.75`
- передавать `K` вниз по уровням как состояние:
  - coarse solve вернул `K`
  - следующий finer level берёт `previousK * 0.75`
- при необходимости расширить `SfdpSingleLevelLayoutResult`, чтобы `K` удобно протаскивался дальше

Что должно измениться в логах после patch:

- у `ComponentId=0` последовательность `K` станет монотонно уменьшаться
- `actualManagedK` начнёт совпадать с `expectedGraphvizK`
- исчезнет текущая аномалия, где `K` на finer levels растёт до `2.143...`
- у `overlap.scale_to_edge_length` для главной компоненты scale factor должен заметно вырасти, потому что pre-overlap layout станет компактнее

Что уже изменилось после patch:

- у `ComponentId=0` в managed diagnostics:
  - `previousNaturalLength -> expectedGraphvizNaturalLength -> actualManagedNaturalLength` теперь совпадают на каждом `multilevel.prolongate`
  - цепочка идёт как `0.50297 -> 0.37723 -> 0.28292 -> ... -> 0.05035`
- `overlap.scale_to_edge_length` для главной компоненты заметно поменялся:
  - до patch: `averageEdgeLength ~= 3.34794`, `scaleFactor ~= 6.4995`
  - после patch: `averageEdgeLength ~= 0.22416`, `scaleFactor ~= 97.0755`
- overlap-path для главной компоненты стал ближе к Graphviz по форме:
  - Graphviz: switch на `iter 49`
  - managed: switch на `iter 80`
  - managed теперь стабильно доходит до `finish` с `terminationReason = converged`
- итоговые метрики полного `WikiVote` после patch:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.076812`
  - Graphviz `EdgeLengthCv = 0.555466`
  - Managed `EdgeLengthCv = 0.543088`

Вывод после patch:

- `K`-path больше не главный разъезд
- следующий подтверждённый разъезд теперь:
  - auto-`p` semantics
  - искусственный `max_depth = 8`
  - затем уже более тонкая доводка `prism`

### Patch 3. Исправить auto-`p` semantics

Статус:

- выполнено
- полный `WikiVote` перепрогнан на свежей сборке
- unit-тесты зелёные

Цель:

- перестать менять `p` по уровням внутри одной компоненты

Файлы:

- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`

Что править:

- если `options.RepulsiveExponent` не задан:
  - вычислять auto-`p` один раз на finest graph компоненты
  - дальше передавать одно effective значение через весь multilevel path
- не пересчитывать `p` отдельно для каждого coarse/fine solve

Что должно измениться в логах после patch:

- у одной компоненты `effectiveP` станет постоянным на всех уровнях
- исчезнет текущий прыжок `-1.8 -> -1 -> -1.8`
- если Graphviz local verbose тоже будет дологирован, effective `p` можно будет проверить напрямую

Что уже изменилось после patch:

- в managed diagnostics появился `component.control` с:
  - `requestedRepulsiveExponent = null`
  - `repulsiveExponent = -1.8`
  - `repulsiveExponentSource = component_auto`
  - `powerLawGraph = true`
- у `ComponentId=0` все `singlelevel.control` теперь пишут:
  - `repulsiveExponent = -1.8`
  - `repulsiveExponentSource = component_auto`
- прыжок `-1.8 -> -1 -> -1.8`, который был до patch, исчез
- на полном `WikiVote` итоговые метрики после patch:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.077609`
  - Graphviz `EdgeLengthCv = 0.555466`
  - Managed `EdgeLengthCv = 0.545195`
- для главной компоненты overlap-path слегка сдвинулся к Graphviz:
  - Graphviz: `mode_switch` на `iter 49`
  - Patch 2: managed `mode_switch` на `iter 80`
  - Patch 3: managed `mode_switch` на `iter 74`
  - `terminationReason` остаётся `converged`

Вывод после patch:

- auto-`p` больше не главный разъезд
- следующий подтверждённый разъезд теперь:
  - искусственный `max_depth = 8`
  - затем доводка `prism` для главной компоненты

### Patch 4. Снять искусственный `max_depth` как stop condition

Статус:

- выполнено
- полный `WikiVote` перепрогнан на свежей сборке
- unit-тесты зелёные

Цель:

- не обрезать multilevel слишком рано на больших графах

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`

Что править:

- убрать жёсткий дефолт `MaxMultilevelDepth = 8` как главный стопор
- заменить на Graphviz-like поведение:
  - глубина определяется coarsening path
  - остановка по `coarsening_not_smaller` / минимальному размеру / explicit user limit
- если лимит нужен, делать его opt-in, а не главным дефолтом

Что должно измениться в логах после patch:

- у `ComponentId=0` больше не будет `base_case=max_depth` на `437` узлах
- появятся дополнительные `multilevel.enter/coarsen` ниже текущего `Level=8`
- если Graphviz levels тоже дологированы, уровни и coarse sizes станут сравнимыми

Что уже изменилось после patch:

- default `maxMultilevelDepth` теперь равен `2147483647`, а не `8`
- у `ComponentId=0` managed теперь идёт так:
  - раньше: stop по `max_depth` на `Level=8`, `nodeCount=437`
  - после patch: coarsen до `Level=14`, stop по `threshold` на `nodeCount=39`
- Graphviz local verbose для той же компоненты всё ещё идёт глубже:
  - coarsest path до `Level=19`, `n=4`
- итоговые метрики полного `WikiVote` после patch:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.080252`
  - Graphviz `EdgeLengthCv = 0.555466`
  - Managed `EdgeLengthCv = 0.540112`
- overlap-path для главной компоненты после patch:
  - Graphviz: `mode_switch` на `iter 49`
  - Patch 3: managed `mode_switch` на `iter 74`
  - Patch 4: managed `mode_switch` на `iter 89`
  - `terminationReason` остаётся `converged`

Вывод после patch:

- `max_depth = 8` больше не блокирует multilevel
- следующий подтверждённый ранний разъезд теперь:
  - `MultilevelThreshold = 64`
  - и уже после него поведение `prism` для главной компоненты

### Patch 5. Снять искусственный `MultilevelThreshold = 64`

Статус:

- выполнено
- полный `WikiVote` перепрогнан на свежей сборке
- unit-тесты зелёные

Цель:

- убрать раннюю остановку multilevel по дефолтному threshold и приблизить default path к Graphviz

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`

Что править:

- заменить дефолтный `MultilevelThreshold = 64` на Graphviz-like default, который не режет coarsening слишком рано
- сохранить сам threshold как явный user override
- обновить docs и help, чтобы новый default был виден снаружи

Что должно измениться в логах после patch:

- у `ComponentId=0` больше не будет `base_case=threshold` на `n=39`
- coarsest path должен дойти примерно до тех же маленьких уровней, что и Graphviz
- после этого можно уже честно сравнивать `prism`, а не последствия ранней остановки multilevel

Что уже изменилось после patch:

- default `MultilevelThreshold` теперь равен `4`, а не `64`
- у `ComponentId=0` managed теперь идёт так:
  - раньше: stop по `threshold` на `Level=14`, `nodeCount=39`
  - после patch: coarsen до `Level=20`, `base_case=coarsening_not_smaller`, `coarseNodeCount=4`
- Graphviz local verbose для той же компоненты идёт до `Level=19`, `n=4`
- итоговые метрики полного `WikiVote` после patch:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.088809`
  - Graphviz `EdgeLengthCv = 0.555466`
  - Managed `EdgeLengthCv = 0.555673`
- overlap-path для главной компоненты после patch:
  - Graphviz: `mode_switch` на `iter 49`
  - Patch 4: managed `mode_switch` на `iter 89`
  - Patch 5: managed `mode_switch` на `iter 61`
  - `terminationReason` остаётся `converged`

Вывод после patch:

- multilevel default path теперь очень близок к Graphviz
- следующим главным разъездом снова становится `prism` для главной компоненты

### Patch 6. Довести `prism` для главной компоненты

Цель:

- приблизить главный runtime path, который сейчас сильнее всего расходится на full `WikiVote`

Файлы:

- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismModelBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismProximityGraphBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismRelaxationSolver.cs`

Что править:

- после выравнивания budget и `K` сравнить shape overlap trace именно для `ComponentId=0`
- если managed всё ещё приходит в shrink слишком поздно или не приходит:
  - смотреть определение `maxOverlap`
  - смотреть proximity / overlap graph
  - смотреть smoothing residual
- довести завершение цикла до Graphviz-like:
  - сначала `neighbors_only`
  - потом `mode_switch`
  - потом shrink
  - потом финал с `neighborhoodOnly=false`

Что должно измениться в логах после patch:

- для `ComponentId=0` overlap trace станет похож на Graphviz:
  - длинная neighbors-only фаза
  - затем `mode_switch`
  - затем `shrink`
  - затем `finish` без overlap
- `overlap.finish.terminationReason` должен стать `converged`, а не `max_iterations`

Что реально сделано:

- в `src/PSGraphView.Sfdp/Prism/SfdpPrismRelaxationSolver.cs` добавлен graphviz-like perturbation для exact `dist == 0`
- `Random` прокинут из layout pipeline до `prism`, чтобы perturbation был seed-driven
- в `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs` в diagnostics добавлен `zeroDistancePerturbations`
- добавлен тест на coincident pair в `tests/PSGraphView.Sfdp.Tests/SfdpPrismRelaxationSolverTests.cs`

Что показал полный `WikiVote`:

- артефакты:
  - `/tmp/psgraphview-sfdp-full-patch6-localgv/wiki-vote-full-graphviz.verbose.log`
  - `/tmp/psgraphview-sfdp-full-patch6-localgv/wiki-vote-full-managed.diagnostics.jsonl`
  - `/tmp/psgraphview-sfdp-full-patch6-localgv/wiki-vote-full-comparison.json`
- метрики не изменились относительно `Patch 5`:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.088809`
  - Graphviz `EdgeLengthCv = 0.555466`
  - Managed `EdgeLengthCv = 0.555673`
- `ComponentId=0` всё ещё идёт:
  - `mode_switch` на `iter 61`
  - `shrink` на `iter 62`
  - `finish` на `iter 63`
- `zeroDistancePerturbations = 0` на всех overlap-итерациях, включая главную компоненту

Вывод после patch:

- exact-zero-distance branch не участвует в главном разъезде full `WikiVote`
- следующий наиболее вероятный разъезд внутри `prism`:
  - либо в box semantics / padding semantics на входе в overlap
  - либо в proximity graph / triangulation path

### Patch 6a. Выровнять box/padding semantics на входе в `prism`

Почему это теперь отдельный шаг:

- compare harness всё ещё подаёт в Graphviz и managed неэквивалентные размеры узлов для overlap path
- в `demos/Compare-WikiVote-Sfdp.ps1` сейчас:
  - Graphviz: `-Nwidth=0.02 -Nheight=0.02 -Gsep=+4`
  - managed: `-NodeRadius 0.72 -SfdpOverlapRemovalPadding 4`
- это уже само по себе делает вход в `prism` не 1:1

Что править:

- сначала сравнить semantics размеров между:
  - `graphviz/lib/neatogen/adjust.c:getSizes`
  - `src/PSGraphView.Sfdp/Prism/SfdpPrismBoxBuilder.cs`
  - `demos/Compare-WikiVote-Sfdp.ps1`
- затем решить, где именно чинить mismatch:
  - либо в demo harness
  - либо в managed box builder
  - либо в обоих местах, если сейчас mixed units
- после правки перепрогнать full `WikiVote`

Что должно измениться в логах после patch:

- `averageLabelSize` у managed должен стать заметно ближе к Graphviz
- стартовый `scale_to_edge_length` для `ComponentId=0` должен приблизиться к Graphviz по смыслу, а не только по факту применения
- `mode_switch` на главной компоненте должен сдвинуться ближе к Graphviz `iter 49`

Что реально сделано:

- в `src/PSGraphView.Sfdp/SfdpOptions.cs` добавлены `OverlapRemovalHalfWidth` и `OverlapRemovalHalfHeight`
- эти override'ы прокинуты через `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs`
- в `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs` добавлены `boxSource`, `boxHalfWidth`, `boxHalfHeight`
- в `demos/Compare-WikiVote-Sfdp.ps1` теперь парсится `Graphviz AverageLabelSize` и в summary пишется suggested managed half-size

Что показал прогон:

- экспериментальный прогон с автоприменением suggested half-size:
  - `/tmp/psgraphview-sfdp-full-patch6ab-localgv/wiki-vote-full-comparison.json`
  - сделал `averageLabelSize` у managed близким к Graphviz
  - но не изменил `mode_switch` для `ComponentId=0`
  - и ухудшил итоговую packed-метрику `MeanEdgeToDiagonal`
- итоговый default compare-path оставлен без автоприменения override:
  - `/tmp/psgraphview-sfdp-full-patch6ab-final-localgv/wiki-vote-full-comparison.json`

Вывод после patch:

- explicit box override полезен как инструмент анализа и для future parity
- но `Graphviz avg_label-size -> managed half-size` нельзя просто включить по умолчанию в current harness
- главный runtime mismatch для `ComponentId=0` этот шаг не закрыл

### Patch 6b. Выровнять proximity / triangulation path для `prism`

Почему это теперь отдельный шаг:

- после `Patch 6` стало понятно, что exact `dist == 0` branch не влияет на главный runtime path
- следующий сильный кандидат уже не solver, а геометрия proximity graph
- Graphviz строит этот path через `call_tri` / `delaunay_tri`
- managed сейчас идёт через `SfdpCallTri` / `SfdpPrismTriangulationBuilder`

Файлы:

- `src/PSGraphView.Sfdp/Prism/SfdpCallTri.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismTriangulationBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismProximityGraphBuilder.cs`
- при необходимости `src/PSGraphView.Sfdp/Prism/SfdpUgGraphBuilder.cs`

Что править:

- сравнить edge set proximity graph на фиксированных наборах точек с Graphviz `call_tri`
- если edge set расходится, сначала чинить triangulation path, а не overlap solver
- добавить differential tests на small fixed point sets и на коллинеарный fallback

Что должно измениться в логах после patch:

- `proximityEdgeCount` и shape overlap-trace на `ComponentId=0` станут ближе к Graphviz
- `mode_switch` и `shrink` для главной компоненты должны ещё приблизиться к Graphviz
- если это действительно главный разъезд, full `WikiVote` должен сдвинуться ближе и по trace, и по метрикам

Что реально сделано:

- через локальный helper против `graphviz/lib/neatogen/delaunay.c` сняты эталонные edge sets для canonical point sets
- найден реальный mismatch на co-circular square:
  - Graphviz выбирал diagonal `0-2`
  - managed выбирал diagonal `1-3`
- в `src/PSGraphView.Sfdp/Prism/SfdpPrismTriangulationBuilder.cs` исправлен tie-break:
  - points on circumcircle теперь трактуются как `outside`
- добавлены tests:
  - `tests/PSGraphView.Sfdp.Tests/SfdpPrismTriangulationBuilderTests.cs`
  - tightened square expectation в `tests/PSGraphView.Sfdp.Tests/SfdpPrismProximityGraphBuilderTests.cs`

Что показал полный `WikiVote`:

- `/tmp/psgraphview-sfdp-full-patch6ab-final-localgv/wiki-vote-full-comparison.json`
- full-метрики и `mode_switch` для `ComponentId=0` не изменились относительно `Patch 5/6`

Вывод после patch:

- triangulation parity на canonical cases стала ближе к Graphviz
- но это не главный remaining mismatch для full `WikiVote`
- на тот момент следующим формальным шагом плана был `Patch 7`

### Patch 7. Выровнять quadtree mode/path

Цель:

- приблизить single-level solver path к Graphviz `NORMAL/FAST/HYBRID`

Файлы:

- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpQuadTree.cs`

Что править:

- ввести режимы вместо одного `UseBarnesHut`
- начать с default, эквивалентного Graphviz `NORMAL`
- затем отдельно добивать `FAST` и `HYBRID`

Что должно измениться в логах после patch:

- managed начнёт явно писать `quadtreeMode`
- станет видно, какой path реально использован на каждом уровне
- force-итерации (`Fnorm`, `step`) станут ближе к Graphviz после того, как Graphviz тоже будет дологирован по уровням

Что реально сделано:

- в `src/PSGraphView.Sfdp/SfdpQuadtreeMode.cs` добавлен enum `None/Normal/Fast/Hybrid`
- в `src/PSGraphView.Sfdp/SfdpOptions.cs` добавлены:
  - `QuadtreeMode`
  - `QuadtreeHybridThreshold`
  - `QuadtreeMaxDepth`
- `BarnesHutThreshold` default изменён с `64` на `45`, ближе к Graphviz `quadtree_size`
- в `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`:
  - добавлен explicit resolve `configured -> effective quadtree mode`
  - `Fast` реализован как batched single-level iteration
  - в diagnostics теперь пишутся `quadtreeMode`, `effectiveQuadtreeMode`, `quadtreeMaxDepthUsed`
- в `src/PSGraphView.Sfdp/SfdpQuadTree.cs` quadtree теперь принимает `maxDepth` и пишет фактическую глубину
- в `src/PSGraphView.PowerShell/ExportGraphViewCmdlet.cs` добавлены параметры:
  - `-SfdpQuadtreeMode`
  - `-SfdpQuadtreeHybridThreshold`
  - `-SfdpQuadtreeMaxDepth`
  - старый `-DisableSfdpBarnesHut` сохранён как alias к `None`
- добавлены тесты:
  - `Hybrid -> Normal`
  - `Hybrid -> Fast`
  - cmdlet surface и diagnostics

Что показал полный `WikiVote`:

- `/tmp/psgraphview-sfdp-full-patch7-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch7-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch7-localgv/wiki-vote-full-graphviz.verbose.log`
- Graphviz:
  - `MeanEdgeToDiagonal = 0.094565`
  - `EdgeLengthCv = 0.555466`
- Managed:
  - `MeanEdgeToDiagonal = 0.085979`
  - `EdgeLengthCv = 0.554829`
- на главной компоненте:
  - Graphviz `mode_switch` на `iter 49`
  - managed `mode_switch` на `iter 77`
  - managed на рабочих уровнях действительно использует `effectiveQuadtreeMode=Normal`
  - на coarsest уровнях ниже порога пишет `effectiveQuadtreeMode=None`

Вывод после patch:

- `Patch 7` дал нужную управляемость и наблюдаемость quadtree path
- default path стал ближе к Graphviz по surface и diagnostics
- но это не закрыл главный runtime mismatch на full `WikiVote`
- следующий шаг теперь уже не "добавить quadtree mode", а добивать parity внутри `Normal` path:
  - closer supernode semantics
  - подбор `max_qtree_level`
  - затем снова смотреть `prism` trace на главной компоненте

### Patch 7a. Добить graphviz-like `NORMAL` internals

Цель:

- приблизить managed `Normal` path к Graphviz не по surface, а по внутренней механике
- убрать remaining mismatch в single-level repulsion до входа в `prism`

Файлы:

- `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpQuadTree.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`

Что править:

- добавить в quadtree path graphviz-like `GetSupernodes(...)`
- считать repulsion в `Normal` path через supernode-буфер, а не через обычный Barnes-Hut accumulate
- ввести optimizer для `quadtreeLevel`, чтобы он подстраивался по работе дерева, как в Graphviz
- добавить diagnostics:
  - `quadtreeLevel`
  - `quadtreeWork`
  - `nsuperAverage`
  - `countsAverage`

Что реально сделано:

- в `src/PSGraphView.Sfdp/SfdpQuadTree.cs`:
  - добавлен `SfdpQuadTreeSupernodeBuffer`
  - добавлен `GetSupernodes(...)`
  - добавлен graphviz-like traversal с накоплением `TraversalCount`
- в `src/PSGraphView.Sfdp/SfdpSingleLevelLayouter.cs`:
  - добавлен `SfdpQuadtreeLevelOptimizer`
  - `Normal` path теперь считает repulsion через `GetSupernodes(...)`
  - `quadtreeLevel` теперь адаптируется по итерациям
  - diagnostics теперь пишут `quadtreeWork`, `nsuperAverage`, `countsAverage`
- в `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`:
  - добавлен тест, который проверяет, что `Normal` path пишет supernode diagnostics и меняет `quadtreeLevel`

Что показал полный `WikiVote`:

- `/tmp/psgraphview-sfdp-full-patch7a-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch7a-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch7a-localgv/wiki-vote-full-graphviz.verbose.log`
- Graphviz:
  - `ElapsedMilliseconds = 7747`
  - `MeanEdgeToDiagonal = 0.094565`
  - `EdgeLengthCv = 0.555466`
- Managed:
  - `ElapsedMilliseconds = 74676`
  - `MeanEdgeToDiagonal = 0.088738`
  - `EdgeLengthCv = 0.555218`
- на главной компоненте:
  - Graphviz `mode_switch` на `iter 49`
  - managed `mode_switch` на `iter 48`
  - managed на рабочих уровнях теперь пишет осмысленные `quadtreeLevel`, `quadtreeWork`, `nsuperAverage`, `countsAverage`

Вывод после patch:

- `Patch 7a` почти закрыл главный remaining mismatch в `Normal` quadtree path
- `mode_switch` на большой компоненте теперь практически совпадает с Graphviz
- time и итоговые метрики заметно ближе к Graphviz, чем после `Patch 7`
- следующий практический шаг теперь уже не внутри quadtree, а снова в `prism`:
  - сравнить scaling / shrink path после `mode_switch`
  - смотреть финальный overlap trace на `ComponentId=0`

### Patch 8. Добить `prism` scaling / shrink parity

Статус:

- частично реализовано
- diagnostics и unit-тесты добавлены
- полный `WikiVote` перепрогнан на свежей сборке

Цель:

- приблизить managed `prism` к Graphviz именно на участке после `mode_switch`
- сделать shrink/scaling path на главной компоненте ближе к `overlap.c`

Почему именно это сейчас:

- после `Patch 7a` `mode_switch` уже почти совпал:
  - Graphviz `iter 49`
  - managed `iter 48`
- но дальше trace ещё не 1:1:
  - Graphviz для `ComponentId=0` делает `no overlap, rescale to shrink`
  - managed для `ComponentId=0` тоже входит в shrink, но сразу возвращается с `scaleStart = 1`
  - bracket-итерации у Graphviz хорошо видны на малых компонентах, но не на главной
- значит главный remaining mismatch сейчас уже не в single-level path и не в самом факте shrink-entry, а в post-switch `prism` semantics

Файлы:

- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpPrismOverlapRemoverTests.cs`
- при необходимости:
  - `src/PSGraphView.Sfdp/Prism/SfdpPrismModelBuilder.cs`
  - `src/PSGraphView.Sfdp/Prism/SfdpPrismStressSystem.cs`

Что править первым:

1. Дологировать shrink/scaling path так, чтобы его можно было сравнивать с Graphviz построчно.
   Нужно писать:
   - `enteredShrinkStage`
   - `scaleStart`
   - `scaleStop`
   - `scaleBest`
   - `scalingIterations`
   - `hasOverlapAtScaleStart`
   - `hasOverlapAtScaleStop`
   - `postSwitchMaxOverlap`
   - `postSwitchMinOverlap`

2. Проверить, действительно ли на главной компоненте нужен shrink bracket.
   После частичной реализации уже видно, что managed входит в shrink и сразу возвращается на `scaleStart = 1`.
   Значит дальше надо сравнивать не сам bisection, а post-switch `minOverlap` и состав графа/модели.

3. Сблизить post-switch `prism` model/graph semantics.
   Нужно сравнить:
   - `SfdpPrismModelBuilder`
   - `SfdpPrismOverlapGraphBuilder`
   - и первую non-neighbor итерацию после `mode_switch`
   Именно там сейчас остаётся разъезд по `minOverlap`.

4. Добавить регрессионные тесты именно на этот путь.
   Нужны тесты, где:
   - после снятия overlap остаётся underlap
   - shrink действительно двигает scale bracket
   - diagnostics пишут bracket iterations и final chosen scale

Что реально сделано:

- в `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`:
  - добавлены `enteredShrinkStage`
  - добавлены `postSwitchMaxOverlap` и `postSwitchMinOverlap`
  - добавлены `scaleBest`, `finalScaleStart`, `finalScaleStop`, `scalingIterations`
  - добавлены `hasOverlapAtScaleStart`, `hasOverlapAtScaleStop`, `returnedEarlyAtScaleStart`
  - shrink теперь входит по факту отсутствия overlap-ребер
- в `tests/PSGraphView.Sfdp.Tests/SfdpPrismOverlapRemoverTests.cs`:
  - добавлен тест на shrink diagnostics

Что показал полный `WikiVote`:

- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-comparison.json`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-managed.diagnostics.jsonl`
- `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-graphviz.verbose.log`
- итоговые метрики не изменились относительно `Patch 7a`:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.088738`
  - Graphviz `EdgeLengthCv = 0.555466`
  - Managed `EdgeLengthCv = 0.555218`
- на главной компоненте:
  - `enteredShrinkStage = true`
  - `postSwitchMaxOverlap = 0.9999983870275831`
  - `postSwitchMinOverlap = 0.0271810908276002`
  - `scaleStart = 1`
  - `scaleBest = 1`
  - `scalingIterations = 0`
  - `returnedEarlyAtScaleStart = true`

Вывод после частичной реализации:

- `Patch 8` уже закрыл слепую зону в shrink/scaling diagnostics
- теперь видно, что на `ComponentId=0` mismatch не в том, что shrink не стартует
- mismatch сидит раньше:
  - в post-switch `minOverlap`
  - и/или в `prism` model/graph semantics при `neighborhoodOnly = false`
- следующий шаг внутри `Patch 8`:
  - сравнить первую non-neighbor итерацию между Graphviz и managed
  - затем править `SfdpPrismModelBuilder` / `SfdpPrismOverlapGraphBuilder`, а не сам bisection

### Patch 8a. Сравнить первую non-neighbor итерацию 1:1

Статус:

- реализовано
- managed и Graphviz verbose выровнены по сопоставимым агрегатам
- полный `WikiVote` перепрогнан через локальный `sfdp`

Цель:

- локализовать разъезд в первой итерации после `mode_switch` на главной компоненте
- понять, почему у Graphviz `post-switch underlap` существенно ниже, чем у managed

Почему именно это следующий шаг:

- `Patch 8` уже показал, что shrink-entry у managed не потерян
- на `ComponentId=0` shrink входит и сразу возвращается с `scaleStart = 1`
- значит следующий разъезд сидит раньше:
  - в составе графа для первой non-neighbor итерации
  - и/или в `ideal distance` / weight semantics

Файлы:

- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismModelBuilder.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs`
- при необходимости:
  - `src/PSGraphView.Sfdp/Prism/SfdpPrismProximityGraphBuilder.cs`
  - `../graphviz/lib/neatogen/overlap.c`

Что сделано:

1. В managed добавлен детальный `overlap.model`.
   Для каждой overlap-итерации теперь пишутся:
   - `combinedEdgeCount`
   - `expandEdgeCount`
   - `shrinkEdgeCount`
   - `overlapEdgeCount`
   - `proximityEdgeCount`
   - `proximityOnlyEdgeCount`
   - `overlapOnlyEdgeCount`
   - `sharedEdgeCount`
   - `maxOverlapFactor`
   - `minOverlapFactor`

2. В локальный Graphviz добавлен verbose для того же места.
   В `OverlapSmoother_new(...)` теперь пишутся:
   - `proximity_edges`
   - `overlap_edges`
   - `combined_edges`
   - `shared_edges`
   - `expand_edges`
   - `shrink_edges`
   - `max_overlap`
   - `min_overlap`

3. Полный `WikiVote` перепрогнан и логи сопоставлены на главной компоненте.
   Ключевые строки:
   - Graphviz, первая non-neighbor итерация:
     - `neighbors_only=0 proximity_edges=21170 overlap_edges=428537 combined_edges=440752 shared_edges=8955 expand_edges=0 shrink_edges=440752 max_overlap=1 min_overlap=0.00684641`
   - managed, первая non-neighbor итерация:
     - `neighborhoodOnly=false proximityEdgeCount=21169 overlapEdgeCount=0 combinedEdgeCount=21169 sharedEdgeCount=0 expandEdgeCount=0 shrinkEdgeCount=21169 maxOverlapFactor=0.9999983870275831 minOverlapFactor=0.0271810908276002`

4. Дополнительно видно, что расхождение начинается ещё раньше, в neighborhood-only фазе.
   Уже на `iter 0` у главной компоненты:
   - Graphviz:
     - `proximity_edges=21178 expand_edges=20834 shrink_edges=344 max_overlap=283.001 min_overlap=0.0493885`
   - managed:
     - `proximityEdgeCount=21181 expandEdgeCount=19529 shrinkEdgeCount=1652 maxOverlapFactor=478.5611977303543 minOverlapFactor=0.03920973909526264`

Что показал patch:

- у Graphviz и managed теперь есть сравнимые агрегаты по overlap-модели
- стало видно, что главный разъезд не в shrink bisection
- стало видно, что first non-neighbor mismatch очень большой:
  - Graphviz строит большой `overlap graph`
  - managed в той же точке имеет `overlapEdgeCount = 0`
- стало видно и второе:
  - ещё до `mode_switch` managed и Graphviz уже по-разному делят ребра на `expand` и `shrink`
  - значит проверять нужно не только `SfdpPrismOverlapGraphBuilder`, но и `SfdpPrismModelBuilder`

Критерий готовности:

- выполнен
- по одной паре логов для full `WikiVote` уже можно уверенно сказать:
  - разъезд есть в graph construction
  - и, вероятно, отдельно есть разъезд в model/weight semantics

Следующий шаг после `Patch 8a`:

- делать `Patch 8b`
- сначала проверить, почему managed на первой non-neighbor итерации получает `overlapEdgeCount = 0`, когда Graphviz получает `428537`
- для этого смотреть:
  - `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs`
  - `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- затем отдельно сверять `GetIdealDistance(...)` и веса в:
  - `src/PSGraphView.Sfdp/Prism/SfdpPrismModelBuilder.cs`

---

### Patch 8b. Добить overlap-graph и post-switch prism semantics

Статус:

- частично реализовано
- overlap-graph hypothesis проверена кодом и полным прогоном
- главный mismatch ещё не закрыт

Цель:

- понять, почему managed на первой non-neighbor итерации не строит overlap graph для главной компоненты
- приблизить post-switch `prism` path к Graphviz уже не по диагностике, а по поведению
- после этого повторно проверить, остался ли отдельный разъезд в `ideal distance` и весах

Почему именно это следующий шаг:

- `Patch 8a` уже показал, что главный mismatch теперь виден прямо в агрегатах
- самый жёсткий сигнал такой:
  - Graphviz на первой non-neighbor итерации получает `overlap_edges = 428537`
  - managed в той же точке получает `overlapEdgeCount = 0`
- пока это не исправлено, дальше править shrink scaling или fine tuning модели рано

Файлы:

- `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs`
- `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs`
- `src/PSGraphView.Sfdp/Prism/SfdpPrismModelBuilder.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpPrismOverlapRemoverTests.cs`
- при необходимости:
  - `tests/PSGraphView.Sfdp.Tests/SfdpPrismModelBuilderTests.cs`
  - `tests/PSGraphView.Sfdp.Tests/SfdpPrismOverlapGraphBuilderTests.cs`
  - `../graphviz/lib/neatogen/overlap.c`

Что сделано:

1. В `src/PSGraphView.Sfdp/Prism/SfdpPrismOverlapGraphBuilder.cs` переписан overlap-graph path.
   Теперь он идёт ближе к Graphviz `get_overlap_graph()`, а не как точный 2D overlap graph.

2. В `src/PSGraphView.Sfdp/SfdpPrismOverlapRemover.cs` shrink-entry возвращён к graphviz-like условию.
   Теперь shrink стартует по `maxOverlap < 1.0`, а не по `overlapEdgeCount == 0`.

3. Обновлены тесты:
   - `tests/PSGraphView.Sfdp.Tests/SfdpPrismOverlapGraphBuilderTests.cs`
   - `tests/PSGraphView.Sfdp.Tests/SfdpPrismGraphBuilderTests.cs`

4. Полный `WikiVote` перепрогнан на локальном Graphviz.
   Артефакты:
   - `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-graphviz.verbose.log`
   - `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-managed.diagnostics.jsonl`
   - `/tmp/psgraphview-sfdp-full-patch8b-localgv/wiki-vote-full-comparison.json`

Что показал patch:

- ожидаемого сдвига не произошло:
  - на первой non-neighbor итерации главной компоненты managed по-прежнему пишет:
    - `overlapEdgeCount = 0`
    - `combinedEdgeCount = 21169`
    - `postSwitchMinOverlap = 0.0271810908276002`
  - Graphviz в той же точке по-прежнему пишет:
    - `overlap_edges = 428537`
    - `combined_edges = 440752`
    - `min_overlap = 0.00684641`
- значит overlap-graph mismatch не исчерпывается одной реализацией builder
- значит remaining mismatch сидит раньше:
  - в геометрии, с которой managed входит в post-switch `prism`
  - и/или в том, как до этого формируется общая форма главной компоненты

Критерий готовности:

- пока не выполнен

Следующий шаг внутри `Patch 8b`:

- `Patch 8b.1`
  - выполнен
  - `sharedEdgeCount` на `ComponentId=0` теперь `8958`, то есть почти совпадает с Graphviz `8955`
  - `combinedEdgeCount` опустился до `448589`
- `Patch 8b.2`
  - выполнен
  - Graphviz verbose и managed diagnostics теперь автоматически сводятся в `comparison.json` как `MainComponentGeometry`
  - default-path run после пересборки локального Graphviz показал:
    - Graphviz `mode_switch == finish == after_overlap_removal`
    - managed `mode_switch == finish == after_overlap_removal`
    - значит post-switch / finish path уже не главный источник разъезда
  - ключевой remaining mismatch перенёсся раньше:
    - Graphviz `post_scale avg_edge_len = 0.524444`, `avg_label_size = 0.131111`
    - managed `post_scale avg_edge_len = 21.759999999999255`, `averageLabelSize = 5.439999999999856`
  - вывод:
    - `Patch 8b.2` закрыт
    - следующий шаг должен бить по default box-size / unit semantics, а не по `finish`-геометрии
- `Patch 8b.3`
  - реализован как `pre-overlap transform path`
  - добавлен явный режим `SfdpOverlapRemovalBoxUnits`:
    - `OutputUnits` сохраняет старое поведение
    - `GraphvizPoints` считает default overlap-box half-size как `(nodeRadius + padding) / 72`
  - `Compare-WikiVote-Sfdp.ps1` теперь запускает managed path с `SfdpOverlapRemovalBoxUnits = GraphvizPoints`
  - полный `WikiVote`:
    - `/tmp/psgraphview-sfdp-full-patch8b3-localgv/wiki-vote-full-comparison.json`
  - что это дало на главной компоненте:
    - Graphviz `post_scale avg_edge_len = 0.524444`, managed `0.5244444444443861`
    - Graphviz `post_scale avg_label_size = 0.131111`, managed `0.1311111111110971`
    - Graphviz `mode_switch width = 19.462161`, managed `20.09958405740643`
    - Graphviz `mode_switch avg_edge_len = 3.496258`, managed `2.541669314160542`
  - что это НЕ дало:
    - whole-graph baseline метрика стала хуже:
      - Graphviz `MeanEdgeToDiagonal = 0.094565`
      - Managed `MeanEdgeToDiagonal = 0.008147`
    - это выглядит как эффект pack/component gap на маленьких компонентах, а не как ухудшение главной компоненты
  - вывод:
    - для main component `pre-overlap box/unit semantics` действительно были большим разъездом
    - но после этого сравнение full graph надо смотреть уже не только по общему diagonal, а с учётом packer path
    - следующим шагом нужен отдельный `Patch 9`, а не ещё один подпункт внутри `8b`
    - если `Patch 9` покажет, что main component уже близок, а разъезд создаёт packing:
      - править `SfdpComponentPacker`
      - и сравнивать semantics component-gap / packed bounds
    - если `Patch 9` покажет, что разъезд сидит уже на main component без packing:
      - вернуться в `prism` model fine tuning отдельным patch-ом:
        - `GetIdealDistance(...)`
        - веса expansion/shrink path

---

### Patch 9. Сравнить и при необходимости сблизить `pack/component-gap` semantics

Статус:

- реализован как диагностический patch
- первоначальный вывод по нему позже был уточнён `Patch 10`

Почему это следующий шаг:

- `Patch 8b.3` уже дал полезный и законченный результат:
  - на главной компоненте `post_scale` managed стал почти 1:1 с Graphviz
  - геометрия на `mode_switch` тоже стала заметно ближе
- но full-graph baseline после этого не улучшился:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.008147`
- это уже не похоже на чистый mismatch внутри `prism` главной компоненты
- следующий логичный кандидат:
  - `SfdpComponentPacker`
  - `ComponentGap`
  - порядок и bounds packing-а мелких компонент

Цель:

- отделить quality main component от full-graph quality после packing
- понять, какую часть текущего разъезда создаёт packer, а не сам `sfdp/prism`
- после этого выбрать правильное продолжение:
  - либо править `SfdpComponentPacker`
  - либо вернуться в `prism` fine tuning уже без шума от packing

Файлы:

- `src/PSGraphView.Sfdp/SfdpComponentPacker.cs`
- `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs`
- `demos/Compare-WikiVote-Sfdp.ps1`
- `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`
- при необходимости:
  - `../graphviz/lib/common/postproc.c`
  - `../graphviz/lib/pack/pack.c`

Что делать:

1. Добавить в compare-скрипт отдельные метрики для largest connected component.
   Нужны как минимум:
   - `MainComponentMeanEdgeToDiagonal`
   - `MainComponentEdgeLengthCv`
   - `MainComponentWidth`
   - `MainComponentHeight`
   - `MainComponentMeanEdgeLength`

2. Добавить managed diagnostics вокруг packing.
   Нужны события:
   - `packing.start`
   - `packing.component`
   - `packing.finish`
   И поля:
   - `componentCount`
   - `componentIndex`
   - `componentNodeCount`
   - `componentWidth`
   - `componentHeight`
   - `offsetX`
   - `offsetY`
   - `gap`
   - `packedWidth`
   - `packedHeight`

3. На полном `WikiVote` сравнить:
   - метрики largest connected component до packing
   - full-graph метрики после packing
   - насколько итоговый diagonal меняется из-за мелких компонент

4. Если окажется, что главный remaining mismatch pack-induced:
   - сравнить `ComponentGap`, порядок packing-а и итоговые bounds
   - приблизить `SfdpComponentPacker` к Graphviz `packSubgraphs` / `dotneato_postprocess`

5. Если окажется, что main component сам по себе всё ещё заметно расходится:
   - не тратить время на packer
   - вернуть фокус в `prism` model fine tuning отдельным patch-ом

Что должно измениться в логах после patch:

- `comparison.json` должен показывать отдельно:
  - `MainComponentMetrics`
  - `PackedGraphMetrics`
- managed diagnostics должны показывать:
  - размеры компонент до packing
  - offsets после packing
  - итоговые packed bounds
- после одного полного прогона должно быть понятно:
  - packer ли даёт основной remaining mismatch
  - или проблема остаётся уже на main component

Критерий готовности:

- есть объяснение, почему full-graph `MeanEdgeToDiagonal` ещё расходится после `Patch 8b.3`
- есть решение, куда идти дальше:
  - либо править `SfdpComponentPacker`
  - либо возвращаться в `prism` fine tuning без шума от packing

Что сделано:

1. В `src/PSGraphView.Sfdp/SfdpComponentPacker.cs` добавлен структурированный packing result.
   Теперь можно получить:
   - `strategy`
   - `gap`
   - `margin`
   - `step`
   - per-component source bounds
   - per-component packed bounds
   - итоговые packed bounds

2. В `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs` добавлены diagnostics:
   - `packing.start`
   - `packing.component`
   - `packing.finish`

3. В `demos/Compare-WikiVote-Sfdp.ps1` добавлены:
   - `MainComponentMetrics` для Graphviz и managed
   - `Managed.Packing` summary из diagnostics
   - вывод main-component метрик в консольный summary

4. Добавлен regression test:
   - `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`
   - для disconnected graph diagnostics теперь обязаны содержать packing events

5. Проверка:
   - `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --no-restore --filter "FullyQualifiedName~SfdpLayoutEngineTests|FullyQualifiedName~SfdpComponentPackerTests"`
   - `dotnet build src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj`
   - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore`
   - smoke run:
     - `/tmp/psgraphview-sfdp-patch9-smoke/wiki-vote-subgraph-30-comparison.json`
   - full run:
     - `/tmp/psgraphview-sfdp-full-patch9-localgv/wiki-vote-full-comparison.json`
     - `/tmp/psgraphview-sfdp-full-patch9-localgv/wiki-vote-full-managed.diagnostics.jsonl`
     - `/tmp/psgraphview-sfdp-full-patch9-localgv/wiki-vote-full-graphviz.verbose.log`

Что показал patch:

- patch добавил правильные diagnostics и main-component compare surface
- но его первый вывод оказался неполным:
  - в compare-helper была ошибка
  - `Get-LayoutMetrics` считал `width/height/diagonal` по всем координатам, даже когда сравнивался только largest connected component
  - это было исправлено уже в `Patch 10`
- при этом на уровне post-process geometry главная компонента всё ещё выглядит намного ближе к Graphviz:
  - Graphviz `mode_switch width = 19.462161`
  - managed `mode_switch width = 20.09958405740643`
  - Graphviz `mode_switch avg_edge_len = 3.496258`
  - managed `mode_switch avg_edge_len = 2.541669314160542`

Вывод:

- `Patch 9` выполнил свою инфраструктурную задачу:
  - добавил packing diagnostics
  - подготовил surface для main-component compare
- окончательный вывод о packer надо считать по результатам `Patch 10`, а не по первой интерпретации `Patch 9`

---

### Patch 10. Локализовать и выровнять geometry handoff от `postprocess` до итогового SVG

Статус:

- реализован как диагностический patch
- показал, что главный remaining mismatch не в renderer/output-transform path

Почему это был следующий шаг:

- `Patch 9` дал противоречивый вывод, который нужно было проверить checkpoint-ами
- на full `WikiVote`:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.008147`
  - Graphviz `MainComponentMeanEdgeToDiagonal = 0.094585`
  - Managed `MainComponentMeanEdgeToDiagonal = 0.008149`
- при этом в diagnostics главная компонента выглядит намного ближе к Graphviz на checkpoint-ах внутри overlap/postprocess:
  - Graphviz `mode_switch width = 19.462161`
  - managed `mode_switch width = 20.09958405740643`
- значит нужно было проверить:
  - handoff между `after_overlap_removal` и final layout
  - и renderer path в `SfdpSvgExporter`

Цель:

- понять, на каком именно переходе managed geometry перестаёт быть похожей на Graphviz:
  - сразу после `after_overlap_removal`
  - при возврате component layout в `SfdpLayoutEngine`
  - на `layout.finish`
  - при экспорте в SVG
- после этого исправить именно тот слой, где появляется реальный разъезд

Файлы:

- `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs`
- `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs`
- `src/PSGraphView.Sfdp/SfdpPostProcessor.cs`
- `src/PSGraphView.Sfdp/SfdpSvgExporter.cs`
- `demos/Compare-WikiVote-Sfdp.ps1`
- `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`
- при необходимости:
  - `tests/PSGraphView.PowerShell.Tests/ExportGraphViewCmdletTests.cs`

Что делать:

1. Добавить managed diagnostics на handoff-точках между компонентной раскладкой и финальным layout.
   Нужны события:
   - `component.geometry stage=layout_component_return`
   - `layout.geometry stage=before_packing`
   - `layout.geometry stage=after_packing`
   - `svg.geometry stage=export_input`
   - `svg.geometry stage=viewbox`

2. На каждом checkpoint логировать одинаковый минимум:
   - `width`
   - `height`
   - `averageEdgeLength`
   - `averageLabelSize` если применимо
   - `minX`
   - `minY`
   - `maxX`
   - `maxY`

3. В `SfdpSvgExporter` добавить diagnostics именно про export semantics:
   - `padding`
   - `contentBounds`
   - `naturalWidth`
   - `naturalHeight`
   - `outputWidth`
   - `outputHeight`
   - `viewBox`

4. В `Compare-WikiVote-Sfdp.ps1` добавить отдельную сводку:
   - `ManagedLayoutGeometry`
   - `ManagedSvgGeometry`
   - `ManagedExtractedSvgMetrics`
   Чтобы было видно, совпадают ли layout bounds с тем, что потом читает compare из SVG.

5. По полному `WikiVote` сравнить цепочку:
   - `after_overlap_removal`
   - `layout_component_return`
   - `before_packing`
   - `after_packing`
   - `layout.finish`
   - `svg export_input`
   - координаты, извлечённые из готового SVG

6. По результату выбрать одно из двух продолжений:
   - если разъезд появляется до `layout.finish`, чинить handoff внутри layout pipeline
   - если `layout.finish` нормальный, а SVG уже нет, чинить `SfdpSvgExporter` / viewBox / export semantics

Что должно измениться в логах после patch:

- один full run должен показывать точный checkpoint, где geometry начинает расходиться
- `comparison.json` должен явно содержать:
  - `ManagedMainComponentGeometry`
  - `ManagedLayoutGeometry`
  - `ManagedSvgGeometry`
  - `ManagedExtractedSvgMetrics`
- должно стать понятно:
  - проблема в layout handoff
  - или проблема в renderer/output semantics

Критерий готовности:

- есть точный ответ, на каком слое появляется разъезд после `Patch 9`
- следующий patch можно формулировать уже узко

Что сделано:

1. В `src/PSGraphView.Sfdp/SfdpMultilevelLayouter.cs` добавлен:
   - `component.geometry stage=layout_component_return`

2. В `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs` добавлены:
   - internal overload с общим diagnostics writer на весь export path
   - `layout.geometry stage=before_packing`
   - `layout.geometry stage=after_packing`
   - расширенный `layout.finish` с `diagonal` и `averageEdgeLength`

3. В `src/PSGraphView.Sfdp/SfdpSvgExporter.cs` добавлены:
   - `svg.geometry stage=export_input`
   - `svg.geometry stage=viewbox`

4. В `demos/Compare-WikiVote-Sfdp.ps1` исправлен compare-helper bug:
   - `Get-LayoutMetrics` теперь считает bounds только по вершинам сравниваемого графа
   - добавлены `ManagedLayoutGeometry` и `ManagedSvgGeometry`

5. Добавлены/обновлены тесты:
   - `tests/PSGraphView.Sfdp.Tests/SfdpSvgExporterTests.cs`
   - `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`

6. Проверка:
   - `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --no-restore --filter "FullyQualifiedName~SfdpLayoutEngineTests|FullyQualifiedName~SfdpSvgExporterTests|FullyQualifiedName~SfdpComponentPackerTests"`
   - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore`
   - full run:
     - `/tmp/psgraphview-sfdp-full-patch10b-localgv/wiki-vote-full-comparison.json`
     - `/tmp/psgraphview-sfdp-full-patch10b-localgv/wiki-vote-full-managed.diagnostics.jsonl`
     - `/tmp/psgraphview-sfdp-full-patch10b-localgv/wiki-vote-full-graphviz.verbose.log`

Что показал patch:

- main component у managed уже близок к Graphviz:
  - Graphviz `MainComponentMeanEdgeToDiagonal = 0.094585`
  - Managed `MainComponentMeanEdgeToDiagonal = 0.093877`
- handoff-путь внутри managed совпадает сам с собой:
  - `component.geometry stage=layout_component_return`: width `20.099584`
  - `layout.geometry stage=after_packing`: width `220.524444`
  - `svg.geometry stage=export_input`: width `220.524444`
- значит:
  - renderer / `SfdpSvgExporter` не даёт главный разъезд
  - full-graph mismatch появляется уже на уровне packing/global bounds, а не на export step

Вывод:

- `Patch 10` выполнил диагностическую задачу
- следующий правильный шаг:
  - отдельный patch на full-graph packing parity
  - снова сравнивать `SfdpComponentPacker` с Graphviz `packSubgraphs` / `dotneato_postprocess`

### Patch 11. Сблизить full-graph packing с Graphviz polyomino `packSubgraphs`

Статус:

- реализован как первый algorithmic patch для full-graph packing parity
- дал заметный прогресс, но не закрыл remaining mismatch до конца

Почему это был следующий шаг:

- `Patch 10` показал, что main component уже близка к Graphviz
- при этом full-graph bounds у managed были раздуваются именно на packing-стадии:
  - `component.geometry stage=layout_component_return`: width `20.099584`
  - `layout.geometry stage=after_packing`: width `220.524444`
- значит следующая правка должна была идти не в `prism` и не в exporter, а в `SfdpComponentPacker`

Цель:

- убрать самый грубый разъезд с Graphviz `packSubgraphs`:
  - packing по прямоугольным bbox вместо polyomino
  - слишком большой default gap в managed
- сохранить уже близкую к Graphviz main component и сократить full-graph bounds

Файлы:

- `src/PSGraphView.Sfdp/SfdpComponentPacker.cs`
- `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs`
- `src/PSGraphView.Sfdp/SfdpOptions.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpComponentPackerTests.cs`

Что сделано:

1. `SfdpComponentPacker` переведён с rectangle occupancy на graphviz-like polyomino occupancy.
   Теперь packing shape строится не по всему bbox компоненты, а по:
   - node boxes
   - line cells для локальных рёбер компоненты

2. `LayoutEngine` теперь передаёт в packer полный `SfdpCsrGraph`, чтобы packing мог восстановить локальные edges для каждой компоненты.

3. Default `ComponentGap` уменьшен:
   - `40 -> 16`
   Это убирает явный over-pack относительно Graphviz scale.

4. Обновлены тесты packer-а:
   - сохранён тест на явный gap между двумя компонентами
   - добавлен сценарий, где tiny components могут использовать holes внутри dominant shape вместо выталкивания всей раскладки наружу

5. Проверка:
   - `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --no-restore`
   - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore`
   - full run:
     - `/tmp/psgraphview-sfdp-full-patch11-localgv/wiki-vote-full-comparison.json`
     - `/tmp/psgraphview-sfdp-full-patch11-localgv/wiki-vote-full-managed.diagnostics.jsonl`
     - `/tmp/psgraphview-sfdp-full-patch11-localgv/wiki-vote-full-graphviz.verbose.log`

Что показал patch:

- full-graph packing у managed стал существенно компактнее:
  - `layout.geometry stage=after_packing`: width `220.524444 -> 110.524444`
- итоговая full-graph метрика тоже заметно улучшилась:
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.016874`
  - до patch-а было `0.008147`
- main component осталась почти без изменений и всё ещё близка к Graphviz:
  - Graphviz `MainComponentMeanEdgeToDiagonal = 0.094585`
  - Managed `MainComponentMeanEdgeToDiagonal = 0.093883`

Что ещё не совпадает:

- remaining mismatch всё ещё именно packing-specific:
  - Graphviz verbose: `margin = 8`, `step size = 29`
  - Managed diagnostics: `gap = 16`, `margin = 8`, `step = 2`
- значит после polyomino patch-а следующая разница уже не в самом факте bbox-packing, а в:
  - packing units / cell scale semantics
  - возможно, финальном normalize/shift path, более близком к `dotneato_postprocess`

Вывод:

- `Patch 11` подтвердил, что full-graph mismatch действительно сидел в `SfdpComponentPacker`
- но это был только первый слой проблемы
- следующий правильный шаг:
  - не возвращаться в `prism`
  - добивать packing units / grid step parity уже ближе к Graphviz `packSubgraphs`

### Patch 12. Сблизить packing units / grid step semantics с Graphviz `packSubgraphs`

Статус:

- реализован
- задачу по packing scale / step можно считать закрытой

Почему это следующий шаг:

- `Patch 11` уже убрал главный прямоугольный over-pack
- после этого remaining mismatch стал намного уже и теперь выглядит как mismatch единиц packing-а:
  - Graphviz verbose: `margin = 8`, `step size = 29`
  - Managed diagnostics: `gap = 16`, `margin = 8`, `step = 2`
- main component уже близка к Graphviz, значит:
  - не надо возвращаться в `prism`
  - не надо трогать renderer
  - надо добивать именно packing scale / cell semantics

Цель:

- понять, в каких единицах Graphviz реально считает `packSubgraphs` для `sfdp`
- привести managed packing к той же шкале:
  - `source bounds`
  - `polyomino cells`
  - `perimeter`
  - `step size`
- после этого ещё раз проверить, нужен ли отдельный patch на `dotneato_postprocess`-style normalize/shift

Файлы:

- `src/PSGraphView.Sfdp/SfdpComponentPacker.cs`
- `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs`
- `tests/PSGraphView.Sfdp.Tests/SfdpComponentPackerTests.cs`
- `demos/Compare-WikiVote-Sfdp.ps1`
- при необходимости:
  - `../graphviz/lib/pack/pack.c`
  - `../graphviz/lib/common/postproc.c`

Что делать:

1. Расширить managed diagnostics вокруг packing shape.
   Добавить поля:
   - `sourceWidth`
   - `sourceHeight`
   - `roundedMinX`
   - `roundedMinY`
   - `cellCount`
   - `perimeter`
   - `gridWidth`
   - `gridHeight`
   - `step`
   - `margin`
   Для `packing.start` и `packing.component`.

2. На одном full run сравнить не только `packedWidth`, но и shape-level данные largest component.
   Сравнить:
   - `sourceWidth/sourceHeight`
   - `cellCount`
   - `perimeter`
   - `step`
   - `offset`

3. Если подтвердится, что Graphviz pack делает расчёт в другой шкале:
   - ввести отдельную packing-scale семантику в managed
   - не менять layout coordinates глобально
   - масштабировать только packing shape и offsets, а потом возвращать результат в layout units

4. После этого снова прогнать full `WikiVote`.
   Смотреть:
   - `layout.geometry stage=after_packing`
   - `Managed.Packing.Start/Finish`
   - full `MeanEdgeToDiagonal`
   - main component метрики

5. Только если packing units уже будут близки, а full bounds всё ещё заметно расходятся:
   - идти в отдельный подпатч на финальный normalize/shift path
   - сравнивать это уже с `dotneato_postprocess`

Что должно измениться в логах после patch:

- managed `step` должен стать хотя бы одного порядка с Graphviz, а не `2` против `29`
- `cellCount` / `perimeter` largest component должны стать сравнимыми по масштабу
- `after_packing width` должен ещё заметно снизиться относительно `110.524444`
- при этом `MainComponentMeanEdgeToDiagonal` должен остаться близким к Graphviz

Критерий готовности:

- есть понятный ответ, в чём именно remaining mismatch:
  - в packing unit scale
  - или уже в финальном shift/normalize path
- после patch-а следующий шаг можно формулировать уже узко:
  - либо `Patch 12a` про normalize/shift
  - либо считать full-graph packing parity в основном закрытой

Что сделано:

1. В `src/PSGraphView.Sfdp/SfdpComponentPacker.cs` введена отдельная packing scale:
   - `scale = 72`
   - packing shape строится в point-like пространстве
   - итоговые offsets возвращаются обратно в layout units

2. `ComponentGap` снова трактуется в layout units, а packing margin вычисляется отдельно:
   - default стал `16 / 72`
   - при этом в diagnostics managed margin теперь сравним с Graphviz `margin = 8`

3. В diagnostics добавлены shape-level поля для packing:
   - `scale`
   - `cellCount`
   - `gridWidth/gridHeight`
   - `perimeter`
   - `componentWidthScaled/componentHeightScaled`
   - `roundedMinXScaled/roundedMinYScaled`

4. Обновлены тесты:
   - `tests/PSGraphView.Sfdp.Tests/SfdpComponentPackerTests.cs`
   - `tests/PSGraphView.Sfdp.Tests/SfdpLayoutEngineTests.cs`

5. Проверка:
   - `dotnet test tests/PSGraphView.Sfdp.Tests/PSGraphView.Sfdp.Tests.csproj --no-restore`
   - `dotnet test tests/PSGraphView.PowerShell.Tests/PSGraphView.PowerShell.Tests.csproj --no-restore`
   - full run:
     - `/tmp/psgraphview-sfdp-full-patch12-localgv/wiki-vote-full-comparison.json`
     - `/tmp/psgraphview-sfdp-full-patch12-localgv/wiki-vote-full-managed.diagnostics.jsonl`
     - `/tmp/psgraphview-sfdp-full-patch12-localgv/wiki-vote-full-graphviz.verbose.log`

Что показал patch:

- `step` у managed стал того же порядка, что и у Graphviz:
  - Graphviz `29`
  - Managed `35`
- full-graph packing резко приблизился к Graphviz:
  - `layout.geometry stage=after_packing`: width `110.524444 -> 24.811667`
  - Graphviz `MeanEdgeToDiagonal = 0.094565`
  - Managed `MeanEdgeToDiagonal = 0.077753`
- main component осталась почти без изменений:
  - Graphviz `MainComponentMeanEdgeToDiagonal = 0.094585`
  - Managed `MainComponentMeanEdgeToDiagonal = 0.093877`

Вывод:

- `Patch 12` закрыл главную проблему по packing scale / cell step
- full-graph mismatch теперь уже не похож на грубый scale bug
- следующий шаг надо формулировать уже уже:
  - сравнить финальный shift / normalize path
  - и при необходимости смотреть в сторону `dotneato_postprocess`

### Patch 13. Проверить и при необходимости сблизить финальный shift / normalize path после packing

Статус:

- не начат
- это следующий практический patch после `Patch 12`

Цель:

- понять, остаётся ли разъезд после того, как packing scale уже выровнен
- если да, проверить:
  - final component offsets
  - root bounds normalize
  - финальный post-pack handoff относительно Graphviz `dotneato_postprocess`

---

## Порядок выполненных patch-ов и ближайшего следующего шага

Если идти строго по цене/эффекту, я бы делал так:

1. Patch 0b
2. Patch 1
3. Patch 2
4. Patch 3
5. Patch 4
6. Patch 5
7. Patch 6a
8. Patch 6b
9. Patch 7
10. Patch 7a
11. Patch 8
12. Patch 8a
13. Patch 8b
14. Patch 9
15. Patch 10
16. Patch 11
17. Patch 12
18. Patch 13

Именно в таком порядке, потому что:

- сначала надо убрать слепые зоны логов
- потом убрать compare-harness noise
- потом исправить самые жёсткие algorithmic mismatches, уже доказанные логами:
  - `K`
  - `p`
  - `max_depth`
- потом добить именно главный текущий runtime mismatch в `prism`:
  - сначала входные box/padding semantics
  - затем proximity / triangulation path
- и только потом переходить к quadtree mode и его внутренностям

Следующий практический шаг после `Patch 12`:

  - перед compare для `PSGraphView` больше не нужно полагаться на `tests/.../bin`
  - `-UseLocalModules` сам делает fresh `dotnet publish` и берёт модуль из publish output
- `Patch 8b.1` уже подтверждён на свежем модуле:
  - `sharedEdgeCount = 8958` против Graphviz `8955`
  - `minOverlapFactor = 0.006829...` против Graphviz `0.00684641`
- `Patch 8b.2` теперь тоже закрыт:
  - `mode_switch == finish == after_overlap_removal` подтверждено на default path у Graphviz и managed
  - compare-скрипт пишет эти checkpoint-ы в `comparison.json` автоматически
- `Patch 8b.3` дал важный сдвиг на главной компоненте:
  - `pre-overlap box/unit semantics` действительно были реальным разъездом
- `Patch 12` закрыл главный mismatch по packing scale:
  - `step = 35` у managed против Graphviz `29`
  - full `MeanEdgeToDiagonal = 0.016874 -> 0.077753`
  - `after_packing width = 110.524444 -> 24.811667`
- при этом main component по-прежнему близка к Graphviz
- значит следующий наиболее оправданный шаг по цене/эффекту:
  - остаться в packing path
  - сравнить и выровнять финальный shift / normalize path
  - при необходимости сравнить managed post-pack handoff с Graphviz `dotneato_postprocess`
  - смотреть в:
    - `src/PSGraphView.Sfdp/SfdpLayoutEngine.cs`
    - `src/PSGraphView.Sfdp/SfdpComponentPacker.cs`
    - `../graphviz/lib/common/postproc.c`

Следующий практический шаг после текущего состояния:

- оформить это как `Patch 13`
- сначала проверить:
  - final component offsets
  - packed bounds normalize
  - root bounds после packing
- и только потом решать, нужен ли буквальный `dotneato_postprocess`-style shift
