# Fetches freely-licensed photos from Wikimedia Commons for the seeded listings,
# centre-crops them to 4:3, scales to 800x600, and writes credits.json alongside.
#
# Several photos per subject, named <stem>-1.jpg, <stem>-2.jpg and so on, so six
# tractors do not all show the same machine. The seeder hands them out in rotation
# and simply uses however many it finds on disk - adding more here increases the
# variety without touching any C#.
#
# Attribution is a licence condition for CC BY / CC BY-SA, so credits.json is part of
# the output, not an optional extra. Credits are merged by file name, so an image and
# its credit line can never drift apart even when one subject is refetched.
#
#   pwsh tools/fetch-seed-images.ps1
#   pwsh tools/fetch-seed-images.ps1 -Only tractor,sheep
param(
    [string]$OutDir = "$PSScriptRoot\..\AgriConnect\wwwroot\seed-images",
    [string[]]$Only,
    # Commons is an archive as much as a photo library: searching "tractor" returns
    # 1950s machines and "milk" returns a 1910 delivery wagon, both correct matches
    # for the words and both useless on a listing that is meant to be on offer this
    # week. Nothing older than this is accepted, and an image with no date at all is
    # treated as undatable rather than assumed recent.
    [int]$MinYear = 2008
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$UA = "AgriConnect-training-project/1.0 (educational)"

# stem = @(count, "query", "fallback query", ...). Six each, which is the most
# listings the seeder puts in one equipment or marketplace category, so no two cards
# on a category page need ever show the same photograph. Labour goes deeper than six
# but its list shows people rather than pictures, and the photo only appears once a
# worker is opened.
#
# Six is past the point where Wikimedia's own relevance ordering is reliable, which
# is what MustMatch and Reject below are for: they discard the results that merely
# share a word with the query. Expect fewer than six for the narrow subjects - the
# seeder uses however many it finds.
$Subjects = [ordered]@{
    # --- equipment ---
    "tractor"        = @(6, "tractor ploughing field India", "farm tractor field work")
    "rotavator"      = @(6, "rotary tiller agriculture", "cultivator tractor implement")
    "harvester"      = @(6, "combine harvester rice field", "combine harvester crop")
    "sprayer"        = @(6, "knapsack sprayer", "agricultural sprayer machine", "pesticide sprayer farm", "boom sprayer tractor field")
    "water-pump"     = @(6, "irrigation pump agriculture field", "water pump irrigation")
    "plough"         = @(6, "plough agricultural implement", "ploughing field tractor")
    "seed-drill"     = @(6, "seed drill agriculture", "seed sowing machine farm")
    "thresher"       = @(6, "threshing machine grain", "thresher agriculture")
    "trailer"        = @(6, "tractor trailer farm", "farm trailer agriculture")
    "power-weeder"   = @(6, "power tiller agriculture", "two wheel tractor farming",
                            "walking tractor field", "hand tractor farming",
                            "rotary tiller walk behind", "power tiller paddy field",
                            "mini tiller cultivator garden")
    "chaff-cutter"   = @(6, "chaff cutter fodder", "fodder chopping machine", "forage harvester chopper",
                            "straw cutter machine farm", "silage chopper machine",
                            "fodder cutting machine dairy")

    # --- marketplace, livestock ---
    "sheep"          = @(6, "sheep flock grazing", "sheep farm")
    "goat"           = @(6, "goat herd grazing", "goats farm")
    "cow"            = @(6, "cow cattle India", "dairy cattle farm")
    "buffalo"        = @(6, "water buffalo farm", "buffalo cattle")
    "poultry"        = @(6, "chickens poultry farm", "free range chickens")
    "duck"           = @(6, "ducks farm pond", "domestic ducks")

    # --- marketplace, vegetables ---
    "tomato"         = @(6, "tomatoes harvest crate market", "tomatoes vegetable harvest")
    "onion"          = @(6, "onions harvest market", "onion bulbs sacks")
    "brinjal"        = @(6, "brinjal eggplant vegetable", "aubergine harvest")
    "okra"           = @(6, "okra lady finger vegetable", "okra harvest")
    "green-chilli"   = @(6, "green chilli peppers harvest", "chilli pepper market")
    "carrot"         = @(6, "carrots harvest bunch", "carrots market")
    "beans"          = @(6, "green beans harvest", "cluster beans vegetable")
    "drumstick"      = @(6, "moringa drumstick pods vegetable", "moringa oleifera pods")

    # --- marketplace, fruits ---
    "mango"          = @(6, "ripe mangoes fruit", "mango fruit tree")
    "banana"         = @(6, "banana bunch ripe fruit", "banana plantation harvest",
                            "bananas market stall", "banana fruit harvest basket",
                            "banana hands fruit bunch")
    "guava"          = @(6, "guava fruit", "guava tree fruit")
    "papaya"         = @(6, "papaya fruit tree", "papaya fruit")
    "sapota"         = @(6, "sapodilla fruit", "chikoo sapota fruit")
    "pomegranate"    = @(6, "pomegranate fruit", "pomegranate arils")

    # --- marketplace, grains and pulses ---
    "samba-paddy"    = @(6, "paddy field rice drying", "rice paddy grain", "paddy harvest India")
    "groundnut"      = @(6, "groundnut peanut harvest", "peanut crop harvest")
    "black-gram"     = @(6, "black gram urad beans", "pulses lentils grain heap")
    "green-gram"     = @(6, "green gram mung beans", "mung bean seeds")
    "maize"          = @(6, "maize corn cobs harvest", "corn kernels harvest")
    "ragi"           = @(6, "finger millet ragi", "millet grain")
    "cotton"         = @(6, "cotton boll field harvest", "raw cotton harvest")

    # --- marketplace, dairy ---
    "cow-milk"       = @(6, "milking cow by hand", "milk cans dairy farm")
    "buffalo-milk"   = @(6, "buffalo milking dairy", "milk churn dairy farm")
    "goat-milk"      = @(6, "milking goat", "goat dairy milking")

    # --- labour ---
    "harvesting"             = @(10, "harvesting rice India", "farm workers harvest")
    "sowing-and-planting"    = @(10, "rice transplanting India", "sowing seeds field")
    "weeding"                = @(10, "weeding agricultural field workers", "weeding crop field")
    "general-farm-help"      = @(10, "agriculture workers field India", "farm labour India", "agricultural labourers working", "farm workers field crop")
    "spraying"               = @(10, "farmer spraying pesticide crop field", "crop spraying agriculture")
    "ploughing"              = @(10, "farmer driving tractor field", "ploughing field bullock")
    "irrigation"             = @(10, "irrigation channel field farmer", "field irrigation water")
    "threshing-and-winnowing"= @(10, "threshing grain by hand", "winnowing grain farm", "threshing floor harvest")
    "fencing"                = @(10, "farm fence wire posts", "fencing field rural")
    "livestock-herding"      = @(10, "shepherd herding sheep", "herding goats field")
    "loading-and-unloading"  = @(10, "loading sacks lorry farm", "farm workers carrying sacks",
                                 "carrying baskets harvest workers", "workers loading truck sacks",
                                 "unloading goods lorry market", "porters carrying load market",
                                 "loading grain bags vehicle", "man carrying sack shoulder")
    "nursery-work"           = @(10, "plant nursery seedlings", "seedling trays nursery")
    "pruning"                = @(10, "pruning fruit tree", "orchard pruning", "pruning vineyard vines",
                                 "pruning shears branch", "tree trimming orchard worker",
                                 "pruning grape vine", "pruning plants secateurs")
    "dairy-milking"          = @(10, "milking cow dairy farm", "hand milking")
    "machine-operator"       = @(10, "tractor driver agriculture", "farm machinery operator")
}

# Wikimedia's search relevance is weak, so the file title is scored too. Without
# this, "banana bunch" returns a Banana Bunchy Top Virus plate and "milk" returns a
# 1910 milk wagon - both correctly matched, both useless on a listing card.
$MustMatch = @{
    "tractor" = @("tractor"); "rotavator" = @("rotavator","tiller","harrow","cultivator","rotary","hoe")
    "harvester" = @("harvester","harvesting"); "sprayer" = @("sprayer","spray")
    "water-pump" = @("pump","irrigation"); "plough" = @("plough","plow","ploughing")
    "seed-drill" = @("drill","seeder","sowing"); "thresher" = @("thresh")
    "trailer" = @("trailer","tractor"); "power-weeder" = @("tiller","weeder","cultivator")
    "chaff-cutter" = @("chaff","fodder","forage")

    "sheep" = @("sheep","lamb","ewe","ram"); "goat" = @("goat")
    "cow" = @("cow","cattle","bull","calf"); "buffalo" = @("buffalo")
    "poultry" = @("chicken","poultry","hen","rooster","fowl"); "duck" = @("duck")

    "tomato" = @("tomato"); "onion" = @("onion"); "brinjal" = @("brinjal","eggplant","aubergine","solanum melongena")
    "okra" = @("okra","lady","abelmoschus"); "green-chilli" = @("chil","pepper","capsicum")
    "carrot" = @("carrot"); "beans" = @("bean"); "drumstick" = @("moringa","drumstick")

    "mango" = @("mango"); "banana" = @("banana"); "guava" = @("guava","psidium")
    "papaya" = @("papaya","carica"); "sapota" = @("sapodilla","sapota","chikoo","manilkara")
    "pomegranate" = @("pomegranate","punica")

    "samba-paddy" = @("rice","paddy"); "groundnut" = @("groundnut","peanut","arachis")
    "black-gram" = @("gram","bean","lentil","pulse","urad","vigna")
    "green-gram" = @("gram","mung","moong","vigna","bean")
    "maize" = @("maize","corn"); "ragi" = @("millet","ragi","eleusine")
    "cotton" = @("cotton","gossypium")

    "cow-milk" = @("milking","milk can","milk churn","milkmaid")
    "buffalo-milk" = @("milk","buffalo","dairy"); "goat-milk" = @("milk","goat")

    "harvesting" = @("harvest"); "sowing-and-planting" = @("transplant","sowing","planting","paddy","seed")
    "weeding" = @("weed"); "general-farm-help" = @("farm","agricultur","field","labour","worker")
    "spraying" = @("spray"); "ploughing" = @("plough","plow","tractor")
    "irrigation" = @("irrigat","canal","channel","water")
    "threshing-and-winnowing" = @("thresh","winnow","grain")
    "fencing" = @("fence","fencing"); "livestock-herding" = @("herd","shepherd","graz","flock")
    "loading-and-unloading" = @("load","sack","carry","basket","porter","bag","haul","unload")
    "nursery-work" = @("nursery","seedling","sapling"); "pruning" = @("prun","orchard","trim","vine","secateur")
    "dairy-milking" = @("milk"); "machine-operator" = @("tractor","machine","operator","driver")
}

# Anything whose title hits one of these is not a neat photo of the thing itself.
$Reject = @(
    "virus", "disease", "diseased", "infected", "infection", "pest", "damage", "blight",
    "deficiency", "symptom", "lesion", "fungus", "mold", "rotten", "larva", "pupa",
    "diagram", "chart", "logo", "icon", "stamp", "coin", "banknote", "poster",
    "title page", "illustration", "drawing", "painting", "engraving",
    "cross section", "anatomy", "microscope", "specimen", "herbarium",
    "museum", "monument", "statue", "memorial", "cemetery",
    # A photograph of an exhibit about the thing, rather than of the thing. Searching
    # "milking" returned a museum case holding a wooden milking stool and a brass
    # plaque: correctly matched, correctly dated, and no use on a listing for milk.
    "stool", "exhibit", "exhibition", "display case", "plaque", "trophy", "award",
    "contest", "placard", "signage", "replica", "model of", "miniature",
    "slaughter", "carcass", "meat", "butcher", "skull",
    "19th century", "20th century", "historic", "historical", "heritage", "archive",
    "archival", "vintage", "antique", "postcard", "black and white", "monochrome",
    "colonial", "wartime", "retro", "restored", "preserved", "rally", "exhibition",
    "fried", "cooked", "boiled", "recipe", "dish", "curry", "packet", "packaging",
    "woolworths", "supermarket shelf", "restaurant", "canned",
    "helicopter", "aircraft", "aerial", "airplane", "aeroplane", "drone",
    "apparatus", "supplies", "catalogue", "catalog", "advertisement", "trade card"
)

function ConvertTo-PlainText($s) {
    if (-not $s) { return "" }
    return (($s -replace '<[^>]+>', '') -replace '\s+', ' ').Trim()
}

# When the photograph was taken, from EXIF where the camera recorded it and from the
# upload date otherwise. Returns $null when neither carries a year, which for Commons
# usually means a scan of something printed.
function Get-CaptureYear($meta) {
    if (-not $meta) { return $null }
    foreach ($field in @("DateTimeOriginal", "DateTime")) {
        $value = ConvertTo-PlainText $meta.$field.value
        if ($value -match '\b(18\d{2}|19\d{2}|20\d{2})\b') { return [int]$matches[1] }
    }
    return $null
}

# Candidates best-first. Portrait images crop badly into the 4:3 cards, SVGs and
# diagrams are not photographs, and a rejected title is the wrong subject however
# well it matched the words.
function Find-Images($query, $subject) {
    $enc = [uri]::EscapeDataString($query)
    $api = "https://commons.wikimedia.org/w/api.php?action=query&generator=search" +
           "&gsrsearch=$enc&gsrnamespace=6&gsrlimit=50&prop=imageinfo" +
           "&iiprop=url|extmetadata|size|mime&iiurlwidth=1200&format=json"

    $res = $null
    for ($attempt = 1; $attempt -le 3 -and -not $res; $attempt++) {
        try {
            $res = Invoke-RestMethod -Uri $api -Headers @{ "User-Agent" = $UA } -TimeoutSec 45
        } catch {
            Start-Sleep -Seconds (2 * $attempt)   # Commons rate-limits bursts
        }
    }
    if (-not $res -or -not $res.query) { return @() }

    $must = $MustMatch[$subject]
    $scored = @()

    foreach ($prop in $res.query.pages.PSObject.Properties) {
        $info = $prop.Value.imageinfo[0]
        if (-not $info) { continue }
        if ($info.mime -notin @("image/jpeg", "image/png")) { continue }
        if ($info.width -lt 700) { continue }
        if ($info.width -lt $info.height) { continue }

        $title = $prop.Value.title.ToLowerInvariant()

        # Match reject terms at a word boundary, not anywhere in the string. Plain
        # substring matching threw away every carrot photo, because "carrot" contains
        # "rot" - and would have done the same to "rotavator" and "rotary".
        # The boundary is only at the start, so "disease" still catches "diseased".
        if ($Reject | Where-Object { $title -match ("\b" + [regex]::Escape($_)) }) { continue }
        if ($must -and -not ($must | Where-Object { $title -like "*$_*" })) { continue }

        # A year in the title is a date, not a model number often enough to be worth
        # the few tractors it costs us. Commons titles its archive material this way.
        if ($title -match '\b(18|19)\d{2}\b') { continue }

        # The actual age of the photograph. Undated is rejected too: on Commons a
        # file with neither EXIF nor an upload date is nearly always a scan of
        # something printed, which is exactly what we are trying to avoid.
        $year = Get-CaptureYear $info.extmetadata
        if (-not $year -or $year -lt $MinYear) { continue }

        $score = 0

        # Prefer the recent one when two are otherwise equal, without letting a 2024
        # snapshot of the wrong thing outrank a 2012 photo of the right one.
        $score += [Math]::Min($year - $MinYear, 16) * 0.25
        foreach ($word in ($query -split '\s+')) {
            if ($word.Length -ge 4 -and $title -like "*$($word.ToLowerInvariant())*") { $score += 4 }
        }
        $ratio = $info.width / $info.height
        $score -= [Math]::Abs($ratio - (4.0 / 3.0)) * 6
        if ($info.width -ge 1200) { $score += 3 }
        if ($info.width -ge 2000) { $score += 1 }

        $scored += [pscustomobject]@{ Info = $info; Title = $prop.Value.title; Score = $score }
    }

    return @($scored | Sort-Object Score -Descending)
}

function Save-Cropped($sourceFile, $destFile) {
    $img = [System.Drawing.Image]::FromFile($sourceFile)
    try {
        $ratio = 4.0 / 3.0
        if (($img.Width / $img.Height) -gt $ratio) {
            $ch = $img.Height; $cw = [int]($ch * $ratio)
        } else {
            $cw = $img.Width;  $ch = [int]($cw / $ratio)
        }
        $cx = [int](($img.Width - $cw) / 2)
        $cy = [int](($img.Height - $ch) / 2)

        $out = New-Object System.Drawing.Bitmap(800, 600)
        $g = [System.Drawing.Graphics]::FromImage($out)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.DrawImage($img,
            (New-Object System.Drawing.Rectangle(0, 0, 800, 600)),
            (New-Object System.Drawing.Rectangle($cx, $cy, $cw, $ch)),
            [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()

        $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
                 Where-Object { $_.MimeType -eq "image/jpeg" }
        $ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
        $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
            [System.Drawing.Imaging.Encoder]::Quality, 82L)
        $out.Save($destFile, $codec, $ep)
        $out.Dispose()
    } finally {
        $img.Dispose()
    }
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$creditsPath = Join-Path $OutDir "credits.json"

$credits = [System.Collections.Generic.List[object]]::new()
if ((Test-Path $creditsPath) -and $Only) {
    foreach ($e in (Get-Content $creditsPath -Raw | ConvertFrom-Json | ForEach-Object { $_ })) {
        $credits.Add($e)
    }
}

$todo = if ($Only) { @($Subjects.Keys | Where-Object { $_ -in $Only }) } else { @($Subjects.Keys) }
if ($Only) {
    $unknown = @($Only | Where-Object { $_ -notin $Subjects.Keys })
    if ($unknown) { throw "Unknown subject(s): $($unknown -join ', ')" }
}

$grandTotal = 0

foreach ($name in $todo) {
    $spec = $Subjects[$name]
    $wanted = [int]$spec[0]
    $queries = $spec[1..($spec.Count - 1)]

    # Clear this subject's old variants so a smaller count does not leave orphans
    # behind that the seeder would still pick up.
    Get-ChildItem $OutDir -Filter "$name-*.jpg" -ErrorAction SilentlyContinue | Remove-Item -Force
    foreach ($e in @($credits | Where-Object { $_.File -like "$name-*.jpg" })) { [void]$credits.Remove($e) }

    $saved = 0
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $tmp = Join-Path $env:TEMP "agriconnect-seed-$name.bin"

    foreach ($q in $queries) {
        if ($saved -ge $wanted) { break }

        foreach ($candidate in (Find-Images $q $name)) {
            if ($saved -ge $wanted) { break }
            if (-not $seen.Add($candidate.Title)) { continue }   # same file from another query

            $dest = Join-Path $OutDir "$name-$($saved + 1).jpg"
            $ok = $false

            # Full-resolution url first: the generated thumbnail 404s for some files.
            foreach ($src in @($candidate.Info.url, $candidate.Info.thumburl)) {
                if (-not $src -or $ok) { continue }
                try {
                    Invoke-WebRequest -Uri $src -Headers @{ "User-Agent" = $UA } `
                        -OutFile $tmp -TimeoutSec 90 -UseBasicParsing
                    Save-Cropped $tmp $dest
                    $ok = $true
                } catch { }
            }
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
            if (-not $ok) { continue }

            $meta = $candidate.Info.extmetadata
            $credits.Add([pscustomobject]@{
                File        = "$name-$($saved + 1).jpg"
                Query       = $q
                CommonsPage = "https://commons.wikimedia.org/wiki/" + ($candidate.Title -replace ' ', '_')
                Author      = ConvertTo-PlainText $meta.Artist.value
                License     = ConvertTo-PlainText $meta.LicenseShortName.value
                # Recorded so "none of these are old photographs" is a claim anyone
                # can check against the file, rather than one you have to take on
                # trust after the fetch has finished.
                Year        = Get-CaptureYear $meta
            })
            $saved++
        }
    }

    $grandTotal += $saved
    $colour = if ($saved -eq $wanted) { "Green" } elseif ($saved -gt 0) { "Yellow" } else { "Red" }
    Write-Host ("{0,-24} {1}/{2} image(s)" -f $name, $saved, $wanted) -ForegroundColor $colour
    Start-Sleep -Milliseconds 400
}

$credits | Sort-Object File | ConvertTo-Json -Depth 3 | Out-File $creditsPath -Encoding utf8

Write-Host ""
Write-Host "$grandTotal images written, $($credits.Count) credited, in $OutDir"


