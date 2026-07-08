using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using web.Data;
using web.Models;
using web.Services.Email;
using web.Services.Sms;

var builder = WebApplication.CreateBuilder(args);

var dbDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_dbs");
Directory.CreateDirectory(dbDirectory);
var dbPath = Path.Combine(dbDirectory, "festival.db");
var logDbPath = Path.Combine(dbDirectory, "festival_logs.db");

var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_files", "DataProtection-Keys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("FestivalVagtstyring");

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.SQLite(logDbPath, rollOver: false));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services
    .AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IImapService, ImapService>();
builder.Services.AddHttpClient<ISmsService, SmsService>((sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["Sms:Url"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});
builder.Services.AddScoped<ISmsMessageLogService, SmsMessageLogService>();
builder.Services.AddScoped<IDashboardSmsFlowService, DashboardSmsFlowService>();
builder.Services.AddSingleton<ISmsGatewayStatusCache, SmsGatewayStatusCache>();

builder.Services.AddHostedService<web.Services.ScheduledMoveService>();
builder.Services.AddHostedService<SmsStatusUpdateService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeeder.SeedRolesAsync(roleManager);
}

// Configure the HTTP request pipeline.

// Appen kører i en Docker-container bag en reverse proxy der terminerer TLS,
// så Kestrel ser altid requests som http. Uden dette bliver Request.Scheme
// (og dermed genererede webhook-URLs mv.) forkert sat til http i stedet for https.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Reverse proxyens IP er ikke kendt på forhånd (Docker-netværk/host), så vi
// stoler på alle proxyer i stedet for kun standardværdien (loopback).
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "beskedcenter",
    pattern: "BeskedCenter/{action=Index}/{id?}",
    defaults: new { controller = "Beskeder" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "minprofile",
    pattern: "MinProfile/{action=Index}/{id?}",
    defaults: new { controller = "Profile" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "administration",
    pattern: "Administration/{action=Index}/{id?}",
    defaults: new { controller = "Admin" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
