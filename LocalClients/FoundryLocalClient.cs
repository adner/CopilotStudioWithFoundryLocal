namespace LocalClients;

using System.ClientModel;
using Microsoft.AI.Foundry.Local;
using OpenAI;
using OpenAI.Chat;

public class FoundryLocalClient : LocalOpenAiClient
{

    public FoundryLocalClient(string modelAlias) : base()
    {
        LocalOpenAiClient.manager = FoundryLocalManager.StartModelAsync(aliasOrModelId: modelAlias).Result;

        ApiKeyCredential key = new ApiKeyCredential(manager.ApiKey);

        var modelInfo = LocalOpenAiClient.manager.GetModelInfoAsync(aliasOrModelId: modelAlias).Result;

        this.modelId = modelInfo?.ModelId;

        LocalOpenAiClient.currentlyActiveModel = modelInfo;

        client = new OpenAIClient(key, new OpenAIClientOptions
        {
            Endpoint = LocalOpenAiClient.manager.Endpoint
        });
    }

    public override string GetResponse(string userMessage)
    {
        {
            //Simple  request response, since Foundry Local does not support tool calling.

            if (client == null)
                throw new InvalidOperationException("OpenAIClient is not initialized.");
            if (modelId == null)
                throw new InvalidOperationException("ModelId is not set.");

            messages.Add(new UserChatMessage(userMessage));

            var chatClient = client.GetChatClient(LocalOpenAiClient.currentlyActiveModel?.ModelId);

            ChatCompletion completion = chatClient.CompleteChat(messages);

            return completion.Content[0].Text;
        }
    }
}
