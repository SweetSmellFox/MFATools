using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;
using MFATools.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MFATools.Utils;

public class MFAOCRRecognition : IMaaCustomRecognition
{
    public string Name { get; set; } = "MFAOCRRecognition";
    public static Bitmap? Bitmap { get; set; } = null;
    public static string? Output { get; set; } = null;
    public bool Analyze(in IMaaContext context, in AnalyzeArgs args, in AnalyzeResults results)
    {
        var taskItemViewModel = new TaskItemViewModel
        {
            Task = new TaskModel
            {
                Recognition = "OCR",
                Roi = new List<int>
                {
                    args.Roi.X,
                    args.Roi.Y,
                    args.Roi.Width,
                    args.Roi.Height
                }
            },
            Name = "AppendOCR",
        };
        var image = new MaaImageBuffer();
        image.TrySetEncodedData(BitmapToBytes(Bitmap));

        var detail =
            context.RunRecognition(taskItemViewModel.Name, image, taskItemViewModel.ToString());
        if (detail != null)
        {
            var query = JsonConvert.DeserializeObject<OCRHelper.RecognitionQuery>(detail.Detail);
            if (!string.IsNullOrWhiteSpace(query?.Best?.Text))
                Output = query.Best.Text;
        }
        else
        {
            Growls.ErrorGlobal("识别失败！");
        }
        return true;
    }

    public static byte[] BitmapToBytes(Bitmap? bitmap)
    {
        if (bitmap == null)
            return [];

        MemoryStream? ms = null;
        try
        {
            ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            byte[] byteImage = new Byte[ms.Length];
            byteImage = ms.ToArray();
            return byteImage;
        }
        catch (ArgumentNullException ex)
        {
            LoggerService.LogError(ex);
            return [];
        }
        finally
        {
            ms?.Close();
        }
    }
}
