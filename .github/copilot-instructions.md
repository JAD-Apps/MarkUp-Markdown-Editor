# Copilot Instructions

## Project Guidelines
- User prefers the agent to continue working through the full coding task without pausing after intermediate issues or asking to stop early.
- Use a local `.env` file for remote UI test credentials and ensure `.env` is gitignored.

## Release Convention
When a new version is released, two files must be kept in sync:
1. **`CHANGELOG.md`** (this repo) — add the new `## [x.y.z] - YYYY-MM-DD` entry using the **actual git commit date** (not a projected or placeholder date).
2. **`jad-apps-site/app.js`** (https://github.com/John-Donnelly/jad-apps-site) — update or prepend an entry in the `changelogHighlights` array for the `markup` project using the same date, e.g.:
   ```js
   {
     version: "x.y.z",
     date: "YYYY-MM-DD",
     title: "Short release title",
     notes: [
       "Bullet one.",
       "Bullet two."
     ]
   }
   ```
   Keep only the two most recent released versions in `changelogHighlights` (plus an `"Unreleased"` entry if applicable).

Commit messages should follow Conventional Commits: `type(scope): description`  
Common types: `feat`, `fix`, `docs`, `test`, `chore`, `refactor`, `release`.
