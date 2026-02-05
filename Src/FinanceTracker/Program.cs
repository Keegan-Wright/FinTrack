using System.Security.Claims;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Models.Request.Auth;
using FinanceTracker.Validators.Models;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

namespace FinanceTracker;

public partial class Program
{
    public static async Task Main(string[] args)
    {
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

        builder.Services.AddIdentityCore<FinanceTrackerUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<FinanceTrackerContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<FinanceTrackerUser>, IdentityNoOpEmailSender>();

        AddFinanceTrackerServices(builder.Services);
        AddFinanceTrackerValidators(builder.Services);
        

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
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FinanceTrackerContext>>().CreateDbContext();
        await db.Database.MigrateAsync();

        var users = await db.Users.ToListAsync();

        await app.RunAsync();
    }
}