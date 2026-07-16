using Npgsql;
using System.Data;

namespace EcommerceTests.Helpers
{
    public class DatabaseHelper
    {
        private NpgsqlConnection _connection;
        private readonly string _connectionString;

        public DatabaseHelper(string host, int port, string database, string username, string password)
        {
            _connectionString = $"Host ={host};Port={port};Database={database};Username={username};Password={password}";
        }

        public void Connect()
        {
            _connection = new NpgsqlConnection(_connectionString);
            _connection.Open();
        }

        public void Disconnect()
        {
            if (_connection != null && _connection.Status == ConnectionState.Open)
            {
                _connection.Close();
                _connection.Dispose();
            }
        }

        public Order GetOrderById(string orderId)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT order_id, customer_email, original_amount,discount_amount, final_amount, promotion_code, ststus,created_at FROM orders WHERE order_id = @orderId",_connection);
                cmd.Parameters.AddWithValue("orderId", orderId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new Order
                    {
                        OrderId = reader.GetString(0),
                        CustomerEmail = reader.GetString(1),
                        OriginalAccount = reader.GetDecimal(2),
                        DiscountAmount = reader.GetDecimal(3),
                        FinalAmount = reader.GetDecimal(4),
                        PromotionCode = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Status = reader.GetString(6),
                        CreatedAt = reader.GetDateTime(7)

                    };


                }
                return null ;

        }

        public AuditLog GetAuditLogByOrderId(string orderId)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT audit_id, promotion_id, order_id,discount_applied, used_at Frompromotion_audit_log WHERE order_id = @orderId",_connection
            );
            cmd.Parameters.AddWithValue("orderId", orderId);

            using var reader = cmd.ExecuteReader();
            if(reader.Read())
            {
                return new AuditLog
                {
                    AuditId = reader.GetInt32(0),
                    PromotionId = reader.GetString(1),
                    OrderId = reader.GetString(2),
                    DiscountApplied = reader.GetDecimal(3),
                    UsedAt = reader.GetDateTime(4)
                };
            }
            return null;

        }

        public void DeleteOrder(string orderId)
        {
            using var cmd = new NpgsqlCommand(" DELETE FROM orders WHERE order_id = @orderId",_connection);
            cmd.Parameters.AddWithValue("orderId", orderId);
            cmd.ExecuteNonQuery();
        }

        public bool VerifyOrderTotals(string orderId, decimal expectedOriginal,
            decimal expectedDiscount, decimal expectedFinal)
        {
            var order = GetOrderById(orderId);
            if (order == null) return false;

            return order.OriginalAmount == expectedOriginal && 
            order.DiscountAmount == expectedDiscount &&
            order.FinalAmount == expectedFinal;


        }
    }

    public class Order
    {
        public string OrderId { get; set; }
        public string CustomerEmail { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string PromotionCode { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLog
    {
        public int AuditId { get; set; }
        public string PromotionId { get; set; }
        public string OrderId { get; set; }
        public decimal DiscountApplied { get; set; }
        public DateTime UsedAt { get; set; }
    }
}
