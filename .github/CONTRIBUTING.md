# Contributing Guidelines

Contributions to this package are most welcome!

## Building and testing

```
dotnet build src/Umbraco.Community.SearchSpellCheck.sln
dotnet test src/Umbraco.Community.SearchSpellCheck.Tests/Umbraco.Community.SearchSpellCheck.Tests.csproj
```

The package should build with no compiler warnings. Please keep it that way.

## Running the test site

There is a test site in the solution to make working with this repository easier.

The site installs itself the first time you run it, so there is no database in the repository to
set up or log in to. Previously a SQLite database was committed for convenience, but it carried the
backoffice user table with it, so it has been removed.

```
dotnet run --project src/Umbraco.Community.SearchSpellCheck.TestSite
```

On first run Umbraco performs an unattended install using the credentials in
`appsettings.Development.json`:

| | |
|---|---|
| Email | `admin@example.com` |
| Password | `1234567890` |

These are local development credentials for a throwaway site. Do not reuse them anywhere real, and
do not point the test site at a database you care about.

The site ships with the [Clean starter kit](https://marketplace.umbraco.com/package/clean), so the
first unattended install also creates and publishes a set of pages the templates already understand,
and the `SpellCheckIndex` is built from them about a minute after start-up. To try the spell checker:

1. Log in to `/umbraco`.
2. Check `SearchSpellCheck:IndexedFields` in `appsettings.Development.json` lists the aliases of the
   properties you want words from. Textstring, textarea, Rich Text Editor, Block List and Block Grid
   properties are read.
3. Create and publish a page, or edit one. The `SpellCheckIndex` updates on publish. You can also
   rebuild it from **Settings → Examine Management**.
4. Search from the site's search page and misspell something. The "Did you mean" suggestion on the
   no-results page comes from this package.

Note that backoffice login needs HTTPS, so use the `https` URL from the launch profile.

## Raising changes

- Open an issue first for anything substantial, so we can agree the approach before you spend time
  on it.
- Add a test for behaviour you fix or add. There is an NUnit project in the solution, and the
  suggestion tests run against a real in-memory Lucene index rather than mocks, so end to end
  behaviour is testable.
- Keep the public `ISuggestionService` surface stable. If something needs to go, mark it
  `[Obsolete]` with a reason and a removal version rather than deleting it.
