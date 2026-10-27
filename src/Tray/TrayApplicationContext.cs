using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// Owns everything that lives in the notification area: icon, menu, global hotkeys and the settings dialog.
class TrayApplicationContext : ApplicationContext
{
    const int MenuClosingDelayMilliseconds = 250;
    const int NotificationMilliseconds = 5000;
    static readonly int[] TimerChoices = { 0, 3, 5, 10 };

    readonly HotkeyWindow hotkeys = new HotkeyWindow();
    readonly NotifyIcon trayIcon = new NotifyIcon();
    readonly Icon idleIcon = TrayIcons.CreateIdle();
    readonly Icon recordingIcon = TrayIcons.CreateRecording();
    readonly CaptureController controller;
    AppSettings settings;
    ToolStripMenuItem recordItem;
    bool isSettingsOpen;

    public TrayApplicationContext()
    {
        settings = SettingsStore.Load();
        Loc.Language = settings.Language;
        controller = new CaptureController(settings, ShowNotification);
        controller.RecordingStateChanged += UpdateRecordingState;

        trayIcon.Icon = idleIcon;
        trayIcon.Text = AppInfo.Name;
        RebuildMenu();
        trayIcon.Visible = true;
        RegisterHotkeys();
    }

    protected override void ExitThreadCore()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        hotkeys.Dispose();
        controller.Shutdown();
        idleIcon.Dispose();
        recordingIcon.Dispose();
        base.ExitThreadCore();
    }

    void RegisterHotkeys()
    {
        var conflicts = new List<string>();
        foreach (CaptureAction action in AppSettings.AllActions)
        {
            CaptureAction capturedAction = action;
            Hotkey hotkey = settings.GetHotkey(action);
            if (!hotkeys.Register(hotkey, delegate { controller.Capture(capturedAction); }))
                conflicts.Add(hotkey.ToString());
        }
        if (conflicts.Count > 0)
            ShowNotification(AppInfo.Name, string.Format(Loc.T("notify.hotkeyConflict"), string.Join(", ", conflicts.ToArray())));
    }

    void ShowNotification(string title, string message)
    {
        trayIcon.ShowBalloonTip(NotificationMilliseconds, title, message, ToolTipIcon.Info);
    }

    void RebuildMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (CaptureAction action in AppSettings.AllActions)
            menu.Items.Add(CreateActionItem(action));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateTimerMenu());
        menu.Items.Add(Loc.T("menu.openFolder"), null, delegate { OpenSaveFolder(); });
        menu.Items.Add(Loc.T("menu.settings"), null, delegate { OpenSettings(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("menu.exit"), null, delegate { ExitThread(); });

        ContextMenuStrip previous = trayIcon.ContextMenuStrip;
        trayIcon.ContextMenuStrip = menu;
        if (previous != null) previous.Dispose();
        UpdateRecordingState(controller.IsRecording);
    }

    // A menu must not be disposed from inside its own click handler, so rebuilds triggered by a click wait a moment.
    void RebuildMenuLater()
    {
        SynchronizationContext.Current.Post(delegate { RebuildMenu(); }, null);
    }

    ToolStripMenuItem CreateActionItem(CaptureAction action)
    {
        var item = new ToolStripMenuItem(Loc.T("action." + action)) { ShortcutKeyDisplayString = settings.GetHotkey(action).ToString() };
        item.Click += delegate { CaptureAfterMenuCloses(action); };
        if (action == CaptureAction.Record) recordItem = item;
        return item;
    }

    ToolStripMenuItem CreateTimerMenu()
    {
        var timerMenu = new ToolStripMenuItem(Loc.T("menu.timer"));
        foreach (int seconds in TimerChoices)
        {
            int chosenSeconds = seconds;
            string text = seconds == 0 ? Loc.T("menu.timer.off") : string.Format(Loc.T("menu.timer.seconds"), seconds);
            var item = new ToolStripMenuItem(text) { Checked = settings.DelaySeconds == seconds };
            item.Click += delegate { SetTimerDelay(chosenSeconds); };
            timerMenu.DropDownItems.Add(item);
        }
        return timerMenu;
    }

    // Waits so the closing menu is gone before the screen is frozen.
    void CaptureAfterMenuCloses(CaptureAction action)
    {
        var delay = new System.Windows.Forms.Timer { Interval = MenuClosingDelayMilliseconds };
        delay.Tick += delegate
        {
            delay.Stop();
            delay.Dispose();
            if (!isSettingsOpen) controller.Capture(action);
        };
        delay.Start();
    }

    void SetTimerDelay(int seconds)
    {
        settings.DelaySeconds = seconds;
        SaveSettings();
        RebuildMenuLater();
    }

    void OpenSaveFolder()
    {
        Directory.CreateDirectory(settings.SaveFolder);
        Process.Start(new ProcessStartInfo(settings.SaveFolder) { UseShellExecute = true });
    }

    // Global hotkeys are released while the dialog is open, otherwise they would fire instead of being typed into the hotkey boxes.
    void OpenSettings()
    {
        if (isSettingsOpen) return;
        isSettingsOpen = true;
        hotkeys.UnregisterAll();
        try
        {
            using (var form = new SettingsForm(settings))
            {
                if (form.ShowDialog() == DialogResult.OK) ApplySettings(form);
            }
        }
        finally
        {
            isSettingsOpen = false;
            RegisterHotkeys();
        }
    }

    void ApplySettings(SettingsForm form)
    {
        settings = form.Result;
        controller.Settings = settings;
        Loc.Language = settings.Language;
        SaveSettings();
        StartupRegistration.SetEnabled(form.RunAtStartup);
        RebuildMenuLater();
    }

    void SaveSettings()
    {
        try
        {
            SettingsStore.Save(settings);
        }
        catch (IOException error)
        {
            ShowNotification(Loc.T("notify.error"), error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            ShowNotification(Loc.T("notify.error"), error.Message);
        }
    }

    void UpdateRecordingState(bool isRecording)
    {
        trayIcon.Icon = isRecording ? recordingIcon : idleIcon;
        trayIcon.Text = isRecording ? string.Format(Loc.T("tray.recording"), settings.GetHotkey(CaptureAction.Record)) : AppInfo.Name;
        if (recordItem != null)
            recordItem.Text = Loc.T(isRecording ? "action.StopRecording" : "action.Record");
    }
}
