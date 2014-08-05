﻿using System;
using System.Collections.Generic;
using System.Web;
using cmstar.WebApi.Slim.ParamDecoders;
using Common.Logging;
using cmstar.Serialization.Json;

namespace cmstar.WebApi.Slim
{
    /// <summary>
    /// 包含Slim WebAPI中处理HTTP请求的具体流程。
    /// </summary>
    public class SlimApiInvocationHandler
    {
        private readonly Dictionary<string, ApiMethodInfo> _registeredMethods
            = new Dictionary<string, ApiMethodInfo>(ApiEnvironment.DefaultMethodNameComparer);

        private readonly Dictionary<string, DecoderBinding> _decoderMap
            = new Dictionary<string, DecoderBinding>(ApiEnvironment.DefaultMethodNameComparer);

        private readonly string _metaMethodName = SlimApiEnvironment.MetaParamMethodName;
        private readonly string _metaRequestFormat = SlimApiEnvironment.MetaParamFormat;
        private readonly string _metaCallback = SlimApiEnvironment.MetaParamCallback;

        protected readonly ILog Logger;

        /// <summary>
        /// 初始化<see cref="SlimApiInvocationHandler"/>的新实例。
        /// </summary>
        /// <param name="callerType">WebAPI的注册类型。通常是<see cref="SlimApiHttpHandler"/>的子类。</param>
        /// <param name="methods">
        /// API注册信息。可以为<c>null</c>，在之后通过<see cref="AddRegistry"/>方法添加API注册。
        /// </param>
        public SlimApiInvocationHandler(Type callerType, IEnumerable<ApiMethodInfo> methods)
        {
            if (methods != null)
            {
                foreach (var method in methods)
                {
                    AddRegistry(method);
                }
            }

            Logger = LogManager.GetLogger(callerType ?? GetType());
        }

        /// <summary>
        /// 添加一个WebAPI注册。
        /// </summary>
        /// <param name="apiMethodInfo">包含WebAPI的注册信息。</param>
        public void AddRegistry(ApiMethodInfo apiMethodInfo)
        {
            var methodName = apiMethodInfo.MethodName;

            // 由于函数可能有重载，名称是一样的，这里自动对方法名称进行改名
            for (var i = 2; _registeredMethods.ContainsKey(methodName); i++)
            {
                methodName = apiMethodInfo.MethodName + i;
            }

            _registeredMethods.Add(methodName, apiMethodInfo);

            var methodBinding = ResolveDefaultDecoderBinding(apiMethodInfo);
            _decoderMap.Add(methodName, methodBinding);
        }

        /// <summary>
        /// 处理HTTP请求。
        /// </summary>
        /// <param name="context"><see cref="HttpContext"/>。</param>
        public void ProcessRequest(HttpContext context)
        {
            var request = context.Request;

            var methodName = request.Params[_metaMethodName];
            var apiMethodInfo = ResolveMethodInfo(context, methodName);
            if (apiMethodInfo == null)
                return;

            var decoder = ResolveDecoder(context, methodName, request.Params[_metaRequestFormat]);
            if (decoder == null)
                return;

            var paramValueMap = DecodeParam(context, decoder);
            if (paramValueMap == null)
                return;

            try
            {
                var apiMethodContext = new ApiMethodContext();
                apiMethodContext.CacheProvider = apiMethodInfo.CacheProvider;
                apiMethodInfo.CacheExpiration = apiMethodInfo.CacheExpiration;
                apiMethodContext.CacheKeyProvider = () => CacheKeyHelper.GetCacheKey(apiMethodInfo, paramValueMap);
                ApiMethodContext.Current = apiMethodContext;

                object result;
                if (apiMethodInfo.AutoCacheEnabled)
                {
                    var cacheProvider = apiMethodInfo.CacheProvider;
                    var cacheKey = CacheKeyHelper.GetCacheKey(apiMethodInfo, paramValueMap);
                    result = cacheProvider.Get(cacheKey);

                    if (result == null)
                    {
                        result = apiMethodInfo.Invoke(paramValueMap);
                        cacheProvider.Add(cacheKey, result, apiMethodInfo.CacheExpiration);
                    }
                }
                else
                {
                    result = apiMethodInfo.Invoke(paramValueMap);
                }

                WriteResponse(context, 200, result);
            }
            catch (Exception ex)
            {
                WriteResponse(context, 500, null, 500, "Unhandled exception.", ex);
            }
        }

        private IDictionary<string, object> DecodeParam(HttpContext context, IRequestDecoder decoder)
        {
            try
            {
                var paramValueMap = decoder.DecodeParam(context.Request);
                return paramValueMap ?? new Dictionary<string, object>(0);
            }
            catch (Exception ex)
            {
                if (ex is JsonContractException || ex is JsonFormatException)
                {
                    WriteResponse(context, 400, null, 400, "Bad JSON.", ex);
                }
                else if (ex is InvalidCastException)
                {
                    WriteResponse(context, 400, null, 400, "Invalid parameter value.", ex);
                }
                else
                {
                    WriteResponse(context, 500, null, 500, "Unhandled exception.", ex);
                }

                return null;
            }
        }

