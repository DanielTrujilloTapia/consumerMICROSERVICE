using Dapper;
using MySql.Data.MySqlClient;
using RabbitConsumerService.Models;

public class OrderService
{
    private readonly string _mysqlConnectionString;

    public OrderService(IConfiguration configuration)
    {
        _mysqlConnectionString = configuration.GetConnectionString("MySqlConnection");
    }

    public async Task<IEnumerable<OrderMessage>> GetOrdersAsync()
    {
        const string sql = "SELECT * FROM Orders;";
        using var connection = new MySqlConnection(_mysqlConnectionString);
        var orders = await connection.QueryAsync<OrderMessage>(sql);
        return orders;
    }
}
