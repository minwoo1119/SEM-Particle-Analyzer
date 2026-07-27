using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SemParticleAnalyzer.Infrastructure;
using SemParticleAnalyzer.Models;
using SemParticleAnalyzer.Services;

namespace SemParticleAnalyzer.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IImageLoader _imageLoader;
    private readonly IAnalysisService _analysisService;
    private readonly IResultExportService _exportService;
    private readonly IAnalysisPresetService _presetService;
    private readonly ICalibrationService _calibrationService;
    private Mat? _source;
    private AnalysisResult? _result;
    private SourceImageInfo? _sourceInfo;
    private CancellationTokenSource? _analysisCancellation;
    private BitmapSource? _displayImage;
    private string _statusText = "이미지를 열어 분석을 시작하세요.";
    private bool _isBusy;
    private ViewerMode _viewerMode = ViewerMode.Original;
    private ViewerTool _viewerTool = ViewerTool.SelectAndRoi;
    private ParticleMeasurement? _selectedObject;

    public MainViewModel()
    {
        _imageLoader = new ImageLoader();
        _analysisService = new AnalysisService();
        _exportService = new ResultExportService();
        _presetService = new AnalysisPresetService();
        _calibrationService = new CalibrationService();
        OpenImageCommand = new AsyncRelayCommand(OpenImageAsync, () => !IsBusy);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => _source is not null && !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => _source is not null && _result is not null && !IsBusy);
        SavePresetCommand = new AsyncRelayCommand(SavePresetAsync, () => !IsBusy);
        LoadPresetCommand = new AsyncRelayCommand(LoadPresetAsync, () => !IsBusy);
        ToggleObjectCommand = new RelayCommand(ToggleSelectedObject, () => SelectedObject is not null);
        CalculateScaleCommand = new RelayCommand(CalculateScale, () => _source is not null);
    }

    public AnalysisSettings Settings { get; private set; } = new();
    public ObservableCollection<ParticleMeasurement> Objects { get; } = [];
    public ObservableCollection<HistogramBin> DiameterHistogram { get; } = [];
    public Array ThresholdModes => Enum.GetValues<ThresholdMode>();
    public Array BorderRules => Enum.GetValues<BorderObjectRule>();
    public Array ViewerModes => Enum.GetValues<ViewerMode>();
    public Array ViewerTools => Enum.GetValues<ViewerTool>();
    public Array LengthUnits => Enum.GetValues<LengthUnit>();
    public AsyncRelayCommand OpenImageCommand { get; }
    public AsyncRelayCommand AnalyzeCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public AsyncRelayCommand SavePresetCommand { get; }
    public AsyncRelayCommand LoadPresetCommand { get; }
    public RelayCommand ToggleObjectCommand { get; }
    public RelayCommand CalculateScaleCommand { get; }

    public BitmapSource? DisplayImage { get => _displayImage; private set => SetProperty(ref _displayImage, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RefreshCommands();
        }
    }
    public ViewerMode ViewerMode
    {
        get => _viewerMode;
        set { if (SetProperty(ref _viewerMode, value)) RefreshDisplay(); }
    }
    public ViewerTool ViewerTool
    {
        get => _viewerTool;
        set => SetProperty(ref _viewerTool, value);
    }
    public ParticleMeasurement? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value))
                ToggleObjectCommand.NotifyCanExecuteChanged();
        }
    }
    public SourceImageInfo? SourceInfo => _sourceInfo;
    public AnalysisSummary? Summary => _result?.Summary;
    public int ImagePixelWidth => _source?.Width ?? 0;
    public int ImagePixelHeight => _source?.Height ?? 0;
    public string ImageDescription => _sourceInfo is null ? "이미지 없음" :
        $"{_sourceInfo.Width:N0} × {_sourceInfo.Height:N0} px  ·  {_sourceInfo.Depth}  ·  {_sourceInfo.Channels} channel";
    public string SummaryText => Summary is null ? "분석 결과 없음" :
        $"분할 {Summary.SegmentedCount:N0}  |  통과 {Summary.AcceptedCount:N0}  |  제외 {Summary.RejectedCount:N0}  |  " +
        $"D50 {Summary.D50Pixel?.ToString("F2") ?? "-"} px  |  면적률 {Summary.AreaFraction:P2}  |  {Summary.ProcessingTime.TotalMilliseconds:N0} ms";

    public void SetRoi(RectangleRoi roi)
    {
        Settings.Roi = roi;
        OnPropertyChanged(nameof(Settings));
        StatusText = $"ROI: X {roi.X}, Y {roi.Y}, W {roi.Width}, H {roi.Height} px";
    }

    public bool SelectObjectAt(double imageX, double imageY)
    {
        if (Objects.Count == 0)
        {
            SelectedObject = null;
            return false;
        }
        var selected = Objects
            .Where(x => ContainsPoint(x, imageX, imageY))
            .OrderBy(x => x.BoundingBoxWidth * x.BoundingBoxHeight)
            .FirstOrDefault();
        if (selected is null)
        {
            SelectedObject = null;
            return false;
        }
        SelectedObject = selected;
        StatusText = $"객체 {selected.ObjectId}: Area {selected.AreaPixel2:F2} px², Mean GV {selected.MeanGv:F1}, " +
                     (selected.FinalAccepted ? "Accepted" : $"Rejected ({selected.RejectionSummary})");
        return true;
    }

    private static bool ContainsPoint(ParticleMeasurement particle, double x, double y)
    {
        if (x < particle.BoundingBoxX || x > particle.BoundingBoxX + particle.BoundingBoxWidth ||
            y < particle.BoundingBoxY || y > particle.BoundingBoxY + particle.BoundingBoxHeight)
            return false;
        if (particle.ContourPoints.Count < 3) return true;

        var inside = false;
        for (int i = 0, j = particle.ContourPoints.Count - 1; i < particle.ContourPoints.Count; j = i++)
        {
            var a = particle.ContourPoints[i];
            var b = particle.ContourPoints[j];
            if ((a.Y > y) != (b.Y > y) &&
                x < (double)(b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private async Task OpenImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "SEM 이미지 열기",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        await RunBusyAsync(async token =>
        {
            var loaded = await _imageLoader.LoadAsync(dialog.FileName, token);
            _source?.Dispose();
            _result?.Dispose();
            _source = loaded.Image;
            _sourceInfo = loaded.Info;
            _result = null;
            Settings.Roi = new RectangleRoi { X = 0, Y = 0, Width = _source.Width, Height = _source.Height };
            SelectedObject = null;
            Objects.Clear();
            DiameterHistogram.Clear();
            ViewerMode = ViewerMode.Original;
            RefreshDisplay();
            NotifyState();
            StatusText = $"{loaded.Info.FileName}을(를) 불러왔습니다. 드래그하여 분석 ROI를 지정하세요.";
        });
    }

    private async Task AnalyzeAsync()
    {
        if (_source is null) return;
        await RunBusyAsync(async token =>
        {
            _result?.Dispose();
            var snapshot = CloneSettings(Settings);
            _result = await _analysisService.AnalyzeAsync(_source, snapshot, token);
            SelectedObject = null;
            Objects.Clear();
            foreach (var item in _result.Objects) Objects.Add(item);
            RefreshDistribution();
            ViewerMode = ViewerMode.Overlay;
            RefreshDisplay();
            NotifyState();
            StatusText = $"분석 완료: {_result.Summary.AcceptedCount:N0}개 통과, {_result.Summary.RejectedCount:N0}개 제외";
        });
    }

    private async Task ExportAsync()
    {
        if (_source is null || _sourceInfo is null || _result is null) return;
        var dialog = new OpenFolderDialog { Title = "결과를 저장할 상위 폴더 선택" };
        if (dialog.ShowDialog() != true) return;
        await RunBusyAsync(async token =>
        {
            // 저장 직전 원본 해상도로 재분석해 Preview와 최종 결과의 불일치를 방지한다.
            _result.Dispose();
            _result = await _analysisService.AnalyzeAsync(_source, CloneSettings(Settings), token);
            var path = await _exportService.ExportAsync(dialog.FolderName, _source, _sourceInfo,
                CloneSettings(Settings), _result, token);
            SelectedObject = null;
            Objects.Clear();
            foreach (var item in _result.Objects) Objects.Add(item);
            RefreshDistribution();
            RefreshDisplay();
            NotifyState();
            StatusText = $"원본 해상도 분석 결과를 저장했습니다: {path}";
        });
    }

    private async Task SavePresetAsync()
    {
        var dialog = new SaveFileDialog { Title = "분석 설정 저장", Filter = "Analysis preset|*.json", FileName = "analysis_settings.json" };
        if (dialog.ShowDialog() != true) return;
        await _presetService.SaveAsync(dialog.FileName, Settings, CancellationToken.None);
        StatusText = "분석 설정을 저장했습니다.";
    }

    private async Task LoadPresetAsync()
    {
        var dialog = new OpenFileDialog { Title = "분석 설정 불러오기", Filter = "Analysis preset|*.json" };
        if (dialog.ShowDialog() != true) return;
        Settings = await _presetService.LoadAsync(dialog.FileName, CancellationToken.None);
        OnPropertyChanged(nameof(Settings));
        StatusText = "분석 설정을 불러왔습니다.";
    }

    private void ToggleSelectedObject()
    {
        if (SelectedObject is null) return;
        SelectedObject.ManualOverride = SelectedObject.FinalAccepted ? ManualOverrideType.Exclude : ManualOverrideType.Include;
        var index = Objects.IndexOf(SelectedObject);
        Objects[index] = SelectedObject;
        OnPropertyChanged(nameof(SummaryText));
        StatusText = $"객체 {SelectedObject.ObjectId}의 수동 판정을 변경했습니다.";
    }

    private void CalculateScale()
    {
        if (_source is null) return;
        try
        {
            var scale = _calibrationService.CalculateImageScale(_source.Width, _source.Height, Settings.Calibration);
            Settings.Calibration.MicrometersPerPixel = scale;
            Settings.Calibration.Enabled = true;
            Settings.Calibration.PixelDistance = Settings.Calibration.ActualImageWidth is > 0 ? _source.Width : _source.Height;
            Settings.Calibration.ActualLength = Settings.Calibration.ActualImageWidth
                ?? Settings.Calibration.ActualImageHeight ?? 0;
            Settings.Calibration.Unit = Settings.Calibration.InputUnit switch
            {
                LengthUnit.Nanometer => "nm",
                LengthUnit.Millimeter => "mm",
                _ => "µm"
            };
            Settings = CloneSettings(Settings);
            OnPropertyChanged(nameof(Settings));
            StatusText = $"Scale calibration 완료: {scale:G8} µm/pixel";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            MessageBox.Show(ex.Message, "Scale calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> operation)
    {
        _analysisCancellation?.Dispose();
        _analysisCancellation = new CancellationTokenSource();
        IsBusy = true;
        try { await operation(_analysisCancellation.Token); }
        catch (OperationCanceledException) { StatusText = "작업을 취소했습니다."; }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            MessageBox.Show(ex.Message, "SEM Particle Analyzer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { IsBusy = false; }
    }

    private void Cancel() => _analysisCancellation?.Cancel();

    private void RefreshDisplay()
    {
        var mat = ViewerMode switch
        {
            ViewerMode.Preprocessed => _result?.Preprocessed,
            ViewerMode.BinaryMask => _result?.BinaryMask,
            ViewerMode.Overlay => _result?.Overlay,
            _ => _source
        };
        if (mat is null) { DisplayImage = null; return; }
        var bitmap = BitmapSourceConverter.ToBitmapSource(mat);
        bitmap.Freeze();
        DisplayImage = bitmap;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(SourceInfo));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ImagePixelWidth));
        OnPropertyChanged(nameof(ImagePixelHeight));
        OnPropertyChanged(nameof(ImageDescription));
        OnPropertyChanged(nameof(SummaryText));
        RefreshCommands();
    }

    private void RefreshDistribution()
    {
        DiameterHistogram.Clear();
        foreach (var bin in ParticleStatistics.Histogram(
                     Objects.Where(x => x.FinalAccepted).Select(x => x.EquivalentDiameterPixel)))
            DiameterHistogram.Add(bin);
    }

    private void RefreshCommands()
    {
        OpenImageCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        SavePresetCommand.NotifyCanExecuteChanged();
        LoadPresetCommand.NotifyCanExecuteChanged();
        CalculateScaleCommand.NotifyCanExecuteChanged();
    }

    private static AnalysisSettings CloneSettings(AnalysisSettings settings) =>
        JsonSerializer.Deserialize<AnalysisSettings>(JsonSerializer.Serialize(settings))!;

    public void Dispose()
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
        _result?.Dispose();
        _source?.Dispose();
    }
}
