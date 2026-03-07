# Test kết nối DeepBim-MCP - chạy khi Revit đã Start
# Chạy script này để kiểm tra Revit plugin có đang lắng nghe không

$ErrorActionPreference = "Stop"
$ports = 8080..8099

Write-Host "`n=== Test DeepBim-MCP Connection ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check if any port is listening
$listening = $false
foreach ($p in $ports) {
    $conn = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    if ($conn) {
        Write-Host "[OK] Port $p is LISTENING (Revit plugin is running)" -ForegroundColor Green
        $listening = $true
        break
    }
}

if (-not $listening) {
    Write-Host "[FAIL] No process listening on ports 8080-8099" -ForegroundColor Red
    Write-Host "       -> Open Revit, click MCP Switch, then Start" -ForegroundColor Yellow
    exit 1
}

# 2. Try to send say_hello
$port = $conn.LocalPort
$msg = '{"jsonrpc":"2.0","method":"say_hello","params":{"name":"Test"},"id":"1"}'

Write-Host "`nSending say_hello to localhost:$port ..." -ForegroundColor Gray

try {
    $client = New-Object System.Net.Sockets.TcpClient("localhost", $port)
    $stream = $client.GetStream()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($msg)
    $stream.Write($bytes, 0, $bytes.Length)
    
    $buffer = New-Object byte[] 4096
    $read = $stream.Read($buffer, 0, 4096)
    $response = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
    $client.Close()
    
    if ($response -match '"error"') {
        Write-Host "[WARN] Plugin responded but returned error:" -ForegroundColor Yellow
        Write-Host $response
    } else {
        Write-Host "[OK] Plugin responded successfully:" -ForegroundColor Green
        Write-Host $response
    }
} catch {
    Write-Host "[FAIL] Could not connect: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Test complete ===" -ForegroundColor Cyan
Write-Host "If you see [OK] above, Claude should be able to call say_hello." -ForegroundColor Gray
