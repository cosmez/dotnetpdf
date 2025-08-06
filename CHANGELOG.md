# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.0.3] - 2025-08-06

### Added
- **Enhanced Object Identification** - Added ObjectId property to PdfPageObjectInfo model
  - Shows unique sequential ID alongside index for better object identification
  - Display format: "Index: X, ID: Y" where ID is sequential (index + 1)

### Enhanced
- **Real Text Content Extraction** - Implemented actual text extraction for text objects in list-objects command
  - Added TextContent property to PdfPageObjectInfo model (nullable string)
  - Manual implementation using FPDFTextGetBoundedText with object bounds
  - Smart truncation: displays up to 30 characters with "..." if longer, full text if ≤30 chars
  - Only extracts text for text objects (type == 1), null for other object types
- **Improved Display Format** - Enhanced list-objects command output
  - Format: "Page: X, Index: Y, ID: Z, Type: ..., Bounds: [...], Text: '...'"
  - Text objects now show meaningful content preview instead of placeholder text

### Changed
- **Version Update** - Bumped application version from v0.4 to v1.5 in Program.cs

### Technical Details
- **PdfPageObjectService.cs** - Added ExtractTextFromObject() method with unsafe code
- **Text Extraction** - Uses FPDFPageObjGetBounds and FPDFTextGetBoundedText APIs
- **String Conversion** - Proper UTF-16 string conversion via GetUtf16TextString helper
- **Error Handling** - Exception handling with debug logging for failed extractions

### Example Output
```
Found 193 objects:
- Page: 1, Index: 30, ID: 31, Type: Text (1), Bounds: [L: 44.16, B: 783.92, R: 347.19, T: 805.01], Text: "Sample Document for PDF Te"
- Page: 1, Index: 31, ID: 32, Type: Text (1), Bounds: [L: 346.99, B: 783.62, R: 411.22, T: 804.74], Text: "Testing"
```

### Code Analysis
- **LibTiff Investigation** - Analyzed unused LibTiff subfolder containing legacy TIFF-to-PDF conversion code
  - Confirmed 5 files (~3,658 lines) are dead code not referenced in active codebase
  - BitMiracle.LibTiff.NET package present but unused (actual TIFF support via SixLabors.ImageSharp)
  - Recommendation: LibTiff folder and dependency can be safely removed

---

## [2.0.2] - 2025-08-05

### Refactored
- **ImageSharp Dependency Cleanup** - Moved all ImageSharp encoder logic from CLI application to library for better separation of concerns
  - **`PdfRenderService`** - Added private `GetEncoder(string imageFormat)` method to handle image format conversion internally
  - **Enhanced Format Support** - Supports multiple image formats: "png", "jpg/jpeg", "gif", "bmp", "tiff/tif", "webp"
  - **Simplified API** - `ConvertPdfToImage()` and `ConvertPdfToImageAsync()` methods now accept `string imageFormat` parameter instead of `IImageEncoder`
  - **Smart Format Detection** - CLI automatically detects image format from output filename extension when provided
  - **Default Format** - Uses "png" as the default format when no format is specified

### Changed
- **CLI Application** - `CommandsHandler.ConvertPdfToImages()` refactored to pass image format string instead of creating encoder objects
- **PdfProcessor** - Updated method signatures to use `string imageFormat = "png"` parameter instead of `IImageEncoder encoder`
- **Dependencies** - Removed `SixLabors.ImageSharp` from main CLI project, keeping it only in the library where it's actually needed

### Removed
- **IoUtils Cleanup** - Removed all ImageSharp-related methods (`GetEncoder`, `GetExtension`) from CLI utilities
- **Unnecessary Imports** - Cleaned up ImageSharp imports from CLI project files
- **CLI Dependencies** - `SixLabors.ImageSharp` package reference removed from `dotnet.pdf.csproj`

