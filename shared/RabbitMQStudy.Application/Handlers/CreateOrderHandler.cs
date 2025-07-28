using MediatR;
using Microsoft.Extensions.Logging;
using RabbitMQStudy.Application.Commands;
using RabbitMQStudy.Application.Interfaces;
using RabbitMQStudy.Domain.Entities;
using RabbitMQStudy.Domain.Events;

namespace RabbitMQStudy.Application.Handlers
{
    public class CreateOrderHandler(IMessagePublisher publisher, ILogger<CreateOrderHandler> logger) : IRequestHandler<CreateOrderCommand, Guid>
    {
        public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Iniciando processamento do pedido para cliente: {CustomerName}", request.CustomerName);

            try
            {
                // Criar a entidade Order
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CreatedAt = DateTime.UtcNow,
                    Status = OrderStatus.Pending
                };

                logger.LogDebug("Pedido criado com ID: {OrderId}", order.Id);

                // Adicionar itens ao pedido
                var orderItems = request.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList();

                order.Items = orderItems;
                order.TotalAmount = orderItems.Sum(item => item.Quantity * item.UnitPrice);

                logger.LogInformation("Pedido {OrderId} criado para {CustomerName} com {ItemCount} itens, valor total: {TotalAmount}",
                    order.Id, order.CustomerName, orderItems.Count, order.TotalAmount);

                // Criar o evento com a estrutura atualizada
                var orderEvent = new OrderCreatedEvent
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    TotalAmount = order.TotalAmount,
                    CreatedAt = order.CreatedAt,
                    Items = [.. order.Items.Select(item => new Domain.Events.OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    })]
                };

                logger.LogDebug("Evento OrderCreatedEvent criado, tentando publicar...");

                // Publicar o evento
                try
                {
                    logger.LogInformation("Publicando evento order.created para pedido {OrderId}", order.Id);
                    await publisher.PublishAsync("order.created", orderEvent);
                    logger.LogInformation("Evento publicado com sucesso para pedido {OrderId}", order.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Erro ao publicar evento para pedido {OrderId}: {ErrorMessage}",
                        order.Id, ex.Message);
                    throw;
                }

                return order.Id;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao processar pedido para {CustomerName}: {ErrorMessage}",
                    request.CustomerName, ex.Message);
                throw;
            }
        }
    }
}