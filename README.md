# Wares

A Dalamud plugin for FFXIV that lets you mark inventory items as **Wares** (vendor junk) or **Market** (to list on the market board), and sell every Wares-marked item in one click.

## Features

- **Native badges.** Wares-marked items show a gil coin in the corner of their inventory cell; Market-marked items show an **M**.
- **Only valid marks.** An item that can't be sold to a vendor can't be marked for Wares, and one that can't go on the market board can't be marked for Market. The menu entry is greyed out instead.
- **One mark per item.** Marking an item for Wares removes its Market mark and vice versa.
- **Ctrl+click multi-select.** Ctrl+left-click cells to select several items (they light up like a hovered cell), then right-click one of them to mark, re-mark or clear them all at once. Items that can't take a mark are skipped. Clicking anywhere else clears the selection, like File Explorer.
- **Sell Wares button.** While you're talking to a vendor (or have a retainer's inventory open), a native **Sell Wares (N)** button appears in the inventory. It sells every Wares-marked item one by one and answers the game's confirmation prompts (unique, untradable, HQ, melded items) for you. Click it again to stop.
- **Movable button.** Settings has an X / Y drag control for each inventory layout to line the button up, live, with a preview toggle so you can do it away from a vendor.
- Works with the normal, larger and "open all bags" inventory layouts, including sorted inventories. Key items and crystals are never touched.

Marks are saved per character and apply to the stack you marked: they follow it when you move it, and are gone once it's sold or used up. To mark an item, right-click it and choose **Mark for Wares** / **Mark for Market** (or **Unmark ...**). Turn on **Remember marks for every copy of an item** in the settings to have a mark cover every copy of that item (NQ and HQ separately), including ones you pick up later.

## Commands

- `/wares` - open settings
- `/wares sell` - start selling while at a vendor

## Installation Instructions

1. Open the game chat and type `/xlsettings`, then click the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL into the empty box at the bottom:
   ```
   https://raw.githubusercontent.com/mcbro12345/DalamudPlugins/main/pluginmaster.json
   ```
3. Click the **+** button to add it, then **Save and Close**.
4. Type `/xlplugins` to open the Plugin Installer, search for "Wares," and click **Install**.

That's it. Updates will show up in the Plugin Installer automatically.

## Credits

Wares wouldn't exist without these open-source projects:

- [SimpleTweaks](https://github.com/Caraxi/SimpleTweaksPlugin) by Caraxi and contributors - AGPL-3.0 (selling is adapted from its Quick Sell tweak)
- [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) by MidoriKami - MIT
- [CriticalCommonLib](https://github.com/Critical-Impact/CriticalCommonLib) by Critical-Impact - reference for the inventory grid layout

Full details in `THIRD_PARTY_NOTICES.md`.

## Contributing

Issues and PRs are welcome - see `CONTRIBUTING.md`.

## AI-assisted development

Parts of this codebase were built with AI coding tools. See `AI-GENERATED-NOTICE.md` for details.

## Disclaimer

Wares is an unofficial, fan-made project. It's not affiliated with or endorsed by Square Enix or the Dalamud project. FINAL FANTASY XIV and related trademarks belong to their respective owners.

## License

AGPL-3.0, because Wares includes code adapted from SimpleTweaks - see `LICENSE`. Third-party components keep their own licenses (see `THIRD_PARTY_NOTICES.md`).
