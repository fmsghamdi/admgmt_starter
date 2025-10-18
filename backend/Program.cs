using admgmt_backend.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ==========================
// 🔹 الإعدادات العامة
// ==========================
var config = builder.Configuration;

// السماح بعدة عناوين Frontend (مثلاً 5173 و 5174)
var frontOrigins = config.GetSection("Frontend:Origins").Get<string[]>()
                  ?? new[] { config["Frontend:Origin"] ?? "http://localhost:5174" };

// ==========================
// 🔹 إضافة الخدمات (Dependency Injection)
// ==========================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // نخلي الإخراج بصيغة camelCase عشان يطابق أسماء الخصائص في الواجهة
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Swagger (للتجربة أثناء التطوير)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================
// 🔹 إعداد سياسة CORS
// ==========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins(frontOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ==========================
// 🔹 تسجيل الخدمات
// ==========================
builder.Services.AddSingleton<IPasswordPolicyService, PasswordPolicyService>();
builder.Services.AddScoped<IADService, ADService>();

// (ممكن لاحقًا تضيف مصادقة JWT هنا)
// builder.Services.AddAuthentication(...);
// builder.Services.AddAuthorization();

var app = builder.Build();

// ==========================
// 🔹 Middlewares
// ==========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// تفعيل سياسة الـ CORS
app.UseCors("frontend");

// app.UseAuthentication();
app.UseAuthorization();

// ==========================
// 🔹 خرائط الـ API
// ==========================
app.MapControllers();

// Endpoint صحي للتأكد أن الخدمة تعمل
app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    env = app.Environment.EnvironmentName
}));

// ==========================
// 🔹 تشغيل التطبيق
// ==========================
app.Run();
