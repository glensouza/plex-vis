# CI/CD Setup

This guide covers setting up automated deployments using GitHub Actions.

## Overview

The repository includes a workflow that automatically builds and deploys the app via SSH when you push to the `main` branch.

## Prerequisites

### GitHub Secrets

Go to **Settings** > **Secrets and variables** > **Actions** and add:

| Secret | Description |
|--------|-------------|
| `HOST` | The IP address of your LXC container |
| `USERNAME` | The SSH username (e.g., `ubuntu`) |
| `KEY` | Your private SSH key (PEM format) |
| `PLEX_TOKEN` | Your Plex API Token (for fetching images) |

## GitHub Actions Workflow

Create `.github/workflows/deploy.yml`:

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
          dotnet-version: 10.0.x

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
          strip_components: 1
          overwrite: true

      # Restart the Service
      - name: Restart Systemd Service
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.HOST }}
          username: ${{ secrets.USERNAME }}
          key: ${{ secrets.KEY }}
          script: |
            chmod +x /var/www/plexvis/PlexVis.Web
            sudo systemctl restart plexvis.service
```

## How It Works

1. **Trigger:** Workflow runs on every push to `main` branch
2. **Build:** .NET publishes a self-contained Linux executable
3. **Deploy:** Files are copied to the server via SCP
4. **Restart:** The systemd service is restarted to load new code

## Verification

After pushing to `main`:

1. Go to **Actions** tab in GitHub
2. Watch the workflow run
3. Verify deployment succeeded (green checkmark)
4. Test your app at your server URL

## Troubleshooting

### SSH connection failed

**Error:** `ssh: connect to host ... port 22: Connection refused`

**Solution:** 
- Verify your server's IP address is correct in the `HOST` secret
- Ensure SSH is enabled on your server
- Check firewall rules allow port 22

### Permission denied

**Error:** `Permission denied (publickey)`

**Solution:**
- Verify the `KEY` secret contains the correct private key
- Ensure the corresponding public key is in `~/.ssh/authorized_keys` on the server

### Service restart failed

**Error:** `Failed to restart plexvis.service`

**Solution:**
- Verify the username has sudo permissions for systemctl
- Add to `/etc/sudoers` if needed:
  ```
  your_username ALL=(ALL) NOPASSWD: /bin/systemctl restart plexvis.service
  ```

---

[← Previous: Deployment](03-deployment.md) | [Back to README](../.github/README.md) | [Next: SQL Reference →](05-sql-reference.md)
