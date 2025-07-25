using MediatR;

namespace RabbitMQStudy.Application.Commands
{
    public class CreateOrderCommand : IRequest<Guid>
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = [];
    }

    public class OrderItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}