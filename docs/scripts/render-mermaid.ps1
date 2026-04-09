<#
.SYNOPSIS
  Renders Mermaid markup files to PNG via mermaid.ink API.
.PARAMETER InputDir
  Directory containing .mmd files.
.PARAMETER OutputDir
  Directory for output PNG files.
#>
param(
    [string]$InputDir,
    [string]$OutputDir
)

Get-ChildItem "$InputDir\*.mmd" | ForEach-Object {
    $name = $_.BaseName
    $code = Get-Content $_.FullName -Raw -Encoding UTF8
    $json = @{ code = $code; mermaid = @{ theme = "default" } } | ConvertTo-Json -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $base64 = [Convert]::ToBase64String($bytes)
    $url = "https://mermaid.ink/img/base64:$base64"
    $outFile = Join-Path $OutputDir "$name.png"
    try {
        Invoke-WebRequest -Uri $url -OutFile $outFile -UseBasicParsing
        Write-Output "OK: $name.png"
    }
    catch {
        Write-Warning "FAIL: $name - $_"
    }
}
