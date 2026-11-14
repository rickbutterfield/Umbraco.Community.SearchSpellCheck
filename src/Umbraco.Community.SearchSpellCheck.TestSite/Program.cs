WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Umbraco.Cms.Infrastructure.Services.IndexingRebuilderService has two constructors that are
// ambiguous to the DI container (neither's parameter list is a superset of the other's) from
// 17.3.x through at least 17.6.0. The Development environment turns on eager service-provider
// validation by default, which throws on this at startup. Production mode resolves it lazily and
// never hits it, so this only needs disabling here, not in the package itself.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = false;
    options.ValidateScopes = false;
});

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
