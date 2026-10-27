using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

class RecordingResult
{
    public string FilePath;
    public Bitmap FirstFrame;
    public string ErrorMessage;
}

// Captures a screen region on a worker thread and streams each frame to the chosen encoder.
class ScreenRecorder
{
    readonly object stateLock = new object();
    Thread worker;
    bool stopRequested;

    // Raised on the UI thread that called Start; the receiver owns FirstFrame.
    public event Action<RecordingResult> Finished;

    public bool IsRecording
    {
        get
        {
            lock (stateLock) return worker != null;
        }
    }

    // Falls back to GIF when MP4 is chosen but ffmpeg is not installed.
    public static RecordingFormat EffectiveFormat(AppSettings settings)
    {
        bool canUseMp4 = settings.RecordFormat == RecordingFormat.Mp4 && FfmpegEncoder.FindOnPath() != null;
        return canUseMp4 ? RecordingFormat.Mp4 : RecordingFormat.Gif;
    }

    public void Start(Rectangle region, AppSettings settings)
    {
        string extension = EffectiveFormat(settings) == RecordingFormat.Mp4 ? ".mp4" : ".gif";
        string path = OutputFiles.NextRecordingPath(settings, extension);
        IFrameEncoder encoder = CreateEncoder(path, region, settings);
        SynchronizationContext uiContext = SynchronizationContext.Current;

        lock (stateLock)
        {
            stopRequested = false;
            worker = new Thread(() => RecordLoop(region, settings, path, encoder, uiContext)) { IsBackground = true };
            worker.Start();
        }
    }

    public void Stop()
    {
        lock (stateLock) stopRequested = true;
    }

    public void StopAndWait()
    {
        Thread running;
        lock (stateLock)
        {
            stopRequested = true;
            running = worker;
        }
        if (running != null) running.Join(5000);
    }

    static IFrameEncoder CreateEncoder(string path, Rectangle region, AppSettings settings)
    {
        if (EffectiveFormat(settings) == RecordingFormat.Mp4)
            return new FfmpegEncoder(FfmpegEncoder.FindOnPath(), path, region.Width, region.Height, settings.RecordFramesPerSecond);
        return new GifEncoder(File.Create(path), region.Width, region.Height);
    }

    void RecordLoop(Rectangle region, AppSettings settings, string path, IFrameEncoder encoder, SynchronizationContext uiContext)
    {
        var result = new RecordingResult { FilePath = path };
        try
        {
            Capture(region, settings, encoder, result);
        }
        catch (Exception error)
        {
            result.ErrorMessage = error.Message;
        }

        lock (stateLock) worker = null;
        Action<RecordingResult> handler = Finished;
        if (handler != null) uiContext.Post(delegate { handler(result); }, null);
    }

    void Capture(Rectangle region, AppSettings settings, IFrameEncoder encoder, RecordingResult result)
    {
        TimeSpan frameInterval = TimeSpan.FromSeconds(1.0 / settings.RecordFramesPerSecond);
        TimeSpan maxDuration = TimeSpan.FromSeconds(settings.RecordMaxSeconds);
        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan nextFrameAt = TimeSpan.Zero;

        while (!IsStopRequested() && clock.Elapsed < maxDuration)
        {
            TimeSpan wait = nextFrameAt - clock.Elapsed;
            if (wait > TimeSpan.Zero) Thread.Sleep(wait);

            using (Bitmap frame = ScreenCapture.CopyArea(region, settings.IncludeCursor))
            {
                if (result.FirstFrame == null) result.FirstFrame = (Bitmap)frame.Clone();
                encoder.AddFrame(frame, clock.Elapsed);
            }
            nextFrameAt += frameInterval;
            if (nextFrameAt < clock.Elapsed) nextFrameAt = clock.Elapsed; // skip missed frames instead of bursting to catch up
        }
        encoder.Finish(clock.Elapsed);
    }

    bool IsStopRequested()
    {
        lock (stateLock) return stopRequested;
    }
}