### Technical Benefits
- **Cleaner Separation** - CLI application no longer needs to know about image encoding specifics
- **Reduced Dependencies** - CLI project has fewer external dependencies to manage
- **Better Encapsulation** - Image encoding logic properly contained within the rendering service
- **Simpler API** - Library users work with familiar format strings instead of encoder objects
- **Maintained Functionality** - All existing PDF to image conversion features preserved

### Testing Status
- ✅ Build process: Both library and CLI compile successfully
- ✅ CLI functionality: `convert` command works correctly with new string-based format parameter
- ✅ Format detection: Automatic format detection from filename extensions working
- ✅ API compatibility: Library methods accept format strings and handle encoding internally
- ✅ Dependency management: ImageSharp properly isolated to library only

### Migration Notes
- **For Library Users:** If you were passing `IImageEncoder` objects to `ConvertPdfToImage()`, now pass format strings like "png", "jpg", "gif", "bmp", "tiff", or "webp"
- **CLI Users:** No changes required - all commands work exactly the same
- **Developers:** ImageSharp encoders are now handled internally by the library - no need to create encoder objects in client code

---

## [2.0.1] - 2025-08-05

### Added
- **Enhanced Progress Reporting** - Comprehensive progress notification support added to all long-running operations
  - All service methods now accept optional `IProgress<T>` parameters for real-time progress feedback
  - Progress reporting maintains backward compatibility - all parameters are optional with null defaults
  - Detailed progress types provide operation-specific information:
    - `PdfTextProgress(int Current, int Max, PDfPageText PageText)` - Text extraction with current page data
    - `PdfBookmarkProgress(int Current, int Max, PDfBookmark PageText)` - Bookmark processing with current bookmark
    - `PdfProgress(int Current, int Max)` - General progress for other operations

### Enhanced
- **PdfProcessor Facade** - Updated all public methods to support and pass through progress parameters
- **Service Layer** - All services now report progress at appropriate intervals during processing
- **Documentation** - Updated MIGRATION.md with comprehensive progress reporting examples and usage patterns

### Technical Details
- Progress reporting occurs during page-by-page processing for optimal user feedback
- Final progress report indicates completion (Current = Max) for all operations
- Progress parameters are consistently placed as the last optional parameter in all method signatures
- No performance impact - progress reporting is efficiently implemented with minimal overhead

---

## [2.0.0] - 2025-08-05

### Added
- **NEW: DotNet.Pdf.Core Class Library** - Complete refactoring of PDF functionality into a well-organized class library
  - `PdfProcessor` - Main facade class providing unified API for all PDF operations
  - `BasePdfService` - Base class for all PDF services with common functionality
  - **Service Classes** for better separation of concerns:
    - `PdfTextExtractionService` - Text extraction from PDF documents
    - `PdfBookmarkService` - Bookmark/outline extraction and processing
    - `PdfInformationService` - Document metadata and information extraction
    - `PdfAttachmentService` - Attachment listing and extraction
    - `PdfPageObjectService` - Page object analysis and inspection
    - `PdfFormFieldService` - Form field inspection and analysis
    - `PdfWatermarkService` - Watermark addition functionality
  - **Model Classes** moved to organized namespace:
    - `PdfModels.cs` - All PDF data models (PdfInfo, PDfBookmark, PDfPageText, etc.)
    - `PdfEnums.cs` - PDF-specific enumerations (PdfPageObjectType, PdfAnnotationType)
  - **Progress Reporting Support** - All long-running operations now support `IProgress<T>` callbacks:
    - `PdfProgress` - General progress reporting for most operations
    - `PdfTextProgress` - Text extraction progress with current page data
    - `PdfBookmarkProgress` - Bookmark extraction progress with current bookmark data
    - `PdfSplitProgress` - PDF splitting progress with filename information
  - **Improved Error Handling** - Comprehensive logging and exception handling across all services
  - **Thread Safety** - Proper use of PDFiumLock across all PDF operations
  - **Dependency Injection** - Full DI support with ILoggerFactory integration

