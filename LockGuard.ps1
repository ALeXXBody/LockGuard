Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Cfg = @{
    LockHour = 18; UnlockHour = 9
    LogDir = "C:\ProgramData\LockGuard\Logs"
    ConfigFile = "C:\ProgramData\LockGuard\config.json"
    AppTitle = "LockGuard"; Version = "2.1"
}

$State = @{
    InternetOff = $false; IsNightHours = $false; Monitoring = $false
    LastEvent = "None"; PasswordHash = $null; PasswordSalt = $null; Configured = $false
}

function New-RandomSalt {
    $bytes = New-Object byte[] 32
    (New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

function Hash-Password {
    param([string]$Password, [string]$Salt)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hash = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Password + $Salt))
    return [Convert]::ToBase64String($hash)
}

function Save-Config {
    $config = @{ PasswordHash = $State.PasswordHash; PasswordSalt = $State.PasswordSalt; LockHour = $Cfg.LockHour; UnlockHour = $Cfg.UnlockHour }
    $dir = Split-Path $Cfg.ConfigFile -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $config | ConvertTo-Json | Set-Content -Path $Cfg.ConfigFile -Encoding UTF8
}

function Load-Config {
    if (Test-Path $Cfg.ConfigFile) {
        try {
            $c = Get-Content $Cfg.ConfigFile -Raw | ConvertFrom-Json
            $State.PasswordHash = $c.PasswordHash; $State.PasswordSalt = $c.PasswordSalt
            if ($c.LockHour) { $Cfg.LockHour = $c.LockHour }
            if ($c.UnlockHour) { $Cfg.UnlockHour = $c.UnlockHour }
            $State.Configured = $true; return $true
        } catch { return $false }
    }
    return $false
}

function Test-Password {
    param([string]$Password)
    if (-not $State.PasswordHash -or -not $State.PasswordSalt) { return $false }
    return ((Hash-Password -Password $Password -Salt $State.PasswordSalt) -eq $State.PasswordHash)
}

function Show-PasswordDialog {
    param([string]$Title = "Enter Password", [string]$Prompt = "Enter password to continue:")
    $dlg = New-Object System.Windows.Forms.Form
    $dlg.Text = $Title; $dlg.Size = New-Object System.Drawing.Size(500, 280); $dlg.StartPosition = "CenterParent"
    $dlg.FormBorderStyle = "FixedDialog"; $dlg.MaximizeBox = $false; $dlg.MinimizeBox = $false
    $dlg.BackColor = [System.Drawing.Color]::FromArgb(35,35,35); $dlg.ForeColor = [System.Drawing.Color]::White
    $dlg.Font = New-Object System.Drawing.Font("Segoe UI", 11); $dlg.TopMost = $true
    $dlg.Padding = New-Object System.Windows.Forms.Padding(20)

    $lbl = New-Object System.Windows.Forms.Label; $lbl.Text = $Prompt
    $lbl.Location = New-Object System.Drawing.Point(25,25); $lbl.Size = New-Object System.Drawing.Size(430,30); $dlg.Controls.Add($lbl)

    $txtPass = New-Object System.Windows.Forms.TextBox; $txtPass.Location = New-Object System.Drawing.Point(25,65)
    $txtPass.Size = New-Object System.Drawing.Size(430,35); $txtPass.UseSystemPasswordChar = $true
    $txtPass.Font = New-Object System.Drawing.Font("Segoe UI",14); $txtPass.BackColor = [System.Drawing.Color]::FromArgb(50,50,50)
    $txtPass.ForeColor = [System.Drawing.Color]::White; $txtPass.BorderStyle = "FixedSingle"; $dlg.Controls.Add($txtPass)

    $lblErr = New-Object System.Windows.Forms.Label; $lblErr.Text = ""; $lblErr.ForeColor = [System.Drawing.Color]::FromArgb(255,80,80)
    $lblErr.Location = New-Object System.Drawing.Point(25,110); $lblErr.Size = New-Object System.Drawing.Size(430,25); $dlg.Controls.Add($lblErr)

    $btnOk = New-Object System.Windows.Forms.Button; $btnOk.Text = "OK"; $btnOk.Size = New-Object System.Drawing.Size(190,45)
    $btnOk.Location = New-Object System.Drawing.Point(25,150); $btnOk.BackColor = [System.Drawing.Color]::FromArgb(0,140,220)
    $btnOk.ForeColor = [System.Drawing.Color]::White; $btnOk.FlatStyle = "Flat"
    $btnOk.Font = New-Object System.Drawing.Font("Segoe UI",11,[System.Drawing.FontStyle]::Bold)
    $btnOk.DialogResult = [System.Windows.Forms.DialogResult]::OK; $dlg.Controls.Add($btnOk)

    $btnCancel = New-Object System.Windows.Forms.Button; $btnCancel.Text = "Cancel"; $btnCancel.Size = New-Object System.Drawing.Size(190,45)
    $btnCancel.Location = New-Object System.Drawing.Point(265,150); $btnCancel.BackColor = [System.Drawing.Color]::FromArgb(80,80,80)
    $btnCancel.ForeColor = [System.Drawing.Color]::White; $btnCancel.FlatStyle = "Flat"
    $btnCancel.Font = New-Object System.Drawing.Font("Segoe UI",11)
    $btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel; $dlg.Controls.Add($btnCancel)

    $dlg.AcceptButton = $btnOk; $dlg.CancelButton = $btnCancel
    $result = $dlg.ShowDialog(); $pw = $txtPass.Text; $dlg.Dispose()
    if ($result -eq [System.Windows.Forms.DialogResult]::OK) { return $pw }
    return $null
}

