
using System.Collections;
using AzureKeycloakTester.Models;
using AzureKeycloakTester.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using AzureKeycloakTester.Testing;
using Microsoft.AspNetCore.Authentication;

namespace AzureKeycloakTester.Extensions
{
    public static class ServiceExtensions
    {
        public static void AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                //The KeyCloakSettings class in Options folder is useful for binding the corresponding
                //appsettings.json file. It's not vital but helps keep uniformity between projects
                var keyCloakSettings = new KeyCloakSettings();
                configuration.Bind(nameof(keyCloakSettings), keyCloakSettings);
                services.AddSingleton(keyCloakSettings);

                //For testing
                //var khelper = new KeycloakTokenHelper(keyCloakSettings.Authority, keyCloakSettings.ClientId,
                //    keyCloakSettings.ClientSecret, keyCloakSettings.Proxy, keyCloakSettings.ProxyAddressURL, keyCloakSettings.AutoTrustKeycloakCert, keyCloakSettings.BypassProxy);
                //var token = khelper.GetTokenForUser("assetsadmin", "password123", "").Result;
                //Make sure the following three options are set. It's possible some aren't needed but do keep it
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                        
                    })
                    //I'm not sure how neccesary setting the session store is (although AddCookie itself is needed)
                    //May as well keep it in. Make sure the MemoryCacheTicketStore.cs class is included from Models
                    .AddCookie(o => o.SessionStore = new MemoryCacheTicketStore())

                    .AddOpenIdConnect(options =>
                    {
                        if (keyCloakSettings.Proxy || keyCloakSettings.AutoTrustKeycloakCert)
                        {
                            var httpClientHandler = new HttpClientHandler();

                            //This is vital if behind a proxy. Especially true for our proxy. Set this up correctly when
                            //deploying behind proxy (some proxies are silent and don't need it)
                            if (keyCloakSettings.Proxy)
                            {
                                Log.Information("{Function} Proxy = {Proxy}, Bypass = {Bypass}", "AddKeycloakAuthentication", keyCloakSettings.ProxyAddressURL, keyCloakSettings.BypassProxy);
                                httpClientHandler.UseProxy = true;
                                httpClientHandler.UseDefaultCredentials = true;
                                //Don't set bypass list if the string is blank. Breaks proxy setup
                                if (!string.IsNullOrWhiteSpace(keyCloakSettings.BypassProxy))
                                {
                                    httpClientHandler.Proxy = new WebProxy()
                                    {
                                        Address = new Uri(keyCloakSettings.ProxyAddressURL),
                                        BypassList = new[] { keyCloakSettings.BypassProxy }
                                    };
                                }
                                else
                                {
                                    httpClientHandler.Proxy = new WebProxy()
                                    {
                                        Address = new Uri(keyCloakSettings.ProxyAddressURL),

                                    };
                                }
                            }
                            //Sometimes we need to trust a self signed certificate or ignore ssl errors. In which case set 
                            //AutoTrustKeycloakCert to true.It won't break to always set it to true but better to only do it 
                            //when needed for security reasons
                            if (keyCloakSettings.AutoTrustKeycloakCert)
                            {
                                Log.Information("{Function} Trust Keycloak Server ssl", "AddKeycloakAuthentication");
                                httpClientHandler.ServerCertificateCustomValidationCallback =
                                    (sender, certificate, chain, sslPolicyErrors) => true;
                            }

                            options.BackchannelHttpHandler = httpClientHandler;
                        }

                        
                        
                        //This should be of the form keycloak baseurl + "/realms/{MyRealm}
                        //where {MyRealm} should be replaced with your actual realm eg. HDPBC
                        options.Authority = keyCloakSettings.Authority;
                        //Keycloak client id
                        options.ClientId = keyCloakSettings.ClientId;
                        //Keycloak client secret
                        options.ClientSecret = keyCloakSettings.ClientSecret;
                        //Next two lines only matters for handling sign out. We've never done it properly yet
                        //May need to properly work out how to do this in future
                        options.RemoteSignOutPath = keyCloakSettings.RemoteSignOutPath;
                        options.SignedOutRedirectUri = keyCloakSettings.SignedOutRedirectUri;
                        //Make sure these next four lines are present
                        options.RequireHttpsMetadata = false;
                        options.GetClaimsFromUserInfoEndpoint = true;
                        options.ResponseType = OpenIdConnectResponseType.Code;
                        options.SaveTokens = true;
                        //You may need to add additional scopes depending on your program
                        options.Scope.Add("openid");
                        
                        
                        options.Events = new OpenIdConnectEvents()
                        {
                            //OnTokenValidate only needed for debugging. Feel free to remove
                            OnTokenValidated = context =>
                            {
                                // Access the tokens
                                var accessToken = context.SecurityToken as JwtSecurityToken;
                                var idToken = context.SecurityToken as JwtSecurityToken;

                                // Convert tokens to JSON strings
                                var accessTokenJson = accessToken?.RawData; // Access token as JSON string
                                var idTokenJson = idToken?.RawData;
                                Log.Information("{Function} The access {Access}, the ID {TheId}",
                                    "OnTokenValidated", accessTokenJson, idTokenJson);
                                
                                // Log the issuer claim from the token
                                var issuer = context.Principal.FindFirst("iss")?.Value;
                                Log.Information("Token Issuer: {Issuer}", issuer);
                                var audience = context.Principal.FindFirst("aud")?.Value;
                                Log.Information("Token Audience: {Audience}", audience);
                                return Task.CompletedTask;
                            },
                            //OnRemoteFailure only needed for debugging. Feel free to remove
                            OnRemoteFailure = context =>
                            {

                                
                                Log.Error("{Function} Failure Message: {Message}", "OnRemoteFailure",
                                    context.Failure.Message);
                                if (context.Response.Body.CanRead)
                                {
                                    string result = ReadStreamToString(context.Response.Body);
                                    Log.Error("{Function} Failure body: {Body}", "OnRemoteFailure", result);
                                }

                                foreach (DictionaryEntry dictionaryEntry in context.Failure.Data)
                                {
                                    Log.Error("{Function} Failure Date: {Key}:{Value}", "OnRemoteFailure",
                                        dictionaryEntry.Key, dictionaryEntry.Value);
                                }

                                if (context.Failure.InnerException != null)
                                {
                                    Log.Error("{Function} Inner Exception message: {InnerMessage}", "OnRemoteFailure",
                                        context.Failure.InnerException.Message);
                                }

                                if (context.Properties != null)
                                {
                                    foreach (var authenticationProperty in context.Properties.Items)
                                    {
                                        Log.Error("{Function} Property: {Key}:{Value}", "OnRemoteFailure",
                                            authenticationProperty.Key, authenticationProperty.Value);
                                    }
                                }

                               

                                context.HandleResponse();

                                return context.Response.CompleteAsync();
                            }
                        };
                        
                        //Make sure these two are present and set to None (Unspecified may also work but untested so
                        //unless you wish to play around stick with these
                        options.NonceCookie.SameSite = SameSiteMode.None;
                        options.CorrelationCookie.SameSite = SameSiteMode.None;

                        options.TokenValidationParameters = new TokenValidationParameters();


                        //The following will map keycloak preferred_username claim to User.Identity.Name
                        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "preferred_username");

                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            NameClaimType = "preferred_username"
                        };

                        //These are optional on if you want to enforce certain conditions.
                        //Commented out but kepy as an example
                        //options.TokenValidationParameters.NameClaimType = "name";
                        //options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
                        //options.TokenValidationParameters = new TokenValidationParameters
                        //{
                        //    NameClaimType = "name",
                        //    RoleClaimType = ClaimTypes.Role

                        //};


                        //We had a case where the program for some reason didn't trust the keycloak as a valid Issuer
                        //You can define it here setting ValidIssuer to keycloak's base URL but only do it 
                        //if neccessary
                        if (!string.IsNullOrWhiteSpace(keyCloakSettings.ValidIssuer))
                        {
                            Log.Information("{Function} Setting valid issuer {ValidIssuer}",
                                "AddKeycloakAuthentication", keyCloakSettings.ValidIssuer);
                            options.TokenValidationParameters.ValidateIssuer = true;
                            options.TokenValidationParameters.ValidIssuer = keyCloakSettings.ValidIssuer;
                            // Use ValidIssuers if there are multiple valid issuers. Edge case. Not likely to need this
                            //but useful to know
                            // ValidIssuers = new[] { "http://auth-hdpbc.healthbc.org/realms/HDP", "other-issuer" },
                        }

                        //If you want to restrict audience as well. I think this really is only relevant
                        //to the API and you would set it there to tell it which keycloak clients you trust 
                        //tokens from
                        if (!string.IsNullOrWhiteSpace(keyCloakSettings.ValidAudience))
                        {
                            Log.Information("{Function} Setting valid audience {ValidAudience}",
                                "AddKeycloakAuthentication", keyCloakSettings.ValidAudience);
                            options.TokenValidationParameters.ValidAudience = keyCloakSettings.ValidAudience;
                        }

                        
                    });
            }
            catch (Exception e)
            {
                Log.Error(e, "{Function} Crash", "AddKeycloakAuthentication");
                throw;
            }


            //Just used to display debug info. Turn off to prevent displaying sensitive details in error messages
            IdentityModelEventSource.ShowPII = true;
        }

        static string ReadStreamToString(Stream stream)
        {
            // Create a StreamReader with a specific encoding (UTF-8 in this example)
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                // Read the entire stream into a string
                string result = reader.ReadToEnd();
                return result;
            }
        }



    }


}
