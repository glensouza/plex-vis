# Deployment

This guide covers deploying Plex Velocity Visualizer to a production Linux LXC container.

## Overview

This project uses a "Bare Metal" deployment strategy to save resources. The app runs as a Systemd service behind Nginx on your Linux LXC container.

## Server Preparation

### Step 1: Create App Directory

SSH into your LXC container and prepare the directories:

```bash
sudo mkdir -p /var/www/plexvis
sudo chown -R $USER:$USER /var/www/plexvis
```

### Step 2: Add User to Plex Group

Add your user to the `plex` group so the app can read the database:

```bash
sudo usermod -aG plex $USER
```

> **Note:** Log out and back in for group changes to take effect.

### Step 3: Configure Systemd

Create the service file:

```bash
sudo nano /etc/systemd/system/plexvis.service
```

Add the following content (replace `your_username` with your actual username):

```ini
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
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
# IMPORTANT: Point this to your ACTUAL Plex backup location
Environment=Plex__DatabasePath="/var/lib/plexmediaserver/Library/Application Support/Plex Media Server/Plug-in Support/Databases/com.plexapp.plugins.library.db"

[Install]
WantedBy=multi-user.target
```

### Step 4: Enable the Service

```bash
sudo systemctl enable plexvis.service
sudo systemctl start plexvis.service
```

### Step 5: Verify Service Status

```bash
sudo systemctl status plexvis.service
```

You should see `Active: active (running)`.

## Nginx Reverse Proxy (Optional)

If you want to access the app on port 80 instead of 5000:

### Create Nginx Configuration

```bash
sudo nano /etc/nginx/sites-available/plexvis
```

Add the following:

```nginx
server {
    listen 80;
    server_name plexvis.local;

    location / {
        proxy_pass http://127.0.0.1:5000;
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

### Enable the Site

```bash
sudo ln -s /etc/nginx/sites-available/plexvis /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## Troubleshooting

### Service fails to start

Check the logs:

```bash
sudo journalctl -u plexvis.service -f
```

### Permission denied on database

Verify user group membership:

```bash
groups $USER
```

Ensure `plex` is in the list.

### Nginx 502 Bad Gateway

Verify the app is running:

```bash
curl http://127.0.0.1:5000
```

---

[← Previous: Getting Started](02-getting-started.md) | [Back to README](../.github/README.md) | [Next: CI/CD Setup →](04-cicd.md)
