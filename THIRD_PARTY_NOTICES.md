# Third-Party Notices

Wares uses or derives material from the following open-source projects.

## SimpleTweaks

Copyright belongs to Caraxi and the SimpleTweaks contributors.

Used under the GNU Affero General Public License v3.0. The one-click selling in `Wares/Game/QuickSell.cs` is adapted from SimpleTweaks' "Quick Sell Items at Vendors" tweak (`Tweaks/QuickSellItems.cs`) and its `GenerateCallback` / `ValueString` helpers (`Utility/Common.cs`, `Utility/Extensions.cs`).

Upstream project: `Caraxi/SimpleTweaksPlugin` on GitHub.

Because Wares includes code adapted from SimpleTweaks, Wares as a whole is distributed under the AGPL-3.0 (see `LICENSE`).

## KamiToolKit

Copyright © 2024 MidoriKami and contributors.

Used under the MIT License. The source snapshot under `vendor/KamiToolKit/` is built alongside Wares to provide the native UI nodes (badges, selection highlight, sell button).

Upstream project: `MidoriKami/KamiToolKit` on GitHub.

License: `vendor/KamiToolKit/LICENSE`.

## Allagan Tools / CriticalCommonLib

No code is copied, but the inventory-grid cell lookup (cell `i` is node id `i + 3`) and the per-layout page mapping follow what CriticalCommonLib does.

Upstream project: `Critical-Impact/CriticalCommonLib` on GitHub.
