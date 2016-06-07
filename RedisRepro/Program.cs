using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace RedisRepro
{
    class Program
    {
        // KEYS[1] = = key
        // ARGV[1] = absolute-expiration - ticks as long (-1 for none)
        // ARGV[2] = sliding-expiration - ticks as long (-1 for none)
        // ARGV[3] = relative-expiration (long, in seconds, -1 for none) - Min(absolute-expiration - Now, sliding-expiration)
        // ARGV[4] = data - byte[]
        // this order should not change LUA script depends on it
        private const string SetScript = (@"
                redis.call('HMSET', KEYS[1], 'absexp', ARGV[1], 'sldexp', ARGV[2], 'data', ARGV[4])
                if ARGV[3] ~= '-1' then
                  redis.call('EXPIRE', KEYS[1], ARGV[3])
                end
                return 1");
        private const string AbsoluteExpirationKey = "absexp";
        private const string SlidingExpirationKey = "sldexp";
        private const string DataKey = "data";
        private const long NotPresent = -1;

        private static ConnectionMultiplexer _connection;
        private static IDatabase _cache;
        private static string _instance;

        static void Main(string[] args)
        {
            _connection = ConnectionMultiplexer.Connect("localhost:6379");
            _cache = _connection.GetDatabase();
            _instance = string.Empty;

            var tasks = new Task[25];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => GetValueFromCacheAsync());
            }
            Task.WaitAll(tasks);

            Console.WriteLine("Enter any key to exit");
            Console.ReadLine();
        }

        private static async Task GetValueFromCacheAsync()
        {
            var enter = DateTime.Now;
            var sw1 = new Stopwatch();
            sw1.Start();
            var value = await GetAndRefreshAsync("foo");
            sw1.Stop();

            var sw2 = new Stopwatch();
            sw2.Start();
            Thread.Sleep(5 * 1000);
            sw2.Stop();
            var exit = DateTime.Now;

            Console.WriteLine($"Value {Encoding.UTF8.GetString(value)}. Enter: {enter}, Exit: {exit}, " +
                $"Cache get duration {sw1.ElapsedMilliseconds} ms. Sleep duration {sw2.ElapsedMilliseconds}");
        }

        private static async Task<byte[]> GetAndRefreshAsync(string key, bool getData = true)
        {
            var results = await _cache.HashMemberGetAsync(_instance + key, AbsoluteExpirationKey, SlidingExpirationKey, DataKey);
            if (results.Length >= 3 && results[2].HasValue)
            {
                return results[2];
            }

            return null;
        }
    }

    internal static class RedisExtensions
    {
        private const string HmGetScript = (@"return redis.call('HMGET', KEYS[1], unpack(ARGV))");

        internal static async Task<RedisValue[]> HashMemberGetAsync(
            this IDatabase cache,
            string key,
            params string[] members)
        {
            var result = await cache.ScriptEvaluateAsync(
                HmGetScript,
                new RedisKey[] { key },
                GetRedisMembers(members));

            // TODO: Error checking?
            return (RedisValue[])result;
        }

        private static RedisValue[] GetRedisMembers(params string[] members)
        {
            var redisMembers = new RedisValue[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                redisMembers[i] = (RedisValue)members[i];
            }

            return redisMembers;
        }
    }
}
