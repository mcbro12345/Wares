using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.ContextMenu;

using Wares.Base;

using NativeContextMenu = KamiToolKit.ContextMenu.ContextMenu;

namespace Wares.Game;

// Right-clicking a single item adds the mark / unmark entries to the game's
// own item menu. Right-clicking one of several selected items opens a menu
// holding only the Wares entries instead, since the game's entries (Discard,
// Link...) act on one item, not the selection. Entries for a mark the item(s)
// can't take are shown greyed out.
internal sealed unsafe class ItemContextMenu : IDisposable
{
    private const char PrefixGlyph = 'W';

    private readonly MarkStore marks;
    private readonly SlotSelection selection;
    // The game's "open the item menu for this slot" call. For a multi-selection
    // it is skipped entirely, so the game's menu never opens (no sound, no
    // flash) and the Wares-only menu opens in its place.
    private readonly Hook<AgentInventoryContext.Delegates.OpenForItemSlot> openForItemSlotHook;
    private NativeContextMenu? selectionMenu;
    private List<Entry>? pendingSelection;
    private long lastOpenedAt = long.MinValue / 2;

    private const long DuplicateOpenWindowMs = 300;

    public ItemContextMenu(MarkStore marks, SlotSelection selection)
    {
        this.marks = marks;
        this.selection = selection;
        openForItemSlotHook = Services.GameInterop.HookFromAddress<AgentInventoryContext.Delegates.OpenForItemSlot>(
            AgentInventoryContext.Addresses.OpenForItemSlot.Value, OpenForItemSlotDetour);
        openForItemSlotHook.Enable();
        Services.ContextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose()
    {
        Services.ContextMenu.OnMenuOpened -= OnMenuOpened;
        openForItemSlotHook.Dispose();
        selectionMenu?.Dispose();
    }

    public void OnFrameworkUpdate()
    {
        if (pendingSelection is not { } entries) return;
        pendingSelection = null;
        OpenSelectionMenu(entries);
    }

    // Ctrl+right-click on a bag cell. While Ctrl is held the cells are locked
    // (see SelectionInput) and the game may not open the item menu at all, so
    // the click is handled here instead: the selection menu for a selected
    // item, the game's normal menu for any other.
    public void OpenForRightClick(SlotRef slot)
    {
        if (RecentlyOpened()) return;
        if (TryQueueSelectionMenu(slot)) return;

        selection.Clear();
        var agent = AgentInventoryContext.Instance();
        var owner = GameAddons.VisibleInventoryLayout();
        if (agent == null || owner == null) return;
        lastOpenedAt = Environment.TickCount64;
        openForItemSlotHook.Original(agent, slot.Container, slot.Slot, 0, owner->Id);
    }

    private void OpenForItemSlotDetour(AgentInventoryContext* agent, InventoryType inventoryType, int slot, int a4, uint addonId)
    {
        try
        {
            if (!QuickSell.IsSelling)
            {
                // The same right-click was already handled by OpenForRightClick.
                if (RecentlyOpened()) return;
                if (TryQueueSelectionMenu(new SlotRef(inventoryType, slot))) return;
                lastOpenedAt = Environment.TickCount64;
            }
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Failed to handle the item menu for the selection.");
        }
        openForItemSlotHook.Original(agent, inventoryType, slot, a4, addonId);
    }

    // One right-click can reach both the game's menu call and
    // OpenForRightClick; whichever comes first wins.
    private bool RecentlyOpened() => Environment.TickCount64 - lastOpenedAt < DuplicateOpenWindowMs;

    private bool TryQueueSelectionMenu(SlotRef clicked)
    {
        if (selection.Count < 2 || !selection.Contains(clicked)) return false;
        var entries = selection.Snapshot().Select(Describe).Where(entry => entry.Key != 0).ToList();
        if (entries.Count < 2) return false;
        pendingSelection = entries;
        lastOpenedAt = Environment.TickCount64;
        return true;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (QuickSell.IsSelling) return;
        if (args.MenuType != ContextMenuType.Inventory || args.Target is not MenuTargetInventory { TargetItem: { } target }) return;

        var clicked = new SlotRef((InventoryType)target.ContainerType, (int)target.InventorySlot);
        if (!PlayerBags.IsBag(clicked.Container) || PlayerBags.GetItem(clicked) == null) return;

        selection.Clear();
        var entry = Describe(clicked);
        if (entry.Key != 0) AddSingleItemEntries(args, entry);
    }

    private void AddSingleItemEntries(IMenuOpenedArgs args, Entry entry)
    {
        if (entry.Mark == MarkKind.Wares)
            Add(args, "Unmark Wares", true, () => marks.Set([(entry.Slot, entry.Key)], MarkKind.None));
        else
            Add(args, "Mark for Wares", entry.CanSell, () => marks.Set([(entry.Slot, entry.Key)], MarkKind.Wares));

        if (entry.Mark == MarkKind.Market)
            Add(args, "Unmark Market", true, () => marks.Set([(entry.Slot, entry.Key)], MarkKind.None));
        else
            Add(args, "Mark for Market", entry.CanList, () => marks.Set([(entry.Slot, entry.Key)], MarkKind.Market));
    }

    private void OpenSelectionMenu(List<Entry> entries)
    {
        var sellable = entries.Where(entry => entry.CanSell).ToList();
        var listable = entries.Where(entry => entry.CanList).ToList();
        var marked = entries.Where(entry => entry.Mark != MarkKind.None).ToList();
        int total = entries.Count;

        selectionMenu ??= new NativeContextMenu();
        selectionMenu.Clear();
        // The menu lists the lowest DisplayPriority first, so the order below
        // is passed negated: Wares, then Market, then Clear.
        AddSelectionItem(BulkLabel("Wares", sellable.Count, total), sellable.Count > 0, 3,
            () => MarkSelection(sellable, MarkKind.Wares));
        AddSelectionItem(BulkLabel("Market", listable.Count, total), listable.Count > 0, 2,
            () => MarkSelection(listable, MarkKind.Market));
        if (marked.Count > 0)
            AddSelectionItem($"Clear Marks on {marked.Count} Item{(marked.Count == 1 ? "" : "s")}", true, 1,
                () => MarkSelection(marked, MarkKind.None));

        // Other plugins add item entries (Search in Market Board, Vendor
        // Location...) to any menu opened over a hovered item. This menu is
        // about the selection, not one item, so it's opened with none hovered.
        Services.GameGui.HoveredItem = 0;
        selectionMenu.Open();
    }

    private void AddSelectionItem(string name, bool enabled, int priority, Action onClick)
        => selectionMenu!.AddItem(new ContextMenuItem
        {
            Name = name,
            IsEnabled = enabled,
            OnClick = onClick,
            DisplayPriority = -priority,
        });

    private void MarkSelection(IEnumerable<Entry> entries, MarkKind kind)
    {
        marks.Set(entries.Select(entry => (entry.Slot, entry.Key)), kind);
        selection.Clear();
    }

    private static string BulkLabel(string kind, int eligible, int total)
        => eligible == total ? $"Mark {total} Items for {kind}" : $"Mark {eligible} of {total} Items for {kind}";

    private static void Add(IMenuOpenedArgs args, string name, bool enabled, Action onClicked)
        => args.AddMenuItem(new MenuItem
        {
            Name = name,
            PrefixChar = PrefixGlyph,
            IsEnabled = enabled,
            OnClicked = _ => onClicked(),
        });

    private Entry Describe(SlotRef slot)
    {
        var item = PlayerBags.GetItem(slot);
        if (item == null) return default;
        return new Entry(slot, ItemRules.MarkKey(item), marks.Get(slot, item), ItemRules.CanSellToVendor(item), ItemRules.CanListOnMarket(item));
    }

    private readonly record struct Entry(SlotRef Slot, uint Key, MarkKind Mark, bool CanSell, bool CanList);
}
