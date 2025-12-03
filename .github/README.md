# Plex Velocity Visualizer

Plex Velocity Visualizer is a .NET Blazor Server application that visualizes your Plex viewing habits with a specific focus on "Viewing Velocity" — a metric that tracks how quickly you watch shows after they air.

Unlike the standard Plex "On Deck," which prioritizes what you watched last, this dashboard prioritizes shows based on your urgency (average lag time between Air Date and Watch Date).

## 🏗 Architecture

This application is designed to run in a Home Lab environment, specifically on a Linux LXC container that has direct filesystem access to your Plex Media Server's backup database.

Framework: .NET 10 (Blazor Server)

Orchestration: .NET Aspire (Local Development)

Data Access: Dapper (Read-Only SQLite)

Deployment: Self-Contained Linux Binary (Systemd Service)

Infrastructure: Ubuntu LXC Container (Proxied via Nginx)

## 🚀 Features

Velocity "On Deck": Calculates your average "days to watch" for every show.

Smart Sorting: Prioritizes the next episode of shows you typically watch within 24-48 hours of release.

Direct Asset Linking: Proxies Plex images using your local Plex Token.

Read-Only Safety: Connects to Plex database backups to prevent database locking or corruption.

## 🛠️ Getting Started (Local Development)

Prerequisites.

- NET 10.0 SDK or later
- Access to a com.plexapp.plugins.library.db file (copy one from your server for testing).
- Configuration (appsettings.Development.json)

You need to point the app to your local copy of the database and your live Plex server for images.

```json
{
  "Plex": {
    "DatabasePath": "C:\\Path\\To\\Your\\Copy\\com.plexapp.plugins.library.db",
    "ServerUrl": "[http://192.168.1.50:32400](http://192.168.1.50:32400)",
    "Token": "YOUR_PLEX_TOKEN"
  }
}
```

Running the App

```bash
dotnet run --project PlexVis.AppHost
```

This will spin up the Aspire dashboard and the Blazor frontend.

## 📦 Deployment (Production on LXC)

This project uses a "Bare Metal" deployment strategy to save resources.

It runs as a Systemd service behind Nginx on your Linux LXC container.

## Server Preparation

### 1. SSH into your LXC container and prepare the directories and permissions.

Create app directory

```bash
sudo mkdir -p /var/www/plexvis
sudo chown -R $USER:$USER /var/www/plexvis
```

### 2. Add your user to the 'plex' group (so the app can read the DB)

```bash
sudo usermod -aG plex $USER
```

### 3. Configure Systemd

Create the service file: `sudo nano /etc/systemd/system/plexvis.service`

```bash
[Unit]
Description=Plex Visualizer .NET App
After=network.target

[Service]
User=your_username
Group=your_username
WorkingDirectory=/var/www/plexvis
ExecStart=/var/www/plexvis/PlexVis.Web
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=[http://127.0.0.1:5000](http://127.0.0.1:5000)
# IMPORTANT: Point this to your ACTUAL Plex backup location
Environment=Plex__DatabasePath="/var/lib/plexmediaserver/.../Databases/com.plexapp.plugins.library.db"

[Install]
WantedBy=multi-user.target
Enable the service:sudo systemctl enable plexvis.service
sudo systemctl start plexvis.service
```

### 4. CI/CD Setup (GitHub Actions)

This repository includes a workflow that automatically builds and deploys the app via SSH.Required GitHub Secrets:

Go to Settings > Secrets and variables > Actions and add:

SecretDescriptionHOSTThe IP address of your LXC container.USERNAMEThe SSH username (e.g., ubuntu).KEYYour private SSH key (PEM format).PLEX_TOKENYour Plex API Token (for fetching images).

## 4. (Optional) Nginx Reverse ProxyIf you want to access the app on port 80 instead of 5000.

