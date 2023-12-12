using System.CommandLine;
using System.Text.Json;
using PDFiumCore;
using SixLabors.ImageSharp.Processing;

namespace dotnet.pdf;


class Program
{
    private const string Version = "v0.3";
    static async Task<int> Main(string[] args)
    {
        fpdfview.FPDF_InitLibrary();
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
            description: "use bookmarks for names", getDefaultValue:() => false);           
        
        var outputScriptOption = new Option<FileInfo?>(
            name: "--output-script",
            description: "text file with list of files to split to, separated by newlines");   
        
        var rootCommand = new RootCommand($"DotNet.Pdf {Version} [merge, split, convert, extract PDF documents and pages]");


        var splitCommand = new Command("split", "Split PDF");
        splitCommand.AddOption(fileOption);
        splitCommand.AddOption(directoryOption);
        splitCommand.AddOption(rangeOption);
        splitCommand.AddOption(passwordOption);
        splitCommand.AddOption(useBookmarksOption);
        splitCommand.AddOption(outputNameOption);
        splitCommand.AddOption(outputScriptOption);
        splitCommand.SetHandler(SplitCommand, fileOption, directoryOption, rangeOption, 
            passwordOption, useBookmarksOption, outputNameOption, outputScriptOption);
        
        var recursiveOption = new Option<bool>(
            name: "--recursive",
            description: "Look for PDFs recursively", getDefaultValue:() => false);  
        
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
        
        var mergeCommand = new Command("merge", "Merge PDFs");
        mergeCommand.AddOption(fileInputsOption);
        mergeCommand.AddOption(outputFileOption);
        mergeCommand.AddOption(inputScriptOption);
        mergeCommand.AddOption(inputDirectoryOption);
        mergeCommand.AddOption(recursiveOption);
        mergeCommand.SetHandler(MergeCommand, fileInputsOption, outputFileOption, inputScriptOption, 
            inputDirectoryOption, recursiveOption);
        
        
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
        convertCommand.SetHandler(ConvertCommand, fileOption, directoryOption, rangeOption, passwordOption, 
            outputNameOption, dpiOption, encoderOption);
        
        
        
        var inputImageOption = new Option<FileInfo>(
            name: "--input",
            description: "input image filename");
        
        var imageCommand = new Command("imagetopdf", "Convert image to PDF");
        imageCommand.AddOption(inputImageOption);
        imageCommand.AddOption(outputFileOption);
        imageCommand.SetHandler(ImageToPdfCommand, inputImageOption, outputFileOption);
        
        var outputFormatOption = new Option<string>(
            name: "--format",
            description: "output format [text json xml]", getDefaultValue: () => "text");
        
        var textCommand = new Command("text", "Extract PDF Text");
        textCommand.AddOption(fileOption);
        textCommand.AddOption(rangeOption);
        textCommand.AddOption(passwordOption);
        textCommand.AddOption(outputFormatOption);
        textCommand.SetHandler(TextCommand, fileOption, rangeOption, passwordOption, outputFormatOption);
        
        var bookmarksCommand = new Command("bookmarks", "Extract PDF Bookmarks (outlines)");
        bookmarksCommand.AddOption(fileOption);
        bookmarksCommand.AddOption(passwordOption);
        bookmarksCommand.AddOption(outputFormatOption);
        bookmarksCommand.SetHandler(BookmarksCommand, fileOption, passwordOption, outputFormatOption);
        
        var infoCommand = new Command("info", "Extract PDF Information");
        infoCommand.AddOption(fileOption);
        infoCommand.AddOption(passwordOption);
        infoCommand.AddOption(outputFormatOption);
        infoCommand.SetHandler(InfoCommand, fileOption, passwordOption, outputFormatOption);
        
        rootCommand.AddCommand(splitCommand);
        rootCommand.AddCommand(mergeCommand);
        rootCommand.AddCommand(convertCommand);
        rootCommand.AddCommand(imageCommand);
        rootCommand.AddCommand(textCommand);
        rootCommand.AddCommand(bookmarksCommand);
        rootCommand.AddCommand(infoCommand);
        rootCommand.SetHandler(RootCommand);

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Retrieves information from a PDF file and displays it in the console or as a JSON string.
    /// </summary>
    /// <param name="input">The input file to extract information from.</param>
    /// <param name="password">Optional password for encrypted PDF file. Pass null or empty string if PDF is not password protected.</param>
    /// <param name="outputFormat">The format in which to display the information. Valid values are "text" or "json".</param>
    private static void InfoCommand(FileInfo input, string? password, string outputFormat)
    {
        if (input.Exists)
        {
            
            var pdfInfo = Pdfium.GetPdfInformation(input.FullName, password ?? "");

            if (pdfInfo is not null)
            {
                if (outputFormat == "text")
                {
                    Console.WriteLine($"Pages = {pdfInfo?.Pages}");
                    Console.WriteLine($"Author = {pdfInfo?.Author}");
                    Console.WriteLine($"CreationDate = {pdfInfo?.CreationDate}");
                    Console.WriteLine($"Creator = {pdfInfo?.Creator}");
                    Console.WriteLine($"Keywords = {pdfInfo?.Keywords}");
                    Console.WriteLine($"Producer = {pdfInfo?.Producer}");
                    Console.WriteLine($"ModifiedDate = {pdfInfo?.ModifiedDate}");
                    Console.WriteLine($"Subject = {pdfInfo?.Subject}");
                    Console.WriteLine($"Title = {pdfInfo?.Title}");
                    Console.WriteLine($"Version = {pdfInfo?.Version}");
                    Console.WriteLine($"Trapped = {pdfInfo?.Trapped}");
                }
                
                if (outputFormat == "json")
                {
                    var jsonString = JsonSerializer.Serialize(
                        pdfInfo!, SourceGenerationContext.Default.PdfInfo);
                    Console.WriteLine(jsonString);
                }
            }
        }
    }

