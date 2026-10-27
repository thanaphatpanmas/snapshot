using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

static class OutputFiles
{
    const string RecordingTemplate = "Screen Recording {date} at {time}";

    public static string SaveScreenshot(Bitmap image, AppSettings settings)
    {
        string path = NextPath(settings, settings.FileNameTemplate, ExtensionOf(settings.Format));
        SaveBitmap(image, path, settings);
        return path;
    }

    public static string NextRecordingPath(AppSettings settings, string extension)
    {
        return NextPath(settings, RecordingTemplate, extension);
    }

    public static string BuildFileName(string template, DateTime now)
    {
        string name = template
            .Replace("{date}", now.ToString("yyyy-MM-dd"))
            .Replace("{time}", now.ToString("HH.mm.ss"));
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        name = new string(name.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return name.Length == 0 ? "Screenshot" : name;
    }

    public static string ExtensionOf(ImageFileFormat format)
    {
        switch (format)
        {
            case ImageFileFormat.Jpg: return ".jpg";
            case ImageFileFormat.Bmp: return ".bmp";
            default: return ".png";
        }
    }

    // The extension decides the encoder, so the markup editor can overwrite a file in its original format.
    public static void SaveBitmap(Bitmap image, string path, AppSettings settings)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                SaveJpeg(image, path, settings.JpegQuality);
                break;
            case ".bmp":
                image.Save(path, ImageFormat.Bmp);
                break;
            default:
                image.Save(path, ImageFormat.Png);
                break;
        }
    }

    static string NextPath(AppSettings settings, string template, string extension)
    {
        Directory.CreateDirectory(settings.SaveFolder);
        string baseName = BuildFileName(template, DateTime.Now);
        string path = Path.Combine(settings.SaveFolder, baseName + extension);
        for (int duplicate = 2; File.Exists(path); duplicate++)
            path = Path.Combine(settings.SaveFolder, baseName + " (" + duplicate + ")" + extension);
        return path;
    }

    static void SaveJpeg(Bitmap image, string path, int quality)
    {
        ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        using (var parameters = new EncoderParameters(1))
        {
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            image.Save(path, jpegCodec, parameters);
        }
    }
}
