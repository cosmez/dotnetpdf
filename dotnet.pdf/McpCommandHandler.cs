using DotNet.Pdf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.pdf;
internal class McpCommandHandler : BaseCommandHandler
{
    private readonly PdfProcessor _pdfProcessor;

    public McpCommandHandler(ILogger<McpCommandHandler> logger, ILoggerFactory loggerFactory) : base(logger)
    {
        _pdfProcessor = new PdfProcessor(loggerFactory);
    }


    public async Task StartMcpServerAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Configure all logs to go to stderr
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        // core services for tools
        builder.Services.AddSingleton(_pdfProcessor);
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddSingleton<CommandsHandler>(sp => new CommandsHandler(sp.GetRequiredService<ILogger<CommandsHandler>>(), sp.GetRequiredService<ILoggerFactory>()));
        builder.Services.AddSingleton<MoreCommandsHandler>(sp => new MoreCommandsHandler(sp.GetRequiredService<ILogger<MoreCommandsHandler>>(), sp.GetRequiredService<ILoggerFactory>()));

        await builder.Build().RunAsync();
    }
}
