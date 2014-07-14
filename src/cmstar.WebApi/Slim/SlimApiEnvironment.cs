﻿namespace cmstar.WebApi.Slim
{
    /// <summary>
    /// 包含SlimWebAPI相关的全局常量和静态成员的定义。
    /// </summary>
    public static class SlimApiEnvironment
    {
        public static string MetaParamMethodName = "~method";
        public static string MetaParamFormat = "~format";
        public static string MetaParamCallback = "~callback";

        public const string MetaRequestFormatJson = "json";
        public const string MetaRequestFormatPost = "post";
        public const string MetaRequestFormatGet = "get";
    }
}
