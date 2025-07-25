namespace RabbitMQStudy.Application.Interfaces
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(string routingKey, T message);
    }
}