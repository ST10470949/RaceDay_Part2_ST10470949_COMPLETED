# RaceDay — Part 2 RESTful API
## Project Description
RaceDay is a web-based event management system for running, walking and cycling events. Part 2 provides the RESTful ASP.NET Core Web API that the Part 3 MVC application will consume.

## The API supports two roles:

Organiser — creates, updates and deletes their own Events, manages Categories, views enrolments, records Results and manages race-day weather/route information.
Participant — creates an account, browses Events and Categories, enrols in a Category, views their own enrolments and views their own Results.

## Part 1 Design

The `/docs` folder contains the planning documents used for Part 2:

**Authentication note:** the original Part 1 endpoint plan describes login as returning a JWT, while the official Part 2 guide explicitly requires server-side session management. The implementation follows the Part 2 guide and uses session authentication. The endpoint routes themselves remain aligned with Part 1.

- `RaceDay_ERD.pdf` — updated ERD matching the implemented database model.
- `RaceDay_API_Endpoint_Plan.pdf` — endpoint plan for the implemented API.
- `RaceDay_API_Endpoint_Plan.md` — original Part 1 endpoint plan.
- `RaceDay_Database.sql` — executable SQL Server schema and sample data.
- `RaceDay_ERD_Original.pdf` — original Part 1 ERD retained for comparison.

Part 2 keeps the Part 1 `/api/users/me`, `/api/enrolments/me` and `/api/results/me` routes. The guide's `/api/profile/me` route is also available as a compatibility alias to the same own-profile actions.


## Technologies Used

- C# / ASP.NET Core Web API
- .NET 8
- Entity Framework Core 8.0.10
- SQL Server / SQL Server LocalDB
- ASP.NET Core Session
- Microsoft Identity `PasswordHasher<User>`
- Swagger / OpenAPI
- xUnit
- EF Core InMemory provider for unit tests

## Project Structure

```text
RaceDay
├── docs
│   ├── RaceDay_ERD.pdf
│   ├── RaceDay_ERD_Original.pdf
│   ├── RaceDay_API_Endpoint_Plan.pdf
│   ├── RaceDay_API_Endpoint_Plan.md
│   └── RaceDay_Database.sql
├── RaceDay.API
│   ├── Controllers
│   ├── Data
│   ├── DTOs
│   ├── Migrations
│   ├── Models
│   ├── Services
│   ├── appsettings.json
│   └── Program.cs
├── RaceDay.Tests
│   ├── TestHelpers
│   └── test classes
├── .github
└── RaceDay.sln
```

## Database Setup

The API uses the `RaceDayConnection` connection string.

Example LocalDB connection:

```text
Server=(localdb)\MSSQLLocalDB;Database=RaceDay;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

You can create the database in either of these ways:

### Option 1 — SQL script

Open `docs/RaceDay_Database.sql` in SQL Server Management Studio and execute the complete script.

**Important:** the script drops and recreates the `RaceDay` database. Do not run it against a database containing data you want to keep.

### Option 2 — EF Core migration

From the solution directory:

```powershell
dotnet ef database update --project RaceDay.API
```

The initial migration is included under:

```text
RaceDay.API/Migrations/
```

The migration creates the same schema represented in the SQL script and ERD.

### Sample SQL accounts

The SQL script includes four sample accounts. The sample password is:

```text
RaceDay123!
```

The API still hashes passwords normally when new users register.

## Authentication

Registration, login and logout use server-side ASP.NET Core Session.

Passwords are never stored as plain text. `PasswordHasher<User>` creates a salted password hash before the user is saved.

After login the session stores:

```text
UserId
Role
FullName
```

Protected endpoints use this session information to enforce access.

### Authentication endpoints

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/logout
```

## API Endpoints

### Profile

```text
GET /api/users/me
PUT /api/users/me
GET /api/profile/me
PUT /api/profile/me
```

The `/api/profile/me` routes are aliases for the Part 1 `/api/users/me` routes.

### Events

```text
GET    /api/events
GET    /api/events/mine
GET    /api/events/{id}
POST   /api/events
PUT    /api/events/{id}
DELETE /api/events/{id}
```

### Categories

```text
GET    /api/events/{id}/categories
POST   /api/events/{id}/categories
PUT    /api/categories/{id}
DELETE /api/categories/{id}
```

### Enrolments

```text
POST   /api/enrolments
GET    /api/enrolments/me
PUT    /api/enrolments/{id}/status
DELETE /api/enrolments/{id}
GET    /api/events/{id}/enrolments
```

### Results

```text
POST /api/results
GET  /api/results/me
GET  /api/events/{id}/results
```

### Weather and route information

```text
GET  /api/events/{id}/weather
POST /api/events/{id}/weather
```

## Role-Based Access

The API returns the appropriate HTTP status code:

- `200 OK` — successful request
- `201 Created` — resource created
- `204 No Content` — successful deletion
- `400 Bad Request` — invalid input
- `401 Unauthorized` — not logged in
- `403 Forbidden` — logged in with the wrong role or trying to access another user's/event owner's data
- `404 Not Found` — requested resource does not exist
- `409 Conflict` — duplicate enrolment or duplicate result

Organiser ownership is checked at API level. An Organiser cannot edit another Organiser's Event, Categories, enrolments or Results.

## Swagger

Run the API in the Development environment and open:

```text
/swagger
```

Swagger documents the controllers and XML comments and allows the endpoints to be tested.

Recommended complete test flow:

1. Register an Organiser.
2. Register a Participant.
3. Login as Organiser.
4. Create an Event.
5. Add Categories.
6. Login as Participant.
7. Enrol in a Category.
8. Login as Organiser.
9. View Event enrolments.
10. Capture a Result.
11. Login as Participant.
12. View personal Results.
13. Test a wrong-role request and confirm `403 Forbidden`.
14. Test an unauthenticated protected request and confirm `401 Unauthorized`.

## Unit Testing

The `RaceDay.Tests` project uses xUnit and EF Core InMemory so the tests do not require a real SQL Server database.

The tests cover:

- Registration
- Password hashing
- Duplicate registration
- Successful login/session creation
- Failed login
- Logout
- Profile access/update
- Event creation and ownership
- Event update/delete ownership
- Public event browsing
- Role enforcement
- Category management
- Participant enrolment
- Duplicate/full enrolment handling
- Enrolment cancellation
- Organiser enrolment management
- Result capture and duplicate-result prevention
- Participant result privacy
- Weather ownership

Run:

```powershell
dotnet test
```

## CI/CD

The GitHub Actions workflow remains under:

```text
.github/workflows/dotnet-ci.yml
```

GitHub commit history, CI screenshots and the Part 2 video are intentionally left unchanged/provided separately.

## Notes for Part 3

The API is separated from the MVC front end. The MVC application should consume the API endpoints rather than connecting directly to the RaceDay database or duplicating the API's business rules.

## Screenshot of the CI workflows Green Tick
### IMAGE
<img width="1911" height="920" alt="Screenshot 2026-10-08 221731" src="https://github.com/user-attachments/assets/85164932-f2b4-4773-ab72-b268c6d16fa2" />

## Video Presentation (Unlisted on YouTube)
### Link
