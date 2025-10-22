using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace admgmt_backend.Services
{
    /*public sealed class OUVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string? Description { get; set; }
    }*/

    /*public sealed class ADUserVm
    {
        public string DisplayName { get; set; } = "";
        public string SAM { get; set; } = "";
        public string Email { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public DateTime? LastLogonUtc { get; set; }
        public bool Enabled { get; set; }
    }*/

    /*public sealed class ADGroupVm
    {
        public string Name { get; set; } = "";
        public string SAM { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string? Description { get; set; }
        public int MemberCount { get; set; }
    }*/

   /* public sealed class ADObjectVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string ObjectClass { get; set; } = "";
    }*/

   /* public sealed class ADObjectDetailsVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string ObjectClass { get; set; } = "";
        public string? SAM { get; set; }
        public string? Email { get; set; }
        public DateTime? LastLogonUtc { get; set; }
        public bool? Enabled { get; set; }
        public string? Description { get; set; }
        public int? MemberCount { get; set; }
        public List<string>? MembersSam { get; set; }
    }*/

    /*public sealed class UsersQueryOptions
    {
        public string? Query { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
        public string? OuDistinguishedName { get; set; }
    }*/

    public sealed class PowerShellAdService : IADService
    {
        private readonly ILogger<PowerShellAdService> _log;
        private readonly string _baseDn;

        public PowerShellAdService(IConfiguration cfg, ILogger<PowerShellAdService> log)
        {
            _log = log;
            _baseDn = cfg["AD:BaseDN"] ?? "";
            if (string.IsNullOrWhiteSpace(_baseDn))
                throw new ArgumentException("Missing config AD:BaseDN (e.g., DC=UQU,DC=LOCAL)");
        }

        public Task<List<OUVm>> GetRootOusAsync() => GetOUsAsync(null, 0, 1000);

        public Task<List<OUVm>> GetOUsAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                var script = @"
param($BaseDN, $FilterText)
$ous = Get-ADOrganizationalUnit -LDAPFilter '(objectClass=organizationalUnit)' -SearchBase $BaseDN -SearchScope OneLevel -ResultPageSize 1000 -ErrorAction Stop |
    Select-Object Name, DistinguishedName, @{N='Description';E={(Get-ADOrganizationalUnit $_.DistinguishedName -Properties description).description}}
if ($FilterText) { $ous = $ous | Where-Object { $_.Name -like ""*$FilterText*"" -or ($_.Description -like ""*$FilterText*"") } }
$ous
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["BaseDN"] = _baseDn,
                    ["FilterText"] = q ?? ""
                });

                var list = res.Select(o => new OUVm
                {
                    Name = PowerShellRunner.GetProp<string>(o, "Name", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    Description = PowerShellRunner.GetProp<string>(o, "Description", null)
                }).Skip(skip).Take(take).ToList();
                return list;
            });
        }

        public Task<List<OUVm>> GetChildOUsAsync(string? parentDn)
        {
            return Task.Run(() =>
            {
                var searchBase = string.IsNullOrWhiteSpace(parentDn) ? _baseDn : parentDn;
                var script = @"
param($SearchBase)
Get-ADOrganizationalUnit -LDAPFilter '(objectClass=organizationalUnit)' -SearchBase $SearchBase -SearchScope OneLevel -ResultPageSize 1000 -ErrorAction Stop |
    Select-Object Name, DistinguishedName, @{N='Description';E={(Get-ADOrganizationalUnit $_.DistinguishedName -Properties description).description}}
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["SearchBase"] = searchBase
                });

                return res.Select(o => new OUVm
                {
                    Name = PowerShellRunner.GetProp<string>(o, "Name", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    Description = PowerShellRunner.GetProp<string>(o, "Description", null)
                }).ToList();
            });
        }

        public Task<List<ADObjectVm>> GetOuObjectsAsync(string dn, int skip, int take, string? q)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Base, $Q)
$filter = '(|(objectClass=user)(objectClass=group)(objectClass=computer))'
$objs = Get-ADObject -LDAPFilter $filter -SearchBase $Base -SearchScope Subtree -ResultPageSize 1000 -ErrorAction Stop |
    Select-Object Name, DistinguishedName, ObjectClass
if ($Q) { $objs = $objs | Where-Object { $_.Name -like ""*$Q*"" } }
$objs
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["Base"] = string.IsNullOrWhiteSpace(dn) ? _baseDn : dn,
                    ["Q"] = q ?? ""
                });

                return res.Skip(skip).Take(take).Select(o => new ADObjectVm
                {
                    Name = PowerShellRunner.GetProp<string>(o, "Name", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    ObjectClass = PowerShellRunner.GetProp<string>(o, "ObjectClass", "")
                }).ToList();
            });
        }

        public Task<bool> CreateOUAsync(string parentDn, string name, string? description)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Parent, $Name, $Desc)
