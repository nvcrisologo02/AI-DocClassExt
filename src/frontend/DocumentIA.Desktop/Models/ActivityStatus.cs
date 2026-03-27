using System.ComponentModel;
using System.Collections.ObjectModel;

namespace DocumentIA.Desktop.Models
{
    public class ActivityDetailRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public enum ActivityStatusEnum
    {
        Pending,
        Running,
        Completed,
        Skipped,
        Failed
    }

    public class ActivityStatus : INotifyPropertyChanged
    {
        private string _name;
        private ActivityStatusEnum _status;
        private long? _durationMs;
        private string? _message;
        private bool _isLast;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public ActivityStatusEnum Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public long? DurationMs
        {
            get => _durationMs;
            set
            {
                if (_durationMs != value)
                {
                    _durationMs = value;
                    OnPropertyChanged(nameof(DurationMs));
                }
            }
        }

        public string? Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                }
            }
        }

        public bool IsLast
        {
            get => _isLast;
            set
            {
                if (_isLast != value)
                {
                    _isLast = value;
                    OnPropertyChanged(nameof(IsLast));
                }
            }
        }

        public ObservableCollection<ActivityDetailRow> DetailRows { get; }

        public ActivityStatus(string name)
        {
            _name = name;
            _status = ActivityStatusEnum.Pending;
            _durationMs = null;
            _message = null;
            _isLast = false;
            DetailRows = new ObservableCollection<ActivityDetailRow>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
