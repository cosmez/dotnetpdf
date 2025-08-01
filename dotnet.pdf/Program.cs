using System.CommandLine;
using PDFiumCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine.Invocation;

namespace dotnet.pdf;

class Program
{
    private const string Version = "v0.4";
    private static ServiceProvider _serviceProvider = null!;
    private static CommandsHandler _commandHandler = null!;
    private static MoreCommandsHandler _moreCommandHandler = null!;

    static async Task<int> Main(string[] args)
    {
        // Initialize logging and services
        _serviceProvider = ConfigureServices();
        _commandHandler = _serviceProvider.GetService<CommandsHandler>()!;
        _moreCommandHandler = _serviceProvider.GetService<MoreCommandsHandler>()!;

        try
        {
            fpdfview.FPDF_InitLibrary();

            var rootCommand = new RootCommand($"DotNet.Pdf {Version} [merge, split, convert, extract PDF documents and pages]");

            // Configure commands
            ConfigureSplitCommand(rootCommand);
            ConfigureMergeCommand(rootCommand);
            ConfigureConvertCommand(rootCommand);
            ConfigureImageToPdfCommand(rootCommand);
            ConfigureTextCommand(rootCommand);
            ConfigureBookmarksCommand(rootCommand);
            ConfigureInfoCommand(rootCommand);
            ConfigureRotateCommand(rootCommand);
            ConfigureRemoveCommand(rootCommand);
            ConfigureInsertCommand(rootCommand);
            ConfigureReorderCommand(rootCommand);
            ConfigureListAttachmentsCommand(rootCommand);
            ConfigureExtractAttachmentsCommand(rootCommand);
            ConfigureListPageObjectsCommand(rootCommand);
            ConfigureListFormFieldsCommand(rootCommand);
            ConfigureWatermarkCommand(rootCommand);

            rootCommand.SetHandler(() =>
            {
                Console.WriteLine($"DotNet.Pdf  {Version} [merge, split, convert, extract PDF documents and pages]");
            });

            int retCode = await rootCommand.InvokeAsync(args);
            return retCode;
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Fatal error occurred during application execution");
            Console.WriteLine($"Fatal error: {ex.Message}");
            return -1;
        }
        finally
        {
            try
            {
                fpdfview.FPDF_DestroyLibrary();
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<Program>>();
                logger?.LogError(ex, "Error destroying PDFium library");
            }
            finally
            {
                await _serviceProvider.DisposeAsync();
            }
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<CommandsHandler>();
        services.AddSingleton<MoreCommandsHandler>();
        return services.BuildServiceProvider();
    }

    #region Command Configuration Methods

    private static void ConfigureSplitCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var directoryOption = new Option<DirectoryInfo?>(
            name: "--output",
            description: "Output Directory");

        var rangeOption = new Option<string?>(
            name: "--range",
            description: "page range");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var outputNameOption = new Option<string?>(
            name: "--names",
            description: "output filenames, ex: {original}-{page}");

        var useBookmarksOption = new Option<bool>(
            name: "--use-bookmarks",
            description: "use bookmarks for names", getDefaultValue: () => false);

        var outputScriptOption = new Option<FileInfo?>(
            name: "--output-script",
            description: "text file with list of files to split to, separated by newlines");

        var splitCommand = new Command("split", "Split PDF");
        splitCommand.AddOption(fileOption);
        splitCommand.AddOption(directoryOption);
        splitCommand.AddOption(rangeOption);
        splitCommand.AddOption(passwordOption);
        splitCommand.AddOption(useBookmarksOption);
        splitCommand.AddOption(outputNameOption);
        splitCommand.AddOption(outputScriptOption);

        splitCommand.SetHandler((input, outputDirectory, rangeOption, password, useBookmarks, outputNames, outputScript) =>
        {
            _commandHandler.SplitPdf(input, outputDirectory, rangeOption, password, useBookmarks, outputNames, outputScript);
        }, fileOption, directoryOption, rangeOption, passwordOption, useBookmarksOption, outputNameOption, outputScriptOption);

        rootCommand.AddCommand(splitCommand);
    }

