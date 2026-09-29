using System;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;

using Wares.Base;
using Wares.Game;
using Wares.UI;
using Wares.UI.Windows;

namespace Wares;

public sealed class Plugin : IDalamudPlugin, IDisposable
{
    private const string CommandName = "/wares";

    private readonly Configuration configuration;
    private readonly MarkStore marks;
    private readonly SlotSelection selection = new();
    private readonly SellController seller;
    private readonly ItemContextMenu contextMenu;
    private readonly WindowSystem windowSystem = new("Wares");
    private readonly SettingsWindow settingsWindow;
    private readonly Task nativeUiInitialization;
    private SelectionInput? selectionInput;
    private InventoryOverlay? overlay;
    private SellButton? sellButton;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configuration.Migrate();
        configuration.Save();

        marks = new MarkStore(configuration);
        seller = new SellController(configuration, marks);
        contextMenu = new ItemContextMenu(marks, selection);

        settingsWindow = new SettingsWindow(configuration, marks.Counts, marks.ClearCharacter);
        windowSystem.AddWindow(settingsWindow);

        nativeUiInitialization = KamiToolKitLibrary.InitializeAsync(Services.PluginInterface, "Wares")
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Services.Log.Error(task.Exception!, "KamiToolKit failed to initialise; native badges and the sell button are unavailable.");
                    return Task.CompletedTask;
                }
                return Services.Framework.RunOnFrameworkThread(EnableNativeUi);
            }).Unwrap();

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Wares settings.\n/wares sell → Sell every marked ware (while at a vendor).",
        });
        Services.Framework.Update += OnFrameworkUpdate;
        Services.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Services.PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        Services.PluginInterface.UiBuilder.OpenMainUi += OpenSettings;
    }

    public void Dispose()
    {
        Services.Framework.Update -= OnFrameworkUpdate;
        Services.CommandManager.RemoveHandler(CommandName);
        Services.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        Services.PluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        windowSystem.RemoveAllWindows();
        contextMenu.Dispose();
        marks.Dispose();
        selectionInput?.Dispose();
        if (nativeUiInitialization.IsCompletedSuccessfully)
        {
            sellButton?.Dispose();
            overlay?.Dispose();
            KamiToolKitLibrary.Dispose();
        }
    }

    private void EnableNativeUi()
    {
        selectionInput = new SelectionInput(configuration, selection, contextMenu.OpenForRightClick);
        overlay = new InventoryOverlay(configuration, marks, selection);
        sellButton = new SellButton(configuration, seller);
    }

    private void OpenSettings() => settingsWindow.IsOpen = true;

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("sell", StringComparison.OrdinalIgnoreCase))
        {
            if (seller.Venue == SellVenue.None) Services.Chat.PrintError("Talk to a vendor first to sell your wares.", "Wares");
            else seller.Start();
            return;
        }
        OpenSettings();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        selectionInput?.OnFrameworkUpdate();
        overlay?.OnFrameworkUpdate();
        contextMenu.OnFrameworkUpdate();
        marks.OnFrameworkUpdate();
        seller.Update();
    }
}
