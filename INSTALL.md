# Installing AgriConnect

Two ways to run it. **Route A (Docker)** needs nothing on your machine but Docker and
gets you a working site in one command. **Route B (from source)** is for working on
the code.

- [Requirements](#requirements)
- [Route A — Docker](#route-a--docker-recommended)
- [Route B — from source](#route-b--from-source)
- [First run](#first-run)
- [Configuration reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)
- [Everyday commands](#everyday-commands)
- [Replacing the seed photos](#replacing-the-seed-photos)

---

## Requirements

### Common to both routes

| | |
|---|---|
| OS | Windows 10/11, macOS 12+, or any modern Linux |
| Disk | **~4 GB** free — 1.5 GB for the SQL Server image, ~1 GB for the .NET images, ~500 MB for the database, 44 MB of seed photos |
| RAM | 4 GB free. SQL Server alone wants 2 GB and will not start with less |
| Ports | **8080** (web) and **1433** (SQL Server) must be free |
| Browser | Any current Chrome, Edge, Firefox or Safari |
| Git | To clone the repository |

### Route A only

| | |
|---|---|
| **Docker Desktop** 4.x | Windows/macOS. Linux: Docker Engine 24+ with the Compose v2 plugin |

Check it is working:

```powershell
docker --version
docker compose version
```

> On Windows, Docker Desktop must be set to **Linux containers** (the default). The
> SQL Server image is Linux-only.

### Route B only

| | |
|---|---|
| **.NET 8 SDK** (8.0.100 or later) | https://dotnet.microsoft.com/download/dotnet/8.0 — the **SDK**, not just the runtime |
| **SQL Server 2019 or 2022** | Developer or Express edition, *or* just the compose `db` service from Route A |

Check:

```powershell
dotnet --version          # 8.0.x
```

> SQL Server specifically. The booking approval path uses `sp_getapplock`, and the
> schema uses a filtered unique index — neither exists in PostgreSQL, MySQL or SQLite.

---

## Route A — Docker (recommended)

**1. Clone**

```powershell
git clone https://github.com/<your-account>/AgriConnect.git
cd AgriConnect
```

**2. Start**

```powershell
docker compose up -d --build
```

The first build downloads the .NET SDK, ASP.NET runtime and SQL Server images and
compiles the app — allow **5–15 minutes** depending on your connection. Later starts
take seconds.

**3. Wait for the database**

The web container does not start until SQL Server answers a real query, not just a
port probe — SQL Server accepts TCP connections well before it can serve anything, and
without that wait the first migration fails. This is the compose healthcheck doing its
job; `docker compose ps` shows `db` as `healthy` when it is ready, usually 30–60
seconds in.

**4. Open it**

**http://localhost:8080**

On that first request the application creates the database, applies all five
migrations, and seeds the demo data — give it another 30–60 seconds. Watch it happen
with `docker compose logs -f web`.

That is the whole installation. Go to [First run](#first-run).

---

## Route B — from source

**1. Clone**

```powershell
git clone https://github.com/<your-account>/AgriConnect.git
cd AgriConnect
```

**2. Get a SQL Server**

Easiest is to borrow the one from compose and skip the rest of Docker:

```powershell
docker compose up -d db
```

That gives you SQL Server on `localhost,1433`, user `sa`, password `AgriDev!Pass2026`.
Any other SQL Server instance works just as well — you will need its connection string
in the next step.

**3. Set the connection string**

It holds a password, so it is deliberately **not** in `appsettings.json`. Startup fails
with a clear error if it is missing. Set it once per machine with user secrets:

```powershell
cd AgriConnect
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Server=localhost,1433;Database=AgriConnect;User Id=sa;Password=AgriDev!Pass2026;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

On bash/zsh use `\` for line continuation instead of the backtick, or put it on one
line.

For a local default instance with Windows authentication, use:

```
Server=(localdb)\MSSQLLocalDB;Database=AgriConnect;Trusted_Connection=True;MultipleActiveResultSets=true
```

User secrets live in your user profile, never in the repository.

**4. Run**

```powershell
dotnet run
```

Open the URL it prints — **http://localhost:5037** by default. Migrations apply and
demo data is seeded on startup, exactly as in Route A.

> `dotnet run` must be executed from the `AgriConnect` folder (the one with
> `AgriConnect.csproj`), not the repository root.

**Optional — Visual Studio:** open `AgriConnect.sln`, right-click the project →
**Manage User Secrets**, paste the connection string into the JSON that opens, then
press **F5**.

---

## First run

Sign in with any of these. They exist only in the Development environment.

| Account | Password | |
|---|---|---|
| `user@agriconnect.local` | `User!Dev2026` | **Ordinary member — start here** |
| `admin@agriconnect.local` | `Admin!Dev2026` | Administrator — the `/Admin` section |
| `murugan@agriconnect.local` | `Demo!Dev2026` | Owns the tractor, has requests waiting to approve |
| `selvi@agriconnect.local` | `Demo!Dev2026` | Has a pending request of her own |
| `kumar@agriconnect.local` | `Demo!Dev2026` | Sells livestock |

Roughly 140 more members exist, three in every district, with logins like
`saraswathi32@agriconnect.local` — same `Demo!Dev2026` password. The administrator's
**Users** page lists them all.

You can also **Register** a fresh account from the header. There is no email
confirmation step. After registering, open **Profile** and set your district — the
nearest-first ranking on the labour pages needs it.

Worth trying first:

1. **/equipment → Tractor** — pick a machine, choose dates on the calendar, send a request.
2. Sign out, sign in as that machine's owner, go to **Bookings → Received** and approve it.
3. **/labour → Weeding** — notice the same worker's other tasks share one calendar.
4. **/marketplace → Tomato** — the reference price next to each asking price comes from the administrator's market price table.
5. Switch the header language to **தமிழ்**.

A fuller walkthrough is in [docs/user-guide.md](docs/user-guide.md).

---

## Configuration reference

Every setting can come from `appsettings.json`, an environment variable, or user
secrets. As an environment variable, a `:` becomes `__` (double underscore).

| Setting | Env var | Default | What it does |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | *none — required* | SQL Server connection string. Startup throws if absent. |
| `DataProtection:KeyPath` | `DataProtection__KeyPath` | *empty* | Folder for the data protection key ring. Set to a mounted volume in Docker; leave empty from source to keep the default, which already persists in your user profile. |
| `Seed:AdminEmail` | `Seed__AdminEmail` | `admin@agriconnect.local` | The administrator account created on startup. |
| `Seed:AdminPassword` | `Seed__AdminPassword` | `Admin!Dev2026` in Development; **required** elsewhere | No fallback outside Development — startup fails rather than create a known-password admin on a real server. |
| `Seed:DemoEmail` | `Seed__DemoEmail` | `user@agriconnect.local` | The ordinary demo member. |
| `Seed:DemoPassword` | `Seed__DemoPassword` | `User!Dev2026` | |
| `Seed:DemoData` | `Seed__DemoData` | `true` in Development | `false` seeds only the administrator. Demo data is **never** seeded outside Development, whatever this is set to. |
| `ASPNETCORE_ENVIRONMENT` | same | `Development` in compose and `dotnet run` | `Production` disables demo data and the developer exception page. |

> **User secrets only load in Development.** Running as `Production` fails on the
> missing connection string unless you supply it another way — an environment
> variable, or your host's secret store.

The compose file sets `ConnectionStrings__DefaultConnection`,
`DataProtection__KeyPath` and `ASPNETCORE_ENVIRONMENT` for you.

### Before deploying this anywhere real

1. Change `MSSQL_SA_PASSWORD` in `docker-compose.yml` and the matching connection string.
2. Set `ASPNETCORE_ENVIRONMENT=Production` and supply `Seed__AdminPassword`.
3. Put a reverse proxy with TLS in front — the container serves plain HTTP by design.
4. Encrypt the data protection key ring with a certificate; the files are currently
   plaintext, protected only by the volume's `0600` permissions.
5. Replace the seed photos (see [below](#replacing-the-seed-photos)).

---

## Troubleshooting

**`Connection string 'DefaultConnection' not found.` on startup**
It is deliberately absent from `appsettings.json`. Route B step 3 — and make sure you
ran `dotnet user-secrets set` from inside the `AgriConnect` folder, not the repo root.
If you are running as `Production`, user secrets do not load at all; use an
environment variable.

**`Failed to determine the https port for redirect` in the log**
Expected, and left in on purpose. The container serves plain HTTP and TLS belongs to
whatever sits in front of it, so `UseHttpsRedirection` has nothing to redirect to.
Silencing it would also hide it from someone who *did* mean to serve HTTPS and forgot
to configure the port.

**The web container keeps restarting / `A network-related or instance-specific error`**
SQL Server is not up yet. `docker compose ps` — wait for `db` to read `healthy`, which
takes 30–60 seconds from cold. If it never gets there, `docker compose logs db`;
usually it is RAM (SQL Server needs 2 GB) or a bad `MSSQL_SA_PASSWORD` that fails the
complexity rules.

**`Ports are not available: 0.0.0.0:1433` or `:8080`**
Something already has the port — often a local SQL Server service on 1433. Stop it, or
change the left-hand side of the mapping in `docker-compose.yml` (`"1434:1433"`) and
update your connection string to match.

**First page load is very slow**
It is doing the migration and seeding ~143 members, 265 listings and several hundred
bookings, and copying 368 photos into `wwwroot/uploads`. Once only.
`docker compose logs -f web` shows progress.

**Listing photos are missing / broken images**
The seeder copies the photos into `wwwroot/uploads` on first run. In Docker that is a
named volume; if you ran `docker compose down -v` the volume went with it, and the
*next* start reseeds everything. From source, check
`AgriConnect/wwwroot/uploads` exists and is writable.

**Signed out after every rebuild (Docker)**
That is the data protection key ring being thrown away — check the `keys` volume is
still mounted and `DataProtection__KeyPath` is still set in `docker-compose.yml`.

**Start the database over**

```powershell
docker compose down -v      # drops the database AND the uploaded photos
docker compose up -d --build
```

From source: drop the `AgriConnect` database in SQL Server and run again — it will
re-migrate and reseed.

**Error 1934 running ad-hoc SQL against `Listings`**
The table has a filtered index, which makes SQL Server require `QUOTED_IDENTIFIER ON`
for any insert, update or delete on it. EF Core always sets it, so the application
never sees this; `sqlcmd` defaults it OFF. Put `SET QUOTED_IDENTIFIER ON;` at the top
of your script.

---

## Everyday commands

```powershell
docker compose up -d --build     # build and start
docker compose ps                # is the db healthy yet?
docker compose logs -f web       # follow the application log
docker compose stop              # stop, keep the data
docker compose down              # remove the containers, keep the data
docker compose down -v           # remove the containers AND the database and photos
```

From source:

```powershell
cd AgriConnect
dotnet run                                   # run
dotnet watch run                             # run, reloading on file changes
dotnet build                                 # compile only
dotnet ef migrations add <Name>              # after changing an entity
dotnet ef database update                    # apply migrations by hand (startup does it anyway)
```

`dotnet ef` needs the tool once per machine: `dotnet tool install --global dotnet-ef`.

---

## Replacing the seed photos

The 368 photos in `AgriConnect/wwwroot/seed-images` are Wikimedia Commons images under
CC BY, CC BY-SA and public-domain terms — **not this project's own**. Their authors and
licences are listed in [docs/image-credits.md](docs/image-credits.md), which those
licences require you to keep. Replace them before using this for anything real.

Photos are matched to categories by filename convention, so replacing one is just
overwriting the file:

```
power-weeder-1.jpg, power-weeder-2.jpg …   → the "Power Weeder" category
tractor-1.jpg … tractor-6.jpg              → "Tractor"
```

Add another numbered file and it joins the rotation — no code change. To rebuild the
whole set from Commons, or just one subject:

```powershell
tools/fetch-seed-images.ps1                 # everything, several per category
tools/fetch-seed-images.ps1 -Only tractor   # one subject
tools/write-image-credits.ps1               # regenerate docs/image-credits.md
```

Then `docker compose down -v` and start again, so the seeder re-copies them into
`uploads`.