function Show-PasswordSetup {
    $dlg = New-Object System.Windows.Forms.Form; $dlg.Text = "LockGuard - Set Password"
    $dlg.Size = New-Object System.Drawing.Size(500, 380); $dlg.StartPosition = "CenterScreen"
    $dlg.FormBorderStyle = "FixedDialog"; $dlg.MaximizeBox = $false; $dlg.MinimizeBox = $false
    $dlg.BackColor = [System.Drawing.Color]::FromArgb(35,35,35); $dlg.ForeColor = [System.Drawing.Color]::White
    $dlg.Font = New-Object System.Drawing.Font("Segoe UI", 11); $dlg.TopMost = $true
    $dlg.Padding = New-Object System.Windows.Forms.Padding(20)

    $title = New-Object System.Windows.Forms.Label; $title.Text = "Set Your Password"
    $title.Font = New-Object System.Drawing.Font("Segoe UI",16,[System.Drawing.FontStyle]::Bold)
    $title.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255)
    $title.Location = New-Object System.Drawing.Point(25,15); $title.AutoSize = $true; $dlg.Controls.Add($title)

    $info = New-Object System.Windows.Forms.Label; $info.Text = "This password is required to exit or disable LockGuard."
    $info.ForeColor = [System.Drawing.Color]::FromArgb(160,160,160)
    $info.Location = New-Object System.Drawing.Point(25,50); $info.Size = New-Object System.Drawing.Size(430,25); $dlg.Controls.Add($info)

    $lbl1 = New-Object System.Windows.Forms.Label; $lbl1.Text = "Password:"; $lbl1.Location = New-Object System.Drawing.Point(25,88)
    $lbl1.AutoSize = $true; $dlg.Controls.Add($lbl1)

    $txtPass = New-Object System.Windows.Forms.TextBox; $txtPass.Location = New-Object System.Drawing.Point(25,115)
    $txtPass.Size = New-Object System.Drawing.Size(430,35); $txtPass.UseSystemPasswordChar = $true
    $txtPass.Font = New-Object System.Drawing.Font("Segoe UI",14); $txtPass.BackColor = [System.Drawing.Color]::FromArgb(50,50,50)
    $txtPass.ForeColor = [System.Drawing.Color]::White; $txtPass.BorderStyle = "FixedSingle"; $dlg.Controls.Add($txtPass)

    $lbl2 = New-Object System.Windows.Forms.Label; $lbl2.Text = "Confirm password:"; $lbl2.Location = New-Object System.Drawing.Point(25,160)
    $lbl2.AutoSize = $true; $dlg.Controls.Add($lbl2)

    $txtConfirm = New-Object System.Windows.Forms.TextBox; $txtConfirm.Location = New-Object System.Drawing.Point(25,187)
    $txtConfirm.Size = New-Object System.Drawing.Size(430,35); $txtConfirm.UseSystemPasswordChar = $true
    $txtConfirm.Font = New-Object System.Drawing.Font("Segoe UI",14); $txtConfirm.BackColor = [System.Drawing.Color]::FromArgb(50,50,50)
    $txtConfirm.ForeColor = [System.Drawing.Color]::White; $txtConfirm.BorderStyle = "FixedSingle"; $dlg.Controls.Add($txtConfirm)

    $lblErr = New-Object System.Windows.Forms.Label; $lblErr.Text = ""; $lblErr.ForeColor = [System.Drawing.Color]::FromArgb(255,80,80)
    $lblErr.Location = New-Object System.Drawing.Point(25,230); $lblErr.Size = New-Object System.Drawing.Size(430,25); $dlg.Controls.Add($lblErr)

    $btnSet = New-Object System.Windows.Forms.Button; $btnSet.Text = "Set Password"
    $btnSet.Size = New-Object System.Drawing.Size(430,45); $btnSet.Location = New-Object System.Drawing.Point(25,265)
    $btnSet.BackColor = [System.Drawing.Color]::FromArgb(0,140,220); $btnSet.ForeColor = [System.Drawing.Color]::White
    $btnSet.FlatStyle = "Flat"; $btnSet.Font = New-Object System.Drawing.Font("Segoe UI",12,[System.Drawing.FontStyle]::Bold)
    $dlg.Controls.Add($btnSet)

    $script:setClicked = $false
    $btnSet.Add_Click({
        if ($txtPass.Text.Length -lt 4) { $lblErr.Text = "Password must be at least 4 characters"; return }
        if ($txtPass.Text -ne $txtConfirm.Text) { $lblErr.Text = "Passwords do not match"; return }
        $script:setClicked = $true; $dlg.DialogResult = [System.Windows.Forms.DialogResult]::OK; $dlg.Close()
    })

    $dlg.ShowDialog() | Out-Null; $pw = $txtPass.Text; $dlg.Dispose()
    if ($script:setClicked -and $pw.Length -ge 4) { return $pw }
    return $null
}

