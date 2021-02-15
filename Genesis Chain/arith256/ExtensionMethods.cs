using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace firstchain.arith256
{
    public static class ExtensionMethods
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            var buffer = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, buffer, 0, first.Length);
            Buffer.BlockCopy(second, 0, buffer, first.Length, second.Length);
            return buffer;
        }

        public static byte[] Concat(this byte[] first, byte second)
        {
            var buffer = new byte[first.Length + 1];
            Buffer.BlockCopy(first, 0, buffer, 0, first.Length);
            buffer[buffer.Length - 1] = second;
            return buffer;
        }

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> first, T second)
        {
            foreach (var item in first)
                yield return item;

            yield return second;
        }

        // ToHexNumberString    prints out hex bytes in reverse order, as they are internally little-endian but big-endian is what people use:
        //                      bytes 0xEE,0xFF would represent what a person would write down as 0xFFEE

        // ToHexNumberData      prints out hex bytes in order

        public static string ToHexNumberString(this byte[] value)
        {
            return Bits.ToString(value.Reverse().ToArray()).Replace("-", "").ToLower();
        }

        public static string ToHexNumberString(this UInt256 value)
        {
            return ToHexNumberString(value.ToByteArray());
        }

        public static string ToHexNumberString(this BigInteger value)
        {
            return ToHexNumberString(value.ToByteArray());
        }

        public static string ToHexDataString(this byte[] value)
        {
            return $"[{Bits.ToString(value).Replace("-", ",").ToLower()}]";
        }

        public static string ToHexDataString(this UInt256 value)
        {
            return ToHexDataString(value.ToByteArray());
        }

        public static string ToHexDataString(this BigInteger value)
        {
            return ToHexDataString(value.ToByteArray());
        }

        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static UInt32 ToUnixTime(this DateTime value)
        {
            return (UInt32)((value - unixEpoch).TotalSeconds);
        }

        public static DateTime UnixTimeToDateTime(this UInt32 value)
        {
            return unixEpoch.AddSeconds(value);
        }

        [DebuggerStepThrough]
        public static void Do(this SemaphoreSlim semaphore, Action action)
        {
            semaphore.Wait();
            try
            {
                action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        [DebuggerStepThrough]
        public static T Do<T>(this SemaphoreSlim semaphore, Func<T> func)
        {
            semaphore.Wait();
            try
            {
                return func();
            }
            finally
            {
                semaphore.Release();
            }
        }

        [DebuggerStepThrough]
        public async static Task DoAsync(this SemaphoreSlim semaphore, Action action)
        {
            await semaphore.WaitAsync();
            try
            {
                action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        [DebuggerStepThrough]
        public async static Task DoAsync(this SemaphoreSlim semaphore, Func<Task> action)
        {
            await semaphore.WaitAsync();
            try
            {
                await action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static int ToIntChecked(this UInt32 value)
        {
            checked
            {
                return (int)value;
            }
        }

        public static int ToIntChecked(this UInt64 value)
        {
            checked
            {
                return (int)value;
            }
        }

        public static int ToIntChecked(this long value)
        {
            checked
            {
                return (int)value;
            }
        }

        public static uint ToUIntChecked(this int value)
        {
            checked
            {
                return (uint)value;
            }
        }

        public static byte[] NextBytes(this Random random, long length)
        {
            var buffer = (byte[])Array.CreateInstance(typeof(byte), length);
            random.NextBytes(buffer);
            return buffer;
        }

        public static void Forget(this Task task)
        {
        }

        [DebuggerStepThrough]
        public static void DoRead(this ReaderWriterLockSlim rwLock, Action action)
        {
            rwLock.EnterReadLock();
            try
            {
                action();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        [DebuggerStepThrough]
        public static T DoRead<T>(this ReaderWriterLockSlim rwLock, Func<T> func)
        {
            rwLock.EnterReadLock();
            try
            {
                return func();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        [DebuggerStepThrough]
        public static void DoWrite(this ReaderWriterLockSlim rwLock, Action action)
        {
            rwLock.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        [DebuggerStepThrough]
        public static T DoWrite<T>(this ReaderWriterLockSlim rwLock, Func<T> func)
        {
            rwLock.EnterWriteLock();
            try
            {
                return func();
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static int THOUSAND(this int value)
        {
            return value * 1000;
        }

        public static int MILLION(this int value)
        {
            return value * 1000 * 1000;
        }

        public static long BILLION(this int value)
        {
            return (long)value * 1000 * 1000 * 1000;
        }

        public static long THOUSAND(this long value)
        {
            return value * 1000;
        }

        public static long MILLION(this long value)
        {
            return value * 1000 * 1000;
        }

        public static long BILLION(this long value)
        {
            return value * 1000 * 1000 * 1000;
        }

        public static decimal THOUSAND(this decimal value)
        {
            return value * 1000;
        }

        public static decimal MILLION(this decimal value)
        {
            return value * 1000 * 1000;
        }

        public static decimal BILLION(this decimal value)
        {
            return value * 1000 * 1000 * 1000;
        }

        public static void DisposeList(this IEnumerable<IDisposable> disposables)
        {
            foreach (var item in disposables.Where(x => x != null))
                item.Dispose();
        }

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> keyPairs)
        {
            return keyPairs.ToDictionary(x => x.Key, x => x.Value);
        }

        public static List<T> SafeToList<T>(this ICollection<T> collection)
        {
            var list = new List<T>(collection.Count);
            foreach (var item in collection)
                list.Add(item);

            return list;
        }

        public static byte[] HexToByteArray(this string value)
        {
            if (value.Length % 2 != 0)
                throw new ArgumentOutOfRangeException();

            var bytes = new byte[value.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Byte.Parse(value.Substring(i * 2, 2), NumberStyles.HexNumber);
            }

            return bytes;
        }

        public static UInt32 NextUInt32(this Random random)
        {
            return (UInt32)random.Next(int.MinValue, int.MaxValue);
        }

        public static UInt64 NextUInt64(this Random random)
        {
            return (random.NextUInt32() << 32) + random.NextUInt32();
        }

        public static UInt256 NextUInt256(this Random random)
        {
            return new UInt256(
                (new BigInteger(random.NextUInt32()) << 96) +
                (new BigInteger(random.NextUInt32()) << 64) +
                (new BigInteger(random.NextUInt32()) << 32) +
                new BigInteger(random.NextUInt32()));
        }

        public static BigInteger NextUBigIntegerBytes(this Random random, int byteCount)
        {
            var bytes = random.NextBytes(byteCount).Concat(new byte[1]);
            return new BigInteger(bytes);
        }

        public static bool NextBool(this Random random)
        {
            return random.Next(2) == 0;
        }
        /*
        public static ImmutableBitArray ToImmutableBitArray(this BitArray bitArray)
        {
            return new ImmutableBitArray(bitArray);
        }
        */
        public static void RemoveWhere<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            foreach (var item in dictionary)
            {
                if (predicate(item))
                {
                    TValue ignore;
                    dictionary.TryRemove(item.Key, out ignore);
                }
            }
        }

        public static IEnumerable<KeyValuePair<TKey, TValue>> TakeAndRemoveWhere<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            foreach (var item in dictionary)
            {
                if (predicate(item))
                {
                    TValue value;
                    if (dictionary.TryRemove(item.Key, out value))
                    {
                        yield return new KeyValuePair<TKey, TValue>(item.Key, value);
                    }
                }
            }
        }
        /*
       public static bool TryAdd<TKey, TValue>(this ImmutableDictionary<TKey, TValue>.Builder dict, TKey key, TValue value)
       {
           try
           {
               dict.Add(key, value);
               return true;
           }
           catch (ArgumentException)
           {
               return false;
           }
       }

       public static bool TryAdd<TKey, TValue>(this ImmutableSortedDictionary<TKey, TValue>.Builder dict, TKey key, TValue value)
       {
           try
           {
               dict.Add(key, value);
               return true;
           }
           catch (ArgumentException)
           {
               return false;
           }
       }
       */
        public static double NextDouble(this Random random, double minValue, double maxValue)
        {
            if (maxValue < minValue)
                throw new ArgumentException();

            var range = maxValue - minValue;
            return (random.NextDouble() * range) + minValue;
        }

        public static ulong Sum<T>(this IEnumerable<T> source, Func<T, ulong> selector)
        {
            checked
            {
                var sum = 0UL;
                foreach (var item in source)
                    sum += selector(item);

                return sum;
            }
        }

        public static BigInteger Sum<T>(this IEnumerable<T> source, Func<T, BigInteger> selector)
        {
            checked
            {
                var sum = new BigInteger();
                foreach (var item in source)
                    sum += selector(item);

                return sum;
            }
        }

        public static IEnumerable<TResult> DequeueSelect<T, TResult>(this ConcurrentQueue<T> queue, Func<T, TResult> selector)
        {
            T result;
            while (queue.TryDequeue(out result))
                yield return selector(result);
        }

        [DebuggerStepThrough]
        public static void Time(this Stopwatch stopwatch, Action action)
        {
            if (stopwatch.IsRunning)
                throw new InvalidOperationException();

            stopwatch.Start();
            try
            {
                action();
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public static IEnumerable<T> UsingAsEnumerable<T>(this IEnumerator<T> enumerator)
        {
            using (enumerator)
            {
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
            }
        }
    }
}
