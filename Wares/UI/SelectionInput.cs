using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Wares.Base;
using Wares.Game;

using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace Wares.UI;

// Ctrl+left-click on a bag cell toggles it in the multi-selection; a left
// click without Ctrl anywhere clears the selection, like Explorer.
//
// None of the game's own events are swallowed: blocking the grid's drag
// events left cells believing a drag was still in progress, which stopped
// them opening their item menu. Instead, while Ctrl is held every bag cell
// gets the game's own "locked" drag-drop flag, so a Ctrl+click can't pick the
// item up, and the click itself is read straight from the cursor input.
internal sealed unsafe class SelectionInput : IDisposable
{
    private readonly Configuration configuration;
    private readonly SlotSelection selection;
    private readonly Action<SlotRef> onCtrlRightClick;
    // Cells this class locked, so only those are unlocked again.
    private readonly HashSet<nint> lockedCells = new();

    public SelectionInput(Configuration configuration, SlotSelection selection, Action<SlotRef> onCtrlRightClick)
    {
        this.configuration = configuration;
        this.selection = selection;
        this.onCtrlRightClick = onCtrlRightClick;
    }

    public void Dispose() => UpdateCellLocks(false);

    private static bool CtrlHeld => Services.KeyState[VirtualKey.CONTROL];

    public void OnFrameworkUpdate()
    {
        bool selecting = configuration.EnableMultiSelect && CtrlHeld;
        UpdateCellLocks(selecting);

        var framework = GameFramework.Instance();
        if (framework != null && (framework->CursorInputs.MouseButtonPressedFlags & MouseButtonFlags.LBUTTON) != 0)
        {
            if (!selecting) selection.Clear();
            else if (CellUnderCursor(framework->CursorInputs) is { } slot) selection.Toggle(slot);
        }

        // A locked cell may ignore the right-click too, so while Ctrl is held
        // the item menu is opened from here.
        if (selecting && framework != null && (framework->CursorInputs.MouseButtonPressedFlags & MouseButtonFlags.RBUTTON) != 0
            && CellUnderCursor(framework->CursorInputs) is { } rightClicked)
            onCtrlRightClick(rightClicked);

        if (selection.Count > 0 && GameAddons.VisibleInventoryLayout() == null) selection.Clear();
        selection.Prune();
    }

    private void UpdateCellLocks(bool locked)
    {
        foreach (var grid in InventoryGrid.All)
        {
            var addon = GameAddons.Get(grid.AddonName);
            if (addon == null) continue;
            for (int cell = 0; cell < PlayerBags.SlotsPerPage; cell++)
            {
                var dragDrop = InventoryGrid.DragDropAt(addon, cell);
                if (dragDrop == null) continue;
                if (locked && addon->IsVisible)
                {
                    if (dragDrop->Flags.HasFlag(DragDropFlag.Locked)) continue;
                    dragDrop->Flags |= DragDropFlag.Locked;
                    lockedCells.Add((nint)dragDrop);
                }
                else if (lockedCells.Remove((nint)dragDrop))
                {
                    dragDrop->Flags &= ~DragDropFlag.Locked;
                }
            }
        }
        // Anything left belonged to a grid that has since closed.
        if (!locked) lockedCells.Clear();
    }

    private SlotRef? CellUnderCursor(CursorInputData cursor)
    {
        var stage = AtkStage.Instance();
        var underCursor = stage != null && stage->AtkCollisionManager != null ? stage->AtkCollisionManager->IntersectingAddon : null;
        short x = (short)cursor.PositionX, y = (short)cursor.PositionY;

        foreach (var grid in InventoryGrid.All)
        {
            var addon = GameAddons.GetVisible(grid.AddonName);
            if (addon == null || grid.CurrentPage() is not { } page) continue;
            // Ignore cells covered by another window.
            if (underCursor != null && underCursor != addon && underCursor != GameAddons.Get(grid.LayoutName)) continue;

            int cell = InventoryGrid.CellAt(addon, x, y);
            if (cell < 0) continue;
            if (configuration.DebugLogging) Services.Log.Info($"Ctrl+click on {grid.AddonName} cell {cell}.");
            var slot = InventorySortMap.Resolve(page * PlayerBags.SlotsPerPage + cell);
            return PlayerBags.GetItem(slot) != null ? slot : null;
        }
        return null;
    }
}
