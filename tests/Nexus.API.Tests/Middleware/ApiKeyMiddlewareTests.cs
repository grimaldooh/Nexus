using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Nexus.API.Middleware;
using Xunit;

namespace Nexus.API.Tests.Middleware;

public class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SwaggerRequest_SkipsApiKeyCheck()
    {
        var called = false;
        var configuration = new ConfigurationBuilder().Build();
        var middleware = new ApiKeyMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, configuration);

        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger/index.html";

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingConfig_Returns500()
    {
        var configuration = new ConfigurationBuilder().Build();
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, configuration);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_InvalidKey_Returns401()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = "valid-key"
            })
            .Build();

        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, configuration);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Api-Key"] = "invalid-key";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_ValidKey_CallsNext()
    {
        var called = false;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = "valid-key"
            })
            .Build();

        var middleware = new ApiKeyMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, configuration);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Request.Headers["X-Api-Key"] = "valid-key";

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }
}
