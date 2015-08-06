﻿// Copyright © 2013-2015 Sergey Odinokov, Marco Casamento 
// This software is based on https://github.com/HangfireIO/Hangfire.Redis 

// Hangfire.Redis.StackExchange is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// Hangfire.Redis.StackExchange is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with Hangfire.Redis.StackExchange. If not, see <http://www.gnu.org/licenses/>.

using Hangfire.Annotations;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading;

namespace Hangfire.Redis
{
    public sealed class RedisLock : IDisposable
    {
        readonly IDatabase _redis;
        readonly RedisKey _key;
        readonly RedisValue _owner;
        public RedisLock([NotNull]IDatabase redis, [NotNull]RedisKey key, [NotNull]RedisValue owner, [NotNull]TimeSpan timeOut)
        {
            _redis = redis;
            _key = key;
            _owner = owner;

            bool lockObtained = false;
            TimeSpan waitingTimeToObtainLock = TimeSpan.Zero;
            //The comparison below uses timeOut as a max timeSpan in waiting Lock
            int i = 0;
            while (!lockObtained && waitingTimeToObtainLock < timeOut )
            {
                lockObtained = _redis.LockTake(key, owner, timeOut);
                if (!lockObtained)
                    //Maybe the lock already belongs to the owner, in that case, extends it
                    lockObtained = _redis.LockExtend(key, owner, timeOut);

                if (!lockObtained)
                {
                    SleepBackOffMultiplier(i);
                    i++;
                }
            }
            if (!lockObtained)
                throw new TimeoutException(string.Format("Lock on {0} with owner identifier {1} Exceeded timeout of {2}", key, RedisStorage.Identity, timeOut));
        }

        public void Dispose()
        {
            if (!_redis.LockRelease(_key, _owner))
                Debug.WriteLine("Can't release lock {0} - {1}",_key, _owner);

        }

        private static void SleepBackOffMultiplier(int i)
        {
            //exponential/random retry back-off.
            var rand = new Random(Guid.NewGuid().GetHashCode());
            var nextTry = rand.Next(
                (int)Math.Pow(i, 2), (int)Math.Pow(i + 1, 2) + 1);

            Thread.Sleep(nextTry);
        }
    }
}
