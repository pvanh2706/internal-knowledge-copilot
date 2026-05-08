using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Xunit;

namespace InternalKnowledgeCopilot.Tests;

public sealed class HealthConfigurationTests
{
    [Fact]
    public void StorageOptions_UseExpectedDefaults()
    {
        var options = new AppStorageOptions();

        Assert.Equal("./storage", options.RootPath);
        Assert.Equal(20 * 1024 * 1024, options.MaxUploadBytes);
        Assert.Contains(".pdf", options.AllowedExtensions);
    }

    [Fact]
    public void ChromaOptions_UseExpectedDefaults()
    {
        var options = new ChromaOptions();

        Assert.Equal("http://localhost:8000", options.BaseUrl);
        Assert.Equal("knowledge_chunks", options.Collection);
    }
}
