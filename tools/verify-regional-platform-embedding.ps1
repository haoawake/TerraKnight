param([string]$Dll='artifacts/regional-platforms-build/TerraKnight.dll')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$manifest=Get-Content -LiteralPath (Join-Path $root 'artifacts/regional-platforms/assets.json') -Raw | ConvertFrom-Json
$assembly=[Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $Dll).Path)
$checked=0
foreach($asset in $manifest.assets) {
    $name='TerraKnight.'+[IO.Path]::GetFileName($asset.file)
    $stream=$assembly.GetManifestResourceStream($name)
    if($null -eq $stream) {throw "Missing embedded resource: $name"}
    try {
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
        if($hash -ne $asset.sha256) {throw "Embedded image differs from verified native PNG: $name"}
    } finally {$stream.Dispose()}
    $checked++
}
if($checked -ne 48) {throw "Expected 48 embedded platform images, got $checked"}
$result=@{passed=$true;embedded_images=$checked;dll_sha256=(Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash.ToLowerInvariant()}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'artifacts/regional-platforms/embedded-validation.json') -Encoding utf8
$result | ConvertTo-Json -Compress
