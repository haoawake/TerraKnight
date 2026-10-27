param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
# Unity reads PlayerSettings before managed plugins start. Give the test copy its
# own engine preference namespace, while sharing all immutable bulk data.
New-Item -ItemType Directory -Path $Destination | Out-Null
foreach($item in Get-ChildItem -LiteralPath $Source){
 $target=Join-Path $Destination $item.Name
 if($item.Name -eq 'globalgamemanagers'){
  $bytes=[IO.File]::ReadAllBytes($item.FullName)
  $encoding=[Text.Encoding]::GetEncoding(28591)
  $raw=$encoding.GetString($bytes)
  if(([regex]::Matches($raw,'Team Cherry')).Count -ne 1 -or $raw.IndexOf('Hollow Knight') -lt 0){throw 'Unexpected Unity PlayerSettings identity layout'}
  $company='TK'+[Guid]::NewGuid().ToString('N').Substring(0,9)
  $product='Hollow Knight'
  $raw=$raw.Replace('Team Cherry',$company)
  $patched=$encoding.GetBytes($raw)
  if($patched.Length -ne $bytes.Length){throw 'PlayerSettings patch changed file size'}
  [IO.File]::WriteAllBytes($target,$patched)
  $originalPrefs=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Team Cherry\Hollow Knight')
  $privatePrefs=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\'+$company+'\'+$product)
  try{if($originalPrefs){foreach($pref in $originalPrefs.GetValueNames()){$privatePrefs.SetValue($pref,$originalPrefs.GetValue($pref),$originalPrefs.GetValueKind($pref))}}}
  finally{if($originalPrefs){$originalPrefs.Close()};$privatePrefs.Close()}
  @{Company=$company;Product=$product;OriginalHash=(Get-FileHash -LiteralPath $item.FullName).Hash;PrivateHash=(Get-FileHash -LiteralPath $target).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path $Destination -Parent) 'unity-private-settings.json')
 }elseif($item.PSIsContainer){New-Item -ItemType Junction -Path $target -Target $item.FullName | Out-Null}
 else{
  # Hard links need no symlink privilege and share the immutable data on this volume.
  try{New-Item -ItemType HardLink -Path $target -Target $item.FullName -ErrorAction Stop | Out-Null}
  catch{
   try{New-Item -ItemType SymbolicLink -Path $target -Target $item.FullName -ErrorAction Stop | Out-Null}
   catch{throw ('Cannot share immutable Unity data without copying: '+$item.Name+'. Use a template on the same volume. '+$_.Exception.Message)}
  }
 }
}