New-ADOrganizationalUnit -Name $Name -Path $Parent -ProtectedFromAccidentalDeletion:$false -ErrorAction Stop | Out-Null
if ($Desc) { Set-ADOrganizationalUnit -Identity ""OU=$Name,$Parent"" -Description $Desc -ErrorAction Stop }
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["Parent"] = parentDn, ["Name"] = name, ["Desc"] = description ?? ""
                });
                return true;
            });
        }

        public Task<bool> DeleteOUAsync(string dn)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Dn)
Set-ADOrganizationalUnit -Identity $Dn -ProtectedFromAccidentalDeletion:$false -ErrorAction SilentlyContinue
Remove-ADOrganizationalUnit -Identity $Dn -Confirm:$false -Recursive -ErrorAction Stop
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Dn"] = dn });
                return true;
            });
        }

        public Task<bool> RenameOUAsync(string dn, string newName)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Dn, $NewName)
Rename-ADObject -Identity $Dn -NewName ""OU=$NewName"" -ErrorAction Stop
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Dn"] = dn, ["NewName"] = newName });
                return true;
            });
        }

        public Task<bool> MoveObjectAsync(string dn, string newParentDn)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Dn, $Parent)
Move-ADObject -Identity $Dn -TargetPath $Parent -ErrorAction Stop
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Dn"] = dn, ["Parent"] = newParentDn });
                return true;
            });
        }

        public Task<bool> MoveUserBySamAsync(string sam, string newParentDn)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Sam, $Parent)
$u = Get-ADUser -Identity $Sam -ErrorAction Stop
Move-ADObject -Identity $u.DistinguishedName -TargetPath $Parent -ErrorAction Stop
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Sam"] = sam, ["Parent"] = newParentDn });
                return true;
            });
        }

        public Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Dn)
$o = Get-ADObject -Identity $Dn -Properties * -ErrorAction Stop
if ($o.ObjectClass -eq 'user') {
    $u = Get-ADUser -Identity $Dn -Properties displayName,mail,lastLogonTimestamp,userAccountControl,sAMAccountName -ErrorAction Stop
    [PSCustomObject]@{
        Name = $u.Name
        DistinguishedName = $u.DistinguishedName
        ObjectClass = 'user'
        SAM = $u.SamAccountName
        Email = $u.Mail
        LastLogonUtc = if ($u.lastLogonTimestamp) { [DateTime]::FromFileTimeUtc([Int64]$u.lastLogonTimestamp) } else { $null }
        Enabled = -not ([bool]($u.userAccountControl -band 2))
        Description = $u.Description
        MemberCount = $null
        MembersSam = $null
    }
}
elseif ($o.ObjectClass -eq 'group') {
    $g = Get-ADGroup -Identity $Dn -Properties Description,member -ErrorAction Stop
    $members = @()
    if ($g.member) {
        foreach ($m in $g.member) {
            try {
                $mu = Get-ADUser -Identity $m -ErrorAction Stop
                $members += $mu.SamAccountName
            } catch {}
        }
    }
    [PSCustomObject]@{
        Name = $g.Name
        DistinguishedName = $g.DistinguishedName
        ObjectClass = 'group'
        SAM = $null
        Email = $null
        LastLogonUtc = $null
        Enabled = $null
        Description = $g.Description
        MemberCount = $members.Count
        MembersSam = $members
    }
}
else {
    [PSCustomObject]@{
        Name = $o.Name
        DistinguishedName = $o.DistinguishedName
        ObjectClass = $o.ObjectClass
        SAM = $null
        Email = $null
        LastLogonUtc = $null
        Enabled = $null
        Description = $o.Description
        MemberCount = $null
        MembersSam = $null
    }
}
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Dn"] = dn });
                var o = res.FirstOrDefault();
                if (o == null) return null;

                DateTime? ParseDate(object? v)
                {
                    if (v == null) return null;
                    if (v is DateTime dt) return dt.ToUniversalTime();
                    if (DateTime.TryParse(v.ToString(), out var d)) return d.ToUniversalTime();
                    return null;
                }

                var details = new ADObjectDetailsVm
                {
                    Name = PowerShellRunner.GetProp<string>(o, "Name", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    ObjectClass = PowerShellRunner.GetProp<string>(o, "ObjectClass", ""),
                    SAM = PowerShellRunner.GetProp<string>(o, "SAM", null),
                    Email = PowerShellRunner.GetProp<string>(o, "Email", null),
                    LastLogonUtc = ParseDate(o.Properties["LastLogonUtc"]?.Value),
                    Enabled = PowerShellRunner.GetProp<bool?>(o, "Enabled", null),
                    Description = PowerShellRunner.GetProp<string>(o, "Description", null),
                    MemberCount = PowerShellRunner.GetProp<int?>(o, "MemberCount", null),
                    MembersSam = (o.Properties["MembersSam"]?.Value as System.Collections.IEnumerable)?.Cast<object>().Select(x => x?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                };
                return details;
            });
        }

        public Task<List<ADUserVm>> GetUsersAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                var script = @"
