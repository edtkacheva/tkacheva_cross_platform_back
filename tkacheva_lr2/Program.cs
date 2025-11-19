using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using tkacheva_lr2.Data;

var builder = WebApplication.CreateBuilder(args);

// ====== Добавляем DbContext ======
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=users.db"));


// ====== Настройка контроллеров + JSON (чтобы избежать циклов) ======
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
    });


// ====== Настройка Swagger ======
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ====== JWT Авторизация ======
var jwtKey = builder.Configuration["JwtKey"] ?? "super_puper_duper_secret_key_12345678901234567890";

// Добавляем Authentication + JWT
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

// Авторизация (роли + правила)
builder.Services.AddAuthorization();


// ====== Старт приложения ======

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// важно! авторизация должна идти после UseRouting
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
