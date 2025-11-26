namespace TodoWpfApp.Models
{
    public class ReminderSettings
    {
        public bool RemindersEnabled { get; set; } = true;
        public int LeadTimeHours { get; set; } = 24;
        public string Theme { get; set; } = "Light";

        public static ReminderSettings CreateDefault() => new ReminderSettings();
    }
}
