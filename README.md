# RaceDay

RaceDay is an event platform for runs, walks and rides. **Organisers** create events, categories and publish results. **Participants** browse events, enrol, and track their race history.

| Part | Project | What it is |
|---|---|---|
| 2 | `RaceDay.Api` | ASP.NET Core Web API (.NET 8), EF Core + SQL Server, cookie session auth |
| 3 | `RaceDay.Web` | ASP.NET Core **MVC** front end that consumes the API, runs in Docker |

## Demo video

> Replace with your unlisted YouTube link: `https://youtu.be/XXXXXXXXXXX`

## CI/CD status

> `![Green build](docs/green-build.png)`  (screenshot of a green GitHub Actions run)

---

# Part 3: MVC web application

## How it fits together

```
Browser  ->  RaceDay.Web (MVC)  ->  RaceDay.Api (REST)  ->  SQL Server
                                          |
                                          +->  Azure Blob Storage (images)
```

- **The MVC app has no database access and no business logic.** Every read and write is an HTTP call to the Part 2 API through one class, `Services/ApiClient.cs`. Rules (who may enrol, unique positions, ownership checks, etc.) stay in the API.
- **Azure is only touched by the API.** The MVC app sends the image file to the API; the API stores it in Blob Storage and keeps the URL.
- **Session management:** on login the MVC app calls `POST /api/auth/login`, keeps the API's session cookie plus the user's role in the server-side ASP.NET Core session, and replays that cookie on every API call. If the API ends the session (401), the user is sent back to the login page.
- **Role-based navigation:** `SessionAuth` filter protects pages. Anonymous users are redirected to login; the wrong role gets an "access denied" page. Organisers and Participants get different menus and pages.

### Project structure (`RaceDay.Web`)

| Folder | Purpose |
|---|---|
| `Controllers/` | `Account`, `Home`, `Events`, `Enrolments`, `Results`, `Profile` (Participant side) and `Organiser*` controllers (dashboard, events, categories, enrolments, results entry) |
| `Services/ApiClient.cs` | The only place that talks to the API: attaches the session cookie, uploads files, turns API errors into friendly messages |
| `Filters/SessionAuthAttribute.cs` | Login and role gate |
| `Models/` | `ApiModels` (shapes returned by the API), `ViewModels` (forms and page models), `Format` (dates, times, "47th", status colours) |
| `Views/` | Razor views, one folder per controller, shared layout with role-based menu |
| `wwwroot/` | `site.css` (RaceDay theme), `site.js` |

### Pages

**Participant:** Home (upcoming events with search / type / date filters), Event details (info + categories + enrol), My enrolments (colour-coded status, pay, cancel), My results ("47th of 312 finishers"), Profile (edit details, upload picture).

**Organiser:** Dashboard (events, total enrolments, upcoming dates), Events (create / edit / delete, banner upload), Categories (add / edit / remove per event), Enrolments (who enrolled, category, status, payment, bib), Results entry (position + finish time, publish, mark event completed).

**Status colours:** green = Confirmed / Paid / Open, amber = Pending / Draft / Closed, red = Cancelled.

## Run it locally

You need the .NET 8 SDK, SQL Server (or LocalDB), and Git.

### 1. Azure Blob Storage (for images)

1. In the Azure portal create a **Storage account** (Standard, LRS is fine).
2. Under *Settings > Configuration* make sure **Allow Blob anonymous access** is **Enabled** (images are shown straight from their blob URL).
3. Copy the connection string from *Security + networking > Access keys*.
4. Store it as a secret for the API (never commit it):

```bash
cd RaceDay.Api
dotnet user-secrets set "AzureStorage:ConnectionString" "<your connection string>"
# optional: dotnet user-secrets set "AzureStorage:ContainerName" "raceday-images"
```

The API creates the container (public read for blobs) on first upload. Without a connection string the app still works; image uploads just return a friendly "storage not configured" message.

### 2. Apply the database changes and start the API

