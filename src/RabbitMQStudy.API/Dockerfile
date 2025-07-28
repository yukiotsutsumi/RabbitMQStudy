FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar os arquivos de projeto primeiro para aproveitar o cache
COPY ["src/RabbitMQStudy.API/RabbitMQStudy.API.csproj", "src/RabbitMQStudy.API/"]
COPY ["shared/RabbitMQStudy.Application/RabbitMQStudy.Application.csproj", "shared/RabbitMQStudy.Application/"]
COPY ["shared/RabbitMQStudy.Domain/RabbitMQStudy.Domain.csproj", "shared/RabbitMQStudy.Domain/"]
COPY ["shared/RabbitMQStudy.Infrastructure/RabbitMQStudy.Infrastructure.csproj", "shared/RabbitMQStudy.Infrastructure/"]

# Restaurar as dependências
RUN dotnet restore "src/RabbitMQStudy.API/RabbitMQStudy.API.csproj"

# Copiar o resto do código
COPY . .

# Construir a API
WORKDIR "/src/src/RabbitMQStudy.API"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Definir variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:8080
ENV RabbitMQ__Host=rabbitmq
ENV RabbitMQ__Port=5672
ENV RabbitMQ__UserName=guest
ENV RabbitMQ__Password=guest
ENV RabbitMQ__VirtualHost=/

ENTRYPOINT ["dotnet", "RabbitMQStudy.API.dll"]