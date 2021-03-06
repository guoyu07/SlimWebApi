﻿using System;
using System.Reflection;
using cmstar.WebApi.Slim;

namespace cmstar.WebApi
{
    /// <summary>
    /// Example 4：
    /// 此示例演示使用setup.Auto和setup.FromType进行批量注册。
    /// </summary>
    public class AutoSetupExample : SlimApiHttpHandler
    {
        public override void Setup(ApiSetup setup)
        {
            // 设置缓存的基础配置
            setup.SetupCacheBase(new HttpRuntimeApiCacheProvider(), TimeSpan.FromSeconds(10));

            // 此方式等同于使用反射获取MethodInfo并批量注册，写法上更为便利
            setup.Auto(new SimpleServiceProvider(), false, BindingFlags.Public | BindingFlags.Static);

            // 注册利用ApiMethodAttribute标记的方法，该特性上已经定义了方法注册配置的相关信息
            setup.Auto(new AttributedServiceProvider());

            // 注册抽象类的静态方法
            setup.FromType(typeof(AbstractServiceProvider), false);
        }
    }
}