function Require-Password {
    param([string]$Action = "perform this action")
    $pw = Show-PasswordDialog -Title "LockGuard - Authentication" -Prompt "Password to ${Action}:"
    if ($null -eq $pw) { return $false }
    if (Test-Password -Password $pw) { return $true }
    $notifyIcon.ShowBalloonTip(5000, "LockGuard ALERT", "Wrong password for: $Action", [System.Windows.Forms.ToolTipIcon]::Error)
    Write-Log "WRONG PASSWORD for: $Action" "ALERT" | Out-Null
    return $false
}

function Ensure-LogDir { if (-not (Test-Path $Cfg.LogDir)) { New-Item -ItemType Directory -Path $Cfg.LogDir -Force | Out-Null } }
function Get-LogFile { return Join-Path $Cfg.LogDir ("LockGuard_{0}.log" -f (Get-Date -Format 'yyyy-MM')) }

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    Ensure-LogDir
    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    try { Add-Content -Path (Get-LogFile) -Value $line -Encoding UTF8 } catch {}
    return $line
}

function Write-AlertLog {
    param([string]$Message)
    Ensure-LogDir
    $line = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    try { Add-Content -Path (Join-Path $Cfg.LogDir "UnlockAttempts.log") -Value $line -Encoding UTF8 } catch {}
}

function Test-IsNightHours {
    $h = (Get-Date).Hour
    if ($Cfg.LockHour -lt $Cfg.UnlockHour) { return ($h -ge $Cfg.LockHour -and $h -lt $Cfg.UnlockHour) }
    else { return ($h -ge $Cfg.LockHour -or $h -lt $Cfg.UnlockHour) }
}

function Get-ActiveAdapters { return Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object { $_.InterfaceDescription -notlike '*Loopback*' } }

function Disable-AllInternet {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { Write-Log "Cannot disable: not running as Administrator" "WARN" | Out-Null; return $false }
    try {
        $adapters = Get-NetAdapter -ErrorAction Stop | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -notlike '*Loopback*' }
        if ($adapters) { foreach ($a in $adapters) { Disable-NetAdapter -Name $a.Name -Confirm:$false -ErrorAction Stop }; $State.InternetOff = $true; Write-Log "Internet DISABLED" "ALERT" | Out-Null; return $true }
        return $false
    } catch { Write-Log "Disable failed: $($_.Exception.Message)" "ERROR" | Out-Null; return $false }
}

function Enable-AllInternet {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { Write-Log "Cannot enable: not running as Administrator" "WARN" | Out-Null; return $false }
    try {
        $adapters = Get-NetAdapter -ErrorAction Stop | Where-Object { $_.Status -eq 'Disabled' -and $_.InterfaceDescription -notlike '*Loopback*' }
        if ($adapters) { foreach ($a in $adapters) { Enable-NetAdapter -Name $a.Name -Confirm:$false -ErrorAction Stop }; $State.InternetOff = $false; Write-Log "Internet RE-ENABLED" "INFO" | Out-Null; return $true }
        return $false
    } catch { Write-Log "Enable failed: $($_.Exception.Message)" "ERROR" | Out-Null; return $false }
}

