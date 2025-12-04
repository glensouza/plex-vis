# Design System: Plex Velocity

This document defines the visual language and UI components for PlexVis.

## 1. Visual Identity

The visual language feels native to the Plex ecosystem (Dark Mode, Orange Accents) but distinct enough to serve as a dashboard.

### Core Palette

| Token | Color | Hex | Usage |
|-------|-------|-----|-------|
| Plex Orange | 🟠 | `#E5A00D` | Primary Brand Color, accents |
| Background | ⬛ | `#1E1E1E` | Main App Background |
| Surface | 🔲 | `#2D2D2D` | Card/Container Background |
| Elevated | 🔳 | `#3D3D3D` | Hover states, elevated surfaces |
| Text Primary | ⬜ | `#ECF0F1` | Main readable text |
| Text Secondary | 🔘 | `#95A5A6` | Metadata labels, dates |
| Border | ➖ | `#404040` | Subtle borders |

## 2. The "Velocity" Color Scale

This application relies on color to denote urgency. We use a traffic light system to grade the "Days to Watch" metric.

| Urgency | Velocity | Color | Hex | Logic |
|---------|----------|-------|-----|-------|
| High | ⚡ Fast | Green | `#2ECC71` | 0-2 days after release |
| Medium | 🐢 Steady | Yellow | `#F1C40F` | 3-7 days after release |
| Low | 🕸️ Stale | Red | `#E74C3C` | 8-30 days (Backlog building) |
| Dead | 💀 Archived | Grey | `#7F8C8D` | 30+ days (Deep backlog) |

## 3. Typography

- **Font Family**: Inter, Open Sans, or standard System Sans-Serif
- **Headings**: Bold weights (700)
- **Metrics**: Monospace (`Consolas`, `Roboto Mono`) for specific "Days" numbers to ensure alignment in lists

### CSS Variables

```css
:root {
    /* Core Palette */
    --plex-orange: #E5A00D;
    --bg-main: #1E1E1E;
    --bg-surface: #2D2D2D;
    --bg-elevated: #3D3D3D;
    --text-primary: #ECF0F1;
    --text-secondary: #95A5A6;
    
    /* Velocity Scale */
    --velocity-fast: #2ECC71;
    --velocity-steady: #F1C40F;
    --velocity-stale: #E74C3C;
    --velocity-archived: #7F8C8D;
}
```

## 4. UI Components

### Velocity Card

The primary component for displaying show velocity data.

```css
.velocity-card {
    background-color: #2D2D2D;
    border-radius: 8px;
    border-left: 4px solid var(--velocity-color);
    color: #ECF0F1;
    padding: 16px;
    display: flex;
    gap: 12px;
}

.velocity-badge {
    background-color: rgba(229, 160, 13, 0.2);
    color: #E5A00D;
    padding: 4px 8px;
    border-radius: 4px;
    font-weight: bold;
    font-size: 0.8rem;
}
```

### Velocity Badge Variants

```css
.badge-fast {
    background-color: rgba(46, 204, 113, 0.2);
    color: #2ECC71;
}

.badge-steady {
    background-color: rgba(241, 196, 15, 0.2);
    color: #F1C40F;
}

.badge-stale {
    background-color: rgba(231, 76, 60, 0.2);
    color: #E74C3C;
}

.badge-archived {
    background-color: rgba(127, 140, 141, 0.2);
    color: #7F8C8D;
}
```

## 5. Dashboard Layout

### Grid Layout

- Responsive cards for "On Deck" items
- Auto-fit grid that adapts to screen size
- Minimum card width of 280px

### Sidebar Navigation

- Fixed width of 250px on desktop
- Collapsible on mobile
- Active state uses orange highlight

### Stat Cards

- Grid of 4 cards for key metrics
- Hover effect with subtle elevation
- Monospace numbers for easy scanning

## 6. Responsive Breakpoints

| Breakpoint | Width | Behavior |
|------------|-------|----------|
| Mobile | < 641px | Single column, collapsed nav |
| Desktop | ≥ 641px | Sidebar + content area |

---

[← Back to Architecture](01-architecture.md) | [Next: Getting Started →](02-getting-started.md)
