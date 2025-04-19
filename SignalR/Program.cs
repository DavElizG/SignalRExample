using DataAcces.Data;
using Microsoft.EntityFrameworkCore;
using Services.Hubs;
using Services.Interfaces;
using Services.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Configurar CORS con origen desde variable de entorno
var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")?.Split(',') 
    ?? new string[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder => policyBuilder
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Obtener la cadena de conexión de la variable de entorno o del archivo de configuración
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"Usando la cadena de conexión: {connectionString}");

// Configurar MySQL con reintentos
builder.Services.AddDbContext<ChatMessagesContext>((provider, options) => {
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }
    );
});

builder.Services.AddScoped<IChatService, ChatService>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Habilitar Swagger según la variable de entorno
bool enableSwagger = true;
if (bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), out bool swaggerEnabled))
{
    enableSwagger = swaggerEnabled;
}

if (enableSwagger)
{
    builder.Services.AddSwaggerGen();
}

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? app.Environment.EnvironmentName;
if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase) && enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Solo redirigir a HTTPS si no estamos en un contenedor Docker
if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

// Usar CORS
app.UseCors("AllowAll");

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Aplicar migraciones al inicio si estamos en entorno Development o si se especifica en una variable
var shouldMigrate = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
if (bool.TryParse(Environment.GetEnvironmentVariable("APPLY_MIGRATIONS"), out bool applyMigrations))
{
    shouldMigrate = applyMigrations;
}

if (shouldMigrate)
{
    int maxRetryAttempts = 5;
    TimeSpan delay = TimeSpan.FromSeconds(5);

    for (int retryAttempt = 1; retryAttempt <= maxRetryAttempts; retryAttempt++)
    {
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ChatMessagesContext>();
                Console.WriteLine($"Intentando conectar a la base de datos (intento {retryAttempt}/{maxRetryAttempts})...");
                db.Database.OpenConnection();
                Console.WriteLine("Conexión establecida con éxito.");
                db.Database.CloseConnection();
                
                Console.WriteLine("Aplicando migraciones...");
                db.Database.Migrate();
                Console.WriteLine("Database migrations applied successfully.");
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al conectar a la base de datos (intento {retryAttempt}/{maxRetryAttempts}): {ex.Message}");
            
            if (retryAttempt == maxRetryAttempts)
            {
                Console.WriteLine($"No se pudo conectar a la base de datos después de {maxRetryAttempts} intentos.");
                Console.WriteLine($"La aplicación continuará ejecutándose, pero las funciones que requieren base de datos podrían fallar.");
            }
            else
            {
                Console.WriteLine($"Reintentando en {delay.TotalSeconds} segundos...");
                Thread.Sleep(delay);
            }
        }
    }
}

app.Run();