$mainForm = New-Object System.Windows.Forms.Form
$mainForm.Text = "$($Cfg.AppTitle) v$($Cfg.Version)"
$mainForm.Size = New-Object System.Drawing.Size(820, 620)
$mainForm.StartPosition = "CenterScreen"
$mainForm.FormBorderStyle = "FixedSingle"
$mainForm.MaximizeBox = $false
$mainForm.BackColor = [System.Drawing.Color]::FromArgb(30,30,30)
$mainForm.ForeColor = [System.Drawing.Color]::White
$mainForm.Font = New-Object System.Drawing.Font("Segoe UI", 9)

# Title
$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = "LockGuard v$($Cfg.Version)"; $titleLabel.Font = New-Object System.Drawing.Font("Segoe UI",16,[System.Drawing.FontStyle]::Bold)
$titleLabel.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255); $titleLabel.Location = New-Object System.Drawing.Point(15,10); $titleLabel.AutoSize = $true; $mainForm.Controls.Add($titleLabel)

$subtitleLabel = New-Object System.Windows.Forms.Label
$subtitleLabel.Text = "Password-protected night internet guard & intrusion monitor"
$subtitleLabel.Font = New-Object System.Drawing.Font("Segoe UI",9,[System.Drawing.FontStyle]::Italic)
$subtitleLabel.ForeColor = [System.Drawing.Color]::FromArgb(120,120,120); $subtitleLabel.Location = New-Object System.Drawing.Point(17,42); $subtitleLabel.AutoSize = $true; $mainForm.Controls.Add($subtitleLabel)

# Status Panel
$statusPanel = New-Object System.Windows.Forms.Panel; $statusPanel.Location = New-Object System.Drawing.Point(15,75)
$statusPanel.Size = New-Object System.Drawing.Size(380,200); $statusPanel.BorderStyle = "FixedSingle"; $statusPanel.BackColor = [System.Drawing.Color]::FromArgb(40,40,40); $mainForm.Controls.Add($statusPanel)

$stTitle = New-Object System.Windows.Forms.Label; $stTitle.Text = "STATUS"; $stTitle.Font = New-Object System.Drawing.Font("Segoe UI",11,[System.Drawing.FontStyle]::Bold); $stTitle.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255); $stTitle.Location = New-Object System.Drawing.Point(10,8); $stTitle.AutoSize = $true; $statusPanel.Controls.Add($stTitle)

$lblNightDay = New-Object System.Windows.Forms.Label; $lblNightDay.Text = "Time: DAY"; $lblNightDay.Font = New-Object System.Drawing.Font("Segoe UI",10); $lblNightDay.Location = New-Object System.Drawing.Point(10,38); $lblNightDay.Size = New-Object System.Drawing.Size(350,22); $statusPanel.Controls.Add($lblNightDay)

$lblInternet = New-Object System.Windows.Forms.Label; $lblInternet.Text = "Internet: ON"; $lblInternet.Font = New-Object System.Drawing.Font("Segoe UI",10); $lblInternet.Location = New-Object System.Drawing.Point(10,62); $lblInternet.Size = New-Object System.Drawing.Size(350,22); $statusPanel.Controls.Add($lblInternet)

$lblHours = New-Object System.Windows.Forms.Label; $lblHours.Text = "Night: $($Cfg.LockHour):00 - $($Cfg.UnlockHour):00"; $lblHours.Font = New-Object System.Drawing.Font("Segoe UI",10); $lblHours.Location = New-Object System.Drawing.Point(10,86); $lblHours.Size = New-Object System.Drawing.Size(350,22); $lblHours.ForeColor = [System.Drawing.Color]::FromArgb(180,180,180); $statusPanel.Controls.Add($lblHours)

$lblLastEvent = New-Object System.Windows.Forms.Label; $lblLastEvent.Text = "Last: None"; $lblLastEvent.Font = New-Object System.Drawing.Font("Segoe UI",10); $lblLastEvent.Location = New-Object System.Drawing.Point(10,110); $lblLastEvent.Size = New-Object System.Drawing.Size(350,22); $lblLastEvent.ForeColor = [System.Drawing.Color]::FromArgb(180,180,180); $statusPanel.Controls.Add($lblLastEvent)

$lblMonitor = New-Object System.Windows.Forms.Label; $lblMonitor.Text = "Monitor: ACTIVE"; $lblMonitor.Font = New-Object System.Drawing.Font("Segoe UI",10,[System.Drawing.FontStyle]::Bold); $lblMonitor.Location = New-Object System.Drawing.Point(10,134); $lblMonitor.Size = New-Object System.Drawing.Size(350,22); $lblMonitor.ForeColor = [System.Drawing.Color]::FromArgb(0,200,100); $statusPanel.Controls.Add($lblMonitor)

