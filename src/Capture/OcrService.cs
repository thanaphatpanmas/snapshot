using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

// Calls the Windows.Media.Ocr engine by reflection: this compiler cannot reference the WinRT metadata directly.
static class OcrService
{
    // Returns the recognised text; throws InvalidOperationException when Windows has no OCR language installed.
    public static string Recognize(Bitmap image)
    {
        object engine = WinRtType("Windows.Media.Ocr.OcrEngine").GetMethod("TryCreateFromUserProfileLanguages").Invoke(null, null);
        if (engine == null) throw new InvalidOperationException(Loc.T("ocr.noEngine"));

        using (var stream = new MemoryStream())
        {
            image.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            object randomAccessStream = typeof(WindowsRuntimeStreamExtensions).GetMethod("AsRandomAccessStream").Invoke(null, new object[] { stream });

            MethodInfo createDecoder = WinRtType("Windows.Graphics.Imaging.BitmapDecoder")
                .GetMethod("CreateAsync", new[] { WinRtType("Windows.Storage.Streams.IRandomAccessStream") });
            dynamic decoder = Await(createDecoder.Invoke(null, new[] { randomAccessStream }), "Windows.Graphics.Imaging.BitmapDecoder");
            dynamic softwareBitmap = Await(decoder.GetSoftwareBitmapAsync(), "Windows.Graphics.Imaging.SoftwareBitmap");
            dynamic result = Await(((dynamic)engine).RecognizeAsync(softwareBitmap), "Windows.Media.Ocr.OcrResult");
            return result.Text;
        }
    }

    static Type WinRtType(string fullName)
    {
        Type type = Type.GetType(fullName + ", Windows, ContentType=WindowsRuntime");
        if (type == null) throw new InvalidOperationException("WinRT type not found: " + fullName);
        return type;
    }

    // IAsyncOperation<T> cannot be named at compile time, so it is bridged to Task<T> by reflection.
    static object Await(object operation, string resultTypeName)
    {
        MethodInfo asTask = null;
        foreach (MethodInfo method in typeof(WindowsRuntimeSystemExtensions).GetMethods())
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == "AsTask" && parameters.Length == 1 && parameters[0].ParameterType.Name == "IAsyncOperation`1")
                asTask = method;
        }

        object task = asTask.MakeGenericMethod(WinRtType(resultTypeName)).Invoke(null, new[] { operation });
        ((System.Threading.Tasks.Task)task).Wait();
        return task.GetType().GetProperty("Result").GetValue(task, null);
    }
}
