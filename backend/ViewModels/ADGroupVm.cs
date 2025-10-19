namespace admgmt_backend.ViewModels
{
    public class ADGroupVm
    {
        public string Name { get; set; } = string.Empty;
        public string? SAM { get; set; }
        public string? DistinguishedName { get; set; }
        public string? Description { get; set; }
        public int MemberCount { get; set; }
    }
}
