using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;
using System.Net;
using LocalClients;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .Build();

await LocalRelayServer.RunAsync(configuration);

/// <summary>
/// Azure Relay-based HTTP listener that routes requests to local LLM clients.
/// Establishes a persistent connection to Azure Relay service and routes requests to different LLM backends:
/// - AdminTask requests → LMStudioClient (unsloth/qwen3-30b-a3b-instruct-2507)
/// - ChatCompletion requests → FoundryLocalClient (phi-4-mini)
/// Loads Azure Relay credentials from appsettings.json/appsettings.Development.json
/// </summary> 
public class LocalRelayServer
{
    public static async Task RunAsync(IConfiguration configuration)
    {
        var relayNamespace = configuration["AzureRelay:RelayNamespace"];
        var connectionName = configuration["AzureRelay:ConnectionName"];
        var keyName = configuration["AzureRelay:KeyName"];
        var key = configuration["AzureRelay:Key"];

        var cts = new CancellationTokenSource();

        var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
        var listener = new HybridConnectionListener(new Uri(string.Format("sb://{0}/{1}", relayNamespace, connectionName)), tokenProvider);

        // Subscribe to the status events.
        listener.Connecting += (o, e) => { Console.WriteLine("Connecting"); };
        listener.Offline += (o, e) => { Console.WriteLine("Offline"); };
        listener.Online += (o, e) => { Console.WriteLine("Online"); };

        var lmStudioClient = new LMStudioClient("unsloth/qwen3-30b-a3b-instruct-2507"); // The LM Studio model that will be used for orchestration.
        var foundryLocalClient = new FoundryLocalClient("phi-4-mini"); // The Foundry Local model that will be initially loaded.

        // Provide an HTTP request handler
        listener.RequestHandler = async (context) =>
        {
            // Extract text from the HTTP request
            string requestBody = string.Empty;
            using (var sr = new StreamReader(context.Request.InputStream))
            {
                requestBody = await sr.ReadToEndAsync();
            }

            // Log the extracted information
            Console.WriteLine($"HTTP Method: {context.Request.HttpMethod}");
            Console.WriteLine($"URL: {context.Request.Url}");
            Console.WriteLine($"Request Body: {requestBody}");

            // Parse requestBody into RequestData struct
            RequestData requestData;
            try
            {
                requestData = System.Text.Json.JsonSerializer.Deserialize<RequestData>(requestBody);
                Console.WriteLine($"Parsed RequestData: Type={requestData.Type}, Text={requestData.Text}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse requestBody: {ex.Message}");
                context.Response.StatusCode = HttpStatusCode.BadRequest;
                context.Response.StatusDescription = "Invalid request body";
                await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Invalid request body"));
                context.Response.Close();
                return;
            }

            // Process headers if needed
            foreach (var header in context.Request.Headers.AllKeys)
            {
                Console.WriteLine($"Header - {header}: {context.Request.Headers[header]}");
            }

            string response = string.Empty;


            // Route the request to the appropriate client based on the request type.
            // If the request type is "AdminTask", use the LMStudioClient to process the request.
            // If the request type is "ChatCompletion", use the FoundryLocalClient to process the request.
            if (requestData.Type == "AdminTask")
            {
                response = lmStudioClient.GetResponse(requestData.Text);
            }
            else if (requestData.Type == "ChatCompletion")
            {
                response = foundryLocalClient.GetResponse(requestData.Text);
            }

            context.Response.StatusCode = HttpStatusCode.OK;
            context.Response.StatusDescription = "OK, Request processed";
            using (var sw = new StreamWriter(context.Response.OutputStream))
            {
                await sw.WriteLineAsync(response);
            }

            // The context MUST be closed here
            context.Response.Close();
        };

        // Opening the listener establishes the control channel to
        // the Azure Relay service. The control channel is continuously 
        // maintained, and is reestablished when connectivity is disrupted.
        await listener.OpenAsync();
        Console.WriteLine("Server listening");

        // Start a new thread that will continuously read the console.
        await Console.In.ReadLineAsync();

        // Close the listener after you exit the processing loop.
        await listener.CloseAsync();
    }

    public struct RequestData
    {
        public string Type { get; set; }
        public string Text { get; set; }
    }
}






