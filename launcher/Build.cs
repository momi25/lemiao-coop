// Generated public constants. The private signing key is never shipped.
static class Build {
 public const string Feed = "https://github.com/momi25/lemiao-coop/releases/latest/download/update.json";
 public const string PublicKey = @"<RSAKeyValue><Modulus>wj1Kqrul2IxMmNyVtJ8oED0grdXfwEm+lMGyo5RSCw9Urva1P1OZeHSxrZ3wHD4jtgCz8+ARfc1iuBsk2sqDJ6PlHhylzXm12TEh5B4U2Jy2ffIMoVNzEOPzF3GE1fGotfCcnpWyw7L27o9TepFrsgYQ04sg8EIBcgDT1mlzFnBwec8HlEOg3PEI5galVKI3plNP6vV55nJV52CT85yLgLIkvDSky3PEj0hU3zfuuUDTOVTOs7fS6GBFSSJe14oMHBoEfgmrkRi7KXsmom2PQtKK6VRaYfjjsOaYer2aeYVvtyIANEd1QqMY3fRfjnT+uAGlHPuiZepHP530qOT2sw==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
 public static Release BundledRelease(){return Core.Json.Deserialize<Release>(@"{
  ""schema"": 1,
  ""generation"": 4,
  ""version"": ""0.9.1 / PC checks"",
  ""installer_url"": ""https://github.com/momi25/lemiao-coop/releases/download/lemiao-update-4/Install-Lemiao-Coop-v0.9.1.exe"",
  ""installer_sha256"": ""bb134b1713c818cc3bad6687d8dd0a0ad61636e2531242b60e0023db7acdcec9"",
  ""notes"": ""Adds automatic per-PC compatibility checks, local diagnostics export and pinned build dependencies. Gameplay files unchanged; gameplay remains unverified until tested.""
}
");}
}
