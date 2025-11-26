using Microsoft.AspNetCore.Mvc;
using RabbitConsumerService.Services;
using System.Text.Json;

namespace RabbitConsumerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        /*[HttpGet]
        public IActionResult GetMessages()
        {
            // Si quieres devolver el JSON crudo:
            // return Ok(RabbitConsumerService.GetMessages());

            // Si quieres que los mensajes se deserialicen automáticamente:
            var messages = RabbitMqListenerService.GetMessages()
                .Select(m =>
                {
                    try
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, object>>(m);
                    }
                    catch
                    {
                        return new Dictionary<string, object> { ["raw"] = m };
                    }
                })
                .ToList();

            return Ok(messages);
        }*/

        private readonly OrderService _orderService;

        public MessagesController(OrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _orderService.GetOrdersAsync();
            return Ok(orders);
        }

    }
}
