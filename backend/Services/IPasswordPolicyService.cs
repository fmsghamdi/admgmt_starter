using admgmt_backend.ViewModels;

namespace admgmt_backend.Services
{
    public interface IPasswordPolicyService
    {
        PasswordPolicyVm GetPolicy();
        string[] Validate(string password);
    }
}
