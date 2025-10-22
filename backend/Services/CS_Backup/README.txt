Backend refactor pack (PowerShell-first)

Files:
- Services/IADService.cs
- Services/PowerShellRunner.cs
- Services/PowerShellAdService.cs

Steps:
1) Copy these files into backend/Services/ (overwrite IADService.cs if it exists).
2) Program.cs:
   using admgmt_backend.Services;
   builder.Services.AddScoped<IADService, PowerShellAdService>();
3) appsettings.json:
   "AD": { "BaseDN": "DC=UQU,DC=LOCAL" }
4) Install package:
   dotnet add package Microsoft.PowerShell.SDK
5) Disable old LDAP ADService:
   <ItemGroup><Compile Remove="Services\ADService.cs" /></ItemGroup>