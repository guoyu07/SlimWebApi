﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace cmstar.WebApi
{
    public enum AbcItemType
    {
        ItemA,
        ItemB,
        ItemC
    }

    public class SimpleObject
    {
        public int Id;
        public string Name { get; set; }
        public double Number { get; set; }
        public DateTime DateTime;
        public Guid Guid { get; set; }
        public AbcItemType Abc { get; set; }
    }

    public class ComplexData
    {
        public SimpleObject SimpleObject { get; set; }
        public Dictionary<string, int> StringIntegerMap;
        public IList<Point> Points { get; set; }
    }

    public class SimpleServiceProvider
    {
        public static double Sum(IList<double> values)
        {
            return values == null ? 0 : values.Sum();
        }

        private readonly Random _random = new Random();
        private readonly Guid _guid = Guid.NewGuid();

        public string PropValue { get; set; }

        public Guid GetGuid()
        {
            return _guid;
        }

        public void DoNothingWith(DateTime date, DateTime? nullableDate)
        {
        }

        public int PlusRandom(int x, int y)
        {
            return x + y + Random(1000);
        }

        public void Error(int i)
        {
            throw new Exception("An error occured.");
        }

        public SimpleObject GetSelf(SimpleObject simpleObject)
        {
            return simpleObject;
        }

        public string InputStream(string head, Stream input, string tail)
        {
            var ms = new MemoryStream();
            var buf = new byte[1024];
            int len;
            while ((len = input.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, len);
            }

            var data = ms.ToArray();
            var s = Encoding.UTF8.GetString(data);
            return string.Concat(head, s, tail);
        }

        private int Random(int max)
        {
            return _random.Next(max);
        }
    }

    public abstract class AbstractServiceProvider
    {
        public static string Hello()
        {
            return "World!!";
        }
    }

    public class AttributedServiceProvider
    {
        private int _value;

        // 标记API方法，并没有开启缓存
        [ApiMethod]
        public int Zero()
        {
            return 0;
        }

        // 在特性上配置缓存超时并开启自动缓存
        [ApiMethod(AutoCacheEnabled = true, CacheExpiration = 3)]
        public DateTime Now()
        {
            return DateTime.Now;
        }

        // 使用ApiMethodContext手工管理缓存，以应对特殊的场景（比如API内同时有读写）
        [ApiMethod(CacheExpiration = 5)]
        public int ManualCache()
        {
            // 此方法使用ApiMethodContext手工管理缓存
            var value = ApiMethodContext.Current.GetCachedResult();
            if (value != null)
                return (int)value;

            _value++;
            ApiMethodContext.Current.SetCachedResult(_value);
            return _value;
        }

        [ApiMethod(CompressionMethods = ApiCompressionMethods.GZip)]
        public string ForceGzipString()
        {
            return Guid.NewGuid().ToString();
        }

        [ApiMethod(CompressionMethods = ApiCompressionMethods.Defalte)]
        public string ForceDeflateString()
        {
            return Guid.NewGuid().ToString();
        }

        [ApiMethod(CompressionMethods = ApiCompressionMethods.Auto)]
        public string AutoCompressionString()
        {
            return Guid.NewGuid().ToString();
        }
    }
}