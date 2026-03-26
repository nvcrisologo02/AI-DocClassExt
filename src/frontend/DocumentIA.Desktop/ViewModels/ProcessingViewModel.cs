using DocumentIA.Desktop.Models;
using DocumentIA.Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DocumentIA.Desktop.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
    }

    public class ProcessingViewModel : NotifyPropertyChanged
    {
        private readonly IOrchestratorApiClient _apiClient;
        private CancellationTokenSource _pollingCts;

        public ObservableCollection<ActivityStatus> ActivityStatuses { get; }
        public ObservableCollection<ActivityLogEntry> ActivityLogs { get; }

        private string _selectedDocumentPath;
        public string SelectedDocumentPath
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

        private string _selectedClassificationType = "auto"; // "nota-simple@1.4", "resumen-documental", "auto"
        public string SelectedClassificationType
        {
            get => _selectedClassificationType;
            set
            {
                if (_selectedClassificationType != value)
                {
                    _selectedClassificationType = value;
                    OnPropertyChanged(nameof(SelectedClassificationType));
                }
            }
        }

        private string _instanceId;
        public string InstanceId
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

        private string _correlationId;
        public string CorrelationId
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

        private object _outputJson;
        public object OutputJson
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
                }
            }
        }

        private string _apiStatusMessage;
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

        public bool IsExecuteEnabled => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedDocumentPath) && IsApiConnected;

        public ICommand ExecuteCommand { get; }
        public ICommand SelectFileCommand { get; }

        public ProcessingViewModel()
        {
            _apiClient = new OrchestratorApiClient("http://localhost:7071");
            ActivityStatuses = new ObservableCollection<ActivityStatus>();
            ActivityLogs = new ObservableCollection<ActivityLogEntry>();
            
            ExecuteCommand = new RelayCommand(_ => ExecuteAsync().ConfigureAwait(false), _ => IsExecuteEnabled);
            SelectFileCommand = new RelayCommand(_ => SelectFile());

            InitializeActivities();
            CheckApiConnectionAsync().ConfigureAwait(false);
        }

        private void InitializeActivities()
        {
            ActivityStatuses.Clear();
            var activities = new[] { "Clasificación", "Extracción", "Normalización", "Validación", "Persistencia" };
            foreach (var activity in activities)
            {
                ActivityStatuses.Add(new ActivityStatus(activity));
            }
        }

        private async Task CheckApiConnectionAsync()
        {
            try
            {
                IsApiConnected = await _apiClient.CheckConnectionAsync();
                ApiStatusMessage = IsApiConnected ? "API Conectada ✓" : "API No Disponible ✗";
            }
            catch
            {
                IsApiConnected = false;
                ApiStatusMessage = "Error al conectar con API ✗";
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
                CorrelationId = request.Traceability.CorrelationId;

                ActivityLogs.Add(new ActivityLogEntry("Lectura", ActivityStatusEnum.Completed, message: $"{documentName}"));
                ActivityLogs.Add(new ActivityLogEntry("Envío", ActivityStatusEnum.Running, message: "Enviando a servidor..."));

                // Send request
                var response = await _apiClient.IngestDocumentAsync(request);
                InstanceId = response.InstanceId;

                ActivityLogs.Add(new ActivityLogEntry("Envío", ActivityStatusEnum.Completed, message: $"Instance: {response.InstanceId}"));
                ActivityLogs.Add(new ActivityLogEntry("Procesamiento", ActivityStatusEnum.Running, message: "Esperando resultado..."));

                // Poll status
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
            var expectedType = SelectedClassificationType switch
            {
                "nota-simple@1.4" => "nota-simple@1.4",
                "resumen-documental" => "resumen-documental",
                _ => null // auto
            };

            return new ProcessingRequest
            {
                Traceability = new Traceability
                {
                    CorrelationId = $"DESKTOP-{SelectedClassificationType}-{DateTime.Now:yyyyMMdd-HHmmss}",
                    SubmittedBy = "usuario.desktop@sareb.es",
                    IdGdc = null,
                    IdActivo = "DESKTOP-TEST"
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
                    SkipDuplicateCheck = true,
                    ForceReprocess = true,
                    SkipGDCUpload = true,
                    Classification = new ClassificationSettings
                    {
                        Provider = expectedType == null ? "auto" : null,
                        Model = "auto",
                        Threshold = 0.5m
                    },
                    Extraction = new ExtractionSettings
                    {
                        Model = "auto",
                        Threshold = 0.80m
                    }
                }
            };
        }

        private async Task PollStatusAsync(string statusUri)
        {
            _pollingCts = new CancellationTokenSource();
            var maxRetries = 60; // 2-5 minutes depending on interval
            var retryCount = 0;
            var pollIntervalMs = 2000; // 2 seconds

            ProcessingStatus lastStatus = null;

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
                    ActivityLogs.Add(new ActivityLogEntry($"Poll #{retryCount}", ActivityStatusEnum.Failed, message: ex.Message));
                }
            }

            if (lastStatus?.RuntimeStatus == "Completed")
            {
                ActivityLogs.Add(new ActivityLogEntry("Completado", ActivityStatusEnum.Completed, message: "Procesamiento exitoso ✓"));
                OutputJson = lastStatus.Output;
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
                var customStatus = status.CustomStatus;
                if (customStatus == null) return;

                // Get current activity
                var currentActivity = customStatus.CurrentActivity ?? customStatus.CurrentActivityAlt;
                
                // Get timeline
                var timeline = customStatus.ActivityTimeline ?? customStatus.ActivityTimelineAlt ?? new System.Collections.Generic.List<ActivityTimeline>();

                // Update all activities based on timeline
                foreach (var activity in ActivityStatuses)
                {
                    var timelineEntry = timeline.FirstOrDefault(t => t.GetName().Equals(activity.Name, StringComparison.OrdinalIgnoreCase));

                    if (timelineEntry != null)
                    {
                        activity.DurationMs = timelineEntry.GetDuration();
                        
                        var state = timelineEntry.GetState();
                        if (state == "Completed")
                        {
                            activity.Status = ActivityStatusEnum.Completed;
                        }
                        else if (state == "Failed")
                        {
                            activity.Status = ActivityStatusEnum.Failed;
                        }
                        else if (activity.Name.Equals(currentActivity, StringComparison.OrdinalIgnoreCase))
                        {
                            activity.Status = ActivityStatusEnum.Running;
                        }
                    }
                    else if (activity.Name.Equals(currentActivity, StringComparison.OrdinalIgnoreCase))
                    {
                        activity.Status = ActivityStatusEnum.Running;
                    }
                }

                // Log progress
                ActivityLogs.Add(new ActivityLogEntry($"Actividad: {currentActivity}", ActivityStatusEnum.Running));
            }
            catch { /* ignore logging errors */ }
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
    }

    public class NotifyPropertyChanged : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
