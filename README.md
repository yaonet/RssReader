# RssReader

## Features

- **RSS Feed Management**: Add, view, and delete RSS feeds
- **Category Organization**: Organize feeds into categories
- **Article Reading**: Read articles from your subscribed feeds
- **OPML Import**: Batch import feeds from OPML files

## OPML Import

The RSS Reader now supports importing feeds from OPML files, which is a standard format used by many RSS readers for exporting feed subscriptions.

### How to Import Feeds

1. Navigate to the "Feeds" page
2. Click on the "Import OPML" tab
3. Select a default category (optional - feeds without a category in the OPML file will use this)
4. Choose your OPML file (`.opml` or `.xml` extension)
5. Click "Import Feeds"

### OPML File Format

The importer supports standard OPML format with:
- **Categories**: Nested outline elements create categories automatically
- **Feed URLs**: Extracted from `xmlUrl` attribute
- **Feed Titles**: Taken from `title` or `text` attribute
- **Website URLs**: Extracted from `htmlUrl` attribute (optional)

### Import Process

- **Automatic Category Creation**: Categories found in the OPML file are created automatically if they don't exist
- **Duplicate Detection**: Feeds that already exist (same URL) are skipped
- **Progress Tracking**: Real-time progress bar shows import status
- **Recent Articles**: The 10 most recent articles are imported for each feed

## Getting Started

1. Create at least one category in the "Categories" page
2. Add feeds manually or import from OPML
3. Browse and read articles from your subscribed feeds