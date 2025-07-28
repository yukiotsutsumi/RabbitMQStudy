namespace RabbitMQStudy.Infrastructure.Messaging
{
    public class RabbitMQSettings
    {
        public string Host { get; set; } = "rabbitmq";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
    }
}
