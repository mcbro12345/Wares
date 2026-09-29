using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using Wares.Base;

namespace Wares.Game;

// A real bag slot: the container and index the item actually lives in, which
// is not where the inventory window shows it once the bags have been sorted.
internal readonly record struct SlotRef(InventoryType Container, int Slot);

internal static unsafe class PlayerBags
{
    public const int SlotsPerPage = 35;
    public const int PageCount = 4;

    public static InventoryType Page(int page) => (InventoryType)((uint)InventoryType.Inventory1 + (uint)page);

    public static bool IsBag(InventoryType type) => type is >= InventoryType.Inventory1 and <= InventoryType.Inventory4;

    public static InventoryItem* GetItem(SlotRef slot)
    {
        if (!IsBag(slot.Container)) return null;
        var manager = InventoryManager.Instance();
        if (manager == null) return null;
        var container = manager->GetInventoryContainer(slot.Container);
        if (container == null || !container->IsLoaded || slot.Slot < 0 || slot.Slot >= container->Size) return null;
        var item = container->GetInventorySlot(slot.Slot);
        return item == null || item->ItemId == 0 ? null : item;
    }

    // Calls visit for every occupied bag slot, in real (unsorted) order.
    public static void ForEachItem(Action<SlotRef> visit)
    {
        for (int page = 0; page < PageCount; page++)
        for (int slot = 0; slot < SlotsPerPage; slot++)
        {
            var slotRef = new SlotRef(PlayerBags.Page(page), slot);
            if (GetItem(slotRef) != null) visit(slotRef);
        }
    }
}

// Translates a position in the inventory window (page * 35 + cell) to the real
// slot through the game's ItemOrderModule sorter. Which way round the sorter's
// table reads is checked against the icons the window is actually showing
// (see Calibrate), so a wrong assumption corrects itself instead of badging
// the wrong items.
internal static unsafe class InventorySortMap
{
    public enum Mode
    {
        // sorter.Items[displayIndex] names the real page and slot.
        DisplayToReal,
        // sorter.Items[realIndex] names the display page and slot.
        RealToDisplay,
        // No sorting information: display order is real order.
        Identity,
    }

    public static Mode Current { get; private set; } = Mode.DisplayToReal;

    public static SlotRef Resolve(int displayIndex) => Resolve(displayIndex, Current);

    public static SlotRef Resolve(int displayIndex, Mode mode)
    {
        var identity = FromIndex(displayIndex, PlayerBags.SlotsPerPage);
        var module = ItemOrderModule.Instance();
        if (module == null || module->InventorySorter == null || mode == Mode.Identity) return identity;

        var sorter = module->InventorySorter;
        int perPage = sorter->ItemsPerPage > 0 ? sorter->ItemsPerPage : PlayerBags.SlotsPerPage;
        long count = sorter->Items.LongCount;

        if (mode == Mode.DisplayToReal)
        {
            if (displayIndex < 0 || displayIndex >= count) return identity;
            var entry = sorter->Items[displayIndex].Value;
            return entry == null ? identity : new SlotRef(PlayerBags.Page(entry->Page), entry->Slot);
        }

        for (long i = 0; i < count; i++)
        {
            var entry = sorter->Items[i].Value;
            if (entry != null && entry->Page * perPage + entry->Slot == displayIndex)
                return FromIndex((int)i, perPage);
        }
        return identity;
    }

    // samples: (display index, icon id the window shows there). Switches to
    // whichever mode explains the most of them, if it beats the current one.
    public static void Calibrate(ReadOnlySpan<(int DisplayIndex, uint IconId)> samples)
    {
        if (samples.Length < 3) return;
        int best = Score(samples, Current);
        if (best == samples.Length) return;
        var bestMode = Current;
        foreach (var mode in Enum.GetValues<Mode>())
        {
            if (mode == Current) continue;
            int score = Score(samples, mode);
            if (score > best)
            {
                best = score;
                bestMode = mode;
            }
        }
        if (bestMode == Current) return;
        Services.Log.Info($"Inventory sort mapping switched from {Current} to {bestMode} ({best}/{samples.Length} icons matched).");
        Current = bestMode;
    }

    private static int Score(ReadOnlySpan<(int DisplayIndex, uint IconId)> samples, Mode mode)
    {
        int score = 0;
        foreach (var (displayIndex, iconId) in samples)
        {
            var item = PlayerBags.GetItem(Resolve(displayIndex, mode));
            if (item != null && ItemRules.ExpectedIconId(item) == iconId % 1_000_000) score++;
        }
        return score;
    }

    private static SlotRef FromIndex(int index, int perPage)
        => new(PlayerBags.Page(index / perPage), index % perPage);
}
