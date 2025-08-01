using System.Text.Json.Serialization;

namespace App;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class Order
{
    public int OrderId { get; set; }
    public Product[] Items { get; set; } = [];
}

[JsonSerializable(typeof(Product))]
[JsonSerializable(typeof(Order))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
