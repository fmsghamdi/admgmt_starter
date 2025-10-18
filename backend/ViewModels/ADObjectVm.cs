namespace admgmt_backend.ViewModels
{
    public class ADObjectVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string? SamAccountName { get; set; }
        public string ObjectClass { get; set; } = ""; // user | group | computer | other
        public bool? Disabled { get; set; }
    }
}
