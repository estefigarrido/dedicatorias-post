@echo off
REM Permite que celulares y la tablet (misma red Wi-Fi) abran el sitio en el puerto 8080.
REM Se ejecuta UNA sola vez, como administrador.
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Pidiendo permisos de administrador...
  powershell -NoProfile -Command "Start-Process '%~f0' -Verb RunAs"
  exit /b
)
netsh http add urlacl url=http://+:8080/ sddl=D:(A;;GX;;;WD)
netsh advfirewall firewall add rule name="post dedicatorias 8080" dir=in action=allow protocol=TCP localport=8080
echo.
echo Listo. Ya podes cerrar esta ventana y abrir iniciar.bat
pause
