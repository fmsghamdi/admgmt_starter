using admgmt_backend.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Controllers + CORS
builder.Services.AddControllers();
builder.Services.AddCors(p => p.AddDefaultPolicy(x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Bind AD options from appsettings.json
builder.Services.Configure<AdOptions>(builder.Configuration.GetSection("AD"));

// DI
builder.Services.AddSingleton<PowerShellRunner>();
builder.Services.AddScoped<IADService, PowerShellAdService>();

var app = builder.Build();

// Dev exception page + simple error logging
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERR] {ex}");
        throw;
    }
});

app.UseCors();
app.MapControllers();
app.Run();
