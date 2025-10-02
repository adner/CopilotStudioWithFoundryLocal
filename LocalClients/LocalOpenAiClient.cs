using System.ClientModel;
using System.Text;
using Microsoft.AI.Foundry.Local;
using OpenAI;
using OpenAI.Chat;
using OpenAI.VectorStores;

public abstract class LocalOpenAiClient
{
    protected OpenAIClient? client;
    protected string? modelId;

    protected static FoundryLocalManager manager;

    protected static ModelInfo currentlyActiveModel;

    protected List<ChatMessage> messages;

    public LocalOpenAiClient(string modelId, Uri endpoint)
    {
        ApiKeyCredential key = new ApiKeyCredential("No key required!");

        this.modelId = modelId;

        client = new OpenAIClient(key, new OpenAIClientOptions
        {
            Endpoint = endpoint
        });

        messages = new List<ChatMessage>();
    }

    public LocalOpenAiClient()
    {
        messages = new List<ChatMessage>();
    }

    public abstract string GetResponse(string userMessage);
    
}