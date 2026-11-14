# Umbraco.Community.SearchSpellCheck

A Lucene.Net spell checker for Umbraco search. One long-lived line per Umbraco major, version-aligned: the
package's major version is the Umbraco major it targets (`17.x.x` for Umbraco 17).

## Lines

Branch-per-major, `vN/dev` for work and `vN/main` for releases. A fix that applies to more than one line is ported
deliberately with `change-backport`; say in the commit body when a change only applies to one major. A new line is cut
from the previous major's `dev`.

| Line | Integration branch | Release branch | Phase |
|---|---|---|---|
| Umbraco 13 | `v13/dev` | `v13/main` | Security fixes only, until Umbraco 13 reaches end of life in mid December 2026 |
| Umbraco 17 | `v17/dev` | `v17/main` | Active. Supports Umbraco 17.8.1 or later |
| Umbraco 18 | `v18/dev` | `v18/main` | Active. Supports Umbraco 18.3.1 or later |
| Umbraco 19 | `v19/dev` | `v19/main` | Active, not started. Umbraco 19 is at RC. Run `major-upgrade 19` after 18 |

`develop` and `main` are the old v13-era branches. They are frozen and are being retired. Do not target them.

The default branch is `v17/dev`. Upgrade plans live in `docs/upgrades/v<major>.md`.

## Docs

The package's user docs are `docs/README_nuget.md` (shown on NuGet) and `.github/README.md` (shown on GitHub). Keep
both in step. Contributor steps are in `.github/CONTRIBUTING.md`.

## Build and test

```
dotnet build src/Umbraco.Community.SearchSpellCheck.sln
dotnet test src/Umbraco.Community.SearchSpellCheck.Tests
```

The build must have no warnings. Run the demo site with `dotnet run --project src/Umbraco.Community.SearchSpellCheck.TestSite`
(https; backoffice login needs it). The v13 line's test site needs the .NET 8 SDK.
