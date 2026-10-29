using System.Text.RegularExpressions;
using AgriConnect.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect.Data;

public static class DbSeeder
{
    public const string AdminRole = "Administrator";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, IWebHostEnvironment env)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(AdminRole))
            await roles.CreateAsync(new IdentityRole(AdminRole));

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = config["Seed:AdminEmail"] ?? "admin@agriconnect.local";

        if (await users.FindByEmailAsync(email) is null)
        {
            // Never ship a hardcoded password outside Development.
            var password = config["Seed:AdminPassword"] ?? (env.IsDevelopment()
                ? "Admin!Dev2026"
                : throw new InvalidOperationException(
                    "Seed:AdminPassword must be configured outside Development."));

            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Platform Administrator",
                District = Districts.All[0]
            };

            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Admin seed failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));

            await users.AddToRoleAsync(admin, AdminRole);
        }

        // Demo data is for Development, and "Seed:DemoData=false" turns it off there
        // too. The HTTP tests use that: they need the real application started, not
        // eighty password hashes and several hundred listings they never look at.
        if (env.IsDevelopment() && config.GetValue("Seed:DemoData", true))
            await SeedDemoDataAsync(services.GetRequiredService<ApplicationDbContext>(), users, config, env);
    }

    /// <summary>
    /// Development-only demo data, so every screen has something to render on first
    /// run and the walkthrough in the README works without typing anything in.
    /// </summary>
    /// <summary>
    /// Photo for each seeded listing, keyed by title so reordering the list above
    /// cannot silently put a goat on the tractor. Files live in
    /// wwwroot/seed-images; see docs/image-credits.md for sources and licences.
    /// </summary>
    /// <summary>
    /// Photos are matched by convention, not by a lookup table: a category called
    /// "Power Weeder" uses wwwroot/seed-images/power-weeder-N.jpg. Several numbered
    /// files per category is what stops six tractors all showing the same machine -
    /// they are handed out in rotation. Drop another numbered file in and it joins
    /// the rotation with no code change, which is why the old five dictionaries of
    /// title-to-image mappings are gone.
    /// </summary>
    private static string ImageStem(string categoryName) =>
        categoryName.ToLowerInvariant().Replace(" ", "-");

    /// <summary>
    /// How many different photographs exist for each category stem. This is the hard
    /// ceiling on how many listings a category can hold before two cards on the same
    /// page show the same machine, and the seeder caps its targets against it rather
    /// than generating listings it cannot illustrate.
    /// </summary>
    private static Dictionary<string, int> ImageVariantCounts(IWebHostEnvironment env)
    {
        var source = Path.Combine(env.WebRootPath, "seed-images");
        if (!Directory.Exists(source)) return [];

        return Directory.GetFiles(source, "*.jpg")
            .Select(f => Regex.Match(Path.GetFileNameWithoutExtension(f), @"^(?<stem>.+)-\d+$"))
            .Where(m => m.Success)
            .GroupBy(m => m.Groups["stem"].Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Copies the bundled photos into wwwroot/uploads under generated names, so
    /// seeded listings and uploaded ones are served by exactly the same code path.
    /// </summary>
    private static async Task AttachSeedImagesAsync(
        ApplicationDbContext db, List<Listing> listings, IWebHostEnvironment env)
    {
        var source = Path.Combine(env.WebRootPath, "seed-images");
        if (!Directory.Exists(source)) return;   // images are optional, seeding is not

        var uploads = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploads);

        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var variants = new Dictionary<string, string[]>();
        var nextVariant = new Dictionary<string, int>();

        // Categories that ran out of photographs and had to show one twice. Logged at
        // the end rather than silently tolerated: "every listing has its own picture"
        // is either true or it is a bug, and counting it is how we know which.
        var shortfall = new Dictionary<string, int>();

        foreach (var listing in listings)
        {
            if (!categories.TryGetValue(listing.CategoryId, out var category)) continue;

            var stem = ImageStem(category.Name);

            if (!variants.TryGetValue(stem, out var files))
            {
                // The numeric suffix has to be checked explicitly: a plain "cow-*"
                // glob also matches cow-milk-1.jpg, which would put a milk churn on
                // a cow listing.
                files = Directory.GetFiles(source, stem + "-*.jpg")
                    .Where(f => Regex.IsMatch(Path.GetFileName(f),
                        "^" + Regex.Escape(stem) + @"-\d+\.jpg$"))
                    .OrderBy(f => f)
                    .ToArray();
                variants[stem] = files;
            }

            if (files.Length == 0) continue;

            // Hand them out one each, never twice, so no two cards in a category show
            // the same photograph. The targets above are capped against this same
            // count, so running out should not happen; wrapping is the fallback for
            // the hand-written listings, which are not subject to that cap.
            var index = nextVariant.GetValueOrDefault(stem);
            nextVariant[stem] = index + 1;

            if (index >= files.Length)
                shortfall[category.Name] = shortfall.GetValueOrDefault(category.Name) + 1;

            var fileName = $"{Guid.NewGuid():N}.jpg";
            File.Copy(files[index % files.Length], Path.Combine(uploads, fileName), overwrite: true);
            db.ListingImages.Add(new ListingImage { ListingId = listing.Id, FileName = fileName });
        }

        await db.SaveChangesAsync();

        if (shortfall.Count > 0)
            Console.WriteLine("Seed images: " + string.Join(", ",
                shortfall.Select(s => $"{s.Key} reused {s.Value}")) +
                ". Fetch more with tools/fetch-seed-images.ps1 -Only <stem>.");
    }

    private static async Task SeedDemoDataAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> users, IConfiguration config,
        IWebHostEnvironment env)
    {
        if (await db.Categories.AnyAsync()) return;

        const string demoPassword = "Demo!Dev2026";
        var today = DateOnly.FromDateTime(DateTime.Today);

        // One fixed seed for every random choice below, so two runs of the seeder
        // produce the same database. Random.Shared made the demo different every
        // time, which meant a bug seen on one run could not be reproduced on the
        // next. Dates stay relative to today, so the data never goes stale.
        var rng = new Random(20261028);

        // --- categories (administrator-managed, SOW §2.3.1) ---
        var cats = new List<Category>();

        void Cat(ListingType t, params string[] names) =>
            cats.AddRange(names.Select(n => new Category { ListingType = t, Name = n }));

        // Marketplace categories are the product itself, so a buyer can ask for sheep
        // rather than "livestock". The group is the heading they sit under, and the
        // unit is what that product is normally sold by.
        void Product(string group, PriceUnit unit, params string[] names) =>
            cats.AddRange(names.Select(n => new Category
            {
                ListingType = ListingType.Produce, Name = n, Group = group, SuggestedUnit = unit
            }));

        // Equipment is grouped by the job it does, the same way products are grouped.
        // Machines are hired by the day, so the unit is the same throughout.
        void Machine(string group, params string[] names) =>
            cats.AddRange(names.Select(n => new Category
            {
                ListingType = ListingType.Equipment, Name = n, Group = group,
                SuggestedUnit = PriceUnit.PerDay
            }));

        Machine("Tillage and land preparation", "Tractor", "Rotavator", "Plough", "Power Weeder");
        // "Sowing and seeding" rather than "Sowing and planting": the labour task of
        // that name would collide with it in the translation file, where keys are the
        // English text and are matched without regard to case.
        Machine("Sowing and seeding", "Seed Drill");
        Machine("Harvesting and threshing", "Harvester", "Thresher");
        Machine("Irrigation and spraying", "Water Pump", "Sprayer");
        Machine("Haulage and fodder", "Trailer", "Chaff Cutter");

        Cat(ListingType.Labour,
            "Harvesting", "Sowing and Planting", "Weeding", "Spraying", "General Farm Help",
            "Ploughing", "Irrigation", "Threshing and Winnowing", "Pruning", "Fencing",
            "Livestock Herding", "Dairy Milking", "Loading and Unloading", "Nursery Work",
            "Machine Operator");

        Product("Livestock", PriceUnit.PerHead, "Sheep", "Goat", "Cow", "Buffalo", "Poultry", "Duck");
        Product("Vegetables", PriceUnit.PerKg,
            "Tomato", "Onion", "Brinjal", "Okra", "Drumstick", "Green Chilli", "Carrot", "Beans");
        Product("Fruits", PriceUnit.PerKg, "Mango", "Banana", "Guava", "Papaya", "Sapota", "Pomegranate");
        Product("Grains and Pulses", PriceUnit.PerQuintal,
            "Samba Paddy", "Groundnut", "Black Gram", "Green Gram", "Maize", "Ragi", "Cotton");
        Product("Dairy", PriceUnit.PerLitre, "Cow Milk", "Buffalo Milk", "Goat Milk");

        db.Categories.AddRange(cats);
        await db.SaveChangesAsync();

        Category C(string name) => cats.First(c => c.Name == name);

        // --- users ---
        // Everyone who takes part in the demo, in creation order. The demo account is
        // made last and deliberately stays out of it: it has to look like a member who
        // registered a minute ago, with no listings and no history.
        var roster = new List<ApplicationUser>();

        async Task<ApplicationUser> User(string local, string name, string village, string town, string district,
                                         Gender? gender = null, string? email = null, string? password = null)
        {
            email ??= $"{local}@agriconnect.local";
            var u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = "9" + rng.Next(100000000, 999999999),
                FullName = name,
                Gender = gender,
                Village = village,
                Town = town,
                District = district
            };
            var r = await users.CreateAsync(u, password ?? demoPassword);
            if (!r.Succeeded)
                throw new InvalidOperationException(
                    $"Seeding {local} failed: " + string.Join("; ", r.Errors.Select(e => e.Description)));
            roster.Add(u);
            return u;
        }

        const Gender m = Gender.Male, f = Gender.Female;

        // The seven the demo walkthrough names.
        var murugan = await User("murugan", "Murugan S", "Kinathukadavu", "Pollachi", "Coimbatore", m);
        var lakshmi = await User("lakshmi", "Lakshmi R", "Thottipalayam", "Sulur", "Coimbatore", f);
        var arumugam = await User("arumugam", "Arumugam K", "Vadugapatti", "Bhavani", "Erode", m);
        var selvi = await User("selvi", "Selvi M", "Papanasam", "Kumbakonam", "Thanjavur", f);
        var kumar = await User("kumar", "Kumar P", "Alanganallur", "Melur", "Madurai", m);
        var devi = await User("devi", "Devi A", "Attur", "Attur", "Salem", f);
        var raja = await User("raja", "Raja V", "Manapparai", "Manapparai", "Tiruchirappalli", m);

        // Everyone else, so each district has people in it and a product page is a
        // real comparison rather than one or two cards.
        var ganesan = await User("ganesan", "Ganesan R", "Annur", "Annur", "Coimbatore", m);
        var meena = await User("meena", "Meena T", "Karamadai", "Mettupalayam", "Coimbatore", f);
        var saravanan = await User("saravanan", "Saravanan M", "Kodumudi", "Kodumudi", "Erode", m);
        var vasanthi = await User("vasanthi", "Vasanthi P", "Chithode", "Perundurai", "Erode", f);
        var perumal = await User("perumal", "Perumal K", "Thiruvidaimarudur", "Kumbakonam", "Thanjavur", m);
        var kalaiselvi = await User("kalaiselvi", "Kalaiselvi N", "Orathanadu", "Pattukkottai", "Thanjavur", f);
        var sundaram = await User("sundaram", "Sundaram V", "Usilampatti", "Usilampatti", "Madurai", m);
        var anitha = await User("anitha", "Anitha S", "Thirumangalam", "Thirumangalam", "Madurai", f);
        var velu = await User("velu", "Velu C", "Omalur", "Omalur", "Salem", m);
        var rajeshwari = await User("rajeshwari", "Rajeshwari G", "Mettur", "Mettur", "Salem", f);
        var ramasamy = await User("ramasamy", "Ramasamy D", "Lalgudi", "Lalgudi", "Tiruchirappalli", m);
        var bhuvana = await User("bhuvana", "Bhuvana M", "Thuraiyur", "Thuraiyur", "Tiruchirappalli", f);
        var karthik = await User("karthik", "Karthik S", "Oddanchatram", "Oddanchatram", "Dindigul", m);
        var pushpa = await User("pushpa", "Pushpa R", "Natham", "Natham", "Dindigul", f);
        var ilango = await User("ilango", "Ilango A", "Cheranmahadevi", "Ambasamudram", "Tirunelveli", m);
        var malathi = await User("malathi", "Malathi K", "Nanguneri", "Nanguneri", "Tirunelveli", f);
        var dhanraj = await User("dhanraj", "Dhanraj P", "Gudiyatham", "Gudiyatham", "Vellore", m);
        var sumathi = await User("sumathi", "Sumathi V", "Vaniyambadi", "Vaniyambadi", "Vellore", f);
        var mohan = await User("mohan", "Mohan L", "Tindivanam", "Tindivanam", "Villupuram", m);
        var jeyanthi = await User("jeyanthi", "Jeyanthi B", "Kallakurichi", "Kallakurichi", "Villupuram", f);

        // Three more members in every district, generated rather than typed out.
        // Twenty-nine people cannot fill fifty-six categories: half of them came up
        // empty or with a single card, which is what made the site read as a mock-up.
        //
        // Three per district rather than five: the state has thirty-eight of them,
        // and every account costs a PBKDF2 hash at seed time. Three still puts
        // somebody in every district, which is what the nearest-first labour ranking
        // needs to be worth anything.
        //
        // Village|town pairs so an address is plausible, not just a district.
        // A village and the taluk town it sits in, for every district in the state.
        // Districts.All is the authority; a district with no entry here would throw
        // on the lookup below, which is deliberate - it is better to fail at seed
        // time than to quietly leave a district with no members in it.
        //
        // Chinnasalem and Thiyagadurgam moved to Kallakurichi, and Vaniyambadi to
        // Tirupathur, when those districts were created out of Villupuram and
        // Vellore. They are listed under their district of today.
        var places = new Dictionary<string, string[]>
        {
            ["Ariyalur"] = ["Thirumanur|Ariyalur", "Andimadam|Andimadam", "Sendurai|Sendurai", "Jayankondam|Jayankondam"],
            ["Chengalpattu"] = ["Madurantakam|Madurantakam", "Thiruporur|Thiruporur", "Cheyyur|Cheyyur", "Pallavaram|Tambaram"],
            ["Chennai"] = ["Manali|Madhavaram", "Sholinganallur|Sholinganallur", "Perungudi|Perungudi", "Villivakkam|Villivakkam"],
            ["Coimbatore"] = ["Kinathukadavu|Pollachi", "Thondamuthur|Coimbatore South", "Annur|Annur",
                              "Karamadai|Mettupalayam", "Sultanpet|Sulur", "Madukkarai|Madukkarai"],
            ["Cuddalore"] = ["Panruti|Panruti", "Kattumannarkoil|Chidambaram", "Nellikuppam|Cuddalore", "Mangalampettai|Virudhachalam"],
            ["Dharmapuri"] = ["Palacode|Palacode", "Pennagaram|Pennagaram", "Karimangalam|Karimangalam", "Nallampalli|Dharmapuri"],
            ["Dindigul"] = ["Oddanchatram|Oddanchatram", "Natham|Natham", "Vedasandur|Vedasandur",
                            "Ayakudi|Palani", "Nilakottai|Nilakottai", "Sithayankottai|Athoor"],
            ["Erode"] = ["Kodumudi|Kodumudi", "Chithode|Perundurai", "Vadugapatti|Bhavani",
                         "Ikkarainegamam|Sathyamangalam", "Anthiyur|Anthiyur", "Kolappalur|Gobichettipalayam"],
            ["Kallakurichi"] = ["Chinnasalem|Chinnasalem", "Thiyagadurgam|Ulundurpet", "Kachirapalayam|Sankarapuram", "Mugaiyur|Tirukoilur"],
            ["Kanchipuram"] = ["Uthiramerur|Uthiramerur", "Walajabad|Walajabad", "Sriperumbudur|Sriperumbudur", "Kundrathur|Kundrathur"],
            ["Kanyakumari"] = ["Thovalai|Thovalai", "Suchindram|Nagercoil", "Thiruvattar|Thuckalay", "Kulasekharam|Kuzhithurai"],
            ["Karur"] = ["Kulithalai|Kulithalai", "Krishnarayapuram|Krishnarayapuram", "Aravakurichi|Aravakurichi", "Kadavur|Kadavur"],
            ["Krishnagiri"] = ["Bargur|Bargur", "Denkanikottai|Denkanikottai", "Pochampalli|Pochampalli", "Shoolagiri|Hosur"],
            ["Madurai"] = ["Alanganallur|Melur", "Chekkanurani|Usilampatti", "Kappalur|Thirumangalam",
                           "Vadipatti|Vadipatti", "Peraiyur|Peraiyur", "Kottampatti|Melur"],
            ["Mayiladuthurai"] = ["Kuthalam|Kuthalam", "Sembanarkoil|Sirkali", "Tharangambadi|Tharangambadi", "Manalmedu|Mayiladuthurai"],
            ["Nagapattinam"] = ["Vedaranyam|Vedaranyam", "Kilvelur|Kilvelur", "Thirukkuvalai|Thirukkuvalai", "Sikkal|Nagapattinam"],
            ["Namakkal"] = ["Rasipuram|Rasipuram", "Mohanur|Namakkal", "Elachipalayam|Tiruchengode", "Vennandur|Paramathi Velur"],
            ["Nilgiris"] = ["Gudalur|Gudalur", "Kotagiri|Kotagiri", "Aravenu|Coonoor", "Thuneri|Udhagamandalam"],
            ["Perambalur"] = ["Kunnam|Kunnam", "Veppanthattai|Veppanthattai", "Labbaikudikadu|Perambalur", "Alathur|Alathur"],
            ["Pudukkottai"] = ["Aranthangi|Aranthangi", "Illuppur|Illuppur", "Gandarvakottai|Gandarvakottai", "Alangudi|Alangudi"],
            ["Ramanathapuram"] = ["Paramakudi|Paramakudi", "Kamuthi|Kamuthi", "Mudukulathur|Mudukulathur", "Thiruvadanai|Thiruvadanai"],
            ["Ranipet"] = ["Sholinghur|Sholinghur", "Nemili|Nemili", "Kaveripakkam|Walajapet", "Banavaram|Arakkonam"],
            ["Salem"] = ["Thalaivasal|Attur", "Kadayampatti|Omalur", "Nangavalli|Mettur",
                         "Konganapuram|Edappadi", "Thevur|Sankari", "Nagalur|Yercaud"],
            ["Sivaganga"] = ["Kalayarkoil|Karaikudi", "Devakottai|Devakottai", "Manamadurai|Manamadurai", "Tirupuvanam|Tirupuvanam"],
            ["Tenkasi"] = ["Sankarankovil|Sankarankovil", "Alangulam|Alangulam", "Puliyangudi|Kadayanallur", "Kuruvikulam|Shenkottai"],
            ["Thanjavur"] = ["Papanasam|Kumbakonam", "Orathanadu|Pattukkottai", "Thiruvidaimarudur|Kumbakonam",
                             "Budalur|Budalur", "Sethubavachatram|Peravurani", "Thillaisthanam|Thiruvaiyaru"],
            ["Theni"] = ["Bodinayakanur|Bodinayakanur", "Cumbum|Cumbum", "Chinnamanur|Uthamapalayam", "Andipatti|Andipatti"],
            ["Thoothukudi"] = ["Kovilpatti|Kovilpatti", "Tiruchendur|Tiruchendur", "Srivaikuntam|Srivaikuntam", "Ottapidaram|Ottapidaram"],
            ["Tiruchirappalli"] = ["Vaiyampatti|Manapparai", "Pullambadi|Lalgudi", "Uppiliyapuram|Thuraiyur",
                                   "Thathaiyangarpet|Musiri", "Thottiyam|Thottiyam", "Marungapuri|Marungapuri"],
            ["Tirunelveli"] = ["Cheranmahadevi|Ambasamudram", "Panagudi|Nanguneri", "Manur|Manur",
                               "Valliyoor|Radhapuram", "Kalakkad|Kalakkad", "Pappakudi|Pappakudi"],
            ["Tirupathur"] = ["Alangayam|Vaniyambadi", "Natrampalli|Natrampalli", "Jolarpet|Jolarpet", "Madhanur|Ambur"],
            ["Tiruppur"] = ["Avinashi|Avinashi", "Palladam|Palladam", "Gudimangalam|Udumalaipettai", "Kundadam|Dharapuram"],
            ["Tiruvallur"] = ["Poonamallee|Poonamallee", "Gummidipoondi|Gummidipoondi", "Tiruttani|Tiruttani", "Pallipattu|Pallipattu"],
            ["Tiruvannamalai"] = ["Cheyyar|Cheyyar", "Polur|Polur", "Chengam|Chengam", "Kalasapakkam|Arani"],
            ["Tiruvarur"] = ["Mannargudi|Mannargudi", "Needamangalam|Needamangalam", "Kodavasal|Kodavasal", "Muthupet|Thiruthuraipoondi"],
            ["Vellore"] = ["Pallikonda|Gudiyatham", "Anaicut|Anaicut", "Kangeyanallur|Katpadi",
                           "Pernambut|Pernambut", "Latteri|K V Kuppam"],
            ["Villupuram"] = ["Olakkur|Tindivanam", "Melmalaiyanur|Gingee", "Kanai|Vanur", "Koonimedu|Marakkanam"],
            ["Virudhunagar"] = ["Sivakasi|Sivakasi", "Rajapalayam|Rajapalayam", "Aruppukottai|Aruppukottai", "Srivilliputhur|Srivilliputhur"],
        };

        string[] maleNames =
        [
            "Anbu", "Bala", "Chandran", "Dhanapal", "Elangovan", "Gopal", "Hari", "Jagadeesan",
            "Kannan", "Manikandan", "Natarajan", "Palanisamy", "Rajendran", "Sakthivel", "Thangavel",
            "Udhayakumar", "Vetrivel", "Yuvaraj", "Senthil", "Muthusamy", "Kathiresan", "Periyasamy",
            "Sivakumar", "Arulmozhi", "Ezhumalai"
        ];
        string[] femaleNames =
        [
            "Amudha", "Bhagyam", "Chitra", "Dhanalakshmi", "Eswari", "Gomathi", "Hemalatha",
            "Indhumathi", "Janaki", "Kavitha", "Lalitha", "Maheswari", "Nirmala", "Poongodi",
            "Revathi", "Saraswathi", "Tamilselvi", "Umadevi", "Valarmathi", "Yamuna", "Sasikala",
            "Kalpana", "Ezhilarasi", "Nandhini", "Vijaya"
        ];
        string[] initials = ["A", "B", "C", "D", "G", "K", "L", "M", "N", "P", "R", "S", "T", "V"];

        // Districts.All is the authority. Say which one is missing rather than
        // letting the lookup below throw a KeyNotFoundException that names nothing.
        var unplaced = Districts.All.Where(d => !places.ContainsKey(d) || places[d].Length < 3).ToArray();
        if (unplaced.Length > 0)
            throw new InvalidOperationException(
                "No village and town pairs for: " + string.Join(", ", unplaced) +
                ". Add at least three to the table in DbSeeder.");

        var seq = 0;
        foreach (var district in Districts.All)
            foreach (var place in places[district].OrderBy(_ => rng.Next()).Take(3))
            {
                var male = rng.Next(2) == 0;
                var first = (male ? maleNames : femaleNames)[rng.Next(maleNames.Length)];
                var address = place.Split('|');
                // The number keeps the login unique without inventing surnames that
                // collide: anbu7@agriconnect.local, and the password is the same as
                // everyone else's.
                await User($"{first.ToLowerInvariant()}{++seq}",
                    $"{first} {initials[rng.Next(initials.Length)]}",
                    address[0], address[1], district, male ? m : f);
            }

        // An ordinary member account for whoever is demonstrating the application:
        // no Administrator role, so it sees exactly what a real user sees. Gender is
        // left unset, which is what a newly registered member looks like.
        await User("demo", "Demo User", "Kinathukadavu", "Pollachi", "Coimbatore",
            email: config["Seed:DemoEmail"] ?? "user@agriconnect.local",
            password: config["Seed:DemoPassword"] ?? "User!Dev2026");

        // --- listings ---
        Listing L(ListingType type, ApplicationUser owner, string title, string category,
                  decimal price, PriceUnit unit, string desc) => new()
        {
            Type = type,
            OwnerId = owner.Id,
            Title = title,
            CategoryId = C(category).Id,
            Price = price,
            PriceUnit = unit,
            Description = desc,
            Village = owner.Village,
            Town = owner.Town,
            District = owner.District!,
            CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 60))
        };

        var tractor = L(ListingType.Equipment, murugan, "Mahindra 575 DI tractor", "Tractor", 1800, PriceUnit.PerDay,
            "47 HP, with trailer. Operator available on request.");

        var listings = new List<Listing>
        {
            tractor,
            L(ListingType.Equipment, murugan, "Rotavator, 7 feet", "Rotavator", 900, PriceUnit.PerDay,
                "Suits tractors above 45 HP."),
            L(ListingType.Equipment, lakshmi, "Paddy harvester", "Harvester", 2600, PriceUnit.PerDay,
                "Track type, suited to wet fields. Diesel on the renter."),
            L(ListingType.Equipment, lakshmi, "Battery knapsack sprayer", "Sprayer", 150, PriceUnit.PerDay,
                "16 litre tank, two spare batteries."),
            L(ListingType.Equipment, arumugam, "5 HP submersible pump set", "Water Pump", 400, PriceUnit.PerDay,
                "Includes 60 feet of delivery pipe."),
            L(ListingType.Equipment, arumugam, "Disc plough, 3 bottom", "Plough", 700, PriceUnit.PerDay,
                "Recently serviced, new discs."),
            L(ListingType.Equipment, selvi, "Mini tractor, 24 HP", "Tractor", 1100, PriceUnit.PerDay,
                "Good for narrow plots and garden land."),
            L(ListingType.Equipment, raja, "Power tiller", "Rotavator", 650, PriceUnit.PerDay,
                "Single owner, well maintained."),

            // Murugan's second tractor. Equipment reserves the machine, so both of his
            // can be hired on the same days - the index counts machines, not owners.
            L(ListingType.Equipment, murugan, "Sonalika 745 DI", "Tractor", 1900, PriceUnit.PerDay,
                "60 HP, mainly for the trailer."),
            L(ListingType.Equipment, ganesan, "John Deere 5050D", "Tractor", 2000, PriceUnit.PerDay,
                "50 HP, power steering. Driver extra."),
            L(ListingType.Equipment, saravanan, "Swaraj 744 FE", "Tractor", 1750, PriceUnit.PerDay,
                "With cage wheels for wet land."),
            L(ListingType.Equipment, karthik, "Massey Ferguson 241", "Tractor", 1650, PriceUnit.PerDay,
                "Reliable, available most weeks."),
            L(ListingType.Equipment, velu, "Rotavator, 5 feet", "Rotavator", 750, PriceUnit.PerDay,
                "Suits 35 HP and above."),
            L(ListingType.Equipment, dhanraj, "Power weeder, 7 HP", "Power Weeder", 550, PriceUnit.PerDay,
                "Petrol, light enough for one person."),
            L(ListingType.Equipment, pushpa, "Power weeder, mini", "Power Weeder", 400, PriceUnit.PerDay,
                "For vegetable beds and nurseries."),
            L(ListingType.Equipment, ramasamy, "Paddy thresher", "Thresher", 1200, PriceUnit.PerDay,
                "Tractor driven, 8 bag an hour."),
            L(ListingType.Equipment, mohan, "Multi-crop thresher", "Thresher", 1400, PriceUnit.PerDay,
                "Paddy, ragi and maize."),
            L(ListingType.Equipment, ilango, "Seed drill, 9 tyne", "Seed Drill", 800, PriceUnit.PerDay,
                "Accurate spacing, recently serviced."),
            L(ListingType.Equipment, jeyanthi, "Seed drill, 11 tyne", "Seed Drill", 900, PriceUnit.PerDay,
                "For groundnut and pulses."),
            L(ListingType.Equipment, sundaram, "Tractor trailer, 3 ton", "Trailer", 600, PriceUnit.PerDay,
                "Hydraulic tipping."),
            L(ListingType.Equipment, bhuvana, "Trailer, 5 ton", "Trailer", 850, PriceUnit.PerDay,
                "Twin axle, good tyres."),
            L(ListingType.Equipment, meena, "Chaff cutter, electric", "Chaff Cutter", 300, PriceUnit.PerDay,
                "2 HP motor, for fodder."),
            L(ListingType.Equipment, rajeshwari, "Chaff cutter, diesel", "Chaff Cutter", 350, PriceUnit.PerDay,
                "Works where there is no supply."),
            L(ListingType.Equipment, vasanthi, "Combine harvester", "Harvester", 2800, PriceUnit.PerDay,
                "Self propelled, operator included."),
            L(ListingType.Equipment, perumal, "Paddy harvester, track", "Harvester", 2500, PriceUnit.PerDay,
                "Good in standing water."),
            L(ListingType.Equipment, anitha, "HTP sprayer with motor", "Sprayer", 450, PriceUnit.PerDay,
                "100 metre hose included."),
            L(ListingType.Equipment, sumathi, "Boom sprayer, tractor", "Sprayer", 950, PriceUnit.PerDay,
                "12 metre boom, covers ground fast."),
            L(ListingType.Equipment, malathi, "Open well pump, 7.5 HP", "Water Pump", 500, PriceUnit.PerDay,
                "Starter and cable included."),
            L(ListingType.Equipment, kalaiselvi, "Diesel pump set", "Water Pump", 450, PriceUnit.PerDay,
                "Portable, for canal lifting."),
            L(ListingType.Equipment, ganesan, "Cultivator, 9 tyne", "Plough", 600, PriceUnit.PerDay,
                "Spring loaded tynes."),
            L(ListingType.Equipment, velu, "Mould board plough", "Plough", 650, PriceUnit.PerDay,
                "Two bottom, for deep ploughing."),

            L(ListingType.Produce, lakshmi, "Tomatoes, hybrid", "Tomato", 24, PriceUnit.PerKg,
                "Picked this morning. Graded and crated."),
            L(ListingType.Produce, selvi, "Tomatoes, country", "Tomato", 21, PriceUnit.PerKg,
                "Smaller fruit, better for gravy."),
            L(ListingType.Produce, lakshmi, "Drumstick", "Drumstick", 38, PriceUnit.PerKg,
                "Tender pods, bundled."),
            L(ListingType.Produce, selvi, "Samba paddy", "Samba Paddy", 2450, PriceUnit.PerQuintal,
                "Dried and ready for the mill."),
            L(ListingType.Produce, selvi, "Black gram", "Black Gram", 7800, PriceUnit.PerQuintal,
                "Last season's crop, well stored."),
            L(ListingType.Produce, kumar, "Jersey cow, 2nd calving", "Cow", 48000, PriceUnit.PerHead,
                "Yields about 12 litres a day."),
            L(ListingType.Produce, kumar, "Country goats", "Goat", 9500, PriceUnit.PerHead,
                "Six available, all healthy."),
            L(ListingType.Produce, devi, "Alphonso mangoes", "Mango", 95, PriceUnit.PerKg,
                "Tree ripened, no carbide."),
            L(ListingType.Produce, devi, "Banana, Poovan", "Banana", 32, PriceUnit.PerKg,
                "Sold by the bunch or by weight."),
            L(ListingType.Produce, arumugam, "Cow milk, bulk", "Cow Milk", 46, PriceUnit.PerLitre,
                "Morning and evening collection."),
            L(ListingType.Produce, raja, "Groundnut", "Groundnut", 6200, PriceUnit.PerQuintal,
                "Shelled, cleaned."),

            // Several sellers for one product, across districts, so a product page is a
            // real comparison rather than a single card.
            L(ListingType.Produce, kumar, "Country sheep, ewes", "Sheep", 8500, PriceUnit.PerHead,
                "Madras Red, bred locally. Four available."),
            L(ListingType.Produce, devi, "Ram lambs", "Sheep", 9200, PriceUnit.PerHead,
                "Eight months old, weaned."),
            L(ListingType.Produce, raja, "Sheep, mixed flock", "Sheep", 7800, PriceUnit.PerHead,
                "Selling the whole flock together if wanted."),
            L(ListingType.Produce, arumugam, "Mecheri sheep", "Sheep", 11000, PriceUnit.PerHead,
                "Known breed, papers available."),

            L(ListingType.Produce, selvi, "Onions, small", "Onion", 19, PriceUnit.PerKg,
                "Sambar onions, cleaned."),
            L(ListingType.Produce, murugan, "Big onions", "Onion", 16, PriceUnit.PerKg,
                "Graded, in 50 kg sacks."),
            L(ListingType.Produce, lakshmi, "Brinjal, purple", "Brinjal", 22, PriceUnit.PerKg,
                "Picked every other day."),
            L(ListingType.Produce, devi, "Maize, dried", "Maize", 2150, PriceUnit.PerQuintal,
                "Moisture under 14 percent."),
            L(ListingType.Produce, kumar, "Desi poultry", "Poultry", 420, PriceUnit.PerHead,
                "Country birds, free range."),
            L(ListingType.Produce, arumugam, "Buffalo milk", "Buffalo Milk", 58, PriceUnit.PerLitre,
                "High fat, morning collection only."),

            // Livestock
            L(ListingType.Produce, sundaram, "Kilakarsal sheep", "Sheep", 9800, PriceUnit.PerHead,
                "Native breed, hardy in dry land."),
            L(ListingType.Produce, ilango, "Sheep, young rams", "Sheep", 8900, PriceUnit.PerHead,
                "Vaccinated, ear tagged."),
            L(ListingType.Produce, velu, "Kanni Adu goats", "Goat", 11500, PriceUnit.PerHead,
                "Tall breed, good growth."),
            L(ListingType.Produce, malathi, "Goats, does in milk", "Goat", 10200, PriceUnit.PerHead,
                "Two litres a day each."),
            L(ListingType.Produce, pushpa, "Boer cross kids", "Goat", 8700, PriceUnit.PerHead,
                "Four months, weaned."),
            L(ListingType.Produce, perumal, "Jersey cross heifers", "Cow", 52000, PriceUnit.PerHead,
                "First calving due in two months."),
            L(ListingType.Produce, rajeshwari, "Kangayam cow", "Cow", 38000, PriceUnit.PerHead,
                "Native breed, good for draught."),
            L(ListingType.Produce, karthik, "Murrah buffalo", "Buffalo", 78000, PriceUnit.PerHead,
                "Nine litres a day, second calving."),
            L(ListingType.Produce, dhanraj, "Buffalo, graded", "Buffalo", 64000, PriceUnit.PerHead,
                "Quiet to handle, machine milked."),
            L(ListingType.Produce, anitha, "Giriraja birds", "Poultry", 480, PriceUnit.PerHead,
                "Dual purpose, eight weeks."),
            L(ListingType.Produce, jeyanthi, "Layer hens", "Poultry", 390, PriceUnit.PerHead,
                "In lay, culling the flock."),
            L(ListingType.Produce, bhuvana, "Country ducks", "Duck", 350, PriceUnit.PerHead,
                "Good foragers, reared on the tank."),

            // Vegetables
            L(ListingType.Produce, ganesan, "Tomatoes, hybrid grade A", "Tomato", 26, PriceUnit.PerKg,
                "Firm fruit, travels well."),
            L(ListingType.Produce, karthik, "Tomatoes, bulk", "Tomato", 18, PriceUnit.PerKg,
                "Oddanchatram market grade."),
            L(ListingType.Produce, pushpa, "Small onions", "Onion", 34, PriceUnit.PerKg,
                "Hand peeled, strong flavour."),
            L(ListingType.Produce, mohan, "Brinjal, long green", "Brinjal", 20, PriceUnit.PerKg,
                "Tender, picked to order."),
            L(ListingType.Produce, vasanthi, "Ladies finger", "Okra", 28, PriceUnit.PerKg,
                "Picked young, no fibre."),
            L(ListingType.Produce, meena, "Okra, bulk", "Okra", 24, PriceUnit.PerKg,
                "Daily supply through the season."),
            L(ListingType.Produce, sumathi, "Green chilli", "Green Chilli", 42, PriceUnit.PerKg,
                "Medium heat, long variety."),
            L(ListingType.Produce, jeyanthi, "Chilli, bird's eye", "Green Chilli", 95, PriceUnit.PerKg,
                "Very hot, small quantity."),
            L(ListingType.Produce, meena, "Carrots, Ooty", "Carrot", 38, PriceUnit.PerKg,
                "Hill grown, washed."),
            L(ListingType.Produce, saravanan, "Cluster beans", "Beans", 32, PriceUnit.PerKg,
                "Tender, no strings."),
            L(ListingType.Produce, ramasamy, "Drumstick, long", "Drumstick", 35, PriceUnit.PerKg,
                "PKM variety, bundled."),

            // Fruits
            L(ListingType.Produce, karthik, "Banganapalli mangoes", "Mango", 72, PriceUnit.PerKg,
                "Naturally ripened in hay."),
            L(ListingType.Produce, dhanraj, "Neelam mangoes", "Mango", 65, PriceUnit.PerKg,
                "Late season, sweet."),
            L(ListingType.Produce, ilango, "Banana, Nendran", "Banana", 48, PriceUnit.PerKg,
                "Large fruit, for chips."),
            L(ListingType.Produce, malathi, "Banana, Rasthali", "Banana", 55, PriceUnit.PerKg,
                "Sold by the hand."),
            L(ListingType.Produce, sumathi, "Guava, Lucknow 49", "Guava", 40, PriceUnit.PerKg,
                "Crisp, seedy centre."),
            L(ListingType.Produce, velu, "Papaya, red lady", "Papaya", 26, PriceUnit.PerKg,
                "Picked at colour break."),
            L(ListingType.Produce, mohan, "Sapota", "Sapota", 52, PriceUnit.PerKg,
                "Cricket ball variety."),
            L(ListingType.Produce, rajeshwari, "Pomegranate, Bhagwa", "Pomegranate", 110, PriceUnit.PerKg,
                "Deep red arils, export grade."),

            // Grains and pulses
            L(ListingType.Produce, perumal, "Ponni paddy", "Samba Paddy", 2520, PriceUnit.PerQuintal,
                "Fine grain, well dried."),
            L(ListingType.Produce, kalaiselvi, "Samba paddy, bulk", "Samba Paddy", 2380, PriceUnit.PerQuintal,
                "Direct from the threshing floor."),
            L(ListingType.Produce, bhuvana, "Groundnut, bold", "Groundnut", 6400, PriceUnit.PerQuintal,
                "Hand picked, uniform size."),
            L(ListingType.Produce, ganesan, "Groundnut, TMV 7", "Groundnut", 5950, PriceUnit.PerQuintal,
                "Good oil content."),
            L(ListingType.Produce, anitha, "Green gram", "Green Gram", 8200, PriceUnit.PerQuintal,
                "Cleaned and graded."),
            L(ListingType.Produce, sundaram, "Black gram, bulk", "Black Gram", 7400, PriceUnit.PerQuintal,
                "Machine cleaned."),
            L(ListingType.Produce, velu, "Ragi, finger millet", "Ragi", 3400, PriceUnit.PerQuintal,
                "Rain fed, stored dry."),
            L(ListingType.Produce, rajeshwari, "Maize, yellow", "Maize", 2080, PriceUnit.PerQuintal,
                "Feed grade, bulk only."),
            L(ListingType.Produce, saravanan, "Cotton, MCU 5", "Cotton", 7100, PriceUnit.PerQuintal,
                "Hand picked, clean lint."),

            // Dairy
            L(ListingType.Produce, meena, "Cow milk, morning", "Cow Milk", 44, PriceUnit.PerLitre,
                "Collected at the gate."),
            L(ListingType.Produce, kalaiselvi, "Cow milk, bulk supply", "Cow Milk", 41, PriceUnit.PerLitre,
                "Daily contract preferred."),
            L(ListingType.Produce, dhanraj, "Buffalo milk, high fat", "Buffalo Milk", 62, PriceUnit.PerLitre,
                "Seven percent fat, for sweets."),
            L(ListingType.Produce, malathi, "Goat milk", "Goat Milk", 95, PriceUnit.PerLitre,
                "Small quantity, booked in advance."),

            // Labour: one entry per person per task. Several of these people are
            // registered for more than one task at different wages, which is what the
            // shared calendar exists for - book Raja to weed and he is also
            // unavailable to harvest.
            L(ListingType.Labour, raja, "Weeding, manual", "Weeding", 420, PriceUnit.PerDay,
                "Available on short notice."),
            L(ListingType.Labour, raja, "Harvesting crew member", "Harvesting", 600, PriceUnit.PerDay,
                "Paddy and groundnut, own sickle."),
            L(ListingType.Labour, raja, "Sowing and transplanting", "Sowing and Planting", 500, PriceUnit.PerDay,
                "Fast transplanting, works in a line."),

            L(ListingType.Labour, devi, "Sowing and transplanting", "Sowing and Planting", 480, PriceUnit.PerDay,
                "Experienced in paddy and groundnut."),
            L(ListingType.Labour, devi, "Weeding, manual", "Weeding", 450, PriceUnit.PerDay,
                "Hand weeding, no chemicals."),

            L(ListingType.Labour, kumar, "Paddy harvesting crew", "Harvesting", 650, PriceUnit.PerDay,
                "Team of six, bring our own sickles."),
            L(ListingType.Labour, murugan, "Pesticide spraying", "Spraying", 700, PriceUnit.PerDay,
                "Licensed, own protective gear."),
            L(ListingType.Labour, selvi, "General farm help", "General Farm Help", 480, PriceUnit.PerDay,
                "Any field work, six days a week."),
            L(ListingType.Labour, arumugam, "Tractor operator", "General Farm Help", 800, PriceUnit.PerDay,
                "15 years driving, all implements."),

            // Most of these people do two or three things, each at its own wage. The
            // unique index allows one entry per person per task, never two.
            L(ListingType.Labour, ganesan, "Ploughing and land prep", "Ploughing", 850, PriceUnit.PerDay,
                "Tractor work, own implements."),
            L(ListingType.Labour, ganesan, "Tractor and implement operator", "Machine Operator", 900, PriceUnit.PerDay,
                "Rotavator, seed drill, trailer."),
            L(ListingType.Labour, ganesan, "Threshing", "Threshing and Winnowing", 700, PriceUnit.PerDay,
                "Run the thresher and bag the grain."),

            L(ListingType.Labour, meena, "Nursery and seedling work", "Nursery Work", 450, PriceUnit.PerDay,
                "Raising seedlings, grafting."),
            L(ListingType.Labour, meena, "Transplanting", "Sowing and Planting", 470, PriceUnit.PerDay,
                "Paddy and vegetable seedlings."),

            L(ListingType.Labour, saravanan, "Irrigation and channel work", "Irrigation", 520, PriceUnit.PerDay,
                "Setting lines, clearing channels."),
            L(ListingType.Labour, saravanan, "Fencing", "Fencing", 600, PriceUnit.PerDay,
                "Posts, barbed wire and gates."),

            L(ListingType.Labour, vasanthi, "Weeding and intercultivation", "Weeding", 440, PriceUnit.PerDay,
                "Careful around young plants."),
            L(ListingType.Labour, vasanthi, "Harvesting, vegetables", "Harvesting", 470, PriceUnit.PerDay,
                "Picking and grading."),

            L(ListingType.Labour, perumal, "Paddy harvesting", "Harvesting", 620, PriceUnit.PerDay,
                "Delta experience, works in water."),
            L(ListingType.Labour, perumal, "Threshing and winnowing", "Threshing and Winnowing", 650, PriceUnit.PerDay,
                "Traditional and machine."),

            L(ListingType.Labour, kalaiselvi, "Sowing", "Sowing and Planting", 460, PriceUnit.PerDay,
                "Broadcast and line sowing."),
            L(ListingType.Labour, kalaiselvi, "Loading and unloading", "Loading and Unloading", 550, PriceUnit.PerDay,
                "Bags and bundles, lorry loading."),

            L(ListingType.Labour, sundaram, "Sheep and goat herding", "Livestock Herding", 500, PriceUnit.PerDay,
                "Grazing and penning, own dog."),
            L(ListingType.Labour, sundaram, "Fencing and pen building", "Fencing", 580, PriceUnit.PerDay,
                "Thorn and wire pens."),

            L(ListingType.Labour, anitha, "Milking, morning and evening", "Dairy Milking", 420, PriceUnit.PerDay,
                "Hand and machine milking."),
            L(ListingType.Labour, anitha, "Cattle care", "Livestock Herding", 450, PriceUnit.PerDay,
                "Feeding, cleaning, shed work."),

            L(ListingType.Labour, velu, "Spraying", "Spraying", 660, PriceUnit.PerDay,
                "Knapsack and power sprayer."),
            L(ListingType.Labour, velu, "Pruning, fruit trees", "Pruning", 620, PriceUnit.PerDay,
                "Mango, guava and sapota."),

            L(ListingType.Labour, rajeshwari, "Weeding", "Weeding", 430, PriceUnit.PerDay,
                "Hand weeding, no chemicals."),
            L(ListingType.Labour, rajeshwari, "Nursery work", "Nursery Work", 460, PriceUnit.PerDay,
                "Potting, watering, shade work."),

            L(ListingType.Labour, ramasamy, "Irrigation", "Irrigation", 490, PriceUnit.PerDay,
                "Drip and flood, pump handling."),
            L(ListingType.Labour, ramasamy, "General farm help", "General Farm Help", 460, PriceUnit.PerDay,
                "Whatever the day needs."),

            L(ListingType.Labour, bhuvana, "Harvesting crew", "Harvesting", 580, PriceUnit.PerDay,
                "Group of four women, paddy and groundnut."),
            L(ListingType.Labour, bhuvana, "Loading and unloading", "Loading and Unloading", 520, PriceUnit.PerDay,
                "Market and godown work."),

            L(ListingType.Labour, karthik, "Machine operator", "Machine Operator", 950, PriceUnit.PerDay,
                "Harvester and thresher, licensed."),
            L(ListingType.Labour, karthik, "Ploughing", "Ploughing", 880, PriceUnit.PerDay,
                "Deep ploughing and puddling."),

            L(ListingType.Labour, pushpa, "Vegetable picking", "Harvesting", 450, PriceUnit.PerDay,
                "Tomato, brinjal and chilli."),
            L(ListingType.Labour, pushpa, "Weeding, vegetable beds", "Weeding", 420, PriceUnit.PerDay,
                "Careful hand weeding."),

            L(ListingType.Labour, ilango, "Pruning and orchard work", "Pruning", 640, PriceUnit.PerDay,
                "Banana and mango, 12 years."),
            L(ListingType.Labour, ilango, "Irrigation", "Irrigation", 510, PriceUnit.PerDay,
                "Basin and drip irrigation."),

            L(ListingType.Labour, malathi, "Dairy milking", "Dairy Milking", 440, PriceUnit.PerDay,
                "Goats and cows, twice daily."),
            L(ListingType.Labour, malathi, "Livestock herding", "Livestock Herding", 470, PriceUnit.PerDay,
                "Goats mainly."),

            L(ListingType.Labour, dhanraj, "Fencing", "Fencing", 610, PriceUnit.PerDay,
                "Chain link and solar fencing."),
            L(ListingType.Labour, dhanraj, "Loading and unloading", "Loading and Unloading", 540, PriceUnit.PerDay,
                "Fodder, manure and produce."),

            L(ListingType.Labour, sumathi, "Sowing and planting", "Sowing and Planting", 455, PriceUnit.PerDay,
                "Seed treatment and sowing."),
            L(ListingType.Labour, sumathi, "Nursery work", "Nursery Work", 440, PriceUnit.PerDay,
                "Seedling trays and transplanting."),

            L(ListingType.Labour, mohan, "General farm help", "General Farm Help", 470, PriceUnit.PerDay,
                "Available at short notice."),
            L(ListingType.Labour, mohan, "Threshing", "Threshing and Winnowing", 630, PriceUnit.PerDay,
                "Machine threshing, bagging."),

            L(ListingType.Labour, jeyanthi, "Spraying", "Spraying", 680, PriceUnit.PerDay,
                "Trained in safe handling."),
            L(ListingType.Labour, jeyanthi, "Ploughing", "Ploughing", 820, PriceUnit.PerDay,
                "Bullock and tractor ploughing.")
        };

        // Labour listings carry skills and experience; produce carries a quantity.
        foreach (var l in listings.Where(x => x.Type == ListingType.Labour))
        {
            l.Skills = l.Description;
            l.ExperienceYears = rng.Next(3, 22);
        }

        // Plausible amounts for the unit: nobody offers 400 cows or 3 kg of paddy.
        decimal QuantityFor(PriceUnit unit) => unit switch
        {
            PriceUnit.PerHead => rng.Next(2, 14),
            PriceUnit.PerQuintal => rng.Next(8, 110),
            PriceUnit.PerLitre => rng.Next(25, 260),
            _ => rng.Next(40, 700)
        };

        foreach (var l in listings.Where(x => x.Type == ListingType.Produce))
            l.Quantity = QuantityFor(l.PriceUnit);

        // A sold listing, so the status filter has something to hide. Found by title:
        // an index here silently moves every time a listing is added above.
        Find("Drumstick").Status = ListingStatus.Sold;

        // ------------------------------------------------------------------
        // Generated listings. The ones above are written out because the README
        // walkthrough names them; these bring every category up to a depth worth
        // browsing. They are kept in their own list so Find() above still sees
        // exactly the hand-written titles and cannot be tripped by a duplicate.
        // ------------------------------------------------------------------
        var extra = new List<Listing>();

        // What to charge comes from what people already ask for the same thing,
        // jittered - so generated prices sit in a believable band instead of being
        // invented. A category nobody has listed yet falls back on its unit.
        decimal BasePrice(Category c)
        {
            var asked = listings.Where(l => l.CategoryId == c.Id).Select(l => l.Price).ToList();
            if (asked.Count > 0) return asked.Average();
            return (c.SuggestedUnit ?? PriceUnit.PerDay) switch
            {
                PriceUnit.PerKg => 30, PriceUnit.PerQuintal => 5000, PriceUnit.PerHead => 9000,
                PriceUnit.PerLitre => 50, PriceUnit.PerHour => 120, _ => 600
            };
        }

        decimal Jitter(decimal to)
        {
            var value = to * (decimal)(0.85 + rng.NextDouble() * 0.30);
            // Nobody quotes a tractor at 1,847 rupees a day.
            return value >= 1000 ? Math.Round(value / 10) * 10 : Math.Round(value);
        }

        Listing Gen(ListingType type, ApplicationUser owner, string title, Category c, string desc) => new()
        {
            Type = type,
            OwnerId = owner.Id,
            Title = title,
            CategoryId = c.Id,
            Price = Jitter(BasePrice(c)),
            PriceUnit = c.SuggestedUnit ?? PriceUnit.PerDay,
            Description = desc,
            Village = owner.Village,
            Town = owner.Town,
            District = owner.District!,
            CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 90))
        };

        // One entry per person per category, which the filtered unique index enforces
        // for labour and plain sense asks for in the marketplace. Equipment is exempt:
        // owning two tractors is normal.
        var offers = new HashSet<(string Owner, int Category)>(
            listings.Where(l => l.Type != ListingType.Equipment).Select(l => (l.OwnerId, l.CategoryId)));

        // Picking members in a shuffled order per category is what spreads the work
        // around; taking them in roster order would give the first few people
        // everything and leave the rest with empty profiles.
        IEnumerable<ApplicationUser> Shuffled() => roster.OrderBy(_ => rng.Next());

        string[] labourNotes =
        [
            "Available at short notice.", "Works in a group of four.", "Own tools, own transport.",
            "Six days a week, half day on Sunday.", "Takes work within 20 km.",
            "Paid by the day, meals not included.", "Can start the same week.",
            "Known in the village, ask anyone.", "Will travel for a week or more at a time."
        ];

        // How deep each category goes. A fixed target gave every single one of them
        // the same count, which is the one thing a real marketplace never looks like:
        // some machines are everywhere and some are rare, and the same goes for
        // produce. Labour stays deeper throughout, because a task nobody has
        // registered for is a dead end rather than a thin page.
        var variants = ImageVariantCounts(env);

        int Target(Category category)
        {
            var wanted = category.ListingType == ListingType.Labour
                ? rng.Next(6, 11)    // 6 to 10 people
                : rng.Next(1, 7);    // 1 to 6 listings

            // Never ask for more listings than there are photographs to go round.
            // Each listing in a category gets a different one, so past this ceiling
            // the page starts repeating itself - which is exactly what it used to do.
            var ceiling = variants.GetValueOrDefault(ImageStem(category.Name));
            return ceiling > 0 ? Math.Min(wanted, ceiling) : wanted;
        }

        // Six to ten people per task. Most of these workers come up for two or three
        // tasks, which is exactly what the shared calendar exists to keep honest.
        foreach (var task in cats.Where(c => c.ListingType == ListingType.Labour))
        {
            var have = listings.Count(l => l.CategoryId == task.Id);
            var target = Target(task);
            foreach (var worker in Shuffled())
            {
                if (have >= target) break;
                if (!offers.Add((worker.Id, task.Id))) continue;

                var years = rng.Next(2, 26);
                var note = labourNotes[rng.Next(labourNotes.Length)];
                var l = Gen(ListingType.Labour, worker, $"{task.Name}, {years} seasons", task, note);
                l.ExperienceYears = years;
                l.Skills = $"{task.Name}. {note}";
                extra.Add(l);
                have++;
            }
        }

        string[] produceNotes =
        [
            "Farm fresh, picked to order.", "Graded and bagged.", "Bulk lots only.",
            "From our own land, no middleman.", "Ready now, collect from the farm.",
            "Small lots, regular supply.", "Cleaned and weighed at the gate.",
            "Delivered to the market if the quantity suits."
        ];

        // One to six sellers per product. Some products are in every second village
        // and some are a single farmer's sideline; a page showing one seller is a
        // true answer, not a gap.
        foreach (var product in cats.Where(c => c.ListingType == ListingType.Produce))
        {
            var have = listings.Count(l => l.CategoryId == product.Id);
            var target = Target(product);
            foreach (var seller in Shuffled())
            {
                if (have >= target) break;
                if (!offers.Add((seller.Id, product.Id))) continue;

                var l = Gen(ListingType.Produce, seller, $"{product.Name}, {seller.Town}", product,
                    produceNotes[rng.Next(produceNotes.Length)]);
                l.Quantity = QuantityFor(l.PriceUnit);
                extra.Add(l);
                have++;
            }
        }

        string[] machineNotes =
        [
            "Single owner, well maintained.", "Recently serviced, new parts.",
            "Available most weeks outside the season.", "Operator extra if needed.",
            "Diesel on the renter.", "Delivered free within 25 km.", "Papers and insurance current."
        ];

        // One to six machines per kind. Unlike the other two this allows the same
        // owner twice, because equipment reserves the machine, not the person.
        foreach (var kind in cats.Where(c => c.ListingType == ListingType.Equipment))
        {
            var have = listings.Count(l => l.CategoryId == kind.Id);
            var target = Target(kind);
            foreach (var owner in Shuffled())
            {
                if (have >= target) break;
                extra.Add(Gen(ListingType.Equipment, owner, $"{kind.Name}, {owner.Town}", kind,
                    machineNotes[rng.Next(machineNotes.Length)]));
                have++;
            }
        }

        db.Listings.AddRange(listings);
        db.Listings.AddRange(extra);
        await db.SaveChangesAsync();

        await AttachSeedImagesAsync(db, [.. listings, .. extra], env);

        // Look these up rather than by position: the list above gets reordered and
        // added to, and an index silently points at the wrong thing. Titles repeat
        // across workers now, so the owner is part of the key.
        Listing Find(string title, ApplicationUser? owner = null) =>
            listings.Single(l => l.Title == title && (owner == null || l.OwnerId == owner.Id));

        var harvester = Find("Paddy harvester");
        var crew = Find("Paddy harvesting crew");
        var rajaWeeding = Find("Weeding, manual", raja);
        var rajaHarvesting = Find("Harvesting crew member", raja);

        // --- bookings: every status, plus the conflict the demo walkthrough relies on ---
        var demoBookings = new List<Booking>
        {
            // Approved on the tractor for days 10-12, with a still-pending request
            // overlapping it. Approving the second must be refused.
            new Booking
            {
                ListingId = tractor.Id, RequesterId = selvi.Id,
                FromDate = today.AddDays(10), ToDate = today.AddDays(12),
                Status = BookingStatus.Approved, Message = "Two days of ploughing."
            },
            new Booking
            {
                ListingId = tractor.Id, RequesterId = kumar.Id,
                FromDate = today.AddDays(11), ToDate = today.AddDays(13),
                Status = BookingStatus.Pending, Message = "Needed for the north plot."
            },
            // Owner downtime is just an approved booking against yourself.
            new Booking
            {
                ListingId = tractor.Id, RequesterId = murugan.Id,
                FromDate = today.AddDays(20), ToDate = today.AddDays(23),
                Status = BookingStatus.Approved, Message = "Service and tyre change."
            },
            new Booking
            {
                ListingId = harvester.Id, RequesterId = arumugam.Id,
                FromDate = today.AddDays(4), ToDate = today.AddDays(6),
                Status = BookingStatus.Rejected, Message = "Harvest is running late."
            },
            new Booking
            {
                ListingId = harvester.Id, RequesterId = devi.Id,
                FromDate = today.AddDays(-14), ToDate = today.AddDays(-12),
                Status = BookingStatus.Completed, Message = "Done, paid in full."
            },
            // Raja is booked to WEED on days 6-8. Because labour reserves the person,
            // he is also unavailable to harvest or sow on those days - his other two
            // tasks show the same blocked dates and drop out of a dated search.
            new Booking
            {
                ListingId = rajaWeeding.Id, RequesterId = lakshmi.Id,
                FromDate = today.AddDays(6), ToDate = today.AddDays(8),
                Status = BookingStatus.Approved, Message = "Two acres of groundnut."
            },
            // A pending request for a DIFFERENT task of his, overlapping those dates.
            // Approving it must be refused; approving something else that clashes must
            // auto-reject it.
            new Booking
            {
                ListingId = rajaHarvesting.Id, RequesterId = murugan.Id,
                FromDate = today.AddDays(7), ToDate = today.AddDays(9),
                Status = BookingStatus.Pending, Message = "Harvest the east plot."
            },
            new Booking
            {
                ListingId = crew.Id, RequesterId = lakshmi.Id,
                FromDate = today.AddDays(2), ToDate = today.AddDays(5),
                Status = BookingStatus.Cancelled, Message = "Rain, postponed."
            },
            new Booking
            {
                ListingId = crew.Id, RequesterId = murugan.Id,
                FromDate = today.AddDays(7), ToDate = today.AddDays(9),
                Status = BookingStatus.Pending, Message = "Three acres of samba."
            }
        };
        db.Bookings.AddRange(demoBookings);

        // ------------------------------------------------------------------
        // Generated booking history. Nine bookings across the whole platform left
        // every calendar free and every "my bookings" page blank, which is the
        // clearest sign a site has never been used. These give it a past, a diary
        // and a queue of requests waiting on their owners.
        //
        // They obey the rule the application itself enforces rather than writing
        // rows straight into the table: equipment reserves the machine, labour
        // reserves the person. Book one of a worker's tasks and the rest of their
        // calendar closes - so the seeded data can never contradict what
        // BookingService would allow, and the overlap check is the same function.
        // ------------------------------------------------------------------
        var everyListing = listings.Concat(extra).ToDictionary(l => l.Id);

        var busy = new Dictionary<string, List<(DateOnly From, DateOnly To)>>();

        bool Free(Listing l, DateOnly from, DateOnly to) =>
            !busy.TryGetValue(l.BookingResourceKey, out var spans)
            || spans.All(s => !BookingService.Overlaps(from, to, s.From, s.To));

        void Occupy(Listing l, DateOnly from, DateOnly to)
        {
            if (!busy.TryGetValue(l.BookingResourceKey, out var spans))
                busy[l.BookingResourceKey] = spans = [];
            spans.Add((from, to));
        }

        // The hand-written bookings claim their dates first, so nothing generated can
        // quietly overwrite the conflict the walkthrough depends on.
        foreach (var b in demoBookings.Where(b => b.Status is BookingStatus.Approved or BookingStatus.Completed))
            Occupy(everyListing[b.ListingId], b.FromDate, b.ToDate);

        string[] requestNotes =
        [
            "Two acres, the plot behind the school.", "Three days should finish it.",
            "Will arrange food and transport.", "Same as last season, if you are free.",
            "Urgent, the rain is due.", "Half the field only this time.",
            "Can shift the dates a day either way.", "Paying in cash on the last day.",
            "North plot, near the canal.", "Bring the usual crew if you can."
        ];

        var bookable = everyListing.Values
            .Where(l => l.IsBookable && l.Status == ListingStatus.Active).ToList();
        var generatedBookings = new List<Booking>();

        for (var attempt = 0; attempt < 900; attempt++)
        {
            var listing = bookable[rng.Next(bookable.Count)];
            var requester = roster[rng.Next(roster.Count)];
            if (requester.Id == listing.OwnerId) continue;   // owner downtime is seeded by hand

            var from = today.AddDays(rng.Next(-80, 26));
            var to = from.AddDays(rng.Next(1, 5));

            // Work that is already over is settled one way or the other. Work still to
            // come is either agreed or sitting in the owner's queue.
            var status = to <= today
                ? rng.Next(8) == 0 ? BookingStatus.Cancelled : BookingStatus.Completed
                : rng.Next(10) switch
                {
                    < 5 => BookingStatus.Approved,
                    < 8 => BookingStatus.Pending,
                    8 => BookingStatus.Rejected,
                    _ => BookingStatus.Cancelled
                };

            // Only a booking that was actually agreed holds the calendar. A pending
            // request may well clash with one - that is the queue the owner has to
            // work through, and approving it is what the lock in ApproveAsync guards.
            if (status is BookingStatus.Approved or BookingStatus.Completed)
            {
                if (!Free(listing, from, to)) continue;
                Occupy(listing, from, to);
            }

            generatedBookings.Add(new Booking
            {
                ListingId = listing.Id,
                RequesterId = requester.Id,
                FromDate = from,
                ToDate = to,
                Status = status,
                Message = requestNotes[rng.Next(requestNotes.Length)],
                // Asked for before the job, not after it finished.
                CreatedAt = from.ToDateTime(TimeOnly.MinValue).AddDays(-rng.Next(2, 16))
            });
        }

        db.Bookings.AddRange(generatedBookings);

        // --- reference prices (SOW §2.3.7) ---
        MarketPrice P(string category, string district, decimal price, PriceUnit unit) => new()
        {
            CategoryId = C(category).Id, District = district, Price = price, Unit = unit,
            Source = "District Agricultural Marketing Committee",
            AsOfDate = today.AddDays(-rng.Next(1, 7))
        };

        var prices = new List<MarketPrice>
        {
            // Now per product rather than per broad category: a reference price for
            // tomatoes in Coimbatore means something, one for "vegetables" did not.
            P("Tomato", "Coimbatore", 21, PriceUnit.PerKg),
            P("Tomato", "Salem", 19, PriceUnit.PerKg),
            P("Onion", "Salem", 17, PriceUnit.PerKg),
            P("Mango", "Salem", 88, PriceUnit.PerKg),
            P("Banana", "Coimbatore", 30, PriceUnit.PerKg),
            P("Samba Paddy", "Thanjavur", 2380, PriceUnit.PerQuintal),
            P("Groundnut", "Tiruchirappalli", 6050, PriceUnit.PerQuintal),
            P("Maize", "Salem", 2080, PriceUnit.PerQuintal),
            P("Cow", "Madurai", 45000, PriceUnit.PerHead),
            P("Sheep", "Madurai", 8800, PriceUnit.PerHead),
            P("Sheep", "Salem", 9400, PriceUnit.PerHead),
            P("Goat", "Madurai", 9100, PriceUnit.PerHead),
            P("Goat", "Salem", 10800, PriceUnit.PerHead),
            P("Buffalo", "Dindigul", 70000, PriceUnit.PerHead),
            P("Poultry", "Madurai", 440, PriceUnit.PerHead),
            P("Cow Milk", "Erode", 44, PriceUnit.PerLitre),
            P("Cow Milk", "Coimbatore", 43, PriceUnit.PerLitre),
            P("Buffalo Milk", "Vellore", 60, PriceUnit.PerLitre),
            P("Brinjal", "Villupuram", 21, PriceUnit.PerKg),
            P("Okra", "Erode", 26, PriceUnit.PerKg),
            P("Green Chilli", "Vellore", 44, PriceUnit.PerKg),
            P("Carrot", "Coimbatore", 36, PriceUnit.PerKg),
            P("Guava", "Vellore", 38, PriceUnit.PerKg),
            P("Papaya", "Salem", 24, PriceUnit.PerKg),
            P("Pomegranate", "Salem", 105, PriceUnit.PerKg),
            P("Green Gram", "Madurai", 8050, PriceUnit.PerQuintal),
            P("Black Gram", "Madurai", 7600, PriceUnit.PerQuintal),
            P("Ragi", "Salem", 3320, PriceUnit.PerQuintal),
            P("Cotton", "Erode", 7250, PriceUnit.PerQuintal)
        };

        // Every product gets a reference figure in three districts. The hand-written
        // rows above cover the ones the walkthrough quotes; without the rest, most of
        // the market price screen was blank and "compare with the going rate" - the
        // whole point of that screen - only worked for nine products.
        foreach (var product in cats.Where(c => c.ListingType == ListingType.Produce))
            foreach (var district in Districts.All.OrderBy(_ => rng.Next()).Take(3))
            {
                if (prices.Any(p => p.CategoryId == product.Id && p.District == district)) continue;
                prices.Add(new MarketPrice
                {
                    CategoryId = product.Id,
                    District = district,
                    Price = Jitter(BasePrice(product)),
                    Unit = product.SuggestedUnit ?? PriceUnit.PerKg,
                    Source = "District Agricultural Marketing Committee",
                    AsOfDate = today.AddDays(-rng.Next(1, 7))
                });
            }

        db.MarketPrices.AddRange(prices);

        db.ListingReports.Add(new ListingReport
        {
            ListingId = Find("Country goats").Id, ReportedById = devi.Id,
            Reason = "Photographs look like they were taken from another website."
        });

        db.Notifications.AddRange(
            new Notification
            {
                UserId = murugan.Id, Message = "Kumar P requested your Mahindra 575 DI tractor.",
                LinkUrl = "/Bookings/Received"
            },
            new Notification
            {
                UserId = selvi.Id, Message = "Your tractor booking was approved.",
                LinkUrl = "/Bookings/Mine", IsRead = true
            });

        // Tell owners about the requests waiting on them, and requesters how theirs
        // went. Without this the bell is empty for everyone but Murugan, even though
        // there are now a few hundred bookings in the system.
        var nameOf = roster.ToDictionary(u => u.Id, u => u.FullName ?? "A member");

        foreach (var b in generatedBookings)
        {
            var listing = everyListing[b.ListingId];

            // Completed and cancelled jobs are old news. A notification for each would
            // bury the ones that still need the member to do something.
            if (b.Status == BookingStatus.Pending)
                db.Notifications.Add(new Notification
                {
                    UserId = listing.OwnerId,
                    Message = $"{nameOf[b.RequesterId]} requested your {listing.Title}.",
                    LinkUrl = "/Bookings/Received"
                });
            else if (b.Status is BookingStatus.Approved or BookingStatus.Rejected)
                db.Notifications.Add(new Notification
                {
                    UserId = b.RequesterId,
                    Message = $"Your booking of {listing.Title} was "
                              + (b.Status == BookingStatus.Approved ? "approved." : "declined."),
                    LinkUrl = "/Bookings/Mine",
                    IsRead = true
                });
        }

        await db.SaveChangesAsync();
    }
}

