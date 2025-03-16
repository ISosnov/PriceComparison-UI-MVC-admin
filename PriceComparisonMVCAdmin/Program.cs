using PriceComparisonMVCAdmin.Infrastructure;
using PriceComparisonMVCAdmin.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurationService.ConfigureServices(builder);

builder.Services.AddHttpClient<IApiService, ApiService>();
builder.Services.AddSingleton<TokenManager>();
builder.Services.AddTransient<IAuthService, AuthService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

//app.UseCookiePolicy(new CookiePolicyOptions
//{
//    MinimumSameSitePolicy = SameSiteMode.Strict,
//    Secure = CookieSecurePolicy.Always
//});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Seller}/{action=Index}/{id?}");

app.Run();
