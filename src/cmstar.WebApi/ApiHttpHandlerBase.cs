﻿using System;
using System.Collections.Generic;
using System.Web;
using Common.Logging;

namespace cmstar.WebApi
{
    /// <summary>
    /// <see cref="IHttpHandler"/>的实现，包含了基本的API处理流程。这是一个抽象类。
    /// </summary>
    public abstract class ApiHttpHandlerBase : IHttpHandler
    {
        private static readonly Dictionary<Type, ApiHandlerState> HandlerStates
            = new Dictionary<Type, ApiHandlerState>();

        private ILog _log;

        public void ProcessRequest(HttpContext context)
        {
            var httpResponse = context.Response;

            httpResponse.AddHeader("Pragma", "No-Cache");
            httpResponse.Expires = 0;
            httpResponse.CacheControl = "no-cache";

            var handlerState = GetCurrentTypeHandler();
            var requestState = CreateRequestState(context, handlerState);

            try
            {
                PerformProcessRequest(context, handlerState, requestState);
            }
            catch (Exception ex)
            {
                Logger.Fatal("Can not process the request.", ex);
                throw;
            }
        }

        public virtual bool IsReusable
        {
            get { return true; }
        }

        /// <summary>
        /// 对WebAPI进行注册和配置。
        /// </summary>
        /// <param name="setup">提供用于Web API注册与配置的方法。</param>
        public abstract void Setup(ApiSetup setup);

        /// <summary>
        /// 获取指定的API方法所对应的参数解析器。
        /// </summary>
        /// <param name="method">包含API方法的有关信息。</param>
        /// <returns>key为解析器的名称，value为解析器实例。</returns>
        protected abstract IDictionary<string, IRequestDecoder> ResolveDecoders(ApiMethodInfo method);

        /// <summary>
        /// 创建用于保存当前API请求信息的对象实例。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="handlerState"><see cref="ApiHandlerState"/>的实例。</param>
        /// <returns>用于保存当前API请求信息的对象实例。</returns>
        protected abstract object CreateRequestState(
            HttpContext context, ApiHandlerState handlerState);

        /// <summary>
        /// 获取当前API请求所管理的方法名称。
        /// 若未能获取到名称，返回null。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <returns>方法名称。</returns>
        protected abstract string RetriveRequestMethodName(
            HttpContext context, object requestState);

        /// <summary>
        /// 获取当前调用的API方法所使用的参数解析器的名称。
        /// 若未能获取到名称，返回null。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <returns>调用的API方法所使用的参数解析器的名称。</returns>
        protected abstract string RetrieveRequestDecoderName(
            HttpContext context, object requestState);

        /// <summary>
        /// 创建当前调用的API方法所需要的参数值集合。
        /// 集合以参数名称为key，参数的值为value。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <param name="decoder">用于API参数解析的<see cref="IRequestDecoder"/>实例。</param>
        /// <returns>记录参数名称和对应的值。</returns>
        protected abstract IDictionary<string, object> DecodeParam(
            HttpContext context, object requestState, IRequestDecoder decoder);

        /// <summary>
        /// 将指定的<see cref="ApiResponse"/>序列化并写如HTTP输出流中。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <param name="apiResponse">用于表示API返回的数据。</param>
        protected abstract void WriteResponse(
            HttpContext context, object requestState, ApiResponse apiResponse);

        /// <summary>
        /// 获取当前请求的描述信息。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <param name="apiResponse">用于表示API返回的数据。</param>
        /// <returns>描述信息。</returns>
        protected abstract string GetRequestDescription(
            HttpContext context, object requestState, ApiResponse apiResponse);

        /// <summary>
        /// 当成功处理API方法调用后触发此方法。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <param name="apiResponse">用于表示API返回的数据。</param>
        protected virtual void OnSuccess(HttpContext context, object requestState, ApiResponse apiResponse)
        {
            WriteResponse(context, requestState, apiResponse);

            if (LogSuccessRequests && Logger.IsInfoEnabled)
            {
                var requestDescription = GetRequestDescription(context, requestState, apiResponse);
                Logger.Info(requestDescription);
            }
        }

        /// <summary>
        /// 当调用过程中出现未处理异常时触发此方法。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        /// <param name="exception">异常的实例。</param>
        protected virtual void OnError(HttpContext context, object requestState, Exception exception)
        {
            var apiException = exception as ApiException;
            var apiResponse = apiException == null
                ? new ApiResponse(500, "Internal error.")
                : new ApiResponse(apiException.Code, apiException.Description);

            WriteResponse(context, requestState, apiResponse);

            if (Logger.IsErrorEnabled)
            {
                var requestDescription = GetRequestDescription(context, requestState, apiResponse);
                Logger.Error(requestDescription, exception);
            }
        }

        /// <summary>
        /// 当本次API访问中未指定访问的方法名称或名称错误时触发此方法。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        protected virtual void OnMethodNotFound(HttpContext context, object requestState)
        {
            const int code = 400;
            const string msg = "Bad entry.";

            var apiResponse = new ApiResponse(code, msg);
            WriteResponse(context, requestState, apiResponse);

            if (Logger.IsWarnEnabled)
            {
                var requestDescription = GetRequestDescription(context, requestState, apiResponse);
                Logger.Warn(requestDescription);
            }
        }

