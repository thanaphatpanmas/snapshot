using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Pipes raw frames to an external ffmpeg, which does the H.264 encoding; the app itself bundles no codec.
class FfmpegEncoder : IFrameEncoder
{
    const int FinishTimeoutMilliseconds = 30000;

    readonly Process process;
    readonly Stream input;
    readonly int width;
    readonly int height;
    readonly int framesPerSecond;
    long framesWritten;
    byte[] lastFrame;

    public FfmpegEncoder(string ffmpegPath, string outputPath, int width, int height, int framesPerSecond)
    {
        this.width = width;
        this.height = height;
        this.framesPerSecond = framesPerSecond;

        string arguments = string.Format(
            "-y -loglevel error -f rawvideo -pix_fmt bgra -video_size {0}x{1} -framerate {2} -i - -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p \"{3}\"",
            width, height, framesPerSecond, outputPath);
        process = Process.Start(new ProcessStartInfo(ffmpegPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        });
        process.ErrorDataReceived += delegate { }; // drained so a full stderr pipe can never stall the encoder
        process.BeginErrorReadLine();
        input = process.StandardInput.BaseStream;
    }

    public static string FindOnPath()
    {
        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string folder in pathVariable.Split(Path.PathSeparator))
        {
            if (folder.Length == 0) continue;
            string candidate = Path.Combine(folder.Trim('"'), "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // Duplicates the frame to cover any gap, so the video keeps real-time speed even when capture falls behind.
    public void AddFrame(Bitmap frame, TimeSpan timestamp)
    {
        byte[] pixels = CopyPixels(frame);
        long framesDue = (long)(timestamp.TotalSeconds * framesPerSecond) + 1;
        WriteUntil(pixels, framesDue);
        lastFrame = pixels;
    }

    public void Finish(TimeSpan endTimestamp)
    {
        if (lastFrame != null) WriteUntil(lastFrame, (long)(endTimestamp.TotalSeconds * framesPerSecond) + 1);
        input.Dispose();
        process.WaitForExit(FinishTimeoutMilliseconds);
        process.Dispose();
    }

    void WriteUntil(byte[] pixels, long frameCount)
    {
        while (framesWritten < frameCount)
        {
            input.Write(pixels, 0, pixels.Length);
            framesWritten++;
        }
    }

    byte[] CopyPixels(Bitmap frame)
    {
        BitmapData data = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[width * height * 4];
        try
        {
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally
        {
            frame.UnlockBits(data);
        }
        return pixels;
    }
}
