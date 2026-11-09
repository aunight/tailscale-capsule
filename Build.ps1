param([string]$Version='2.1.1',[string]$InnoCompiler='',[switch]$SkipInstaller)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$publish=Join-Path $PSScriptRoot 'artifacts\publish'
$release=Join-Path $PSScriptRoot 'artifacts\release'
New-Item -ItemType Directory -Force $release | Out-Null
dotnet publish TailscaleCapsule.csproj -c Release -p:Platform=x64 -p:Version=$Version --self-contained true -o $publish
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
Copy-Item LICENSE,THIRD_PARTY_NOTICES.md -Destination $publish
Compress-Archive -Path "$publish\*" -DestinationPath (Join-Path $release "TailscaleCapsule-$Version-Windows-x64.zip") -Force
if(!$SkipInstaller){
  if(!$InnoCompiler){$InnoCompiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'}
  if(!(Test-Path -LiteralPath $InnoCompiler)){throw 'Specify -InnoCompiler with the path to ISCC.exe'}
  & $InnoCompiler "/DAppVersion=$Version" "/DPublishRoot=$publish" "/DReleaseRoot=$release" installer\TailscaleCapsule.iss
  if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
}
$files=Get-ChildItem $release -File | Where-Object Name -Match "^TailscaleCapsule-$([regex]::Escape($Version))-"
$checksums=$files | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$_.Name }
$checksums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
