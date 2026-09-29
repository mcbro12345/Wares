using System;
using System.Numerics;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

using Wares.Base;
using Wares.Game;

namespace Wares.UI;

// While an item is dragged the game hides the cell's icon (and the badge in
// it) and draws the item on the drag-drop manager's own floating component,
// DragDropS, which lives in its own top-level window so it draws over
// everything. This puts a copy of the item's badge on that floating icon for
// the length of the drag, at the same spot as in the cell.
internal sealed unsafe class DragBadge : IDisposable
{
    private readonly TextNode gilBadge = InventoryOverlay.CreateBadge(SeIconChar.Gil.ToIconString(), InventoryOverlay.GilColor, InventoryOverlay.GilOutline);
    private readonly TextNode marketBadge = InventoryOverlay.CreateBadge("M", InventoryOverlay.MarketColor, InventoryOverlay.MarketOutline);
    private AtkResNode* attachedTo;

    public void Dispose()
    {
        Detach();
        gilBadge.Dispose();
        marketBadge.Dispose();
    }

    // kind: the mark on the item being dragged out of a bag cell, or None.
    public void Update(MarkKind kind)
    {
        var stage = AtkStage.Instance();
        if (kind == MarkKind.None || stage == null || !stage->DragDropManager.IsDragging)
        {
            Detach();
            return;
        }

        var floating = FloatingIcon(&stage->DragDropManager);
        if (floating == null)
        {
            Detach();
            return;
        }

        if (floating != attachedTo)
        {
            Detach();
            Attach(floating);
        }

        gilBadge.IsVisible = kind == MarkKind.Wares;
        marketBadge.IsVisible = kind == MarkKind.Market;
    }

    private void Attach(AtkResNode* node)
    {
        // Top-right corner of the icon, as in a cell.
        float width = node->Width > 0 ? node->Width : 44.0f;
        var position = new Vector2(width - 19.0f, 2.0f);
        gilBadge.Position = position;
        marketBadge.Position = position;
        gilBadge.AttachNode(node);
        marketBadge.AttachNode(node);
        attachedTo = node;
    }

    private void Detach()
    {
        if (attachedTo == null) return;
        gilBadge.IsVisible = false;
        marketBadge.IsVisible = false;
        gilBadge.DetachNode();
        marketBadge.DetachNode();
        attachedTo = null;
    }

    // The icon component of the floating drag-drop, falling back to the
    // drag-drop itself, then to the DragDropS window.
    private static AtkResNode* FloatingIcon(AtkDragDropManager* manager)
    {
        var dragDropS = manager->DragDropS;
        if (dragDropS != null)
        {
            if (dragDropS->AtkComponentIcon != null && dragDropS->AtkComponentIcon->OwnerNode != null)
                return (AtkResNode*)dragDropS->AtkComponentIcon->OwnerNode;
            if (dragDropS->AtkComponentBase.OwnerNode != null)
                return (AtkResNode*)dragDropS->AtkComponentBase.OwnerNode;
        }
        var addon = GameAddons.GetVisible("DragDropS");
        return addon != null ? addon->RootNode : null;
    }
}
