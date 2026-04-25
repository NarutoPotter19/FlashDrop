namespace FlashDrop.API.Modules.Identity
{

    //JwtSettings is going to be a record (not a class) so its properties
    // are immutable once bound from configuration — they cannot be accidentally
    // mutated at runtime. Each property uses 'init' setter (set once at init time).
    public record JwtSettings
    {
        // The property names here we are trying to create in this file 
        //MUST match the keys in appsettings.json exactly
        // (case-insensitive matching is done by the configuration binder):
        //
        //   appsettings.json            JwtSettings record
        //   "JwtSettings": {         →  public record JwtSettings
        //     "Secret": "...",       →    Secret
        //     "Issuer": "...",       →    Issuer
        //     "Audience": "...",     →    Audience
        //     "ExpiryMinutes": 60    →    ExpiryMinutes
        //   }
        public string Secret { get; init; } = string.Empty;

        public string Issuer { get; init; } = string.Empty;

        public string Audience { get; init; } = string.Empty;


        public int  ExpiryMinutes { get; init; } 


    }
}
