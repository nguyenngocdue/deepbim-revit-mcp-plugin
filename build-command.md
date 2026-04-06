# 1. Build MSI cho tất cả các version (từ RevitVersions.json)
.\installers\msi\Build-Installer.ps1

# 2. Build MSI cho Revit 2025 cụ thể
.\installers\msi\Build-Installer.ps1 -Versions 2025

# 3. Build cho nhiều version
.\installers\msi\Build-Installer.ps1 -Versions 2024,2025

# 4. Build với configuration Release (mặc định)
.\installers\msi\Build-Installer.ps1 -Configuration Release

# 5. Build với phiên bản sản phẩm tùy chỉnh
.\installers\msi\Build-Installer.ps1 -Versions 2025 -ProductVersion 2.0.0

# 6. Skip build plugin (dùng output hiện có)
.\installers\msi\Build-Installer.ps1 -Versions 2025 -SkipBuild