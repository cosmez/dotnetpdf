# Migration Guide: v1.x to v2.0

This guide helps developers migrate from the old monolithic PDF classes to the new DotNet.Pdf.Core library architecture.

## Overview

Version 2.0 introduces a complete refactoring of PDF functionality from monolithic partial classes into a well-organized class library with proper separation of concerns.

## What Changed

### Before (v1.x)
```csharp
// Old way - static methods on partial classes
var texts = Pdfium.GetPdfText(filename, pageRange, password);
var bookmarks = Pdfium.GetPdfBookmarks(filename, password);
var info = Pdfium.GetPdfInformation(filename, password);
var attachments = Pdfium.GetPdfAttachments(filename, password);
bool success = Pdfium.ExtractAttachment(filename, index, outputPath, password);
```

### After (v2.0)
```csharp
// New way - service-oriented architecture
using DotNet.Pdf.Core;
using Microsoft.Extensions.Logging;

// Create processor (typically done via DI)
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var pdfProcessor = new PdfProcessor(loggerFactory);

// Use the processor
var texts = pdfProcessor.GetPdfText(filename, pageRange, password);
var bookmarks = pdfProcessor.GetPdfBookmarks(filename, password);
var info = pdfProcessor.GetPdfInformation(filename, password);
var attachments = pdfProcessor.GetPdfAttachments(filename, password);
bool success = pdfProcessor.ExtractAttachment(filename, index, outputPath, password);
```

## Migration Steps

### Step 1: Add Package Reference
Add reference to the new class library in your project file:

```xml
<ProjectReference Include="path\to\DotNet.Pdf.Core\DotNet.Pdf.Core.csproj" />
```

### Step 2: Update Using Statements
```csharp
// Add these using statements
using DotNet.Pdf.Core;
using DotNet.Pdf.Core.Models;
using Microsoft.Extensions.Logging;
```

### Step 3: Replace Static Calls

| Old Static Method | New Service Method | Service Class |
|------------------|-------------------|---------------|
| `Pdfium.GetPdfText()` | `pdfProcessor.GetPdfText()` | PdfTextExtractionService |
| `Pdfium.GetPdfBookmarks()` | `pdfProcessor.GetPdfBookmarks()` | PdfBookmarkService |
| `Pdfium.GetPdfInformation()` | `pdfProcessor.GetPdfInformation()` | PdfInformationService |
| `Pdfium.GetPdfAttachments()` | `pdfProcessor.GetPdfAttachments()` | PdfAttachmentService |
| `Pdfium.ExtractAttachment()` | `pdfProcessor.ExtractAttachment()` | PdfAttachmentService |

### Step 4: Progress Reporting Support
All long-running operations now support progress reporting using `IProgress<T>`:

```csharp
// Text extraction with progress
var progress = new Progress<PdfTextProgress>(p => 
{
    Console.WriteLine($"Processing page {p.Current}/{p.Max}: {p.PageText.PageNumber}");
});
var textPages = pdfProcessor.GetPdfText("document.pdf", progress: progress);

// Bookmark extraction with progress
var bookmarkProgress = new Progress<PdfBookmarkProgress>(p => 
{
    Console.WriteLine($"Processing bookmark {p.Current}/{p.Max}: {p.PageText.Title}");
});
var bookmarks = pdfProcessor.GetPdfBookmarks("document.pdf", progress: bookmarkProgress);

// Attachment operations with progress
var attachmentProgress = new Progress<PdfProgress>(p => 
{
    Console.WriteLine($"Processing {p.Current}/{p.Max}");
});
var attachments = pdfProcessor.GetPdfAttachments("document.pdf", progress: attachmentProgress);

// Page object analysis with progress
var pageProgress = new Progress<PdfProgress>(p => 
{
    Console.WriteLine($"Analyzing page {p.Current}/{p.Max}");
});
var objects = pdfProcessor.ListPageObjects("document.pdf", progress: pageProgress);

// Form field analysis with progress
var formProgress = new Progress<PdfProgress>(p => 
{
    Console.WriteLine($"Processing page {p.Current}/{p.Max}");
});
var fields = pdfProcessor.ListFormFields("document.pdf", progress: formProgress);

// Watermark operations with progress
var watermarkProgress = new Progress<PdfProgress>(p => 
{
    Console.WriteLine($"Adding watermark to page {p.Current}/{p.Max}");
});
pdfProcessor.AddWatermark("input.pdf", "output.pdf", options, progress: watermarkProgress);
```

