using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using HotelMan_HotelAdmin.Models;
using HttpMultipartParser;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace HotelMan_HotelAdmin;

public class HotelAdmin
{
    public async Task<APIGatewayProxyResponse> ListHotels(APIGatewayProxyRequest request)
    {
        // query string parameter called token is passed to this lambda method.
        
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,GET");
        response.Headers.Add("Content-Type","application/json");

        if (request?.QueryStringParameters == null)
        {
            Console.WriteLine("Query string is null. You must configure the Query String Mapping in your API resource in API Gateway");
            return response;
        }
        var token = request.QueryStringParameters.ContainsKey("token") ?  request.QueryStringParameters["token"]: "";
        if (string.IsNullOrEmpty(token))
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            response.Body = JsonSerializer.Serialize(new { Error = "Query parameter 'token' not present." });
            return response;
        }
        

        var tokenDetails = new JwtSecurityToken(token);
        var userId = tokenDetails.Claims.FirstOrDefault(x => x.Type == "sub")?.Value;
        
        var region = Environment.GetEnvironmentVariable("AWS_REGION");
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));
        using var dbContext = new DynamoDBContext(dbClient);

        var hotels = await dbContext.ScanAsync<Hotel>(new[] { new ScanCondition("UserId", ScanOperator.Equal, userId) })
            .GetRemainingAsync();

        response.Body = JsonSerializer.Serialize(new {Hotels=hotels});
        
        return response;
    }
    public async Task<APIGatewayProxyResponse> AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,POST");
        response.Headers.Add("Content-Type","application/json");

        var bodyContent = request.IsBase64Encoded
            ? Convert.FromBase64String(request.Body)
            : Encoding.UTF8.GetBytes(request.Body);

        Console.WriteLine($"Request size after decode: {bodyContent.Length}");
        
        await using var memStream = new MemoryStream(bodyContent);
        var formData = await MultipartFormDataParser.ParseAsync(memStream).ConfigureAwait(false);

        var hotelName = formData.GetParameterValue("hotelName");
        var hotelRating = formData.GetParameterValue("hotelRating");
        var hotelCity = formData.GetParameterValue("hotelCity");
        var hotelPrice = formData.GetParameterValue("hotelPrice");

        var file = formData.Files.FirstOrDefault();
        var fileName = file.FileName;

        
        
        var userId = formData.GetParameterValue("userId");
        var idToken = formData.GetParameterValue("idToken");

        var token = new JwtSecurityToken(idToken);
        var group = token.Claims.FirstOrDefault(x => x.Type == "cognito:groups");
        if (group == null || group.Value != "Admin")
        {
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.Body = JsonSerializer.Serialize(new { Error = "Unauthorised. Must be a member of Admin group." });
        }

        var region = Environment.GetEnvironmentVariable("AWS_REGION");
        var bucketName = Environment.GetEnvironmentVariable("bucketName");


        var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));
        
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = file.Data
            });

            var hotel = new Hotel
            {
                UserId = userId,
                Id = Guid.NewGuid().ToString(),
                Name = hotelName,
                CityName = hotelCity,
                Price = int.Parse(hotelPrice),
                Rating = int.Parse(hotelRating),
                FileName = fileName
            };

            using var dbContext = new DynamoDBContext(dbClient);
            await dbContext.SaveAsync(hotel);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        

        response.Body = JsonSerializer.Serialize(new { Message = "Hotel information was stored successfully." });
        return response;
    }
}