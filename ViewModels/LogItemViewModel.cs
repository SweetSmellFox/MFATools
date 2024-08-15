using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFLocalizeExtension.Extensions;
using System.Globalization;
using MFATools.Utils;

namespace MFATools.ViewModels
{
    public class LogItemViewModel : ObservableObject
    {
        private readonly string[]? _formatArgsKeys;

        public LogItemViewModel(string resourceKey, Brush color, string weight = "Regular", bool useKey = false,
            string dateFormat = "MM'-'dd'  'HH':'mm':'ss", bool showTime = true, params string[]? formatArgsKeys)
        {
            _resourceKey = resourceKey;

            Time = DateTime.Now.ToString(dateFormat);
            Color = color;
            Weight = weight;
            ShowTime = showTime;
            if (useKey)
            {
                _formatArgsKeys = formatArgsKeys;
                UpdateContent();

                // 订阅语言切换事件
                LanguageManager.LanguageChanged += OnLanguageChanged;
            }
            else
            {
                Content = resourceKey;
            }
        }

        public LogItemViewModel(string content, Brush color, string weight = "Regular",
            string dateFormat = "MM'-'dd'  'HH':'mm':'ss", bool showTime = true)
        {
            Time = DateTime.Now.ToString(dateFormat);
            Color = color;
            Weight = weight;
            ShowTime = showTime;

            Content = content;
        }

        private string? _time;

        public string? Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        private bool _showTime = true;

        public bool ShowTime
        {
            get => _showTime;
            set => SetProperty(ref _showTime, value);
        }

        private string? _content;

        public string? Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private Brush? _color;

        public Brush? Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        private string _weight = "Regular";

        public string Weight
        {
            get => _weight;
            set => SetProperty(ref _weight, value);
        }

        private string? _resourceKey;

        public string? ResourceKey
        {
            get => _resourceKey;
            set
            {
                if (SetProperty(ref _resourceKey, value))
                {
                    UpdateContent();
                }
            }
        }

        private void UpdateContent()
        {
            if (_formatArgsKeys == null || _formatArgsKeys.Length == 0)

                Content = ResourceKey.GetLocalizationString();
            else
            {
                // 获取每个格式化参数的本地化字符串
                var formatArgs = _formatArgsKeys.Select(key => key.GetLocalizedFormattedString()).ToArray();

                // 使用本地化字符串更新内容
                Content = _resourceKey.GetLocalizedFormattedString(formatArgs);
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateContent();
        }
    }
}