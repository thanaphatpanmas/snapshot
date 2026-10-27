using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Streams frames straight to disk with a fixed 252-colour palette, so memory stays flat however long the recording is.
// Only the changed rectangle of each frame is written, and unchanged frames just extend the previous frame's delay.
class GifEncoder : IFrameEncoder
{
    const int RedLevels = 6;
    const int GreenLevels = 7;
    const int BlueLevels = 6;
    const int MinimumDelayCentiseconds = 2;
    const int ClearCode = 256;
    const int EndOfInformationCode = 257;
    const int FirstFreeCode = 258;
    const int MaxCodeSize = 12;
    const int CodeTableSize = 1 << MaxCodeSize;

    static readonly byte[] RedIndexByValue = BuildIndexTable(RedLevels);
    static readonly byte[] GreenIndexByValue = BuildIndexTable(GreenLevels);
    static readonly byte[] BlueIndexByValue = BuildIndexTable(BlueLevels);

    readonly Stream output;
    readonly int width;
    readonly int height;
    byte[] previousIndices;
    byte[] pendingPixels;
    Rectangle pendingBounds;
    TimeSpan pendingStart;

    public GifEncoder(Stream output, int width, int height)
    {
        this.output = output;
        this.width = width;
        this.height = height;
        WriteHeader();
    }

    public void AddFrame(Bitmap frame, TimeSpan timestamp)
    {
        byte[] indices = Quantize(frame);
        if (previousIndices == null)
        {
            SetPending(indices, new Rectangle(0, 0, width, height), timestamp);
            return;
        }

        Rectangle changed = FindChangedBounds(previousIndices, indices);
        if (changed.IsEmpty) return;

        WritePendingFrame(timestamp - pendingStart);
        SetPending(indices, changed, timestamp);
    }

    public void Finish(TimeSpan endTimestamp)
    {
        if (pendingPixels != null) WritePendingFrame(endTimestamp - pendingStart);
        output.WriteByte(0x3B);
        output.Dispose();
    }

    void SetPending(byte[] indices, Rectangle bounds, TimeSpan timestamp)
    {
        previousIndices = indices;
        pendingBounds = bounds;
        pendingPixels = ExtractRectangle(indices, bounds);
        pendingStart = timestamp;
    }

