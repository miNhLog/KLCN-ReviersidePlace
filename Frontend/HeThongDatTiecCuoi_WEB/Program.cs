using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

const string externalCookieScheme = "External";

var builder = WebApplication.CreateBuilder(args);
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddHttpClient<IRiversideApiClient, RiversideApiClient>(client =>
    {
        var baseUrl = builder.Configuration["RiversideApi:BaseUrl"]
            ?? throw new InvalidOperationException("Thiếu RiversideApi:BaseUrl.");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(20);
    });

builder.Services.AddHttpClient("RiversideApi", client =>
{
    var baseUrl = builder.Configuration["RiversideApi:BaseUrl"]
        ?? throw new InvalidOperationException("Thiếu RiversideApi:BaseUrl.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/dang-nhap";
        options.AccessDeniedPath = "/khong-co-quyen";
        options.Cookie.Name = "rp_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
    })
    .AddCookie(externalCookieScheme, options =>
    {
        options.Cookie.Name = "rp_external_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddGoogle("Google", options =>
    {
        options.SignInScheme = externalCookieScheme;
        options.ClientId = string.IsNullOrWhiteSpace(googleClientId)
            ? "google-client-id-not-configured"
            : googleClientId;
        options.ClientSecret = string.IsNullOrWhiteSpace(googleClientSecret)
            ? "google-client-secret-not-configured"
            : googleClientSecret;
        options.SaveTokens = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
