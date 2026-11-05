Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class QaNative {
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
}
"@

$ErrorActionPreference = "Stop"
$tools = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Split-Path -Parent $tools
$exe = Join-Path $project "番茄时钟.exe"
$failures = New-Object System.Collections.Generic.List[string]

function Wait-Until {
    param([ScriptBlock]$Condition, [int]$TimeoutMs = 5000, [int]$IntervalMs = 100)
    $deadline = [DateTime]::Now.AddMilliseconds($TimeoutMs)
    while ([DateTime]::Now -lt $deadline) {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds $IntervalMs
    }
    return $false
}

function Find-ById {
    param($Window, [string]$Id)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    return $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-ById {
    param($Window, [string]$Id)
    $element = Find-ById $Window $Id
    if ($null -eq $element) { throw "找不到控件 $Id" }

    $invoke = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
        $invoke.Invoke()
    } else {
        $toggle = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$toggle)) {
            $toggle.Toggle()
        } else {
            throw "控件 $Id 不支持点击操作"
        }
    }
    Start-Sleep -Milliseconds 450
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if ($Condition) {
        Write-Host "PASS  $Message"
    } else {
        Write-Host "FAIL  $Message" -ForegroundColor Red
        $script:failures.Add($Message)
    }
}

$p = Start-Process -FilePath $exe -PassThru
$script:windowCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$script:window = $null
$found = Wait-Until {
    $script:window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children, $script:windowCondition)
    return ($null -ne $script:window)
} 6000
$window = $script:window

if (-not $found) {
    Write-Host "FAIL  窗口没有出现" -ForegroundColor Red
    if (-not $p.HasExited) { $p.Kill() }
    exit 1
}

Write-Host "窗口: $($window.Current.Name) at $($window.Current.BoundingRectangle)"
Start-Sleep -Milliseconds 600

$time = Find-ById $window "TimeText"
$status = Find-ById $window "StatusText"
$primary = Find-ById $window "PrimaryButton"

Assert-True ($time.Current.Name -eq "25:00") "默认显示 25:00"
Assert-True ($status.Current.Name -eq "准备专注") "默认状态为准备专注"

# Start and let the countdown tick.
Invoke-ById $window "PrimaryButton"
Start-Sleep -Milliseconds 1400
Assert-True ($time.Current.Name -ne "25:00") "开始后倒计时在减少（$($time.Current.Name)）"

# Pause.
Invoke-ById $window "PrimaryButton"
Start-Sleep -Milliseconds 300
Assert-True ($status.Current.Name -eq "已暂停") "暂停后状态变为已暂停"
$pausedTime = $time.Current.Name
Start-Sleep -Milliseconds 1200
Assert-True ($time.Current.Name -eq $pausedTime) "暂停后时间不再变化"

# Preset 30 minutes.
Invoke-ById $window "Preset30"
Start-Sleep -Milliseconds 350
Assert-True ($time.Current.Name -eq "30:00") "点击 30分 后显示 30:00"
Assert-True ($status.Current.Name -eq "准备专注") "切换预设后重置为准备状态"

# Pin toggle.
$pin = Find-ById $window "PinButton"
$toggle = $pin.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
$handle = [IntPtr]$window.Current.NativeWindowHandle
$before = $toggle.Current.ToggleState
$wasTopmost = (([QaNative]::GetWindowLong($handle, -20) -band 8) -ne 0)
$toggle.Toggle()
Start-Sleep -Milliseconds 350
$after = $toggle.Current.ToggleState
$isTopmost = (([QaNative]::GetWindowLong($handle, -20) -band 8) -ne 0)
Assert-True ($before -ne $after) "固定按钮可以切换状态（$before -> $after）"
Assert-True ($wasTopmost -ne $isTopmost) "固定按钮真的改变置顶状态（$wasTopmost -> $isTopmost）"
$toggle.Toggle()
Start-Sleep -Milliseconds 250

# Custom 5-second countdown.
Invoke-ById $window "PresetCustom"
Start-Sleep -Milliseconds 400
$minutes = Find-ById $window "CustomMinutesBox"
$seconds = Find-ById $window "CustomSecondsBox"

# Empty durations are rejected without closing the dialog.
$minutes.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("0")
$seconds.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("00")
Start-Sleep -Milliseconds 150
Invoke-ById $window "CustomApply"
$errorText = Find-ById $window "CustomError"
Assert-True ($errorText.Current.Name -like "至少设置*") "自定义 0 秒会被拒绝并显示提示"
Assert-True (-not $minutes.Current.IsOffscreen) "校验失败后自定义弹层仍然打开"

$minutes.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("0")
$seconds.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("05")
Start-Sleep -Milliseconds 150
Invoke-ById $window "CustomApply"
Start-Sleep -Milliseconds 350
Assert-True ($time.Current.Name -eq "00:05") "自定义 5 秒后显示 00:05"
Assert-True ($status.Current.Name -eq "准备专注") "自定义时长后回到准备状态"

# Run it to completion.
Invoke-ById $window "PrimaryButton"
Start-Sleep -Milliseconds 6200
$finishTitle = Find-ById $window "FinishTitle"
$finishRect = $finishTitle.Current.BoundingRectangle
Assert-True ($finishTitle.Current.Name -eq "该做眼保健操啦") "倒计时结束后出现眼保健操提醒"
Assert-True (-not $finishTitle.Current.IsOffscreen) "眼保健操提醒实际可见"

# The reminder should lead to a 5-minute eye break.
Invoke-ById $window "FinishPrimary"
Start-Sleep -Milliseconds 600
Assert-True ($status.Current.Name -eq "护眼休息") "点击休息后进入护眼休息模式"
Assert-True ($time.Current.Name -match "^(05:00|04:5[0-9])$") "护眼休息从 5 分钟开始（$($time.Current.Name)）"

# End the break and return to focus.
Invoke-ById $window "PrimaryButton"
Start-Sleep -Milliseconds 450
Assert-True ($status.Current.Name -eq "准备专注") "结束休息后回到专注模式"

# Restore the friendly 25-minute default.
Invoke-ById $window "Preset25"
Start-Sleep -Milliseconds 300
Assert-True ($time.Current.Name -eq "25:00") "恢复默认 25:00"

# Minimize through the actual button, then restore through the window pattern.
$windowPattern = $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
Invoke-ById $window "MinButton"
Start-Sleep -Milliseconds 500
Assert-True ($windowPattern.Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Minimized) "最小化按钮可用"
$windowPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
Start-Sleep -Milliseconds 500
Assert-True ($windowPattern.Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Normal) "窗口可以恢复显示"

# Exit via the close button and confirm the process ends.
Invoke-ById $window "CloseButton"
$exited = Wait-Until { $p.HasExited } 5000
Assert-True $exited "退出按钮关闭应用"

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "$($failures.Count) 项检查失败" -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "全部 UI 检查通过" -ForegroundColor Green
exit 0
