using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text.RegularExpressions;
using admgmt_backend.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace admgmt_backend.Services
{
    public class ADService : IADService
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<ADService> _logger;

        public ADService(IConfiguration cfg, ILogger<ADService> logger)
        {
            _cfg = cfg;
            _logger = logger;
        }

        // ===== Helpers =====
        private string GetHostFromConfig()
        {
            var path = _cfg["AD:LdapPath"] ?? "";
            var m = Regex.Match(path, @"^LDAPS?://([^/]+)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(path)) return path.Trim();
            return _cfg["AD:Domain"] ?? "";
        }

        private string BuildPath(string relative)
        {
            var useLdaps = (_cfg["AD:UseLdaps"] ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            var scheme = useLdaps ? "LDAPS" : "LDAP";
            var host = GetHostFromConfig();
            relative = relative?.TrimStart('/') ?? "";
            return $"{scheme}://{host}/{relative}";
        }

        private PrincipalContext CreateContext()
        {
            var user = _cfg["AD:ServiceUserUPN"];
            var pass = _cfg["AD:ServicePassword"];
            var host = GetHostFromConfig();
            _logger.LogInformation("CreateContext: Server={Server}, User={User}", host, user);
            return new PrincipalContext(ContextType.Domain, host, null, ContextOptions.Negotiate, user, pass);
        }

        private string GetDefaultNamingContext()
        {
            using var root = new DirectoryEntry(BuildPath("RootDSE"), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
            var dn = root.Properties["defaultNamingContext"]?.Value?.ToString();
            return string.IsNullOrWhiteSpace(dn) ? (_cfg["AD:DefaultContainer"] ?? "") : dn!;
        }

        private static string? GetParentDn(string dn)
        {
            var i = dn.IndexOf(',');
            return i > 0 ? dn[(i + 1)..] : null;
        }

        private DirectorySearcher MakeSearcher(string baseDn, SearchScope scope, string filter, string[] props)
        {
            var entry = new DirectoryEntry(
                BuildPath(baseDn),
                _cfg["AD:ServiceUserUPN"],
                _cfg["AD:ServicePassword"],
                AuthenticationTypes.Secure
            );

            var ds = new DirectorySearcher(entry)
            {
                Filter = filter,
                SearchScope = scope,
                PageSize = 500,
                ClientTimeout = TimeSpan.FromSeconds(8),
                ServerTimeLimit = TimeSpan.FromSeconds(8),
                ReferralChasing = ReferralChasingOption.None
            };
            foreach (var p in props) ds.PropertiesToLoad.Add(p);
            return ds;
        }

        private static bool IsDisabledUac(int uac) => (uac & 0x2) == 0x2;

        private static DateTime? ReadFileTimeUtc(object? raw)
        {
            try
            {
                if (raw == null) return null;
                long v = raw is long l ? l : Convert.ToInt64(raw);
                if (v <= 0) return null;
                return DateTime.FromFileTimeUtc(v);
            }
            catch { return null; }
        }

        private static string ClassFromResult(SearchResult r)
        {
            if (r.Properties["objectClass"]?.Count > 0)
            {
                var arr = r.Properties["objectClass"];
                var last = arr[arr.Count - 1]?.ToString()?.ToLowerInvariant();
                if (last == "user" || last == "group" || last == "computer")
                    return last!;
            }
            return "other";
        }

        // ===== Users (قديم) =====
        public async Task<List<ADUserVm>> GetUsersAsync(string? search = null, int take = 100, int skip = 0)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var q = new UserPrincipal(ctx);
                if (!string.IsNullOrWhiteSpace(search))
                    q.SamAccountName = $"*{search}*";

                using var ps = new PrincipalSearcher(q);
                return ps.FindAll()
                    .Skip(skip).Take(take)
                    .OfType<UserPrincipal>()
                    .Select(u => new ADUserVm
                    {
                        SamAccountName = u.SamAccountName ?? "",
                        DisplayName = u.DisplayName ?? "",
                        Email = u.EmailAddress ?? "",
                        Enabled = u.Enabled ?? false
                    }).ToList();
            });
        }

        public async Task<ADUserVm?> GetUserAsync(string samAccountName)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                if (user == null) return null;
                return new ADUserVm
                {
                    SamAccountName = user.SamAccountName ?? "",
                    DisplayName = user.DisplayName ?? "",
                    Email = user.EmailAddress ?? "",
                    Enabled = user.Enabled ?? false
                };
            });
        }

        public async Task<bool> SetUserEnabledAsync(string samAccountName, bool enabled)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                if (user == null) return false;
                user.Enabled = enabled;
                user.Save();
                return true;
            });
        }

        public async Task<bool> ResetPasswordAsync(string samAccountName, string newPassword)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                if (user == null) return false;
                user.SetPassword(newPassword);
                user.Save();
                return true;
            });
        }

        public async Task<bool> ResetPasswordWithOptionsAsync(string samAccountName, string newPassword, bool forceChangeAtNextLogon, bool unlockIfLocked)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName) as UserPrincipal;
                if (user == null) return false;

                user.SetPassword(newPassword);

                if (forceChangeAtNextLogon)
                {
                    user.ExpirePasswordNow();
                }

                if (unlockIfLocked && user.IsAccountLockedOut())
                {
                    user.UnlockAccount();
                }

                user.Save();
                return true;
            });
        }

        public async Task<bool> UnlockUserAsync(string samAccountName)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName) as UserPrincipal;
                if (user == null) return false;
                if (user.IsAccountLockedOut()) user.UnlockAccount();
                user.Save();
                return true;
            });
        }

        // ===== Users (جديد) =====
        public async Task<PagedResult<ADUserVm>> GetUsersAdvancedAsync(UsersQueryOptions options)
        {
            return await Task.Run(() =>
            {
                var baseDn = string.IsNullOrWhiteSpace(options.OuDn) ? GetDefaultNamingContext() : options.OuDn!;
                var clauses = new List<string>();

                // Users فقط
                clauses.Add("(&(objectCategory=person)(objectClass=user))");

                // نص البحث
                if (!string.IsNullOrWhiteSpace(options.Q))
                {
                    string q = options.Q.Replace(")", "").Replace("(", "");
                    clauses.Add($"(|(displayName=*{q}*)(sAMAccountName=*{q}*)(mail=*{q}*))");
                }

                // حالة Enabled/Disabled
                if (!string.IsNullOrWhiteSpace(options.Status))
                {
                    var st = options.Status!.ToLowerInvariant();
                    if (st == "enabled")
                        clauses.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))"); // NOT disabled
                    else if (st == "disabled")
                        clauses.Add("(userAccountControl:1.2.840.113556.1.4.803:=2)");
                    // locked سنتحقق بها لاحقاً (بحاجة Context)، لذلك نترك LDAP عام
                }

                string filter = $"(&{string.Join("", clauses)})";

                using var ds = MakeSearcher(
                    baseDn,
                    string.IsNullOrWhiteSpace(options.OuDn) ? SearchScope.Subtree : SearchScope.OneLevel,
                    filter,
                    new[] { "displayName", "mail", "sAMAccountName", "userAccountControl", "distinguishedName", "lastLogonTimestamp" }
                );

                var all = new List<ADUserVm>();
                foreach (SearchResult r in ds.FindAll())
                {
                    var sam = r.Properties["sAMAccountName"]?.Count > 0 ? r.Properties["sAMAccountName"][0]?.ToString() ?? "" : "";
                    var display = r.Properties["displayName"]?.Count > 0 ? r.Properties["displayName"][0]?.ToString() ?? "" : "";
                    var mail = r.Properties["mail"]?.Count > 0 ? r.Properties["mail"][0]?.ToString() ?? "" : "";
                    bool enabled = true;
                    if (r.Properties["userAccountControl"]?.Count > 0)
                    {
                        var uac = Convert.ToInt32(r.Properties["userAccountControl"][0]);
                        enabled = !IsDisabledUac(uac);
                    }

                    all.Add(new ADUserVm
                    {
                        SamAccountName = sam,
                        DisplayName = display,
                        Email = mail,
                        Enabled = enabled
                    });
                }

                // locked؟ نتأكد بها لو طلبها
                if (options.Status?.ToLowerInvariant() == "locked")
                {
                    using var ctx = CreateContext();
                    all = all.Where(u =>
                    {
                        try
                        {
                            var up = UserPrincipal.FindByIdentity(ctx, u.SamAccountName);
                            return up != null && up.IsAccountLockedOut();
                        }
                        catch { return false; }
                    }).ToList();
                }

                var total = all.Count;
                var paged = all.Skip(options.Skip).Take(options.Take).ToList();

                return new PagedResult<ADUserVm>
                {
                    Items = paged,
                    Total = total
                };
            });
        }

        public async Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? dn)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                UserPrincipal? up = null;
                DirectoryEntry? de = null;

                if (!string.IsNullOrWhiteSpace(dn))
                {
                    de = new DirectoryEntry(BuildPath(dn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                    up = UserPrincipal.FindByIdentity(ctx, IdentityType.DistinguishedName, dn);
                }
                else if (!string.IsNullOrWhiteSpace(sam))
                {
                    up = UserPrincipal.FindByIdentity(ctx, sam);
                    if (up != null)
                        de = up.GetUnderlyingObject() as DirectoryEntry;
                }

                if (up == null || de == null) return null;

                // خصائص إضافية
                var lastLogonTimestamp = de.Properties["lastLogonTimestamp"]?.Value;
                var pwdLastSet = de.Properties["pwdLastSet"]?.Value;
                var accExpires = de.Properties["accountExpires"]?.Value;

                DateTime? lastLogonUtc = ReadFileTimeUtc(lastLogonTimestamp);
                DateTime? pwdLastSetUtc = ReadFileTimeUtc(pwdLastSet);
                DateTime? accExpiresUtc = ReadFileTimeUtc(accExpires);

                var dn2 = de.Properties["distinguishedName"]?.Value?.ToString() ?? "";
                var mail = de.Properties["mail"]?.Value?.ToString();
                var uac = de.Properties["userAccountControl"]?.Value is int v ? v : 0;
                bool enabled = (uac & 2) != 2;

                // المجموعات
                var groups = new List<string>();
                try
                {
                    foreach (var g in up.GetGroups())
                    {
                        if (g is GroupPrincipal gp)
                            groups.Add(gp.SamAccountName ?? gp.Name ?? "");
                    }
                }
                catch { }

                bool? locked = null;
                try { locked = up.IsAccountLockedOut(); } catch { }

                var vm = new ADObjectDetailsVm
                {
                    Name = up.DisplayName ?? up.SamAccountName ?? "",
                    DistinguishedName = dn2,
                    SamAccountName = up.SamAccountName ?? "",
                    ObjectClass = "user",
                    Email = mail,
                    Enabled = enabled,
                    Locked = locked,
                    LastLogonUtc = lastLogonUtc
                };

                // قيم إضافية للـ Modal
                vm.Extra["parentDn"] = GetParentDn(dn2);
                vm.Extra["passwordLastSetUtc"] = pwdLastSetUtc;
                vm.Extra["accountExpiresUtc"] = accExpiresUtc;
                vm.Extra["groups"] = groups;

                return vm;
            });
        }

        // ===== Groups =====
        public async Task<List<ADGroupVm>> GetGroupsAsync(string? search = null, int take = 100, int skip = 0)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var q = new GroupPrincipal(ctx);
                if (!string.IsNullOrWhiteSpace(search))
                    q.Name = $"*{search}*";
                using var ps = new PrincipalSearcher(q);
                return ps.FindAll()
                    .Skip(skip).Take(take)
                    .OfType<GroupPrincipal>()
                    .Select(g => new ADGroupVm
                    {
                        Name = g.SamAccountName ?? g.Name ?? "",
                        Description = g.Description ?? ""
                    }).ToList();
            });
        }

        public async Task<bool> AddUserToGroupAsync(string samAccountName, string groupName)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                var grp = GroupPrincipal.FindByIdentity(ctx, groupName);
                if (user == null || grp == null) return false;
                grp.Members.Add(user);
                grp.Save();
                return true;
            });
        }

        public async Task<bool> RemoveUserFromGroupAsync(string samAccountName, string groupName)
        {
            return await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                var grp = GroupPrincipal.FindByIdentity(ctx, groupName);
                if (user == null || grp == null) return false;
                grp.Members.Remove(user);
                grp.Save();
                return true;
            });
        }

        // ===== OUs =====
        public async Task<List<OUVm>> GetOUsAsync(string? baseDn = null, int take = 500, int skip = 0)
        {
            return await Task.Run(() =>
            {
                var dn = string.IsNullOrWhiteSpace(baseDn) ? GetDefaultNamingContext() : baseDn!;
                _logger.LogInformation("GetOUsAsync: baseDn={BaseDn}", dn);

                using var ds = MakeSearcher(dn, SearchScope.Subtree, "(objectClass=organizationalUnit)",
                    new[] { "name", "distinguishedName", "description" });

                var results = new List<OUVm>();
                foreach (SearchResult r in ds.FindAll().Cast<SearchResult>().Skip(skip).Take(take))
                {
                    var ouDn = r.Properties["distinguishedName"]?.Count > 0 ? r.Properties["distinguishedName"][0]?.ToString() ?? "" : "";
                    var name = r.Properties["name"]?.Count > 0 ? r.Properties["name"][0]?.ToString() ?? "" : "";
                    var desc = r.Properties["description"]?.Count > 0 ? r.Properties["description"][0]?.ToString() ?? "" : null;

                    results.Add(new OUVm
                    {
                        Name = name,
                        DistinguishedName = ouDn,
                        ParentDn = GetParentDn(ouDn),
                        Description = desc,
                        ChildCount = 1
                    });
                }
                _logger.LogInformation("GetOUsAsync: returned {Count} items.", results.Count);
                return results;
            });
        }

        public async Task<List<OUVm>> GetChildOUsAsync(string? parentDn = null)
        {
            return await Task.Run(() =>
            {
                string baseDn = string.IsNullOrWhiteSpace(parentDn) ? GetDefaultNamingContext() : parentDn!;
                _logger.LogInformation("GetChildOUsAsync: parentDn={Parent}", baseDn);

                using var ds = MakeSearcher(baseDn, SearchScope.OneLevel, "(objectClass=organizationalUnit)",
                    new[] { "name", "distinguishedName", "description" });

                var list = new List<OUVm>();
                foreach (SearchResult r in ds.FindAll())
                {
                    var dn = r.Properties["distinguishedName"]?.Count > 0 ? r.Properties["distinguishedName"][0]?.ToString() ?? "" : "";
                    var name = r.Properties["name"]?.Count > 0 ? r.Properties["name"][0]?.ToString() ?? "" : "";
                    var desc = r.Properties["description"]?.Count > 0 ? r.Properties["description"][0]?.ToString() ?? "" : null;

                    list.Add(new OUVm
                    {
                        Name = name,
                        DistinguishedName = dn,
                        ParentDn = baseDn,
                        Description = desc,
                        ChildCount = 1
                    });
                }
                _logger.LogInformation("GetChildOUsAsync: returned {Count} items.", list.Count);
                return list;
            });
        }

        // ===== Objects in OU =====
        public async Task<List<ADObjectVm>> GetOuObjectsAsync(string ouDn, int take = 200, int skip = 0, string? search = null)
        {
            return await Task.Run(() =>
            {
                string searchPart = string.IsNullOrWhiteSpace(search) ? "" : $"(|(name=*{search}*)(sAMAccountName=*{search}*)(mail=*{search}*))";
                string filter = $"(&(|(&(objectCategory=person)(objectClass=user))(objectClass=group)(objectClass=computer)){searchPart})";

                using var ds = MakeSearcher(ouDn, SearchScope.OneLevel, filter,
                    new[] { "name", "distinguishedName", "sAMAccountName", "objectClass", "userAccountControl" });

                var list = new List<ADObjectVm>();
                foreach (SearchResult r in ds.FindAll().Cast<SearchResult>().Skip(skip).Take(take))
                {
                    string dn = r.Properties["distinguishedName"]?.Count > 0 ? r.Properties["distinguishedName"][0]?.ToString() ?? "" : "";
                    string name = r.Properties["name"]?.Count > 0 ? r.Properties["name"][0]?.ToString() ?? "" : "";
                    string? sam = r.Properties["sAMAccountName"]?.Count > 0 ? r.Properties["sAMAccountName"][0]?.ToString() : null;

                    string oc = ClassFromResult(r);
                    bool? disabled = null;
                    if (r.Properties["userAccountControl"]?.Count > 0)
                    {
                        int uac = Convert.ToInt32(r.Properties["userAccountControl"][0]);
                        disabled = IsDisabledUac(uac);
                    }

                    list.Add(new ADObjectVm
                    {
                        Name = name,
                        DistinguishedName = dn,
                        SamAccountName = sam,
                        ObjectClass = oc,
                        Disabled = disabled
                    });
                }

                _logger.LogInformation("GetOuObjectsAsync: {Count} objects under {Ou}", list.Count, ouDn);
                return list;
            });
        }

        // ===== Object details =====
        public async Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn)
        {
            return await Task.Run(() =>
            {
                using var entry = new DirectoryEntry(BuildPath(dn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                using var ds = new DirectorySearcher(entry)
                {
                    SearchScope = SearchScope.Base,
                    Filter = "(objectClass=*)",
                    ClientTimeout = TimeSpan.FromSeconds(6),
                    ServerTimeLimit = TimeSpan.FromSeconds(6),
                    ReferralChasing = ReferralChasingOption.None
                };
                ds.PropertiesToLoad.AddRange(new[]
                {
                    "name", "distinguishedName", "sAMAccountName", "objectClass",
                    "mail", "userAccountControl", "lastLogonTimestamp", "lastLogon"
                });

                var res = ds.FindOne();
                if (res == null) return null;

                string name = res.Properties["name"]?.Count > 0 ? res.Properties["name"][0]?.ToString() ?? "" : "";
                string dn2  = res.Properties["distinguishedName"]?.Count > 0 ? res.Properties["distinguishedName"][0]?.ToString() ?? "" : "";
                string? sam = res.Properties["sAMAccountName"]?.Count > 0 ? res.Properties["sAMAccountName"][0]?.ToString() : null;
                string oc   = ClassFromResult(res);
                string? mail= res.Properties["mail"]?.Count > 0 ? res.Properties["mail"][0]?.ToString() : null;

                bool? enabled = null;
                if (res.Properties["userAccountControl"]?.Count > 0)
                {
                    int uac = Convert.ToInt32(res.Properties["userAccountControl"][0]);
                    enabled = !IsDisabledUac(uac);
                }

                DateTime? lastLogon = ReadFileTimeUtc(res.Properties["lastLogonTimestamp"]?.Count > 0 ? res.Properties["lastLogonTimestamp"][0] : null)
                                      ?? ReadFileTimeUtc(res.Properties["lastLogon"]?.Count > 0 ? res.Properties["lastLogon"][0] : null);

                bool? locked = null;
                if (oc == "user" && !string.IsNullOrWhiteSpace(sam))
                {
                    using var ctx = CreateContext();
                    var up = UserPrincipal.FindByIdentity(ctx, IdentityType.DistinguishedName, dn2);
                    if (up != null) locked = up.IsAccountLockedOut();
                }

                var vm = new ADObjectDetailsVm
                {
                    Name = name,
                    DistinguishedName = dn2,
                    SamAccountName = sam,
                    ObjectClass = oc,
                    Email = mail,
                    Enabled = enabled,
                    Locked = locked,
                    LastLogonUtc = lastLogon
                };

                vm.Extra["parentDn"] = GetParentDn(dn2);
                return vm;
            });
        }

        // ===== Mutations for OU =====
        public async Task<bool> CreateOUAsync(string parentDn, string name, string? description = null) =>
            await Task.Run(() =>
            {
                using var parent = new DirectoryEntry(BuildPath(parentDn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                using var newOu = parent.Children.Add($"OU={name}", "organizationalUnit");
                if (!string.IsNullOrWhiteSpace(description)) newOu.Properties["description"].Value = description;
                newOu.CommitChanges(); return true;
            });

        public async Task<bool> RenameOUAsync(string dn, string newName) =>
            await Task.Run(() =>
            {
                using var ou = new DirectoryEntry(BuildPath(dn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                ou.Rename($"OU={newName}"); ou.CommitChanges(); return true;
            });

        public async Task<bool> DeleteOUAsync(string dn) =>
            await Task.Run(() =>
            {
                using var ou = new DirectoryEntry(BuildPath(dn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                var parentDn = GetParentDn(dn); if (parentDn == null) return false;
                using var parent = new DirectoryEntry(BuildPath(parentDn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                var cn = $"OU={ou.Properties["name"].Value}";
                var childToRemove = parent.Children.Find(cn, "organizationalUnit");
                parent.Children.Remove(childToRemove);
                parent.CommitChanges(); return true;
            });

        public async Task<bool> MoveObjectAsync(string objectDn, string targetOuDn) =>
            await Task.Run(() =>
            {
                using var obj = new DirectoryEntry(BuildPath(objectDn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                using var target = new DirectoryEntry(BuildPath(targetOuDn), _cfg["AD:ServiceUserUPN"], _cfg["AD:ServicePassword"]);
                obj.MoveTo(target); obj.CommitChanges(); return true;
            });

        public async Task<bool> MoveUserBySamAsync(string samAccountName, string targetOuDn) =>
            await Task.Run(() =>
            {
                using var ctx = CreateContext();
                var user = UserPrincipal.FindByIdentity(ctx, samAccountName) as UserPrincipal;
                if (user == null) return false;
                var de = user.GetUnderlyingObject() as DirectoryEntry;
                var dn = de?.Properties["distinguishedName"]?.Value?.ToString();
                if (string.IsNullOrWhiteSpace(dn)) return false;
                return MoveObjectAsync(dn!, targetOuDn).GetAwaiter().GetResult();
            });
    }
}
