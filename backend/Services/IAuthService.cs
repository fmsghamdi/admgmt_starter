using System.Security.Claims;

namespace admgmt_backend.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// يتحقق من اسم المستخدم/كلمة المرور ضد AD.
        /// عند النجاح يرجع Claims (اسم، إيميل، مجموعات، الخ).
        /// يرجع null إذا فشل التحقق.
        /// </summary>
        Task<ClaimsIdentity?> ValidateUserAsync(string username, string password);
    }
}
