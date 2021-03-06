﻿using System;
using System.Collections.Generic;

namespace cmstar.WebApi
{
    /// <summary>
    /// 包含一个Web API方法的设置信息。
    /// </summary>
    public class ApiMethodSetting
    {
        private ApiCompressionMethods _compressionMethods = ApiCompressionMethods.None;
        private TimeSpan _cacheExpiration = TimeSpan.Zero;
        private bool _autoCacheEnabled;
        private string _methodName;

        /// <summary>
        /// 获取或设置调用当前WebAPI所使用的名称。
        /// </summary>
        public string MethodName
        {
            get
            {
                return _methodName;
            }
            set
            {
                ArgAssert.NotNullOrEmptyOrWhitespace(value, "value");
                _methodName = value;
            }
        }

        /// <summary>
        /// 获取或设置当前WebAPI所用缓存的超时时间。若未开启缓存，则为<see cref="TimeSpan.Zero"/>。
        /// </summary>
        public TimeSpan CacheExpiration
        {
            get
            {
                return _cacheExpiration;
            }
            set
            {
                if (value.Ticks <= 0)
                    throw new ArgumentException("The expiration time must be greater than zero.", "value");

                _cacheExpiration = value;
            }
        }

        /// <summary>
        /// 指定对于此API方法使用何种压缩方式输出结果。
        /// 默认值为<see cref="ApiCompressionMethods.None"/>。
        /// </summary>
        /// <remarks>
        /// 压缩需要消耗计算资源，而且对于较小的数据的压缩并不会显著缩小体积（甚至可能更大），
        /// 需要评估API方法返回数据的体积加以评估以确定是否使用显式的压缩配置。
        /// </remarks>
        public ApiCompressionMethods CompressionMethods
        {
            get { return _compressionMethods; }
            set { _compressionMethods = value; }
        }

        /// <summary>
        /// 获取或设置当前WebAPI所使用的缓存提供器。
        /// </summary>
        public IApiCacheProvider CacheProvider { get; set; }

        /// <summary>
        /// 获取或设置当前WebAPI注册中使用的缓存命名空间。
        /// </summary>
        public string CacheNamespace { get; set; }

        /// <summary>
        /// 是否允许对当前方法进行自动缓存。
        /// </summary>
        public bool AutoCacheEnabled
        {
            get { return _autoCacheEnabled && CacheProvider != null; }
            set { _autoCacheEnabled = value; }
        }

        /// <summary>
        /// 获取或设置一个方法，在当前WebAPI被调用前执行此方法。
        /// 此方法的第一个参数指向当前被调用的WebAPI方法的注册信息；
        /// 第二个参数为当前被调用的WebAPI方法的参数表。
        /// </summary>
        public Action<ApiMethodInfo, IDictionary<string, object>> MethodInvoking { get; set; }

        /// <summary>
        /// 获取或设置一个方法，在当前WebAPI被调用后（即便调用期间出现异常）执行此方法。
        /// 此方法的第一个参数指向当前被调用的WebAPI方法的注册信息；
        /// 第二个参数为当前被调用的WebAPI方法的参数表；
        /// 第三个参数为当前被调用的WebAPI方法的返回值，若方法没有返回值，或调用期间出现异常，则为null；
        /// 第四个参数为WebAPI方法调用过程中的异常，若没有异常，则为null。
        /// </summary>
        public Action<ApiMethodInfo, IDictionary<string, object>, object, Exception> MethodInvoked { get; set; }
    }
}
