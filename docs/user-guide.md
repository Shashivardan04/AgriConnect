# AgriConnect — user guide

Written for people using the site, not for people building it.

## Getting an account

Choose **Register** and give an email address and password. There is nothing to
choose about what kind of member you are: list a tractor and you are an equipment
owner, list tomatoes and you are a seller, fill in your skills and farmers can
request you for work. Most people end up doing several of these.

Then open **your name in the top bar** to fill in your profile: your name, phone
number, and your village, town and district. This matters more than it looks —
listings you create are pre-filled from it, other members search on it, and labour
results are ordered by who is nearest to *your* district. Leave it blank and the site
cannot tell who is close to you.

You can also state whether you are male or female, or leave it as **Prefer not to
say**. It appears on your labour profile if you give it, and farmers can filter on it
when looking for workers.

## Finding something

The three sections in the top bar are **Equipment**, **Marketplace** and **Labour**.
Each one opens on its categories rather than a wall of listings, so you start by
saying what you want:

```
EQUIPMENT                        MARKETPLACE              LABOUR
 Tillage and land preparation     Livestock                Harvesting    6 people
   Tractor      6 machines          Sheep    6 sellers     Weeding       5 people
   Plough       3 machines          Goat     4 sellers     Ploughing     3 people
 Harvesting and threshing         Vegetables
   Harvester    3 machines           Tomato   4 sellers
```

Equipment counts **machines**, because each one is booked separately — six tractors
from five owners means six you could hire. Labour counts **people**, because a worker
with three tasks is still one person. The marketplace counts **sellers**.

Pick a category and you get the listings, with the same filters everywhere:

| Filter | What it does |
|---|---|
| Search | Matches the title, description and (for labour) skills |
| Category | Tractors, vegetables, harvesting, and so on |
| District | The district the item is in |
| Town or village | Narrows within a district |
| Min / max price | Daily rate, price per kg, or wage, depending on the section |
| Available from / to | Equipment and labour only — hides anything already booked for those dates |
| Gender | Labour only, and only where the worker has stated it |
| Sort by | Newest, price, or name |

Contact details are shown once you are signed in.

## Hiring labour

**Labour** lists the tasks — harvesting, sowing, weeding, spraying, general farm
help — with how many people have registered for each and the lowest daily wage among
them. Pick the task you need and you get the people who do it, each with their own
wage, village and years of experience.

They are ordered by best match: people in your district first, then the lowest wage,
then the most experience. You can re-sort or filter at any point. "Nearby" here means
*the same district, and then the same town* — the site records place names, not map
positions, so it cannot tell you who is ten kilometres closer.

A worker's name opens their profile: every task they have registered for, the wage
for each, and their calendar.

### One worker, one calendar

Somebody may register for several tasks — Raja weeds for ₹420 a day, harvests for
₹600 and transplants for ₹500. He is still one person, so **all three share a single
calendar**. Book him to harvest on the 10th to the 12th and he is unavailable for
weeding and sowing on those days as well, to you and to everyone else. He drops out
of searches for *any* of his tasks over those dates.

This works the other way round too: when he marks himself unavailable on his profile,
it applies to everything he does.

### Registering for a task yourself

Choose **Register for a task** in the Labour section, pick the work, and set the wage
you expect for it. Register again for each further task you take on, each with its own
wage — tasks you have already registered for are not offered twice.

## Making a booking

This works the same way for equipment and for labour. Open the listing — or a
worker's profile, choosing which task you need — and the calendar shows confirmed
bookings in red; everything else is free. Pick your dates, add a short message, and
choose **Request these dates**.

**The end date is the day you hand it back and is not charged.** A booking from the
10th to the 12th is two days, and somebody else may start on the 12th.

Your request is *pending* until the owner decides. Watch **Alerts** in the top bar,
or **My bookings**, for the answer. You can withdraw a request at any time with
**Cancel**.

If your dates are already confirmed for someone else, the site refuses the request
straight away rather than letting you wait for a refusal.

## Responding to requests

**My bookings → Requests for my listings** lists everything people have asked for,
pending first.

- **Approve** confirms it. Anything still pending that clashes with those dates is
  declined automatically and those members are told why — you do not have to tidy up.
  If you are a worker, that covers requests for your *other* tasks on those days too.
- **Decline** refuses one request without affecting the others.
- **Mark complete** closes a booking once the job is done.

Two people cannot end up with the same equipment on the same day, even if you and a
co-owner approve two requests at the same moment.

### Blocking your own dates

On your own listing, use **Block these dates** for servicing, repairs, or days you
are simply not available. Blocked dates look the same as booked ones to everybody
else, and nobody can request them.

A worker blocking dates blocks them across every task they do — it is one calendar.
Equipment is per machine: servicing one tractor does not take the other off the site.

## Buying produce and livestock

**Marketplace** lists what is for sale by product, grouped under Livestock,
Vegetables, Fruits, Grains and Pulses, and Dairy. Each product shows how many people
are selling it, how much there is in total, and the lowest price:

```
LIVESTOCK
  Sheep      4 sellers   27 head   from ₹7,800 per head
  Goat       1 seller    10 head   from ₹9,500 per head
VEGETABLES
  Tomato     2 sellers  951 kg     from ₹21 per kg
```

Open **Sheep** and you get every sheep on offer in every district, each with the
number of head and the price per head. Open **Tomato** and you get kilos and ₹/kg.
Filter by district or town from there if you only want what is near you.

Quantities and prices are totalled **separately for each unit**. If most people sell
sheep by the head and one sells by weight, you will see both figures side by side —
`240 kg · from ₹310 per kg` and `27 head · from ₹7,800 per head` — rather than one
averaged number that would be true of neither.

## Selling produce and livestock

List it under **Marketplace**, choosing the product — Sheep, Tomato, Groundnut — and
give a quantity and an asking price. Picking the product sets the unit it is normally
sold in, and the quantity box says what it wants: *Quantity (head)* for sheep,
*Quantity (kg)* for tomatoes. You can change the unit if you genuinely sell that way.

Produce is not booked by date — buyers see your contact details and get in touch.

Where the administrator has recorded a reference price for your category and
district, your listing shows how your asking price compares:

> **14% above** the reference price of ₹21.00 per kg.
> Vegetables in Coimbatore, as of 24 Oct 2026, District Agricultural Marketing Committee.

That figure is a recorded market price, **not** a valuation or a forecast. Prices
move with variety, quality, grade and date. Treat it as one more piece of
information, not as what your crop is worth.

When it sells, edit the listing and set its status to **Sold** — that keeps it
visible with a Sold badge. **Remove listing** hides it entirely while keeping its
booking history.

## Market prices

**Market prices** in the top bar lists every reference price on record, filterable by
district and category, each with its source and the date it applies to.

## Reporting a listing

On any listing that is not yours, **Report this listing** sends it to the
administrators with your reason. They either dismiss the report or remove the
listing.

## For administrators

**Admin** in the top bar covers:

- **Users** — accounts, districts, and how much each has listed or booked
- **Categories** — add or remove categories per section. A category in use by a
  listing cannot be removed
- **Reference market prices** — add, edit and delete the figures sellers compare
  against. Always record the source and the date
- **Reported listings** — dismiss, or remove the listing
- **All bookings** — every request on the platform

Administrators can also approve or decline bookings on anyone's listing.
