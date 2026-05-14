# Backend Public XML Docs Design

## Goal

Improve backend documentation for the public UniEmu API surface with useful XML documentation comments.

## Scope

Document public API boundaries in `UniEmu` and `UniEmu.Scripting.Api`:

- API controllers and their public action methods.
- Public request, response, DTO, enum, and contract types.
- Public scripting API types and members visible to user scripts.
- Public service methods when they are used as application-layer entry points.

Do not document EF migrations, tests, private/internal helpers, or implementation-only runtime plumbing unless a public member is part of the supported boundary.

## Style

Use concise Russian XML docs for new comments, matching the existing scripting API tone. Keep existing English docs when they are already accurate, but normalize touched files to Russian when it improves consistency. Prefer meaningful `summary`, `param`, and `returns` text over mechanical comments.

## Verification

Run a backend build with `SkipYarnBuild=True` to ensure XML docs do not break compilation.