$lblProtected = New-Object System.Windows.Forms.Label; $lblProtected.Text = "Protection: LOCKED (password required)"; $lblProtected.Font = New-Object System.Drawing.Font("Segoe UI",10,[System.Drawing.FontStyle]::Bold); $lblProtected.Location = New-Object System.Drawing.Point(10,160); $lblProtected.Size = New-Object System.Drawing.Size(350,22); $lblProtected.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255); $statusPanel.Controls.Add($lblProtected)

# Adapter Panel
$adapterPanel = New-Object System.Windows.Forms.Panel; $adapterPanel.Location = New-Object System.Drawing.Point(410,75)
$adapterPanel.Size = New-Object System.Drawing.Size(380,200); $adapterPanel.BorderStyle = "FixedSingle"; $adapterPanel.BackColor = [System.Drawing.Color]::FromArgb(40,40,40); $mainForm.Controls.Add($adapterPanel)

$adTitle = New-Object System.Windows.Forms.Label; $adTitle.Text = "NETWORK ADAPTERS"; $adTitle.Font = New-Object System.Drawing.Font("Segoe UI",11,[System.Drawing.FontStyle]::Bold); $adTitle.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255); $adTitle.Location = New-Object System.Drawing.Point(10,8); $adTitle.AutoSize = $true; $adapterPanel.Controls.Add($adTitle)

$adapterListLabel = New-Object System.Windows.Forms.Label; $adapterListLabel.Text = "Loading..."; $adapterListLabel.Font = New-Object System.Drawing.Font("Consolas",9); $adapterListLabel.Location = New-Object System.Drawing.Point(10,35); $adapterListLabel.Size = New-Object System.Drawing.Size(355,155); $adapterListLabel.ForeColor = [System.Drawing.Color]::FromArgb(200,200,200); $adapterPanel.Controls.Add($adapterListLabel)

# Buttons
$btnDisable = New-Object System.Windows.Forms.Button; $btnDisable.Text = "Disable Internet"; $btnDisable.Size = New-Object System.Drawing.Size(180,40); $btnDisable.Location = New-Object System.Drawing.Point(15,290); $btnDisable.BackColor = [System.Drawing.Color]::FromArgb(180,40,40); $btnDisable.ForeColor = [System.Drawing.Color]::White; $btnDisable.FlatStyle = "Flat"; $btnDisable.Font = New-Object System.Drawing.Font("Segoe UI",10,[System.Drawing.FontStyle]::Bold); $btnDisable.Cursor = [System.Windows.Forms.Cursors]::Hand; $mainForm.Controls.Add($btnDisable)

$btnEnable = New-Object System.Windows.Forms.Button; $btnEnable.Text = "Enable Internet"; $btnEnable.Size = New-Object System.Drawing.Size(180,40); $btnEnable.Location = New-Object System.Drawing.Point(210,290); $btnEnable.BackColor = [System.Drawing.Color]::FromArgb(40,160,80); $btnEnable.ForeColor = [System.Drawing.Color]::White; $btnEnable.FlatStyle = "Flat"; $btnEnable.Font = New-Object System.Drawing.Font("Segoe UI",10,[System.Drawing.FontStyle]::Bold); $btnEnable.Cursor = [System.Windows.Forms.Cursors]::Hand; $mainForm.Controls.Add($btnEnable)

$btnRefresh = New-Object System.Windows.Forms.Button; $btnRefresh.Text = "Refresh"; $btnRefresh.Size = New-Object System.Drawing.Size(120,30); $btnRefresh.Location = New-Object System.Drawing.Point(410,290); $btnRefresh.BackColor = [System.Drawing.Color]::FromArgb(60,60,60); $btnRefresh.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255); $btnRefresh.FlatStyle = "Flat"; $mainForm.Controls.Add($btnRefresh)

$btnChangePw = New-Object System.Windows.Forms.Button; $btnChangePw.Text = "Change Password"; $btnChangePw.Size = New-Object System.Drawing.Size(130,30); $btnChangePw.Location = New-Object System.Drawing.Point(540,290); $btnChangePw.BackColor = [System.Drawing.Color]::FromArgb(60,60,60); $btnChangePw.ForeColor = [System.Drawing.Color]::FromArgb(180,180,180); $btnChangePw.FlatStyle = "Flat"; $mainForm.Controls.Add($btnChangePw)

