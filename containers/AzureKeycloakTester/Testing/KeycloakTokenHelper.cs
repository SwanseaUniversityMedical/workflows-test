using AzureKeycloakTester.Options;
using Serilog;
using System.Net;
using System.Net.Http;

namespace AzureKeycloakTester.Testing
{
    public class KeycloakTokenHelper
    {


        public string _keycloakBaseUrl { get; set; }
        public string _clientId { get; set; }
        public string _clientSecret { get; set; }
        public bool _useProxy { get; set; }
        public string _proxyUrl { get; set; }
        public bool _trustssl { get; set; }
        public string _bypassList { get; set; }

        public KeycloakTokenHelper(string keycloakBaseUrl, string clientId, string clientSecret, bool useProxy, string proxyurl, bool trustssl, string bypassList)
        {
            _keycloakBaseUrl = keycloakBaseUrl;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _useProxy = useProxy;
            _proxyUrl = proxyurl;
            _trustssl = trustssl;
            _bypassList = bypassList;
        }

        public async Task<string> GetTokenForUser(string username, string password, string requiredRole)
        {



            string keycloakBaseUrl = _keycloakBaseUrl;
            string clientId = _clientId;
            string clientSecret = _clientSecret;

            Log.Information($"GetTokenForUser _proxyUrl > {_proxyUrl} UseProxy > {_useProxy}");
            var handler = new HttpClientHandler();
            if (_useProxy || _trustssl)
            {
                

                //This is vital if behind a proxy. Especially true for our proxy. Set this up correctly when
                //deploying behind proxy (some proxies are silent and don't need it)
                if (_useProxy)
                {
                    Log.Information("{Function} Proxy = {Proxy}, Bypass = {Bypass}", "GetTokenForUser", _proxyUrl);
                    handler.UseProxy = true;
                    handler.UseDefaultCredentials = true;
                    handler.Proxy = new WebProxy()
                    {
                        Address = new Uri(_proxyUrl),
                        BypassList = new[] { _bypassList }
                    };
                }

                //Sometimes we need to trust a self signed certificate or ignore ssl errors. In which case set 
                //AutoTrustKeycloakCert to true.It won't break to always set it to true but better to only do it 
                //when needed for security reasons
                if (_trustssl)
                {
                    Log.Information("{Function} Trust Keycloak Server ssl", "GetTokenForUser");
                    handler.ServerCertificateCustomValidationCallback =
                        (sender, certificate, chain, sslPolicyErrors) => true;
                }
            }

            // Create an HttpClient with the handler
            return await KeycloakCommon.GetTokenForUserGuts(username, password, requiredRole, handler, keycloakBaseUrl, clientId, clientSecret);

        }
    }



    public class TokenRoles
    {
        public List<string> roles { get; set; }
    }
}
