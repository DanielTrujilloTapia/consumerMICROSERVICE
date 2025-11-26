using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using RabbitConsumerService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitConsumerService.Services
{
    public class RabbitMqListenerService : BackgroundService
    {
        private readonly ILogger<RabbitMqListenerService> _logger;
        private readonly IConfiguration _configuration;
        private IConnection _connection;
        private IModel _channel;

        private readonly string _exchangeName;
        private readonly string _queueName;
        private readonly string _mysqlConnectionString;
        private readonly string _rabbitHostName;
        private readonly string _rabbitUserName;
        private readonly string _rabbitPassword;
        private readonly string _rabbitExchangeType;

        public RabbitMqListenerService(IConfiguration configuration, ILogger<RabbitMqListenerService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Lectura de configuración de nombres
            _exchangeName = _configuration["TopicAndQueueNames:OrderCreatedTopic"];
            _queueName = _configuration["TopicAndQueueNames:OrderQueueName"];
            _mysqlConnectionString = _configuration.GetConnectionString("MySqlConnection");

            // Lectura de configuración de RabbitMQ
            _rabbitHostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
            _rabbitUserName = _configuration["RabbitMQ:UserName"] ?? "guest";
            _rabbitPassword = _configuration["RabbitMQ:Password"] ?? "guest";
            _rabbitExchangeType = _configuration["RabbitMQ:ExchangeType"] ?? ExchangeType.Fanout;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 1. Creación de Conexión y Canal con configuración
            var factory = new ConnectionFactory
            {
                HostName = _rabbitHostName,
                UserName = _rabbitUserName,
                Password = _rabbitPassword
            };

            // Manejo de errores básico para la conexión
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ No se pudo conectar a RabbitMQ. Asegúrate de que el servidor esté corriendo.");
                return Task.CompletedTask;
            }

            // 2. Declaración y Binding (Asegurar que la cola y el exchange existan y estén ligados)
            _channel.ExchangeDeclare(exchange: _exchangeName, type: _rabbitExchangeType, durable: true);
            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue: _queueName, exchange: _exchangeName, routingKey: string.Empty);

            // 3. Configuración del Consumidor
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await InsertMessageToDatabaseAsync(message);
                    // ACK: Confirma que el mensaje ha sido procesado exitosamente
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation($"✅ Mensaje procesado y ACK enviado: {message}");
                }
                catch (Exception ex)
                {
                    // NACK o no hacer nada: Si hay un error, el mensaje se queda en la cola 
                    // (o puede ser re-enviado dependiendo de la configuración del canal y la política DLX).
                    _logger.LogError(ex, $"❌ Error al procesar o insertar mensaje: {message}");
                    // Opcional: _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true); 
                    // Si BasicNack no se usa, el mensaje queda sin ACK y será re-entregado eventualmente.
                }
            };

            // 4. Iniciar el Consumo
            // autoAck: false -> Nosotros controlamos el reconocimiento (BasicAck/BasicNack)
            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation($"🚀 Escuchando mensajes de la cola '{_queueName}' en el exchange '{_exchangeName}'...");

            return Task.CompletedTask;
        }

        private async Task InsertMessageToDatabaseAsync(string message)
        {
            // Deserializa el JSON del mensaje al modelo C#
            var order = System.Text.Json.JsonSerializer.Deserialize<OrderMessage>(message);

            const string sql = @"
                INSERT INTO Orders (
                    Id, UserName, TotalPrice, FirstName, LastName, EmailAddress,
                    AddressLine, Country, State, ZipCode, CardName, CardNumber,
                    Expiration, CVV, PaymentMethod, CreateBy, CreateDate
                )
                VALUES (
                    @Id, @UserName, @TotalPrice, @FirstName, @LastName, @EmailAddress,
                    @AddressLine, @Country, @State, @ZipCode, @CardName, @CardNumber,
                    @Expiration, @CVV, @PaymentMethod, @CreateBy, @CreateDate
                );
            ";

            using var connection = new MySqlConnection(_mysqlConnectionString);
            await connection.ExecuteAsync(sql, new
            {
                order.Id,
                order.UserName,
                order.TotalPrice,
                order.FirstName,
                order.LastName,
                order.EmailAddress,
                order.AddressLine,
                order.Country,
                order.State,
                order.ZipCode,
                order.CardName,
                order.CardNumber,
                order.Expiration,
                order.CVV,
                order.PaymentMethod,
                CreateBy = "RabbitConsumerService",
                CreateDate = DateTime.UtcNow
            });
        }

        public override void Dispose()
        {
            // Cierre seguro de la conexión al detener el servicio
            if (_channel?.IsOpen == true)
            {
                _channel.Close();
            }
            if (_connection?.IsOpen == true)
            {
                _connection.Close();
            }
            base.Dispose();
        }
    }
}