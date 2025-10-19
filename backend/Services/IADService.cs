using admgmt_backend.ViewModels;

namespace admgmt_backend.Services
{
    public interface IADService
    {
        // موجود سابقًا:
        Task<List<ADUserVm>> GetUsersAsync(string? search = null, int take = 100, int skip = 0);
        Task<ADUserVm?> GetUserAsync(string samAccountName);
        Task<bool> SetUserEnabledAsync(string samAccountName, bool enabled);
        Task<bool> ResetPasswordAsync(string samAccountName, string newPassword);
        Task<bool> ResetPasswordWithOptionsAsync(string samAccountName, string newPassword, bool forceChangeAtNextLogon, bool unlockIfLocked);
        Task<bool> UnlockUserAsync(string samAccountName);

        Task<List<ADGroupVm>> GetGroupsAsync(string? search = null, int take = 100, int skip = 0);
        Task<bool> AddUserToGroupAsync(string samAccountName, string groupName);
        Task<bool> RemoveUserFromGroupAsync(string samAccountName, string groupName);

        Task<List<OUVm>> GetOUsAsync(string? baseDn = null, int take = 500, int skip = 0);
        Task<List<OUVm>> GetChildOUsAsync(string? parentDn = null);
        Task<List<ADObjectVm>> GetOuObjectsAsync(string ouDn, int take = 200, int skip = 0, string? search = null);
        Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn);

        Task<bool> CreateOUAsync(string parentDn, string name, string? description = null);
        Task<bool> RenameOUAsync(string dn, string newName);
        Task<bool> DeleteOUAsync(string dn);
        Task<bool> MoveObjectAsync(string objectDn, string targetOuDn);
        Task<bool> MoveUserBySamAsync(string samAccountName, string targetOuDn);

        // الجديد:
        Task<(List<ADUserVm> Items, int Total)> GetUsersAdvancedAsync(UsersQueryOptions opts);
        Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam = null, string? dn = null);
    }
}
