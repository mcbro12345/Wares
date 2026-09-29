using System.Collections.Generic;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Wares.Base;

namespace Wares.Game;

internal enum SellVenue
{
    None,
    Vendor,
    Retainer,
}

// Sells every Wares-marked item in the bags, one at a time: each sale waits
// until the slot actually empties (or gives up after a few seconds) before
// the next item goes, and any confirmation prompt the game raises for a sale
// (unique, untradable, HQ, melded...) is accepted while a sale is pending.
internal sealed unsafe class SellController(Configuration configuration, MarkStore marks)
{
    private const long SaleTimeoutMs = 4000;
    private const long PromptSettleMs = 120;

    private readonly Stopwatch clock = new();
    private readonly HashSet<SlotRef> attempted = new();
    private SlotRef? pending;
    private uint pendingKey;
    private int pendingQuantity;
    private long pendingSince;
    private long nextSaleAt;
    private long promptSeenAt = -1;
    private int sold;
    private int failed;
    private long gilAtStart;

    public bool IsRunning { get; private set; }

    public SellVenue Venue
    {
        get
        {
            if (GameAddons.IsVisible("Shop")) return SellVenue.Vendor;
            if (configuration.SellAtRetainer && (GameAddons.IsVisible("RetainerGrid0") || GameAddons.IsVisible("RetainerSellList")))
                return SellVenue.Retainer;
            return SellVenue.None;
        }
    }

    public int CountSellableWares()
    {
        int count = 0;
        PlayerBags.ForEachItem(slot =>
        {
            if (IsSellableWare(slot)) count++;
        });
        return count;
    }

    public void Toggle()
    {
        if (IsRunning) Finish("stopped");
        else Start();
    }

    public void Start()
    {
        if (IsRunning || Venue == SellVenue.None) return;
        attempted.Clear();
        pending = null;
        sold = 0;
        failed = 0;
        promptSeenAt = -1;
        gilAtStart = InventoryManager.Instance()->GetGil();
        clock.Restart();
        nextSaleAt = 0;
        IsRunning = true;
    }

    public void Update()
    {
        if (!IsRunning) return;

        var venue = Venue;
        if (venue == SellVenue.None)
        {
            Finish("the vendor window closed");
            return;
        }

        long now = clock.ElapsedMilliseconds;
        if (pending is { } slot)
        {
            if (configuration.AutoConfirmPrompts && ConfirmPrompt(now)) return;

            var item = PlayerBags.GetItem(slot);
            if (item == null || ItemRules.MarkKey(item) != pendingKey || item->Quantity != pendingQuantity)
            {
                sold++;
                pending = null;
                nextSaleAt = now + configuration.SellIntervalMs;
            }
            else if (now - pendingSince > SaleTimeoutMs)
            {
                failed++;
                pending = null;
                nextSaleAt = now + configuration.SellIntervalMs;
                if (configuration.DebugLogging) Services.Log.Debug($"Sale of {slot} timed out.");
            }
            return;
        }

        if (now < nextSaleAt) return;

        var next = FindNextWare();
        if (next is not { } target)
        {
            Finish(null);
            return;
        }

        attempted.Add(target);
        var targetItem = PlayerBags.GetItem(target);
        uint key = ItemRules.MarkKey(targetItem);
        int quantity = targetItem->Quantity;
        var owner = GameAddons.VisibleInventoryLayout();
        string text = venue == SellVenue.Vendor ? QuickSell.VendorSellText : QuickSell.RetainerSellText;
        if (QuickSell.TrySell(target, text, owner != null ? owner->Id : 0u))
        {
            pending = target;
            pendingKey = key;
            pendingQuantity = quantity;
            pendingSince = now;
            promptSeenAt = -1;
            if (configuration.DebugLogging) Services.Log.Debug($"Selling {target} (item {key} x{quantity}).");
        }
        else
        {
            failed++;
            nextSaleAt = now + configuration.SellIntervalMs;
            if (configuration.DebugLogging) Services.Log.Debug($"No \"{text}\" entry for {target}.");
        }
    }

    private SlotRef? FindNextWare()
    {
        SlotRef? found = null;
        PlayerBags.ForEachItem(slot =>
        {
            if (found == null && !attempted.Contains(slot) && IsSellableWare(slot)) found = slot;
        });
        return found;
    }

    private bool IsSellableWare(SlotRef slot)
    {
        var item = PlayerBags.GetItem(slot);
        return item != null && marks.Get(slot, item) == MarkKind.Wares && ItemRules.CanSellToVendor(item);
    }

    // Accepts a Yes/No prompt once it has had a moment to finish opening.
    private bool ConfirmPrompt(long now)
    {
        var addon = GameAddons.GetVisible("SelectYesno");
        if (addon == null || !addon->IsReady)
        {
            promptSeenAt = -1;
            return false;
        }
        if (promptSeenAt < 0) promptSeenAt = now;
        if (now - promptSeenAt < PromptSettleMs) return true;

        var yes = ((AddonSelectYesno*)addon)->YesButton;
        if (yes != null && !yes->IsEnabled) return true;

        var value = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int, Int = 0 };
        addon->FireCallback(1, &value, true);
        promptSeenAt = -1;
        pendingSince = now;
        return true;
    }

    private void Finish(string? reason)
    {
        if (!IsRunning) return;
        IsRunning = false;
        pending = null;
        if (!configuration.PrintSellSummary) return;

        long earned = InventoryManager.Instance()->GetGil() - gilAtStart;
        string summary = sold == 0 && failed == 0
            ? "Nothing marked as wares to sell."
            : $"Sold {sold} item{(sold == 1 ? "" : "s")} for {earned:N0} gil.";
        if (failed > 0) summary += $" {failed} could not be sold.";
        if (reason != null) summary += $" (Selling {reason}.)";
        Services.Chat.Print(summary, "Wares");
    }
}
