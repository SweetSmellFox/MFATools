using System.Windows;
using System.Windows.Controls;
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
            Attribute ??= attribute;
    }

    private void InitializeTag()
    {
        Style = FindResource("TagBaseStyle") as Style;
        ContextMenu contextMenu = new ContextMenu();
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
            HitMode = HitMode.None, PlacementType = PlacementType.Left
        };
        popTip.BindLocalization("CopiedToClipboard", Poptip.ContentProperty);
        Poptip.SetInstance(this, popTip);
    }

    private void Tip()
    {
        if (popTip != null)
        {
            TaskManager.RunTaskAsync(() =>
            {
                Dispatcher.Invoke(() => { popTip.IsOpen = true; });

                Task.Delay(500).Wait();

                Dispatcher.Invoke(() => { popTip.IsOpen = false; });
            });
        }
    }

    private void Copy(object sender, RoutedEventArgs e)
    {
        Clipboard.SetDataObject(Attribute.ToString());
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