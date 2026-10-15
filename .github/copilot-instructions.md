# Project Instructions: QuanLyNhaTro

## 1. Project Overview
- Thesis title: Xây dựng Website quản lý nhà trọ.
- This is a solo university IT thesis project.
- The application manages a small rental property.
- Keep the project practical, understandable, and suitable for a student thesis.
- Do not add multi-branch management unless explicitly required by the supervisor.

## 2. Technology Stack
- IDE: Visual Studio 2022.
- Framework: ASP.NET Core MVC.
- Target framework: .NET 8.
- Language: C#.
- ORM: Entity Framework Core 8.
- Database: SQL Server 2022 Express.
- Frontend: Razor Views, HTML, CSS, and Bootstrap.
- Use the existing SQL Server database and EF Core migrations.

## 3. Current Project Status
- The MVC project has been created and runs successfully.
- EF Core SQL Server and Tools packages are installed.
- ApplicationDbContext is registered in Program.cs.
- Database QuanLyNhaTroDb has been created.
- The main entities are Building, Room, TenantProfile, and RentalContract.
- Existing models, primary key configuration, relationships, migrations, and database tables must be inspected before making changes.

## 4. Mandatory Supervisor Requirements
The following three features must work with real SQL Server data:
1. Building and Room Management.
2. Tenant Management.
3. Rental Contract Management.

Do not replace database operations with hardcoded or in-memory sample data.

## 5. Business Rules
- A building can contain multiple rooms.
- Each room belongs to one building.
- RoomCode must be unique within the same building.
- Each rental contract refers to one room and one tenant.
- ContractCode must be unique.
- Validate dates, rent, and deposit values.
- Prevent conflicting active rental contracts for the same room.
- Do not delete records that are referenced by other records without checking the business rules.
- Keep room status consistent with the actual rental situation.

## 6. Coding Rules
- Follow ASP.NET Core MVC conventions.
- Use dependency injection for ApplicationDbContext.
- Use Entity Framework Core for database operations.
- Use asynchronous database operations where appropriate.
- Validate inputs on both the server side and through suitable UI validation.
- Keep controllers, models, views, and data access organized.
- Avoid unnecessary packages, design patterns, APIs, or a separate frontend.
- Do not drop or recreate the database merely to resolve a coding issue.
- Do not delete or rewrite existing migrations without explaining the reason.
- Inspect existing code before editing it.
- Preserve existing property names and primary key mappings unless a change is necessary and explained.
- Implement one feature at a time.

## 7. How to Assist
- Explain important concepts in beginner-friendly Vietnamese.
- Before significant edits, summarize the planned changes and affected files.
- Provide complete code for files that need to be created or replaced.
- Explain where each new file belongs and why it is needed.
- Provide clear build, run, and manual testing steps.
- Do not claim that a feature works until it has been tested.
- Prefer the simplest correct solution that satisfies the thesis requirements.
- Avoid generating the entire application in one large change.

## Scope Clarification
- The application manages one physical rental property at one location.
- Building records represent buildings or blocks within that same property, not separate business branches or rental locations.
- Rooms belong to buildings within the same property.
- The Address field must not be used to introduce multi-branch management.
- Do not implement branch management, branch-specific permissions, or cross-branch reporting.
- Keep the implementation small and aligned with the supervisor's requirements.