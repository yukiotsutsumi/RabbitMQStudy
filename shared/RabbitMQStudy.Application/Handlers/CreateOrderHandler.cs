using MediatR;
using RabbitMQStudy.Application.Commands;
using RabbitMQStudy.Application.Interfaces;
using RabbitMQStudy.Domain.Entities;
using RabbitMQStudy.Domain.Events;

namespace RabbitMQStudy.Application.Handlers
{
    public class CreateOrderHandler(IMessagePublisher publisher) : IRequestHandler<CreateOrderCommand, Guid>
    {
        public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
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

            Console.WriteLine($"Pedido {order.Id} criado para {order.CustomerName}");

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

            // Publicar o evento
            await publisher.PublishAsync("order.created", orderEvent);

            return order.Id;
        }
    }
}