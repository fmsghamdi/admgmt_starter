using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace admgmt_backend.Services
{
    // ملاحظة مهمة:
    // لا يوجد تعريف IADService هنا. الواجهة موجودة في Services/IADService.cs في مشروعك.

    #region ViewModels (مطابقة للأسماء المستخدمة في مشروعك)
    public sealed class OUVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string? Description { get; set; }
    }

    public sealed class ADUserVm
    {
        public string DisplayName { get; set; } = "";
        public string SAM { get; set; } = "";
        public string Email { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public DateTime? LastLogonUtc { get; set; }
        public bool Enabled { get; set; }
    }

    public sealed class ADGroupVm
    {
        public string Name { get; set; } = "";
        public string SAM { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string? Description { get; set; }
        public int MemberCount { get; set; }
    }

    public sealed class ADObjectVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string ObjectClass { get; set; } = "";
    }
    #endregion

    public sealed class ADService : IADService
    {
        private readonly ILogger<ADService> _log;
        private readonly string _ldapPath;              // مثال: LDAP://AZDC03.uqu.local/DC=UQU,DC=LOCAL
        private readonly TimeSpan _searchTimeout = TimeSpan.FromSeconds(20);

        public ADService(IConfiguration cfg, ILogger<ADService> log)
        {
            _log = log;
            var domainPath = cfg["AD:LdapPath"];
            if (string.IsNullOrWhiteSpace(domainPath))
                throw new ArgumentException("AD:LdapPath is not configured.");
            _ldapPath = domainPath!;
        }

        private DirectoryEntry MakeEntry(string path) => new(path);

        private static T? GetProp<T>(SearchResult r, string name)
        {
            if (!r.Properties.Contains(name)) return default;
            var v = r.Properties[name];
            if (v == null || v.Count == 0) return default;
            try { return (T?)Convert.ChangeType(v[0], typeof(T)); }
            catch { return default; }
        }

        private static string EscapeLdap(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
        }

        private static DateTime? ToDate(long? fileTime)
        {
            if (fileTime is null or 0) return null;
            try { return DateTime.FromFileTimeUtc(fileTime.Value); } catch { return null; }
        }

        private static bool HasFlag(int value, int flag) => (value & flag) == flag;

        // ================= OUs =================

        public Task<List<OUVm>> GetRootOusAsync() => GetOUsAsync(null, 0, 1000);

        // يطابق توقيع الواجهة الأصلية لديك
        public Task<List<OUVm>> GetOUsAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                using var entry = MakeEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(objectClass=organizationalUnit)",
                    SearchScope = SearchScope.OneLevel,
                    PageSize = 1000,
                    SizeLimit = 0,
                    ClientTimeout = _searchTimeout,
                    ServerTimeLimit = _searchTimeout
                };
                searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "description" });

                var results = searcher.FindAll().Cast<SearchResult>()
                    .Select(r => new OUVm
                    {
                        Name = GetProp<string>(r, "name") ?? "",
                        DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                        Description = GetProp<string>(r, "description") ?? ""
                    });

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var qq = q.Trim().ToLowerInvariant();
                    results = results.Where(x =>
                        (x.Name ?? "").ToLowerInvariant().Contains(qq) ||
                        (x.Description ?? "").ToLowerInvariant().Contains(qq));
                }

                return results.Skip(skip).Take(take).ToList();
            });
        }

        // مطابق لتوقيع: GetChildOUsAsync(string? parentDn)
        public Task<List<OUVm>> GetChildOUsAsync(string? parentDn)
        {
            return Task.Run(() =>
            {
                using var parent = string.IsNullOrWhiteSpace(parentDn) ? MakeEntry(_ldapPath) : MakeEntry($"LDAP://{parentDn}");
                using var searcher = new DirectorySearcher(parent)
                {
                    Filter = "(objectClass=organizationalUnit)",
                    SearchScope = SearchScope.OneLevel,
                    PageSize = 1000,
                    SizeLimit = 0,
                    ClientTimeout = _searchTimeout,
                    ServerTimeLimit = _searchTimeout
                };
                searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "description" });

                var results = searcher.FindAll().Cast<SearchResult>();
                return results.Select(r => new OUVm
                {
                    Name = GetProp<string>(r, "name") ?? "",
                    DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                    Description = GetProp<string>(r, "description") ?? ""
                }).ToList();
            });
        }

        // مطابق لتوقيع الواجهة لديك
        public Task<List<ADObjectVm>> GetOuObjectsAsync(string dn, int skip, int take, string? q)
        {
            return Task.Run(() =>
            {
                using var parent = string.IsNullOrWhiteSpace(dn) ? MakeEntry(_ldapPath) : MakeEntry($"LDAP://{dn}");
                using var searcher = new DirectorySearcher(parent)
                {
                    Filter = "(&(|(objectClass=user)(objectClass=group)(objectClass=computer))(distinguishedName=*))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000,
                    SizeLimit = 0,
                    ClientTimeout = _searchTimeout,
                    ServerTimeLimit = _searchTimeout
                };

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var escaped = EscapeLdap(q);
                    searcher.Filter = $"(&{searcher.Filter}(|(name=*{escaped}*)(sAMAccountName=*{escaped}*)(displayName=*{escaped}*)))";
                }

                searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "objectClass" });

                var results = searcher.FindAll().Cast<SearchResult>();
                var list = results.Skip(skip).Take(take).Select(r =>
                {
                    var name = GetProp<string>(r, "name") ?? "";
                    var dn2 = GetProp<string>(r, "distinguishedName") ?? "";
                    var objClass = "object";
                    if (r.Properties.Contains("objectClass") && r.Properties["objectClass"].Count > 0)
                        objClass = r.Properties["objectClass"][r.Properties["objectClass"].Count - 1]?.ToString() ?? "object";
                    return new ADObjectVm { Name = name, DistinguishedName = dn2, ObjectClass = objClass };
                }).ToList();

                return list;
            });
        }

        public Task<bool> CreateOUAsync(string parentDn, string name, string? description)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var parentEntry = MakeEntry($"LDAP://{parentDn}");
                    using var newOu = parentEntry.Children.Add($"OU={name}", "organizationalUnit");
                    if (!string.IsNullOrWhiteSpace(description))
                        newOu.Properties["description"].Value = description;
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
                    using var parent = MakeEntry(_ldapPath);
                    using var searcher = new DirectorySearcher(parent)
                    {
                        Filter = $"(&(objectClass=organizationalUnit)(distinguishedName={EscapeLdap(dn)}))",
                        SearchScope = SearchScope.Subtree,
                        PageSize = 1,
                        SizeLimit = 1
                    };
                    var res = searcher.FindOne();
                    if (res == null) return false;

                    using var ouEntry = res.GetDirectoryEntry();
                    var parentEntry = ouEntry.Parent;
                    parentEntry.Children.Remove(ouEntry);
                    parentEntry.CommitChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to delete OU {DN}", dn);
                    return false;
                }
            });
        }

        // متطلبات إضافية في واجهتك:
        public Task<bool> RenameOUAsync(string dn, string newName) => Task.Run(() =>
        {
            try
            {
                using var ou = MakeEntry($"LDAP://{dn}");
                ou.Rename($"OU={newName}");
                ou.CommitChanges();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to rename OU {DN} -> {New}", dn, newName);
                return false;
            }
        });

        public Task<bool> MoveObjectAsync(string dn, string newParentDn) => Task.Run(() =>
        {
            try
            {
                using var obj = MakeEntry($"LDAP://{dn}");
                using var parent = MakeEntry($"LDAP://{newParentDn}");
                obj.MoveTo(parent);
                parent.CommitChanges();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to move object {DN} -> {Parent}", dn, newParentDn);
                return false;
            }
        });

        public Task<bool> MoveUserBySamAsync(string sam, string newParentDn) => Task.Run(() =>
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam);
                if (user == null) return false;

                using var de = user.GetUnderlyingObject() as DirectoryEntry;
                if (de == null) return false;

                using var parent = MakeEntry($"LDAP://{newParentDn}");
                de.MoveTo(parent);
                parent.CommitChanges();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to move user {SAM} -> {Parent}", sam, newParentDn);
                return false;
            }
        });

        public Task<ADObjectVm?> GetObjectDetailsAsync(string dn) => Task.Run(() =>
        {
            using var entry = MakeEntry($"LDAP://{dn}");
            using var searcher = new DirectorySearcher(entry)
            {
                Filter = "(objectClass=*)",
                SearchScope = SearchScope.Base,
                PageSize = 1,
                SizeLimit = 1,
                ClientTimeout = _searchTimeout,
                ServerTimeLimit = _searchTimeout
            };
            searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName", "objectClass" });
            var one = searcher.FindOne();
            if (one == null) return null;

            var name = GetProp<string>(one, "name") ?? "";
            var objClass = "object";
            if (one.Properties.Contains("objectClass") && one.Properties["objectClass"].Count > 0)
                objClass = one.Properties["objectClass"][one.Properties["objectClass"].Count - 1]?.ToString() ?? "object";

            return new ADObjectVm { Name = name, DistinguishedName = dn, ObjectClass = objClass };
        });

        // ================= Users =================

        public Task<List<ADUserVm>> GetUsersAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                using var entry = MakeEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(&(objectClass=user)(!(objectClass=computer)))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000,
                    SizeLimit = 0,
                    ClientTimeout = _searchTimeout,
                    ServerTimeLimit = _searchTimeout
                };

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var escaped = EscapeLdap(q);
                    searcher.Filter = $"(&{searcher.Filter}(|(displayName=*{escaped}*)(sAMAccountName=*{escaped}*)(mail=*{escaped}*)))";
                }

                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "displayName","sAMAccountName","mail","distinguishedName","lastLogonTimestamp","userAccountControl"
                });

                var results = searcher.FindAll().Cast<SearchResult>();
                var list = results.Skip(skip).Take(take).Select(one => new ADUserVm
                {
                    DisplayName = GetProp<string>(one, "displayName") ?? GetProp<string>(one, "sAMAccountName") ?? "",
                    SAM = GetProp<string>(one, "sAMAccountName") ?? "",
                    Email = GetProp<string>(one, "mail") ?? "",
                    DistinguishedName = GetProp<string>(one, "distinguishedName") ?? "",
                    LastLogonUtc = ToDate(GetProp<long?>(one, "lastLogonTimestamp")),
                    Enabled = !HasFlag(GetProp<int?>(one, "userAccountControl") ?? 0, 0x2) // ACCOUNTDISABLE
                }).ToList();

                return list;
            });
        }

        public Task<ADUserVm?> GetUserAsync(string sam)
        {
            return Task.Run(() =>
            {
                using var entry = MakeEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(sam)}))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1,
                    SizeLimit = 1
                };
                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "displayName","sAMAccountName","mail","distinguishedName","lastLogonTimestamp","userAccountControl"
                });
                var one = searcher.FindOne();
                if (one == null) return null;

                return new ADUserVm
                {
                    DisplayName = GetProp<string>(one, "displayName") ?? GetProp<string>(one, "sAMAccountName") ?? "",
                    SAM = GetProp<string>(one, "sAMAccountName") ?? "",
                    Email = GetProp<string>(one, "mail") ?? "",
                    DistinguishedName = GetProp<string>(one, "distinguishedName") ?? "",
                    LastLogonUtc = ToDate(GetProp<long?>(one, "lastLogonTimestamp")),
                    Enabled = !HasFlag(GetProp<int?>(one, "userAccountControl") ?? 0, 0x2)
                };
            });
        }

        public Task<bool> SetUserEnabledAsync(string sam, bool enabled)
        {
            return Task.Run(() =>
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam);
                if (user == null) return false;
                user.Enabled = enabled;
                user.Save();
                return true;
            });
        }

        // الواجهة لديك تحتوي ResetPasswordAsync و ResetPasswordWithOptionsAsync
        public Task<bool> ResetPasswordAsync(string sam, string newPassword)
            => ResetPasswordWithOptionsAsync(sam, newPassword, false, false);

        public Task<bool> ResetPasswordWithOptionsAsync(string sam, string newPassword, bool forceChange, bool unlock)
        {
            return Task.Run(() =>
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam);
                if (user == null) return false;

                user.SetPassword(newPassword);
                if (forceChange) user.ExpirePasswordNow();
                if (unlock) user.UnlockAccount();
                user.Save();
                return true;
            });
        }

        public Task<bool> UnlockUserAsync(string sam)
        {
            return Task.Run(() =>
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam);
                if (user == null) return false;
                user.UnlockAccount();
                user.Save();
                return true;
            });
        }

        // قد تكون في الواجهة دالة متقدمة للبحث — نبقيها بسيطة لتوافق البنية وتبني المشروع
        public Task<List<ADUserVm>> GetUsersAdvancedAsync(UsersQueryOptions options)
{
    // دعم نطاق البحث حسب OU إن وُجد
    return Task.Run(() =>
    {
        var take = options?.Take > 0 ? options.Take : 50;
        var skip = options?.Skip >= 0 ? options.Skip : 0;
        var q = options?.Query;

        var searchRoot = string.IsNullOrWhiteSpace(options?.OuDistinguishedName)
            ? _ldapPath
            : $"LDAP://{options!.OuDistinguishedName}";

        using var entry = MakeEntry(searchRoot);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = "(&(objectClass=user)(!(objectClass=computer)))",
            SearchScope = SearchScope.Subtree,
            PageSize = 1000,
            SizeLimit = 0,
            ClientTimeout = _searchTimeout,
            ServerTimeLimit = _searchTimeout
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var escaped = EscapeLdap(q!);
            searcher.Filter = $"(&{searcher.Filter}(|(displayName=*{escaped}*)(sAMAccountName=*{escaped}*)(mail=*{escaped}*)))";
        }

        searcher.PropertiesToLoad.AddRange(new[]
        {
            "displayName","sAMAccountName","mail","distinguishedName","lastLogonTimestamp","userAccountControl"
        });

        var results = searcher.FindAll().Cast<SearchResult>();
        return results.Skip(skip).Take(take).Select(one => new ADUserVm
        {
            DisplayName = GetProp<string>(one, "displayName") ?? GetProp<string>(one, "sAMAccountName") ?? "",
            SAM = GetProp<string>(one, "sAMAccountName") ?? "",
            Email = GetProp<string>(one, "mail") ?? "",
            DistinguishedName = GetProp<string>(one, "distinguishedName") ?? "",
            LastLogonUtc = ToDate(GetProp<long?>(one, "lastLogonTimestamp")),
            Enabled = !HasFlag(GetProp<int?>(one, "userAccountControl") ?? 0, 0x2)
        }).ToList();
    });
}


        public Task<ADUserVm?> GetUserDetailsAsync(string? sam, string? dn)
        {
            if (!string.IsNullOrWhiteSpace(sam))
                return GetUserAsync(sam);

            if (string.IsNullOrWhiteSpace(dn))
                return Task.FromResult<ADUserVm?>(null);

            return Task.Run(() =>
            {
                using var entry = MakeEntry($"LDAP://{dn}");
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(objectClass=user)",
                    SearchScope = SearchScope.Base,
                    PageSize = 1,
                    SizeLimit = 1
                };
                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "displayName","sAMAccountName","mail","distinguishedName","lastLogonTimestamp","userAccountControl"
                });
                var one = searcher.FindOne();
                if (one == null) return null;

                return new ADUserVm
                {
                    DisplayName = GetProp<string>(one, "displayName") ?? GetProp<string>(one, "sAMAccountName") ?? "",
                    SAM = GetProp<string>(one, "sAMAccountName") ?? "",
                    Email = GetProp<string>(one, "mail") ?? "",
                    DistinguishedName = GetProp<string>(one, "distinguishedName") ?? "",
                    LastLogonUtc = ToDate(GetProp<long?>(one, "lastLogonTimestamp")),
                    Enabled = !HasFlag(GetProp<int?>(one, "userAccountControl") ?? 0, 0x2)
                };
            });
        }

        // ================= Groups =================

        public Task<List<ADGroupVm>> GetGroupsAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                using var entry = MakeEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(objectCategory=group)",
                    PageSize = 500,
                    SearchScope = SearchScope.Subtree,
                    ClientTimeout = _searchTimeout,
                    ServerTimeLimit = _searchTimeout
                };

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var escaped = EscapeLdap(q);
                    searcher.Filter = $"(&(objectCategory=group)(|(name=*{escaped}*)(sAMAccountName=*{escaped}*)))";
                }

                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "name","sAMAccountName","distinguishedName","description","member"
                });

                var results = searcher.FindAll().Cast<SearchResult>();
                return results
                    .Select(r => new ADGroupVm
                    {
                        Name = GetProp<string>(r, "name") ?? "",
                        SAM = GetProp<string>(r, "sAMAccountName") ?? "",
                        DistinguishedName = GetProp<string>(r, "distinguishedName") ?? "",
                        Description = GetProp<string>(r, "description") ?? "",
                        MemberCount = r.Properties["member"]?.Count ?? 0
                    })
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            });
        }

        public Task<List<ADUserVm>> GetGroupMembersAsync(string groupSam)
        {
            return Task.Run(() =>
            {
                using var entry = MakeEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectCategory=group)(sAMAccountName={EscapeLdap(groupSam)}))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1,
                    SizeLimit = 1,
                    ClientTimeout = _searchTimeout,
                    ServerTimeLimit = _searchTimeout
                };
                searcher.PropertiesToLoad.AddRange(new[] { "distinguishedName", "member" });

                var grp = searcher.FindOne();
                if (grp == null) return new List<ADUserVm>();

                var members = grp.Properties["member"];
                if (members == null || members.Count == 0) return new List<ADUserVm>();

                var list = new List<ADUserVm>(members.Count);
                foreach (var m in members)
                {
                    var memberDn = m?.ToString();
                    if (string.IsNullOrWhiteSpace(memberDn)) continue;

                    using var mEntry = MakeEntry($"LDAP://{memberDn}");
                    using var mSearcher = new DirectorySearcher(mEntry)
                    {
                        Filter = "(objectClass=user)",
                        SearchScope = SearchScope.Base,
                        PageSize = 1,
                        SizeLimit = 1,
                        ClientTimeout = _searchTimeout,
                        ServerTimeLimit = _searchTimeout
                    };
                    mSearcher.PropertiesToLoad.AddRange(new[] { "displayName", "sAMAccountName", "mail", "distinguishedName", "lastLogonTimestamp", "userAccountControl" });

                    var one = mSearcher.FindOne();
                    if (one == null) continue;

                    list.Add(new ADUserVm
                    {
                        DisplayName = GetProp<string>(one, "displayName") ?? GetProp<string>(one, "sAMAccountName") ?? "",
                        SAM = GetProp<string>(one, "sAMAccountName") ?? "",
                        Email = GetProp<string>(one, "mail") ?? "",
                        DistinguishedName = GetProp<string>(one, "distinguishedName") ?? "",
                        LastLogonUtc = ToDate(GetProp<long?>(one, "lastLogonTimestamp")),
                        Enabled = !HasFlag(GetProp<int?>(one, "userAccountControl") ?? 0, 0x2)
                    });
                }

                return list;
            });
        }

        // دوال عضوية المجموعات — مطابقة لأسماء الواجهة لديك
        public Task<bool> AddUserToGroupAsync(string userSam, string groupSam) => Task.Run(() =>
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, userSam);
                var group = GroupPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, groupSam);
                if (user == null || group == null) return false;
                if (!group.Members.Contains(user)) group.Members.Add(user);
                group.Save();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to add {User} to {Group}", userSam, groupSam);
                return false;
            }
        });

        public Task<bool> RemoveUserFromGroupAsync(string userSam, string groupSam) => Task.Run(() =>
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, userSam);
                var group = GroupPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, groupSam);
                if (user == null || group == null) return false;
                if (group.Members.Contains(user)) group.Members.Remove(user);
                group.Save();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to remove {User} from {Group}", userSam, groupSam);
                return false;
            }
        });
    }
}
