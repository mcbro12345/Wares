using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

using Wares.Base;

namespace Wares.Game;

// The icon each mark is shown with: the gil symbol for Wares, a boxed M for
// Market, and the player's own Grand Company seal for Seals.
internal static unsafe class MarkIcons
{
    private const uint StormSealIcon = 65004;
    private const uint SerpentSealIcon = 65005;
    private const uint FlameSealIcon = 65006;

    // 1 Maelstrom, 2 Order of the Twin Adder, 3 Immortal Flames, 0 none.
    private static byte GrandCompany
    {
        get
        {
            var ui = UIState.Instance();
            return ui == null ? (byte)0 : ui->PlayerState.GrandCompany;
        }
    }

    // The seal currency's own item icon, for the badge on a bag cell.
    public static uint SealIconId => GrandCompany switch
    {
        2 => SerpentSealIcon,
        3 => FlameSealIcon,
        _ => StormSealIcon,
    };

    // Menu text can't show item icons, so Seals entries use the Grand
    // Company's emblem from the game's text icons instead.
    private static BitmapFontIcon? SealTextIcon => GrandCompany switch
    {
        1 => BitmapFontIcon.LaNoscea,
        2 => BitmapFontIcon.BlackShroud,
        3 => BitmapFontIcon.Thanalan,
        _ => null,
    };

    // An entry in the game's own item menu must have a prefix glyph (Dalamud
    // puts its own boxed "D" on any that hasn't). Wares and Market have glyphs
    // of their own; the seal emblem isn't a font glyph, so a Seals entry gets a
    // blank prefix and keeps the emblem at the start of its text.
    private const SeIconChar BlankPrefix = (SeIconChar)' ';

    public static SeIconChar MenuPrefix(MarkKind kind) => kind switch
    {
        MarkKind.Wares => SeIconChar.Gil,
        MarkKind.Market => SeIconChar.BoxedLetterM,
        _ => BlankPrefix,
    };

    public static SeString MenuText(MarkKind kind, string text)
        => kind == MarkKind.Seals ? Label(kind, text) : new SeStringBuilder().AddText(text).Build();

    // A label led by the mark's icon, for the multi-select menu, which has no
    // prefix of its own.
    public static SeString Label(MarkKind kind, string text)
    {
        var builder = new SeStringBuilder();
        switch (kind)
        {
            case MarkKind.Wares:
                builder.AddText($"{SeIconChar.Gil.ToIconString()} ");
                break;
            case MarkKind.Market:
                builder.AddText($"{SeIconChar.BoxedLetterM.ToIconString()} ");
                break;
            case MarkKind.Seals when SealTextIcon is { } icon:
                builder.AddIcon(icon).AddText(" ");
                break;
        }
        return builder.AddText(text).Build();
    }
}
