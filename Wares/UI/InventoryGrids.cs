using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Wares.Game;

namespace Wares.UI;

// One of the grid addons that draws 35 bag cells. Every inventory layout
// (normal, larger, and "open all bags") is made of these; key items and
// crystals use different addons, so they are never touched.
internal sealed record InventoryGrid(string AddonName, string LayoutName, int GridIndex)
{
    // Cell i of a grid is node id i + 3 (the same lookup Allagan Tools uses).
    private const uint FirstSlotNodeId = 3;

    public static readonly InventoryGrid[] All =
    [
        new("InventoryGrid", "Inventory", 0),
        new("InventoryGrid0", "InventoryLarge", 0),
        new("InventoryGrid1", "InventoryLarge", 1),
        new("InventoryGrid0E", "InventoryExpansion", 0),
        new("InventoryGrid1E", "InventoryExpansion", 1),
        new("InventoryGrid2E", "InventoryExpansion", 2),
        new("InventoryGrid3E", "InventoryExpansion", 3),
    ];

    public static InventoryGrid? Find(string addonName)
    {
        foreach (var grid in All)
            if (grid.AddonName == addonName) return grid;
        return null;
    }

    // The bag page (0-3) this grid is currently showing, or null while its
    // layout is closed or on a tab that isn't a bag page.
    public unsafe int? CurrentPage()
    {
        var layout = GameAddons.GetVisible(LayoutName);
        if (layout == null) return null;
        int page = LayoutName switch
        {
            "Inventory" => ((AddonInventory*)layout)->TabIndex,
            "InventoryLarge" => ((AddonInventoryLarge*)layout)->TabIndex * 2 + GridIndex,
            _ => GridIndex,
        };
        return page is >= 0 and < PlayerBags.PageCount ? page : null;
    }

    public static unsafe AtkComponentNode* SlotNode(AtkUnitBase* grid, int cell)
    {
        var node = grid->GetNodeById(FirstSlotNodeId + (uint)cell);
        if (node == null || (ushort)node->Type < 1000) return null;
        var componentNode = (AtkComponentNode*)node;
        return componentNode->Component == null ? null : componentNode;
    }

    public static unsafe AtkComponentDragDrop* DragDropAt(AtkUnitBase* grid, int cell)
    {
        var node = SlotNode(grid, cell);
        if (node == null || node->Component->GetComponentType() != ComponentType.DragDrop) return null;
        return (AtkComponentDragDrop*)node->Component;
    }

    // The cell under the given screen position, or -1.
    public static unsafe int CellAt(AtkUnitBase* grid, short x, short y)
    {
        for (int cell = 0; cell < PlayerBags.SlotsPerPage; cell++)
        {
            var node = SlotNode(grid, cell);
            if (node != null && node->AtkResNode.IsVisible() && node->AtkResNode.CheckCollisionAtCoords(x, y, true))
                return cell;
        }
        return -1;
    }
}
