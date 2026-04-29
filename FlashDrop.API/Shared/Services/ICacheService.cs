namespace FlashDrop.API.Shared.Services
{

    //ICacheService — the contract every cache-using handler depends on.
    //
    // WHY AN INTERFACE instead of using RedisCacheService directly?
    // Testability: in unit tests you can mock ICacheService without a real Redis instance.
    // Flexibility: swap Redis for Memcached or in-memory cache by changing
    //              only the DI registration in Program.cs — no handler code changes.
    public interface ICacheService
    {


        //GetAsync<T> — retrieve a cached value by key.
        //
        // Returns T? (nullable T) because:
        //   - Cache HIT  → returns the deserialized T object
        //   - Cache MISS → returns null (the key doesn't exist in Redis)
        //
        // Callers check:  var cached = await _cache.GetAsync<ProductDto>(key);
        //                 if (cached != null) return cached;  // cache hit — skip DB
        //                 // cache miss — query DB, then call SetAsync
        //
        // Generic <T> so ONE interface handles all types:
        //   GetAsync<ProductDto>("product:abc")
        //   GetAsync<List<ProductDto>>("products:active:page:1")
        Task<T?> GetAsync<T>(string key);

//: SetAsync<T> — store a value in cache with an expiry.
    //
    // expiry (TimeSpan) MUST always be provided — no infinite cache entries.
    // Reason: if a product's stock changes, we want the cache to expire
    //         within a bounded time window and reflect the update.
    //
    // Typical expiry values in this project:
    //   Product by ID:     TimeSpan.FromMinutes(5)
    //   Active products:   TimeSpan.FromMinutes(2)  (changes more often)
            Task SetAsync<T>(string key, T value, TimeSpan expiry);
        
    }
}
