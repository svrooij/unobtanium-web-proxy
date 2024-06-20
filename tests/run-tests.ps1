IF (!$outputDir) {
    $outputDir = Join-Path $(Get-Location) "coverage"
    Write-Output "Output directory not specified. Using default: $outputDir"
}

Write-Output "Coverage output directory: $outputDir"

$unitTestProject = Join-Path $(Get-Location) "tests" "Titanium.Web.Proxy.UnitTests" "Titanium.Web.Proxy.UnitTests.csproj"
$integrationTestProject = Join-Path $(Get-Location) "tests" "Titanium.Web.Proxy.IntegrationTests" "Titanium.Web.Proxy.IntegrationTests.csproj"

$collectCoverageParam = '/p:CollectCoverage=true;CoverletOutputFormat=json%2clcov%2ccobertura;MergeWith=' + "$outputDir.json;CoverletOutput=$outputDir"
$skipObsoleteParam = '/p:ExcludeByAttribute=ObsoleteAttribute' # Exclude obsolete code from coverage %2cGeneratedCodeAttribute%2cCompilerGeneratedAttribute

$unitExit = 0

dotnet test $unitTestProject --configuration Release -v minimal --no-build --logger GitHubActions $collectCoverageParam $skipObsoleteParam -- RunConfiguration.CollectSourceInformation=true
$unitExit = $LastExitCode
dotnet test $integrationTestProject -v minimal --no-build --logger GitHubActions $collectCoverageParam $skipObsoleteParam -- RunConfiguration.CollectSourceInformation=true

if ($unitExit -ne 0 -or $LastExitCode -ne 0) {
    exit 1
}