namespace admgmt_backend.ViewModels
{
    public class OUVm
    {
        public string Name { get; set; } = string.Empty;
        public string DistinguishedName { get; set; } = string.Empty;
        public string? ParentDn { get; set; }
        public string? Description { get; set; }
        public int ChildCount { get; set; }
    }
}
