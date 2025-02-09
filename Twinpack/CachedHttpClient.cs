﻿using System;
using System.Net.Http;
using System.Runtime.Caching; // Add a reference to System.Runtime.Caching.dll
using System.Threading.Tasks;

public class CachedHttpClient : HttpClient
{
    private readonly ObjectCache _cache;

    public CachedHttpClient() : base()
    {
        _cache = MemoryCache.Default;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan? cacheDuration=null)
    {
        if(request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
            return await base.SendAsync(request);

        cacheDuration = cacheDuration ?? TimeSpan.FromHours(12);

        var url = request.RequestUri.ToString(); 
        if (_cache.Contains(url))
        {
            return _cache.Get(url) as HttpResponseMessage;
        }

        var response = await base.SendAsync(request);
        var cacheItemPolicy = new CacheItemPolicy
        {
            AbsoluteExpiration = DateTimeOffset.Now.Add((TimeSpan)cacheDuration)
        };
        _cache.Set(url, response, cacheItemPolicy);

        return response;
    }

    public void Invalidate()
    {
        foreach (var item in _cache)
            _cache.Remove(item.Key);
    }
}