$btnClearLog = New-Object System.Windows.Forms.Button; $btnClearLog.Text = "Clear Log"; $btnClearLog.Size = New-Object System.Drawing.Size(110,30); $btnClearLog.Location = New-Object System.Drawing.Point(680,290); $btnClearLog.BackColor = [System.Drawing.Color]::FromArgb(60,60,60); $btnClearLog.ForeColor = [System.Drawing.Color]::FromArgb(180,180,180); $btnClearLog.FlatStyle = "Flat"; $mainForm.Controls.Add($btnClearLog)

# Log Viewer
$logLabel = New-Object System.Windows.Forms.Label; $logLabel.Text = "LOG"; $logLabel.Font = New-Object System.Drawing.Font("Segoe UI",11,[System.Drawing.FontStyle]::Bold); $logLabel.ForeColor = [System.Drawing.Color]::FromArgb(0,180,255); $logLabel.Location = New-Object System.Drawing.Point(15,332); $logLabel.AutoSize = $true; $mainForm.Controls.Add($logLabel)

$logBox = New-Object System.Windows.Forms.TextBox; $logBox.Multiline = $true; $logBox.ScrollBars = "Vertical"; $logBox.ReadOnly = $true; $logBox.BackColor = [System.Drawing.Color]::FromArgb(20,20,20); $logBox.ForeColor = [System.Drawing.Color]::FromArgb(0,220,0); $logBox.Font = New-Object System.Drawing.Font("Consolas",9); $logBox.Location = New-Object System.Drawing.Point(15,355); $logBox.Size = New-Object System.Drawing.Size(775,215); $logBox.BorderStyle = "None"; $mainForm.Controls.Add($logBox)

# System Tray
$notifyIcon = New-Object System.Windows.Forms.NotifyIcon; $notifyIcon.Text = "LockGuard"; $notifyIcon.Visible = $true; $notifyIcon.Icon = [System.Drawing.SystemIcons]::Shield
$trayMenu = New-Object System.Windows.Forms.ContextMenuStrip; $trayMenu.BackColor = [System.Drawing.Color]::FromArgb(40,40,40); $trayMenu.ForeColor = [System.Drawing.Color]::White
$menuShow = $trayMenu.Items.Add("Show Window"); $menuShow.Font = New-Object System.Drawing.Font("Segoe UI",9,[System.Drawing.FontStyle]::Bold)
$trayMenu.Items.Add("-")
$menuDisable = $trayMenu.Items.Add("Disable Internet"); $menuEnable = $trayMenu.Items.Add("Enable Internet")
$trayMenu.Items.Add("-")
$menuStatus = $trayMenu.Items.Add("Status: --"); $menuStatus.ForeColor = [System.Drawing.Color]::FromArgb(150,150,150); $menuStatus.Enabled = $false
$trayMenu.Items.Add("-")
$menuExit = $trayMenu.Items.Add("Exit (password required)")
$notifyIcon.ContextMenuStrip = $trayMenu

function Update-UI {
    $State.IsNightHours = Test-IsNightHours
    if ($State.IsNightHours) { $lblNightDay.Text = "Time: NIGHT (protection ON)"; $lblNightDay.ForeColor = [System.Drawing.Color]::FromArgb(255,100,100) }
    else { $lblNightDay.Text = "Time: DAY (monitoring)"; $lblNightDay.ForeColor = [System.Drawing.Color]::FromArgb(0,200,100) }

    $adapters = Get-ActiveAdapters
    $upCount = ($adapters | Where-Object { $_.Status -eq 'Up' }).Count
    $downCount = ($adapters | Where-Object { $_.Status -eq 'Disabled' }).Count
    if ($State.InternetOff -or $downCount -gt 0) { $lblInternet.Text = "Internet: OFF ($downCount disabled)"; $lblInternet.ForeColor = [System.Drawing.Color]::FromArgb(255,80,80) }
    else { $lblInternet.Text = "Internet: ON ($upCount active)"; $lblInternet.ForeColor = [System.Drawing.Color]::FromArgb(0,200,100) }

    $adapterText = ""
    foreach ($a in (Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object { $_.InterfaceDescription -notlike '*Loopback*' })) {
        $icon = if ($a.Status -eq 'Up') {"[+]"} elseif ($a.Status -eq 'Disabled') {"[-]"} else {"[ ]"}
        $adapterText += "$icon $($a.Name)" + [char]9 + "$($a.Status)" + [char]9 + "$($a.InterfaceDescription)" + [Environment]::NewLine
    }
    $adapterListLabel.Text = $adapterText.TrimEnd()

    $btnDisable.Enabled = (-not $State.InternetOff); $btnEnable.Enabled = $State.InternetOff
    $menuDisable.Enabled = (-not $State.InternetOff); $menuEnable.Enabled = $State.InternetOff

    $m = if ($State.IsNightHours) {"NIGHT"} else {"DAY"}; $n = if ($State.InternetOff) {"OFF"} else {"ON"}
    $menuStatus.Text = "Mode: $m | Net: $n"; $notifyIcon.Text = "LockGuard [$m|$n]"
    if ($State.IsNightHours -and -not $State.InternetOff) { $notifyIcon.Icon = [System.Drawing.SystemIcons]::Warning }
    elseif ($State.InternetOff) { $notifyIcon.Icon = [System.Drawing.SystemIcons]::Error }
    else { $notifyIcon.Icon = [System.Drawing.SystemIcons]::Shield }
}