param($BaseDN, $Q)
$users = if ($Q) {
    Get-ADUser -Filter ""(displayName -like '*$Q*' -or SamAccountName -like '*$Q*' -or mail -like '*$Q*')"" -SearchBase $BaseDN -SearchScope Subtree -Properties displayName,mail,lastLogonTimestamp,userAccountControl -ErrorAction Stop
} else {
    Get-ADUser -Filter * -SearchBase $BaseDN -SearchScope Subtree -Properties displayName,mail,lastLogonTimestamp,userAccountControl -ErrorAction Stop
}
$users | Select-Object @{N='DisplayName';E={$_.DisplayName}}, @{N='SAM';E={$_.SamAccountName}}, @{N='Email';E={$_.Mail}},
    DistinguishedName, @{N='LastLogonUtc';E={ if ($_.lastLogonTimestamp) { [DateTime]::FromFileTimeUtc([Int64]$_.lastLogonTimestamp) } else { $null } }},
    @{N='Enabled';E={ -not ([bool]($_.userAccountControl -band 2)) }}
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["BaseDN"] = _baseDn,
                    ["Q"] = q ?? ""
                });

                return res.Skip(skip).Take(take).Select(o => new ADUserVm
                {
                    DisplayName = PowerShellRunner.GetProp<string>(o, "DisplayName", ""),
                    SAM = PowerShellRunner.GetProp<string>(o, "SAM", ""),
                    Email = PowerShellRunner.GetProp<string>(o, "Email", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    LastLogonUtc = (o.Properties["LastLogonUtc"]?.Value as DateTime?)?.ToUniversalTime(),
                    Enabled = PowerShellRunner.GetProp<bool>(o, "Enabled", false)
                }).ToList();
            });
        }

        public Task<ADUserVm?> GetUserAsync(string sam)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Sam)
$u = Get-ADUser -Identity $Sam -Properties displayName,mail,lastLogonTimestamp,userAccountControl -ErrorAction Stop
[PSCustomObject]@{
    DisplayName = $u.DisplayName
    SAM = $u.SamAccountName
    Email = $u.Mail
    DistinguishedName = $u.DistinguishedName
    LastLogonUtc = if ($u.lastLogonTimestamp) { [DateTime]::FromFileTimeUtc([Int64]$u.lastLogonTimestamp) } else { $null }
    Enabled = -not ([bool]($u.userAccountControl -band 2))
}
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Sam"] = sam });
                var o = res.FirstOrDefault();
                if (o == null) return null;
                return new ADUserVm
                {
                    DisplayName = PowerShellRunner.GetProp<string>(o, "DisplayName", ""),
                    SAM = PowerShellRunner.GetProp<string>(o, "SAM", ""),
                    Email = PowerShellRunner.GetProp<string>(o, "Email", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    LastLogonUtc = (o.Properties["LastLogonUtc"]?.Value as DateTime?)?.ToUniversalTime(),
                    Enabled = PowerShellRunner.GetProp<bool>(o, "Enabled", false)
                };
            });
        }

        public Task<bool> SetUserEnabledAsync(string sam, bool enabled)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Sam, $Enabled)
if ($Enabled) { Enable-ADAccount -Identity $Sam -ErrorAction Stop } else { Disable-ADAccount -Identity $Sam -ErrorAction Stop }
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Sam"] = sam, ["Enabled"] = enabled });
                return true;
            });
        }

        public Task<bool> ResetPasswordWithOptionsAsync(string sam, string newPassword, bool forceChange, bool unlock)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Sam, $Pwd, $ForceChange, $Unlock)
