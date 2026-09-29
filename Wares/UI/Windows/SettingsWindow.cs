using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Wares.Base;
using Wares.Game;

namespace Wares.UI.Windows;

public sealed class SettingsWindow : Window
{
    private static readonly (string Layout, string Label)[] Layouts =
    [
        ("Inventory", "Normal inventory"),
        ("InventoryLarge", "Larger inventory"),
        ("InventoryExpansion", "Open all bags"),
    ];

    private readonly Configuration configuration;
    private readonly Func<(int Wares, int Market)> markCounts;
    private readonly Action clearMarks;
    private bool confirmingClear;

    internal SettingsWindow(Configuration configuration, Func<(int Wares, int Market)> markCounts, Action clearMarks)
        : base("Wares Settings###WaresSettings")
    {
        this.configuration = configuration;
        this.markCounts = markCounts;
        this.clearMarks = clearMarks;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 300),
            MaximumSize = new Vector2(720, 900),
        };
    }

    public override void OnClose()
    {
        // The preview is only for lining the button up; don't leave it on.
        if (!configuration.PreviewSellButton) return;
        configuration.PreviewSellButton = false;
        configuration.Save();
    }

    public override void Draw()
    {
        Section("INVENTORY");
        Toggle("Show Wares and Market badges on items", configuration.ShowBadges, v => configuration.ShowBadges = v);
        Toggle("Ctrl+click to select several items", configuration.EnableMultiSelect, v => configuration.EnableMultiSelect = v,
            "Ctrl+left-click items to select them, then right-click one of them to mark them all. Clicking anywhere else clears the selection.");

        Section("SELLING");
        Toggle("Accept sale confirmation prompts automatically", configuration.AutoConfirmPrompts, v => configuration.AutoConfirmPrompts = v,
            "Answers Yes to the prompts the game shows before selling unique, untradable, high-quality or melded items.");
        Toggle("Also sell wares through a retainer", configuration.SellAtRetainer, v => configuration.SellAtRetainer = v,
            "Shows the Sell Wares button while a retainer's inventory is open, and sells with \"Have Retainer Sell Items\".");
        Toggle("Print a summary in chat after selling", configuration.PrintSellSummary, v => configuration.PrintSellSummary = v);
        ImGui.SetNextItemWidth(200);
        int interval = configuration.SellIntervalMs;
        if (ImGui.SliderInt("Delay between sales", ref interval, 100, 1000, "%d ms"))
            configuration.SellIntervalMs = interval;
        SaveAfterEdit();

        Section("SELL BUTTON POSITION");
        DrawButtonPosition();

        Section("MARKS");
        Toggle("Remember marks for every copy of an item", configuration.RememberMarksByItem, v => configuration.RememberMarksByItem = v,
            "On: marking an item marks every copy of it, including ones you pick up later.\n" +
            "Off: a mark applies only to the stack you marked, follows it when you move it, and is gone once it's sold or used up.\n" +
            "Each mode keeps its own marks, so switching back brings the old ones back.");
        DrawMarks();

        Section("DEBUG");
        Toggle("Log sell and click details", configuration.DebugLogging, v => configuration.DebugLogging = v);
    }

    // Chat 2's "Adjust Position" control: one full-width X / Y drag per layout,
    // applied live as it's dragged. Ctrl+click a number to type it.
    private void DrawButtonPosition()
    {
        Toggle("Show the button now to position it", configuration.PreviewSellButton, v => configuration.PreviewSellButton = v,
            "Shows the Sell Wares button in the inventory even away from a vendor. Turns itself off when this window closes.");
        ImGui.Spacing();

        foreach (var (layout, label) in Layouts)
        {
            ImGui.TextUnformatted(label);
            ImGui.SetNextItemWidth(-1);
            var offset = configuration.SellButtonOffsets.TryGetValue(layout, out var saved) ? saved : SellButton.DefaultOffset(layout);
            if (ImGui.DragFloat2($"##SellButtonOffset{layout}", ref offset, 1, -2000, 4000, "%.0fpx"))
                configuration.SellButtonOffsets[layout] = offset;
            SaveAfterEdit();
            ImGui.Spacing();
        }

        ImGui.TextUnformatted("Button width");
        ImGui.SetNextItemWidth(-1);
        float width = configuration.SellButtonWidth;
        if (ImGui.DragFloat("##SellButtonWidth", ref width, 1, 80, 300, "%.0fpx"))
            configuration.SellButtonWidth = width;
        SaveAfterEdit();

        ImGui.TextDisabled("Offsets are measured from the inventory window's top-left corner.");
        if (ImGui.Button("Reset position and width"))
        {
            configuration.SellButtonOffsets.Clear();
            configuration.SellButtonWidth = Configuration.DefaultSellButtonWidth;
            configuration.Save();
        }
    }

    private void DrawMarks()
    {
        var (wares, market) = markCounts();
        ImGui.TextUnformatted($"This character has {wares} item{(wares == 1 ? "" : "s")} marked for Wares and {market} for Market.");
        if (!confirmingClear)
        {
            ImGui.BeginDisabled(wares + market == 0);
            if (ImGui.Button("Clear all marks")) confirmingClear = true;
            ImGui.EndDisabled();
            return;
        }

        ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.4f, 1.0f), "Remove every mark on this character?");
        if (ImGui.Button("Yes, clear them"))
        {
            clearMarks();
            confirmingClear = false;
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) confirmingClear = false;
    }

    private void SaveAfterEdit()
    {
        if (ImGui.IsItemDeactivatedAfterEdit()) configuration.Save();
    }

    private void Toggle(string label, bool value, Action<bool> set, string? tooltip = null)
    {
        if (ImGui.Checkbox(label, ref value))
        {
            set(value);
            configuration.Save();
        }
        if (tooltip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title);
        ImGui.Separator();
    }
}
