using System;
using System.Collections.Generic;

namespace admgmt_backend.Services
{
    // خيارات بحث المستخدمين المتقدمة (لتطابق توقيع الواجهة)
    public sealed class UsersQueryOptions
    {
        public string? Query { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        // اختياري: حصر البحث داخل OU معيّن
        public string? OuDistinguishedName { get; set; }
    }

    // تفاصيل كائن AD (مستخدم/مجموعة/حاسب/OU...)
    public sealed class ADObjectDetailsVm
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string ObjectClass { get; set; } = "";   // user / group / computer / organizationalUnit / ...

        // لو الكائن "user"
        public string? SAM { get; set; }
        public string? Email { get; set; }
        public DateTime? LastLogonUtc { get; set; }
        public bool? Enabled { get; set; }

        // لو الكائن "group"
        public string? Description { get; set; }
        public int? MemberCount { get; set; }
        public List<string>? MembersSam { get; set; }
    }
}
