using System;

namespace RabbitConsumerService.Models
{
    public class ProductsMessage
    {
        public int Id { get; set; }

        public string ProductName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }

        // Precios
        public decimal Price { get; set; }           // Precio de venta

        // Inventario
        public int Stock { get; set; }
        public string UnitOfMeasure { get; set; }    // kg, pieza, paquete, litros, etc.

        // Imagen del producto
        public string ImageUrl { get; set; }
    }
}
