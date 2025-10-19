namespace admgmt_backend.ViewModels
{
    public class ADUserVm
    {
    
        public string SamAccountName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string? SAM { get; set; }
        public string? DistinguishedName { get; set; }
        public DateTime? LastLogonUtc { get; set; }
    }
}
