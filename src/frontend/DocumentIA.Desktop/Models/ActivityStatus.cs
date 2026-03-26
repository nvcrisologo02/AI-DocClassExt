namespace DocumentIA.Desktop.Models
{
    public enum ActivityStatusEnum
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public class ActivityStatus
    {
        public string Name { get; set; }
        public ActivityStatusEnum Status { get; set; }
        public long? DurationMs { get; set; }
        public string Message { get; set; }

        public ActivityStatus(string name)
        {
            Name = name;
            Status = ActivityStatusEnum.Pending;
            DurationMs = null;
            Message = null;
        }
    }
}
