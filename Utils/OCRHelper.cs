using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using MFATools.ViewModels;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MFATools.Views;
using Newtonsoft.Json;

// using PaddleOCRSharp;

namespace MFATools.Utils;

public class OCRHelper
{
    public class RecognitionQuery
    {
        [JsonProperty("all")] public List<RecognitionResult>? All;
        [JsonProperty("best")] public RecognitionResult? Best;
        [JsonProperty("filtered")] public List<RecognitionResult>? Filtered;
    }

    public class RecognitionResult
    {
        [JsonProperty("box")] public List<int>? Box;
        [JsonProperty("score")] public double? Score;
        [JsonProperty("text")] public string? Text;
    }

    public static void Initialize()
    {
    }

    public static string ReadTextFromMAATasker(Bitmap bitmap,int x, int y, int width, int height)
    {
        string result = string.Empty;
        TaskItemViewModel taskItemViewModel = new TaskItemViewModel
        {
            Task = new TaskModel
            {
                Recognition = "Custom",
                CustomRecognition = "MFAOCRRecognition",
                Roi = new List<int>
                {
                    x,
                    y,
                    width,
                    height
                }
            },
            Name = "AppendOCR",
        };
        MFAOCRRecognition.Bitmap = bitmap;
        var job = MaaProcessor.Instance.GetCurrentTasker()?
            .AppendTask(taskItemViewModel.Name, taskItemViewModel.ToString());
        if (job?.Wait() == MaaJobStatus.Succeeded)
        {
            // var query =
            //     JsonConvert.DeserializeObject<RecognitionQuery>(job.QueryRecognitionDetail()?
            //             .Detail
            //         ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(MFAOCRRecognition.Output))
                result = MFAOCRRecognition.Output;
        }
        else
        {
            Growls.ErrorGlobal("识别失败！");
        }
        MFAOCRRecognition.Output = null;
        MFAOCRRecognition.Bitmap = null;
        Console.WriteLine($"识别结果: {result}");
        // TaskItemViewModel taskItemViewModel = new TaskItemViewModel
        // {
        //     Task = new TaskModel
        //     {
        //         Recognition = "OCR",
        //         Roi = new List<int>
        //         {
        //             x,
        //             y,
        //             width,
        //             height
        //         }
        //     },
        //     Name = "AppendOCR",
        // };
        // var job = MaaProcessor.Instance.GetCurrentTasker()?
        //     .AppendTask(taskItemViewModel.Name, taskItemViewModel.ToString());
        // if (job?.Wait() == MaaJobStatus.Succeeded)
        // {
        //     var query =
        //         JsonConvert.DeserializeObject<RecognitionQuery>(job.QueryRecognitionDetail()?
        //                 .Detail
        //             ?? string.Empty);
        //     if (!string.IsNullOrWhiteSpace(query?.Best?.Text))
        //         result = query.Best.Text;
        // }
        // else
        // {
        //     Growls.ErrorGlobal("识别失败！");
        // }
        //
        // Console.WriteLine($"识别结果: {result}");
        return result;
    }

    public static string ReadTextFromMAAContext(in IMaaContext context,
        IMaaImageBuffer image,
        int x,
        int y,
        int width,
        int height)
    {
        var result = string.Empty;
        var taskItemViewModel = new TaskItemViewModel
        {
            Task = new TaskModel
            {
                Recognition = "OCR",
                Roi = new List<int>
                {
                    x,
                    y,
                    width,
                    height
                }
            },
            Name = "AppendOCR",
        };
        var detail =
            context.RunRecognition(taskItemViewModel.Name, image, taskItemViewModel.ToString());

        if (detail != null)
        {
            var query = JsonConvert.DeserializeObject<RecognitionQuery>(detail.Detail);
            if (!string.IsNullOrWhiteSpace(query?.Best?.Text))
                result = query.Best.Text;
        }
        else
        {
            Growls.ErrorGlobal("识别失败！");
        }

        Console.WriteLine($"识别结果: {result}");

        return result;
    }
}
