IF (!$outputDir) {
    $outputDir = Join-Path $(Get-Location) "coverage"
    Write-Output "Output directory not specified. Using default: $outputDir"
}

Write-Output "Coverage output directory: $outputDir"

$unitTestProject = Join-Path $(Get-Location) "tests" "Unobtanium.Web.Proxy.KestrelTests" "Unobtanium.Web.Proxy.KestrelTests.csproj"

$collectCoverageParam = '/p:CollectCoverage=true;CoverletOutputFormat=json%2clcov%2ccobertura;MergeWith=' + "$outputDir.net9.0.json;CoverletOutput=$outputDir"
$skipObsoleteParam = '/p:ExcludeByAttribute=ObsoleteAttribute' # Exclude obsolete code from coverage %2cGeneratedCodeAttribute%2cCompilerGeneratedAttribute

$unitExit = 0

dotnet test $unitTestProject --configuration Release -v minimal --no-build --logger GitHubActions $collectCoverageParam $skipObsoleteParam -f net9.0 -- RunConfiguration.CollectSourceInformation=true
$unitExit = $LastExitCode
if ($unitExit -ne 0 -or $LastExitCode -ne 0) {
    exit 1
}