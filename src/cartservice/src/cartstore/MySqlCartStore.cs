using System;
using Grpc.Core;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace cartservice.cartstore
{
    public class MySqlCartStore : ICartStore
    {
        private readonly string tableName;
        private readonly string connectionString;

        public MySqlCartStore(IConfiguration configuration)
        {
            string mysqlPassword = configuration["MYSQL_PASSWORD"];
            string mysqlUser = configuration["MYSQL_USER"];
            string databaseName = configuration["MYSQL_DATABASE_NAME"];
            string mysqlHost = configuration["MYSQL_HOST"];
            
            connectionString = $"Server={mysqlHost};" +
                               $"Database={databaseName};" +
                               $"User ID={mysqlUser};" +
                               $"Password={mysqlPassword};" +
                               $"SslMode=Required;";

            tableName = configuration["MYSQL_TABLE_NAME"];
        }

        public async Task AddItemAsync(string userId, string productId, int quantity)
        {
            Console.WriteLine($"AddItemAsync for {userId} called");
            try
            {
                using MySqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                // Fetch the current quantity for our userId/productId tuple
                string fetchCmd = $"SELECT quantity FROM {tableName} WHERE userID='{userId}' AND productID='{productId}'";
                MySqlCommand command = new(fetchCmd, connection);
                var currentQuantity = (int)(await command.ExecuteScalarAsync() ?? 0);
                var totalQuantity = quantity + currentQuantity;

                string insertCmd = $"INSERT INTO {tableName} (userId, productId, quantity) VALUES ('{userId}', '{productId}', {totalQuantity}) ON DUPLICATE KEY UPDATE quantity={totalQuantity}";
                MySqlCommand cmdInsert = new(insertCmd, connection);
                await cmdInsert.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new RpcException(
                    new Status(StatusCode.FailedPrecondition, $"Can't access cart storage at {connectionString}. {ex}"));
            }
        }

        public async Task<Hipstershop.Cart> GetCartAsync(string userId)
        {
            Console.WriteLine($"GetCartAsync called for userId={userId}");
            Hipstershop.Cart cart = new();
            cart.UserId = userId;

            try
            {
                using MySqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                string cartFetchCmd = $"SELECT productId, quantity FROM {tableName} WHERE userId = '{userId}'";
                MySqlCommand command = new(cartFetchCmd, connection);
                using MySqlDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Hipstershop.CartItem item = new()
                    {
                        ProductId = reader.GetString(0),
                        Quantity = reader.GetInt32(1)
                    };
                    cart.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                throw new RpcException(
                    new Status(StatusCode.FailedPrecondition, $"Can't access cart storage at {connectionString}. {ex}"));
            }
            return cart;
        }

        public async Task EmptyCartAsync(string userId)
        {
            Console.WriteLine($"EmptyCartAsync called for userId={userId}");

            try
            {
                using MySqlConnection connection = new(connectionString);
                await connection.OpenAsync();
                
                string deleteCmd = $"DELETE FROM {tableName} WHERE userID = '{userId}'";
                MySqlCommand command = new(deleteCmd, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new RpcException(
                    new Status(StatusCode.FailedPrecondition, $"Can't access cart storage at {connectionString}. {ex}"));
            }
        }

        public bool Ping()
        {
            try
            {
                using MySqlConnection connection = new(connectionString);
                connection.Open();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
