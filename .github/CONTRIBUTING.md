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
dotnet run --project src/Umbraco.Community.SearchSpellCheck.TestSite.13.x
```

On first run Umbraco performs an unattended install using the credentials in
`appsettings.Development.json`:

| | |
|---|---|
| Email | `admin@example.com` |
| Password | `1234567890` |

These are local development credentials for a throwaway site. Do not reuse them anywhere real, and
do not point the test site at a database you care about.

The site starts empty. To exercise the spell checker you will need some content with text in it:

1. Log in to `/umbraco`.
2. Create a document type with a textstring, textarea, Block List or Block Grid property. Those are
   the four editors the package reads words from.
3. Add the property alias to `SearchSpellCheck:IndexedFields` in `appsettings.Development.json`.
4. Create and publish a page or two.
5. The `SpellCheckIndex` rebuilds on publish. You can also rebuild it from **Settings → Examine
   Management**.
6. Search from the site's search page and misspell something.

If you would rather start from a fuller site, the views in the test site were written against the
[Clean starter kit](https://marketplace.umbraco.com/package/clean), so installing that package gives
you content the existing templates already understand.

## Raising changes

- Open an issue first for anything substantial, so we can agree the approach before you spend time
  on it.
- Add a test for behaviour you fix or add. There is an NUnit project in the solution, and the
  suggestion tests run against a real in-memory Lucene index rather than mocks, so end to end
  behaviour is testable.
- Keep the public `ISuggestionService` surface stable. If something needs to go, mark it
  `[Obsolete]` with a reason and a removal version rather than deleting it.
