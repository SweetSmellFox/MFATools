using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Data.Enum;
using HandyControl.Tools.Extension;
using MFATools.Utils;
using Attribute = MFATools.Utils.Attribute;

namespace MFATools.Controls;

public class AttributeTag : Tag
{
    public Attribute Attribute
    {
        get => (Attribute)GetValue(AttributeProperty);
        set => SetValue(AttributeProperty, value);
    }

    public static readonly DependencyProperty AttributeProperty =
        DependencyProperty.Register(nameof(Attribute), typeof(Attribute), typeof(AttributeTag),
            new PropertyMetadata(null, OnAttributeValueChanged));

    private static void OnAttributeValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AttributeTag tag)
        {
            if (e.NewValue == null)
            {
                if (tag.Parent is Panel parentPanel)
                    parentPanel.Children.Remove(tag);
            }
            else if (e.NewValue is Attribute attribute)
            {
                if (string.IsNullOrWhiteSpace(attribute.Value?.ToString()))
                {
                    if (tag.Parent is Panel parentPanel)
                        parentPanel.Children.Remove(tag);
                }
                else
                {
                    string contentText;

                    if (attribute.Value is List<List<int>> lli)
                    {
                        contentText = $"{tag.Attribute.Key} : {ConvertListToString(lli)}";
                    }
                    else if (attribute.Value is List<int> li)
                    {
                        contentText = $"{tag.Attribute.Key} : [{string.Join(",", li)}]";
                    }
                    else if (attribute.Value is List<string> ls)
                    {
                        contentText = $"{tag.Attribute.Key} : [{string.Join(",", ls)}]";
                    }
                    else if (attribute.Value is string s)
                    {
                        contentText = $"{tag.Attribute.Key} : {s}";
                    }
                    else
                    {
                        contentText = $"{tag.Attribute.Key} : {attribute.Value}";
                    }

                    // 使用 TextBlock 包装内容并设置 TextTrimming

                    var textBlock = new TextBlock
                    {
                        Text = contentText,
                        TextAlignment = TextAlignment.Left,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MaxWidth = 310,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap
                    };
                    tag.Content = textBlock;
                }
            }
        }
    }

    static string ConvertListToString(List<List<int>> listOfLists)
    {
        var formattedLists = listOfLists
            .Select(innerList => $"[{string.Join(",", innerList)}]");
        return string.Join(",", formattedLists);
    }

    static AttributeTag()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AttributeTag),
            new FrameworkPropertyMetadata(typeof(AttributeTag)));
    }

    public AttributeTag()
    {
        InitializeTag();
    }

    public AttributeTag(Attribute? attribute)
    {
        InitializeTag();
        if (attribute != null)
            Attribute = attribute;
    }

    private void InitializeTag()
    {
        Style = FindResource("TagBaseStyle") as Style;
        ContextMenu contextMenu = new ContextMenu();
        MouseDown += (_, args) =>
        {
            if (args.ChangedButton == MouseButton.Left)
                Copy(null, null);
        };
        MenuItem copyItem = new();
        copyItem.BindLocalization("Copy", MenuItem.HeaderProperty);
        copyItem.Click += Copy;
        MenuItem copyKeyItem = new();
        copyKeyItem.BindLocalization("CopyKey", MenuItem.HeaderProperty);
        copyKeyItem.Click += CopyKey;
        MenuItem copyValueItem = new();
        copyValueItem.BindLocalization("CopyValue", MenuItem.HeaderProperty);
        copyValueItem.Click += CopyAttribute;
        contextMenu.Items.Add(copyItem);
        contextMenu.Items.Add(copyKeyItem);
        contextMenu.Items.Add(copyValueItem);
        ContextMenu = contextMenu;
        popTip = new()
        {
            HitMode = HitMode.None, PlacementType = PlacementType.BottomLeft
        };
        popTip.BindLocalization("CopiedToClipboard", Poptip.ContentProperty);
        Poptip.SetInstance(this, popTip);
    }

    private CancellationTokenSource? _fadeOutCts;

    public void ShowPopTip(Poptip popTip)
    {
        CallSwitchPopTip(popTip, true);
    }

    public void HidePopTip(Poptip popTip)
    {
        CallSwitchPopTip(popTip, false);
    }

    private void CallSwitchPopTip(Poptip popTip, bool isShow)
    {
        if (popTip == null) throw new ArgumentNullException(nameof(popTip));

        var method = typeof(Poptip).GetMethod("SwitchPoptip", BindingFlags.NonPublic | BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException("Method SwitchPoptip not found.");
        }

        // 调用私有方法
        method.Invoke(popTip, [isShow]);
    }

    private void Tip()
    {
        if (popTip != null)
        {
            ShowPopTip(popTip);

            _fadeOutCts?.Cancel();
            _fadeOutCts = new CancellationTokenSource();

            TaskManager.RunTaskAsync(() =>
            {
                try
                {
                    Task.Delay(600, _fadeOutCts.Token).Wait();
                    if (!_fadeOutCts.Token.IsCancellationRequested)
                    {
                        popTip.Dispatcher.Invoke(() => HidePopTip(popTip));
                    }
                }
                catch (TaskCanceledException)
                {
                }
            });
        }
    }


    private void Copy(object? sender, RoutedEventArgs? e)
    {
        string attributeText = Attribute.ToString();

        attributeText = attributeText.Replace("recommended roi", "roi");

        Clipboard.SetDataObject(attributeText);
        Tip();
    }

    private void CopyKey(object sender, RoutedEventArgs e)
    {
        Clipboard.SetDataObject(Attribute.GetKey());
        Tip();
    }

    private void CopyAttribute(object sender, RoutedEventArgs e)
    {
        Clipboard.SetDataObject(Attribute.GetValue());
        Tip();
    }

    private Poptip? popTip;
}