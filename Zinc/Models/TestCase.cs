using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using Zinc.Core.Models;

namespace Zinc.Models;

/// <summary>一个测试点：输入、期望输出与最近一次评测的观测值。</summary>
public partial class TestCase : ObservableObject
{
    /// <summary>从 1 开始的序号，删除测试点后由 <c>EditorViewModel</c> 重排。</summary>
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string? _input;

    [ObservableProperty]
    private string? _expectedOutput;

    [ObservableProperty]
    [property: JsonIgnore]
    private string? _actualOutput;

    [ObservableProperty]
    [property: JsonIgnore]
    private string? _errorOutput;

    [ObservableProperty]
    [property: JsonIgnore]
    private JudgeResult? _result;

    [ObservableProperty]
    [property: JsonIgnore]
    private double _executionTimeMs;
}
