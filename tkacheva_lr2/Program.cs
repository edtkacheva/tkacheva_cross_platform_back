using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка строки подключения к базе данных
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=users.db"));

// Регистрация сервисов в DI
builder.Services.AddScoped<RSSFeedService>();  // Сервис для работы с RSS
builder.Services.AddScoped<RSSChannelService>();  // Сервис для работы с каналами
builder.Services.AddScoped<UserArticleState>();  // Сущность для отслеживания состояния статей
builder.Services.AddScoped<UserService>();  // Пример дополнительного сервиса (если есть)
builder.Services.AddScoped<ArticleService>();  // Пример дополнительного сервиса (если есть)
builder.Services.AddScoped<AuthService>();  // Пример дополнительного сервиса для авторизации

// Настройка HTTP клиента для работы с RSS
builder.Services.AddHttpClient<RSSFeedService>();

// Настройка контроллеров
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
    });

// Настройка Swagger для документации API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка JWT авторизации
var jwtKey = builder.Configuration["JwtKey"] ?? "super_puper_duper_secret_key_12345678901234567890";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Настройка CORS для работы с фронтендом (если необходимо)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Добавление авторизации и аутентификации
builder.Services.AddAuthorization();

// Старт приложения
var app = builder.Build();

// Среда разработки, включение Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Маршруты контроллеров
app.MapControllers();

// Настройка CORS
app.UseCors("AllowAngular");

app.Run();