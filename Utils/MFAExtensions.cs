using System.Collections.ObjectModel;
using System.Windows;
using HandyControl.Controls;
using MFATools.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Extensions;

namespace MFATools.Utils;

public static class MFAExtensions
{
    public static int _horizontalExpansion = DataSet.GetData("HorizontalExpansion", 100);
    public static int _verticalExpansion = DataSet.GetData("VerticalExpansion", 100);
    public static int HorizontalExpansion 
    {
        get => _horizontalExpansion;
        set
        {
            _horizontalExpansion = value;
            DataSet.SetData("HorizontalExpansion", value);
        }
    }
    public static int VerticalExpansion 
    {
        get => _verticalExpansion;
        set
        {
            _verticalExpansion = value;
            DataSet.SetData("VerticalExpansion", value);
        }
    }
    
    public static BitmapImage? BitmapToBitmapImage(Bitmap? bitmap)
    {
        if (bitmap == null)
            return null;
        try
        {
            BitmapImage bitmapImage = new BitmapImage();
            using MemoryStream ms = new MemoryStream();

            bitmap.Save(ms, ImageFormat.Png);
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ms;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
        catch (Exception e)
        {
            LoggerService.LogError(e);
            return null;
        }
    }
    public static void UpdateWriteableBitmap(this Bitmap? srcBitmap, WriteableBitmap? destWb)
    {
        if (srcBitmap == null || destWb == null) return;

        // 锁定 GDI+ Bitmap 的像素（非托管内存）
        var rect = new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height);
        var bmpData = srcBitmap.LockBits(
            rect,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb); // 强制 32 位 ARGB 格式，与 WriteableBitmap 兼容

        try
        {
            // 锁定 WriteableBitmap 的缓冲区（托管内存）
            destWb.Lock();

            // 复制像素数据（非托管 → 托管）
            int byteCount = bmpData.Stride * srcBitmap.Height;
            byte[] buffer = new byte[byteCount];
            Marshal.Copy(bmpData.Scan0, buffer, 0, byteCount); // 非托管 → 字节数组
            Marshal.Copy(buffer, 0, destWb.BackBuffer, byteCount); // 字节数组 → WriteableBitmap 缓冲区

            // 通知 UI 更新区域（全图更新，可改为仅更新矩形区域进一步优化）
            destWb.AddDirtyRect(new Int32Rect(0, 0, srcBitmap.Width, srcBitmap.Height));
        }
        finally
        {
            // 解锁资源（必须执行）
            srcBitmap.UnlockBits(bmpData);
            destWb.Unlock();
        }
    }
    
    public static Dictionary<TKey, TaskModel> MergeTaskModels<TKey>(
        this IEnumerable<KeyValuePair<TKey, TaskModel>>? taskModels,
        IEnumerable<KeyValuePair<TKey, TaskModel>>? additionalModels) where TKey : notnull
    {
        if (additionalModels == null)
            return taskModels?.ToDictionary() ?? new Dictionary<TKey, TaskModel>();
        return taskModels?
            .Concat(additionalModels)
            .GroupBy(pair => pair.Key)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var mergedModel = group.First().Value;
                    foreach (var taskModel in group.Skip(1))
                    {
                        mergedModel.Merge(taskModel.Value);
                    }

                    return mergedModel;
                }
            ) ?? new Dictionary<TKey, TaskModel>();
    }

    public static void BindLocalization(this FrameworkElement control, string resourceKey,
        DependencyProperty? property = null)
    {
        property ??= InfoElement.TitleProperty;
        var locExtension = new LocExtension(resourceKey);
        locExtension.SetBinding(control, property);
    }

    public static int ToNumber(this string? key,int defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(key))
            return defaultValue;
        return int.TryParse(key, out var result) ? result : defaultValue;
    }

    public static string GetLocalizationString(this string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;
        return LocalizeDictionary.Instance.GetLocalizedObject(key, null, null) as string ?? string.Empty;
    }

    public static string GetLocalizedFormattedString(this string? key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;
        string localizedString = LocalizeDictionary.Instance.GetLocalizedObject(key, null, null) as string ?? key;
        return string.Format(localizedString, args);
    }

    public static string FormatWith(this string format, params object?[] args)
    {
        return string.Format(format, args);
    }

    public static void AddRange<T>(this ObservableCollection<T>? collection, IEnumerable<T>? items)
    {
        if (collection == null || items == null)
            return;
        foreach (var item in items)
            collection.Add(item);
    }

    public static bool IsTrue(this bool? value)
    {
        return value == true;
    }
}