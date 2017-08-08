﻿using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace WebOptimizer.Test
{
    public class AssetMiddlewareTest
    {
        [Fact2]
        public async Task AssetMiddleware_NoCache()
        {
            string cssContent = "*{color:red}";

            var pipeline = new AssetPipeline();
            var asset = new Mock<IAsset>().SetupAllProperties();
            asset.SetupGet(a => a.ContentType).Returns("text/css");
            asset.SetupGet(a => a.Route).Returns("/file.css");
            asset.Setup(a => a.ExecuteAsync(It.IsAny<HttpContext>()))
                 .Returns(Task.FromResult(cssContent.AsByteArray()));

            StringValues values;
            var response = new Mock<HttpResponse>().SetupAllProperties();
            var context = new Mock<HttpContext>().SetupAllProperties();
            context.Setup(s => s.Request.Headers.TryGetValue("Accept-Encoding", out values))
                   .Returns(false);
            context.Setup(c => c.Response)
                   .Returns(response.Object);

            context.Setup(c => c.Request.Path).Returns("/file.css");

            var next = new Mock<RequestDelegate>();
            var env = new HostingEnvironment();
            var cache = new Mock<IMemoryCache>();

            var member = pipeline.GetType().GetField("_assets", BindingFlags.NonPublic | BindingFlags.Instance);
            member.SetValue(pipeline, new List<IAsset> { asset.Object });

            var options = new AssetMiddlewareOptions(env) { EnableCaching = false };
            var middleware = new AssetMiddleware(next.Object, env, cache.Object, pipeline, options);
            var stream = new MemoryStream();

            response.Setup(r => r.Body).Returns(stream);
            await middleware.InvokeAsync(context.Object);

            Assert.Equal("text/css", context.Object.Response.ContentType);
            Assert.Equal(cssContent.AsByteArray(), await stream.AsBytesAsync());
            Assert.Equal(0, response.Object.StatusCode);
        }

        [Fact2]
        public async Task AssetMiddleware_Cache()
        {
            var cssContent = "*{color:red}".AsByteArray();

            var pipeline = new AssetPipeline();
            var asset = new Mock<IAsset>().SetupAllProperties();
            asset.SetupGet(a => a.ContentType).Returns("text/css");
            asset.SetupGet(a => a.Route).Returns("/file.css");
            asset.Setup(a => a.ExecuteAsync(It.IsAny<HttpContext>()))
                 .Returns(Task.FromResult(cssContent));

            StringValues values;
            var response = new Mock<HttpResponse>().SetupAllProperties();
            var context = new Mock<HttpContext>().SetupAllProperties();
            context.Setup(s => s.Request.Headers.TryGetValue("Accept-Encoding", out values))
                   .Returns(false);
            context.Setup(c => c.Response)
                   .Returns(response.Object);

            context.Setup(c => c.Request.Path).Returns("/file.css");

            var next = new Mock<RequestDelegate>();
            var env = new HostingEnvironment();
            var cache = new Mock<IMemoryCache>();

            object bytes = cssContent;
            cache.Setup(c => c.TryGetValue(It.IsAny<string>(), out bytes))
                 .Returns(true);

            var member = pipeline.GetType().GetField("_assets", BindingFlags.NonPublic | BindingFlags.Instance);
            member.SetValue(pipeline, new List<IAsset> { asset.Object });

            var options = new AssetMiddlewareOptions(env) { EnableCaching = false };
            var middleware = new AssetMiddleware(next.Object, env, cache.Object, pipeline, options);
            var stream = new MemoryStream();

            response.Setup(r => r.Body).Returns(stream);
            await middleware.InvokeAsync(context.Object);

            Assert.Equal("text/css", context.Object.Response.ContentType);
            Assert.Equal(cssContent, await stream.AsBytesAsync());
            Assert.Equal(0, response.Object.StatusCode);
        }

        [Fact2]
        public async Task AssetMiddleware_Conditional()
        {
            var cssContent = "*{color:red}".AsByteArray();

            var pipeline = new AssetPipeline();
            var asset = new Mock<IAsset>().SetupAllProperties();
            asset.SetupGet(a => a.ContentType).Returns("text/css");
            asset.SetupGet(a => a.Route).Returns("/file.css");
            asset.Setup(a => a.ExecuteAsync(It.IsAny<HttpContext>()))
                 .Returns(Task.FromResult(cssContent));
            asset.Setup(a => a.GenerateCacheKey(It.IsAny<HttpContext>())).Returns("etag");

            StringValues values = "etag";
            var response = new Mock<HttpResponse>().SetupAllProperties();
            var context = new Mock<HttpContext>().SetupAllProperties();
            context.Setup(s => s.Request.Headers.TryGetValue("If-None-Match", out values))
                   .Returns(true);
            context.Setup(c => c.Response)
                   .Returns(response.Object);

            context.Setup(c => c.Request.Path).Returns("/file.css");

            var next = new Mock<RequestDelegate>();
            var env = new HostingEnvironment();
            var cache = new Mock<IMemoryCache>();

            object bytes = cssContent;
            cache.Setup(c => c.TryGetValue(It.IsAny<string>(), out bytes))
                 .Returns(true);

            var member = pipeline.GetType().GetField("_assets", BindingFlags.NonPublic | BindingFlags.Instance);
            member.SetValue(pipeline, new List<IAsset> { asset.Object });

            var options = new AssetMiddlewareOptions(env) { EnableCaching = false };
            var middleware = new AssetMiddleware(next.Object, env, cache.Object, pipeline, options);
            var stream = new MemoryStream();

            response.Setup(r => r.Body).Returns(stream);
            await middleware.InvokeAsync(context.Object);

            Assert.Equal("text/css", context.Object.Response.ContentType);
            Assert.Equal(0, stream.Length);
            Assert.Equal(304, response.Object.StatusCode);
        }
    }
}