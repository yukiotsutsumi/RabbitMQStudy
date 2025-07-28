using Microsoft.OpenApi.Models;
using RabbitMQStudy.Application.Commands;
using RabbitMQStudy.Infrastructure.Config;
using RabbitMQStudy.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// No início do Program.cs, após definir o builder
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddDebug();
    config.SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Program");

logger.LogInformation("Iniciando aplicação...");
logger.LogInformation("Configurando serviços...");

// Após configurar o RabbitMQ
logger.LogInformation("Configuração do RabbitMQ: {Host}:{Port}",
    builder.Configuration["RabbitMQ:Host"],
    builder.Configuration["RabbitMQ:Port"]);

// Antes de construir a aplicação
logger.LogInformation("Construindo aplicação...");

// Após construir a aplicação
logger.LogInformation("Aplicação construída com sucesso");

builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));

// Adicionar serviços ao container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RabbitMQ Study API", Version = "v1" });
});

// Adicionar MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly));

// Adicionar serviços de infraestrutura (RabbitMQ, etc.)
builder.Services.AddInfrastructure();

// Configurar logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configurar o pipeline de requisições HTTP
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RabbitMQ Study API v1"));

// Middleware para capturar exceções não tratadas
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

// Iniciar a aplicação
app.Run();