        /// <summary>
        /// 当本次API访问中指定访问的方法所关联的参数解析器名称不存在时触发此方法。
        /// </summary>
        /// <param name="context">当前请求的<see cref="HttpContext"/>实例。</param>
        /// <param name="requestState">用于保存当前API请求信息的对象实例。</param>
        protected virtual void OnDecoderNotFound(HttpContext context, object requestState)
        {
            const int code = 400;
            const string msg = "Unsupported format.";

            var apiResponse = new ApiResponse(code, msg);
            WriteResponse(context, requestState, apiResponse);

            if (Logger.IsWarnEnabled)
            {
                var requestDescription = GetRequestDescription(context, requestState, apiResponse);
                Logger.Warn(requestDescription);
            }
        }

        /// <summary>
        /// 指定是否将处理成功的API请求写入日志。
        /// </summary>
        protected bool LogSuccessRequests
        {
            get { return true; }
        }

        /// <summary>
        /// 处理<see cref="ApiMethodInfo"/>的调用过程中出现的异常。
        /// </summary>
        /// <param name="ex"><see cref="ApiMethodInfo"/>的调用过程中出现的异常。</param>
        /// <returns>
        /// 返回一个<see cref="ApiResponse"/>实例以表示请求处理成功，后续进入<see cref="OnSuccess"/>方法；
        /// 返回null则继续异常处理流程，进入<see cref="OnError"/>方法。
        /// </returns>
        protected virtual ApiResponse TranslateMethodInvocationError(Exception ex)
        {
            return null;
        }

        /// <summary>
        /// 获取当前API使用的<see cref="ILog"/>实例。
        /// </summary>
        /// <returns>当前API使用的<see cref="ILog"/>实例。</returns>
        protected virtual ILog Logger
        {
            get { return _log ?? (_log = LogManager.GetLogger(GetType())); }
        }

        private void PerformProcessRequest(
            HttpContext context, ApiHandlerState handlerState, object requestState)
        {
            var methodInvocationStarted = false;

            try
            {
                var methodName = RetriveRequestMethodName(context, requestState);
                var method = handlerState.GetMethod(methodName);
                if (method == null)
                {
                    OnMethodNotFound(context, requestState);
                    return;
                }

                var decoderName = RetrieveRequestDecoderName(context, requestState);
                var decoder = handlerState.GetDecoder(methodName, decoderName);
                if (decoder == null)
                {
                    OnDecoderNotFound(context, requestState);
                    return;
                }

                var param = DecodeParam(context, requestState, decoder) ?? new Dictionary<string, object>(0);

                ApiMethodContext.Current = new ApiMethodContext
                {
                    Raw = context,
                    CacheProvider = method.CacheProvider,
                    CacheExpiration = method.CacheExpiration,
                    CacheKeyProvider = () => CacheKeyHelper.GetCacheKey(method, param)
                };

                object result;
                if (method.AutoCacheEnabled)
                {
                    var cacheProvider = method.CacheProvider;
                    var cacheKey = CacheKeyHelper.GetCacheKey(method, param);
                    result = cacheKey == null ? null : cacheProvider.Get(cacheKey);

                    if (result == null)
                    {
                        methodInvocationStarted = true;
                        result = method.Invoke(param);
                        cacheProvider.Add(cacheKey, result, method.CacheExpiration);
                    }
                }
                else
                {
                    methodInvocationStarted = true;
                    result = method.Invoke(param);
                }

                var apiResponse = new ApiResponse(result);
                OnSuccess(context, requestState, apiResponse);
            }
            catch (Exception ex)
            {
                var apiResponse = methodInvocationStarted ? TranslateMethodInvocationError(ex) : null;
                if (apiResponse == null)
                {
                    OnError(context, requestState, ex);
                }
                else
                {
                    OnSuccess(context, requestState, apiResponse);
                }
            }
        }

        private ApiHandlerState GetCurrentTypeHandler()
        {
            var type = GetType();

            ApiHandlerState handlerState;
            if (HandlerStates.TryGetValue(type, out handlerState))
                return handlerState;

            lock (HandlerStates)
            {
                if (HandlerStates.TryGetValue(type, out handlerState))
                    return handlerState;

                var setup = new ApiSetup(type);
                Setup(setup);

                handlerState = new ApiHandlerState();

                foreach (var apiMethodInfo in setup.ApiMethodInfos)
                {
                    var methodName = apiMethodInfo.MethodName;

                    // 由于函数可能有重载，名称是一样的，这里自动对方法名称进行改名
                    for (var i = 2; handlerState.GetMethod(methodName) != null; i++)
                    {
                        methodName = apiMethodInfo.MethodName + i;
                    }

                    handlerState.AddMethod(methodName, apiMethodInfo);

                    var decoderMap = ResolveDecoders(apiMethodInfo);
                    if (decoderMap != null)
                    {
                        foreach (var decoder in decoderMap)
                        {
                            handlerState.AddDecoder(methodName, decoder.Key, decoder.Value);
                        }
                    }
                }

                HandlerStates.Add(type, handlerState);
                return handlerState;
            }
        }
    }
}