sudo nano /etc/nginx/sites-available/plexvisserver {
    listen 80;
    server_name plexvis.local;

    location / {
        proxy_pass [http://127.0.0.1:5000](http://127.0.0.1:5000);
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

## 🔍 The SQL Logic

The core logic relies on calculating the delta between last_viewed_at and originally_available_at.

AVG(settings.last_viewed_at - strftime('%s', episode.originally_available_at))

Low Score: You watch episodes almost immediately after they air (High Urgency).

High Score: You wait months or years to watch (Backlog).

## Other Visualizations

### Plex Media Server SQLite Scripts

This guide provides SQL scripts for querying the Plex Media Server database (com.plexapp.plugins.library.db).

#### 1. The Schema Mental Model

To effectively query Plex, you need to understand the "Hierarchy of Three"

1. `metadata_items`: This is the "Content" (The Movie "Avatar", the Episode "Ozymandias").

  - Key Columns: `id`, `title`, `year`, `summary`, `rating, tags_genre`, `tags_collection`.
  - Note: `metadata_type` defines what it is (1=Movie, 2=Show, 3=Season, 4=Episode, 8=Artist, 9=Album, 10=Track).

1. `media_items`: This is the "File Information" (Resolution, Codec, Bitrate).      
  - Key Columns: `id`, `metadata_item_id`, `width`, `height`, `bitrate`, `audio_channels`.

1. `media_parts`: This is the "Physical File" (The actual path on your disk).

  - Key Columns: `id`, `media_item_id`, `file`, `size`, `duration`.

##### The Relationship Chain

`metadata_items` (1) -> (Many) `media_items` (1) -> (Many) `media_parts`

#### 2. Basic Reporting Scripts

##### List All Movies with File Paths

This is the most common "Hello World" query for Plex.

It joins the three main tables to show you exactly where your movies are stored.

```SQL
SELECT 
    m.title AS MovieTitle, 
    m.year AS Year, 
    p.file AS FilePath,
    ROUND(p.size / 1073741824.0, 2) AS SizeGB
FROM metadata_items m
JOIN media_items i ON m.id = i.metadata_item_id
JOIN media_parts p ON i.id = p.media_item_id
WHERE m.metadata_type = 1 -- 1 = Movies
ORDER BY m.title;
List All TV Episodes with File PathsSlightly more complex because we often want the Show Name and Season Number, which requires joining metadata_items on itself (Episodes -> Seasons -> Shows).SELECT 
    show.title AS ShowName,
    s.index AS SeasonNumber,
    e.index AS EpisodeNumber,
    e.title AS EpisodeTitle,
    p.file AS FilePath
FROM metadata_items e
JOIN metadata_items s ON e.parent_id = s.id       -- Join Episode to Season
JOIN metadata_items show ON s.parent_id = show.id -- Join Season to Show
JOIN media_items i ON e.id = i.metadata_item_id
JOIN media_parts p ON i.id = p.media_item_id
WHERE e.metadata_type = 4 -- 4 = Episodes
ORDER BY show.title, s.index, e.index;
```

#### 3. Maintenance & Cleanup Scripts

##### Find Duplicate Movies

This finds entries where you have more than one video file for a single movie entry (e.g., you have a 1080p version and a 4K version).

```SQL
SELECT 
    m.title, 
    COUNT(p.id) as FileCount
FROM metadata_items m
JOIN media_items i ON m.id = i.metadata_item_id
JOIN media_parts p ON i.id = p.media_item_id
WHERE m.metadata_type = 1
GROUP BY m.id
HAVING COUNT(p.id) > 1
ORDER BY FileCount DESC;
```

##### Find "Unmatched" Items

Files that Plex sees but hasn't matched to an agent (files that usually look like "Filename.mkv" instead of "Proper Title").

```SQL
SELECT 
    id, 
    title, 
    added_at 
FROM metadata_items 
WHERE guid LIKE 'local://%' 
AND metadata_type IN (1, 2, 8); -- Movies, Shows, Artists
Find Missing Local Assets (Subtitles)Find movies that do not have an external subtitle file associated with them.SELECT m.title
FROM metadata_items m
WHERE m.metadata_type = 1
AND m.id NOT IN (
    SELECT DISTINCT i.metadata_item_id
    FROM media_items i
    JOIN media_parts p ON i.id = p.media_item_id
    JOIN media_streams s ON i.id = s.media_item_id
    WHERE s.stream_type_id = 3 -- 3 = Subtitle
);
```

#### 4. Advanced: Tags, Genres, and Collections

Plex stores Genres, Collections, and Actors in a tags table, linked by a taggings table.

##### List All Items in a Specific Collection

Replace 'Star Wars' with your collection name.

```SQL
SELECT m.title, m.year
FROM metadata_items m
JOIN taggings tg ON m.id = tg.metadata_item_id
JOIN tags t ON tg.tag_id = t.id
WHERE t.tag = 'Star Wars' 
AND t.tag_type = 2; -- 2 = Collection
Export Watch History (View Counts)Note: This pulls from the account settings table. view_count > 0 implies watched.SELECT 
    m.title, 
    settings.view_count, 
    datetime(settings.last_viewed_at, 'unixepoch', 'localtime') as LastWatched
FROM metadata_items m
JOIN metadata_item_settings settings ON m.guid = settings.guid
WHERE settings.view_count > 0
ORDER BY settings.last_viewed_at DESC;
```

#### 5. Dangerous Operations (Update/Delete)

WARNING: Stop your Plex server before running these.

##### Hard Reset "Date Added"

If your "Recently Added" hub is messed up and you want to reset the "Added At" date to the file's creation date (requires getting creation date from OS, which SQL can't do alone, but here is how you update it if you have a unix timestamp).

  - Example: Set added_at to a specific timestamp for a specific movie ID

```SQL 
UPDATE metadata_items 
SET added_at = 1672531200 -- Jan 1, 2023
WHERE id = 12345;
```

##### Remove "Trash" (Soft Deleted items)

If "Empty Trash" in the UI isn't working, this lists items marked as deleted.

```SQL
SELECT * FROM metadata_items WHERE deleted_at IS NOT NULL;
```

#### 6. Custom Scripts

##### Priority "On Deck" (Based on Viewing Speed)

This script generates a custom "Continue Watching" list.

It calculates your average "Lag Time" (How many days after an episode airs do you typically watch it?) for every show, and then prioritizes the next unwatched episode of shows with the lowest lag time (highest urgency).

```SQL
WITH 
-- 1. Calculate "Viewing Velocity" per show: Avg seconds between Air Date and Watch Date
ShowVelocity AS (
    SELECT 
        show.id AS ShowID,
        show.title AS ShowTitle,
        AVG(settings.last_viewed_at - strftime('%s', episode.originally_available_at)) AS AvgLagSeconds
    FROM metadata_items episode
    JOIN metadata_items season ON episode.parent_id = season.id
    JOIN metadata_items show ON season.parent_id = show.id
    JOIN metadata_item_settings settings ON episode.guid = settings.guid
    WHERE episode.metadata_type = 4          -- Episodes
      AND settings.view_count > 0            -- Watched
      AND episode.originally_available_at IS NOT NULL
      AND settings.last_viewed_at IS NOT NULL
      -- Ensure we don't calculate negative lag (watching before air date) as it skews data
      AND settings.last_viewed_at >= strftime('%s', episode.originally_available_at)
    GROUP BY show.id
),

-- 2. Find the "Next Up" episode for every show
NextEpisodes AS (
    SELECT 
        show.id AS ShowID,
        show.title AS ShowTitle,
        season.index AS SeasonNum,
        episode.index AS EpisodeNum,
        episode.title AS EpisodeTitle,
        MIN(season.index * 1000 + episode.index) as GlobalIndex -- Trick to find earliest next ep
    FROM metadata_items episode
    JOIN metadata_items season ON episode.parent_id = season.id
    JOIN metadata_items show ON season.parent_id = show.id
    LEFT JOIN metadata_item_settings settings ON episode.guid = settings.guid
    WHERE episode.metadata_type = 4
      AND (settings.view_count IS NULL OR settings.view_count = 0) -- Unwatched
      AND episode.originally_available_at IS NOT NULL
    GROUP BY show.id
)

-- 3. Combine to show "Next Up", prioritized by your "Urgency"
SELECT 
    v.ShowTitle,
    n.SeasonNum,
    n.EpisodeNum,
    n.EpisodeTitle,
    ROUND(v.AvgLagSeconds / 86400.0, 1) AS AvgDaysToWatch -- Lower is more urgent
FROM ShowVelocity v
JOIN NextEpisodes n ON v.ShowID = n.ShowID
ORDER BY AvgDaysToWatch ASC; -- Shows you watch fastest appear first
```

## .NET Aspire & Blazor Server Architecture

This document outlines the implementation plan for a .NET Aspire application hosted directly on a Linux LXC server.

This app will read a backup Plex SQLite database and visualize viewing habits.

### 1. System Architecture

We will deploy this as a Self-Contained Linux Service.

- Host: Linux Ubuntu LXC.
- App: Blazor Server (PlexVis.Web) running on Kestrel (localhost:5000).
- Process Manager: systemd (ensures app starts on boot and restarts on crash).
- Data Access: Direct file system access to Plex backups.

#### File Permission Strategy (Critical)

Since we are not using Docker volumes, we rely on Linux users and groups.

The app will run as same user as Plex does.

This user already belongs to a group that has read access to the Plex directories.

### 2. Project Structure & Code

A. The AppHost (Local Dev Only)

The AppHost is still useful for your local development machine to orchestrate the environment, but it will not be used in production.

B. The Blazor App (Data Access)

In PlexVis.Web, we will use Dapper for high-performance SQLite querying.

`PlexService.cs` The paths now point to the actual locations on the Linux server, or are configurable via appsettings.json.

```CSharp
using Dapper;
using Microsoft.Data.Sqlite;

public class PlexService
{
    private readonly IConfiguration _config;

    public PlexService(IConfiguration config)
    {
        _config = config;
    }

    private string GetConnectionString() 
    {
        // In Prod: /var/lib/plexmediaserver/.../com.plexapp.plugins.library.db
        var dbPath = _config["Plex:DatabasePath"];
        return $"Data Source={dbPath};Mode=ReadOnly;";
    }

    public async Task<IEnumerable<ShowVelocity>> GetShowVelocitiesAsync()
    {
        var sql = @"
        WITH ShowVelocity AS (
            SELECT 
                show.id AS ShowID,
                show.title AS ShowTitle,
                show.user_thumb_url AS ThumbUrl,
                show.hash AS FolderHash,
                AVG(settings.last_viewed_at - strftime('%s', episode.originally_available_at)) AS AvgLagSeconds
            FROM metadata_items episode
            JOIN metadata_items season ON episode.parent_id = season.id
            JOIN metadata_items show ON season.parent_id = show.id
            JOIN metadata_item_settings settings ON episode.guid = settings.guid
            WHERE episode.metadata_type = 4 
              AND settings.view_count > 0 
              AND episode.originally_available_at IS NOT NULL
            GROUP BY show.id
        )
        SELECT * FROM ShowVelocity ORDER BY AvgLagSeconds ASC";

        using var conn = new SqliteConnection(GetConnectionString());
        return await conn.QueryAsync<ShowVelocity>(sql);
    }
}
```

### 3. Server Setup (One-Time)

Run these commands on your LXC server to prepare it.

1. Create Directory:sudo mkdir -p /var/www/plexvis

```bash
sudo chown -R $USER:$USER /var/www/plexvis
```

2. Create Systemd Service:

```bash
sudo nano /etc/systemd/system/plexvis.service
```

```bash
[Unit]
Description=Plex Visualizer .NET App
After=network.target

[Service]
# Replace 'ubuntu' with your actual username
User=ubuntu
Group=ubuntu
WorkingDirectory=/var/www/plexvis
ExecStart=/var/www/plexvis/PlexVis.Web
Restart=always
# Set environment variables here
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=[http://127.0.0.1:5000](http://127.0.0.1:5000)
# Point to your Plex DB
Environment=Plex__DatabasePath="/var/lib/plexmediaserver/Library/Application Support/Plex Media Server/Plug-in Support/Databases/com.plexapp.plugins.library.db"

[Install]
WantedBy=multi-user.target
Enable Service:sudo systemctl enable plexvis.service
Install & Configure Nginx:sudo nano /etc/nginx/sites-available/plexvisserver {
    listen 80;
    server_name plexvis.local; # Or your IP

    location / {
        proxy_pass [http://127.0.0.1:5000](http://127.0.0.1:5000);
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

3. Enable it:

```bash
sudo systemctl enable plexvis.service
```

4. CI/CD Pipeline (GitHub Actions)

This workflow publishes the files and copies them to the server via rsync.

Prerequisites (Secrets)
- `HOST`, `USERNAME`, `KEY` (Same as before)
- No Docker secrets needed.

##### The Workflow File

Create .github/workflows/deploy.yml:

```yaml
name: Deploy PlexVis Direct

on:
  push:
    branches: [ "main" ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      # Publish as a standalone executable (includes .NET runtime)
      - name: Publish Application
        run: |
          dotnet publish PlexVis.Web/PlexVis.Web.csproj \
          -c Release \
          -r linux-x64 \
          --self-contained true \
          -o ./publish

      # Copy files to server
      - name: Deploy to Server
        uses: appleboy/scp-action@master
        with:
          host: ${{ secrets.HOST }}
          username: ${{ secrets.USERNAME }}
          key: ${{ secrets.KEY }}
          source: "./publish/*"
          target: "/var/www/plexvis"
          strip_components: 1 # Removes the './publish' folder nesting
          overwrite: true

      # Restart the Service
      - name: Restart Systemd Service
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.HOST }}
          username: ${{ secrets.USERNAME }}
          key: ${{ secrets.KEY }}
          script: |
            # Give execute permission just in case
            chmod +x /var/www/plexvis/PlexVis.Web
            sudo systemctl restart plexvis.service
```

## 📄 License

[MIT](./LICENSE)
