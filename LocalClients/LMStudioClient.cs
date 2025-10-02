namespace LocalClients;

using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
using Microsoft.AI.Foundry.Local;
using System.Text.Json;
using OpenAI.VectorStores;

public class LMStudioClient : LocalOpenAiClient
{


    public LMStudioClient(string modelId) : base(modelId, new Uri("http://127.0.0.1:1234/v1"))
    {
    }

    public override string GetResponse(string userMessage)
    {
        if (client == null)
            throw new InvalidOperationException("OpenAIClient is not initialized.");
        if (modelId == null)
            throw new InvalidOperationException("ModelId is not set.");

        messages.Clear();

        messages.Add(new UserChatMessage(userMessage));

        var chatClient = client.GetChatClient(modelId);

        bool requiresAction;

        ChatCompletionOptions options = new()
        {
            Tools = { openTaskManagerTool, listCatalogModelsTool, listLoadedModelsTool, setActiveModelTool, loadModelTool, getGPUutilizationTool },
        };

        string resultText = string.Empty;

        do
        {
            requiresAction = false;
            ChatCompletion completion = chatClient.CompleteChat(messages, options);

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    {
                        // Add the assistant message to the conversation history.
                        messages.Add(new AssistantChatMessage(completion));
                        resultText = completion.Content[0].Text;
                        break;
                    }

                case ChatFinishReason.ToolCalls:
                    {
                        // First, add the assistant message with tool calls to the conversation history.
                        messages.Add(new AssistantChatMessage(completion));

                        // Then, add a new tool message for each tool call that is resolved.
                        foreach (ChatToolCall toolCall in completion.ToolCalls)
                        {
                            switch (toolCall.FunctionName)
                            {
                                case nameof(OpenTaskManager):
                                    {
                                        string toolResult = OpenTaskManager();
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                case nameof(ListCatalogModels):
                                    {
                                        string toolResult = ListCatalogModels();
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                case nameof(ListLoadedModels):
                                    {
                                        string toolResult = ListLoadedModels();
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                case nameof(GetGpuMemoryUtilization):
                                    {
                                        string toolResult = GetGpuMemoryUtilization();
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                case nameof(SetActiveModel):
                                    {
                                        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        bool hasModelAlias = argumentsJson.RootElement.TryGetProperty("modelAlias", out JsonElement modelAlias);

                                        string toolResult = SetActiveModel(modelAlias.ToString());
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                 case nameof(LoadModel):
                                    {
                                        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        bool hasModelAlias = argumentsJson.RootElement.TryGetProperty("modelAlias", out JsonElement modelAlias);

                                        string toolResult = LoadModel(modelAlias.ToString());
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }

                                default:
                                    {
                                        // Handle other unexpected calls.
                                        throw new NotImplementedException();
                                    }
                            }
                        }

                        requiresAction = true;
                        break;
                    }

                case ChatFinishReason.Length:
                    throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    throw new NotImplementedException("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    throw new NotImplementedException("Deprecated in favor of tool calls.");

                default:
                    throw new NotImplementedException(completion.FinishReason.ToString());
            }
        } while (requiresAction);

        return resultText;
    }

    private static string OpenTaskManager()
    {
        try
        {
            System.Diagnostics.Process.Start("taskmgr.exe");
            return "Task Manager has been opened successfully.";
        }
        catch (Exception ex)
        {
            return $"Failed to open Task Manager: {ex.Message}";
        }
    }

    private static string ListCatalogModels()
    {
        try
        {
            if (LocalOpenAiClient.manager == null) throw new NullReferenceException("Initialize FoundryLocalManager first!");

            List<ModelInfo> models = LocalOpenAiClient.manager.ListCatalogModelsAsync().Result;

            StringBuilder builder = new StringBuilder();

            foreach (var model in models)
            {
                builder.Append("-" + model.Alias + "\n");
            }

            return "The models available in the Azure Foundry Local catalog are:\n\n" + builder.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to list models in catalog: {ex.Message}";
        }
    }

    private static string ListLoadedModels()
    {
        try
        {
            if (LocalOpenAiClient.manager == null) throw new NullReferenceException("Initialize FoundryLocalManager first!");

            List<ModelInfo> models = LocalOpenAiClient.manager.ListLoadedModelsAsync().Result;

            StringBuilder builder = new StringBuilder();

            foreach (var model in models)
            {
                builder.Append("-" + model.Alias + "\n");
            }

            return "The models currently loaded in Azure Foundry Local:\n\n" + builder.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to list loaded models: {ex.Message}";
        }
    }

    private static string SetActiveModel(string modelAlias)
    {
        try
        {
            FoundryLocalClient.currentlyActiveModel = LocalOpenAiClient.manager.GetModelInfoAsync(modelAlias).Result;

            return "The active model has been set to " + modelAlias;
        }
        catch (Exception ex)
        {
            return $"Failed to set active model: {ex.Message}";
        }
    }

    private static string LoadModel(string modelAlias)
    {
        try
        {
            FoundryLocalClient.currentlyActiveModel = LocalOpenAiClient.manager.LoadModelAsync(modelAlias).Result;

            return "The model " + modelAlias + "has been loaded and is now active.";
        }
        catch (Exception ex)
        {
            return $"Failed to load model: {ex.Message}";
        }
    }

/// <summary>
/// Uses nvidia-smi to get statistics on GPU utilization
/// </summary>
/// <returns></returns>
    private static string GetGpuMemoryUtilization()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=memory.total,memory.used,memory.free --format=csv,nounits,noheader",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (string.IsNullOrWhiteSpace(output))
                return "No GPU information found. Ensure NVIDIA drivers and nvidia-smi are installed.";

            var sb = new StringBuilder();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int gpuIndex = 0;
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length == 3)
                {
                    sb.AppendLine($"GPU {gpuIndex}: Total: {parts[0].Trim()} MiB, Used: {parts[1].Trim()} MiB, Free: {parts[2].Trim()} MiB");
                }
                gpuIndex++;
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to get GPU memory utilization: {ex.Message}";
        }
    }

    /// <summary>
    /// LLM tool definitions
    /// </summary>
    private static readonly ChatTool openTaskManagerTool = ChatTool.CreateFunctionTool(
    functionName: nameof(OpenTaskManager),
    functionDescription: "Open the Windows Task Manager");

    private static readonly ChatTool listCatalogModelsTool = ChatTool.CreateFunctionTool(
    functionName: nameof(ListCatalogModels),
    functionDescription: "Lists the available models in the Azure Foundry Local catalog.");

    private static readonly ChatTool listLoadedModelsTool = ChatTool.CreateFunctionTool(
    functionName: nameof(ListLoadedModels),
    functionDescription: "Lists the currently loaded models in Azure Foundry Local.");

    private static readonly ChatTool getGPUutilizationTool = ChatTool.CreateFunctionTool(
    functionName: nameof(GetGpuMemoryUtilization),
    functionDescription: "Returns the utilization of the GPUs in the system.");

    private static readonly ChatTool setActiveModelTool = ChatTool.CreateFunctionTool(
        functionName: nameof(SetActiveModel),
        functionDescription: "Sets the model that is currently active in Foundry Local.",
        functionParameters: BinaryData.FromString(
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""modelAlias"": {
                        ""type"": ""string"",
                        ""description"": ""The alias of the model to set as active.""
                    }
                },
                ""required"": [ ""modelAlias"" ]
            }"
        )
    );
    
    private static readonly ChatTool loadModelTool = ChatTool.CreateFunctionTool(
        functionName: nameof(LoadModel),
        functionDescription: "Loads a model in Azure Foundry Local.",
        functionParameters: BinaryData.FromString(
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""modelAlias"": {
                        ""type"": ""string"",
                        ""description"": ""The alias of the model to be loaded.""
                    }
                },
                ""required"": [ ""modelAlias"" ]
            }"
        )
    );
}