### Step 6: Update Model Namespaces
Models have moved to the new namespace:

```csharp
// Old namespace
dotnet.pdf.PdfInfo
dotnet.pdf.PDfBookmark
dotnet.pdf.PDfPageText

// New namespace  
DotNet.Pdf.Core.Models.PdfInfo
DotNet.Pdf.Core.Models.PDfBookmark
DotNet.Pdf.Core.Models.PDfPageText
```

### Step 7: Update JSON Serialization
```csharp
// Old way
JsonSerializer.Serialize(data, SourceGenerationContext.Default.PdfInfo);

// New way
JsonSerializer.Serialize(data, DotNet.Pdf.Core.Models.SourceGenerationContext.Default.PdfInfo);
```

## Dependency Injection Setup

### ASP.NET Core / Generic Host
```csharp
services.AddSingleton<PdfProcessor>(provider =>
{
    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    return new PdfProcessor(loggerFactory);
});
```

### Console Application
```csharp
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddSingleton<PdfProcessor>(provider =>
{
    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    return new PdfProcessor(loggerFactory);
});

var serviceProvider = services.BuildServiceProvider();
var pdfProcessor = serviceProvider.GetRequiredService<PdfProcessor>();
```

## Progress Reporting Types

The new library provides several progress types for different operations:

### PdfProgress
Used for general operations like attachment processing, page object analysis, form field analysis, and watermarking:
```csharp
public record PdfProgress(int Current, int Max);
```

### PdfTextProgress  
Used specifically for text extraction operations:
```csharp
public record PdfTextProgress(int Current, int Max, PDfPageText PageText);
```

### PdfBookmarkProgress
Used specifically for bookmark extraction operations:
```csharp
public record PdfBookmarkProgress(int Current, int Max, PDfBookmark PageText); 
```

### PdfSplitProgress
Used for PDF splitting operations (available in extended functionality):
```csharp
public record PdfSplitProgress(int Current, int Max, string Filename);
```

## Advanced Usage: Direct Service Access

If you need more control, you can use services directly:

```csharp
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Use individual services
var textService = new PdfTextExtractionService(
    loggerFactory.CreateLogger<PdfTextExtractionService>());
var bookmarkService = new PdfBookmarkService(
    loggerFactory.CreateLogger<PdfBookmarkService>());

var texts = textService.ExtractText(filename, pageRange, password);
var bookmarks = bookmarkService.GetBookmarks(filename, password);
```

## Benefits of Migration

1. **Better Architecture** - Clean separation of concerns
2. **Improved Testing** - Services can be easily mocked and tested
3. **Better Error Handling** - Comprehensive logging and exception handling
4. **Thread Safety** - Proper PDFium lock management
5. **Dependency Injection** - Full DI support
6. **Maintainability** - Easier to extend and modify

## Common Issues

### Issue: Cannot find PdfProcessor
**Solution:** Make sure you've added the project reference and using statement:
```csharp
using DotNet.Pdf.Core;
```

### Issue: Models not found
**Solution:** Update namespace references:
```csharp
using DotNet.Pdf.Core.Models;
```

### Issue: JSON serialization errors
**Solution:** Use the new SourceGenerationContext:
```csharp
DotNet.Pdf.Core.Models.SourceGenerationContext.Default.PdfInfo
```

## Support

If you encounter issues during migration, check:
1. All using statements are updated
2. Project references are correctly added
3. Model namespaces are updated
4. JSON serialization contexts are updated

The new architecture provides the same functionality with better organization and extensibility.