        private ApiMethodInfo ResolveMethodInfo(HttpContext context, string methodName)
        {
            if (methodName == null)
            {
                WriteResponse(context, 400, null, 400, "Method name not specified.");
                return null;
            }

            ApiMethodInfo apiMethodInfo;
            if (!_registeredMethods.TryGetValue(methodName, out apiMethodInfo))
            {
                WriteResponse(context, 400, null, 400, "Method not found.");
                return null;
            }

            return apiMethodInfo;
        }

        private IRequestDecoder ResolveDecoder(HttpContext context, string methodName, string requestFormat)
        {
            if (requestFormat == null)
                return _decoderMap[methodName].DefaultDecoder;

            IRequestDecoder decoder;
            switch (requestFormat.ToLower())
            {
                case SlimApiEnvironment.MetaRequestFormatJson:
                    decoder = _decoderMap[methodName].JsonDecoder;
                    break;

                case SlimApiEnvironment.MetaRequestFormatGet:
                case SlimApiEnvironment.MetaRequestFormatPost:
                    decoder = _decoderMap[methodName].HttpParamDecoder;
                    break;

                default:
                    WriteResponse(context, 400, null, 400, "Unknow format.");
                    return null;
            }

            if (decoder == null)
                WriteResponse(context, 400, null, 400, "The format is not supported on the method.");

            return decoder;
        }

        private void WriteResponse(HttpContext context, int httpStatusCode,
            object responseData, int responseCode = 0, string responseMessage = "", Exception ex = null)
        {
            var httpResponse = context.Response;
            var callback = context.Request[_metaCallback];
            var isJsonp = !string.IsNullOrEmpty(callback);

            if (isJsonp)
            {
                httpResponse.ContentType = "text/javascript";
                httpResponse.Write(callback);
                httpResponse.Write("(");
            }
            else
            {
                httpResponse.ContentType = "application/json";
            }

            var responseObject = new SlimApiResponse<object>(responseCode, responseMessage, responseData);
            var json = JsonHelper.Serialize(responseObject);
            context.Response.Write(json);

            if (isJsonp)
            {
                context.Response.Write(")");
            }

            if (httpStatusCode != 200)
            {
                httpResponse.StatusCode = httpStatusCode;

                var httpRequest = context.Request;
                var requestDescription = string.Format("{0,-15} {1}",
                    httpRequest.UserHostAddress, httpRequest.RawUrl);

                if (httpStatusCode >= 400 && httpStatusCode < 500)
                {
                    Logger.Warn(requestDescription);
                    Logger.Warn(json, ex);
                }
                else if (httpStatusCode >= 500 && httpStatusCode < 600)
                {
                    Logger.Error(requestDescription);
                    Logger.Error(json, ex);
                }
            }
        }

        private DecoderBinding ResolveDefaultDecoderBinding(ApiMethodInfo apiMethodInfo)
        {
            var binding = new DecoderBinding();
            var param = apiMethodInfo.Method.GetParameters();

            if (param.Length == 0)
            {
                binding.HttpParamDecoder = EmptyParamMethodRequestDecoder.Instance;
                binding.JsonDecoder = EmptyParamMethodRequestDecoder.Instance;
                binding.DefaultDecoder = binding.JsonDecoder;
                return binding;
            }

            var paramInfoMap = apiMethodInfo.ParamInfoMap;

            if (TypeHelper.IsPlainMethod(apiMethodInfo.Method, true))
            {
                binding.HttpParamDecoder = new InlineParamHttpParamDecoder(paramInfoMap);
                binding.JsonDecoder = new InlineParamJsonDecoder(paramInfoMap);
                binding.DefaultDecoder = binding.HttpParamDecoder;
            }
            else if (param.Length == 1)
            {
                binding.JsonDecoder = new SingleObjectJsonDecoder(paramInfoMap);
                binding.DefaultDecoder = binding.JsonDecoder;

                var paramType = param[0].ParameterType;
                if (TypeHelper.IsPlainType(paramType, true))
                {
                    binding.HttpParamDecoder = new SingleObjectHttpParamDecoder(paramInfoMap);
                }
            }
            else
            {
                binding.JsonDecoder = new InlineParamJsonDecoder(paramInfoMap);
                binding.DefaultDecoder = binding.JsonDecoder;
            }

            return binding;
        }

        private class DecoderBinding
        {
            public IRequestDecoder HttpParamDecoder;
            public IRequestDecoder JsonDecoder;
            public IRequestDecoder DefaultDecoder;
        }
    }
}
