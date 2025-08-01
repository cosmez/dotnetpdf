# =============================================================================
# PowerShell Test Script for dotnet.pdf Command-Line Application
#
# Description:
# This script is fully portable and automatically detects required paths. It assumes
# the following directory structure:
#
# /.../
#   ├── dotnet.pdf/             <-- Project Directory ($projectPath)
#   │   ├── dotnet.pdf.csproj
#   │   └── scripts/
#   │       └── run-tests.ps1   <-- This script
#   └── test-files/             <-- Test Files Directory ($testFilesSourcePath)
#
# It automates testing by:
# 1. Creating a unique temporary folder for a clean test environment.
# 2. Copying the necessary test PDF files into it.
# 3. Executing commands using `dotnet run` against the discovered project.
# 4. Saving the console output of each command to a corresponding .txt log file.
# 5. Opening the temporary folder in Windows Explorer for easy inspection.
#
# =============================================================================


try {
    # Determine paths relative to this script's location ($PSScriptRoot).
    # Assumes this script is in a 'scripts' subfolder of the project directory.
    $projectPath = (Get-Item $PSScriptRoot).Parent.FullName

    $projectParentPath = (Get-Item $projectPath).Parent.FullName
    $testFilesSourcePath = Join-Path $projectParentPath "test-files"

    # --- Path Validation ---
    if (-not (Test-Path (Join-Path $projectPath "dotnet.pdf.csproj"))) {
        throw "Could not find 'dotnet.pdf.csproj' in the determined project path: `"$projectPath`". Please ensure the script is in a 'scripts' subfolder of the project."
    }
    if (-not (Test-Path $testFilesSourcePath -PathType Container)) {
        throw "Could not find the 'test-files' directory at the expected location: `"$testFilesSourcePath`". Please ensure it is a sibling of the project directory."
    }
}
catch {
    Write-Error "CRITICAL SETUP FAILURE: $($_.Exception.Message)"
    if ($Host.Name -eq "ConsoleHost") { Read-Host "Press Enter to exit" }
    exit 1
}

$dotNetFramework = "net8.0"
$pdfPassword = "password" 

# --- Helper Function to Run and Log Tests ---

function Run-Test {
    param(
        [Parameter(Mandatory=$true)]
        [string]$TestName,
        [Parameter(Mandatory=$true)]
        [string]$Arguments
    )
    $logFile = Join-Path $tempDir "$TestName.output.txt"
    $fullCommand = "dotnet run --project `"$projectPath`" --framework $dotNetFramework -- $Arguments"

    Write-Host "--- Running Test: $TestName ---" -ForegroundColor White
    Write-Host "Command: $fullCommand"

    try {
        Invoke-Expression "$fullCommand *> `"$logFile`""
        if ($LASTEXITCODE -eq 0) {
            Write-Host "SUCCESS: Test '$TestName' completed. See '$logFile' for details." -ForegroundColor Green
        } else {
            Write-Host "FAILURE: Test '$TestName' failed with exit code $LASTEXITCODE. See '$logFile' for error details." -ForegroundColor Red
            Get-Content -Path $logFile | Write-Host
        }
    }
    catch {
        Write-Host "CRITICAL FAILURE: Test '$TestName' threw a PowerShell exception." -ForegroundColor DarkRed
        $_ | Out-String | Write-Host
    }
    finally {
        Write-Host ""
    }
}

# --- Main Script Execution ---

# 1. --- Setup Test Environment ---
Write-Host "Auto-detected Project Path: $projectPath" -ForegroundColor Gray
Write-Host "Auto-detected Test Files Path: $testFilesSourcePath" -ForegroundColor Gray
Write-Host "Setting up the test environment..." -ForegroundColor Cyan

$tempDir = Join-Path $env:TEMP "DotNetPdfTest_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -Path $tempDir -ItemType Directory | Out-Null
Write-Host "Test directory created at: $tempDir"

Write-Host "Copying test files..."
Copy-Item -Path (Join-Path $testFilesSourcePath "*") -Destination $tempDir -Force
if ($?) { Write-Host "Test files copied successfully." -ForegroundColor Green } else { Write-Error "Failed to copy test files."; exit 1 }

$dummyImagePath = Join-Path $tempDir "test-image.png"
try {
    Write-Host "Creating a dummy image for testing..."
    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap(200, 100)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::LightGray)
    $font = New-Object System.Drawing.Font("Arial", 12)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Black)
    $graphics.DrawString("Test Image`n$(Get-Date)", $font, $brush, 10, 40)
    $graphics.Dispose(); $bitmap.Save($dummyImagePath, [System.Drawing.Imaging.ImageFormat]::Png); $bitmap.Dispose()
    Write-Host "Dummy image '$dummyImagePath' created." -ForegroundColor Green
} catch {
    Write-Warning "Could not create dummy image. System.Drawing might not be available. Skipping 'imagetopdf' test."
    $imageTestSkipped = $true
}

Write-Host "Opening test directory in Explorer..."
Invoke-Item $tempDir

