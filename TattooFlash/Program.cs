using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using TattooFlash.Components;
using TattooFlash.Configuration;
using TattooFlash.Data;
using TattooFlash.Repositories;
using TattooFlash.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing DefaultConnection.");

builder.Services.AddOptions<R2Configuration>()
    .Bind(builder.Configuration.GetSection("R2"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add MudBlazor services
builder.Services.AddMudServices();

// Register app services 
builder.Services.AddScoped<IDesignRepository, DesignRepository>();
builder.Services.AddScoped<IDesignService, DesignService>();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
