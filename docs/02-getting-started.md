# Getting Started

This guide helps you set up Plex Velocity Visualizer for local development.

## Prerequisites

- .NET 10.0 SDK or later
- Access to a `com.plexapp.plugins.library.db` file (copy one from your server for testing)

## Configuration

Create or update `appsettings.Development.json` in the `PlexVis.Web` project:

```json
{
  "Plex": {
    "DatabasePath": "C:\\Path\\To\\Your\\Copy\\com.plexapp.plugins.library.db",
    "ServerUrl": "http://192.168.1.50:32400",
    "Token": "<YOUR_PLEX_TOKEN>"
  }
}
```

**Configuration Values:**

| Setting | Description |
|---------|-------------|
| `DatabasePath` | Full path to your local copy of the Plex database |
| `ServerUrl` | URL of your Plex server (for fetching images) |
| `Token` | Your Plex API token (see [How to find your Plex token](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/)) |

## Running the App

Start the application using .NET Aspire:

```bash
dotnet run --project PlexVis.AppHost
```

This will spin up:

- The Aspire dashboard (for monitoring)
- The Blazor frontend

## Verification

1. Open your browser to the URL shown in the terminal (typically `https://localhost:5001`)
2. Verify the home page loads with your Plex viewing statistics

## Troubleshooting

### Database not found

**Error:** `Unable to open database file`

**Solution:** Verify the `DatabasePath` in your configuration points to a valid `.db` file.

### Connection refused to Plex server

**Error:** Images not loading or connection errors

**Solution:** 
1. Verify your `ServerUrl` is correct
2. Ensure your Plex token is valid
3. Check that your Plex server is running

### Port already in use

**Error:** `Address already in use`

**Solution:** Either stop the process using the port, or change the port:

```bash
dotnet run --project PlexVis.AppHost --urls "https://localhost:5002"
```

---

[← Previous: Architecture](01-architecture.md) | [Back to README](../.github/README.md) | [Next: Deployment →](03-deployment.md)
