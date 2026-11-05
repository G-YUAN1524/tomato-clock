# Requires Windows PowerShell / PowerShell 7.
# Builds 番茄时钟.exe with the in-box .NET Framework WPF compiler.
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\WPF"
$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$exe = Join-Path $root "番茄时钟.exe"
$icon = Join-Path $root "assets\app.ico"
$manifest = Join-Path $root "app.manifest"
$xaml = Join-Path $root "ui\MainWindow.xaml"
$wav = Join-Path $root "assets\chime.wav"

foreach ($required in @($csc, $icon, $manifest, $xaml, $wav)) {
    if (-not (Test-Path $required)) {
        throw "缺少构建文件: $required"
    }
}

$systemXaml = Join-Path $framework "System.Xaml.dll"
if (-not (Test-Path $systemXaml)) {
    $systemXaml = Join-Path $wpf "System.Xaml.dll"
}
if (-not (Test-Path $systemXaml)) {
    $gacXaml = Get-ChildItem (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_MSIL\System.Xaml") `
        -Recurse -Filter "System.Xaml.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($gacXaml) {
        $systemXaml = $gacXaml.FullName
    }
}

$references = @(
    (Join-Path $framework "System.dll"),
    (Join-Path $framework "System.Core.dll"),
    (Join-Path $framework "System.Drawing.dll"),
    (Join-Path $framework "System.Windows.Forms.dll"),
    $systemXaml,
    (Join-Path $framework "System.Xml.dll"),
    (Join-Path $wpf "PresentationCore.dll"),
    (Join-Path $wpf "PresentationFramework.dll"),
    (Join-Path $wpf "WindowsBase.dll")
)

$sources = Get-ChildItem (Join-Path $root "src") -Filter "*.cs" |
    Sort-Object Name |
    ForEach-Object { $_.FullName }

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/warn:4",
    "/codepage:65001",
    "/out:$exe",
    "/win32icon:$icon",
    "/win32manifest:$manifest",
    "/resource:$xaml,MainWindow.xaml",
    "/resource:$wav,chime.wav"
)

foreach ($reference in $references) {
    if (-not (Test-Path $reference)) {
        throw "缺少 .NET 引用程序集: $reference"
    }
    $arguments += "/reference:$reference"
}
$arguments += $sources

if (Test-Path $exe) {
    Remove-Item $exe -Force
}

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码 $LASTEXITCODE"
}

$config = Join-Path $root "app.config"
if (Test-Path $config) {
    Copy-Item $config "$exe.config" -Force
}

$info = Get-Item $exe
Write-Host ("构建完成: {0} ({1:N0} bytes)" -f $info.FullName, $info.Length)
