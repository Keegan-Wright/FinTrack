using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();


builder.Services.AddDbContextFactory<FinanceTrackerContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("financeTrackerPostgresDb"), options =>
    {
        options.MigrationsAssembly("FinanceTracker.Data.Migrations");
        options.EnableRetryOnFailure();
        options.CommandTimeout(0);
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ClaimsPrincipal>(s => s.GetRequiredService<IHttpContextAccessor>().HttpContext.User);

builder.Services.AddIdentityCore<FinanceTrackerUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<FinanceTrackerContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<FinanceTrackerUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

using var scope = app.Services.CreateScope();
scope.ServiceProvider.GetRequiredService<IDbContextFactory<FinanceTrackerContext>>().CreateDbContext().Database.Migrate();

app.Run();