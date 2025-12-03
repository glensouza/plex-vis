# Architecture

This document describes the architecture of Plex Velocity Visualizer.

## Overview

This application is designed to run in a Home Lab environment, specifically on a Linux LXC container that has direct filesystem access to your Plex Media Server's backup database.

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 (Blazor Server) |
| Orchestration | .NET Aspire (Local Development) |
| Data Access | Dapper (Read-Only SQLite) |
| Deployment | Self-Contained Linux Binary (Systemd Service) |
| Infrastructure | Ubuntu LXC Container (Proxied via Nginx) |

## System Architecture

We deploy this as a Self-Contained Linux Service:

- **Host:** Linux Ubuntu LXC
- **App:** Blazor Server (`PlexVis.Web`) running on Kestrel (`localhost:5000`)
- **Process Manager:** systemd (ensures app starts on boot and restarts on crash)
- **Data Access:** Direct file system access to Plex backups

## Project Structure

```
plex-vis/
├── .aspire/              # .NET Aspire configuration
├── PlexVis.ApiService/   # API service layer
├── PlexVis.AppHost/      # Aspire orchestration host
├── PlexVis.ServiceDefaults/ # Shared service configurations
├── PlexVis.Web/          # Blazor Server frontend
└── docs/                 # Documentation
```

## File Permission Strategy

Since we are not using Docker volumes, we rely on Linux users and groups.

The app will run as the same user as Plex does. This user already belongs to a group that has read access to the Plex directories.

## Data Flow

1. Plex Media Server creates database backups
2. PlexVis.Web reads from backup database (read-only)
3. Dapper queries SQLite for viewing statistics
4. Blazor Server renders visualization in browser

---

[← Back to README](../.github/README.md) | [Next: Getting Started →](02-getting-started.md)
