using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace admgmt_backend.Services
{
    public class AdAuthService : IAuthService
    {
        private readonly IConfiguration _cfg;
        public AdAuthService(IConfiguration cfg) { _cfg = cfg; }

        private PrincipalContext CreateContext()
        {
            // نستخدم حساب خدمي للاستعلام عن خصائص المستخدم بعد التحقق
            var domain = _cfg["AD:Domain"];
            var user = _cfg["AD:ServiceUserUPN"];
            var pass = _cfg["AD:ServicePassword"];

            // تقدر تحدد Container أو Server إذا حبيت (ليس إلزامي)
            var container = _cfg["AD:DefaultContainer"];
            var server = _cfg["AD:LdapServer"];
            var ctx = string.IsNullOrWhiteSpace(server)
                ? (!string.IsNullOrWhiteSpace(container)
                    ? new PrincipalContext(ContextType.Domain, domain, container, user, pass)
                    : new PrincipalContext(ContextType.Domain, domain, user, pass))
                : (!string.IsNullOrWhiteSpace(container)
                    ? new PrincipalContext(ContextType.Domain, server, container, ContextOptions.Negotiate, user, pass)
                    : new PrincipalContext(ContextType.Domain, server, null, ContextOptions.Negotiate, user, pass));
            return ctx;
        }

        public async Task<ClaimsIdentity?> ValidateUserAsync(string username, string password)
        {
            return await Task.Run(() =>
            {
                // ملاحظة: username يُفضّل أن يكون بصيغة UPN (user@domain) أو SAM (DOMAIN\\user)
                using var ctx = CreateContext();

                // تحقق من صحة بيانات الاعتماد
                bool ok;
                try
                {
                    ok = ctx.ValidateCredentials(username, password, ContextOptions.Negotiate);
                }
                catch
                {
                    // مشاكل شبكة/شهادة/LDAP—نرجّع فشل
                    return null;
                }

                if (!ok) return null;

                // جلب كائن المستخدم من AD
                var up = UserPrincipal.FindByIdentity(ctx, IdentityType.UserPrincipalName, username)
                         ?? UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, username);

                if (up == null) return null;

                // بناء الـ Claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, up.SamAccountName ?? username),
                    new Claim(ClaimTypes.Upn, up.UserPrincipalName ?? username),
                    new Claim(ClaimTypes.GivenName, up.GivenName ?? string.Empty),
                    new Claim(ClaimTypes.Surname, up.Surname ?? string.Empty),
                    new Claim(ClaimTypes.Email, up.EmailAddress ?? string.Empty),
                    new Claim("displayName", up.DisplayName ?? string.Empty),
                };

                // جلب المجموعات كـ Claims (قد تحتاج صلاحيات/اتصال GC)
                try
                {
                    var groups = up.GetAuthorizationGroups();
                    foreach (var g in groups.OfType<GroupPrincipal>())
                    {
                        var name = g.SamAccountName ?? g.Name;
                        if (!string.IsNullOrWhiteSpace(name))
                            claims.Add(new Claim(ClaimTypes.Role, name));
                    }
                }
                catch
                {
                    // تجاهل أخطاء جلب المجموعات (أحياناً بسبب Trusted Domains/GC)
                }

                return new ClaimsIdentity(claims, "AD");
            });
        }
    }
}
