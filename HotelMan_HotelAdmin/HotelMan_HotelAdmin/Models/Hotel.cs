using Amazon.DynamoDBv2.DataModel;

namespace HotelMan_HotelAdmin.Models;

[DynamoDBTable("Hotels")]
public class Hotel
{
    [DynamoDBHashKey("userId")] public string? UserId { get; set; }

    [DynamoDBRangeKey("Id")] public string? Id { get; set; }

    public string? Name { get; set; }
    public int Price { get; set; }
    public int Rating { get; set; }
    public string? CityName { get; set; }
    public string? FileName { get; set; }
}