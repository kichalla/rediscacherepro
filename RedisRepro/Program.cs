using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace RedisRepro
{
    class Program
    {
        private static RedisCache _redisCache;

        static void Main(string[] args)
        {
            _redisCache = new RedisCache(new RedisCacheOptions()
            {
                Configuration = "localhost:6379"
            });

            _redisCache.Set("foo", Encoding.UTF8.GetBytes("bar"), new DistributedCacheEntryOptions());

            var tasks = new Task[25];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => GetValueFromCacheAsync());
            }
            Task.WaitAll(tasks);

            Console.ReadLine();
        }

        private static async Task GetValueFromCacheAsync()
        {
            var enter = DateTime.Now;
            var sw1 = new Stopwatch();
            sw1.Start();
            var value = await _redisCache.GetAsync("foo");
            sw1.Stop();

            var sw2 = new Stopwatch();
            sw2.Start();
            Thread.Sleep(5 * 1000);
            sw2.Stop();
            var exit = DateTime.Now;

            Console.WriteLine($"Value {Encoding.UTF8.GetString(value)}. Enter: {enter}, Exit: {exit}, " +
                $"Cache get duration {sw1.ElapsedMilliseconds} ms. Sleep duration {sw2.ElapsedMilliseconds}");
        }
    }
}
