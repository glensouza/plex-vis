# Plex Velocity Visualizer

Plex Velocity Visualizer is a .NET Blazor Server application that visualizes your Plex viewing habits with a specific focus on "Viewing Velocity" — a metric that tracks how quickly you watch shows after they air.

Unlike the standard Plex "On Deck," which prioritizes what you watched last, this dashboard prioritizes shows based on your urgency (average lag time between Air Date and Watch Date).

## Features

- **Velocity "On Deck":** Calculates your average "days to watch" for every show
- **Smart Sorting:** Prioritizes the next episode of shows you typically watch within 24-48 hours of release
- **Direct Asset Linking:** Proxies Plex images using your local Plex Token
- **Read-Only Safety:** Connects to Plex database backups to prevent database locking or corruption

## Quick Start

```bash
# Clone the repository
git clone https://github.com/glensouza/plex-vis.git

# Run with .NET Aspire
dotnet run --project PlexVis.AppHost
```

See [Getting Started](../docs/02-getting-started.md) for detailed setup instructions.

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](../docs/01-architecture.md) | System architecture and tech stack |
| [Getting Started](../docs/02-getting-started.md) | Local development setup |
| [Deployment](../docs/03-deployment.md) | Production deployment to Linux LXC |
| [CI/CD Setup](../docs/04-cicd.md) | GitHub Actions automated deployments |
| [SQL Reference](../docs/05-sql-reference.md) | Plex database queries and scripts |

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 (Blazor Server) |
| Orchestration | .NET Aspire (Local Development) |
| Data Access | Dapper (Read-Only SQLite) |
| Deployment | Self-Contained Linux Binary (Systemd Service) |
| Infrastructure | Ubuntu LXC Container (Proxied via Nginx) |

## Contributing

Please read [CONTRIBUTING.md](../CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Security

For security concerns, please see [SECURITY.md](SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.
