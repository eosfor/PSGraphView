# Architecture Decisions

## 2026-03-30 00:08 PDT - Эталонные export targets фиксируем явно

Решение: для baseline compare и последующего parity используем явные target-spec из локального `graphviz`:

- `-Tsvg:svg`
- `-Tpng:cairo`
- `-Tjpg:cairo`

Причины:

- для `svg` эталонный path в текущем локальном `graphviz` идёт через core SVG plugin, а не через cairo path
- для `png` и `jpg` на локальной сборке reference path идёт через cairo-backed renderer/device chain
- explicit target-spec убирает двусмысленность от plugin priority и различий между сборками
- compare harness должен воспроизводимо сравнивать один и тот же backend, а не то, что выбралось по умолчанию

Телеметрия:

- code inspection: `/Users/andrei/repo/graphviz/plugin/core/gvrender_core_svg.c`
- code inspection: `/Users/andrei/repo/graphviz/plugin/pango/gvrender_pango.c`
- code inspection: `/Users/andrei/repo/graphviz/plugin/gd/gvdevice_gd.c`
- code inspection: `/Users/andrei/repo/graphviz/lib/common/emit.c`

## 2026-03-30 00:08 PDT - Новый export pipeline выносим в отдельный проект

Решение: production-код нового export pipeline планируем в отдельном проекте `PSGraphView.GVExport`.

Причины:

- это отделяет export parity от layout parity и не раздувает `PSGraphView.Sfdp`
- в `PSGraphView.Sfdp` разумно оставить layout, post-processing и layout-specific adapters
- `PSGraphView.PowerShell` должен оставаться тонким user-facing слоем, а не местом для scene/raster/device логики
- общий scene/viewport/raster слой потенциально пригодится не только для `Sfdp`, но и для других renderer-ов

Телеметрия:

- repo structure inspection: `/Users/andrei/repo/PSGraphView/src/PSGraphView.Sfdp/`
- repo structure inspection: `/Users/andrei/repo/PSGraphView/src/PSGraphView.PowerShell/`
- user direction: отдельный проект для нового export functionality

## 2026-03-30 00:11 PDT - Patch 0 baseline compare harness принят

Решение: считаем `Patch 0` закрытым и используем новый harness как рабочий baseline для следующих export patch-ей.

Причины:

- оба planned compare-script теперь реально запускаются в локальной репе
- small-graph harness даёт быстрый повторяемый baseline, где mismatch читается без шума от большого layout
- WikiVote harness тоже рабочий и уже сохраняет те же артефакты рядом с output-ами
- managed `png/jpg` сейчас честно помечаются как unsupported, а не маскируются под успешный результат

Телеметрия:

- run: `pwsh -NoProfile -File demos/Compare-Export-SmallGraphs.ps1 -UseLocalModules -OutputDir /tmp/psgraphview-export-small-compare-smoke`
- result: 6/6 cases completed successfully
- result: graphviz `svg/png/jpg` produced for all 6 cases
- result: managed `svg` produced for all 6 cases
- result: managed `png/jpg` unsupported for all 6 cases, как и ожидалось до `Patch 4`
- result: во всех 6 small-graph cases `NodeGroupDelta = 0` и `EdgeGroupDelta = 0`
- result: во всех 6 small-graph cases `WidthMatch = false`, `HeightMatch = false`, `ViewBoxMatch = false`
- result: во всех 6 small-graph cases `TitleDelta = -1` и `RectDelta = 1`
- run: `pwsh -NoProfile -File demos/Compare-WikiVote-Export.ps1 -UseLocalModules -UseSubgraph -SubgraphSeedCount 10 -OutputDir /tmp/psgraphview-export-wikivote-smoke`
- result: script completed successfully and produced `svg/png/jpg` reference outputs plus managed `svg`
- result: `SubgraphSeedCount = 10` yielded `10 vertices / 0 edges`, so this size is good for smoke only, not for edge-path parity work
