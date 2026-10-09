[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"

function Get-Value {
    param(
        [Parameter(Mandatory = $true)]
        [object] $InputObject,
        [Parameter(Mandatory = $true)]
        [string[]] $Names
    )

    foreach ($name in $Names) {
        $property = $InputObject.PSObject.Properties |
            Where-Object { $_.Name -ieq $name } |
            Select-Object -First 1
        if ($null -ne $property) {
            return $property.Value
        }
    }

    return $null
}

function Has-Property {
    param(
        [Parameter(Mandatory = $true)]
        [object] $InputObject,
        [Parameter(Mandatory = $true)]
        [string[]] $Names
    )

    foreach ($name in $Names) {
        if ($null -ne ($InputObject.PSObject.Properties |
                Where-Object { $_.Name -ieq $name } |
                Select-Object -First 1)) {
            return $true
        }
    }

    return $false
}

$root = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Output directory does not exist: $root"
}
$rootItem = Get-Item -LiteralPath $root -Force
if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Output directory cannot be a symbolic link or reparse point: $root"
}

$manifestPath = Join-Path $root "proof-harness-bundle.json"
$resultPath = Join-Path $root "proof-result-bundle.json"
foreach ($requiredPath in @($manifestPath, $resultPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required proof bundle file is missing: $requiredPath"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
$contractVersion = Get-Value $result @("ContractVersion", "Version")
if ([string]::IsNullOrWhiteSpace([string]$contractVersion)) {
    throw "The proof result bundle has no contractVersion."
}

$artifactReferences = [Collections.Generic.List[string]]::new()
foreach ($propertyName in @("ComponentResults", "Components", "ScenarioResults", "ProbeResults")) {
    $records = Get-Value $result @($propertyName)
    foreach ($record in @($records)) {
        if ($null -eq $record) {
            continue
        }
        foreach ($reference in @(Get-Value $record @("EvidenceArtifacts"))) {
            if (-not [string]::IsNullOrWhiteSpace([string]$reference)) {
                $artifactReferences.Add([string]$reference)
            }
        }
    }
}

$componentResults = Get-Value $result @("ComponentResults", "Components")
if (@($componentResults).Count -eq 0 -or
    -not (Has-Property $result @("ScenarioResults")) -or
    -not (Has-Property $result @("ProbeResults"))) {
    throw "The proof result bundle is missing component, scenario, or probe result arrays."
}

$references = @($artifactReferences | Sort-Object -Unique)
if ($references.Count -eq 0) {
    throw "The proof result bundle does not reference any evidence artifacts."
}

$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$indexedFiles = [Collections.Generic.List[object]]::new()
$missing = [Collections.Generic.List[string]]::new()
foreach ($reference in $references) {
    $relativePath = $reference.Replace('\', '/')
    if ([IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Split('/') -contains '..') {
        throw "Evidence path must be relative and cannot traverse directories: $reference"
    }

    $candidate = [IO.Path]::GetFullPath((Join-Path $root ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    if (-not $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Evidence path resolves outside the output directory: $reference"
    }

    $pathCursor = $root
    foreach ($segment in $relativePath.Split('/')) {
        $pathCursor = Join-Path $pathCursor $segment
        if (Test-Path -LiteralPath $pathCursor) {
            $pathItem = Get-Item -LiteralPath $pathCursor -Force
            if (($pathItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Evidence paths cannot traverse symbolic links or reparse points: $reference"
            }
        }
    }

    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $files = @(Get-Item -LiteralPath $candidate)
    }
    elseif (Test-Path -LiteralPath $candidate -PathType Container) {
        $entries = @(Get-ChildItem -LiteralPath $candidate -Force -Recurse)
        if (@($entries | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -gt 0) {
            throw "Evidence directories cannot contain symbolic links or reparse points: $reference"
        }
        $files = @($entries | Where-Object { -not $_.PSIsContainer })
        if ($files.Count -eq 0) {
            $missing.Add($reference)
            continue
        }
    }
    else {
        $missing.Add($reference)
        continue
    }

    foreach ($file in $files) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Evidence files cannot be symbolic links or reparse points: $($file.FullName)"
        }
        $hash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
        $indexedFiles.Add([ordered]@{
            Path = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            Length = $file.Length
            Sha256 = $hash.Hash.ToLowerInvariant()
        })
    }
}

$report = [ordered]@{
    ContractVersion = "1.0"
    GeneratedAtUtc = [DateTime]::UtcNow.ToString("O")
    HarnessContractVersion = Get-Value $manifest @("ContractVersion")
    ResultContractVersion = $contractVersion
    ReferencedArtifactCount = $references.Count
    VerifiedFileCount = $indexedFiles.Count
    MissingReferences = @($missing)
    VerifiedFiles = @($indexedFiles | Sort-Object Path -Unique)
    Scope = "Evidence path existence and SHA-256 inventory only; does not verify evidence contents, tool behavior, deformation, MO2 integration, or in-game behavior."
}

$reportPath = Join-Path $root "proof-evidence-integrity.json"
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host "Wrote evidence integrity inventory: $reportPath"
Write-Host "Verified files: $($report.VerifiedFileCount); missing references: $($report.MissingReferences.Count)"

if ($missing.Count -gt 0) {
    exit 1
}
