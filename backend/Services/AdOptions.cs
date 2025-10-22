namespace admgmt_backend.Services
{
    public class AdOptions
    {
        public string BaseDN { get; set; } = "";
        public string DomainController { get; set; } = ""; // IP or FQDN
        public string ServiceUserUPN { get; set; } = "";
        public string ServicePassword { get; set; } = "";
        public int    SearchTimeoutSeconds { get; set; } = 30;
        public bool   UseLdaps { get; set; } = false;
    }
}
