namespace admgmt_backend.ViewModels
{
    public enum UserStatusFilter { Any, Enabled, Disabled, Locked }

    public class UsersQueryOptions
    {
        public string? Q { get; set; }                 // نص البحث العام
        public string? OuDn { get; set; }              // OU DN اختياري
        public UserStatusFilter Status { get; set; } = UserStatusFilter.Any;

        public int Take { get; set; } = 100;
        public int Skip { get; set; } = 0;

        // فرز (اختياري)
        public string? SortBy { get; set; }    // displayName | sam | email | lastLogon
        public bool Desc { get; set; }
    }
}
