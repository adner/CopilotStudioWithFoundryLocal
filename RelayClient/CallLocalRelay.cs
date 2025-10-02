using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Azure.Relay;
using Microsoft.Extensions.Configuration;

namespace Company.Function;

public class CallLocalRelay
{
    private readonly ILogger<CallLocalRelay> _logger;
    private readonly IConfiguration _configuration;

    public CallLocalRelay(ILogger<CallLocalRelay> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("CallLocalRelay")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        // Extract string from request body
        string requestBody = string.Empty;
        using (StreamReader reader = new StreamReader(req.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }

        var relayNamespace = _configuration["AzureRelay:RelayNamespace"];
        var connectionName = _configuration["AzureRelay:ConnectionName"];
        var keyName = _configuration["AzureRelay:KeyName"];
        var key = _configuration["AzureRelay:Key"];

        var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
        var uri = new Uri(string.Format("https://{0}/{1}", relayNamespace, connectionName));
        var token = (await tokenProvider.GetTokenAsync(uri.AbsoluteUri, TimeSpan.FromHours(1))).TokenString;
        var client = new HttpClient();
        var request = new HttpRequestMessage()
        {
            RequestUri = uri,
            Method = HttpMethod.Post,
            Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
        };

        request.Headers.Add("ServiceBusAuthorization", token);
        var response = await client.SendAsync(request);

        var responseText = await response.Content.ReadAsStringAsync();

        return new OkObjectResult(responseText);
    }
}