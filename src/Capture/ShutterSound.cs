using System;
using System.IO;
using System.Media;

// Synthesised so no audio file has to ship next to the exe.
static class ShutterSound
{
    const int SampleRate = 22050;
    const double DurationSeconds = 0.14;

    static byte[] wavBytes;
    static SoundPlayer player;

    public static void Play()
    {
        if (wavBytes == null) wavBytes = BuildWav();
        player = new SoundPlayer(new MemoryStream(wavBytes));
        player.Play();
    }

    static byte[] BuildWav()
    {
        int sampleCount = (int)(SampleRate * DurationSeconds);
        var noise = new Random(1);
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + sampleCount * 2);
        writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(sampleCount * 2);

        for (int index = 0; index < sampleCount; index++)
        {
            double seconds = index / (double)SampleRate;
            double sample = Click(seconds, noise) + Click(seconds - 0.06, noise);
            writer.Write((short)(Math.Max(-1, Math.Min(1, sample)) * short.MaxValue * 0.6));
        }
        return stream.ToArray();
    }

    // A short noise burst over a decaying tone sounds like a mechanical shutter blade.
    static double Click(double secondsSinceStart, Random noise)
    {
        if (secondsSinceStart < 0) return 0;
        double noisePart = (noise.NextDouble() * 2 - 1) * Math.Exp(-secondsSinceStart * 140) * 0.7;
        double tonePart = Math.Sin(2 * Math.PI * 1800 * secondsSinceStart) * Math.Exp(-secondsSinceStart * 90) * 0.4;
        return noisePart + tonePart;
    }
}
