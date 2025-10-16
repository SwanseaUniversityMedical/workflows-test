namespace AzureKeycloakTester.Options
{
    //The KeyCloakSettings class in Options folder is useful for binding the corresponding
    //appsettings.json file. It's not vital but helps keep uniformity between projects
    public class KeyCloakSettings
    {

        public string Authority { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RemoteSignOutPath { get; set; }
        public string SignedOutRedirectUri { get; set; }
        public string TokenExpiredAddress { get; set; }
        public string ValidAudience { get; set; }

        public bool Proxy { get; set; }
        public string RedirectURL { get; set; }

        public bool UseRedirectURL { get; set; }

        public string BypassProxy { get; set; }

        public string ProxyAddressURL { get; set; }
        
        public string ValidIssuer { get; set; }
        public bool AutoTrustKeycloakCert { get; set; }
    }
}