```bash
cd RaceDay.Api
dotnet ef database update          # applies InitialCreate + AddImageUrls
dotnet run --launch-profile https  # https://localhost:7100
```

### 3. Start the MVC app

```bash
cd RaceDay.Web
dotnet run --launch-profile http   # http://localhost:5200
```

The API address is the `Api:BaseUrl` setting in `RaceDay.Web/appsettings.json` (default `https://localhost:7100`).

## Run it in Docker

The MVC app runs in a container; the API and SQL Server keep running on your machine.

**1. Start the API so the container can reach it** (plain http, listens on all interfaces):

```bash
cd RaceDay.Api
dotnet run --launch-profile http-docker
```

**2. Build the image:**

```bash
docker build -t raceday-web ./RaceDay.Web
```

**3. Run it (single command):**

```bash
docker run --rm -p 8080:8080 --add-host=host.docker.internal:host-gateway -e Api__BaseUrl=http://host.docker.internal:5100 raceday-web
```

Open **http://localhost:8080**. It behaves the same as the local run: same pages, same API, same culture settings (the app pins `en-US` so numbers and dates parse identically everywhere).

Notes:
- `--add-host=host.docker.internal:host-gateway` is needed on Linux; Docker Desktop (Windows/Mac) already provides that name and ignores it harmlessly.
- To point at a deployed API instead, change `Api__BaseUrl`.
- The Dockerfile is multi-stage (SDK image to build and publish, small ASP.NET runtime image to run), caches the restore layer, and runs as a non-root user.

## Tests and CI

`dotnet test` runs the API tests, including new upload tests that use a fake blob store, so no Azure account is needed. The GitHub Actions workflow (`.github/workflows/ci.yml`) restores, builds the **API**, builds the **MVC project**, runs the tests, then builds the Docker image.

## Part 3 design decisions

1. **Images:** `POST /api/events/{id}/banner` and `POST /api/participants/{id}/picture` accept JPG / PNG / WebP up to 5 MB, upload to Azure Blob Storage and save the URL on the event / participant (migration `AddImageUrls`).
2. **"Publish" results:** the API has no separate draft state for results, so a result is visible to the participant as soon as the organiser saves it. The results page also has a *Mark event completed* button that sets the event status to Completed.
3. **"47th out of 312 finishers"** counts finishers in the participant's own category, because positions are unique per category in the API.
4. **Payment:** *Pay now* on My enrolments calls the API's payment endpoint, which marks the enrolment Paid and Confirmed. There is no real payment gateway.
5. **Event status** decides what participants see: only Open and Closed upcoming events are listed on the home page, and only Open events accept enrolments (an API rule).

---

# Part 2: RaceDay API

### Tech stack (API)

- ASP.NET Core Web API (controllers), .NET 8
- Entity Framework Core 8 (Code-First, migrations) + SQL Server (LocalDB by default)
- Cookie authentication (session), ASP.NET Core `PasswordHasher` (PBKDF2) for password hashing
- Swagger / Swashbuckle with XML documentation
- xUnit + `WebApplicationFactory` integration tests (in-memory database)
- GitHub Actions (build + test on every push)

### Setup and run the API

Prerequisites: .NET 8 SDK, SQL Server or SQL Server LocalDB (installed with Visual Studio), Git.

```bash
git clone <your-repo-url>
cd <repo-folder>

# 1. Check the connection string in RaceDay.Api/appsettings.json (defaults to LocalDB)

# 2. Create the database schema from the EF Core migrations
dotnet tool install --global dotnet-ef        # once only
cd RaceDay.Api
dotnet ef migrations add InitialCreate        # first time only, if no Migrations folder exists
dotnet ef database update

# 3. Run the API
dotnet run --launch-profile https
```

Open **https://localhost:7100/swagger** to view and test every endpoint. In Development the app also applies pending migrations at startup.

#### Using the API in Swagger

