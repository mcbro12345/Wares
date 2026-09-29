using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wares.Game;

internal static unsafe class GameAddons
{
    // The three layouts the player inventory can be set to, in the order the
    // sell button prefers them when more than one happens to exist.
    public static readonly string[] InventoryLayouts = ["InventoryExpansion", "InventoryLarge", "Inventory"];

    public static AtkUnitBase* Get(string name)
    {
        var manager = RaptureAtkUnitManager.Instance();
        return manager == null ? null : manager->GetAddonByName(name);
    }

    public static AtkUnitBase* GetVisible(string name)
    {
        var addon = Get(name);
        return addon != null && addon->IsVisible ? addon : null;
    }

    public static bool IsVisible(string name) => GetVisible(name) != null;

    public static AtkUnitBase* VisibleInventoryLayout()
    {
        foreach (string name in InventoryLayouts)
        {
            var addon = GetVisible(name);
            if (addon != null) return addon;
        }
        return null;
    }
}
