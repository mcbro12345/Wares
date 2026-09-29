using System.Collections.Generic;
using System.Linq;

namespace Wares.Game;

// The Ctrl+click multi-selection. Each slot remembers which item it held when
// selected, so a slot whose item has moved or been used drops out on its own.
internal sealed unsafe class SlotSelection
{
    private readonly Dictionary<SlotRef, uint> selected = new();

    public int Count => selected.Count;

    public bool Contains(SlotRef slot) => selected.ContainsKey(slot);

    public void Toggle(SlotRef slot)
    {
        if (selected.Remove(slot)) return;
        var item = PlayerBags.GetItem(slot);
        if (item != null) selected[slot] = ItemRules.MarkKey(item);
    }

    public void Clear() => selected.Clear();

    public List<SlotRef> Snapshot()
    {
        Prune();
        return selected.Keys.ToList();
    }

    public void Prune()
    {
        if (selected.Count == 0) return;
        foreach (var (slot, key) in selected.ToList())
        {
            var item = PlayerBags.GetItem(slot);
            if (item == null || ItemRules.MarkKey(item) != key) selected.Remove(slot);
        }
    }
}
