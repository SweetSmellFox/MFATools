﻿using System.Collections.ObjectModel;
using System.Windows;
using HandyControl.Controls;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Extensions;

namespace MFATools.Utils;

public static class MFAExtensions
{
    public const double HorizontalExpansion = 100;
    public const double VerticalExpansion = 100;
    
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