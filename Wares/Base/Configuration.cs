using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace Wares.Base;

public enum MarkKind
{
    None,
    Wares,
    Market,
}

// A mark on one particular stack, used when marks aren't remembered per item.
public sealed class SlotMark
{
    // The item key the slot held when marked; the mark only counts while the
    // slot still holds that item.
    public uint ItemKey { get; set; }
    public MarkKind Kind { get; set; }
}

// One character's marks. Items are keyed by item id, with HQ copies offset by
// 1,000,000 (the game's own convention), so NQ and HQ stacks mark separately.
public sealed class CharacterMarks
{
    public HashSet<uint> Wares { get; set; } = new();
    public HashSet<uint> Market { get; set; } = new();

    // Per-stack marks, keyed by "container:slot" of the real bag slot.
    public Dictionary<string, SlotMark> Slots { get; set; } = new();
}

public sealed class Configuration : IPluginConfiguration
{
    private const int CurrentVersion = 1;

    public const float DefaultSellButtonWidth = 130.0f;

    public int Version { get; set; } = CurrentVersion;

    public Dictionary<ulong, CharacterMarks> Marks { get; set; } = new();

    // Marks
    // On: a mark applies to every copy of the item, now and later. Off: it
    // applies only to the stack that was marked.
    public bool RememberMarksByItem { get; set; } = true;

    // Inventory display
    public bool ShowBadges { get; set; } = true;
    public bool EnableMultiSelect { get; set; } = true;

    // Selling
    public bool AutoConfirmPrompts { get; set; } = true;
    public bool SellAtRetainer { get; set; } = true;
    public int SellIntervalMs { get; set; } = 250;
    public bool PrintSellSummary { get; set; } = true;

    // Sell button placement, one offset per inventory layout (keyed by the
    // layout's addon name) so each can be lined up on its own.
    public Dictionary<string, Vector2> SellButtonOffsets { get; set; } = new();
    public float SellButtonWidth { get; set; } = DefaultSellButtonWidth;
    // Shows the button outside vendors so it can be positioned.
    public bool PreviewSellButton { get; set; }

    // Debugging
    public bool DebugLogging { get; set; }

    public void Migrate()
    {
        Marks ??= new();
        foreach (var marks in Marks.Values) marks.Slots ??= new();
        SellButtonOffsets ??= new();
        Version = CurrentVersion;
    }

    public void Save() => Services.PluginInterface.SavePluginConfig(this);
}
