using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;

using Wares.Base;

namespace Wares.Game;

// Wares and Market marks for the logged-in character. An item holds at most
// one mark: setting either kind removes the other.
//
// With "remember marks per item" on, a mark belongs to the item itself, so
// every copy (and any copy picked up later) carries it. With it off, a mark
// belongs to the one stack that was marked: it follows that stack when it is
// moved between slots, and is gone once the stack is sold, used up or
// discarded.
internal sealed unsafe class MarkStore : IDisposable
{
    private readonly Configuration configuration;
    private readonly List<(SlotRef From, SlotRef To)> pendingMoves = new();
    private bool slotsDirty;

    public MarkStore(Configuration configuration)
    {
        this.configuration = configuration;
        Services.GameInventory.ItemMoved += OnItemMoved;
        Services.GameInventory.ItemRemoved += OnItemRemoved;
    }

    public void Dispose()
    {
        Services.GameInventory.ItemMoved -= OnItemMoved;
        Services.GameInventory.ItemRemoved -= OnItemRemoved;
    }

    private bool PerItem => configuration.RememberMarksByItem;

    private CharacterMarks? Current
    {
        get
        {
            ulong contentId = Services.PlayerState.ContentId;
            if (contentId == 0) return null;
            if (!configuration.Marks.TryGetValue(contentId, out var marks))
                configuration.Marks[contentId] = marks = new CharacterMarks();
            return marks;
        }
    }

    public MarkKind Get(SlotRef slot, InventoryItem* item)
    {
        var marks = Current;
        if (marks == null || item == null) return MarkKind.None;
        uint key = ItemRules.MarkKey(item);

        if (!PerItem)
            return marks.Slots.TryGetValue(SlotId(slot), out var slotMark) && slotMark.ItemKey == key ? slotMark.Kind : MarkKind.None;

        if (marks.Wares.Contains(key)) return MarkKind.Wares;
        if (marks.Market.Contains(key)) return MarkKind.Market;
        return MarkKind.None;
    }

    public void Set(IEnumerable<(SlotRef Slot, uint Key)> items, MarkKind kind)
    {
        var marks = Current;
        if (marks == null) return;
        foreach (var (slot, key) in items)
        {
            if (!PerItem)
            {
                if (kind == MarkKind.None) marks.Slots.Remove(SlotId(slot));
                else marks.Slots[SlotId(slot)] = new SlotMark { ItemKey = key, Kind = kind };
                continue;
            }
            marks.Wares.Remove(key);
            marks.Market.Remove(key);
            if (kind == MarkKind.Wares) marks.Wares.Add(key);
            else if (kind == MarkKind.Market) marks.Market.Add(key);
        }
        configuration.Save();
    }

    public (int Wares, int Market) Counts()
    {
        var marks = Current;
        if (marks == null) return (0, 0);
        if (PerItem) return (marks.Wares.Count, marks.Market.Count);
        return (marks.Slots.Values.Count(mark => mark.Kind == MarkKind.Wares),
            marks.Slots.Values.Count(mark => mark.Kind == MarkKind.Market));
    }

    public void ClearCharacter()
    {
        var marks = Current;
        if (marks == null) return;
        if (PerItem)
        {
            marks.Wares.Clear();
            marks.Market.Clear();
        }
        else
        {
            marks.Slots.Clear();
        }
        configuration.Save();
    }

    // Moves are applied together once per frame so a swap (two moves, each
    // into the other's slot) doesn't overwrite one stack's mark with the other.
    public void OnFrameworkUpdate()
    {
        var marks = Current;
        if (marks != null && pendingMoves.Count > 0)
        {
            var moving = pendingMoves
                .Select(move => (move.To, Found: marks.Slots.TryGetValue(SlotId(move.From), out var mark), Mark: mark))
                .ToList();
            foreach (var (from, _) in pendingMoves) marks.Slots.Remove(SlotId(from));
            foreach (var (to, found, mark) in moving)
                if (found) marks.Slots[SlotId(to)] = mark!;
            slotsDirty = true;
        }
        pendingMoves.Clear();

        if (slotsDirty)
        {
            slotsDirty = false;
            configuration.Save();
        }
    }

    private void OnItemMoved(GameInventoryEvent type, InventoryEventArgs args)
    {
        if (args is not InventoryItemMovedArgs data) return;
        var from = new SlotRef((InventoryType)data.SourceInventory, (int)data.SourceSlot);
        var to = new SlotRef((InventoryType)data.TargetInventory, (int)data.TargetSlot);
        if (PlayerBags.IsBag(from.Container) || PlayerBags.IsBag(to.Container)) pendingMoves.Add((from, to));
    }

    private void OnItemRemoved(GameInventoryEvent type, InventoryEventArgs args)
    {
        if (args is not InventoryItemRemovedArgs data) return;
        var slot = new SlotRef((InventoryType)data.Inventory, (int)data.Slot);
        if (PlayerBags.IsBag(slot.Container) && Current is { } marks && marks.Slots.Remove(SlotId(slot)))
            slotsDirty = true;
    }

    private static string SlotId(SlotRef slot) => $"{(int)slot.Container}:{slot.Slot}";
}
