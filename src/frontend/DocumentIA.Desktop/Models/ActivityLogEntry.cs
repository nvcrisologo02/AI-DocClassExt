using System;

namespace DocumentIA.Desktop.Models
{
    public class ActivityLogEntry
    {
        public string ActivityName { get; set; }
        public ActivityStatusEnum Status { get; set; }
        public DateTime Timestamp { get; set; }
        public long? DurationMs { get; set; }
        public string? Message { get; set; }

        public ActivityLogEntry(string activityName, ActivityStatusEnum status, long? durationMs = null, string? message = null)
        {
            ActivityName = activityName;
            Status = status;
            DurationMs = durationMs;
            Message = message;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            var statusStr = Status switch
            {
                ActivityStatusEnum.Running => "▶",
                ActivityStatusEnum.Completed => "✓",
                ActivityStatusEnum.Skipped => "↷",
                ActivityStatusEnum.Failed => "✗",
                _ => "◯"
            };

            var durationStr = DurationMs.HasValue ? $"({DurationMs}ms)" : "";
            var messageStr = string.IsNullOrWhiteSpace(Message) ? "" : $" [{Message}]";

            return $"{statusStr} {ActivityName} {durationStr}{messageStr}";
        }
    }
}
