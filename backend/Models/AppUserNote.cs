namespace admgmt_backend.Models
{
    public class AppUserNote
    {
        public long Id { get; set; }
        public string SamAccountName { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