Write-Host "Setup complete. Starting tests..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

# 2. --- Run Tests (Corrected Argument Formatting) ---

# Test 1: Get Info
$arguments = 'info --input "{0}"' -f (Join-Path $tempDir 'basic-text.pdf')
Run-Test -TestName "Info_Text" -Arguments $arguments

# Test 2: Extract Text
$arguments = 'text --input "{0}"' -f (Join-Path $tempDir 'basic-text.pdf')
Run-Test -TestName "Text_Extraction" -Arguments $arguments

# Test 3: Extract Bookmarks
$arguments = 'bookmarks --input "{0}"' -f (Join-Path $tempDir 'pdf-example-bookmarks.pdf')
Run-Test -TestName "Bookmarks_Extraction" -Arguments $arguments

# Test 4: Split PDF into individual pages
$arguments = 'split --input "{0}" --output "{1}"' -f (Join-Path $tempDir 'sample-report.pdf'), $tempDir
Run-Test -TestName "Split_Pages" -Arguments $arguments

# Test 5: Split PDF using bookmarks for filenames
$splitByBookmarkDir = Join-Path $tempDir "split-by-bookmark"
New-Item -Path $splitByBookmarkDir -ItemType Directory -Force | Out-Null
$arguments = 'split --input "{0}" --use-bookmarks --output "{1}"' -f (Join-Path $tempDir 'pdf-example-bookmarks.pdf'), $splitByBookmarkDir
Run-Test -TestName "Split_By_Bookmarks" -Arguments $arguments

# Test 6: Merge two PDFs
$arguments = 'merge --input "{0}" "{1}" --output "{2}"' -f (Join-Path $tempDir 'annotations.pdf'), (Join-Path $tempDir 'basic-text.pdf'), (Join-Path $tempDir 'merged-document.pdf')
Run-Test -TestName "Merge_Docs" -Arguments $arguments

# Test 7: Convert PDF page to an image
$arguments = 'convert --input "{0}" --range 1 --dpi 150 --encoder .png --output "{1}"' -f (Join-Path $tempDir 'image-extraction.pdf'), $tempDir
Run-Test -TestName "Convert_To_Image" -Arguments $arguments

# Test 8: Convert an image to a PDF
if (-not $imageTestSkipped) {
    $arguments = 'imagetopdf --input "{0}" --output "{1}"' -f $dummyImagePath, (Join-Path $tempDir 'pdf-from-image.pdf')
    Run-Test -TestName "Image_To_Pdf" -Arguments $arguments
}

# Test 9: Rotate a PDF page
$arguments = 'rotate --input "{0}" --output "{1}" --range 1 --rotation 90' -f (Join-Path $tempDir 'basic-text.pdf'), (Join-Path $tempDir 'rotated-basic-text.pdf')
Run-Test -TestName "Rotate_Page" -Arguments $arguments

# Test 10: Remove pages from a PDF
$arguments = 'remove --input "{0}" --output "{1}" --pages "2"' -f (Join-Path $tempDir 'sample-report.pdf'), (Join-Path $tempDir 'sample-report-removed-page2.pdf')
Run-Test -TestName "Remove_Pages" -Arguments $arguments

# Test 11: Insert blank pages into a PDF
$arguments = 'insert --input "{0}" --output "{1}" --positions "1:2"' -f (Join-Path $tempDir 'basic-text.pdf'), (Join-Path $tempDir 'basic-text-inserted.pdf')
Run-Test -TestName "Insert_Pages" -Arguments $arguments

# Test 12: Reorder pages in a PDF
$arguments = 'reorder --input "{0}" --output "{1}" --order "3,1,2"' -f (Join-Path $tempDir 'sample-report.pdf'), (Join-Path $tempDir 'sample-report-reordered.pdf')
Run-Test -TestName "Reorder_Pages" -Arguments $arguments

# Test 13: Access a password-protected file
$arguments = 'info --input "{0}" --password "{1}"' -f (Join-Path $tempDir 'protected-password-samplefiles.pdf'), $pdfPassword
Run-Test -TestName "Info_Protected_File" -Arguments $arguments

# Test 14: List attachments from a file
$arguments = 'list-attachments --input "{0}"' -f (Join-Path $tempDir 'dev-example.pdf')
Run-Test -TestName "List_Attachments" -Arguments $arguments

# Test 15: Extract all attachments from a file
$extractAttachmentsDir = Join-Path $tempDir "extracted-attachments"
New-Item -Path $extractAttachmentsDir -ItemType Directory -Force | Out-Null
$arguments = 'extract-attachments --input "{0}" --output "{1}"' -f (Join-Path $tempDir 'dev-example.pdf'), $extractAttachmentsDir
Run-Test -TestName "Extract_Attachments" -Arguments $arguments

# 3. --- Test Completion ---
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "All tests have been executed."
Write-Host "Please review the generated files and logs in the directory opened in Explorer:"
Write-Host $tempDir
Write-Host "=============================================" -ForegroundColor Cyan