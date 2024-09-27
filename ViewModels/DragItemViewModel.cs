using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MFATools.Utils;
using MFATools.Views;

namespace MFATools.ViewModels;

public class DragItemViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DragItemViewModel"/> class.
    /// </summary>
    /// <param name="name">The name (viewed name).</param>
    /// <param name="originalName">The original name (may not be the same as viewed name).</param>
    /// <param name="storageKey">The storage key.</param>
    public DragItemViewModel(TaskInterfaceItem interfaceItem)
    {
        InterfaceItem = interfaceItem;
        Name = interfaceItem.Name ?? "未命名";
    }


    private string _name = string.Empty;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private bool? _isCheckedWithNull = false;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the key is checked with null.
    /// </summary>
    public bool? IsCheckedWithNull
    {
        get => _isCheckedWithNull;
        set
        {
            SetProperty(ref _isCheckedWithNull, value);
            value ??= false;
            if (InterfaceItem != null)
                InterfaceItem.Check = IsChecked;
            JsonHelper.WriteToJsonFilePath(MaaProcessor.Resource, "interface", MaaInterface.Instance);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the key is checked.
    /// </summary>
    public bool IsChecked
    {
        get => IsCheckedWithNull != false;
        set => IsCheckedWithNull = value;
    }


    private bool _enableSetting;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the setting enabled.
    /// </summary>
    public bool EnableSetting
    {
        get => _enableSetting;
        set
        {
            SetProperty(ref _enableSetting, value);
        }
    }

    private TaskInterfaceItem? _interfaceItem;

    public TaskInterfaceItem? InterfaceItem
    {
        get => _interfaceItem;
        set
        {
            if (_interfaceItem != null)
            {
                if (_interfaceItem.Name != null)
                    Name = _interfaceItem.Name;
                if ((_interfaceItem.Option == null || _interfaceItem.Option?.Count == 0) &&
                    _interfaceItem.Repeatable == false)
                    SettingVisibility = Visibility.Hidden;
            }

            SetProperty(ref _interfaceItem, value);
        }
    }

    private Visibility _visibility = Visibility.Visible;

    public Visibility SettingVisibility
    {
        get => _visibility;
        set => SetProperty(ref _visibility, value);
    }
}