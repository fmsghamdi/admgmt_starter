namespace admgmt_backend.Services
{
    public class PasswordPolicyVm
    {
        public int MinLength { get; set; }
        public int HistoryCount { get; set; }
        public int MaxAgeDays { get; set; }
        public bool ComplexityEnabled { get; set; }
    }

    public interface IPasswordPolicyService
    {
        Task<PasswordPolicyVm> GetDefaultPolicyAsync();
    }
}