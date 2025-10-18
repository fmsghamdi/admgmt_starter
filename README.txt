# AD Management System (Starter)
This repository contains a starter for a professional Active Directory Management System.

## Tech
- Backend: ASP.NET Core 8 Web API, System.DirectoryServices.AccountManagement, EF Core (MySQL), Serilog, Swagger
- Frontend: React + Vite + TypeScript, MUI v6, i18n (ar/en) with RTL support
- Auth: JWT stub (with notes for Windows Authentication via IIS/Negotiate)
- No Docker required

## Prereqs
- .NET SDK 8.x
- Node.js 18+ / PNPM or NPM
- MySQL 8+ (create schema `admgmt` and user/password from appsettings.json)

## Backend (first run)
```bash
cd backend
dotnet restore
dotnet build
dotnet run --urls http://localhost:5079
```
> Adjust AD credentials in `appsettings.json` (ServiceUserUPN and ServicePassword). The account must have permission to query AD and reset passwords if you will use that endpoint.

### Windows Authentication (optional)
- If hosting in IIS on a domain-joined Windows server:
  - Enable Windows Authentication in IIS for the site.
  - In Program.cs, you can add Negotiate if needed: `builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();` and adjust the default schemes accordingly.
- For Linux/Kestrel + Kerberos (advanced), configure SPNs and keytabs (not covered in this starter).

## Frontend
```bash
cd frontend
npm install
npm run dev
```
Create a `.env` file in `frontend` with:
```
VITE_API_URL=http://localhost:5079
```

## Sample calls
- Obtain token (temporary):
  POST http://localhost:5079/api/auth/token
  Body: { "username": "admin", "password": "admin" }

- List users:
  GET http://localhost:5079/api/users
  Header: Authorization: Bearer <token>

- List groups:
  GET http://localhost:5079/api/groups
  Header: Authorization: Bearer <token>

## Next Steps
- Implement real AD bind validation in AuthController (validate user/password against AD).
- Add endpoints for OUs, Deleted Items (Recycle Bin via LDAP), Devices (via computer objects), Office 365 (via Microsoft Graph).
- Implement audit logging (save to MySQL via AppDbContext).
- Add role-based authorization and granular permissions.
- Harden security (secret storage, HTTPS, certificate pinning where applicable).