1. `POST /api/auth/register` to create an Organiser (e.g. `"role": "Organiser"`).
2. `POST /api/auth/login`. The browser stores the `RaceDay.Session` cookie and sends it automatically on later requests.
3. Use the protected endpoints. To switch role, call `POST /api/auth/logout` then log in as the other user (or use a private window).

### Running the tests

```bash
dotnet test
```

Tests boot the real API in memory with an in-memory database, so no SQL Server is needed. They cover registration and login, event management by organisers, role rejection (401 vs 403), ownership rules, registrations (enrolments), bib numbers and results.

### Roles

| Role | Can do |
|---|---|
| **Organiser** | Create, update and delete **their own** events, routes and categories; view registrations for their events; mark payment, assign bib numbers; record, update and delete results; manage their own profile. |
| **Participant** | Register an account, manage their own profile, view events/routes/categories/results; register for an event category; pay/cancel their own registration; view their own registrations and results. |

Role is chosen at registration and validated against the allowed values (`Organiser`, `Participant`). It is stored with the account and placed in the session cookie at login. **401** means not logged in; **403** means logged in but not allowed (wrong role, or someone else's data).

Ownership rules go beyond role checks: an organiser can only change events they created, and a participant can only see their own registrations and results.

### Endpoints

All endpoints are documented in Swagger. Summary:

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` |
| Events | `GET /api/events`, `GET /api/events/{id}`, `POST`, `PUT`, `DELETE /api/events/{id}`, `GET /api/organisers/{id}/events` |
| Routes | `POST /api/routes`, `GET /api/events/{id}/routes`, `PUT`/`DELETE /api/routes/{id}` |
| Categories | `POST /api/event-categories`, `GET /api/events/{id}/categories`, `PUT`/`DELETE /api/event-categories/{id}` |
| Participants | `POST /api/participants`, `GET`/`PUT`/`DELETE /api/participants/{id}` |
| Registrations | `POST /api/registrations`, `GET`/`PUT`/`DELETE /api/registrations/{id}`, `GET /api/participants/{id}/registrations`, `PUT /api/registrations/{id}/payment`, `PUT /api/registrations/{id}/bib-number` |
| Uploads (Part 3) | `POST /api/events/{id}/banner`, `POST /api/participants/{id}/picture` (multipart, field `file`) |
| Results | `POST /api/results`, `GET /api/events/{id}/results`, `GET /api/participants/{id}/results`, `PUT`/`DELETE /api/results/{id}` |

### Changes from the Part 1 plan

These were needed to meet the Part 2 brief or to make the system safe. Update this list to match your own decisions.

1. **Added authentication endpoints** (`/api/auth/*`): the brief requires role-based registration and login, which the Part 1 plan did not cover.
2. **Added organiser profile endpoints** (`GET`/`PUT /api/organisers/{id}`): the brief requires both roles to view and update their own profile.
3. **Added `GET /api/events/{id}/registrations`** (and `GET /api/registrations/{id}`): the brief requires organisers to view all enrolments for their events.
4. **Event has `name` and `distance`**: required by the brief.
5. **`POST /api/participants`** also requires a `password`, since the participant must be able to log in.
6. **`registrationDate` is set by the server** and is not accepted in the request body of `POST /api/registrations`.
7. **Ownership checks** were added on top of role checks (see Roles).
8. **Registration rules:** only events with status `Open` accept registrations; cancelling keeps the record with status `Cancelled`; participants can mark only their own registration `Paid` or `Cancelled`; bib numbers go only to paid, non-cancelled registrations and must be unique within an event.
9. **Result rules:** the participant must hold a non-cancelled registration in the given category; one result per participant per event; positions are unique per category.
10. **Delete rules:** events and categories with registrations or results return `409 Conflict`. Deleting a participant account also removes their registrations and results.

### Data model

`User` (login + role) one-to-one with `Organiser` or `Participant` profile. `Organiser` has many `Event`; `Event` has many `EventRoute` and `EventCategory`; `Participant` and `EventCategory` are linked by `Registration`; `Result` records position and finish time per participant per event. Passwords are stored only as hashes.
