using System.Text.RegularExpressions;
using admgmt_backend.ViewModels;
using Microsoft.Extensions.Configuration;

namespace admgmt_backend.Services
{
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private readonly PasswordPolicyVm _policy;

        public PasswordPolicyService(IConfiguration cfg)
        {
            _policy = new PasswordPolicyVm();
            cfg.GetSection("PasswordPolicy").Bind(_policy);
        }

        public PasswordPolicyVm GetPolicy() => _policy;

        public string[] Validate(string password)
        {
            var errs = new List<string>();
            if (password.Length < _policy.MinLength)
                errs.Add($"Minimum length is {_policy.MinLength}.");

            if (_policy.RequireUpper && !Regex.IsMatch(password, "[A-Z]"))
                errs.Add("At least one uppercase letter is required.");

            if (_policy.RequireLower && !Regex.IsMatch(password, "[a-z]"))
                errs.Add("At least one lowercase letter is required.");

            if (_policy.RequireDigit && !Regex.IsMatch(password, "[0-9]"))
                errs.Add("At least one digit is required.");

            if (_policy.RequireSpecial && !Regex.IsMatch(password, @"[^A-Za-z0-9]"))
                errs.Add("At least one special character is required.");

            return errs.ToArray();
        }
    }
}
