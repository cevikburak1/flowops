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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var entitlements = new PlanEntitlements(builder.Configuration);
entitlements.Validate();
builder.Services.AddSingleton(entitlements);
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
var secret = builder.Configuration["Auth:ClientSecret"]
    ?? throw new InvalidOperationException("Auth:ClientSecret is required.");
var keys = builder.Configuration["Auth:DataProtectionKeyPath"];
if (!development) AuthDeployment.Validate(webOrigin, authority, keys);
if (!string.IsNullOrWhiteSpace(keys))
    builder.Services.AddDataProtection().SetApplicationName("FlowOps").PersistKeysToFileSystem(new DirectoryInfo(keys));

builder.Services.AddDbContext<FlowOpsDb>(options => options.UseNpgsql(connection, postgres =>
    postgres.MigrationsHistoryTable("__EFMigrationsHistory", "flowops")));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealth>("postgres");
var requestLimit = development ? Math.Clamp(builder.Configuration.GetValue("Testing:RequestsPerMinute", 240), 240, 3000) : 240;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var caller = context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var hubRequest = context.Request.Path.Value?.Contains("/notifications/hub", StringComparison.Ordinal) == true;
        return RateLimitPartition.GetFixedWindowLimiter(hubRequest ? $"hub:{caller}" : $"http:{caller}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = requestLimit, Window = TimeSpan.FromMinutes(1) });
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (builder.Configuration["Auth:TrustedProxy"] is { Length: > 0 } proxy)
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});
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
builder.Services.AddSignalR();
builder.Services.AddSingleton<ProcessTargets>();
builder.Services.AddSingleton<ProcessObjectStore>();
builder.Services.AddHttpClient("flowops-process", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IUserIdProvider, KeycloakUserIdProvider>();
builder.Services.AddHttpClient("flowops-webhook").ConfigurePrimaryHttpMessageHandler(() =>
    new SocketsHttpHandler { AllowAutoRedirect = false });
if (builder.Configuration["Integrations:Webhook:Url"] is { Length: > 0 } webhook &&
    (!Uri.TryCreate(webhook, UriKind.Absolute, out var endpoint) ||
        (endpoint.Scheme != "https" && (!development || endpoint.Scheme != "http")) ||
        string.IsNullOrWhiteSpace(builder.Configuration["Integrations:Webhook:Key"])))
    throw new InvalidOperationException("The operator webhook requires a valid URL and authentication key.");
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef =>
        ef.UsePostgreSql(connection)));
    elsa.UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef =>
        ef.UsePostgreSql(connection)));
    elsa.AddActivity<WaitForPurchaseApproval>();
    elsa.AddActivity<EvaluatePurchaseBranch>();
    elsa.AddActivity<NotifyPurchaseRequester>();
    elsa.AddActivity<ProcessTimer>();
    elsa.AddActivity<ProcessHttp>();
    elsa.AddActivity<ProcessAssignment>();
    elsa.AddWorkflow<PurchaseApprovalWorkflow>();
});
builder.Services.AddHostedService<PurchaseWorkflowWorker>();
builder.Services.AddHostedService<ProcessWorker>();
builder.Services.AddHostedService<ApprovalAssignments>();
builder.Services.AddHostedService<NotificationDispatcher>();
builder.Services.AddSingleton<InvitationEmailDelivery>();
builder.Services.AddHostedService(services => services.GetRequiredService<InvitationEmailDelivery>());

var app = builder.Build();
if (!development)
{
    var protector = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("FlowOps.StartupCheck.v1");
    var marker = Guid.NewGuid().ToString();
    if (protector.Unprotect(protector.Protect(marker)) != marker)
        throw new InvalidOperationException("The API key ring failed its startup verification.");
}
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    context.Response.Headers.XContentTypeOptions = "nosniff";
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
app.MapHub<NotificationHub>("/api/notifications/hub", options =>
    options.CloseOnAuthenticationExpiration = true).RequireAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
    await database.Database.MigrateAsync();
    if (development && builder.Configuration.GetValue<bool>("FlowOps:SeedDevelopment"))
        await DevelopmentSeed.ApplyAsync(database);
}
await app.RunAsync();
