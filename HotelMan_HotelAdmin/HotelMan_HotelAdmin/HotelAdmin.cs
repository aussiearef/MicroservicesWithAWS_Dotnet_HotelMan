using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using HttpMultipartParser;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace HotelMan_HotelAdmin;

public class HotelAdmin
{
    public APIGatewayProxyResponse AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,POST");

        var bodyContent = request.IsBase64Encoded
            ? Convert.FromBase64String(request.Body)
            : Encoding.UTF8.GetBytes(request.Body);

        using var memStream = new MemoryStream(bodyContent);
        var formData = MultipartFormDataParser.Parse(memStream);

        var hotelName = formData.GetParameterValue("hotelName");
        var hotelRating = formData.GetParameterValue("hotelRating");
        var hotelCity = formData.GetParameterValue("hotelCity");
        var hotelPrice = formData.GetParameterValue("hotelPrice");

        var file = formData.Files.FirstOrDefault();
        var fileName = file.FileName;
        // file.Data

        var userId = formData.GetParameterValue("userId");
        var idToken = formData.GetParameterValue("idToken");

        var token = new JwtSecurityToken(jwtEncodedString: idToken);
        var group = token.Claims.FirstOrDefault(x => x.Type == "cognito:groups");
        if (group == null || group.Value != "Admin")
        {
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.Body = JsonSerializer.Serialize(new {Error="Unauthorised. Must be a member of Admin group."});
        }
        Console.WriteLine("OK.");

        return response;
    }
}