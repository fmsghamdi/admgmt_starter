using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using admgmt_backend.ViewModels;

namespace admgmt_backend.Services
{
    // ملاحظة مهمة:
    // لا تضع تعريف الواجهة هنا. IADService موجود في IADService.cs
    // هذا الملف يحتوي فقط على التطبيق (implementation).
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class ADService : IADService
    {
        private readonly ILogger<ADService> _log;
        private readonly IConfiguration _config;

        // إعدادات AD من appsettings.json (عدّل المفاتيح لتطابق الموجود عندك)
        private readonly string _ldapPath;
        private readonly string? _bindUser;
        private readonly string? _bindPassword;
        private readonly TimeSpan _searchTimeout;

        public ADService(ILogger<ADService> log, IConfiguration config)
        {
            _log = log;
            _config = config;

            _ldapPath = _config["AD:LdapPath"] ?? "LDAP:///";
            _bindUser = _config["AD:ServiceUserUPN"];
            _bindPassword = _config["AD:ServicePassword"];

            var timeoutSec = _config.GetValue<int?>("AD:SearchTimeoutSeconds") ?? 30;
            _searchTimeout = TimeSpan.FromSeconds(timeoutSec);
        }

        #region ===== Users =====

        public async Task<(List<ADUserVm> Items, int Total)> GetUsersAdvancedAsync(UsersQueryOptions opts)
        {
            try
            {
                opts ??= new UsersQueryOptions();
                var q = (opts.Q ?? "").Trim();
                var take = opts.Take <= 0 ? 50 : Math.Min(opts.Take, 500);
                var skip = Math.Max(0, opts.Skip);

                string? ouDn = null;
                try
                {
                    var ouProp = typeof(UsersQueryOptions).GetProperty("OUdn",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.IgnoreCase);

                    if (ouProp != null)
                    {
                        ouDn = ouProp.GetValue(opts) as string;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to get OUdn property");
                }

                ouDn = (ouDn ?? "").Trim();

            return await Task.Run(() =>
            {
                try 
                {
                    using var entry = string.IsNullOrWhiteSpace(ouDn)
                        ? MakeDirectoryEntry(_ldapPath)
                        : MakeDirectoryEntry($"LDAP://{ouDn}");

                    using var searcher = new DirectorySearcher(entry)
                    {
                        PageSize = 100,
                        SizeLimit = take + skip,
                        SearchScope = SearchScope.Subtree,
                        CacheResults = true,
                        ClientTimeout = _searchTimeout,
                        ServerTimeLimit = _searchTimeout,
                        ServerPageTimeLimit = TimeSpan.FromSeconds(5)
                    };

                    // Build filter
                    var filterParts = new List<string> { "(objectCategory=person)", "(objectClass=user)" };

                    // Handle enabled/disabled status
                    if (opts.Status == UserStatusFilter.Enabled)
                    {
                        filterParts.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))");
                    }
                    else if (opts.Status == UserStatusFilter.Disabled)
                    {
                        filterParts.Add("(userAccountControl:1.2.840.113556.1.4.803:=2)");
                    }

                    // Handle search text
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        var esc = EscapeLdap(q);
                        filterParts.Add($"(|(displayName=*{esc}*)(sAMAccountName=*{esc}*)(mail=*{esc}*)(cn=*{esc}*))");
                    }

                    searcher.Filter = "(&" + string.Join("", filterParts) + ")";

                    // Request specific properties
                    searcher.PropertiesToLoad.Clear();
                    searcher.PropertiesToLoad.AddRange(new[]
                    {
                        "displayName",
                        "sAMAccountName",
                        "distinguishedName",
                        "mail",
                        "userAccountControl",
                        "lastLogonTimestamp",
                        "cn",
                        "whenCreated",
                        "pwdLastSet"
                    });

                    var results = searcher.FindAll().Cast<SearchResult>().ToList();
                    var total = results.Count;

                    // Handle sorting
                    var sortBy = (opts.SortBy ?? "displayName").ToLowerInvariant();
                    var desc = opts.Desc;

                    Func<SearchResult, object?> keySel = sortBy switch
                    {
                        "sam" or "samaccountname" => s => GetProp<string>(s, "sAMAccountName"),
                        "email" or "mail" => s => GetProp<string>(s, "mail"),
                        "lastlogon" or "lastlogonutc" => s => ToDate(GetProp<long?>(s, "lastLogonTimestamp")),
                        _ => s => GetProp<string>(s, "displayName") ?? GetProp<string>(s, "cn") ?? GetProp<string>(s, "sAMAccountName") ?? ""
                    };

                    var ordered = desc ? results.OrderByDescending(keySel) : results.OrderBy(keySel);
                    var page = ordered.Skip(skip).Take(take)
                        .Select(sr =>
                        {
                            try
                            {
                                var uac = GetProp<int?>(sr, "userAccountControl") ?? 0;
                                var isDisabled = (uac & 0x2) == 0x2;
                                
                                var sam = GetProp<string>(sr, "sAMAccountName") ?? "";
                                if (string.IsNullOrWhiteSpace(sam)) return null;

                                var displayName = GetProp<string>(sr, "displayName");
                                if (string.IsNullOrWhiteSpace(displayName))
                                {
                                    displayName = GetProp<string>(sr, "cn") ?? sam;
                                }

                                return new ADUserVm
                                {
                                    SamAccountName = sam,
                                    DisplayName = displayName,
                                    Email = GetProp<string>(sr, "mail") ?? "",
                                    Enabled = !isDisabled,
                                    SAM = sam,
                                    DistinguishedName = GetProp<string>(sr, "distinguishedName"),
                                    LastLogonUtc = ToDate(GetProp<long?>(sr, "lastLogonTimestamp"))
                                };
                            }
                            catch (Exception ex)
                            {
                                _log.LogWarning(ex, "Failed to map SearchResult to ADUserVm");
                                return null;
                            }
                        })
                        .Where(u => u != null)
                        .ToList()!;

                    return (page, total);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to search AD users");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to execute AD search");
            throw;
        }
    }

    public Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? dn)
        {
            if (string.IsNullOrWhiteSpace(sam) && string.IsNullOrWhiteSpace(dn))
                throw new ArgumentException("Provide sam or dn.");

            return Task.Run(() =>
            {
                try
                {
                    using var entry = MakeDirectoryEntry(_ldapPath);
                    using var searcher = new DirectorySearcher(entry)
                    {
                        PageSize = 1,
                        SizeLimit = 1
                    };

                    if (!string.IsNullOrWhiteSpace(sam))
                    {
                        searcher.Filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdap(sam)}))";
                    }
                    else
                    {
                        searcher.Filter = $"(&(objectCategory=person)(objectClass=user)(distinguishedName={EscapeLdap(dn!)}))";
                    }

                    searcher.PropertiesToLoad.AddRange(new[]
                    {
                        "displayName",
                        "sAMAccountName",
                        "distinguishedName",
                        "mail",
                        "userAccountControl",
                        "lastLogonTimestamp",
                        "whenCreated",
                        "pwdLastSet",
                        "department",
                        "title",
                        "telephoneNumber",
                        "mobile",
                        "manager",
                        "company",
                        "employeeID",
                        "memberOf",
                        "accountExpires"
                    });

                    var result = searcher.FindOne();
                    if (result == null)
                        return null;

                    var uac = GetProp<int?>(result, "userAccountControl") ?? 0;
                    var isDisabled = (uac & 0x2) == 0x2;
                    var isLocked = (uac & 0x10) == 0x10;

                    // استخراج المجموعات
                    var memberOf = GetPropArray<string>(result, "memberOf") ?? Array.Empty<string>();
                    var groups = memberOf.Select(dn => dn.Split(',')[0].Replace("CN=", "")).ToList();

                    // التحقق من تاريخ انتهاء الحساب
                    var accountExpires = GetProp<long?>(result, "accountExpires");
                    string expirationStatus = "Never";
                    if (accountExpires.HasValue && accountExpires.Value != 0 && accountExpires.Value != 9223372036854775807)
                    {
                        var expirationDate = DateTime.FromFileTime(accountExpires.Value);
                        expirationStatus = expirationDate > DateTime.UtcNow ? expirationDate.ToString() : "Expired";
                    }

                    var details = new ADObjectDetailsVm 
                    { 
                        Name = GetProp<string>(result, "sAMAccountName") ?? "",
                        DisplayName = GetProp<string>(result, "displayName") ?? GetProp<string>(result, "cn") ?? "",
                        Type = "user",
                        Properties = new Dictionary<string, string?>
                        {
                            ["Email"] = GetProp<string>(result, "mail"),
                            ["Department"] = GetProp<string>(result, "department"),
                            ["Title"] = GetProp<string>(result, "title"),
                            ["Phone"] = GetProp<string>(result, "telephoneNumber"),
                            ["Mobile"] = GetProp<string>(result, "mobile"),
                            ["Company"] = GetProp<string>(result, "company"),
                            ["EmployeeID"] = GetProp<string>(result, "employeeID"),
                            ["Account Status"] = isDisabled ? "Disabled" : "Enabled",
                            ["Account Lock"] = isLocked ? "Locked" : "Not locked",
                            ["Last Logon"] = ToDate(GetProp<long?>(result, "lastLogonTimestamp"))?.ToString() ?? "Never",
                            ["Created"] = GetProp<DateTime?>(result, "whenCreated")?.ToString() ?? "Unknown",
                            ["Password Last Set"] = ToDate(GetProp<long?>(result, "pwdLastSet"))?.ToString() ?? "Never",
                            ["Account Expires"] = expirationStatus,
                            ["Groups"] = string.Join(", ", groups)
                        }
                    };

                    return details;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to get user details for {Sam} / {Dn}", sam, dn);
                    throw;
                }
            });
        }

        public Task<bool> EnableUserAsync(string sam) => throw new NotImplementedException();
        public Task<bool> DisableUserAsync(string sam) => throw new NotImplementedException();
        public Task<bool> ResetPasswordAsync(string sam, string newPassword) => throw new NotImplementedException();
        public Task<bool> ExpirePasswordNowAsync(string sam) => throw new NotImplementedException();
        public Task<bool> UnlockAccountAsync(string sam) => throw new NotImplementedException();
        public Task<List<ADUserVm>> GetUsersAsync(string? q, int skip, int take) => throw new NotImplementedException();
        public Task<ADUserVm?> GetUserAsync(string sam) => throw new NotImplementedException();
        public Task<bool> SetUserEnabledAsync(string sam, bool enabled) => throw new NotImplementedException();
        public Task<bool> ResetPasswordWithOptionsAsync(string sam, string newPassword, bool forceChange, bool unlock) => throw new NotImplementedException();
        public Task<bool> UnlockUserAsync(string sam) => throw new NotImplementedException();
        public Task<List<OUVm>> GetOuChildrenAsync(string dn) => throw new NotImplementedException();

        #endregion

        #region ===== Groups =====

        public Task<List<ADGroupVm>> GetGroupsAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                using var entry = MakeDirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(objectClass=group)",
                    PageSize = 500
                };

                searcher.PropertiesToLoad.AddRange(new[] 
                { 
                    "name",
                    "sAMAccountName",
                    "distinguishedName",
                    "description",
                    "member"
                });

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var escaped = EscapeLdap(q);
                    searcher.Filter = $"(&(objectClass=group)(|(name=*{escaped}*)(sAMAccountName=*{escaped}*)))";
                }

                var results = searcher.FindAll().Cast<SearchResult>();
                return results
                    .Select(r => new ADGroupVm
                    {
                        Name = GetProp<string>(r, "name") ?? "",
                        SAM = GetProp<string>(r, "sAMAccountName"),
                        DistinguishedName = GetProp<string>(r, "distinguishedName"),
                        Description = GetProp<string>(r, "description"),
                        MemberCount = r.Properties["member"]?.Count ?? 0
                    })
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            });
        }

        public Task<bool> AddUserToGroupAsync(string userSam, string groupSam)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var context = new PrincipalContext(ContextType.Domain, _config["AD:Domain"]);
                    using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, userSam);
                    using var group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupSam);

                    if (user == null || group == null) return false;

                    if (!group.Members.Contains(user))
                    {
                        group.Members.Add(user);
                        group.Save();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to add user {UserSam} to group {GroupSam}", userSam, groupSam);
                    return false;
                }
            });
        }

        public Task<bool> RemoveUserFromGroupAsync(string userSam, string groupSam)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var context = new PrincipalContext(ContextType.Domain, _config["AD:Domain"]);
                    using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, userSam);
                    using var group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupSam);

                    if (user == null || group == null) return false;

                    if (group.Members.Contains(user))
                    {
                        group.Members.Remove(user);
                        group.Save();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to remove user {UserSam} from group {GroupSam}", userSam, groupSam);
                    return false;
                }
            });
        }

        public Task<List<ADUserVm>> GetGroupMembersAsync(string groupSam)
        {
            return Task.Run(() =>
            {
                using var context = new PrincipalContext(ContextType.Domain, _config["AD:Domain"]);
                using var group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupSam);
                
                if (group == null) return new List<ADUserVm>();

                return group.Members
                    .Cast<UserPrincipal>()
                    .Where(u => u != null)
                    .Select(u => new ADUserVm
                    {
                        DisplayName = u.DisplayName ?? u.Name ?? u.SamAccountName ?? "",
                        SAM = u.SamAccountName,
                        Email = u.EmailAddress,
                        DistinguishedName = u.DistinguishedName,
                        LastLogonUtc = u.LastLogon
                    })
                    .ToList();
            });
        }

        public async Task<List<OUVm>> GetRootOusAsync()
        {
            return await Task.Run(() =>
            {
                using var entry = MakeDirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(objectClass=organizationalUnit)",
                    SearchScope = SearchScope.OneLevel,
                    PageSize = 1000,
                    SizeLimit = 0,
                    ServerTimeLimit = _searchTimeout,
                    ClientTimeout = _searchTimeout,
                    CacheResults = true,
                    ServerPageTimeLimit = TimeSpan.FromSeconds(5)
                };

                searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "description" });

                var results = searcher.FindAll().Cast<SearchResult>();
                return results.Select(r => new OUVm
                {
                    Name = GetProp<string>(r, "name") ?? "",
                    DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                    Description = GetProp<string>(r, "description")
                }).ToList();
            });
        }

        public Task<List<OUVm>> GetChildOUsAsync(string? parentDn)
        {
            return Task.Run(() =>
            {
                try
                {
                    // إذا لم يتم تحديد الـ parentDn، نجلب الوحدات الرئيسية
                    if (string.IsNullOrWhiteSpace(parentDn))
                    {
                        using var rootEntry = MakeDirectoryEntry(_ldapPath);
                        using var searcher = new DirectorySearcher(rootEntry)
                        {
                            Filter = "(objectClass=organizationalUnit)",
                            SearchScope = SearchScope.OneLevel,
                            PageSize = 1000,
                            SizeLimit = 0
                        };

                        searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "description" });
                        var results = searcher.FindAll().Cast<SearchResult>();
                        return results.Select(r => new OUVm
                        {
                            Name = GetProp<string>(r, "name") ?? "",
                            DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                            Description = GetProp<string>(r, "description")
                        }).ToList();
                    }

                    // للوحدات الفرعية، نستخدم الـ DN المحدد مباشرة
                    using var parentEntry = MakeDirectoryEntry($"LDAP://{parentDn}");
                    using var childSearcher = new DirectorySearcher(parentEntry)
                    {
                        Filter = "(objectClass=organizationalUnit)",
                        SearchScope = SearchScope.OneLevel,
                        PageSize = 1000,
                        SizeLimit = 0,
                        ServerTimeLimit = _searchTimeout,
                        ClientTimeout = _searchTimeout
                    };

                    childSearcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "description" });

                    var childResults = childSearcher.FindAll().Cast<SearchResult>();
                    return childResults.Select(r => new OUVm
                    {
                        Name = GetProp<string>(r, "name") ?? "",
                        DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                        Description = GetProp<string>(r, "description")
                    }).ToList();
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to get child OUs for parent DN: {ParentDN}", parentDn);
                    throw;
                }
            });
        }

        public Task<List<ADObjectVm>> GetOuObjectsAsync(string dn, int skip, int take, string? q)
        {
            return Task.Run(() =>
            {
                using var entry = MakeDirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(|(objectClass=user)(objectClass=group)(objectClass=computer))(distinguishedName={EscapeLdap(dn)}*))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000,
                    SizeLimit = 0,
                    ServerTimeLimit = _searchTimeout,
                    ClientTimeout = _searchTimeout,
                    CacheResults = true
                };

                searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "objectClass" });

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var escaped = EscapeLdap(q);
                    searcher.Filter = $"(&{searcher.Filter}(name=*{escaped}*))";
                }

                var results = searcher.FindAll().Cast<SearchResult>();
                
                return results
                    .Select(r => new ADObjectVm
                    {
                        Name = GetProp<string>(r, "name") ?? "",
                        DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                        ObjectClass = GetProp<string>(r, "objectClass")?.ToString()?.ToLowerInvariant() ?? "unknown"
                    })
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            });
        }

        public Task<bool> CreateOUAsync(string parentDn, string name, string? description)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var parentEntry = MakeDirectoryEntry($"LDAP://{parentDn}");
                    using var newOu = parentEntry.Children.Add($"OU={name}", "organizationalUnit");
                    
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        newOu.Properties["description"].Value = description;
                    }
                    
                    newOu.CommitChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to create OU {Name} under {ParentDN}", name, parentDn);
                    return false;
                }
            });
        }

        public Task<bool> DeleteOUAsync(string dn)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var entry = MakeDirectoryEntry($"LDAP://{dn}");
                    using var parent = entry.Parent;
                    parent?.Children.Remove(entry);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to delete OU {DN}", dn);
                    return false;
                }
            });
        }

        // Implementations for other OU methods
        public Task<bool> CreateOuAsync(string parentDn, string ouName) => CreateOUAsync(parentDn, ouName, null);
        public Task<List<OUVm>> GetOUsAsync(string? q, int skip, int take) => throw new NotImplementedException();
        public Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn) => throw new NotImplementedException();
        public Task<bool> RenameOUAsync(string dn, string newName) => throw new NotImplementedException();
        public Task<bool> MoveObjectAsync(string dn, string targetOuDn) => throw new NotImplementedException();
        public Task<bool> MoveUserBySamAsync(string sam, string targetOuDn) => throw new NotImplementedException();

        #endregion

        #region ===== Helpers =====

        private DirectoryEntry MakeDirectoryEntry(string path)
        {
            try
            {
                var authTypes = AuthenticationTypes.Secure | AuthenticationTypes.Sealing | AuthenticationTypes.Signing;
                
                if (!string.IsNullOrWhiteSpace(_bindUser) && !string.IsNullOrWhiteSpace(_bindPassword))
                {
                    var entry = new DirectoryEntry(path, _bindUser, _bindPassword, authTypes);
                    entry.AuthenticationType = authTypes;
                    return entry;
                }
                
                var defaultEntry = new DirectoryEntry(path);
                defaultEntry.AuthenticationType = authTypes;
                return defaultEntry;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to create DirectoryEntry for path: {Path}", path);
                throw new InvalidOperationException("Failed to connect to Active Directory. Please check your credentials and connection settings.", ex);
            }
        }

        private static string EscapeLdap(string value)
        {
            // RFC 4515 escaping
            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }

        private static T? GetProp<T>(SearchResult sr, string name)
        {
            if (!sr.Properties.Contains(name) || sr.Properties[name].Count == 0) return default;
            try
            {
                var raw = sr.Properties[name][0];
                if (raw == null) return default;

                if (typeof(T) == typeof(string))
                    return (T?)(object?)raw.ToString();

                if (typeof(T) == typeof(long?) && raw is IConvertible)
                {
                    if (raw is long l) return (T?)(object?)l;
                    if (long.TryParse(raw.ToString(), out var ll))
                        return (T?)(object?)ll;
                }

                if (typeof(T) == typeof(int?) && raw is IConvertible)
                {
                    if (raw is int i) return (T?)(object?)i;
                    if (int.TryParse(raw.ToString(), out var ii))
                        return (T?)(object?)ii;
                }

                return (T?)raw;
            }
            catch { /* ignore */ }
            return default;
        }

        private static T[]? GetPropArray<T>(SearchResult sr, string name)
        {
            if (!sr.Properties.Contains(name)) return null;
            
            try
            {
                var props = sr.Properties[name];
                var result = new T[props.Count];
                
                for (int i = 0; i < props.Count; i++)
                {
                    var raw = props[i];
                    if (raw == null) continue;

                    if (typeof(T) == typeof(string))
                        result[i] = (T)(object)raw.ToString()!;
                    else
                        result[i] = (T)raw;
                }

                return result;
            }
            catch { /* ignore */ }
            
            return null;
        }

        private static DateTime? ToDate(long? fileTime)
        {
            if (fileTime == null || fileTime <= 0) return null;
            try { return DateTime.FromFileTimeUtc(fileTime.Value); }
            catch { return null; }
        }



        #endregion
    }
}
