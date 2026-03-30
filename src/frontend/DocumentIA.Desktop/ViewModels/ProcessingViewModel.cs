using DocumentIA.Desktop.Models;
using DocumentIA.Desktop.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace DocumentIA.Desktop.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
    }

    public class ProcessingViewModel : NotifyPropertyChanged
    {
        private static readonly string[] DefaultActivityOrder =
        {
            "Clasificar",
            "Extraer",
            "Prompt",
            "Validar",
            "Integrar",
            "SubirGDC",
            "Persistir",
            "Resultado"
        };

        private readonly IOrchestratorApiClient _apiClient;
        private readonly DispatcherTimer _apiConnectionTimer;
        private CancellationTokenSource? _pollingCts;
        private string? _lastLoggedActivity;
        private bool _isCheckingApiConnection;

        private static readonly TimeSpan ApiConnectionCheckInterval = TimeSpan.FromSeconds(30);
        private const decimal DefaultClassificationThreshold = 0.5m;
        private const decimal DefaultExtractionThreshold = 0.80m;

        public ObservableCollection<ActivityStatus> ActivityStatuses { get; } = new ObservableCollection<ActivityStatus>();
        public ObservableCollection<ActivityLogEntry> ActivityLogs { get; } = new ObservableCollection<ActivityLogEntry>();

        private string? _selectedDocumentPath;
        public string? SelectedDocumentPath
        {
            get => _selectedDocumentPath;
            set
            {
                if (_selectedDocumentPath != value)
                {
                    _selectedDocumentPath = value;
                    OnPropertyChanged(nameof(SelectedDocumentPath));
                    OnPropertyChanged(nameof(IsExecuteEnabled));
                }
            }
        }

        private static readonly TipologiaPublicadaDto AutoItem = new() { Identificador = "auto", Nombre = "Auto" };

        public ObservableCollection<TipologiaPublicadaDto> AvailableTipologias { get; } = new ObservableCollection<TipologiaPublicadaDto> { AutoItem };

        private TipologiaPublicadaDto _selectedTipologia = AutoItem;
        public TipologiaPublicadaDto SelectedTipologia
        {
            get => _selectedTipologia;
            set
            {
                if (_selectedTipologia != value)
                {
                    _selectedTipologia = value ?? AutoItem;
                    OnPropertyChanged(nameof(SelectedTipologia));
                    OnPropertyChanged(nameof(SelectedClassificationType));
                }
            }
        }

        // Computed from SelectedTipologia — kept for backwards-compat with BuildRequest
        public string SelectedClassificationType => _selectedTipologia.Identificador;

        private string? _instanceId;
        public string? InstanceId
        {
            get => _instanceId;
            set
            {
                if (_instanceId != value)
                {
                    _instanceId = value;
                    OnPropertyChanged(nameof(InstanceId));
                }
            }
        }

        private string? _correlationId;
        public string? CorrelationId
        {
            get => _correlationId;
            set
            {
                if (_correlationId != value)
                {
                    _correlationId = value;
                    OnPropertyChanged(nameof(CorrelationId));
                }
            }
        }

        private object? _outputJson;
        public object? OutputJson
        {
            get => _outputJson;
            set
            {
                if (_outputJson != value)
                {
                    _outputJson = value;
                    OnPropertyChanged(nameof(OutputJson));
                }
            }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                    OnPropertyChanged(nameof(IsExecuteEnabled));
                }
            }
        }

        private bool _isApiConnected;
        public bool IsApiConnected
        {
            get => _isApiConnected;
            set
            {
                if (_isApiConnected != value)
                {
                    _isApiConnected = value;
                    OnPropertyChanged(nameof(IsApiConnected));
                    OnPropertyChanged(nameof(IsExecuteEnabled));
                }
            }
        }

        private string _apiStatusMessage = string.Empty;
        public string ApiStatusMessage
        {
            get => _apiStatusMessage;
            set
            {
                if (_apiStatusMessage != value)
                {
                    _apiStatusMessage = value;
                    OnPropertyChanged(nameof(ApiStatusMessage));
                }
            }
        }

        private bool _skipDuplicateCheck = true;
        public bool SkipDuplicateCheck
        {
            get => _skipDuplicateCheck;
            set
            {
                if (_skipDuplicateCheck != value)
                {
                    _skipDuplicateCheck = value;
                    OnPropertyChanged(nameof(SkipDuplicateCheck));
                }
            }
        }

        private bool _forceReprocess = true;
        public bool ForceReprocess
        {
            get => _forceReprocess;
            set
            {
                if (_forceReprocess != value)
                {
                    _forceReprocess = value;
                    OnPropertyChanged(nameof(ForceReprocess));
                }
            }
        }

        private bool _skipGDCUpload = true;
        public bool SkipGDCUpload
        {
            get => _skipGDCUpload;
            set
            {
                if (_skipGDCUpload != value)
                {
                    _skipGDCUpload = value;
                    OnPropertyChanged(nameof(SkipGDCUpload));
                }
            }
        }

        private string _inputIdActivo = string.Empty;
        public string InputIdActivo
        {
            get => _inputIdActivo;
            set
            {
                if (_inputIdActivo != value)
                {
                    _inputIdActivo = value;
                    OnPropertyChanged(nameof(InputIdActivo));
                }
            }
        }

        private string _inputSubmittedBy = "usuario.desktop@sareb.es";
        public string InputSubmittedBy
        {
            get => _inputSubmittedBy;
            set
            {
                if (_inputSubmittedBy != value)
                {
                    _inputSubmittedBy = value;
                    OnPropertyChanged(nameof(InputSubmittedBy));
                }
            }
        }

        private string _classificationProvider = "auto";
        public string ClassificationProvider
        {
            get => _classificationProvider;
            set
            {
                if (_classificationProvider != value)
                {
                    _classificationProvider = value;
                    OnPropertyChanged(nameof(ClassificationProvider));
                }
            }
        }

        private string _classificationModel = "auto";
        public string ClassificationModel
        {
            get => _classificationModel;
            set
            {
                if (_classificationModel != value)
                {
                    _classificationModel = value;
                    OnPropertyChanged(nameof(ClassificationModel));
                }
            }
        }

        private string _classificationThreshold = "0.5";
        public string ClassificationThreshold
        {
            get => _classificationThreshold;
            set
            {
                if (_classificationThreshold != value)
                {
                    _classificationThreshold = value;
                    OnPropertyChanged(nameof(ClassificationThreshold));
                }
            }
        }

        private string _extractionModel = "auto";
        public string ExtractionModel
        {
            get => _extractionModel;
            set
            {
                if (_extractionModel != value)
                {
                    _extractionModel = value;
                    OnPropertyChanged(nameof(ExtractionModel));
                }
            }
        }

        private string _extractionThreshold = "0.80";
        public string ExtractionThreshold
        {
            get => _extractionThreshold;
            set
            {
                if (_extractionThreshold != value)
                {
                    _extractionThreshold = value;
                    OnPropertyChanged(nameof(ExtractionThreshold));
                }
            }
        }

        private string _extractionThresholdCompletitud = string.Empty;
        /// <summary>Umbral de completitud CU (ratio de campos esperados). Vacío = servidor usa tipología o global.</summary>
        public string ExtractionThresholdCompletitud
        {
            get => _extractionThresholdCompletitud;
            set
            {
                if (_extractionThresholdCompletitud != value)
                {
                    _extractionThresholdCompletitud = value;
                    OnPropertyChanged(nameof(ExtractionThresholdCompletitud));
                }
            }
        }

        private string _extractionThresholdConfianza = string.Empty;
        /// <summary>Umbral de confianza CU para no activar fallback GPT. Vacío = servidor usa tipología o global.</summary>
        public string ExtractionThresholdConfianza
        {
            get => _extractionThresholdConfianza;
            set
            {
                if (_extractionThresholdConfianza != value)
                {
                    _extractionThresholdConfianza = value;
                    OnPropertyChanged(nameof(ExtractionThresholdConfianza));
                }
            }
        }

        private string _apiCheckIntervalSeconds = "30";
        public string ApiCheckIntervalSeconds
        {
            get => _apiCheckIntervalSeconds;
            set
            {
                if (_apiCheckIntervalSeconds != value)
                {
                    _apiCheckIntervalSeconds = value;
                    OnPropertyChanged(nameof(ApiCheckIntervalSeconds));
                    ApplyApiConnectionCheckInterval();
                }
            }
        }

        public bool IsExecuteEnabled => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedDocumentPath) && IsApiConnected;

        public ICommand ExecuteCommand { get; }
        public ICommand SelectFileCommand { get; }

        public ProcessingViewModel()
        {
            _apiClient = new OrchestratorApiClient("http://localhost:7071");
            ExecuteCommand = new RelayCommand(_ => _ = ExecuteAsync(), _ => IsExecuteEnabled);
            SelectFileCommand = new RelayCommand(_ => SelectFile());

            _apiConnectionTimer = new DispatcherTimer
            {
                Interval = ApiConnectionCheckInterval
            };
            _apiConnectionTimer.Tick += async (_, _) => await RefreshApiConnectionAsync();
            ApplyApiConnectionCheckInterval();

            InitializeActivities();

            // Initial connection check on UI thread
            _ = RefreshApiConnectionAsync();
            _apiConnectionTimer.Start();

            // Load published tipologias (non-blocking)
            _ = LoadTipologiasAsync();
        }

        private async Task LoadTipologiasAsync()
        {
            // Remove any previously loaded items (keep AutoItem at index 0)
            while (AvailableTipologias.Count > 1)
                AvailableTipologias.RemoveAt(1);

            var tipologias = await _apiClient.GetTipologiasPublicadasAsync();
            foreach (var t in tipologias)
                AvailableTipologias.Add(t);
        }

        private void InitializeActivities()
        {
            ActivityStatuses.Clear();
            foreach (var activity in DefaultActivityOrder)
            {
                ActivityStatuses.Add(new ActivityStatus(activity));
            }

            UpdateLastActivityFlags();
        }

        private async Task CheckApiConnectionAsync()
        {
            try
            {
                IsApiConnected = await _apiClient.CheckConnectionAsync();
                ApiStatusMessage = IsApiConnected ? "API Conectada ✓" : "API No Disponible ✗";
            }
            catch (Exception ex)
            {
                IsApiConnected = false;
                ApiStatusMessage = $"Error: {ex.Message}";
            }
        }

        private async Task RefreshApiConnectionAsync()
        {
            if (_isCheckingApiConnection)
            {
                return;
            }

            _isCheckingApiConnection = true;
            try
            {
                await CheckApiConnectionAsync();
            }
            finally
            {
                _isCheckingApiConnection = false;
            }
        }

        private void SelectFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Seleccionar documento PDF"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedDocumentPath = dialog.FileName;
            }
        }

        private async Task ExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedDocumentPath) || !File.Exists(SelectedDocumentPath))
            {
                ActivityLogs.Add(new ActivityLogEntry("Error", ActivityStatusEnum.Failed, message: "Documento no encontrado"));
                return;
            }

            IsProcessing = true;
            OutputJson = null;
            _lastLoggedActivity = null;
            InitializeActivities();
            ActivityLogs.Clear();

            try
            {
                ActivityLogs.Add(new ActivityLogEntry("Inicio", ActivityStatusEnum.Running, message: "Leyendo documento..."));

                // Read document
                var documentBytes = File.ReadAllBytes(SelectedDocumentPath);
                var documentBase64 = Convert.ToBase64String(documentBytes);
                var documentName = Path.GetFileName(SelectedDocumentPath);

                // Build request
                var request = BuildRequest(documentName, documentBase64);
                CorrelationId = request.Traceability?.CorrelationId;

                ActivityLogs.Add(new ActivityLogEntry("Lectura", ActivityStatusEnum.Completed, message: $"{documentName}"));
                ActivityLogs.Add(new ActivityLogEntry("Envío", ActivityStatusEnum.Running, message: "Enviando a servidor..."));

                // Send request
                var response = await _apiClient.IngestDocumentAsync(request);
                InstanceId = response.InstanceId;

                ActivityLogs.Add(new ActivityLogEntry("Envío", ActivityStatusEnum.Completed, message: $"Instance: {response.InstanceId}"));
                ActivityLogs.Add(new ActivityLogEntry("Procesamiento", ActivityStatusEnum.Running, message: "Esperando resultado..."));

                // Poll status
                if (string.IsNullOrWhiteSpace(response.StatusQueryUri))
                {
                    throw new Exception("La respuesta no incluye StatusQueryUri");
                }

                await PollStatusAsync(response.StatusQueryUri);
            }
            catch (Exception ex)
            {
                ActivityLogs.Add(new ActivityLogEntry("Error", ActivityStatusEnum.Failed, message: ex.Message));
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private ProcessingRequest BuildRequest(string documentName, string documentBase64)
        {
            string? expectedType = SelectedClassificationType switch
            {
                "nota-simple@1.4" => "nota-simple@1.4",
                "resumen-documental" => "resumen-documental",
                _ => null // auto
            };

            var classificationThreshold = ParseThresholdOrDefault(ClassificationThreshold, DefaultClassificationThreshold);
            var extractionThreshold = ParseThresholdOrDefault(ExtractionThreshold, DefaultExtractionThreshold);
            var submittedBy = string.IsNullOrWhiteSpace(InputSubmittedBy)
                ? "usuario.desktop@sareb.es"
                : InputSubmittedBy.Trim();
            var idActivo = InputIdActivo.Trim();

            return new ProcessingRequest
            {
                Traceability = new Traceability
                {
                    CorrelationId = $"DESKTOP-{SelectedClassificationType}-{DateTime.Now:yyyyMMdd-HHmmss}",
                    SubmittedBy = submittedBy,
                    IdGdc = null,
                    IdActivo = idActivo
                },
                Document = new DocumentInfo
                {
                    Name = documentName,
                    Content = new DocumentContent
                    {
                        Base64 = documentBase64
                    }
                },
                Instructions = new Instructions
                {
                    ExpectedType = expectedType,
                    SkipDuplicateCheck = SkipDuplicateCheck,
                    ForceReprocess = ForceReprocess,
                    SkipGDCUpload = SkipGDCUpload,
                    Classification = new ClassificationSettings
                    {
                        Provider = expectedType == null ? NormalizeOptionalText(ClassificationProvider, "auto") : null,
                        Model = NormalizeOptionalText(ClassificationModel, "auto")!,
                        Threshold = classificationThreshold
                    },
                    Extraction = new ExtractionSettings
                    {
                        Model = NormalizeOptionalText(ExtractionModel, "auto")!,
                        Threshold = extractionThreshold,
                        ThresholdCompletitud = ParseOptionalThreshold(ExtractionThresholdCompletitud),
                        ThresholdConfianza = ParseOptionalThreshold(ExtractionThresholdConfianza)
                    }
                }
            };
        }

        private void ApplyApiConnectionCheckInterval()
        {
            if (int.TryParse(ApiCheckIntervalSeconds, out var seconds) && seconds > 0)
            {
                _apiConnectionTimer.Interval = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                _apiConnectionTimer.Interval = ApiConnectionCheckInterval;
            }
        }

        /// <summary>Parsea un umbral opcional. Retorna null si vacío o inválido (el servidor usará el valor de tipología).</summary>
        private static decimal? ParseOptionalThreshold(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            var normalized = rawValue.Trim().Replace(" ", string.Empty);
            if (normalized.Contains(',') && normalized.Contains('.'))
            {
                normalized = normalized.LastIndexOf(',') > normalized.LastIndexOf('.')
                    ? normalized.Replace(".", string.Empty).Replace(',', '.')
                    : normalized.Replace(",", string.Empty);
            }
            else
            {
                normalized = normalized.Replace(',', '.');
            }

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                && parsed >= 0m && parsed <= 1m)
                return parsed;

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedCurrent)
                && parsedCurrent >= 0m && parsedCurrent <= 1m)
                return parsedCurrent;

            return null;
        }

        private static decimal ParseThresholdOrDefault(string? rawValue, decimal defaultValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue;
            }

            var normalized = rawValue.Trim().Replace(" ", string.Empty);

            // Normalizar separadores para evitar interpretaciones distintas por cultura
            // (ej. "0.5" en es-ES puede interpretarse como 5).
            if (normalized.Contains(',') && normalized.Contains('.'))
            {
                if (normalized.LastIndexOf(',') > normalized.LastIndexOf('.'))
                {
                    normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
                }
                else
                {
                    normalized = normalized.Replace(",", string.Empty);
                }
            }
            else
            {
                normalized = normalized.Replace(',', '.');
            }

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantParsed))
            {
                return invariantParsed >= 0m && invariantParsed <= 1m
                    ? invariantParsed
                    : defaultValue;
            }

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentParsed))
            {
                return currentParsed >= 0m && currentParsed <= 1m
                    ? currentParsed
                    : defaultValue;
            }

            return defaultValue;
        }

        private static string? NormalizeOptionalText(string? value, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return value.Trim();
        }

        private async Task PollStatusAsync(string statusUri)
        {
            _pollingCts = new CancellationTokenSource();
            var maxRetries = 60; // 2-5 minutes depending on interval
            var retryCount = 0;
            var pollIntervalMs = 2000; // 2 seconds

            ProcessingStatus? lastStatus = null;

            while (retryCount < maxRetries && !_pollingCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(pollIntervalMs, _pollingCts.Token);
                    retryCount++;

                    lastStatus = await _apiClient.GetStatusAsync(statusUri);

                    UpdateActivityStatuses(lastStatus);

                    if (lastStatus.RuntimeStatus == "Completed" || lastStatus.RuntimeStatus == "Failed")
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    var errorMessage = ex.InnerException?.Message ?? ex.Message;
                    ActivityLogs.Add(new ActivityLogEntry($"Poll #{retryCount}", ActivityStatusEnum.Failed, message: errorMessage));
                }
            }

            if (lastStatus?.RuntimeStatus == "Completed")
            {
                ActivityLogs.Add(new ActivityLogEntry("Completado", ActivityStatusEnum.Completed, message: "Procesamiento exitoso ✓"));
                OutputJson = lastStatus.Output;
                if (lastStatus.Output != null)
                {
                    PopulateActivityMessagesFromOutput(lastStatus.Output);
                }
                MarkActivitiesComplete();
            }
            else if (lastStatus?.RuntimeStatus == "Failed")
            {
                ActivityLogs.Add(new ActivityLogEntry("Error", ActivityStatusEnum.Failed, message: "Procesamiento fallido ✗"));
            }
            else
            {
                ActivityLogs.Add(new ActivityLogEntry("Timeout", ActivityStatusEnum.Failed, message: "Timeout esperando resultado"));
            }
        }

        private void UpdateActivityStatuses(ProcessingStatus status)
        {
            try
            {
                if (status?.CustomStatus == null) return;

                var customStatus = status.CustomStatus;

                // Extract timeline array  from JObject
                var timeline = new List<ActivityTimeline>();
                
                // Try to get actividades or Actividades from JObject
                var timelineData = customStatus["actividades"] ?? customStatus["Actividades"];
                if (timelineData != null && timelineData.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    foreach (var item in timelineData.Children<JObject>())
                    {
                        var activity = item.ToObject<ActivityTimeline>();
                        if (activity != null)
                        {
                            timeline.Add(activity);
                        }
                    }
                }

                // Extract current activity
                var currentActivityValue = customStatus["actividadActual"]?.Value<string>() ??
                                          customStatus["ActividadActual"]?.Value<string>();
                var currentCanonical = CanonicalizeActivityName(currentActivityValue);

                // Ensure all activities seen in runtime are visible in timeline.
                if (!string.IsNullOrWhiteSpace(currentCanonical))
                {
                    EnsureActivityExists(currentCanonical);
                }

                foreach (var timelineEntry in timeline)
                {
                    var timelineName = CanonicalizeActivityName(timelineEntry.GetName());
                    EnsureActivityExists(timelineName);
                }

                // Update all activities based on timeline
                foreach (var activity in ActivityStatuses)
                {
                    var timelineEntry = timeline.FirstOrDefault(t =>
                        CanonicalizeActivityName(t.GetName()).Equals(activity.Name, StringComparison.OrdinalIgnoreCase));

                    if (timelineEntry != null)
                    {
                        activity.DurationMs = timelineEntry.GetDuration();
                        var timelineMessage = timelineEntry.GetMessage();
                        if (!string.IsNullOrWhiteSpace(timelineMessage))
                        {
                            activity.Message = timelineMessage;
                        }

                        var state = NormalizeState(timelineEntry.GetState());
                        if (state == "completed")
                        {
                            activity.Status = ActivityStatusEnum.Completed;
                        }
                        else if (state == "failed")
                        {
                            activity.Status = ActivityStatusEnum.Failed;
                        }
                        else if (state == "skipped")
                        {
                            activity.Status = ActivityStatusEnum.Skipped;
                        }
                        else if (state == "running" || activity.Name.Equals(currentCanonical, StringComparison.OrdinalIgnoreCase))
                        {
                            activity.Status = ActivityStatusEnum.Running;
                        }
                        else if (activity.Status == ActivityStatusEnum.Running)
                        {
                            // Prevent stale blinking when runtime advances or skips steps.
                            activity.Status = ActivityStatusEnum.Pending;
                        }
                    }
                    else if (activity.Name.Equals(currentCanonical, StringComparison.OrdinalIgnoreCase))
                    {
                        activity.Status = ActivityStatusEnum.Running;
                    }
                    else if (activity.Status == ActivityStatusEnum.Running)
                    {
                        activity.Status = ActivityStatusEnum.Pending;
                    }
                }

                // Log activity transition only once per stage.
                if (!string.IsNullOrWhiteSpace(currentCanonical) &&
                    !currentCanonical.Equals(_lastLoggedActivity, StringComparison.OrdinalIgnoreCase))
                {
                    ActivityLogs.Add(new ActivityLogEntry($"Actividad: {currentCanonical}", ActivityStatusEnum.Running));
                    _lastLoggedActivity = currentCanonical;
                }
            }
            catch { /* ignore logging errors */ }
        }

        private void EnsureActivityExists(string activityName)
        {
            if (string.IsNullOrWhiteSpace(activityName))
            {
                return;
            }

            if (ActivityStatuses.Any(a => a.Name.Equals(activityName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // Keep "Resultado" as the visual terminal node in timeline.
            var resultIndex = ActivityStatuses.ToList().FindIndex(a => a.Name.Equals("Resultado", StringComparison.OrdinalIgnoreCase));
            if (resultIndex >= 0 && !activityName.Equals("Resultado", StringComparison.OrdinalIgnoreCase))
            {
                ActivityStatuses.Insert(resultIndex, new ActivityStatus(activityName));
            }
            else
            {
                ActivityStatuses.Add(new ActivityStatus(activityName));
            }

            UpdateLastActivityFlags();
        }

        private static string NormalizeState(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return string.Empty;
            }

            var normalized = RemoveDiacritics(state).Trim().ToLowerInvariant();
            return normalized switch
            {
                "completed" => "completed",
                "completado" => "completed",
                "failed" => "failed",
                "error" => "failed",
                "running" => "running",
                "inprogress" => "running",
                "enproceso" => "running",
                "skipped" => "skipped",
                "omitido" => "skipped",
                "omitted" => "skipped",
                _ => normalized
            };
        }

        private static string CanonicalizeActivityName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var normalized = RemoveDiacritics(name).Trim().ToLowerInvariant();
            normalized = normalized.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

            return normalized switch
            {
                "prompt" => "Prompt",
                "clasificar" => "Clasificar",
                "clasificacion" => "Clasificar",
                "extraer" => "Extraer",
                "extraccion" => "Extraer",
                "validar" => "Validar",
                "validacion" => "Validar",
                "integrar" => "Integrar",
                "normalizacion" => "Integrar",
                "subirgdc" => "SubirGDC",
                "uploadgdc" => "SubirGDC",
                "persistir" => "Persistir",
                "persistencia" => "Persistir",
                "resultado" => "Resultado",
                _ => name.Trim()
            };
        }

        private static string RemoveDiacritics(string? text)
        {
            text ??= string.Empty;
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private void MarkActivitiesComplete()
        {
            foreach (var activity in ActivityStatuses)
            {
                if (activity.Status == ActivityStatusEnum.Pending || activity.Status == ActivityStatusEnum.Running)
                {
                    activity.Status = ActivityStatusEnum.Completed;
                }
            }
        }

        private void PopulateActivityMessagesFromOutput(JObject output)
        {
            if (output == null)
            {
                return;
            }

            foreach (var activity in ActivityStatuses)
            {
                activity.DetailRows.Clear();

                foreach (var row in BuildActivityDetailRows(activity.Name, output))
                {
                    activity.DetailRows.Add(row);
                }

                var summary = BuildActivityOutputSummary(activity.Name, output);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    activity.Message = summary;
                }
            }
        }

        private string BuildActivityOutputSummary(string activityName, JObject output)
        {
            var canonical = CanonicalizeActivityName(activityName);
            var detalle = FindPropertyIgnoreCase(output, "DetalleEjecucion") as JObject;

            return canonical switch
            {
                "Clasificar" => FirstNonEmptySummary(
                    BuildCompactSummary(FindPropertyIgnoreCase(output, "Identificacion") ?? FindPropertyIgnoreCase(detalle, "Clasificacion")),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "Extraer" => FirstNonEmptySummary(
                    BuildCompactSummary(FindPropertyIgnoreCase(detalle, "Extraccion")),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "Validar" => FirstNonEmptySummary(
                    CombineSummaries(
                        BuildCompactSummary(FindPropertyIgnoreCase(detalle, "Validacion")),
                        BuildCompactSummary(FindPropertyIgnoreCase(detalle, "PostProceso") ?? FindPropertyIgnoreCase(output, "PostProceso"))),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "Integrar" => FirstNonEmptySummary(
                    BuildCompactSummary(FindPropertyIgnoreCase(detalle, "Integracion")),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "SubirGDC" => FirstNonEmptySummary(
                    BuildCompactSummary(FindPropertyIgnoreCase(detalle, "SubidaGDC") ?? FindPropertyIgnoreCase(detalle, "GDC")),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "Persistir" => FirstNonEmptySummary(
                    BuildCompactSummary(FindPropertyIgnoreCase(detalle, "Persistencia")),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "Prompt" => FirstNonEmptySummary(
                    BuildCompactSummary(FindPropertyIgnoreCase(detalle, "Prompt")),
                    BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))),
                "Resultado" => BuildCompactSummary(FindPropertyIgnoreCase(output, "Resultado")),
                _ => BuildCompactSummary(FindTrackingActivitySummary(detalle, canonical))
            };
        }

        private static string CombineSummaries(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second;
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first;
            }

            return $"{first}\n\nPostProceso: {second}";
        }

        private static string FirstNonEmptySummary(params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private IEnumerable<ActivityDetailRow> BuildActivityDetailRows(string activityName, JObject output)
        {
            var canonical = CanonicalizeActivityName(activityName);
            var detalle = FindPropertyIgnoreCase(output, "DetalleEjecucion") as JObject;

            return canonical switch
            {
                "Clasificar" => BuildRowsFromToken(
                    FindPropertyIgnoreCase(output, "Identificacion") ??
                    FindPropertyIgnoreCase(detalle, "Clasificacion"),
                    "Clasificacion"),
                "Extraer" => BuildRowsFromToken(FindPropertyIgnoreCase(detalle, "Extraccion"), "Extraccion"),
                "Validar" => BuildRowsForValidation(detalle, output),
                "Integrar" => BuildRowsFromToken(FindPropertyIgnoreCase(detalle, "Integracion"), "Integracion"),
                "SubirGDC" => BuildRowsFromToken(
                    FindPropertyIgnoreCase(detalle, "SubidaGDC") ??
                    FindPropertyIgnoreCase(detalle, "GDC"),
                    "SubirGDC"),
                "Persistir" => BuildRowsFromToken(FindPropertyIgnoreCase(detalle, "Persistencia"), "Persistencia"),
                "Prompt" => BuildRowsFromToken(FindPropertyIgnoreCase(detalle, "Prompt"), "Prompt"),
                "Resultado" => BuildResultDetailRows(output),
                _ => BuildRowsFromToken(FindTrackingActivitySummary(detalle, canonical), activityName)
            };
        }

        private IEnumerable<ActivityDetailRow> BuildRowsForValidation(JObject? detalle, JObject output)
        {
            var rows = new List<ActivityDetailRow>();

            AddRowsFromToken(rows, FindPropertyIgnoreCase(detalle, "Validacion"), "Validacion");
            AddRowsFromToken(
                rows,
                FindPropertyIgnoreCase(detalle, "PostProceso") ?? FindPropertyIgnoreCase(output, "PostProceso"),
                "PostProceso");

            if (rows.Count == 0)
            {
                rows.Add(new ActivityDetailRow
                {
                    Key = "Validacion",
                    Value = "Sin detalle disponible"
                });
            }

            return rows;
        }

        private IEnumerable<ActivityDetailRow> BuildRowsFromToken(JToken? token, string rootLabel)
        {
            var rows = new List<ActivityDetailRow>();

            AddRowsFromToken(rows, token, rootLabel);

            if (rows.Count == 0)
            {
                rows.Add(new ActivityDetailRow
                {
                    Key = rootLabel,
                    Value = "Sin detalle disponible"
                });
            }

            return rows;
        }

        private IEnumerable<ActivityDetailRow> BuildResultDetailRows(JObject output)
        {
            var rows = new List<ActivityDetailRow>();

            var resultado = FindPropertyIgnoreCase(output, "Resultado");
            AddRowsFromToken(rows, resultado, "Resultado");

            if (rows.Count == 0)
            {
                rows.Add(new ActivityDetailRow
                {
                    Key = "Resultado",
                    Value = "No se encontro la seccion Resultado en la salida"
                });
            }

            return rows;
        }

        private static void AddRowsFromToken(ICollection<ActivityDetailRow> rows, JToken? token, string prefix, int depth = 0)
        {
            if (token == null || rows.Count >= 16)
            {
                return;
            }

            if (depth >= 3)
            {
                rows.Add(new ActivityDetailRow
                {
                    Key = prefix,
                    Value = BuildCompactSummary(token)
                });
                return;
            }

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (rows.Count >= 16)
                    {
                        break;
                    }

                    var childKey = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    AddRowsFromToken(rows, property.Value, childKey, depth + 1);
                }
                return;
            }

            if (token is JArray array)
            {
                if (array.Count == 0)
                {
                    rows.Add(new ActivityDetailRow
                    {
                        Key = prefix,
                        Value = "[]"
                    });
                    return;
                }

                if (array.All(item => item is JValue))
                {
                    rows.Add(new ActivityDetailRow
                    {
                        Key = prefix,
                        Value = string.Join(", ", array.Select(item => BuildCompactSummary(item)))
                    });
                    return;
                }

                rows.Add(new ActivityDetailRow
                {
                    Key = prefix,
                    Value = $"[{array.Count} elementos]"
                });

                var maxChildren = Math.Min(array.Count, 3);
                for (var i = 0; i < maxChildren && rows.Count < 16; i++)
                {
                    AddRowsFromToken(rows, array[i], $"{prefix}[{i}]", depth + 1);
                }

                return;
            }

            rows.Add(new ActivityDetailRow
            {
                Key = prefix,
                Value = BuildCompactSummary(token)
            });
        }

        private void UpdateLastActivityFlags()
        {
            for (var i = 0; i < ActivityStatuses.Count; i++)
            {
                ActivityStatuses[i].IsLast = i == ActivityStatuses.Count - 1;
            }
        }

        private JToken? FindTrackingActivitySummary(JObject? detalle, string canonical)
        {
            var seguimiento = FindPropertyIgnoreCase(detalle, "Seguimiento") as JObject;
            var actividades = FindPropertyIgnoreCase(seguimiento, "Actividades") as JArray;
            if (actividades == null)
            {
                return null;
            }

            foreach (var token in actividades.OfType<JObject>())
            {
                var nombre = token["Nombre"]?.Value<string>() ?? token["nombre"]?.Value<string>();
                if (CanonicalizeActivityName(nombre).Equals(canonical, StringComparison.OrdinalIgnoreCase))
                {
                    return token;
                }
            }

            return null;
        }

        private static JToken? FindPropertyIgnoreCase(JToken? token, string propertyName)
        {
            if (token is not JObject obj || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var prop = obj.Properties().FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            return prop?.Value;
        }

        private static string BuildCompactSummary(JToken? token)
        {
            if (token == null)
            {
                return string.Empty;
            }

            var raw = token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Newtonsoft.Json.Formatting.None);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return raw.Length <= 380 ? raw : raw.Substring(0, 377) + "...";
        }
    }

    public class NotifyPropertyChanged : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
