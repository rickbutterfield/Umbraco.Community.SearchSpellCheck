# SearchSpellCheck

[![Platform](https://img.shields.io/badge/Umbraco-13-%233544B1?style=flat&logo=umbraco)](https://umbraco.com/products/umbraco-cms/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.SearchSpellCheck?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.SearchSpellCheck)
[![GitHub](https://img.shields.io/github/license/rickbutterfield/Umbraco.Community.SearchSpellCheck?color=8AB803)](../LICENSE)

## A Lucene.Net-based spell checker for Umbraco

This project wouldn't exist without [Lars-Erik Aabech](https://github.com/lars-erik) who [created a v7 version of this](https://blog.aabech.no/archive/building-a-spell-checker-for-search-in-umbraco/), which a lot of the work is based on.

## How it works
![alt text](https://raw.githubusercontent.com/rickbutterfield/Umbraco.Community.SearchSpellCheck/refs/heads/develop/docs/screenshot.png "A search result, with a misspelt version of the word 'house'. It is being suggested to the user to instead search for the correct spelling of the word.")

On startup, this extension will index all the content in your site based on the `IndexedFields` settings. On every search, the extension will check the multi-word search term against the index and suggest the most likely words to the user.

## Installation
The Umbraco v13 version of this package is [available via NuGet](https://www.nuget.org/packages/Umbraco.Community.SearchSpellCheck).

To install the package, you can use either .NET CLI:

```
dotnet add package Umbraco.Community.SearchSpellCheck --version 1.0.0-alpha
```

or the NuGet Package Manager:

```
Install-Package Umbraco.Community.SearchSpellCheck -Version 1.0.0-alpha
```

## Configuration
The package can be configured in `appsettings.json`
```
{
    "SearchSpellCheck": {
        "IndexName": "SpellCheckIndex",
        "IndexedFields": [ "nodeName" ],
        "BuildOnStartup": true,
        "RebuildOnPublish": true,
        "EnableLogging": false
    }
}
```

### Settings
`IndexName`: The name of the Lucene index to be created. This is the also name of the folder in the `App_Data` folder that contains the Lucene index. By default it is `SpellCheckIndex` but this can be changed if you need a different naming convention.

`IndexedFields`: The alias(es) of fields to be indexed. This is a comma-separated list of field names. By default only the `nodeName` field is indexed. Currently, there is support for textstring, textareas, TinyMCE, [Grid Layout](https://our.umbraco.com/Documentation/Fundamentals/Backoffice/property-editors/built-in-property-editors/Grid-Layout/) and [Block List Editor](https://our.umbraco.com/Documentation/Fundamentals/Backoffice/property-editors/built-in-property-editors/Block-List-Editor/) fields.

`BuildOnStartup`: Boolean indicating if you want the index to be populated on startup. Defaults to `true`.

`RebuildOnPublish`: Boolean indicating if you want the index to be populated on content being saved and published successfully. Defaults to `true`.

`EnableLogging`: Useful if you want to see what properties are being indexed and the content that is returned from the index. Defaults to `false`.

## Usage
The package enables an `ISuggestionService` to be injected into your constructor:
```cs
private readonly IExamineManager _examineManager;
private readonly ISuggestionService _suggestionService;

public SearchService(
    IExamineManager examineManager,
    ISuggestionService suggestionService)
{
    _examineManager = examineManager;
    _suggestionService = suggestionService;
}

public string GetSuggestions(string searchTerm)
{
    return _suggestionService.GetSuggestion(searchTerm, suggestionAccuracy: 0.25f);
}
```

Which could in turn be returned in a view component or model:
```cs
if (model.TotalResults == 0)
{
    model.SpellCheck = _searchService.GetSuggestions(model.SearchTerm);
}
```

And then returned in the view:
```cs
@if (!string.IsNullOrEmpty(Model.SpellCheck))
{
    <p>Did you mean <a href="?s=@Model.SpellCheck"><em>@Model.SpellCheck</em></a>?</p>
}
```

## License
Copyright &copy; 2021-2025 [Rick Butterfield](https://rickbutterfield.com), and other contributors

Licensed under the [MIT License](LICENSE.md).
