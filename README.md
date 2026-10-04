# Quick Places

A tray app for finding a nearby business fast: search, see whether it's open, its hours, website and phone number, and scan a QR code with your phone to place the call.

## What it does

- Click the tray icon, type what you want (pizza, pharmacy, plumber) and press Enter, or tap a quick category (coffee, food, groceries, gas, pharmacy, ATM, parks, hotels). Your last few searches appear under the categories.
- Results come from the Google Places API, biased to your current location. Each row shows a colored symbol for the kind of place (food, cafe, shop, health, fuel, lodging and about 20 more), its type, its address, its distance, and whether it is open or closed right now.
- Switch the results between a list and a map with the toggle above them; the choice is remembered. The map is an Azure Maps basemap (Road on a light taskbar, Night on a dark one) with a pin per place, marked like its row, and a dot for you. Hover a pin to see its name, click it to open the place. The list stays the keyboard and screen reader view.
- Pick a place and its symbol glides into the detail view (a connected animation). There you see open or closed now with today's hours, the star rating and review count, the price level, and the weekly hours with today highlighted.
- Action buttons open directions in Google Maps, open the website, copy the phone number or copy the address.
- The phone number is also a QR code (`tel:` link). Point your phone's camera at it and tap to call.
- Escape goes back from a place to the results, then closes the flyout.
- Motion (staggered entrances, springy hover and press, loading skeletons) follows Windows' "Animation effects" setting and turns off with it.

Search requests ask for names, addresses, place types and open-now only, which bills as Text Search Enterprise (open-now is what moves it up from Pro). The contact, full hours, rating and price fields are requested once, for the place you open.

## Install

Requires Windows 10 2004+ or Windows 11, x64 or ARM64. Build from source (below) or download the MSIX from a GitHub release. If sideloading, install the signing certificate first.

## Setup

1. In Google Cloud, enable **Places API (New)** and create an API key (restrict it to that API).
2. Copy `OAuth.resw.sample` to `OAuth.resw` and paste the key into `GooglePlacesApiKey`. `OAuth.resw` is gitignored.
3. Optional, for the map view: create an [Azure Maps](https://learn.microsoft.com/azure/azure-maps/) account and paste its primary key into `AzureMapsKey` in the same file. Without it the map falls back to OpenStreetMap tiles.
4. Rebuild. Without a Places key the flyout says what to add.

Windows location access must be on (Settings > Privacy & security > Location).

## Privacy and footprint

- Network calls happen only when you search or open a place, never while the flyout is hidden. A request in flight is cancelled when the flyout hides.
- Your query and approximate location are sent to Google Places. In map view, map tiles for the area shown are downloaded from Azure Maps (or OpenStreetMap's tile servers when the build has no Azure Maps key), which see the requested area. Nothing else leaves the machine.
- Your last five searches are kept in the app's local settings so they can be offered again. Clear them with "Clear" on the start screen.
- The API key is compiled into the package resources, not stored in settings. Restrict it in Google Cloud.
- The flyout closes for real after a minute hidden and releases its memory.

## Build from source

Prerequisites: .NET 10 SDK, `winget install Microsoft.WinAppCli`.

```powershell
winapp run                      # build and run with package identity
dotnet test QuickPlaces.Tests   # parser, formatting and map-bounds tests
.\build-msix-for-gh.ps1         # signed x64 + ARM64 MSIX and a draft GitHub release
```

## License

MIT, see [LICENSE](LICENSE).

Basemap: Azure Maps (© TomTom and others, shown on the map), or © [OpenStreetMap contributors](https://www.openstreetmap.org/copyright) without a key. The map control is [WinUIEx.Maps](https://github.com/dotMorten/WinUIEx.Maps), which is free for noncommercial use; commercial use requires sponsoring its author (see its license).
