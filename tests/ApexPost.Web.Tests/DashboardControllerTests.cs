using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Controllers;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace ApexPost.Web.Tests;

public class DashboardControllerTests
{
    [Fact]
    public void IndexReturnsDashboardModelWithoutExposingStorageConfiguration()
    {
        var controller = new DashboardController(new TestEnvironment(), new JsonSeedOptions { Enabled = true });

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<DashboardViewModel>(result.Model);

        Assert.Equal("Test", model.EnvironmentName);
        Assert.True(model.DemonstrationDataEnabled);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ApexPost.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
