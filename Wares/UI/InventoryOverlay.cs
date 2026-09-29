using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

using Wares.Base;
using Wares.Game;

using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace Wares.UI;

// Draws the Wares (gil) and Market (M) badges on bag cells, and the hover-style
// highlight on multi-selected cells, as native nodes inside each cell.
internal sealed unsafe class InventoryOverlay : IDisposable
{
    private const int CalibrateEveryFrames = 30;

    public static readonly Vector4 GilColor = new(1.0f, 0.93f, 0.55f, 1.0f);
    public static readonly Vector4 GilOutline = new(0.4f, 0.26f, 0.0f, 1.0f);
    public static readonly Vector4 MarketColor = new(1.0f, 0.84f, 0.35f, 1.0f);
    public static readonly Vector4 MarketOutline = new(0.35f, 0.18f, 0.0f, 1.0f);

    private readonly Configuration configuration;
    private readonly MarkStore marks;
    private readonly SlotSelection selection;
    private readonly List<AddonController> controllers = new();
    private readonly Dictionary<string, CellOverlay[]> overlays = new();
    private readonly DragBadge dragBadge;
    private readonly (int, uint)[] samples = new (int, uint)[PlayerBags.SlotsPerPage];
    private int frame;
    private MarkKind pressedKind;
    private bool wasDragging;

    public InventoryOverlay(Configuration configuration, MarkStore marks, SlotSelection selection)
    {
        this.configuration = configuration;
        this.marks = marks;
        this.selection = selection;
        dragBadge = new DragBadge();
        foreach (var grid in InventoryGrid.All)
        {
            var controller = new AddonController
            {
                AddonName = grid.AddonName,
                OnSetup = addon => Attach(grid, addon),
                OnFinalize = _ => Detach(grid),
                OnUpdate = _ => Refresh(grid),
            };
            controller.Enable();
            controllers.Add(controller);
        }
    }

    public void Dispose()
    {
        foreach (var controller in controllers) controller.Dispose();
        controllers.Clear();
        foreach (var grid in InventoryGrid.All) Detach(grid);
        dragBadge.Dispose();
    }

    public static TextNode CreateBadge(string text, Vector4 color, Vector4 outline) => new()
    {
        String = text,
        Size = new Vector2(16.0f),
        FontSize = 14,
        FontType = FontType.Axis,
        TextFlags = TextFlags.Edge | TextFlags.Emboss,
        TextColor = color,
        TextOutlineColor = outline,
        AlignmentType = AlignmentType.Center,
        IsVisible = false,
    };

    // Keeps a dragged item's badge on the floating drag icon. The dragged
    // item is the one the button was pressed on before the drag began (a
    // second press while dragging is the drop, so it doesn't count).
    public void OnFrameworkUpdate()
    {
        var stage = AtkStage.Instance();
        bool dragging = stage != null && stage->DragDropManager.IsDragging;
        var framework = GameFramework.Instance();
        // Judged by last frame's drag state: the game can start the drag in
        // the same frame as the press, before this runs.
        if (!wasDragging && framework != null)
        {
            var cursor = framework->CursorInputs;
            if ((cursor.MouseButtonPressedFlags & MouseButtonFlags.LBUTTON) != 0)
                pressedKind = KindAt((short)cursor.PositionX, (short)cursor.PositionY);
            else if (!dragging && (cursor.MouseButtonHeldFlags & MouseButtonFlags.LBUTTON) == 0)
                pressedKind = MarkKind.None;
        }

        var draggedKind = MarkKind.None;
        foreach (var cells in overlays.Values)
        foreach (var cell in cells)
            if (cell != null && cell.IsBeingDragged) draggedKind = cell.Kind;
        var kind = draggedKind != MarkKind.None ? draggedKind : pressedKind;

        wasDragging = dragging;
        dragBadge.Update(dragging ? kind : MarkKind.None);
    }

    private MarkKind KindAt(short x, short y)
    {
        foreach (var grid in InventoryGrid.All)
        {
            var addon = GameAddons.GetVisible(grid.AddonName);
            if (addon == null || !overlays.TryGetValue(grid.AddonName, out var cells)) continue;
            int cell = InventoryGrid.CellAt(addon, x, y);
            if (cell >= 0 && cells[cell] != null) return cells[cell].Kind;
        }
        return MarkKind.None;
    }

    private void Attach(InventoryGrid grid, AtkUnitBase* addon)
    {
        Detach(grid);
        var cells = new CellOverlay[PlayerBags.SlotsPerPage];
        for (int cell = 0; cell < cells.Length; cell++)
        {
            var slotNode = InventoryGrid.SlotNode(addon, cell);
            if (slotNode != null) cells[cell] = new CellOverlay(slotNode);
        }
        overlays[grid.AddonName] = cells;
    }

