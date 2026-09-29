// Adapted from SimpleTweaks' "Quick Sell Items at Vendors" tweak
// (Tweaks/QuickSellItems.cs and the GenerateCallback / ValueString helpers in
// Utility/Common.cs and Utility/Extensions.cs), Copyright Caraxi and
// contributors, used under the GNU Affero General Public License v3.0.
// https://github.com/Caraxi/SimpleTweaksPlugin
//
// SimpleTweaks sells by letting the game build an item's context menu, firing
// the menu's "Sell" entry itself and closing the menu, all inside one frame,
// so the menu is never drawn. The only change here is that the menu is opened
// on request for a given slot instead of from a hook on a right-click.
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

using Wares.Base;

namespace Wares.Game;

internal static unsafe class QuickSell
{
    private static string? vendorSellText;
    private static string? retainerSellText;

    // Set while a menu is being opened for a sale, so this plugin's own
    // context-menu entries are not added to it.
    public static bool IsSelling { get; private set; }

    // Addon row 93 is the vendor menu's "Sell"; 5480 is the retainer menu's
    // "Have Retainer Sell Items".
    public static string VendorSellText => vendorSellText ??= AddonText(93, "Sell");
    public static string RetainerSellText => retainerSellText ??= AddonText(5480, "Have Retainer Sell Items");

    public static bool TrySell(SlotRef slot, string sellText, uint ownerAddonId)
    {
        var agent = AgentInventoryContext.Instance();
        if (agent == null || PlayerBags.GetItem(slot) == null) return false;

        IsSelling = true;
        try
        {
            agent->OpenForItemSlot(slot.Container, slot.Slot, 0, ownerAddonId);

            var agentAddonId = agent->AgentInterface.GetAddonId();
            if (agentAddonId == 0) return false;
            var addon = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)agentAddonId);
            if (addon == null) return false;

            for (var i = 0; i < agent->ContextItemCount; i++)
            {
                var contextItemParam = agent->EventParams[agent->ContexItemStartIndex + i];
                if (contextItemParam.Type is not (AtkValueType.String or AtkValueType.ManagedString)) continue;
                var contextItemName = ValueString(contextItemParam);

                if (contextItemName != sellText) continue;
                GenerateCallback(addon, 0, i, 0U, 0, 0);
                agent->AgentInterface.Hide();
                addon->Close(false);
                return true;
            }

            // No sell entry (the item can't be sold here): close the menu again.
            agent->AgentInterface.Hide();
            addon->Close(false);
            return false;
        }
        finally
        {
            IsSelling = false;
        }
    }

    private static string ValueString(AtkValue v)
        => Marshal.PtrToStringUTF8(new System.IntPtr(v.String))?.TrimEnd('\0') ?? string.Empty;

    // SimpleTweaks' Common.GenerateCallback, reduced to the int / uint values
    // the context menu takes.
    private static void GenerateCallback(AtkUnitBase* unitBase, params object[] values)
    {
        var atkValues = stackalloc AtkValue[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            switch (values[i])
            {
                case uint uintValue:
                    atkValues[i].Type = AtkValueType.UInt;
                    atkValues[i].UInt = uintValue;
                    break;
                case int intValue:
                    atkValues[i].Type = AtkValueType.Int;
                    atkValues[i].Int = intValue;
                    break;
            }
        }
        unitBase->FireCallback((uint)values.Length, atkValues);
    }

    private static string AddonText(uint rowId, string fallback)
        => Services.DataManager.GetExcelSheet<Addon>().GetRowOrDefault(rowId) is { } row
            ? row.Text.ExtractText()
            : fallback;
}
