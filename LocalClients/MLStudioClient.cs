namespace CopilotStudioWithFoundryLocal;

using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

public class MLStudioClient
{
    private OpenAIClient client;

    private ChatClient chatClient;

    public MLStudioClient(string modelId)
    {
        ApiKeyCredential key = new ApiKeyCredential("No key required!");

        client = new OpenAIClient(key, new OpenAIClientOptions
        {
            Endpoint = new Uri("http://127.0.0.1:1234/v1")
        });

        chatClient = client.GetChatClient(modelId);
    }

    public string GetResponse(string userMessage)
    {
        var completionUpdates = chatClient.CompleteChatStreaming(userMessage);

        StringBuilder builder = new StringBuilder();

        foreach (var completionUpdate in completionUpdates)
        {
            if (completionUpdate.ContentUpdate.Count > 0)
            {
                builder.Append(completionUpdate.ContentUpdate[0].Text);
            }
        }

        return builder.ToString();
    }
}
