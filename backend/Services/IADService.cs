namespace admgmt_backend.Services
{
    public record OUVm(string Name, string DistinguishedName, bool HasChildren);
    public record ADUserVm(string SamAccountName, string UserPrincipalName, string DisplayName, bool Enabled, bool LockedOut);
    public record ADGroupVm(string Name, string DistinguishedName);
    public record ADObjectVm(string Name, string DistinguishedName, string ObjectClass);
    public class ADObjectDetailsVm
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DistinguishedName { get; set; } = string.Empty;
        public string? SamAccountName { get; set; }
        public string ObjectClass { get; set; } = "other";
        public string? Email { get; set; }
        public bool? Enabled { get; set; }
        public bool? Locked { get; set; }
        public DateTime? LastLogonUtc { get; set; }
        public Dictionary<string, List<string>> Attributes { get; set; } = new();
        public Dictionary<string, object?> Extra { get; } = new();
    }
    public class UsersQueryOptions
    {
        public string? Search { get; set; }
        public string? OuDistinguishedName { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public bool? Enabled { get; set; }
        public bool? Locked { get; set; }
    }

    public interface IADService
    {
        // OUs
        Task<List<OUVm>> GetOUsAsync(string? parentDn, int page, int pageSize);
        Task<List<OUVm>> GetChildOUsAsync(string? parentDn);
        Task<(List<ADObjectVm> Items, int Total)> GetOuObjectsAsync(string ouDn, int page, int pageSize);
        Task CreateOUAsync(string parentDn, string name);
        Task DeleteOUAsync(string dn);
        Task RenameOUAsync(string dn, string newName);
        Task MoveObjectAsync(string objectDn, string targetOuDn);
        Task MoveUserBySamAsync(string sam, string targetOuDn);

        // Generic object
        Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn);

        // Users
        Task<(List<ADUserVm> Items, int Total)> GetUsersAsync(string? search, int page, int pageSize);
        Task<(List<ADUserVm> Items, int Total)> GetUsersAdvancedAsync(UsersQueryOptions options);
        Task<ADUserVm?> GetUserAsync(string? samOrUpn);
        Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? upn);
        Task SetUserEnabledAsync(string sam, bool enabled);
        Task ResetPasswordWithOptionsAsync(string sam, string newPassword, bool unlock, bool mustChange);
        Task ResetPasswordAsync(string sam, string newPassword);
        Task UnlockUserAsync(string sam);

        // Groups
        Task<(List<ADGroupVm> Items, int Total)> GetGroupsAsync(string? search, int page, int pageSize);
        Task<List<ADUserVm>> GetGroupMembersAsync(string groupSamOrDn);
        Task AddUserToGroupAsync(string userSam, string groupSamOrDn);
        Task RemoveUserFromGroupAsync(string userSam, string groupSamOrDn);
    }
}