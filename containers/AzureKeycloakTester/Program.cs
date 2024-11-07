using AzureKeycloakTester.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

var appName = typeof(Program).Namespace;
var configuration = GetConfiguration();
Log.Logger = CreateSerilogLogger(configuration);
try
{

    Log.Information("Configuring web host ({ApplicationContext})...", appName);
    var builder = WebApplication.CreateBuilder(args);

    //This is only needed if you want all the .net core output to go to serilog
    builder.Host.UseSerilog();
    // Add services to the container.
    builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
    var services = builder.Services;

    //It's possible the forward limit and some of the forwarded headers aren't needed but leave them in
    //as it doesn't seem to cause issues.
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 2; //Limit number of proxy hops trusted
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    //This may only be relevant when passing a token to an api. Again leave it in
    services.AddCors(options => options.AddDefaultPolicy(
        builder =>
        {
            builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }));
    services.AddMvc().AddViewComponentsAsServices();

    //SameSiteMode in some our examples are set to None. I think Unspecified is the best one here
    services.Configure<CookiePolicyOptions>(options =>
    {
        options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
        options.OnAppendCookie = cookieContext =>
            CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
        options.OnDeleteCookie = cookieContext =>
            CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
    });

    //In some case we just call the keycloak separately. That's fine. In this case it's just offloaded to an
    //extension class caled ServiceExtensions.cs in Extensions folder
    //Make sure though you check out this class as important setup is contained in here as well
    services.AddKeycloakAuthentication(configuration);


    var app = builder.Build();
    //Make sure UseCors appears immediately after creation of app object before anything else
    app.UseCors();
    app.UseForwardedHeaders();
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        //app.UseDeveloperExceptionPage();
    }
    //Disable redirect if using http only site to prevent silent redirect to non existent https site
    var httpsRedirect = configuration["httpsRedirect"];
    if (httpsRedirect != null && httpsRedirect.ToLower() == "true")
    {
        Log.Information("Turning on https Redirect");
        app.UseHttpsRedirection();
    }
    else
    {
        Log.Information("Https redirect disabled. Http only");
    }

    //This is a biggy. If having issues with keycloak DISABLE THIS
    //To be honest. Only enable if you have a good reason to
    if (configuration["sslcookies"] == "true")
    {
        Log.Information("Enabling Secure SSL Cookies");
        app.UseCookiePolicy(new CookiePolicyOptions
        {
            Secure = CookieSecurePolicy.Always
        });
    }
    else
    {
        Log.Information("Disabling Secure SSL Cookies");
        app.UseCookiePolicy();
    }


    app.UseStaticFiles();

    app.UseRouting();
    
    //The order is important here. With the exception of MapControllerRoute make these two the last thing before 
    //app.Run(). And make sure Authentication first then Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    Log.Information("Starting testing ({ApplicationContext})...", appName);
    app.Run();
    


}
catch (Exception ex)
{
    Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", appName);
}
finally
{
    Log.Information("Stopping web ui V3 ({ApplicationContext})...", appName);
    Log.CloseAndFlush();
}
Serilog.ILogger CreateSerilogLogger(IConfiguration configuration)
{
    var seqServerUrl = configuration["Serilog:SeqServerUrl"];
    var seqApiKey = configuration["Serilog:SeqApiKey"];

    if (string.IsNullOrEmpty(seqServerUrl))
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("ApplicationContext", appName)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();
    }
    else
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("ApplicationContext", appName)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Seq(seqServerUrl, apiKey: seqApiKey)
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }
}

IConfiguration GetConfiguration()
{
    var builder1 = new ConfigurationBuilder()
        //.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    return builder1.Build();
}


//This is needed for keycloak
//We've had variations of this method. Use this one with SameSiteMode.None and SameSiteMode.Unspecified exactly as below
void CheckSameSite(HttpContext httpContext, CookieOptions options)
{
    if (options.SameSite == SameSiteMode.None)
    {
        var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
        //configure cookie policy to omit samesite=none when request is not https
        if (!httpContext.Request.IsHttps || DisallowsSameSiteNone(userAgent))
        {
            options.SameSite = SameSiteMode.Unspecified;
        }
    }
}

//This is also needed for keycloak
//  Read comments in https://docs.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-3.1
bool DisallowsSameSiteNone(string userAgent)
{
    // Check if a null or empty string has been passed in, since this
    // will cause further interrogation of the useragent to fail.
    if (String.IsNullOrWhiteSpace(userAgent))
        return false;

    // Cover all iOS based browsers here. This includes:
    // - Safari on iOS 12 for iPhone, iPod Touch, iPad
    // - WkWebview on iOS  12 for iPhone, iPod Touch, iPad
    // - Chrome on iOS 12 for iPhone, iPod Touch, iPad
    // All of which are broken by SameSite=None, because they use the iOS networking
    // stack.
    if (userAgent.Contains("CPU iPhone OS 12") ||
        userAgent.Contains("iPad; CPU OS 12"))
    {
        return true;
    }

    // Cover Mac OS X based browsers that use the Mac OS networking stack. 
    // This includes:
    // - Safari on Mac OS X.
    // This does not include:
    // - Chrome on Mac OS X
    // Because they do not use the Mac OS networking stack.
    if (userAgent.Contains("Macintosh; Intel Mac OS X 10_14") &&
        userAgent.Contains("Version/") && userAgent.Contains("Safari"))
    {
        return true;
    }

    // Cover Chrome 50-69, because some versions are broken by SameSite=None, 
    // and none in this range require it.
    // Note: this covers some pre-Chromium Edge versions, 
    // but pre-Chromium Edge does not require SameSite=None.
    if (userAgent.Contains("Chrome/5") || userAgent.Contains("Chrome/6"))
    {
        return true;
    }

    return false;
}
