using Microsoft.Extensions.DependencyInjection;
using RabbitMQStudy.Application.Interfaces;
using RabbitMQStudy.Infrastructure.Messaging;

namespace RabbitMQStudy.Infrastructure.Config
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();
            return services;
        }
    }
}
