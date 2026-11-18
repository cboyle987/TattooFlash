using Amazon.Runtime;
using Amazon.S3;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using TattooFlash.Web.Components;
using TattooFlash.Web.Configuration;
using TattooFlash.Web.Data;
using TattooFlash.Web.Endpoints;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Repositories;
using TattooFlash.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Debug = builder.Environment.IsDevelopment();
    options.EnableLogs = true;
    options.Environment = builder.Configuration["Sentry:Environment"];
});

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing DefaultConnection.");

builder.Services.ConfigureOptions<ForwardedHeadersConfiguration>();

builder.Services.AddOptions<R2Configuration>()
    .Bind(builder.Configuration.GetSection("R2"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<PaginationConfiguration>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();

//Add Auth0 authentication 
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"] ?? throw new InvalidOperationException("Missing Auth0Configuration");
    options.ClientId = builder.Configuration["Auth0:ClientId"] ?? throw new InvalidOperationException("Missing Auth0Configuration");
    options.ClientSecret = builder.Configuration["Auth0:ClientSecret"] ?? throw new InvalidOperationException("Missing Auth0Configuration");
});

builder.Services.AddCascadingAuthenticationState();

// Add MudBlazor services
builder.Services.AddMudServices(options =>
{
    options.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomLeft;
    options.SnackbarConfiguration.PreventDuplicates = true;
    options.SnackbarConfiguration.VisibleStateDuration = 2500;
});

// Register app services 
builder.Services.AddScoped<IDesignRepository, DesignRepository>();
builder.Services.AddScoped<IDesignService, DesignService>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageService, CloudflareImageService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var r2Config = sp.GetRequiredService<IOptions<R2Configuration>>().Value;
    var credentials = new BasicAWSCredentials(r2Config.AccessKeyId, r2Config.SecretAccessKey);
    return new AmazonS3Client(credentials, new AmazonS3Config
    {
        ServiceURL = r2Config.Endpoint
    });
});


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("TattooFlash");

// Use platform defaults locally; containers configure their persistent volume path.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/account/login", LoginEndpoint.HandleAsync);

app.MapGet("/account/logout", async (HttpContext context) =>
{
    var authenticationProperties = new LogoutAuthenticationPropertiesBuilder()
    .WithRedirectUri("/")
    .Build();

    await context.SignOutAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
});


app.Run();