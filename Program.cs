using AI_103;
using Microsoft.Extensions.Configuration;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.personal.json", optional: false, reloadOnChange: true) //See the values needed at appsettings.json
    .Build();

//await SimpleChatApp.Run(configuration);
//await ToolChatApp.Run(configuration); 
//await AgentWithAiSearch.Run(configuration);
//await AgentWithMcpTool.Run(configuration);
await AgentWithFoundryIQ.Run(configuration);