    private static void ConfigureMergeCommand(RootCommand rootCommand)
    {
        var recursiveOption = new Option<bool>(
            name: "--recursive",
            description: "Look for PDFs recursively", getDefaultValue: () => false);

        var fileInputsOption = new Option<FileInfo[]?>(
            name: "--input",
            description: "multiple filenames to merge");

        var inputDirectoryOption = new Option<DirectoryInfo?>(
            name: "--input-directory",
            description: "merge all files inside this directory");

        var outputFileOption = new Option<FileInfo>(
            name: "--output",
            description: "output pdf filename");

        var inputScriptOption = new Option<FileInfo?>(
            name: "--input-script",
            description: "text file with list of files to merge, separated by newlines");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password for input files");

        var mergeCommand = new Command("merge", "Merge PDFs");
        mergeCommand.AddOption(fileInputsOption);
        mergeCommand.AddOption(outputFileOption);
        mergeCommand.AddOption(inputScriptOption);
        mergeCommand.AddOption(inputDirectoryOption);
        mergeCommand.AddOption(recursiveOption);
        mergeCommand.AddOption(passwordOption);

        mergeCommand.SetHandler((inputFilenames, outputFilename, inputScript, inputDirectory, useRecursive, password) =>
        {
            _commandHandler.MergePdfs(inputFilenames, outputFilename, inputScript, inputDirectory, useRecursive, password);
        }, fileInputsOption, outputFileOption, inputScriptOption, inputDirectoryOption, recursiveOption, passwordOption);

        rootCommand.AddCommand(mergeCommand);
    }

    private static void ConfigureConvertCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var directoryOption = new Option<DirectoryInfo?>(
            name: "--output",
            description: "Output Directory");

        var rangeOption = new Option<string?>(
            name: "--range",
            description: "page range");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var outputNameOption = new Option<string?>(
            name: "--names",
            description: "output filenames, ex: {original}-{page}");

        var dpiOption = new Option<int>(
            name: "--dpi",
            description: "output dpi", getDefaultValue: () => 200);

        var encoderOption = new Option<string>(
            name: "--encoder",
            description: "encoder [.png, .jpg, .gif, .bmp]", getDefaultValue: () => ".jpg");

        var convertCommand = new Command("convert", "Convert PDF to images");
        convertCommand.AddOption(fileOption);
        convertCommand.AddOption(directoryOption);
        convertCommand.AddOption(rangeOption);
        convertCommand.AddOption(passwordOption);
        convertCommand.AddOption(encoderOption);
        convertCommand.AddOption(dpiOption);
        convertCommand.AddOption(outputNameOption);

        convertCommand.SetHandler((input, outputDirectory, rangeOption, password, outputNames, dpi, encoder) =>
        {
            _commandHandler.ConvertPdfToImages(input, outputDirectory, rangeOption, password, outputNames, dpi, encoder);
        }, fileOption, directoryOption, rangeOption, passwordOption, outputNameOption, dpiOption, encoderOption);

