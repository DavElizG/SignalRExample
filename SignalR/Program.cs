using DataAcces.Data;
using Microsoft.EntityFrameworkCore;
using Services.Hubs;
using Services.Interfaces;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar conexión a base de datos
builder.Services.AddDbContext<ChatMessagesContext>(options =>
      options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IChatService, ChatService>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar SignalR
builder.Services.AddSignalR();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder => policyBuilder
            .WithOrigins("http://localhost:5173") // Permite que el frontend (en este origen) se conecte al backend usando SignalR
            .AllowAnyMethod()                     // Permite todos los métodos necesarios para SignalR
            .AllowAnyHeader()                     
            .AllowCredentials());                 
});

var app = builder.Build();

// Configuración del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("AllowAll"); 

app.MapControllers();
app.MapHub<ChatHub>("/chathub"); 

app.Run();