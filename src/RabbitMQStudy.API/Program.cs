using Microsoft.OpenApi.Models;
using RabbitMQStudy.Application.Commands;
using RabbitMQStudy.Infrastructure.Config;
using RabbitMQStudy.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// No in�cio do Program.cs, ap�s definir o builder
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddDebug();
    config.SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Program");

logger.LogInformation("Iniciando aplica��o...");
logger.LogInformation("Configurando servi�os...");

// Ap�s configurar o RabbitMQ
logger.LogInformation("Configura��o do RabbitMQ: {Host}:{Port}",
    builder.Configuration["RabbitMQ:Host"],
    builder.Configuration["RabbitMQ:Port"]);

// Antes de construir a aplica��o
logger.LogInformation("Construindo aplica��o...");

// Ap�s construir a aplica��o
logger.LogInformation("Aplica��o constru�da com sucesso");

builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));

// Adicionar servi�os ao container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RabbitMQ Study API", Version = "v1" });
});

// Adicionar MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly));

// Adicionar servi�os de infraestrutura (RabbitMQ, etc.)
builder.Services.AddInfrastructure();

// Configurar logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configurar o pipeline de requisi��es HTTP
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RabbitMQ Study API v1"));

// Middleware para capturar exce��es n�o tratadas
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            context.Response.StatusCode,
            Message = "Erro interno no servidor"
        });
    });
});

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Iniciar a aplica��o
app.Run();