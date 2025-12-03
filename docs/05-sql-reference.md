# SQL Reference

This document provides SQL scripts for querying the Plex Media Server database (`com.plexapp.plugins.library.db`).

## The Schema Mental Model

To effectively query Plex, you need to understand the "Hierarchy of Three":

### Core Tables

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `metadata_items` | Content info (Movie, Episode) | `id`, `title`, `year`, `summary`, `rating`, `tags_genre`, `tags_collection` |
| `media_items` | File info (Resolution, Codec) | `id`, `metadata_item_id`, `width`, `height`, `bitrate`, `audio_channels` |
| `media_parts` | Physical file path | `id`, `media_item_id`, `file`, `size`, `duration` |

### Metadata Types

| Type | Value |
|------|-------|
| Movie | 1 |
| Show | 2 |
| Season | 3 |
| Episode | 4 |
| Artist | 8 |
| Album | 9 |
| Track | 10 |

### Relationship Chain

```
metadata_items (1) -> (Many) media_items (1) -> (Many) media_parts
```

---

## Basic Reporting Scripts

### List All Movies with File Paths

```sql
SELECT 
    m.title AS MovieTitle, 
    m.year AS Year, 
    p.file AS FilePath,
    ROUND(p.size / 1073741824.0, 2) AS SizeGB
FROM metadata_items m
JOIN media_items i ON m.id = i.metadata_item_id
JOIN media_parts p ON i.id = p.media_item_id
WHERE m.metadata_type = 1
ORDER BY m.title;
```

### List All TV Episodes with File Paths

```sql
SELECT 
    show.title AS ShowName,
    s.index AS SeasonNumber,
    e.index AS EpisodeNumber,
    e.title AS EpisodeTitle,
    p.file AS FilePath
FROM metadata_items e
JOIN metadata_items s ON e.parent_id = s.id
JOIN metadata_items show ON s.parent_id = show.id
JOIN media_items i ON e.id = i.metadata_item_id
JOIN media_parts p ON i.id = p.media_item_id
WHERE e.metadata_type = 4
ORDER BY show.title, s.index, e.index;
```

---

## Maintenance & Cleanup Scripts

### Find Duplicate Movies

Find entries where you have more than one video file for a single movie entry:

```sql
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

### Find Unmatched Items

Files that Plex sees but hasn't matched to an agent:

```sql
SELECT 
    id, 
    title, 
    added_at 
FROM metadata_items 
WHERE guid LIKE 'local://%' 
AND metadata_type IN (1, 2, 8);
```

### Find Missing Local Assets (Subtitles)

Find movies that do not have an external subtitle file:

```sql
SELECT m.title
FROM metadata_items m
WHERE m.metadata_type = 1
AND m.id NOT IN (
    SELECT DISTINCT i.metadata_item_id
    FROM media_items i
    JOIN media_parts p ON i.id = p.media_item_id
    JOIN media_streams s ON i.id = s.media_item_id
    WHERE s.stream_type_id = 3
);
```

---

## Tags, Genres, and Collections

Plex stores Genres, Collections, and Actors in a `tags` table, linked by a `taggings` table.

### List All Items in a Specific Collection

Replace `'Star Wars'` with your collection name:

```sql
SELECT m.title, m.year
FROM metadata_items m
JOIN taggings tg ON m.id = tg.metadata_item_id
JOIN tags t ON tg.tag_id = t.id
WHERE t.tag = 'Star Wars' 
AND t.tag_type = 2;
```

### Export Watch History

```sql
SELECT 
    m.title, 
    settings.view_count, 
    datetime(settings.last_viewed_at, 'unixepoch', 'localtime') as LastWatched
FROM metadata_items m
JOIN metadata_item_settings settings ON m.guid = settings.guid
WHERE settings.view_count > 0
ORDER BY settings.last_viewed_at DESC;
```

---

## The Viewing Velocity Query

This is the core query that powers PlexVis. It calculates average "lag time" between when an episode airs and when you watch it.

```sql
WITH 
-- 1. Calculate "Viewing Velocity" per show
ShowVelocity AS (
    SELECT 
        show.id AS ShowID,
        show.title AS ShowTitle,
        AVG(settings.last_viewed_at - strftime('%s', episode.originally_available_at)) AS AvgLagSeconds
    FROM metadata_items episode
    JOIN metadata_items season ON episode.parent_id = season.id
    JOIN metadata_items show ON season.parent_id = show.id
    JOIN metadata_item_settings settings ON episode.guid = settings.guid
    WHERE episode.metadata_type = 4
      AND settings.view_count > 0
      AND episode.originally_available_at IS NOT NULL
      AND settings.last_viewed_at IS NOT NULL
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
        MIN(season.index * 1000 + episode.index) as GlobalIndex
    FROM metadata_items episode
    JOIN metadata_items season ON episode.parent_id = season.id
    JOIN metadata_items show ON season.parent_id = show.id
    LEFT JOIN metadata_item_settings settings ON episode.guid = settings.guid
    WHERE episode.metadata_type = 4
      AND (settings.view_count IS NULL OR settings.view_count = 0)
      AND episode.originally_available_at IS NOT NULL
    GROUP BY show.id
)

-- 3. Combine to show "Next Up", prioritized by your "Urgency"
SELECT 
    v.ShowTitle,
    n.SeasonNum,
    n.EpisodeNum,
    n.EpisodeTitle,
    ROUND(v.AvgLagSeconds / 86400.0, 1) AS AvgDaysToWatch
FROM ShowVelocity v
JOIN NextEpisodes n ON v.ShowID = n.ShowID
ORDER BY AvgDaysToWatch ASC;
```

**How to interpret:**

- **Low Score:** You watch episodes almost immediately after they air (High Urgency)
- **High Score:** You wait months or years to watch (Backlog)

---

## Dangerous Operations

> ⚠️ **WARNING:** Stop your Plex server before running UPDATE or DELETE queries!

### Reset "Date Added"

Set `added_at` to a specific timestamp for a specific movie ID:

```sql
UPDATE metadata_items 
SET added_at = 1672531200
WHERE id = 12345;
```

### View Soft-Deleted Items

List items marked as deleted (useful if "Empty Trash" in the UI isn't working):

```sql
SELECT * FROM metadata_items WHERE deleted_at IS NOT NULL;
```

---

[← Previous: CI/CD Setup](04-cicd.md) | [Back to README](../.github/README.md)
