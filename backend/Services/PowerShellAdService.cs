using Microsoft.Extensions.Options;
using System.Management.Automation;

namespace admgmt_backend.Services
{
    public class PowerShellAdService : IADService
    {
        private readonly PowerShellRunner _ps;
        private readonly AdOptions _opt;

        // مهم: ما نسوي أي عمل هنا غير التخزين. لا Import-Module ولا تحقق اتصال.
        public PowerShellAdService(PowerShellRunner ps, IOptions<AdOptions> opt)
        {
            _ps = ps;
            _opt = opt?.Value ?? new AdOptions();
        }

        // Helpers
        private string CredExpr =>
            $"$sec = ConvertTo-SecureString '{_opt.ServicePassword.Replace("'", "''")}' -AsPlainText -Force; " +
            $"$cred = New-Object System.Management.Automation.PSCredential('{_opt.ServiceUserUPN}', $sec);";

        private string ServerExpr => string.IsNullOrWhiteSpace(_opt.DomainController) ? "" : $" -Server {_opt.DomainController}";
        private string CommonCred  => $"{CredExpr} ";

        // ----------------- OUs -----------------
        public async Task<List<OUVm>> GetOUsAsync(string? parentDn, int page, int pageSize)
        {
            try
            {
                string baseDn = string.IsNullOrWhiteSpace(parentDn) ? _opt.BaseDN : parentDn;
                string script =
                    CommonCred +
                    $"Import-Module ActiveDirectory; " +
                    $"Get-ADOrganizationalUnit -Filter * -SearchBase '{baseDn}' -ResultSetSize {pageSize}{ServerExpr} -Credential $cred | " +
                    "Select-Object Name, DistinguishedName";

                var result = await _ps.Invoke(script);
                return result.Select(o => new OUVm(
                    PowerShellRunner.GetProp<string>(o, "Name") ?? "",
                    PowerShellRunner.GetProp<string>(o, "DistinguishedName") ?? "",
                    true
                )).ToList();
            }
            catch (Exception ex)
            {
                // إرجاع قائمة فارغة بدلاً من رمي الاستثناء
                Console.WriteLine($"Error getting OUs: {ex.Message}");
                return new List<OUVm>();
            }
        }

        public Task<List<OUVm>> GetChildOUsAsync(string? parentDn) => GetOUsAsync(parentDn, 1, 1000);

