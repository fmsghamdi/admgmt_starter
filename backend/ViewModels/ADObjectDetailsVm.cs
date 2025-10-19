using System.Text.Json.Serialization;

namespace admgmt_backend.ViewModels
{
    public class ADObjectDetailsVm
    {
        public string Name { get; set; } = "";
         public string DisplayName { get; set; } = "";
         public string Type { get; set; } = "";
         public Dictionary<string, string?> Properties { get; set; } = new();
        public string DistinguishedName { get; set; } = "";
        public string? SamAccountName { get; set; }
        public string ObjectClass { get; set; } = "other";
        public string? Email { get; set; }
        public bool? Enabled { get; set; }
        public bool? Locked { get; set; }
        public DateTime? LastLogonUtc { get; set; }

        // مهم: Extra تقبل أي نوع (تواريخ، قوائم…)
        public Dictionary<string, object?> Extra { get; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }
    }
}
