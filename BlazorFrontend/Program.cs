using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using BlazorFrontend;
using BlazorFrontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Gateway API HttpClient — aponta para o YARP na porta 5000
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["GatewayUrl"] ?? "http://localhost:5000")
});

builder.Services.AddScoped<GatewayApiService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddBlazoredLocalStorage();

await builder.Build().RunAsync();
