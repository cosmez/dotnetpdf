# Define the project path
$projectPath = "..\dotnet.pdf.csproj"

# Define output directory
$outputDir = ".\output"

# exe name 
$exeName = "dotnetpdf"

# Target frameworks (adjust according to your project's compatibility)
$framework = "net8.0" 

# Runtime Identifiers
$runtimes = @("win-x64","win-x86", "linux-x64", "osx-x64") # Add or remove runtimes as needed

foreach ($runtime in $runtimes) {
    dotnet pack $projectPath
    dotnet publish $projectPath `
        -c Release `
        -r $runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:AssemblyName=$exeName `
        -o "$outputDir/$runtime" `
        -f $framework
}