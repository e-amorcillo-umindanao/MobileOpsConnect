# MobileOpsConnect

MobileOpsConnect is an ERP (Enterprise Resource Planning) application designed for mobile operations management. It features a robust notification system (Web Push/FCM), user management with role-based access control, and various operational modules.

## Tech Stack
- **Framework**: ASP.NET Core 10.0 (MVC)
- **Database**: SQL Server
- **Identity**: ASP.NET Core Identity
- **Styling**: Tailwind CSS & Vanilla CSS
- **Notifications**: Firebase Cloud Messaging & VAPID (Standard Web Push)

## Getting Started

### Prerequisites
- .NET 10.0 SDK
- Node.js (for Tailwind builds)
- SQL Server

### Installation
1. Clone the repository.
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```
3. Initialize the database:
   ```bash
   dotnet ef database update
   ```
4. Build Tailwind CSS:
   ```bash
   npm install
   npm run build:css
   ```
5. Run the application:
   ```bash
   dotnet run
   ```

## Roles and Permissions
- **SuperAdmin (Alpha)**: Full system control, manages SystemAdmins.
- **SystemAdmin (Beta)**: Manages Department Managers and Staff.
- **DepartmentManager (Charlie)**: Manages Operational Staff and Leaves.
- **WarehouseStaff / Employee**: General users.

## Maintenance
- **VAPID Keys**: Automatically generated on first run and saved to `bin/vapid-keys.json`.
- **Firebase**: Requires `firebase-service-account.json` in the project root for FCM functionality.

## Documentation
Additional documentation can be found in the `documents` folder.