    private void Detach(InventoryGrid grid)
    {
        if (!overlays.Remove(grid.AddonName, out var cells)) return;
        foreach (var cell in cells) cell?.Dispose();
    }

    private void Refresh(InventoryGrid grid)
    {
        if (!overlays.TryGetValue(grid.AddonName, out var cells)) return;
        int? page = grid.CurrentPage();
        bool calibrate = ++frame % CalibrateEveryFrames == 0;
        int sampleCount = 0;

        for (int cell = 0; cell < cells.Length; cell++)
        {
            var overlay = cells[cell];
            if (overlay == null) continue;
            if (page is not { } shownPage)
            {
                overlay.Show(MarkKind.None, false);
                continue;
            }

            int displayIndex = shownPage * PlayerBags.SlotsPerPage + cell;
            var slot = InventorySortMap.Resolve(displayIndex);
            var item = PlayerBags.GetItem(slot);
            var kind = item == null || !configuration.ShowBadges ? MarkKind.None : marks.Get(slot, item);
            overlay.Show(kind, item != null && selection.Contains(slot));

            if (calibrate && overlay.NativeIconId is var iconId and not 0)
                samples[sampleCount++] = (displayIndex, iconId);
        }

        if (sampleCount > 0) InventorySortMap.Calibrate(samples.AsSpan(0, sampleCount));
    }

    private sealed class CellOverlay : IDisposable
    {
        private readonly AtkComponentNode* slotNode;
        private readonly ImageNode highlight;
        private readonly TextNode gilBadge;
        private readonly TextNode marketBadge;
        private readonly AtkComponentIcon* icon;

        public CellOverlay(AtkComponentNode* slotNode)
        {
            this.slotNode = slotNode;
            float width = slotNode->AtkResNode.Width > 0 ? slotNode->AtkResNode.Width : 44.0f;
            float height = slotNode->AtkResNode.Height > 0 ? slotNode->AtkResNode.Height : 48.0f;
            float scale = width / 44.0f;

            // The game's own hovered-slot glow: IconA_Frame part 16, drawn
            // 72x72 and offset so it frames a 44px cell (as in IconExtras).
            highlight = new ImageNode
            {
                Size = new Vector2(72.0f * scale),
                Position = new Vector2(-14.0f * scale, -12.0f * scale),
                PartId = 16,
                WrapMode = KamiToolKit.Enums.WrapMode.Tile,
                IsVisible = false,
            };
            IconNodeTextureHelper.LoadIconAFrameTexture(highlight);
            highlight.AttachNode(slotNode);

            // The badges live inside the cell's icon component, top-right.
            // While the item is dragged the game hides that icon and draws a
            // floating copy instead; DragBadge follows that copy.
            var iconNode = IconNodeOf(slotNode);
            icon = iconNode != null ? (AtkComponentIcon*)iconNode->Component : null;
            var badgeParent = iconNode != null ? iconNode : slotNode;
            var badgePosition = new Vector2((badgeParent->AtkResNode.Width > 0 ? badgeParent->AtkResNode.Width : width) - 19.0f, 2.0f);

            gilBadge = CreateBadge(SeIconChar.Gil.ToIconString(), GilColor, GilOutline);
            gilBadge.Position = badgePosition;
            gilBadge.AttachNode(badgeParent);

            marketBadge = CreateBadge("M", MarketColor, MarketOutline);
            marketBadge.Position = badgePosition;
            marketBadge.AttachNode(badgeParent);
        }

        public MarkKind Kind { get; private set; }

        public bool IsBeingDragged => icon != null && icon->Flags.HasFlag(IconComponentFlags.IsBeingDragged);

        private static AtkComponentNode* IconNodeOf(AtkComponentNode* slotNode)
        {
            if (slotNode->Component->GetComponentType() != ComponentType.DragDrop) return null;
            var icon = ((AtkComponentDragDrop*)slotNode->Component)->AtkComponentIcon;
            return icon == null ? null : icon->OwnerNode;
        }

        public uint NativeIconId
        {
            get
            {
                var dragDrop = slotNode->Component == null ? null : (AtkComponentDragDrop*)slotNode->Component;
                return dragDrop == null || slotNode->Component->GetComponentType() != ComponentType.DragDrop
                    ? 0u
                    : (uint)Math.Max(0, dragDrop->GetIconId());
            }
        }

        public void Show(MarkKind kind, bool selected)
        {
            Kind = kind;
            highlight.IsVisible = selected;
            gilBadge.IsVisible = kind == MarkKind.Wares;
            marketBadge.IsVisible = kind == MarkKind.Market;
        }

        public void Dispose()
        {
            highlight.Dispose();
            gilBadge.Dispose();
            marketBadge.Dispose();
        }
    }
}
