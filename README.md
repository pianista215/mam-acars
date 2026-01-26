# MAM ACARS

Flight recorder for the MAM (Modern Airlines Manager) ecosystem. Captures telemetry data from flight simulators via FSUIPC and submits flight reports to [MAM](https://github.com/pianista215/mam).

## Related Projects

- [MAM](https://github.com/pianista215/mam) - Main web application for airline management
- [MAM Analyzer](https://github.com/pianista215/mam-analyzer) - Analyzes black box data and generates flight reports

## What it does

- Connects to flight simulators via FSUIPC protocol
- Records telemetry data every 2 seconds (position, altitude, speed, fuel, autopilot state)
- Detects flight phases and events automatically
- Stores flight data locally in SQLite database
- Uploads flight reports to the MAM backend API
- Supports automatic updates via Velopack

## Requirements

- Windows 10/11
- .NET 6.0 SDK
- FSUIPC (registered or demo) connected to your flight simulator
- [Velopack CLI](https://docs.velopack.io/) (for packaging)

## Building

### Development build

```powershell
dotnet build
dotnet run
```

### Release build

```powershell
dotnet build --configuration Release
```

## Customization (Branding)

To customize the application for your virtual airline, modify the files in the `MamAcars/branding/` folder:

| File | Purpose |
|------|---------|
| `branding.json` | App name, company, API URL, and update URL |
| `branding.props` | Assembly name, product name, and company for Windows metadata |
| `icon.ico` | Application icon (Windows .ico format) |
| `logo.png` | Logo displayed in the application UI |

### branding.json fields

```json
{
  "AppName": "Mam Acars",
  "AppId": "MamAcars",
  "CompanyName": "Mam Airlines",
  "ApiBaseUrl": "http://localhost:8080/api/",
  "UpdateUrl": "http://localhost:8080/acars-updater/update/"
}
```

- **AppId**: Used as the package identifier (no spaces)
- **ApiBaseUrl**: Backend API endpoint for flight submissions
- **UpdateUrl**: Endpoint for Velopack auto-updates

## Packaging for Distribution

1. **Customize branding** (see above)

2. **Install Velopack CLI** (if not already installed):
   ```powershell
   dotnet tool install -g vpk
   ```

3. **Run the packaging script** from the solution root:
   ```powershell
   .\pack.ps1
   ```

4. **Distribute the output**: Copy the contents of `Releases/` to your MAM server's `acars-releases` folder to enable downloads and auto-updates.

### Output files

After running `pack.ps1`, the `Releases/` folder contains:

| File | Purpose |
|------|---------|
| `*-win-Setup.exe` | Installer for end users |
| `*-full.nupkg` | Update package for Velopack |
| `releases.win.json` | Version manifest for auto-updates |
| `RELEASES` | Legacy release manifest |

## Local Storage

All user data is stored in `%LOCALAPPDATA%\{AppId}\` (e.g., `%LOCALAPPDATA%\MamAcars\`):

| Path | Description |
|------|-------------|
| `sqlite.dat` | SQLite database (flights, events, telemetry) |
| `token.dat` | Encrypted authentication token (Windows DPAPI) |
| `flights/` | Flight data files (JSON/GZip) |
| `logs/app.log` | Rolling daily logs (5-day retention) |

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**.

This means:
- You can use, modify, and distribute this software
- Any derivative work must also be licensed under AGPL-3.0
- If you run a modified version as a network service, you must make the source code available to users
- See [LICENSE](LICENSE) for the full text

Copyright (c) 2026 Unai Sarasola Álvarez
