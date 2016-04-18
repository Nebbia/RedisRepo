# RedisRepo

Wrapper around the StackExchange.Redis library that allows for the most common use cases of a cache.

## Install

### Nuget

```PackageManager
Install-Package RedisRepo -Pre
```

The reason this is in _*Pre-Release*_ (a.k.a. beta) is because of the lack of test coverage. As we get to a point of better test coverage we will make this a full release. This package is used in production currently within several Nebbia Technology projects without any issues.

## Overview

There are two primary abstractions in this library. The `IAppCache` (`RedisCache`) and the `ICacheRepo` (`CacheRepo`). A sample application is provided by [@Hallmanac](https://github.com/Hallmanac/redis-aspnet-azure).

### IAppCache

The `IAppCache` is the abstraction that is directly wrapping the [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) library. It contains methods that are most commonly used when dealing with any cache. It includes `Get` methods for both raw objects and strongly typed values as well as `AddOrUpdate` methods. There are also methods for some advanced scenarios such as [indexed searching](#Indexing) and [partitions](#Partitions) within the cache.

#### Get

Section showing code for `Get` methods with `IAppCache`.

#### AddOrUpdate

Section showing code for `AddOrUpdate` methods with `IAppCache`.

### ICacheRepo

The `ICacheRepo` provides a strongly typed way to interact with the cache. This interface is an abstraction on top of the `IAppCache` interface and provides a way to easily _*Put*_ items in the cache or _*Retreive*_ items from the cache for a given type. This also provides a wrapper around creating and using [indexed searches](#Indexing) within cache in a slightly easier way than what the `IAppCache` provides.

## Partitions

Section explaining partitions.


## Indexing

Section explaining indexing.



