using admgmt_backend.ViewModels;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace admgmt_backend.Services
{
    // ناتج مقسّم للواجهات (يتوافق مع { items, total } في الواجهة)
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
    }

    // خيارات البحث المتقدّم عن المستخدمين
    public class UsersQueryOptions
    {
        // نص البحث (displayName / sAMAccountName / mail)
        public string? Q { get; set; }

        // فلتر الحالة: "any" | "enabled" | "disabled" | "locked"
        public string? Status { get; set; } = "any";

        // تقييد بـ OU محدّد (DistinguishedName)
        public string? OuDn { get; set; }

        // تقسيم الصفحات
        public int Take { get; set; } = 100;
        public int Skip { get; set; } = 0;

        // ترتيب اختياري (غير مستعمل حالياً)
        public string? Sort { get; set; }
    }

    public interface IADService
    {
        // ===== Users (قديم) =====
        Task<List<ADUserVm>> GetUsersAsync(string? search = null, int take = 100, int skip = 0);
        Task<ADUserVm?> GetUserAsync(string samAccountName);
        Task<bool> SetUserEnabledAsync(string samAccountName, bool enabled);
        Task<bool> ResetPasswordAsync(string samAccountName, string newPassword);
        Task<bool> ResetPasswordWithOptionsAsync(string samAccountName, string newPassword, bool forceChangeAtNextLogon, bool unlockIfLocked);
        Task<bool> UnlockUserAsync(string samAccountName);

        // ===== Users (جديد) =====
        Task<PagedResult<ADUserVm>> GetUsersAdvancedAsync(UsersQueryOptions options);
        Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? dn);

        // ===== Groups =====
        Task<List<ADGroupVm>> GetGroupsAsync(string? search = null, int take = 100, int skip = 0);
        Task<bool> AddUserToGroupAsync(string samAccountName, string groupName);
        Task<bool> RemoveUserFromGroupAsync(string samAccountName, string groupName);

        // ===== OUs =====
        Task<List<OUVm>> GetOUsAsync(string? baseDn = null, int take = 500, int skip = 0);
        Task<List<OUVm>> GetChildOUsAsync(string? parentDn = null);

        // Objects under OU + تفاصيل أي كائن
        Task<List<ADObjectVm>> GetOuObjectsAsync(string ouDn, int take = 200, int skip = 0, string? search = null);
        Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn);

        // Mutations على الـ OU
        Task<bool> CreateOUAsync(string parentDn, string name, string? description = null);
        Task<bool> RenameOUAsync(string dn, string newName);
        Task<bool> DeleteOUAsync(string dn);
        Task<bool> MoveObjectAsync(string objectDn, string targetOuDn);
        Task<bool> MoveUserBySamAsync(string samAccountName, string targetOuDn);
    }
}