    void WriteHeader()
    {
        WriteAscii("GIF89a");
        WriteShort(width);
        WriteShort(height);
        output.WriteByte(0xF7); // global colour table present, 256 entries
        output.WriteByte(0);
        output.WriteByte(0);
        WritePalette();
        output.Write(new byte[] { 0x21, 0xFF, 0x0B }, 0, 3);
        WriteAscii("NETSCAPE2.0");
        output.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 }, 0, 5); // loop forever
    }

    void WritePalette()
    {
        var palette = new byte[256 * 3];
        for (int red = 0; red < RedLevels; red++)
        for (int green = 0; green < GreenLevels; green++)
        for (int blue = 0; blue < BlueLevels; blue++)
        {
            int entry = (red * GreenLevels * BlueLevels + green * BlueLevels + blue) * 3;
            palette[entry] = (byte)(red * 255 / (RedLevels - 1));
            palette[entry + 1] = (byte)(green * 255 / (GreenLevels - 1));
            palette[entry + 2] = (byte)(blue * 255 / (BlueLevels - 1));
        }
        output.Write(palette, 0, palette.Length);
    }

    void WritePendingFrame(TimeSpan duration)
    {
        int delay = Math.Max(MinimumDelayCentiseconds, Math.Min(65535, (int)Math.Round(duration.TotalMilliseconds / 10)));
        output.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 }, 0, 4); // keep previous pixels outside this frame's rectangle
        WriteShort(delay);
        output.Write(new byte[] { 0x00, 0x00 }, 0, 2);

        output.WriteByte(0x2C);
        WriteShort(pendingBounds.X);
        WriteShort(pendingBounds.Y);
        WriteShort(pendingBounds.Width);
        WriteShort(pendingBounds.Height);
        output.WriteByte(0x00);

        output.WriteByte(8);
        WriteCompressed(pendingPixels);
        output.WriteByte(0);
    }

    // Standard GIF LZW; the code size grows when the next free code reaches 2^size and the table resets at 4096.
    void WriteCompressed(byte[] pixels)
    {
        var codes = new CodeWriter(output);
        var table = new Dictionary<int, int>();
        int nextCode = FirstFreeCode;
        int codeSize = 9;

        codes.Write(ClearCode, codeSize);
        int prefix = pixels[0];
        for (int index = 1; index < pixels.Length; index++)
        {
            int key = (prefix << 8) | pixels[index];
            int existingCode;
            if (table.TryGetValue(key, out existingCode))
            {
                prefix = existingCode;
                continue;
            }

            codes.Write(prefix, codeSize);
            if (nextCode == (1 << codeSize) && codeSize < MaxCodeSize) codeSize++;
            if (nextCode < CodeTableSize)
            {
                table[key] = nextCode++;
            }
            else
            {
                codes.Write(ClearCode, codeSize);
                table.Clear();
                nextCode = FirstFreeCode;
                codeSize = 9;
            }
            prefix = pixels[index];
        }

        codes.Write(prefix, codeSize);
        if (nextCode == (1 << codeSize) && codeSize < MaxCodeSize) codeSize++;
        codes.Write(EndOfInformationCode, codeSize);
        codes.Flush();
    }

    byte[] Quantize(Bitmap frame)
    {
        BitmapData data = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bgra = new byte[width * height * 4];
        try
        {
            Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
        }
        finally
        {
            frame.UnlockBits(data);
        }

        var indices = new byte[width * height];
        for (int pixel = 0; pixel < indices.Length; pixel++)
        {
            int offset = pixel * 4;
            indices[pixel] = (byte)(RedIndexByValue[bgra[offset + 2]] * GreenLevels * BlueLevels
                + GreenIndexByValue[bgra[offset + 1]] * BlueLevels
                + BlueIndexByValue[bgra[offset]]);
        }
        return indices;
    }

    Rectangle FindChangedBounds(byte[] previous, byte[] current)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                if (previous[rowStart + x] == current[rowStart + x]) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                maxY = y;
            }
        }
        return maxX < 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    byte[] ExtractRectangle(byte[] indices, Rectangle bounds)
    {
        var pixels = new byte[bounds.Width * bounds.Height];
        for (int row = 0; row < bounds.Height; row++)
            Buffer.BlockCopy(indices, (bounds.Y + row) * width + bounds.X, pixels, row * bounds.Width, bounds.Width);
        return pixels;
    }

    static byte[] BuildIndexTable(int levels)
    {
        var table = new byte[256];
        for (int value = 0; value < 256; value++)
            table[value] = (byte)((value * (levels - 1) + 127) / 255);
        return table;
    }

    void WriteShort(int value)
    {
        output.WriteByte((byte)(value & 0xFF));
        output.WriteByte((byte)((value >> 8) & 0xFF));
    }

    void WriteAscii(string text)
    {
        foreach (char character in text)
            output.WriteByte((byte)character);
    }

    // Packs variable-width codes LSB first into the 255-byte sub-blocks GIF requires.
    class CodeWriter
    {
        const int MaxBlockLength = 255;

        readonly Stream output;
        readonly byte[] block = new byte[MaxBlockLength];
        int blockLength;
        uint bitBuffer;
        int bitCount;

        public CodeWriter(Stream output)
        {
            this.output = output;
        }

        public void Write(int code, int codeSize)
        {
            bitBuffer |= (uint)code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                AddByte((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        public void Flush()
        {
            if (bitCount > 0) AddByte((byte)(bitBuffer & 0xFF));
            bitBuffer = 0;
            bitCount = 0;
            FlushBlock();
        }

        void AddByte(byte value)
        {
            block[blockLength++] = value;
            if (blockLength == MaxBlockLength) FlushBlock();
        }

        void FlushBlock()
        {
            if (blockLength == 0) return;
            output.WriteByte((byte)blockLength);
            output.Write(block, 0, blockLength);
            blockLength = 0;
        }
    }
}
