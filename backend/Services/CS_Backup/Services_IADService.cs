using System.Collections.Generic;
using System.Threading.Tasks;

namespace admgmt_backend.Services
{
    public interface IADService
    {
        // OUs
        Task<List<OUVm>> GetRootOusAsync();
        Task<List<OUVm>> GetOUsAsync(string? q, int skip, int take);
        Task<List<OUVm>> GetChildOUsAsync(string? parentDn);
        Task<List<ADObjectVm>> GetOuObjectsAsync(string dn, int skip, int take, string? q);
        Task<bool> CreateOUAsync(string parentDn, string name, string? description);
        Task<bool> DeleteOUAsync(string dn);
        Task<bool> RenameOUAsync(string dn, string newName);
        Task<bool> MoveObjectAsync(string dn, string newParentDn);
        Task<bool> MoveUserBySamAsync(string sam, string newParentDn);

        // Users
        Task<List<ADUserVm>> GetUsersAsync(string? q, int skip, int take);
        Task<(List<ADUserVm> Items, int Total)> GetUsersAdvancedAsync(UsersQueryOptions options);
        Task<ADUserVm?> GetUserAsync(string sam);
        Task<bool> SetUserEnabledAsync(string sam, bool enabled);
        Task<bool> ResetPasswordWithOptionsAsync(string sam, string newPassword, bool forceChange, bool unlock);
        Task<bool> ResetPasswordAsync(string sam, string newPassword);
        Task<bool> UnlockUserAsync(string sam);

        // Details
        Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn);
        Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? dn);

        // Groups
        Task<List<ADGroupVm>> GetGroupsAsync(string? q, int skip, int take);
        Task<List<ADUserVm>> GetGroupMembersAsync(string groupSam);
        Task<bool> AddUserToGroupAsync(string userSam, string groupSam);
        Task<bool> RemoveUserFromGroupAsync(string userSam, string groupSam);
    }
}