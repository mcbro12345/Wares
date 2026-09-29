using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

using Wares.Base;

namespace Wares.Game;

// What the game allows to be done with an item in a bag slot. The vendor and
// market-board rules are checked on the individual copy, not just the item
// row, because a copy can lose a permission (a company crest, collectability).
internal static unsafe class ItemRules
{
    private const uint HqOffset = 1_000_000;

    private static ExcelSheet<Item>? itemSheet;

    private static ExcelSheet<Item> Items => itemSheet ??= Services.DataManager.GetExcelSheet<Item>();

    public static Item? Row(InventoryItem* item)
        => item == null ? null : Items.GetRowOrDefault(item->GetBaseItemId());

    // The key marks are stored under: HQ copies are kept apart from NQ ones.
    public static uint MarkKey(InventoryItem* item)
        => item->GetBaseItemId() + (item->IsHighQuality() ? HqOffset : 0);

    public static uint ExpectedIconId(InventoryItem* item)
        => Row(item) is { } row ? row.Icon : 0u;

    public static bool CanSellToVendor(InventoryItem* item)
    {
        if (Row(item) is not { } row) return false;
        return row.PriceLow > 0
            && !row.IsIndisposable
            && !item->IsCollectable();
    }

    public static bool CanListOnMarket(InventoryItem* item)
    {
        if (Row(item) is not { } row) return false;
        var flags = item->GetFlags();
        // A copy can be "Market Prohibited" even when the item itself can be
        // listed: once gear has any spiritbond on it, or a glamour projected
        // onto it, that copy can no longer be traded or put up for sale.
        return !row.IsUntradable
            && row.ItemSearchCategory.RowId != 0
            && !flags.HasFlag(InventoryItem.ItemFlags.Collectable)
            && !flags.HasFlag(InventoryItem.ItemFlags.CompanyCrestApplied)
            && item->GetSpiritbondOrCollectability() == 0
            && item->GetGlamourId() == 0;
    }

    public static bool CanMark(InventoryItem* item, MarkKind kind) => kind switch
    {
        MarkKind.Wares => CanSellToVendor(item),
        MarkKind.Market => CanListOnMarket(item),
        _ => true,
    };
}
