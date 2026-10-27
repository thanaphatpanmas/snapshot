using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

// Runs one capture end to end: optional delay → capture → save → clipboard/sound → thumbnail.
class CaptureController
{
    const int MinimumRecordingSize = 16;
    const int NotificationPreviewLength = 120;

    readonly ThumbnailStack thumbnails = new ThumbnailStack();
    readonly ScreenRecorder recorder = new ScreenRecorder();
    readonly Action<string, string> notify;
    Rectangle? lastRegion;
    RecordingFrameForm recordingFrame;
    bool isBusy;

    public AppSettings Settings { get; set; }

    public bool IsRecording { get { return recorder.IsRecording; } }

    public event Action<bool> RecordingStateChanged;

    public CaptureController(AppSettings settings, Action<string, string> notify)
    {
        Settings = settings;
        this.notify = notify;
        recorder.Finished += OnRecordingFinished;
    }

    // While recording, the only accepted action is the one that stops it.
    public void Capture(CaptureAction action)
    {
        if (recorder.IsRecording)
        {
            if (action == CaptureAction.Record) recorder.Stop();
            return;
        }
        if (isBusy) return;

        isBusy = true;
        try
        {
            Dispatch(action);
        }
        catch (Exception error)
        {
            notify(Loc.T("notify.error"), error.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    public void Shutdown()
    {
        recorder.StopAndWait();
        CloseRecordingFrame();
        thumbnails.CloseAll();
    }

    void Dispatch(CaptureAction action)
    {
        switch (action)
        {
            case CaptureAction.Region:
                CaptureSelection(SelectionMode.Region, SaveSelectionAsScreenshot);
                break;
            case CaptureAction.Window:
                CaptureSelection(SelectionMode.Window, SaveSelectionAsScreenshot);
                break;
            case CaptureAction.FullScreen:
                CaptureScreenUnderCursor();
                break;
            case CaptureAction.RepeatLastRegion:
                RepeatLastRegion();
                break;
            case CaptureAction.Ocr:
                CaptureSelection(SelectionMode.Region, RecognizeText);
                break;
            case CaptureAction.Record:
                CaptureSelection(SelectionMode.Region, StartRecording);
                break;
        }
    }

    void CaptureSelection(SelectionMode mode, Action<CapturedSelection> handle)
    {
        using (CapturedSelection selection = SelectArea(mode))
        {
            if (selection != null) handle(selection);
        }
    }

    void CaptureScreenUnderCursor()
    {
        WaitForTimerDelay();
        using (Bitmap image = ScreenCapture.CopyArea(Screen.FromPoint(Cursor.Position).Bounds, Settings.IncludeCursor))
            FinishScreenshot(image);
    }

    void RepeatLastRegion()
    {
        if (!lastRegion.HasValue)
        {
            CaptureSelection(SelectionMode.Region, SaveSelectionAsScreenshot);
            return;
        }
        WaitForTimerDelay();
        using (Bitmap image = ScreenCapture.CopyArea(lastRegion.Value, Settings.IncludeCursor))
            FinishScreenshot(image);
    }

    void WaitForTimerDelay()
    {
        if (Settings.DelaySeconds > 0) CountdownForm.Run(Settings.DelaySeconds);
    }

    // Freezes the screen first so the overlay and any pop-ups that vanish on focus loss can't end up in the picture.
    CapturedSelection SelectArea(SelectionMode mode)
    {
        WaitForTimerDelay();
        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        List<Rectangle> windows = WindowFinder.GetVisibleWindowBounds();

        using (Bitmap screenshot = ScreenCapture.CopyArea(virtualScreen, Settings.IncludeCursor))
        using (var selector = new RegionSelectorForm(screenshot, virtualScreen, windows, mode, Settings.ShowMagnifier))
        {
            if (selector.ShowDialog() != DialogResult.OK) return null;
            Rectangle region = Rectangle.Intersect(selector.SelectedRegion, new Rectangle(Point.Empty, screenshot.Size));
            var screenBounds = new Rectangle(region.X + virtualScreen.X, region.Y + virtualScreen.Y, region.Width, region.Height);
            return new CapturedSelection(screenshot.Clone(region, screenshot.PixelFormat), screenBounds);
        }
    }

    void SaveSelectionAsScreenshot(CapturedSelection selection)
    {
        lastRegion = selection.ScreenBounds;
        FinishScreenshot(selection.Image);
    }

    void FinishScreenshot(Bitmap image)
    {
        string path = OutputFiles.SaveScreenshot(image, Settings);
        if (Settings.CopyToClipboard) ClipboardHelper.SetImage(image);
        if (Settings.PlayShutterSound) ShutterSound.Play();
        thumbnails.Add(new ThumbnailForm(path, image, false, Settings));
    }

    void RecognizeText(CapturedSelection selection)
    {
        Bitmap image = (Bitmap)selection.Image.Clone();
        Task.Factory.StartNew(() => OcrService.Recognize(image))
            .ContinueWith(task =>
            {
                image.Dispose();
                ShowOcrResult(task);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    void ShowOcrResult(Task<string> task)
    {
        if (task.IsFaulted)
        {
            notify(Loc.T("notify.error"), task.Exception.GetBaseException().Message);
            return;
        }

        string text = task.Result;
        if (string.IsNullOrWhiteSpace(text))
        {
            notify(AppInfo.Name, Loc.T("ocr.nothing"));
            return;
        }
        ClipboardHelper.SetText(text);
        string preview = text.Length > NotificationPreviewLength ? text.Substring(0, NotificationPreviewLength) + "…" : text;
        notify(Loc.T("ocr.copied"), preview);
    }

    void StartRecording(CapturedSelection selection)
    {
        // Video encoders need even dimensions.
        Rectangle region = new Rectangle(selection.ScreenBounds.X, selection.ScreenBounds.Y, selection.ScreenBounds.Width & ~1, selection.ScreenBounds.Height & ~1);
        if (region.Width < MinimumRecordingSize || region.Height < MinimumRecordingSize) return;

        recorder.Start(region, Settings);
        recordingFrame = new RecordingFrameForm(region);
        recordingFrame.Show();
        RaiseRecordingStateChanged(true);
    }

    void OnRecordingFinished(RecordingResult result)
    {
        CloseRecordingFrame();
        RaiseRecordingStateChanged(false);

        using (result.FirstFrame)
        {
            if (result.ErrorMessage != null || result.FirstFrame == null)
            {
                notify(Loc.T("recording.failed"), result.ErrorMessage ?? string.Empty);
                return;
            }
            thumbnails.Add(new ThumbnailForm(result.FilePath, result.FirstFrame, true, Settings));
        }
    }

    void CloseRecordingFrame()
    {
        if (recordingFrame == null) return;
        recordingFrame.Close();
        recordingFrame = null;
    }

    void RaiseRecordingStateChanged(bool isRecording)
    {
        Action<bool> handler = RecordingStateChanged;
        if (handler != null) handler(isRecording);
    }

    class CapturedSelection : IDisposable
    {
        public readonly Bitmap Image;
        public readonly Rectangle ScreenBounds;

        public CapturedSelection(Bitmap image, Rectangle screenBounds)
        {
            Image = image;
            ScreenBounds = screenBounds;
        }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
