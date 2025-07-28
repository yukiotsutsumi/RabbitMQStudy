using MediatR;
using Microsoft.AspNetCore.Mvc;
using RabbitMQStudy.Application.Commands;

namespace RabbitMQStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController(IMediator mediator, ILogger<OrdersController> logger) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
        {
            try
            {
                logger.LogInformation("Recebida solicitação para criar pedido para {CustomerName}", command.CustomerName);

                var orderId = await mediator.Send(command);

                logger.LogInformation("Pedido criado com sucesso: {OrderId}", orderId);

                return Ok(new { OrderId = orderId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao criar pedido: {Message}, StackTrace: {StackTrace}",
                    ex.Message, ex.StackTrace);

                var innerExMessage = ex.InnerException?.Message ?? "Sem exceção interna";

                return StatusCode(500, new
                {
                    Error = "Erro interno ao processar o pedido",
                    ex.Message,
                    InnerExceptionMessage = innerExMessage
                });
            }
        }

        [HttpGet]
        public IActionResult GetOrders()
        {
            return Ok(new { Message = "API de pedidos está funcionando!" });
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
        }
    }
}