Set-ADAccountPassword -Identity $Sam -Reset -NewPassword (ConvertTo-SecureString $Pwd -AsPlainText -Force) -ErrorAction Stop
if ($ForceChange) { Set-ADUser -Identity $Sam -ChangePasswordAtLogon $true -ErrorAction Stop }
if ($Unlock) { Unlock-ADAccount -Identity $Sam -ErrorAction SilentlyContinue }
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["Sam"] = sam, ["Pwd"] = newPassword, ["ForceChange"] = forceChange, ["Unlock"] = unlock
                });
                return true;
            });
        }

        public Task<bool> ResetPasswordAsync(string sam, string newPassword)
            => ResetPasswordWithOptionsAsync(sam, newPassword, false, false);

        public Task<bool> UnlockUserAsync(string sam)
            => ResetPasswordWithOptionsAsync(sam, "dummy", false, true);

        public Task<List<ADUserVm>> GetUsersAdvancedAsync(UsersQueryOptions options)
            => GetUsersAsync(options?.Query, options?.Skip ?? 0, options?.Take ?? 50);

        public Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? dn)
        {
            if (!string.IsNullOrWhiteSpace(dn)) return GetObjectDetailsAsync(dn!);
            if (!string.IsNullOrWhiteSpace(sam))
            {
                return Task.Run(async () =>
                {
                    var u = await GetUserAsync(sam!);
                    if (u == null) return null;
                    return new ADObjectDetailsVm
                    {
                        Name = u.DisplayName ?? u.SAM,
                        DistinguishedName = u.DistinguishedName,
                        ObjectClass = "user",
                        SAM = u.SAM,
                        Email = u.Email,
                        LastLogonUtc = u.LastLogonUtc,
                        Enabled = u.Enabled
                    };
                });
            }
            return Task.FromResult<ADObjectDetailsVm?>(null);
        }

        public Task<List<ADGroupVm>> GetGroupsAsync(string? q, int skip, int take)
        {
            return Task.Run(() =>
            {
                var script = @"
param($BaseDN, $Q)
$groups = if ($Q) {
    Get-ADGroup -Filter ""(Name -like '*$Q*' -or SamAccountName -like '*$Q*')"" -SearchBase $BaseDN -SearchScope Subtree -Properties description,member -ErrorAction Stop
} else {
    Get-ADGroup -Filter * -SearchBase $BaseDN -SearchScope Subtree -Properties description,member -ErrorAction Stop
}
$groups | Select-Object @{N='Name';E={$_.Name}}, @{N='SAM';E={$_.SamAccountName}}, DistinguishedName, @{N='Description';E={$_.Description}}, @{N='MemberCount';E={($_.member | Measure-Object).Count}}
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?>
                {
                    ["BaseDN"] = _baseDn, ["Q"] = q ?? ""
                });

                return res.Skip(skip).Take(take).Select(o => new ADGroupVm
                {
                    Name = PowerShellRunner.GetProp<string>(o, "Name", ""),
                    SAM = PowerShellRunner.GetProp<string>(o, "SAM", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    Description = PowerShellRunner.GetProp<string>(o, "Description", null),
                    MemberCount = PowerShellRunner.GetProp<int>(o, "MemberCount", 0)
                }).ToList();
            });
        }

        public Task<List<ADUserVm>> GetGroupMembersAsync(string groupSam)
        {
            return Task.Run(() =>
            {
                var script = @"
param($Sam)
Get-ADGroupMember -Identity $Sam -Recursive -ErrorAction Stop |
    Where-Object { $_.ObjectClass -eq 'user' } |
    ForEach-Object {
        $u = Get-ADUser -Identity $_.DistinguishedName -Properties displayName,mail,lastLogonTimestamp,userAccountControl,SamAccountName
        [PSCustomObject]@{
            DisplayName = $u.DisplayName
            SAM = $u.SamAccountName
            Email = $u.Mail
            DistinguishedName = $u.DistinguishedName
            LastLogonUtc = if ($u.lastLogonTimestamp) { [DateTime]::FromFileTimeUtc([Int64]$u.lastLogonTimestamp) } else { $null }
            Enabled = -not ([bool]($u.userAccountControl -band 2))
        }
    }
";
                var res = PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["Sam"] = groupSam });
                return res.Select(o => new ADUserVm
                {
                    DisplayName = PowerShellRunner.GetProp<string>(o, "DisplayName", ""),
                    SAM = PowerShellRunner.GetProp<string>(o, "SAM", ""),
                    Email = PowerShellRunner.GetProp<string>(o, "Email", ""),
                    DistinguishedName = PowerShellRunner.GetProp<string>(o, "DistinguishedName", ""),
                    LastLogonUtc = (o.Properties["LastLogonUtc"]?.Value as DateTime?)?.ToUniversalTime(),
                    Enabled = PowerShellRunner.GetProp<bool>(o, "Enabled", false)
                }).ToList();
            });
        }

        public Task<bool> AddUserToGroupAsync(string userSam, string groupSam)
        {
            return Task.Run(() =>
            {
                var script = @"
param($UserSam, $GroupSam)
Add-ADGroupMember -Identity $GroupSam -Members $UserSam -ErrorAction Stop
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["UserSam"] = userSam, ["GroupSam"] = groupSam });
                return true;
            });
        }

        public Task<bool> RemoveUserFromGroupAsync(string userSam, string groupSam)
        {
            return Task.Run(() =>
            {
                var script = @"
param($UserSam, $GroupSam)
Remove-ADGroupMember -Identity $GroupSam -Members $UserSam -Confirm:$false -ErrorAction Stop
$true
";
                PowerShellRunner.Invoke(script, new Dictionary<string, object?> { ["UserSam"] = userSam, ["GroupSam"] = groupSam });
                return true;
            });
        }
    }
}