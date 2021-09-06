$ErrorActionPreference = "Stop"
$arg = $args[0]
function CopyNewestReport($outputPath, $testPath)
{
    $searchPath = Split-Path -Path $testPath
    Write-Host "Searching for json files in this path: $searchPath"
    # find all json result files and use the most recent one
    $files = Get-ChildItem -Path "$searchPath"  -Filter "*mutation-report.json" -Recurse -ErrorAction SilentlyContinue -Force
    $file = $files | Sort-Object {$_.LastWriteTime} | Select-Object -last 1

    # get the name and the timestamp of the file
    $orgReportFilePath=$file.FullName
    if ($orgReportFilePath)
    {
        $splitted = $orgReportFilePath -split { $_ -in '/', '\' }
        $dateTimeStamp = $splitted[$splitted.Length - 3]
        $fileName =  $splitted[$splitted.Length - 1]
        Write-Host "Last file filename: $orgReportFilePath has timestamp: $dateTimeStamp"

        # create a new filename to use in the output
        $newFileName = "$($outputPath)/$($dateTimeStamp)_mutation-report.json"

        Write-Host "Copy the report file to '$newFileName'"
        # write the new file out to the report directory
        Copy-Item "$orgReportFilePath" "$newFileName"
    }
}

function DeleteDataFromPreviousRuns ($outputPath) {
    # clear the output path
    Write-Host "Deleting previous json files from $($outputPath)"
    Get-ChildItem -Path "$($outputPath)" -Include '*.json', '*.html' -File -Recurse | ForEach-Object { Remove-Item $_ -Recurse -Force }

    Get-ChildItem -Path $pwd -Include 'StrykerOutput' -Recurse | ForEach-Object { Remove-Item $_ -Recurse -Force }
}

function Extend-Json($target, $extend)
{
    if ($target -ne $null -and $target.GetType().Name -eq "PSCustomObject")
    {
        foreach($property in $target | Get-Member -type NoteProperty, Property)
        {
            if (!(Get-Member -InputObject $extend -Name $property.Name -MemberType NoteProperty))
            {
                $extend | Add-Member -MemberType NoteProperty -Value $target.$($property.Name) -Name $property.Name
            }

            $extend.$($property.Name) = Extend-Json $target.$($property.Name) $extend.$($property.Name)
        }
    }

    if ($extend.GetType().Name -eq "Object[]")
    {
        return ,$extend
    }
    else
    {
        return $extend
    }
}

function JoinStykerJsonFile ($additionalFile, $joinedFileName) {
    # Stryker report json files object is not an array :-(, so we cannot join them and have to do it manually
    $report = ConvertFrom-Json (Get-Content $joinedFileName -Raw)
    $additionalContent = ConvertFrom-Json (Get-Content $additionalFile -Raw)

    Extend-Json $report $additionalContent

    $json = ConvertTo-Json $additionalContent -depth 100

    # save the new file to disk
    Set-Content -Path $joinedFileName -Value $json
}

function JoinJsonWithHtmlFile ($joinedJsonFileName) {
    $Json = (Get-Content $joinedJsonFileName | Out-String)

    $report = $("<!DOCTYPE html>
<html>
    <head>
       <script defer src='https://www.unpkg.com/mutation-testing-elements'></script>
    </head>
    <body>
        <mutation-test-report-app title-postfix='Stryker Mutation Testing'></mutation-test-report-app>

        <script>
            document.querySelector('mutation-test-report-app').report = $($Json);
        </script>
    </body>
</html>");

    Set-Content -Path 'mutation-report.html' -Value $report
}

function JoinAllJsonFiles ($joinedFileName) {
    $files = Get-ChildItem  -Filter "*.json" -Exclude $joinedFileName -Recurse -ErrorAction SilentlyContinue -Force
    Write-Host "Found $($files.Count) json files to join"
    $firstFile = $true
    foreach ($file in $files) {
        if ($true -eq $firstFile) {
            # copy the first file as is
            Copy-Item $file.FullName "$joinedFileName"
            $firstFile = $false
            continue
        }

        JoinStykerJsonFile $file.FullName $joinedFileName
    }
    Write-Host "Joined $($files.Count) files to the new json file: $joinedFileName"
}

function CreateReportFromAllJsonFiles ($reportDir) {
    # Join all the json files
    Set-Location "$reportDir"
    $joinedJsonFileName = "mutation-report.json"

    JoinAllJsonFiles $joinedJsonFileName

    # join the json with the html template for the final output
    JoinJsonWithHtmlFile $joinedJsonFileName

    Write-Host "Created new report file: $reportDir/$reportFileName"
}

function GenerateReports($outputPath, $cd)
{
    $projectFiles = get-childitem . *Test*.csproj -Recurse

    foreach( $projectFile in $projectFiles )
    {
        $commands = [System.Collections.ArrayList]::new()
        $projectXml = [xml] (get-content $projectFile.FullName)
        $projectDir = $projectFile.DirectoryName

        # Get list of project references for mutation and create command for it.
        foreach( $itemGroup in $projectXml.Project.ItemGroup )
        {
            foreach($project in $itemGroup.ChildNodes)
            {
                if ($project.Name -eq 'ProjectReference')
                {
                    $path = Resolve-Path "$($projectDir)/$($project.Include)"
                    [void]$commands.Add($path)
                }
            }
        }

        Set-Location $projectFile.Directory

        # Invoke all commands.
        foreach($command in $commands)
        {
            & dotnet tool run dotnet-stryker -p "$($command)" --abort-test-on-fail --threshold-high 99 --threshold-low 90 --threshold-break 85 --mutation-level 'Advanced' --reporters "['json', 'html', 'progress']"

            if ($_.Exception){
                throw $_.Exception
            }

            if ($LastExitCode -ne 0)
            {
                throw "Mutation Tests failed to meet the standard threshold of 85%."
            }

            CopyNewestReport $outputPath $pwd
        }

        Set-Location $cd
    }
}


function Init($outputPath, $cd)
{
    dotnet tool restore

    Write-Host "Deleting data from previous runs.."
    New-Item -ItemType Directory -Force -Path $outputPath
    DeleteDataFromPreviousRuns $outputPath

    Write-Host "Beginning mutation testing..."
    GenerateReports $outputPath $cd

    Write-Host "Merging mutation testing results into a single report..."
    CreateReportFromAllJsonFiles $outputPath

    Set-Location $cd
}

Init $arg $pwd
