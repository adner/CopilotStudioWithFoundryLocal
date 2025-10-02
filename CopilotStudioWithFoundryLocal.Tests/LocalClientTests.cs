namespace CopilotStudioWithFoundryLocal.Tests;

using Xunit;
using LocalClients;
using System.Text;

public class MLStudioClient_Tests
{
    private readonly LMStudioClient client;
    private readonly string modelId = "unsloth/qwen3-30b-a3b-instruct-2507";

    public MLStudioClient_Tests()
    {
        client = new LMStudioClient(modelId);
    }

    [Fact]
    public void Simple_Message()
    {
        var response = client.GetResponse("What is the capital of France?");

        Assert.NotEmpty(response);
        Assert.Contains("Paris", response);
    }

    [Fact]
    public void Tool_Call_OpenTaskMgr()
    {
        var response = client.GetResponse("Please open the Windows task manager?");
    }
}

public class FoundryLocal_Tests
{
    private readonly FoundryLocalClient client;
    private readonly string modelId = "qwen2.5-14b";

    public FoundryLocal_Tests()
    {
        client = new FoundryLocalClient(modelId);
    }

    [Fact]
    public void Simple_Message()
    {
        var response = client.GetResponse("What is the capital of France?");

        Assert.NotEmpty(response);
        Assert.Contains("Paris", response);
    }

    [Fact]
    public void Tool_Call_OpenTaskMgr()
    {
        var response = client.GetResponse("Please open the Windows task manager?");

    }
}