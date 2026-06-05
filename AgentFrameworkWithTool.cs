using System.ComponentModel;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103;

#pragma warning disable OPENAI001

public sealed class AgentFrameworkWithTool
{
    public static async Task Run(IConfiguration configuration)
    {
        var projectClient = new AIProjectClient(
            new Uri(configuration["ProjectUrl"]!),
            new AzureCliCredential());

        var createdAgent = await CreateAgent(projectClient, configuration);
        var agent = projectClient.AsAIAgent(
            createdAgent,
            tools: [AIFunctionFactory.Create(AgentFunction, "test-agent-function")]);

        var conversation = await agent.CreateConversationSessionAsync();
        var run = await agent.RunAsync("This is a simple test, what's your function output?", conversation);
        
        Console.WriteLine($"Agent response: {run.Text}");
    }
    
    private static async Task<ProjectsAgentVersion> CreateAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "You are an AI agent that calls and gives back the result from you connected tool function with a nice message and the function result inside ''."
        };
        
        agentDefinition.Tools.Add(new FunctionTool("test-agent-function", null, null));
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-maf-agent",
            options: new(agentDefinition));

        return agent;
    }

    [Description("Test agent function.")]
    private static string AgentFunction()
    {
        return "Test agent function result. Hello from code-side functions!";
    }
}