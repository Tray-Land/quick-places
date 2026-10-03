# Quick Places

A tray app for finding a nearby business fast: search, see whether it's open, its hours, website and phone number, and scan a QR code with your phone to place the call.

## What it does

- Click the tray icon, type what you want (pizza, pharmacy, plumber), press Enter.
- Results come from the Google Places API, biased to your current location, with distance.
- Pick a place to see open/closed now, the weekly hours (today in bold), phone number, website and a Google Maps link.
- The phone number is also a QR code (`tel:` link). Point your phone's camera at it and tap to call.
- Copy the number, open the website, or open the place in Google Maps from the detail view.

Search requests ask only for names and addresses. The contact and hours fields are requested once, for the place you open, which keeps Google billing low.

## Install

Requires Windows 10 2004+ or Windows 11, x64 or ARM64. Build from source (below) or download the MSIX from a GitHub release. If sideloading, install the signing certificate first.

## Setup

1. In Google Cloud, enable **Places API (New)** and create an API key (restrict it to that API).
2. Copy `OAuth.resw.sample` to `OAuth.resw` and paste the key into `GooglePlacesApiKey`. `OAuth.resw` is gitignored.
3. Rebuild. Without a key the flyout says what to add.

Windows location access must be on (Settings > Privacy & security > Location).

## Privacy and footprint

- Network calls happen only when you search or open a place, never while the flyout is hidden. A request in flight is cancelled when the flyout hides.
- Your query and approximate location are sent to Google Places. Nothing else leaves the machine.
- The API key is compiled into the package resources, not stored in settings. Restrict it in Google Cloud.
- The flyout closes for real after a minute hidden and releases its memory.

## Build from source

Prerequisites: .NET 10 SDK, `winget install Microsoft.WinAppCli`.

```powershell
winapp run                      # build and run with package identity
dotnet test QuickPlaces.Tests   # parser and formatting tests
.\build-msix-for-gh.ps1         # signed x64 + ARM64 MSIX and a draft GitHub release
```

## License

MIT, see [LICENSE](LICENSE).
