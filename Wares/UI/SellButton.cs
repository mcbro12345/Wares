using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

using Wares.Base;
using Wares.Game;

namespace Wares.UI;

// A native "Sell Wares" button inside whichever inventory layout is open. It
// only appears while talking to a vendor (or at a retainer, if enabled) and
// sits at the per-layout offset set in the settings.
internal sealed unsafe class SellButton : IDisposable
{
    public const float Height = 28.0f;

    private readonly Configuration configuration;
    private readonly SellController seller;
    private readonly List<AddonController> controllers = new();
    private readonly Dictionary<string, TextButtonNode> buttons = new();

    public SellButton(Configuration configuration, SellController seller)
    {
        this.configuration = configuration;
        this.seller = seller;
        foreach (string layout in GameAddons.InventoryLayouts)
        {
            var controller = new AddonController
            {
                AddonName = layout,
                OnSetup = addon => Attach(layout, addon),
                OnFinalize = _ => Detach(layout),
                OnUpdate = _ => Refresh(layout),
            };
            controller.Enable();
            controllers.Add(controller);
        }
    }

    public void Dispose()
    {
        foreach (var controller in controllers) controller.Dispose();
        controllers.Clear();
        foreach (string layout in GameAddons.InventoryLayouts) Detach(layout);
    }

    // Where the button goes in a layout when it hasn't been moved yet: the
    // bottom-left corner, clear of the window frame.
    public static Vector2 DefaultOffset(string layout)
    {
        var addon = GameAddons.Get(layout);
        float height = addon != null && addon->RootNode != null ? addon->RootNode->Height : 400.0f;
        return new Vector2(16.0f, MathF.Max(0.0f, height - Height - 12.0f));
    }

    public Vector2 OffsetFor(string layout)
        => configuration.SellButtonOffsets.TryGetValue(layout, out var offset) ? offset : DefaultOffset(layout);

    private void Attach(string layout, AtkUnitBase* addon)
    {
        Detach(layout);
        var button = new TextButtonNode
        {
            Size = new Vector2(configuration.SellButtonWidth, Height),
            String = "Sell Wares",
            IsVisible = false,
            OnClick = seller.Toggle,
        };
        button.AttachNode(addon);
        buttons[layout] = button;
    }

    private void Detach(string layout)
    {
        if (buttons.Remove(layout, out var button)) button.Dispose();
    }

    private void Refresh(string layout)
    {
        if (!buttons.TryGetValue(layout, out var button)) return;

        bool atVendor = seller.Venue != SellVenue.None;
        button.IsVisible = atVendor || configuration.PreviewSellButton || seller.IsRunning;
        if (!button.IsVisible) return;

        button.Position = OffsetFor(layout);
        if (Math.Abs(button.Width - configuration.SellButtonWidth) > 0.5f)
            button.Size = new Vector2(configuration.SellButtonWidth, Height);

        if (seller.IsRunning)
        {
            SetLabel(button, "Stop Selling");
            button.IsEnabled = true;
            return;
        }

        int count = seller.CountSellableWares();
        SetLabel(button, count > 0 ? $"Sell Wares ({count})" : "Sell Wares");
        button.IsEnabled = atVendor && count > 0;
    }

    private static void SetLabel(TextButtonNode button, string label)
    {
        if (button.String.ToString() != label) button.String = label;
    }
}