    /// <summary>
    /// Extracts bookmarks from a PDF file and outputs them in the specified format.
    /// </summary>
    /// <param name="input">The PDF file to extract bookmarks from.</param>
    /// <param name="password">The password for the PDF file. If no password is required, pass null.</param>
    /// <param name="outputFormat">The output format for the extracted bookmarks. Valid values are "text" and "json".</param>
    private static void BookmarksCommand(FileInfo input, string? password, string outputFormat)
    {
        if (input.Exists)
        {
            var bookmarks = Pdfium.GetPdfBookmarks(input.FullName, password ?? "");
            if (bookmarks is not null)
            {
                if (outputFormat == "text")
                {
                    foreach (var bookmark in bookmarks)
                    {
                        Console.WriteLine($"{bookmark.Level}> {bookmark.Title}\t[{bookmark.Action} {bookmark.Page}]");
                    }
                }

                if (outputFormat == "json")
                {
                    var jsonString = JsonSerializer.Serialize(
                        bookmarks!, SourceGenerationContext.Default.ListPDfBookmark);
                    Console.WriteLine(jsonString);
                }
                //if format == text
                
            }
        }
    }

    /// <summary>
    /// Extracts Text from a PDF file and outputs them in the specified format.
    /// </summary>
    /// <param name="input">The input file.</param>
    /// <param name="rangeOption">The range option for parsing text.</param>
    /// <param name="password">The password used to open the input file, if required.</param>
    /// <param name="outputFormat">The output format ('text' or 'json').</param>
    private static void TextCommand(FileInfo input, string? rangeOption, string? password, string outputFormat)
    {
        if (input.Exists)
        {
            var parsedRange = Parsers.ParsePageRange(rangeOption);
            var texts = Pdfium.GetPdfText(input.FullName, parsedRange, password ?? "");
            
            //if format == text
            
            if (texts is not null)
            {
                if (outputFormat == "text")
                {
                    foreach (var text in texts)
                    {
                        Console.WriteLine(text.Text);
                    }
                }
                
                if (outputFormat == "json")
                {
                    var jsonString = JsonSerializer.Serialize(
                        texts!, SourceGenerationContext.Default.ListPDfPageText);
                    Console.WriteLine(jsonString);
                }

                
            }
            
        }
    }

    /// <summary>
    /// Converts an image file to a PDF file 
    /// </summary>
    /// <param name="input">The input image file to convert.</param>
    /// <param name="output">Optional. The output PDF file. If not provided, the default output location will be used.</param>
    /// /
    private static void ImageToPdfCommand(FileInfo input, FileInfo? output)
    {
        if (input.Exists)
        {
            Pdfium.ConvertImageToPdf(input.FullName, output?.FullName);
        }
        
    }

    /// <summary>
    /// Splits a PDF file into multiple files based on the given parameters.
    /// </summary>
    /// <param name="input">The input PDF file to split.</param>
    /// <param name="outputDirectory">The directory to save the splitted files. If null, the same directory as the input file will be used.</param>
    /// <param name="rangeOption">The range option specifying which pages to split. null to split all pages.</param>
    /// <param name="password">The password to unlock the input PDF file. If not provided, an empty string will be used.</param>
    /// <param name="useBookmarks">true to split using bookmarks, false otherwise.</param>
    /// <param name="outputNames">The output file names to be used for splitted files. If null, default names will be used.</param>
    private static void SplitCommand(FileInfo input, DirectoryInfo? outputDirectory, string? rangeOption, 
        string? password, bool useBookmarks, string? outputNames, FileInfo? outputScript)
    {
        var progress = new Progress<PdfSplitProgress>(progress =>
        {
            Console.WriteLine($"{progress.Current+1}/{progress.Max}\t{progress.Filename}");
        });

        Dictionary<int, string> outputScriptNames = new(); 
        
        if (outputScript?.Exists ?? false)
        {
            int i = 1;
            foreach (var line in File.ReadAllLines(outputScript.FullName))
            {
                string filename = string.Empty;
                if (line.Contains('='))
                {
                    string[] parts = line.Split('=');
                    if (int.TryParse(parts[0], out int pageNumber) && parts.Length > 1)
                    {
                        outputScriptNames.TryAdd(pageNumber, parts[1]);
                        i = pageNumber;
                    }
                    
                } 
                else
                {
                    outputScriptNames.TryAdd(i, line);
                }
                i++;
            }
        }

        var outputDir = outputDirectory ?? input.Directory;
        if (outputDir is not null)
        {
            var parsedRange = Parsers.ParsePageRange(rangeOption);
            Pdfium.SplitPdf(input.FullName, outputDir.FullName, password: password ?? "",
                outputName: outputNames, useBookmarks: useBookmarks, progress: progress, pageRange: parsedRange,
                outputScriptNames: outputScriptNames);
        }
    }

