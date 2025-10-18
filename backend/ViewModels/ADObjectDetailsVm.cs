namespace admgmt_backend.ViewModels
{
    public class ADObjectDetailsVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string? SamAccountName { get; set; }
        public string ObjectClass { get; set; } = "";
        public string? Email { get; set; }

        public bool? Enabled { get; set; }
        public bool? Locked { get; set; }
        public DateTime? LastLogonUtc { get; set; }

        // أهم تغيير هنا: نخليها تقبل أي نوع (تواريخ/قوائم/نصوص...)
        public Dictionary<string, object?> Extra { get; set; } = new();
    }
}
