using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Zinc.Abstractions;
using Zinc.Core.Abstractions;
using Zinc.Core.Models;
using Zinc.Models;

namespace Zinc.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly IFileService _fileService;
    private readonly IEditorViewModelFactory _editorFactory;
    private readonly ISettingsService<AppSettings> _settingsService;
    private readonly IWindowService _windowService;
    private readonly ISessionService _sessionService;

    private readonly IReadOnlyList<FileFilter> _filters =
    [
        new() { Name = "C++代码文件", Patterns = ["*.cpp", "*.cxx"] }
    ];

    private int _untitledCount;
    private readonly DispatcherTimer _sessionSaveTimer;
    private bool _isRestoring;

    public ObservableCollection<EditorViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private EditorViewModel? _selectedItem;

    public MainViewModel(
        IDialogService dialogService,
        IFileService fileService,
        IEditorViewModelFactory editorFactory,
        IWindowService windowService,
        ISettingsService<AppSettings> settingsService,
        ISessionService sessionService)
    {
        _dialogService = dialogService;
        _fileService = fileService;
        _editorFactory = editorFactory;
        _settingsService = settingsService;
        _windowService = windowService;
        _sessionService = sessionService;

        _sessionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _sessionSaveTimer.Tick += async (_, _) => await SaveSessionAsync();

        Tabs.CollectionChanged += OnTabsCollectionChanged;

        _ = RestoreSessionAsync();
    }


    /// <summary>新建一个空白标签页并选中。</summary>
    [RelayCommand]
    public void AddNewTab()
    {
        var vm = _editorFactory.Create();
        vm.FileName = $"未标题 {++_untitledCount}";
        Tabs.Add(vm);
        SelectedItem = vm;
    }

    /// <summary>关闭标签页；关闭最后一个标签页时自动补一个空白页。</summary>
    public async Task CloseTab(EditorViewModel vm)
    {
        if (vm.IsDirty || string.IsNullOrEmpty(vm.FilePath))
        {
            await new FAContentDialog()
            {
                Title = "是否要保存代码？",
                Content = new TextBlock()
                {
                    Text = vm.FileName + " 有未保存的更改。"
                },
                PrimaryButtonText = "保存并退出",
                PrimaryButtonCommand = saveCurrentTabCommand,
                SecondaryButtonText = "不保存并退出",
                SecondaryButtonCommand = null,
                CloseButtonText = "取消",
                DefaultButton = FAContentDialogButton.Primary
            }.ShowAsync();
        }

        Tabs.Remove(vm);
        vm.Dispose();

        if (Tabs.Count == 0)
        {
            AddNewTab();
        }

        SelectedItem = Tabs.FirstOrDefault();
    }


    [RelayCommand]
    private void OpenSettings() => _windowService.ShowSettings();

    [RelayCommand]
    private async Task OpenAsync()
    {
        var filePath = await _dialogService.OpenFilePathAsync("选择要打开的文件", _filters);
        if (string.IsNullOrEmpty(filePath))
            return;

        // 同一文件只保留一个标签页，已打开时直接切换。
        var existing = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedItem = existing;
            return;
        }

        var file = _fileService.LoadFile(filePath);
        var vm = _editorFactory.Create(file, filePath);
        vm.FileName = Path.GetFileName(filePath);

        Tabs.Add(vm);
        SelectedItem = vm;
    }

    [RelayCommand]
    private Task SaveCurrentTabAsync()
    {
        return SelectedItem?.SaveAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    private Task SaveAsCurrentTabAsync()
    {
        return SelectedItem?.SaveAsAsync() ?? Task.CompletedTask;
    }
    public AppSettings Settings => _settingsService.Current;

    /// <summary>启动时恢复上次会话：恢复标签页、光标位置与活动文件。</summary>
    private async Task RestoreSessionAsync()
    {
        var snapshot = await _sessionService.LoadAsync();
        if (snapshot is null || snapshot.Tabs.Count == 0)
        {
            AddNewTab();
            return;
        }

        _isRestoring = true;
        try
        {
            foreach (var tab in snapshot.Tabs)
            {
                if (string.IsNullOrEmpty(tab.FilePath) || !File.Exists(tab.FilePath))
                    continue;

                var file = _fileService.LoadFile(tab.FilePath);
                var vm = _editorFactory.Create(file, tab.FilePath);
                vm.FileName = Path.GetFileName(tab.FilePath);
                vm.CaretOffset = tab.CaretOffset;

                Tabs.Add(vm);
            }

            if (Tabs.Count == 0)
            {
                AddNewTab();
            }
            else if (!string.IsNullOrEmpty(snapshot.ActiveFilePath))
            {
                var active = Tabs.FirstOrDefault(t =>
                    string.Equals(t.FilePath, snapshot.ActiveFilePath, StringComparison.OrdinalIgnoreCase));
                SelectedItem = active ?? Tabs[0];
            }
            else
            {
                SelectedItem = Tabs[0];
            }
        }
        finally
        {
            _isRestoring = false;
        }

        RequestSessionSave();
    }

    /// <summary>在标签页增删或切换时请求保存会话。</summary>
    private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isRestoring) return;
        RequestSessionSave();
    }

    partial void OnSelectedItemChanged(EditorViewModel? value)
    {
        if (_isRestoring) return;
        RequestSessionSave();
    }

    /// <summary>防抖保存当前会话（标签列表、光标位置、活动文件）。</summary>
    private void RequestSessionSave()
    {
        _sessionSaveTimer.Stop();
        _sessionSaveTimer.Start();
    }

    /// <summary>将当前标签页状态快照并请求持久化。</summary>
    private async Task SaveSessionAsync()
    {
        var snapshot = new SessionSnapshot
        {
            ActiveFilePath = SelectedItem?.FilePath,
        };

        foreach (var vm in Tabs)
        {
            if (string.IsNullOrEmpty(vm.FilePath)) continue;

            snapshot.Tabs.Add(new TabSession
            {
                FilePath = vm.FilePath,
                CaretOffset = vm.CaretOffset,
            });
        }

        _sessionService.Save(snapshot);
    }
}