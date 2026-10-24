using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHealthChecks();

var dataOptions = new JsonDataOptions();
builder.Configuration.GetSection(JsonDataOptions.SectionName).Bind(dataOptions);
var seedOptions = new JsonSeedOptions();
builder.Configuration.GetSection("ApexPost:JsonSeed").Bind(seedOptions);
builder.Services.AddSingleton(dataOptions);
builder.Services.AddSingleton(seedOptions);
builder.Services.AddSingleton<JsonSeedInitializer>();
builder.Services.AddSingleton<JsonAuditWriter>(serviceProvider => new JsonAuditWriter(serviceProvider.GetRequiredService<JsonDataOptions>().DataDirectory));
builder.Services.AddHostedService<JsonInitializationHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
