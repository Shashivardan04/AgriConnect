# AgriConnect

A web platform that connects farmers in Tamil Nadu with the three things they
otherwise chase by phone and word of mouth: **machinery to rent**, **workers to hire**,
and **a market for what they grow**. It also publishes district-wise reference prices
so a seller knows what their produce is worth before they agree a figure.

**ASP.NET Core 8 MVC · Entity Framework Core 8 · SQL Server 2022 · ASP.NET Core Identity · Bootstrap 5 · English + Tamil**

> **[Installation guide → INSTALL.md](INSTALL.md)** — requirements, step-by-step setup,
> configuration reference and troubleshooting.

In a hurry, with Docker installed:

```powershell
docker compose up -d --build
```

Then open **http://localhost:8080** and sign in as `user@agriconnect.local` /
`User!Dev2026`. The database is created, migrated and filled with demo data on first
start — nothing else to do.

---

## Contents

- [What it does](#what-it-does)
- [Demo accounts](#demo-accounts)
- [Technology](#technology)
- [Project layout](#project-layout)
- [Data model](#data-model)
- [How it is built](#how-it-is-built) — the design decisions and why
- [Demo data](#demo-data)
- [Listing photos](#listing-photos)
- [Languages](#languages)
- [Not included](#not-included)

---

## What it does

The site is three modules over one marketplace engine.

### Equipment rental — `/equipment`

Tractors, rotavators, ploughs, seed drills, sprayers, threshers, harvesters, water
pumps, trailers, chaff cutters and power weeders, browsed under **Tillage / Sowing /
Harvesting / Irrigation / Haulage** headings. An owner lists a machine with a daily or
hourly rate and a location; a farmer picks dates and sends a request. Each machine is
booked separately, so an owner with three tractors can rent out all three on the same
day.

### Produce marketplace — `/marketplace`

Grain, vegetables, fruit, livestock and milk, grouped under **Cereals & Pulses /
Vegetables / Fruits / Livestock / Dairy**. A seller lists a product with a quantity, a
unit (per kg, per quintal, per head, per litre) and a price. Produce is not booked —
the buyer contacts the seller — and every product page shows the district's reference
price beside the asking price, with the difference worked out.

### Agricultural labour — `/labour`

Ploughing, sowing and planting, weeding, spraying, irrigation, pruning, harvesting,
threshing and winnowing, nursery work, dairy milking, livestock herding, fencing,
loading and unloading, machine operation and general farm help. A worker registers
once per task at their own rate; all of their tasks share **one calendar**, so booking
them to weed on Tuesday makes them unavailable to harvest that day too. Results are
ranked nearest-first against the viewer's own district, and can be filtered by task,
experience, rate and gender.

### What every member can do

| | |
|---|---|
| **Browse and search** | By district, category, price range and free text, in either language. Searching without a category returns the whole section. |
| **List** | Create, edit and remove your own equipment, produce and labour listings, with photo uploads (multiple per listing, deletable individually). |
| **Request** | Pick a date range on a listing's availability calendar and send a booking request with a message. |
| **Approve / reject** | Owners see incoming requests on **Bookings → Received**, with a calendar of what is already committed. Approving auto-rejects the pending requests it has just made impossible and notifies those requesters. |
| **Block dates** | Owners mark their own machine or their own time unavailable — it is stored as an approved booking against their own listing. |
| **Track** | **Bookings → Mine** lists everything you have requested and its status. |
| **Be notified** | A bell in the header counts unread notifications: request received, request approved, request rejected. |
| **Report** | Flag a listing as fake, offensive or mispriced for an administrator to look at. |
| **Profile** | Set your district, village/town, phone and (optionally) gender — this is what makes the nearest-first labour ranking work for you. |
| **Switch language** | English ⇄ தமிழ், from the header, remembered in a cookie. |

### What an administrator can do — `/Admin`

A dashboard of member, listing and booking counts; a **members** list with district
and listing/booking totals; **category** management (add or delete categories per
module, with a group heading and a suggested unit); a **reports** queue where a flagged
listing can be dismissed or removed; a read-only view of **every booking** in the
system; and **market price** entry — a reference price per category per district, which
is what the produce pages compare against.

---

## Demo accounts

All seeded on first run, in the Development environment.

| Account | Password | |
|---|---|---|
| `user@agriconnect.local` | `User!Dev2026` | **Ordinary member — start here** |
| `admin@agriconnect.local` | `Admin!Dev2026` | Administrator |
| `murugan@agriconnect.local` | `Demo!Dev2026` | Owns the tractor, has requests to approve |
| `selvi@agriconnect.local` | `Demo!Dev2026` | Has a pending request |
| `kumar@agriconnect.local` | `Demo!Dev2026` | Sells livestock |

Also `lakshmi`, `arumugam`, `devi`, `raja`, `ganesan`, `meena`, `saravanan`,
`vasanthi`, `perumal`, `kalaiselvi`, `sundaram`, `anitha`, `velu`, `rajeshwari`,
`ramasamy`, `bhuvana`, `karthik`, `pushpa`, `ilango`, `malathi`, `dhanraj`,
`sumathi`, `mohan` and `jeyanthi` — all `@agriconnect.local`, all `Demo!Dev2026`.

More members are generated, three in every district, with a numbered login such as
`saraswathi32@agriconnect.local` — same password. `SELECT Email FROM AspNetUsers`
lists them, or the admin's **Users** page does. They are ordinary members: they own
listings, make bookings and have requests waiting on them.

Anyone can also **Register** their own account from the site — there is no email
confirmation step to get through.

---

## Technology

| | |
|---|---|
| Runtime | .NET 8 (LTS) |
| Web | ASP.NET Core 8 MVC + Razor views |
| Data | Entity Framework Core 8.0.33, code-first with migrations |
| Database | SQL Server 2022 — `sp_getapplock` and filtered indexes are used, so SQL Server specifically |
| Auth | ASP.NET Core Identity with the default UI, plus a custom `/Profile` page |
| UI | Bootstrap 5, jQuery validation — no SPA framework, no build step |
| Localisation | `IStringLocalizer` over one shared `.resx`, English + Tamil |
| Container | Multi-stage Dockerfile + docker-compose with SQL Server |

No npm, no webpack, no Node. `wwwroot/lib` holds the three client libraries as files.

---

## Project layout

```
AgriConnect/
  Program.cs              startup, localisation, data protection, migrate + seed
  BookingService.cs       the one service class: overlap rules and approval locking
  Controllers/            Listings, Bookings, Notifications, MarketPrices, Admin,
                          Profile, Culture, Home
  Models/                 entities, enums, search and slug helpers, district list
  Data/
    ApplicationDbContext.cs
    Migrations/           5 migrations, applied automatically on startup
    DbSeeder.cs           the demo dataset
  ViewComponents/         notification bell
  Views/                  Razor + Bootstrap
  Resources/
    SharedResource.ta.resx  every Tamil string
  wwwroot/
    seed-images/          369 licensed photos + credits.json
    uploads/              created at runtime (git-ignored)
    css/ js/ lib/ fonts/
docs/
  user-guide.md           walkthrough for an end user
  er-diagram.md           entity relationships
  image-credits.md        author and licence for all 369 photos
tools/
  fetch-seed-images.ps1   re-download the photo set from Wikimedia Commons
  write-image-credits.ps1 regenerate docs/image-credits.md
  generate-er-diagram.sql
Dockerfile
docker-compose.yml
```

Only `BookingService` is a service class. Search is one method on one controller,
image upload is three lines, and a notification is one insert — none of them earns a
class of its own until something else calls them.

---

## Data model

Seven tables of our own, on top of the ASP.NET Core Identity tables.

| Table | What it holds |
|---|---|
| `Listings` | Equipment, produce **and** labour — one table, a `Type` discriminator. Title, description, price, `PriceUnit`, optional quantity, village/town/district, optional skills and years of experience (labour only), `Status`. |
| `Bookings` | `ListingId`, `RequesterId`, `FromDate`, `ToDate` (half-open), `Status`, message, decision note. An owner's own block is a row where requester = owner. |
| `Categories` | Per module: name, a group heading (Tillage, Vegetables, …) and a suggested `PriceUnit`. Unique on (`ListingType`, `Name`). |
| `ListingImages` | Filename + sort order, cascade-deleted with the listing. |
| `MarketPrices` | Reference price for a category in a district, on a date. Administrator-maintained. |
| `Notifications` | Per user, with `IsRead` — what the header bell counts. |
| `ListingReports` | A flagged listing, its reason, and whether an administrator has resolved it. |
| `AspNetUsers` | Extended (`ApplicationUser`) with full name, phone, village, town, district and optional gender. |

Districts are **not** a table — see below. The schema is drawn out in
[docs/er-diagram.md](docs/er-diagram.md).

---

## How it is built

**Three modules, one table.** Equipment rental, the marketplace and labour are the
same shape — someone offers something at a price in a place, and two of the three are
reserved over a date range — so there is one `Listing` table with a `Type`
discriminator and one `Booking` table. The URLs `/equipment`, `/marketplace` and
`/labour` map onto it through `ListingSlug`, so the site still reads as three modules.
Produce listings simply never get a booking.

**Every section is browsed category-first**, through one `CategoryIndex` action and
one view. `/equipment` lists machines under Tillage / Sowing / Harvesting / Irrigation
/ Haulage headings; `/marketplace` lists products — Sheep, Tomato, Samba Paddy — under
Livestock / Vegetables / Fruits; `/labour` lists the tasks, flat. Picking one gives
the grid, as does any search filter.

**Each index counts whatever is the resource**, which is the same rule as booking.
Equipment counts **machines**, because each is separately bookable — Tractor reads
"6 machines" from five owners, since one farmer has two. Labour counts **people**, as
a worker with three tasks is still one person. The marketplace counts **sellers**,
because that is who you contact.

A "worker profile" is not a table — it is every labour listing owned by one person,
which is what `/labour/worker/{id}` renders. Location is village/town/district text,
so "nearby" means *same district or town*, never a distance.

**Districts are a fixed list of all 38, not a table.** The set changes once a decade
by government order, not because a member added one. Free text is what makes a
location search useless — "Coimbatore", "coimbatore" and "CBE" are three districts to
a database and one to a farmer. The English name is stored and validated on submit;
Tamil is a label looked up by it.

**Gender is on the member, not the listing.** A worker is one person however many
tasks they register for, so it lives on `ApplicationUser` and is self-declared,
optional and nullable — null means not stated. It shows on labour rows and profiles
and can be filtered on there; the marketplace ignores it. `/Profile` is where it and
the location are set, which Identity's default UI does not cover: before that page
existed a registered member had no way to set their district at all, so the
nearest-first labour ranking could never fire for anyone but seeded users.

**Marketplace figures are aggregated per unit, never across units.** A product
suggests its unit (sheep by the head, tomatoes by the kilo) but a seller may override
it. So 6 head and 300 kg are never added, and a product sold both ways shows two
figures side by side rather than one blended number that is true of neither.
`CategoryIndexRow.Build` is that rule.

**Two roles, not five.** Administrator, Farmer, Equipment Owner, Worker and Buyer —
only the first is a role; the rest are things one person does in the same week.
Identity has `Administrator` and everyone else, and ownership checks ask whether
*this* user owns *this* listing.

**Dates are half-open.** A booking covers `[FromDate, ToDate)`. One ending on the
10th and one starting on the 10th do not clash — the item changes hands that morning.

**What a booking reserves differs by type.** Equipment reserves the *machine*, so an
owner with three tractors can rent all three on the same dates. Labour reserves the
*person*: a worker registers once per task — Weeding at ₹420, Harvesting at ₹600 —
and those tasks share **one calendar**. `Listing.BookingResourceKey` is that rule in
one line, and `BookingService.CompetingBookings` is every query that depends on it.

**Approval is the only hard part.** `BookingService.ApproveAsync` takes
`sp_getapplock` on the resource inside a transaction, re-checks for a clash, then
writes. Without it, two owners clicking Approve at the same instant both read "free"
and both commit. The lock is keyed on `BookingResourceKey`, not on the listing: lock
per listing instead and two farmers approving requests on two tasks of one worker take
two different locks, both read a free calendar, and both commit. Approving also
auto-rejects the pending requests it just made impossible, and notifies those
requesters.

**Owner downtime is a booking.** Blocking dates writes an approved booking against
your own listing, so there is no second table and no second overlap check.

**An action's arguments are only as good as the request.** If MVC cannot read a
request body it records the failure and leaves every parameter out, so the action runs
with `type` null and `filter` null and falls over on whichever it touches first. A
plain anonymous `GET /equipment` carrying `Content-Type: multipart/form-data` and no
body was enough to trigger that, and every page in the listings module answered **500**
to it. `ListingsController.OnActionExecuting` refuses an unreadable body with 400 and
takes the section slug from the route rather than the bound parameter. Guarding the
seven call sites individually would have been seven chances to miss one.

**Search input is clamped, not trusted.** Every filter value is compared against a
column, and SQL Server refuses a comparison the column cannot hold — a search term
longer than `Title`, or a price outside `decimal(10,2)`, returned 500 rather than no
results. `ListingSearch.Normalised()` trims and clamps each field once, where the
query string enters, instead of at each of the nine places that read it. Values are
clamped rather than rejected: a mistyped filter should widen, not become a dead end.

**The key ring lives on a volume, not in the container.** Data protection keys sign
the authentication cookie and every antiforgery token, and ASP.NET Core's default
store is a folder inside the container's own filesystem. Rebuilding the image threw
them away, so every member was signed out and any form already open in a browser
failed its antiforgery check on submit. `DataProtection:KeyPath` points the key ring
at the `keys` volume, and `SetApplicationName` fixes its identity so a move or rename
cannot invalidate it either. Running from source leaves the setting empty and keeps
the default, which already persists in the user profile.

> The keys are stored unencrypted — on Linux there is no DPAPI equivalent to use by
> default. The volume's permissions are what protects them (the files are `0600`).
> A real deployment should encrypt the ring with a certificate.

**A status code with no body is a blank page.** `UseStatusCodePagesWithReExecute`
sends 404, 403 and 400 to the error view, which names what happened and links back
into the three sections. `HomeController.Error` restores the status code after the
re-execute, because otherwise a missing page answers 200 with "page not found"
written on it and every crawler and script believes it.

---

## Demo data

The seed covers **all 38 districts of Tamil Nadu** — about **143 members** and
**265 listings** across 56 categories, with several hundred bookings. How deep a
category goes varies: one to six for equipment and the marketplace, six to ten for
labour, and never more than there are photographs to illustrate it, so no two cards
on a page show the same picture. Most workers are registered for two or three tasks,
which is what the shared calendar is for.

Dates are relative to the day it is seeded, so the data never goes stale: roughly
eighty days of finished work behind today and a month of diary ahead, with pending
requests sitting in owners' queues. The random seed is fixed, so two runs produce the
same database and a bug found on one is reproducible on the next.

**The demo data obeys the same rules the application does.** The seeder generates
bookings through `BookingService.Overlaps` and keys occupancy on
`Listing.BookingResourceKey`, rather than writing rows straight into the table. So no
seeded worker is ever in two places at once, and the calendar a farmer sees is one the
application would itself have produced. Pending requests are deliberately allowed to
clash — that is the queue an owner has to work through, and resolving it is what the
lock in `ApproveAsync` guards.

Demo data is **Development only**. Outside it, `Seed:AdminPassword` is required and
only the administrator account is created — see
[INSTALL.md → Configuration reference](INSTALL.md#configuration-reference).

---

## Listing photos

Every seeded listing carries a photo from `AgriConnect/wwwroot/seed-images`. These are
**freely licensed Wikimedia Commons images, not AgriConnect's own — replace them
before using this for anything real.** Author and licence for each are in
[docs/image-credits.md](docs/image-credits.md), which CC BY and CC BY-SA require.

`DbSeeder` copies them into `wwwroot/uploads` under generated names, so seeded and
user-uploaded photos travel the same code path and the views need no special case.

**Photos are matched by convention, not a lookup table.** A category called *Power
Weeder* uses `power-weeder-1.jpg`, `power-weeder-2.jpg` and so on, and the seeder
hands the numbered files out in rotation — which is what stops six tractors all
showing the same machine. Add another numbered file and it joins the rotation; no code
changes. Replacing a photo you dislike is just overwriting that file.

```powershell
tools/fetch-seed-images.ps1                 # all of them, several per category
tools/fetch-seed-images.ps1 -Only tractor   # just one subject
tools/write-image-credits.ps1               # regenerate the credits table
```

The fetcher scores candidates on title relevance and shape, and rejects titles
matching a blocklist. Without that it happily picked a Banana Bunchy Top *Virus* plate
for "bananas" and a 1910 milk wagon for "cow milk" — Commons search matches words, not
subjects. The blocklist matches at a word boundary, because plain substring matching
discarded every carrot photo: "car**rot**".

---

## Languages

English and **தமிழ்**, switchable from the header and remembered in a cookie.
One shared `.resx` keyed by the English text, so an untranslated string falls back to
readable English rather than a resource key.

Formatting stays on `en-IN` in both languages while only the UI culture switches.
That is deliberate: `<input type="date">` always posts ISO `yyyy-MM-dd` and browsers
always post decimals with a dot, but model binding parses them with the *request*
culture — switching the whole culture to `ta-IN` would start rejecting perfectly good
dates and prices on submit. `en-IN` also already gives the lakh/crore digit grouping
a rupee figure wants, in both languages.

Razor's default encoder escapes everything outside Basic Latin, which would send a
Tamil page out as `&#xBB5;&#xBBF;…` — correct, and roughly six times the bytes of the
text. The Tamil Unicode block is allowed through explicitly; every response declares
UTF-8, and the encoder still escapes the characters that matter for XSS.

A bundled Noto Sans Tamil webfont means the script renders the same on a machine with
no Tamil font installed.

---

## Not included

No SignalR, email, maps, SMS, payments, ratings, chat, caching, pagination, or a
repository layer over EF Core. Machine learning and price prediction are out of scope;
the market price screen shows stored figures and subtracts them.

---

## Licence

The application code is this project's own. The seed photographs in
`AgriConnect/wwwroot/seed-images` are **not** — they come from Wikimedia Commons under
CC BY, CC BY-SA and public-domain terms, and each one's author and licence is listed
in [docs/image-credits.md](docs/image-credits.md). Honour those terms, or replace the
photos, before putting this anywhere public.
