using System.Security.Claims;
using System.Threading.RateLimiting;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using FlowOps.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var development = builder.Environment.IsDevelopment();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
var connection = builder.Configuration.GetConnectionString("FlowOps")
    ?? throw new InvalidOperationException("ConnectionStrings:FlowOps is required.");
var authority = builder.Configuration["Auth:Authority"] ?? (development
    ? "http://localhost:58080/realms/flowops"
    : throw new InvalidOperationException("Auth:Authority is required."));
var webOrigin = new Uri(builder.Configuration["Auth:WebOrigin"] ?? (development
    ? "http://localhost:3000"
    : throw new InvalidOperationException("Auth:WebOrigin is required.")));
if (!development && (webOrigin.Scheme != "https" || new Uri(authority).Scheme != "https"))
    throw new InvalidOperationException("Production authentication requires HTTPS.");
var secret = builder.Configuration["Auth:ClientSecret"]
    ?? throw new InvalidOperationException("Auth:ClientSecret is required.");

builder.Services.AddDbContext<FlowOpsDb>(options => options.UseNpgsql(connection, postgres =>
    postgres.MigrationsHistoryTable("__EFMigrationsHistory", "flowops")));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealth>("postgres");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue("sub") ??
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor);
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "FlowOpsSession";
    options.DefaultChallengeScheme = "FlowOpsSession";
})
.AddPolicyScheme("FlowOpsSession", null, options => options.ForwardDefaultSelector = context =>
    context.Request.Headers.ContainsKey("Authorization")
        ? JwtBearerDefaults.AuthenticationScheme
        : CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.Cookie.Name = "flowops.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = false;
})
.AddOpenIdConnect(options =>
{
    options.Authority = authority;
    options.RequireHttpsMetadata = !development;
    options.ClientId = "flowops-web";
    options.ClientSecret = secret;
    options.ResponseType = "code";
    options.UsePkce = true;
    options.Scope.Add("email");
    options.UseTokenLifetime = true;
    options.MapInboundClaims = false;
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.CallbackPath = "/auth/callback";
    if (development)
    {
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.None;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
    }
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        context.ProtocolMessage.RedirectUri = new Uri(webOrigin, "/auth/callback").AbsoluteUri;
        return Task.CompletedTask;
    };
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = "flowops-api";
    options.RequireHttpsMetadata = !development;
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters { ClockSkew = TimeSpan.FromSeconds(30) };
});
builder.Services.AddAuthorization();
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef =>
        ef.UsePostgreSql(connection)));
    elsa.UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef =>
        ef.UsePostgreSql(connection)));
    elsa.AddActivity<WaitForPurchaseApproval>();
    elsa.AddActivity<EvaluatePurchaseBranch>();
    elsa.AddActivity<NotifyPurchaseRequester>();
    elsa.AddWorkflow<PurchaseApprovalWorkflow>();
});
builder.Services.AddHostedService<PurchaseWorkflowWorker>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isApplicationWrite = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
        (path.StartsWithSegments("/api") || path == "/auth/logout");
    if (isApplicationWrite && (context.Request.Cookies.ContainsKey("flowops.session") ||
        !context.Request.Headers.ContainsKey("Authorization")) &&
        !string.Equals(context.Request.Headers.Origin, webOrigin.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (path.StartsWithSegments("/api") || path.StartsWithSegments("/auth"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/auth/login", (string? destination) => Results.Challenge(
    new AuthenticationProperties { RedirectUri = new Uri(webOrigin, destination == "invitation" ? "/join" : "/workspace").AbsoluteUri },
    [OpenIdConnectDefaults.AuthenticationScheme]));
app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var logoutUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
        authority.TrimEnd('/') + "/protocol/openid-connect/logout", new Dictionary<string, string?>
        {
            ["client_id"] = "flowops-web",
            ["post_logout_redirect_uri"] = new Uri(webOrigin, "/").AbsoluteUri
        });
    return Results.Redirect(logoutUrl);
});
app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok());
if (development) app.MapOpenApi();
app.MapFlowOps();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
    await database.Database.MigrateAsync();
    if (development && builder.Configuration.GetValue<bool>("FlowOps:SeedDevelopment"))
        await DevelopmentSeed.ApplyAsync(database);
}
await app.RunAsync();
