using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Serilog;

namespace AzureKeycloakTester.Options
{
    public class CustomCookieEvent : CookieAuthenticationEvents
    {
        private readonly IConfiguration _config;

        public CustomCookieEvent(IConfiguration config)
        {
            _config = config;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            if (context != null)
            {
                var accessToken = context.Properties.GetTokenValue("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    Log.Information("{Function} Cookie Step 1", "ValidatePrinciple");
                    var handler = new JwtSecurityTokenHandler();
                    var tokenS = handler.ReadToken(accessToken) as JwtSecurityToken;
                    var tokenExpiryDate = tokenS.ValidTo;
                    //// If there is no valid `exp` claim then `ValidTo` returns DateTime.MinValue
                    //if (tokenExpiryDate == DateTime.MinValue) throw new Exception("Could not get exp claim from token");
                    if (tokenExpiryDate < DateTime.UtcNow)
                    {
                        Log.Information("{Function} Cookie Step 2", "ValidatePrinciple");
                        var refreshToken = context.Properties.GetTokenValue("refresh_token");

                        //check if users refresh token is still valid?
                        var tokenRefresh = handler.ReadToken(refreshToken) as JwtSecurityToken;
                        var refreshTokenExpiryDate = tokenRefresh.ValidTo;
                        if (refreshTokenExpiryDate < DateTime.UtcNow)
                        {
                            Log.Information("{Function} Cookie Step 3", "ValidatePrinciple");

                            //probably need to log user out
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        else
                        {
                            try
                            {
                                Log.Information("{Function} Cookie Step 4", "ValidatePrinciple");
                                var tokenResponse = await new HttpClient().RequestRefreshTokenAsync(new RefreshTokenRequest
                                {
                                    Address = _config["KeyCloakSettings:Authority"] + "/protocol/openid-connect/token",
                                    ClientId = _config["KeyCloakSettings:ClientId"],
                                    ClientSecret = _config["KeyCloakSettings:ClientSecret"],
                                    RefreshToken = refreshToken
                                });
                                Log.Information("{Function} Cookie Step 5 {add}, {client}, {secret}", "ValidatePrinciple", _config["KeyCloakSettings:Authority"] + "/protocol/openid-connect/token", _config["KeyCloakSettings:ClientId"], _config["KeyCloakSettings:ClientSecret"]);
                                if (tokenResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    Log.Information("{Function} Cookie Step 6", "ValidatePrinciple");
                                    context.Properties.UpdateTokenValue("access_token", tokenResponse.AccessToken);
                                    context.Properties.UpdateTokenValue("refresh_token", tokenResponse.RefreshToken);
                                    await context.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, context.Principal, context.Properties);
                                }
                                else
                                {
                                    Log.Information("{Function} Cookie Step 7", "ValidatePrinciple");
                                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "{Function} Something went wrong", "ValidatePrinciple");
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            }
                        }
                    }
                }
            }
        }
    }
}
