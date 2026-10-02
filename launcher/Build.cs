// Generated public constants. The private signing key is never shipped.
static class Build {
 public const string Feed = "https://github.com/momi25/lemiao-coop/releases/latest/download/update.json";
 public const string PublicKey = @"<RSAKeyValue><Modulus>wj1Kqrul2IxMmNyVtJ8oED0grdXfwEm+lMGyo5RSCw9Urva1P1OZeHSxrZ3wHD4jtgCz8+ARfc1iuBsk2sqDJ6PlHhylzXm12TEh5B4U2Jy2ffIMoVNzEOPzF3GE1fGotfCcnpWyw7L27o9TepFrsgYQ04sg8EIBcgDT1mlzFnBwec8HlEOg3PEI5galVKI3plNP6vV55nJV52CT85yLgLIkvDSky3PEj0hU3zfuuUDTOVTOs7fS6GBFSSJe14oMHBoEfgmrkRi7KXsmom2PQtKK6VRaYfjjsOaYer2aeYVvtyIANEd1QqMY3fRfjnT+uAGlHPuiZepHP530qOT2sw==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
 public static Release BundledRelease(){return Core.Json.Deserialize<Release>(@"{
  ""schema"": 1,
  ""generation"": 1,
  ""version"": ""0.9.1 / Archive repair"",
  ""installer_url"": ""https://github.com/momi25/lemiao-coop/releases/download/v0.9.1-archive4-launcher1/Install-Lemiao-Coop-v0.9.1.exe"",
  ""installer_sha256"": ""db4cf84aa29eacc6ae33f18fdfbb1744c01a9dbd7aa86b5942a4db25d07ca744"",
  ""notes"": ""Native archive packing and installer handoff repaired. Existing file contents preserved; remote equipment variants added. Offline verified; multiplayer gameplay still needs testing.""
}
");}
}
