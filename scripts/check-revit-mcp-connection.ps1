# Kiểm tra nhanh kết nối Revit MCP (port 8080-8099)
# Chạy: .\scripts\check-revit-mcp-connection.ps1

$ports = 8080..8099
$found = @()

foreach ($p in $ports) {
    $conn = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    if ($conn) {
        $found += $p
    }
}

Write-Host ""
if ($found.Count -eq 0) {
    Write-Host "  [KHONG CO] Revit MCP server dang chay." -ForegroundColor Red
    Write-Host ""
    Write-Host "  Lam theo thu tu:" -ForegroundColor Yellow
    Write-Host "  1. Mo Revit"
    Write-Host "  2. Tab Add-Ins > DeepBim-MCP > MCP Switch"
    Write-Host "  3. Bam nut Start (phai thay Running + so port)"
    Write-Host "  4. Thu lai tool say_hello tu Cursor/Claude"
    Write-Host ""
} else {
    Write-Host "  [OK] Revit MCP server dang listen tren port: $($found -join ', ')" -ForegroundColor Green
    Write-Host "  Neu van bi 'Method not found': mo Revit > Settings > bat cac command (say_hello) > Save, roi restart Revit." -ForegroundColor Gray
    Write-Host ""
}
