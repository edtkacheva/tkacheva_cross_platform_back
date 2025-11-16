using tkacheva_lr2;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=users.db"));

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapControllers();
app.MapDefaultControllerRoute();

app.Run();