        public async Task<(List<ADObjectVm> Items, int Total)> GetOuObjectsAsync(string ouDn, int page, int pageSize)
        {
            try
            {
                string sb = string.IsNullOrWhiteSpace(ouDn) ? _opt.BaseDN : ouDn;
                string script =
                    CommonCred +
                    "Import-Module ActiveDirectory; " +
                    // Users + Groups (ممكن تضيف Computers لو تحتاج)
                    $"$u = Get-ADUser -Filter * -SearchBase '{sb}' -ResultSetSize {pageSize}{ServerExpr} -Credential $cred -Properties * | " +
                    "Select-Object Name, DistinguishedName,@{n='ObjectClass';e={'user'}}; " +
                    $"$g = Get-ADGroup -Filter * -SearchBase '{sb}' -ResultSetSize {pageSize}{ServerExpr} -Credential $cred -Properties * | " +
                    "Select-Object Name, DistinguishedName,@{n='ObjectClass';e={'group'}}; " +
                    "$u + $g";

                var result = await _ps.Invoke(script);
                var items = result.Select(o => new ADObjectVm(
                    PowerShellRunner.GetProp<string>(o, "Name") ?? "",
                    PowerShellRunner.GetProp<string>(o, "DistinguishedName") ?? "",
                    PowerShellRunner.GetProp<string>(o, "ObjectClass") ?? ""
                )).ToList();

                return (items, items.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting OU objects: {ex.Message}");
                return (new List<ADObjectVm>(), 0);
            }
        }

        public async Task CreateOUAsync(string parentDn, string name)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"New-ADOrganizationalUnit -Name '{name}' -Path '{parentDn}'{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        public async Task DeleteOUAsync(string dn)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"Remove-ADOrganizationalUnit -Identity '{dn}' -Confirm:$false{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        public async Task RenameOUAsync(string dn, string newName)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"Rename-ADObject -Identity '{dn}' -NewName '{newName}'{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        public async Task MoveObjectAsync(string objectDn, string targetOuDn)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"Move-ADObject -Identity '{objectDn}' -TargetPath '{targetOuDn}'{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        public async Task MoveUserBySamAsync(string sam, string targetOuDn)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"$u = Get-ADUser -Identity '{sam}'{ServerExpr} -Credential $cred; " +
                "if ($u) { " +
                    $"Move-ADObject -Identity $u.DistinguishedName -TargetPath '{targetOuDn}'{ServerExpr} -Credential $cred; " +
                "}";
            await _ps.Invoke(script);
        }

        // ------------- Generic object -------------
        public async Task<ADObjectDetailsVm?> GetObjectDetailsAsync(string dn)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"Get-ADObject -Identity '{dn}' -Properties *{ServerExpr} -Credential $cred";
            var r = await _ps.Invoke(script);
            var o = r.FirstOrDefault();
            if (o == null) return null;

            var details = new ADObjectDetailsVm { DistinguishedName = dn };
            var dict = new Dictionary<string, List<string>>();
            foreach (var p in o.Properties)
            {
                var key = p.Name;
                var vals = new List<string>();
                foreach (var v in p.Value is System.Collections.IEnumerable en && p.Value is not string
                         ? en.Cast<object>() : new[] { p.Value })
                {
                    if (v != null) vals.Add(v.ToString()!);
                }
                dict[key] = vals;
            }
            details.Attributes = dict;
            return details;
        }

        // ---------------- Users ----------------
        public async Task<(List<ADUserVm> Items, int Total)> GetUsersAsync(string? search, int page, int pageSize)
        {
            try
            {
                string filter;
                if (string.IsNullOrWhiteSpace(search))
                {
                    filter = "*";
                }
                else
                {
                    // البحث في displayName, samAccountName, userPrincipalName
                    filter = $"(displayName -like '*{search}*' -or samAccountName -like '*{search}*' -or userPrincipalName -like '*{search}*')";
                }
                
                string script =
                    CommonCred +
                    "Import-Module ActiveDirectory; " +
                    $"Get-ADUser -Filter \"{filter}\" -SearchBase '{_opt.BaseDN}' -ResultSetSize {pageSize}{ServerExpr} -Credential $cred -Properties userPrincipalName,displayName,enabled,lockedout | " +
                    "Select-Object SamAccountName, userPrincipalName, displayName, enabled, lockedout";

                var result = await _ps.Invoke(script);
                var items = result.Select(o => new ADUserVm(
                    PowerShellRunner.GetProp<string>(o, "SamAccountName") ?? "",
                    PowerShellRunner.GetProp<string>(o, "userPrincipalName") ?? "",
                    PowerShellRunner.GetProp<string>(o, "displayName") ?? "",
                    PowerShellRunner.GetProp<bool>(o, "enabled"),
                    PowerShellRunner.GetProp<bool>(o, "lockedout")
                )).ToList();

                return (items, items.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting users: {ex.Message}");
                return (new List<ADUserVm>(), 0);
            }
        }

        public async Task<(List<ADUserVm> Items, int Total)> GetUsersAdvancedAsync(UsersQueryOptions options)
        {
            try
            {
                string filter = "*";
                
                // بناء الفلتر بناءً على المعايير
                var conditions = new List<string>();
                
                if (!string.IsNullOrWhiteSpace(options.Search))
                {
                    conditions.Add($"(displayName -like '*{options.Search}*' -or samAccountName -like '*{options.Search}*' -or userPrincipalName -like '*{options.Search}*')");
                }
                
                if (options.Enabled.HasValue)
                {
                    conditions.Add($"enabled -eq {options.Enabled.Value.ToString().ToLower()}");
                }
                
                if (options.Locked.HasValue)
                {
                    conditions.Add($"lockedout -eq {options.Locked.Value.ToString().ToLower()}");
                }
                
                if (conditions.Count > 0)
                {
                    filter = string.Join(" -and ", conditions);
                }
                
                string searchBase = string.IsNullOrWhiteSpace(options.OuDistinguishedName) ? _opt.BaseDN : options.OuDistinguishedName;
                
                string script =
                    CommonCred +
                    "Import-Module ActiveDirectory; " +
                    $"Get-ADUser -Filter \"{filter}\" -SearchBase '{searchBase}' -ResultSetSize {options.PageSize}{ServerExpr} -Credential $cred -Properties userPrincipalName,displayName,enabled,lockedout | " +
                    "Select-Object SamAccountName, userPrincipalName, displayName, enabled, lockedout";

                var result = await _ps.Invoke(script);
                var items = result.Select(o => new ADUserVm(
                    PowerShellRunner.GetProp<string>(o, "SamAccountName") ?? "",
                    PowerShellRunner.GetProp<string>(o, "userPrincipalName") ?? "",
                    PowerShellRunner.GetProp<string>(o, "displayName") ?? "",
                    PowerShellRunner.GetProp<bool>(o, "enabled"),
                    PowerShellRunner.GetProp<bool>(o, "lockedout")
                )).ToList();

                return (items, items.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting users advanced: {ex.Message}");
                return (new List<ADUserVm>(), 0);
            }
        }

        public async Task<ADUserVm?> GetUserAsync(string? samOrUpn)
        {
            if (string.IsNullOrWhiteSpace(samOrUpn)) return null;
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"Get-ADUser -Identity '{samOrUpn}'{ServerExpr} -Credential $cred -Properties userPrincipalName,displayName,enabled,lockedout | " +
                "Select-Object SamAccountName, userPrincipalName, displayName, enabled, lockedout";
            var r = await _ps.Invoke(script);
            var o = r.FirstOrDefault();
            if (o == null) return null;

            return new ADUserVm(
                PowerShellRunner.GetProp<string>(o, "SamAccountName") ?? "",
                PowerShellRunner.GetProp<string>(o, "userPrincipalName") ?? "",
                PowerShellRunner.GetProp<string>(o, "displayName") ?? "",
                PowerShellRunner.GetProp<bool>(o, "enabled"),
                PowerShellRunner.GetProp<bool>(o, "lockedout")
            );
        }

        public async Task<ADObjectDetailsVm?> GetUserDetailsAsync(string? sam, string? upn)
        {
            if (string.IsNullOrWhiteSpace(sam) && string.IsNullOrWhiteSpace(upn))
                return null;

            string identity = sam ?? upn!;
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"$user = Get-ADUser -Identity '{identity}'{ServerExpr} -Credential $cred -Properties *; " +
                "$groups = Get-ADPrincipalGroupMembership -Identity $user{ServerExpr} -Credential $cred | Select-Object Name, DistinguishedName; " +
                "$user | Select-Object @{n='Name';e={$_.Name}}, @{n='DisplayName';e={$_.DisplayName}}, @{n='SamAccountName';e={$_.SamAccountName}}, @{n='UserPrincipalName';e={$_.UserPrincipalName}}, @{n='EmailAddress';e={$_.EmailAddress}}, @{n='Enabled';e={$_.Enabled}}, @{n='LockedOut';e={$_.LockedOut}}, @{n='LastLogonDate';e={$_.LastLogonDate}}, @{n='PasswordLastSet';e={$_.PasswordLastSet}}, @{n='AccountExpirationDate';e={$_.AccountExpirationDate}}, @{n='DistinguishedName';e={$_.DistinguishedName}}, @{n='Groups';e={$groups}}";

            var result = await _ps.Invoke(script);
            var user = result.FirstOrDefault();
            if (user == null) return null;

            var details = new ADObjectDetailsVm
            {
                Name = PowerShellRunner.GetProp<string>(user, "Name") ?? "",
                DisplayName = PowerShellRunner.GetProp<string>(user, "DisplayName") ?? "",
                SamAccountName = PowerShellRunner.GetProp<string>(user, "SamAccountName") ?? "",
                Email = PowerShellRunner.GetProp<string>(user, "EmailAddress") ?? "",
                Enabled = PowerShellRunner.GetProp<bool>(user, "Enabled"),
                Locked = PowerShellRunner.GetProp<bool>(user, "LockedOut"),
                DistinguishedName = PowerShellRunner.GetProp<string>(user, "DistinguishedName") ?? "",
                ObjectClass = "user"
            };

            // إضافة المعلومات الإضافية
            var lastLogon = PowerShellRunner.GetProp<DateTime?>(user, "LastLogonDate");
            if (lastLogon.HasValue)
                details.LastLogonUtc = lastLogon.Value;

            var passwordLastSet = PowerShellRunner.GetProp<DateTime?>(user, "PasswordLastSet");
            if (passwordLastSet.HasValue)
                details.Extra["PasswordLastSet"] = passwordLastSet.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var accountExpires = PowerShellRunner.GetProp<DateTime?>(user, "AccountExpirationDate");
            if (accountExpires.HasValue)
                details.Extra["AccountExpirationDate"] = accountExpires.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // إضافة OU Path
            var dn = details.DistinguishedName;
            if (!string.IsNullOrEmpty(dn))
            {
                var ouPath = string.Join(" > ", dn.Split(',').Where(p => p.StartsWith("OU=")).Select(p => p.Substring(3)).Reverse());
                details.Extra["OUPath"] = ouPath;
            }

            // إضافة Groups
            var groups = PowerShellRunner.GetProp<object[]>(user, "Groups");
            if (groups != null && groups.Length > 0)
            {
                var groupNames = groups.Select(g => PowerShellRunner.GetProp<string>((PSObject)g, "Name")).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                details.Extra["Groups"] = groupNames;
            }

            return details;
        }

        public async Task SetUserEnabledAsync(string sam, bool enabled)
        {
            string cmd = enabled ? "Enable-ADAccount" : "Disable-ADAccount";
            string script = CommonCred + "Import-Module ActiveDirectory; " +
                            $"{cmd} -Identity '{sam}'{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        public async Task ResetPasswordWithOptionsAsync(string sam, string newPassword, bool unlock, bool mustChange)
        {
            string script =
                CommonCred +
                "Import-Module ActiveDirectory; " +
                $"Set-ADAccountPassword -Identity '{sam}' -Reset -NewPassword (ConvertTo-SecureString '{newPassword.Replace("'", "''")}' -AsPlainText -Force){ServerExpr} -Credential $cred; " +
                (unlock ? $"Unlock-ADAccount -Identity '{sam}'{ServerExpr} -Credential $cred; " : "") +
                (mustChange ? $"Set-ADUser -Identity '{sam}' -ChangePasswordAtLogon $true{ServerExpr} -Credential $cred; " : "");
            await _ps.Invoke(script);
        }

        public Task ResetPasswordAsync(string sam, string newPassword)
            => ResetPasswordWithOptionsAsync(sam, newPassword, unlock: false, mustChange: false);

        public async Task UnlockUserAsync(string sam)
        {
            string script =
                CommonCred + "Import-Module ActiveDirectory; " +
                $"Unlock-ADAccount -Identity '{sam}'{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        // --------------- Groups ----------------
        public async Task<(List<ADGroupVm> Items, int Total)> GetGroupsAsync(string? search, int page, int pageSize)
        {
            try
            {
                string filter = string.IsNullOrWhiteSpace(search) ? "*" : $"*{search}*";
                string script =
                    CommonCred + "Import-Module ActiveDirectory; " +
                    $"Get-ADGroup -Filter \"name -like '{filter}'\" -SearchBase '{_opt.BaseDN}' -ResultSetSize {pageSize}{ServerExpr} -Credential $cred | " +
                    "Select-Object Name, DistinguishedName";

                var r = await _ps.Invoke(script);
                var items = r.Select(o => new ADGroupVm(
                    PowerShellRunner.GetProp<string>(o, "Name") ?? "",
                    PowerShellRunner.GetProp<string>(o, "DistinguishedName") ?? ""
                )).ToList();

                return (items, items.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting groups: {ex.Message}");
                return (new List<ADGroupVm>(), 0);
            }
        }

        public async Task<List<ADUserVm>> GetGroupMembersAsync(string groupSamOrDn)
        {
            string script =
                CommonCred + "Import-Module ActiveDirectory; " +
                $"Get-ADGroupMember -Identity '{groupSamOrDn}'{ServerExpr} -Credential $cred | " +
                "Where-Object {$_.objectClass -eq 'user'} | " +
                "Get-ADUser -Properties userPrincipalName,displayName,enabled,lockedout | " +
                "Select-Object SamAccountName, userPrincipalName, displayName, enabled, lockedout";

            var r = await _ps.Invoke(script);
            return r.Select(o => new ADUserVm(
                PowerShellRunner.GetProp<string>(o, "SamAccountName") ?? "",
                PowerShellRunner.GetProp<string>(o, "userPrincipalName") ?? "",
                PowerShellRunner.GetProp<string>(o, "displayName") ?? "",
                PowerShellRunner.GetProp<bool>(o, "enabled"),
                PowerShellRunner.GetProp<bool>(o, "lockedout")
            )).ToList();
        }

        public async Task AddUserToGroupAsync(string userSam, string groupSamOrDn)
        {
            string script =
                CommonCred + "Import-Module ActiveDirectory; " +
                $"Add-ADGroupMember -Identity '{groupSamOrDn}' -Members '{userSam}'{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }

        public async Task RemoveUserFromGroupAsync(string userSam, string groupSamOrDn)
        {
            string script =
                CommonCred + "Import-Module ActiveDirectory; " +
                $"Remove-ADGroupMember -Identity '{groupSamOrDn}' -Members '{userSam}' -Confirm:$false{ServerExpr} -Credential $cred";
            await _ps.Invoke(script);
        }
    }
}
