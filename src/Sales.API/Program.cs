using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sales.API.Data;
using Sales.API.Messaging;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!);

builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidIssuer = configuration["Jwt:Issuer"],
        ValidAudience = configuration["Jwt:Audience"],
    };
});

builder.Services.AddHttpClient("StockService", client =>
{
    var baseUrl = configuration.GetValue<string>("StockService:BaseUrl");

    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSingleton<IRabbitMQSalesPublisher, RabbitMQSalesPublisher>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
