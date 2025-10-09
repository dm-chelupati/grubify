using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure forwarded headers for Azure Container Apps
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Add Azure Container Apps proxy network (100.100.0.0/16)
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
        System.Net.IPAddress.Parse("100.100.0.0"), 16));
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                ?? new[] { 
                    "http://localhost:3000", 
                    "https://localhost:3000",
                    "https://ca-frontend-ytie5k5kadz6k.kindbush-1a2ac456.eastus2.azurecontainerapps.io"
                };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Use forwarded headers for Azure Container Apps
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Remove UseHttpsRedirection for Azure Container Apps - ACA handles TLS termination
// app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowReactApp");

app.UseAuthorization();

app.MapControllers();

app.Run();
