using System;
using System.Drawing;

interface IFrameEncoder
{
    // Frames arrive at their capture time since the recording started.
    void AddFrame(Bitmap frame, TimeSpan timestamp);

    void Finish(TimeSpan endTimestamp);
}
