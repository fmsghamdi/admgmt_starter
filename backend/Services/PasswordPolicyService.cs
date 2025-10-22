using System.Management.Automation;

namespace admgmt_backend.Services
{
    public class PasswordPolicyService : IPasswordPolicyService
    {
        public async Task<PasswordPolicyVm> GetDefaultPolicyAsync()
        {
            using var ps = PowerShell.Create();
            ps.AddCommand("Get-ADDefaultDomainPasswordPolicy");
            var results = await Task.Run(() => ps.Invoke());
            if (ps.HadErrors || results.Count == 0)
            {
                return new PasswordPolicyVm { MinLength = 8, HistoryCount = 24, MaxAgeDays = 90, ComplexityEnabled = true };
            }
            var r = results[0];
            int minLen = (int)(r.Properties["MinPasswordLength"]?.Value ?? 8);
            int hist = (int)(r.Properties["PasswordHistoryCount"]?.Value ?? 24);
            int maxAge = (int)TimeSpan.FromTicks((long)(r.Properties["MaxPasswordAge"]?.Value ?? TimeSpan.FromDays(90).Ticks)).TotalDays;
            bool comp = (bool)(r.Properties["ComplexityEnabled"]?.Value ?? true);
            return new PasswordPolicyVm { MinLength = minLen, HistoryCount = hist, MaxAgeDays = maxAge, ComplexityEnabled = comp };
        }
    }
}