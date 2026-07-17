using DbUp.Engine.Transactions;
using RabbitMQ.Client;
using System.Text.Json;

namespace User_Service.Services
{
    public class RabbitMqPublisher
    {
        private readonly IConfiguration _configuration;
        
        private readonly string _hostName;
        private readonly string _userName;
        private readonly string _password;


        public async Task PublisAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _hostName,
                UserName = _userName,
                Password = _password
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken:cancellationToken);

            await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, 
                autoDelete: false, arguments: null, cancellationToken: cancellationToken);

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);

            await channel.BasicPublishAsync(exchange: "", routingKey: queueName, body: body, cancellationToken: cancellationToken);

        }

        public RabbitMqPublisher(IConfiguration configuration, [FromKeyedServices("RabbitMqHostName")] string hostName,
           [FromKeyedServices("RabbitMqUserName")] string userName, [FromKeyedServices("RabbitMqPassword")] string password)
        {
            _configuration = configuration;
            _hostName = _configuration["RabbitMq:HostName"] ?? hostName;
            _userName = _configuration["RabbitMq:UserName"] ?? userName;
            _password = _configuration["RabbitMq:Password"] ?? password;
        }



    }
}
