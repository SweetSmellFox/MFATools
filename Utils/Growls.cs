﻿using System.Windows;
using HandyControl.Controls;
using HandyControl.Data;

namespace MFATools.Utils;

public static class Growls
{
  public static void Warning(string message, string token = "")
    {
        Process(() =>
        {
            Growl.Warning(new GrowlInfo
            {
                IsCustom = true,
                Message = message,
                WaitTime = 3,
                Token = token,
                IconKey = ResourceToken.WarningGeometry,
                IconBrushKey = ResourceToken.WarningBrush,
            });
        });
    }

    public static void WarningGlobal(string message, string token = "")
    {
        Process(() =>
        {
            Growl.InfoGlobal(new GrowlInfo
            {
                IsCustom = true,
                Message = message,
                WaitTime = 3,
                IconKey = ResourceToken.WarningGeometry,
                IconBrushKey = ResourceToken.WarningBrush,
                Token = token
            });
        });
    }

    public static void Error(string message, string token = "")
    {
        Process(() =>
        {
            Growl.Info(new GrowlInfo
            {
                IsCustom = true,
                Message = message,
                WaitTime = 6,
                IconKey = ResourceToken.ErrorGeometry,
                IconBrushKey = ResourceToken.DangerBrush,
                Icon = null,
                Token = token
            });

        });
    }

    public static void ErrorGlobal(string message, string token = "")
    {
        Process(() =>
        {
            Growl.InfoGlobal(new GrowlInfo
            {
                IsCustom = true,
                Message = message,
                WaitTime = 6,
                IconKey = ResourceToken.ErrorGeometry,
                IconBrushKey = ResourceToken.DangerBrush,
                Icon = null,
                Token = token
            });
        });
    }
    public static void InfoGlobal(string message, string token = "")
    {
        Process(() =>
        {
            Growl.InfoGlobal(message);
        });
    }

    public static void Info(string message, string token = "")
    {
        Process(() =>
        {
            Growl.Info(message);
        });
    }

    public static void Process(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            action();
        else
            Application.Current.Dispatcher.Invoke(action);
    }
}