function Add-LogLine {
    param([string]$Text)
    $logBox.AppendText("[$(Get-Date -Format 'HH:mm:ss')] $Text`r`n")
    $State.LastEvent = $Text; $lblLastEvent.Text = "Last: $Text"; $lblLastEvent.ForeColor = [System.Drawing.Color]::FromArgb(180,180,180)
}

# Button handlers - all require password
$btnDisable.Add_Click({
    if (Require-Password -Action "disable internet") {
        Add-LogLine "Manual disable..."; $ok = Disable-AllInternet
        if ($ok) { Add-LogLine "Internet disabled" } else { Add-LogLine "FAILED" }; Update-UI
    }
})

$btnEnable.Add_Click({
    if (Require-Password -Action "enable internet") {
        Add-LogLine "Manual enable..."; $ok = Enable-AllInternet
        if ($ok) { Add-LogLine "Internet enabled" } else { Add-LogLine "No disabled adapters" }; Update-UI
    }
})

$btnRefresh.Add_Click({ Update-UI; Add-LogLine "Refreshed" })
$btnClearLog.Add_Click({ $logBox.Clear() })

$btnChangePw.Add_Click({
    if (Require-Password -Action "change password") {
        $newPw = Show-PasswordSetup
        if ($newPw) { $State.PasswordSalt = New-RandomSalt; $State.PasswordHash = Hash-Password -Password $newPw -Salt $State.PasswordSalt; Save-Config; Add-LogLine "Password changed" }
    }
})

# Tray handlers
$menuShow.Add_Click({ $mainForm.Show(); $mainForm.WindowState = "Normal"; $mainForm.BringToFront() })
$menuDisable.Add_Click({ if (Require-Password -Action "disable internet") { Disable-AllInternet | Out-Null; Add-LogLine "Disabled via tray"; Update-UI } })
$menuEnable.Add_Click({ if (Require-Password -Action "enable internet") { Enable-AllInternet | Out-Null; Add-LogLine "Enabled via tray"; Update-UI } })
$menuExit.Add_Click({
    if (Require-Password -Action "exit LockGuard") {
        if ($State.InternetOff) { Enable-AllInternet | Out-Null }
        Write-Log "LockGuard exited (password verified)" "INFO" | Out-Null
        $notifyIcon.Visible = $false; $notifyIcon.Dispose(); [System.Windows.Forms.Application]::Exit()
    }
})

# Form closing -> minimize to tray (password needed to exit)
$mainForm.Add_FormClosing({
    param($s, $e); $e.Cancel = $true; $mainForm.Hide()
    $notifyIcon.ShowBalloonTip(2000, "LockGuard", "In tray. Right-click to exit (password required).", [System.Windows.Forms.ToolTipIcon]::Info)
})

$notifyIcon.Add_DoubleClick({ $mainForm.Show(); $mainForm.WindowState = "Normal"; $mainForm.BringToFront() })

