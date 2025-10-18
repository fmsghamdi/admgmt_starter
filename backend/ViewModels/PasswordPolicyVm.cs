namespace admgmt_backend.ViewModels
{
    public class PasswordPolicyVm
    {
        public int MinLength { get; set; } = 8;
        public bool RequireUpper { get; set; } = true;
        public bool RequireLower { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecial { get; set; } = true;
        public bool ForceChangeOnResetDefault { get; set; } = true;

        // معلوماتية فقط (سياسة القفل الفعلية تدار من الـ GPO)
        public int LockoutThreshold { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;
    }
}
