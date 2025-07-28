using MediatR;
using Microsoft.AspNetCore.Mvc;
using RabbitMQStudy.Application.Commands;
using System.Collections.Concurrent;

namespace RabbitMQStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController(IMediator mediator, ILogger<OrdersController> logger) : ControllerBase
    {
        [HttpPost("batch")]
        public async Task<IActionResult> CreateOrdersBatch([FromBody] List<CreateOrderCommand> commands)
        {
            try
            {
                if (commands == null || commands.Count == 0)
                {
                    return BadRequest(new { Error = "A lista de pedidos não pode ser vazia" });
                }

                if (commands.Count > 100)
                {
                    return BadRequest(new { Error = "Número máximo de pedidos por lote excedido. Limite: 100" });
                }

                logger.LogInformation("Recebida solicitação para criar {Count} pedidos em lote", commands.Count);

                var results = new ConcurrentBag<object>();

                // paralelismo
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 10)
                };

                await Parallel.ForEachAsync(commands, parallelOptions, async (command, token) =>
                {
                    try
                    {
                        logger.LogInformation("Processando pedido para {CustomerName}", command.CustomerName);

                        var orderId = await mediator.Send(command, token);

                        logger.LogInformation("Pedido criado com sucesso: {OrderId}", orderId);

                        results.Add(new
                        {
                            OrderId = orderId,
                            command.CustomerName,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Erro ao criar pedido para {CustomerName}: {Message}",
                            command.CustomerName, ex.Message);

                        results.Add(new
                        {
                            command.CustomerName,
                            Success = false,
                            Error = ex.Message
                        });
                    }
                });

                var resultsList = results.ToList();

                return Ok(new
                {
                    TotalProcessed = commands.Count,
                    SuccessCount = resultsList.Count(r => ((dynamic)r).Success),
                    FailureCount = resultsList.Count(r => !((dynamic)r).Success),
                    Results = resultsList
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao processar lote de pedidos: {Message}, StackTrace: {StackTrace}",
                    ex.Message, ex.StackTrace);

                var innerExMessage = ex.InnerException?.Message ?? "Sem exceção interna";

                return StatusCode(500, new
                {
                    Error = "Erro interno ao processar o lote de pedidos",
                    ex.Message,
                    InnerExceptionMessage = innerExMessage
                });
            }
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
        }
    }
}