# Background Monitor Timer
$monitorTimer = New-Object System.Windows.Forms.Timer; $monitorTimer.Interval = 5000
$monitorTimer.Add_Tick({
    try {
        $wasNight = $State.IsNightHours; $State.IsNightHours = Test-IsNightHours
        if ($State.IsNightHours -and -not $wasNight) { Add-LogLine ">>> Night STARTED"; $notifyIcon.ShowBalloonTip(5000, "LockGuard", "Night mode ON", [System.Windows.Forms.ToolTipIcon]::Warning) }
        if (-not $State.IsNightHours -and $wasNight) {
            Add-LogLine ">>> Night ENDED"
            if ($State.InternetOff) { Enable-AllInternet | Out-Null; Add-LogLine "Internet auto-re-enabled (morning)" }
        }
        $events = Get-WinEvent -FilterHashtable @{ LogName = 'Security'; Id = @(4800,4801,4802,4803); StartTime = (Get-Date).AddSeconds(-8) } -MaxEvents 20 -ErrorAction SilentlyContinue
        foreach ($evt in $events) {
            $user = try { $evt.Properties[1].Value } catch { 'Unknown' }
            switch ($evt.Id) {
                4800 { Add-LogLine "LOCKED by $user"; Write-Log "LOCKED by $user" "INFO" | Out-Null
                    if ($State.IsNightHours) { Add-LogLine "NIGHT LOCK - disabling internet"; Disable-AllInternet | Out-Null; $notifyIcon.ShowBalloonTip(5000, "ALERT", "Locked at NIGHT - internet cut", [System.Windows.Forms.ToolTipIcon]::Warning) }
                }
                4801 { if ($State.IsNightHours) {
                    $alert = "!!! UNLOCK ATTEMPT by $user at NIGHT !!!"; Add-LogLine $alert; Add-AlertLog $alert; Write-Log $alert "ALERT" | Out-Null
                    $notifyIcon.ShowBalloonTip(8000, "INTRUSION ALERT", "Unlock at NIGHT by $user!", [System.Windows.Forms.ToolTipIcon]::Error)
                    Enable-AllInternet | Out-Null; Add-LogLine "Internet re-enabled for user"
                } else { Add-LogLine "UNLOCKED by $user (day)"; if ($State.InternetOff) { Enable-AllInternet | Out-Null } } }
                4802 { Add-LogLine "Screensaver by $user" }
                4803 { if ($State.IsNightHours) { Add-LogLine "Screensaver off at NIGHT by $user"; Write-Log "Screensaver off at night by $user" "WARN" | Out-Null } }
            }
        }
        Update-UI
    } catch {}
})

# === STARTUP ===
$errLog = "C:\ProgramData\LockGuard\Logs\error.log"
try {

Ensure-LogDir

# Load or create password
$loaded = Load-Config
if (-not $loaded) {
    # Show password setup dialog (no parent form needed)
    $newPw = Show-PasswordSetup
    if ($newPw) {
        $State.PasswordSalt = New-RandomSalt; $State.PasswordHash = Hash-Password -Password $newPw -Salt $State.PasswordSalt; Save-Config
        Write-Log "Password set successfully" "INFO" | Out-Null
    } else {
        [System.Windows.Forms.MessageBox]::Show("A password is required to use LockGuard.`n`nPlease run LockGuard again and set a password.", "Setup Required", "OK", "Warning")
        $notifyIcon.Visible = $false; $notifyIcon.Dispose()
        exit
    }
}

Write-Log "LockGuard v$($Cfg.Version) started" "INFO" | Out-Null
Write-Log "User: $env:USERNAME | PC: $env:COMPUTERNAME" "INFO" | Out-Null
Write-Log "Night: $($Cfg.LockHour):00 - $($Cfg.UnlockHour):00" "INFO" | Out-Null

Update-UI; Add-LogLine "LockGuard started - password protection ACTIVE"
$State.IsNightHours = Test-IsNightHours
if ($State.IsNightHours) { Add-LogLine "NIGHT hours - protection ON" } else { Add-LogLine "DAY hours - monitoring" }

try { $p = auditpol /get /subcategory:"Logon" 2>&1; if ($p -match 'Success') { Add-LogLine "Audit policy OK" } else { Add-LogLine "WARNING: Run Install.bat as admin for audit policies" } } catch {}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    Add-LogLine "Running as Administrator - full control"
} else {
    Add-LogLine "WARNING: Not admin - adapter control disabled (run as admin for full control)"
    $lblProtected.Text = "Protection: LIMITED (run as admin)"
    $lblProtected.ForeColor = [System.Drawing.Color]::FromArgb(255,180,0)
}

$monitorTimer.Start()
$notifyIcon.ShowBalloonTip(3000, "LockGuard", "Running. Password required to exit.", [System.Windows.Forms.ToolTipIcon]::Info)
[System.Windows.Forms.Application]::Run($mainForm)

} catch {
    # Write error to file so we can debug
    $errMsg = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') FATAL ERROR: $($_.Exception.Message)`n$($_.ScriptStackTrace)"
    try { Add-Content -Path $errLog -Value $errMsg -Encoding UTF8 } catch {}
    [System.Windows.Forms.MessageBox]::Show("LockGuard crashed:`n`n$($_.Exception.Message)", "LockGuard Error", "OK", "Error")
}