        rootCommand.AddCommand(convertCommand);
    }

    private static void ConfigureImageToPdfCommand(RootCommand rootCommand)
    {
        var inputImageOption = new Option<FileInfo>(
            name: "--input",
            description: "input image filename");

        var outputFileOption = new Option<FileInfo?>(
            name: "--output",
            description: "output pdf filename");

        var imageCommand = new Command("imagetopdf", "Convert image to PDF");
        imageCommand.AddOption(inputImageOption);
        imageCommand.AddOption(outputFileOption);

        imageCommand.SetHandler((input, output) =>
        {
            _commandHandler.ConvertImageToPdf(input, output);
        }, inputImageOption, outputFileOption);

        rootCommand.AddCommand(imageCommand);
    }

    private static void ConfigureTextCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var rangeOption = new Option<string?>(
            name: "--range",
            description: "page range");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var outputFormatOption = new Option<string>(
            name: "--format",
            description: "output format [text json xml]", getDefaultValue: () => "text");

        var textCommand = new Command("text", "Extract PDF Text");
        textCommand.AddOption(fileOption);
        textCommand.AddOption(rangeOption);
        textCommand.AddOption(passwordOption);
        textCommand.AddOption(outputFormatOption);

        textCommand.SetHandler((input, rangeOption, password, outputFormat) =>
        {
            _commandHandler.ExtractText(input, rangeOption, password, outputFormat);
        }, fileOption, rangeOption, passwordOption, outputFormatOption);

        rootCommand.AddCommand(textCommand);
    }

    private static void ConfigureBookmarksCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var outputFormatOption = new Option<string>(
            name: "--format",
            description: "output format [text json xml]", getDefaultValue: () => "text");

        var bookmarksCommand = new Command("bookmarks", "Extract PDF Bookmarks (outlines)");
        bookmarksCommand.AddOption(fileOption);
        bookmarksCommand.AddOption(passwordOption);
        bookmarksCommand.AddOption(outputFormatOption);

        bookmarksCommand.SetHandler((input, password, outputFormat) =>
        {
            _commandHandler.ExtractBookmarks(input, password, outputFormat);
        }, fileOption, passwordOption, outputFormatOption);

        rootCommand.AddCommand(bookmarksCommand);
    }

    private static void ConfigureInfoCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var outputFormatOption = new Option<string>(
            name: "--format",
            description: "output format [text json xml]", getDefaultValue: () => "text");

        var infoCommand = new Command("info", "Extract PDF Information");
        infoCommand.AddOption(fileOption);
        infoCommand.AddOption(passwordOption);
        infoCommand.AddOption(outputFormatOption);

        infoCommand.SetHandler((input, password, outputFormat) =>
        {
            _commandHandler.GetPdfInfo(input, password, outputFormat);
        }, fileOption, passwordOption, outputFormatOption);

        rootCommand.AddCommand(infoCommand);
    }

    private static void ConfigureRotateCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var outputFileOption = new Option<FileInfo>(
            name: "--output",
            description: "output pdf filename");

        var rangeOption = new Option<string?>(
            name: "--range",
            description: "page range to rotate");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var rotationOption = new Option<int>(
            name: "--rotation",
            description: "rotation angle (90, 180, 270 degrees)", getDefaultValue: () => 90);

        var rotateCommand = new Command("rotate", "Rotate PDF pages");
        rotateCommand.AddOption(fileOption);
        rotateCommand.AddOption(outputFileOption);
        rotateCommand.AddOption(rangeOption);
        rotateCommand.AddOption(passwordOption);
        rotateCommand.AddOption(rotationOption);

        rotateCommand.SetHandler((input, output, range, password, rotation) =>
        {
            _commandHandler.RotatePdfPages(input, output, rotation, range, password);
        }, fileOption, outputFileOption, rangeOption, passwordOption, rotationOption);

        rootCommand.AddCommand(rotateCommand);
    }

    private static void ConfigureRemoveCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var outputFileOption = new Option<FileInfo>(
            name: "--output",
            description: "output pdf filename");

        var pagesOption = new Option<string>(
            name: "--pages",
            description: "comma-separated list of page numbers to remove (e.g., 1,3,5)");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var removeCommand = new Command("remove", "Remove pages from PDF");
        removeCommand.AddOption(fileOption);
        removeCommand.AddOption(outputFileOption);
        removeCommand.AddOption(pagesOption);
        removeCommand.AddOption(passwordOption);

        removeCommand.SetHandler((input, output, pages, password) =>
        {
            _commandHandler.RemovePdfPages(input, output, pages, password);
        }, fileOption, outputFileOption, pagesOption, passwordOption);

        rootCommand.AddCommand(removeCommand);
    }

    private static void ConfigureInsertCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var outputFileOption = new Option<FileInfo>(
            name: "--output",
            description: "output pdf filename");

        var positionsOption = new Option<string>(
            name: "--positions",
            description: "insert positions in format 'position:count' (e.g., 1:2,5:1 to insert 2 pages at position 1 and 1 page at position 5)");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var widthOption = new Option<double>(
            name: "--width",
            description: "width of blank pages in points", getDefaultValue: () => 612.0);

        var heightOption = new Option<double>(
            name: "--height",
            description: "height of blank pages in points", getDefaultValue: () => 792.0);

        var insertCommand = new Command("insert", "Insert blank pages into PDF");
        insertCommand.AddOption(fileOption);
        insertCommand.AddOption(outputFileOption);
        insertCommand.AddOption(positionsOption);
        insertCommand.AddOption(passwordOption);
        insertCommand.AddOption(widthOption);
        insertCommand.AddOption(heightOption);

        insertCommand.SetHandler((input, output, positions, password, width, height) =>
        {
            _commandHandler.InsertBlankPages(input, output, positions, width, height, password);
        }, fileOption, outputFileOption, positionsOption, passwordOption, widthOption, heightOption);

        rootCommand.AddCommand(insertCommand);
    }

    private static void ConfigureReorderCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var outputFileOption = new Option<FileInfo>(
            name: "--output",
            description: "output pdf filename");

        var orderOption = new Option<string>(
            name: "--order",
            description: "comma-separated list of page numbers in new order (e.g., 3,1,2,4)");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var reorderCommand = new Command("reorder", "Reorder PDF pages");
        reorderCommand.AddOption(fileOption);
        reorderCommand.AddOption(outputFileOption);
        reorderCommand.AddOption(orderOption);
        reorderCommand.AddOption(passwordOption);

        reorderCommand.SetHandler((input, output, order, password) =>
        {
            _commandHandler.ReorderPdfPages(input, output, order, password);
        }, fileOption, outputFileOption, orderOption, passwordOption);

        rootCommand.AddCommand(reorderCommand);
    }

    private static void ConfigureListAttachmentsCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var outputFormatOption = new Option<string>(
            name: "--format",
            description: "output format: text, json", getDefaultValue: () => "text");

        var listAttachmentsCommand = new Command("list-attachments", "List PDF attachments");
        listAttachmentsCommand.AddOption(fileOption);
        listAttachmentsCommand.AddOption(passwordOption);
        listAttachmentsCommand.AddOption(outputFormatOption);

        listAttachmentsCommand.SetHandler((input, password, outputFormat) =>
        {
            _commandHandler.ListAttachments(input, password, outputFormat);
        }, fileOption, passwordOption, outputFormatOption);

        rootCommand.AddCommand(listAttachmentsCommand);
    }

    private static void ConfigureExtractAttachmentsCommand(RootCommand rootCommand)
    {
        var fileOption = new Option<FileInfo>(
            name: "--input",
            description: "input pdf filename");

        var outputDirectoryOption = new Option<DirectoryInfo?>(
            name: "--output",
            description: "output directory for extracted attachments");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "pdf password");

        var indexOption = new Option<string?>(
            name: "--index",
            description: "extract specific attachment by index (0-based). If not specified, all attachments are extracted");

        var extractAttachmentsCommand = new Command("extract-attachments", "Extract PDF attachments");
        extractAttachmentsCommand.AddOption(fileOption);
        extractAttachmentsCommand.AddOption(outputDirectoryOption);
        extractAttachmentsCommand.AddOption(passwordOption);
        extractAttachmentsCommand.AddOption(indexOption);

        extractAttachmentsCommand.SetHandler((input, outputDirectory, password, index) =>
        {
            _commandHandler.ExtractAttachments(input, outputDirectory, password, index);
        }, fileOption, outputDirectoryOption, passwordOption, indexOption);

        rootCommand.AddCommand(extractAttachmentsCommand);
    }

    private static void ConfigureListPageObjectsCommand(RootCommand rootCommand)
    {
        var inputOption = new Option<FileInfo>("--input", "Input PDF file") { IsRequired = true };
        var rangeOption = new Option<string?>(
            name: "--range",
            description: "page range");

        var passwordOption = new Option<string?>("--password", "PDF password");
        var formatOption = new Option<string?>("--format", description: "Output format (text, json)", getDefaultValue: () => "text");
        
        var command = new Command("list-objects", "List all objects on a page");
        command.AddOption(inputOption);
        command.AddOption(rangeOption);
        command.AddOption(passwordOption);
        command.AddOption(formatOption);
        
        command.SetHandler((input, range, password, format) => 
            _moreCommandHandler.ListPageObjects(input, range, password, format ?? string.Empty),
            inputOption, rangeOption, passwordOption, formatOption);
        
        rootCommand.AddCommand(command);
    }
    
    private static void ConfigureListFormFieldsCommand(RootCommand rootCommand)
    {
        var inputOption = new Option<FileInfo>("--input", "Input PDF file") { IsRequired = true };
        var passwordOption = new Option<string?>("--password", "PDF password");
        var formatOption = new Option<string>("--format", description: "Output format (text, json)", getDefaultValue: () => "text");

        var command = new Command("list-forms", "List all form fields in a document");
        command.AddOption(inputOption);
        command.AddOption(passwordOption);
        command.AddOption(formatOption);

        command.SetHandler((input, password, format) => 
            _moreCommandHandler.ListFormFields(input, password, format),
            inputOption, passwordOption, formatOption);
        
        rootCommand.AddCommand(command);
    }

    private static void ConfigureWatermarkCommand(RootCommand rootCommand)
    {
        var inputOption = new Option<FileInfo>("--input", "Input PDF file") { IsRequired = true };
        var outputOption = new Option<FileInfo>("--output", "Output PDF file") { IsRequired = true };
        var passwordOption = new Option<string?>("--password", "PDF password");

        var textOption = new Option<string?>("--text", "Watermark text");
        var imageOption = new Option<FileInfo?>("--image", "Watermark image path");
        var fontOption = new Option<string>("--font", description: "Font name for text watermark", getDefaultValue:() => "Helvetica");
        var fontSizeOption = new Option<double>("--font-size", description: "Font size for text watermark", getDefaultValue: () => 50.0);
        var colorOption = new Option<string>("--color", description: "Color for text watermark (R,G,B)", getDefaultValue: () => "128,128,128");
        var opacityOption = new Option<byte>("--opacity", description: "Opacity from 0 (transparent) to 255 (opaque)", getDefaultValue: () => 50);
        var rotationOption = new Option<double>("--rotation", description: "Rotation angle in degrees", getDefaultValue: () => 45.0);
        var scaleOption = new Option<double>("--scale", description: "Scale factor for image watermark", getDefaultValue: () => 1.0);

        var command = new Command("watermark", "Add a text or image watermark");
        command.AddOption(inputOption);
        command.AddOption(outputOption);
        command.AddOption(passwordOption);
        command.AddOption(textOption);
        command.AddOption(imageOption);
        command.AddOption(fontOption);
        command.AddOption(fontSizeOption);
        command.AddOption(colorOption);
        command.AddOption(opacityOption);
        command.AddOption(rotationOption);
        command.AddOption(scaleOption);
        
        command.SetHandler((InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var password = context.ParseResult.GetValueForOption(passwordOption);
            var text = context.ParseResult.GetValueForOption(textOption);
            var image = context.ParseResult.GetValueForOption(imageOption);
            var font = context.ParseResult.GetValueForOption(fontOption)!;
            var fontSize = context.ParseResult.GetValueForOption(fontSizeOption);
            var color = context.ParseResult.GetValueForOption(colorOption)!;
            var opacity = context.ParseResult.GetValueForOption(opacityOption);
            var rotation = context.ParseResult.GetValueForOption(rotationOption);
            var scale = context.ParseResult.GetValueForOption(scaleOption);

            _moreCommandHandler.AddWatermark(input, output, password, text, image, font, fontSize, color, opacity, rotation, scale);
        });
        
        rootCommand.AddCommand(command);
    }    

    #endregion


}