    /// <summary>
    /// Converts a PDF file to an image using the specified parameters.
    /// </summary>
    /// <param name="input">The input file.</param>
    /// <param name="outputDirectory">The output directory for the generated images. If null, the input file's directory will be used.</param>
    /// <param name="rangeOption">The page range option.</param>
    /// <param name="password">The password to decrypt the PDF file. If null, no password is used.</param>
    /// <param name="outputNames">The name of the output image files. If null or empty, default names will be used.</param>
    /// <param name="dpi">The resolution in dots per inch for the generated images.</param>
    /// <param name="encoder">The name of the image encoder. If null or empty, the default encoder will be used.</param>
    private static void ConvertCommand(FileInfo input, DirectoryInfo? outputDirectory, string? rangeOption, 
        string? password,string? outputNames, int dpi, string encoder)
    {
        

        var outputDir = outputDirectory ?? input.Directory;
        if (outputDir is not null)
        {
            var pImageEncoder = IoUtils.GetEncoder(encoder);
            
            var parsedRange = Parsers.ParsePageRange(rangeOption);
            if (!string.IsNullOrEmpty(outputNames))
            {
                var ext = Path.GetExtension(outputNames);
                if (!string.IsNullOrEmpty(ext))
                {
                    pImageEncoder = IoUtils.GetEncoder(ext);
                    
                }
            }
            
            var progress = new Progress<PdfProgress>(progress =>
            {
                Console.WriteLine($"{progress.Current+1}/{progress.Max}\t Encoding");
            });
            Pdfium.ConvertPdfToImage(input.FullName, outputDir.FullName, pImageEncoder, dpi, 
                parsedRange, password ?? "", outputNames,progress);
             
        }
    }

    /// <summary>
    /// Merges PDF files according to the specified parameters.
    /// </summary>
    /// <param name="inputFilenames">An optional array of input PDF file names to merge.</param>
    /// <param name="outputFilename">The output PDF file name to save the merged result.</param>
    /// <param name="inputScript">An optional input file containing a list of PDF file names to merge.</param>
    /// <param name="inputDirectory">An optional input directory to search for PDF files to merge.</param>
    /// <param name="useRecursive">A boolean flag indicating whether to recursively search in the input directory.</param>
    private static void MergeCommand(FileInfo[]? inputFilenames, FileInfo? outputFilename, FileInfo? inputScript,
        DirectoryInfo? inputDirectory, bool useRecursive)
    {
         
        List<string> filenames = new();
        if (inputFilenames is not null)
        {
            foreach (var file in  inputFilenames) filenames.Add(file.FullName);
        }

        if (inputDirectory is not null)
        {
            var searchOptions = useRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFileSystemEntries(inputDirectory.FullName,
                         "*.pdf", searchOptions))
            {
                filenames.Add(file);
            }
        }

        if (inputScript?.Exists ?? false)
        {
            foreach (var line in File.ReadAllLines(inputScript.FullName))
            {
                if (File.Exists(line) && Path.GetExtension(line) == ".pdf")
                {
                    filenames.Add(line);
                }
            }
        }


        if (outputFilename != null)
        {
            if (!outputFilename.Exists)
            {
                var progress = new Progress<PdfProgress>(progress =>
                {
                    Console.WriteLine($"{progress.Current + 1}/{progress.Max}\t Merge Progress");
                });
                Pdfium.MergeFiles(outputFilename.FullName, filenames, "", deleteOriginal: false, progress: progress);
            }
            else
            {
                Console.WriteLine($"{outputFilename.FullName} Already exists, specify another location");
            }
        }
        else
        {
            Console.WriteLine($"Missing output filename --output");
        }



        //public static void MergeFiles(string outputFilename, List<string> inputFilenames, string password = "",
        //    bool deleteOriginal = false,
        //    IProgress<PdfProgress>? progress = null)


    }

    /// <summary>
    /// Displays DotNet.Pdf application text
    /// </summary>
    private static void RootCommand()
    {
        Console.WriteLine($"DotNet.Pdf  {Version} [merge, split, convert, extract PDF documents and pages]");
    }
}