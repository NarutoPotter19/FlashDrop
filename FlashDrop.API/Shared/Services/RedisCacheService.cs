
using System.Text.Json;// for JsonSerializer,JsonSerializerOptions, JsonSerializerDefaults
using StackExchange.Redis;// for ConnectionMultiplexer, IDatabase, RedisValue


namespace FlashDrop.API.Shared.Services
{

    //it Implement ICacheService using StackExchange.Redis
    //               and System.Text.Json for serialization.


    // RedisCacheService is the concrete Redis implementation.
    //
    // Singleton lifetime (registered in Program.cs):
    //   - IConnectionMultiplexer (injected) is already Singleton
    //   - RedisCacheService has NO mutable instance state
    //   - Safe to share across all requests simultaneously
    public class RedisCacheService : ICacheService
    {

        //IDatabase is the StackExchange.Redis interface for
    // executing Redis commands (GET, SET, DEL, etc.).
    // We obtain it from the multiplexer — it's lightweight and re-created
    // on each call to GetDatabase() (internally just a wrapper, no new connection).

    private readonly IDatabase _db;


        // JsonSerializerOptions configured once and reused.
        // PropertyNameCaseInsensitive = true means JSON deserialization
        // is forgiving about casing: "productId" and "ProductId" both work.
        // This prevents subtle bugs when deserializing cached data.

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive=true, // PropertyNameCaseInsensitive = true means JSON deserialization
                                              // is forgiving about casing: "productId" and "ProductId" both work.
                                              // This prevents subtle bugs when deserializing cached data.

        };

        //DI injects IConnectionMultiplexer.which we have registered as a singleton in Program.cs
        public RedisCacheService(IConnectionMultiplexer connectionMultiplexer)
        {

            // GetDatabase() returns the default Redis database(index 0).
        // Redis supports multiple databases (0–15) but we only use the default.
            _db = connectionMultiplexer.GetDatabase();
        }





        public async Task<T?> GetAsync<T>(string key)
        {


            var value = await _db.StringGetAsync(key);// StringGetAsync maps to the Redis command: GET key
                                                      // Returns a RedisValue — a struct that can represent:

            if (value.IsNullOrEmpty)
            {
                // Return default(T) — which is null for reference types (like ProductDto).
                return default; // null for class types, 0 for int, etc.
            }

            // value! — the ! null-forgiving operator tells the compiler we've
            // already checked it's not null in the IsNullOrEmpty check above.
            //
            // value.ToString() converts the RedisValue to a plain C# string.
            //
            // JsonSerializer.Deserialize<T> parses the JSON and returns T.
            // If the JSON is malformed (e.g., cache was corrupted), this throws
            // JsonException — which propagates and results in HTTP 500.
            // For a portfolio project this is acceptable. Production code would
            // add a try/catch and treat deserialization failure as a cache miss.

            return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);


        }




        public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
        {


            // Serialize the object to a JSON string.
            // JsonSerializer.Serialize converts ProductDto (or any T) to:
            //   {"id":"3fa85f64...","name":"Air Jordan","sku":"AJ1-RED","price":299.99,...}
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _db.StringSetAsync(key, json, expiry);
        }
       

    }
}