### Changed
- **BREAKING:** Refactored command handlers to use new `PdfProcessor` class instead of static `Pdfium` methods
  - `CommandsHandler` now uses `PdfProcessor` for text, bookmark, info, and attachment operations
  - `MoreCommandsHandler` now uses `PdfProcessor` for page objects, form fields, and watermarks
- **Updated Service Registration** - Modified `Program.cs` to properly inject `ILoggerFactory` into command handlers
- **JSON Serialization Context** - Updated all JSON serialization to use new `DotNet.Pdf.Core.Models.SourceGenerationContext`
- **Project References** - Added reference to new `DotNet.Pdf.Core` library in main application

### Removed
- **OBSOLETE FILES REMOVED:**
  - `Pdfium.Extra.cs` - Functionality moved to dedicated service classes
  - `Pdfium.More.cs` - Functionality moved to dedicated service classes  
  - `Models.cs` - All models moved to `DotNet.Pdf.Core.Models`
  - `PdfAnnotationType.cs` - Moved to `DotNet.Pdf.Core.Models.PdfEnums.cs`

### Technical Improvements
- **Better Architecture** - Clean separation of concerns with dedicated service classes
- **Maintainability** - Organized code structure that's easier to extend and maintain
- **Testability** - Services are now easily testable in isolation
- **Code Reusability** - PDF functionality can now be reused in other applications
- **Documentation** - Comprehensive XML documentation for all public APIs
- **Error Handling** - Improved error messages and logging throughout
- **Progress Reporting** - Real-time progress feedback for long-running operations
  - Text extraction reports progress per page with page data
  - Bookmark extraction reports progress per bookmark with bookmark data
  - Attachment operations report processing progress
  - Page object analysis reports progress per page
  - Form field analysis reports progress per page
  - Watermark operations report progress per page
- **API Compatibility** - All new methods maintain backward compatibility through optional progress parameters

### Migration Notes
- **For Developers:** If you were directly using methods from `Pdfium.Extra` or `Pdfium.More` partial classes, you now need to:
  1. Add reference to `DotNet.Pdf.Core` library
  2. Create an instance of `PdfProcessor` with `ILoggerFactory`
  3. Use the new service methods instead of static calls
  4. **NEW:** Optionally use `IProgress<T>` callbacks for progress reporting on long-running operations
- **Progress Reporting:** All service methods now support optional progress reporting:
  - Use `IProgress<PdfTextProgress>` for text extraction to get page-by-page progress
  - Use `IProgress<PdfBookmarkProgress>` for bookmark extraction to get bookmark-by-bookmark progress
  - Use `IProgress<PdfProgress>` for general operations (attachments, page objects, form fields, watermarks)
- **Backward Compatibility:** All CLI commands work exactly the same - no user-facing changes
- **Dependencies:** No new external dependencies added

### Testing Status
- ✅ Text extraction: Verified working with new `PdfTextExtractionService` and progress reporting
- ✅ Bookmark extraction: Verified working with new `PdfBookmarkService` and progress reporting
- ✅ PDF info extraction: Verified working with new `PdfInformationService`
- ✅ Attachment operations: Verified working with new `PdfAttachmentService` and progress reporting
- ✅ Page object analysis: Verified working with new `PdfPageObjectService` and progress reporting
- ✅ Form field inspection: Verified working with new `PdfFormFieldService` and progress reporting
- ✅ Watermark functionality: Verified working with new `PdfWatermarkService` and progress reporting
- ✅ Progress reporting: All `IProgress<T>` implementations tested and working correctly
- ✅ Build process: All projects build successfully without errors
- ✅ JSON serialization: All output formats working correctly with new model namespaces

### Performance
- **No Performance Impact** - The refactoring maintains the same performance characteristics
- **Memory Usage** - Similar memory usage patterns, with improved cleanup in service classes
- **Threading** - Same thread safety guarantees with cleaner implementation

---

## [1.x.x] - Previous Versions

Previous versions used monolithic partial classes (`Pdfium.Extra.cs` and `Pdfium.More.cs`) for PDF operations. See git history for details of earlier releases.
