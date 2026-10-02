// Generated public constants. The private signing key is never shipped.
static class Build {
 public const string Feed = "https://github.com/momi25/lemiao-coop/releases/latest/download/update.json";
 public const string PublicKey = @"<RSAKeyValue><Modulus>wj1Kqrul2IxMmNyVtJ8oED0grdXfwEm+lMGyo5RSCw9Urva1P1OZeHSxrZ3wHD4jtgCz8+ARfc1iuBsk2sqDJ6PlHhylzXm12TEh5B4U2Jy2ffIMoVNzEOPzF3GE1fGotfCcnpWyw7L27o9TepFrsgYQ04sg8EIBcgDT1mlzFnBwec8HlEOg3PEI5galVKI3plNP6vV55nJV52CT85yLgLIkvDSky3PEj0hU3zfuuUDTOVTOs7fS6GBFSSJe14oMHBoEfgmrkRi7KXsmom2PQtKK6VRaYfjjsOaYer2aeYVvtyIANEd1QqMY3fRfjnT+uAGlHPuiZepHP530qOT2sw==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
 public static Release BundledRelease(){return Core.Json.Deserialize<Release>(@"{
  ""schema"": 1,
  ""generation"": 2,
  ""version"": ""0.9.1 / Setup repair"",
  ""installer_url"": ""https://github.com/momi25/lemiao-coop/releases/download/lemiao-update-2/Install-Lemiao-Coop-v0.9.1.exe"",
  ""installer_sha256"": ""9b0cd34e229ba21023d7da8dd2f4c9a6f4d05dba3c209f292d2dee62ec48cff8"",
  ""notes"": ""Fixes installation on Italian and other Windows languages. Save browser retained; setup errors stay visible. Gameplay files unchanged. No game launch during verification.""
}
");}
}
