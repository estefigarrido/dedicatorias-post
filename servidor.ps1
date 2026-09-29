# Servidor de dedicatorias post.
# - Sirve el sitio (carpeta docs/)
# - Recibe las dedicatorias:      POST /api/notas
# - Unity las lee:                GET  /api/notas?desde=<ultimoId>
# - Moderación (ocultar una):     DELETE /api/notas/<id>?clave=<ClaveAdmin>
# Sin dependencias: usa PowerShell 5.1 + .NET HttpListener.

param(
  [int]$Puerto = 8080,
  [string]$ClaveAdmin = 'post-admin'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$publico = [IO.Path]::GetFullPath((Join-Path $raiz 'docs'))
$datosDir = Join-Path $raiz 'data'
$datosArchivo = Join-Path $datosDir 'notas.json'
New-Item -ItemType Directory -Force $datosDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

$COLORES = @{ azul = '#7aa1ff'; verde = '#49b867'; violeta = '#ab8ae6'; rosa = '#f79ee1'; crema = '#ede8db' }
$MAX_PARA = 20
$MAX_MSG = 150
$MIN_MSG = 3
$ESPERA_SEG = 10   # tiempo mínimo entre publicaciones desde un mismo dispositivo

# Palabras que no se muestran en una pantalla pública (editable).
$PROHIBIDAS = @(
  'puto','puta','putos','putas','pelotudo','pelotuda','pelotudos','forro','forra','forros',
  'conchudo','conchuda','la concha','pija','poronga','culiado','culeado','mierda','hdp',
  'hijo de puta','trolo','trola','mogolico','mogolica','sorete','garca','chupala','chupame',
  'imbecil','idiota','tarado','tarada','cagon','negro de mierda'
)
$regexProhibidas = '(^|[^a-z])(' + (($PROHIBIDAS | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')([^a-z]|$)'

# ---------------- datos ----------------
$notas = New-Object System.Collections.ArrayList
$eliminadas = New-Object System.Collections.ArrayList
if (Test-Path $datosArchivo) {
  $txt = [IO.File]::ReadAllText($datosArchivo, $utf8)
  if ($txt.Trim()) { foreach ($n in (ConvertFrom-Json $txt)) { [void]$notas.Add($n) } }
}
$idArchivo = Join-Path $datosDir 'ultimo-id.txt'
$script:ultimoId = 0
if (Test-Path $idArchivo) { [void][int]::TryParse(([IO.File]::ReadAllText($idArchivo)).Trim(), [ref]$script:ultimoId) }
foreach ($n in $notas) { if ([int]$n.id -gt $script:ultimoId) { $script:ultimoId = [int]$n.id } }
$ultimoPorIp = @{}

function Guardar-Notas {
  $json = ConvertTo-Json -InputObject @($notas.ToArray()) -Depth 5
  [IO.File]::WriteAllText($datosArchivo, $json, $utf8)
  [IO.File]::WriteAllText($idArchivo, [string]$script:ultimoId, $utf8)
}

function Get-Tamano([int]$largo) {
  if ($largo -le 40) { 'S' } elseif ($largo -le 120) { 'M' } elseif ($largo -le 170) { 'L' } else { 'XL' }
}

function Normalizar([string]$s) {
  $d = $s.ToLowerInvariant().Normalize([Text.NormalizationForm]::FormD)
  $sb = New-Object Text.StringBuilder
  foreach ($ch in $d.ToCharArray()) {
    if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -ne [Globalization.UnicodeCategory]::NonSpacingMark) { [void]$sb.Append($ch) }
  }
  $t = $sb.ToString()
  $t = $t -replace '0','o' -replace '1','i' -replace '3','e' -replace '4','a' -replace '5','s' -replace '@','a' -replace '\$','s'
  $t = $t -replace '(.)\1{2,}', '$1'
  return $t
}

function Limpiar([string]$s) { if ($null -eq $s) { return '' }; return ($s -replace '\s+', ' ').Trim() }

# ---------------- respuestas ----------------
function Enviar($ctx, [int]$status, [byte[]]$bytes, [string]$tipo) {
  $r = $ctx.Response
  $r.StatusCode = $status
  $r.ContentType = $tipo
  $r.AddHeader('Access-Control-Allow-Origin', '*')
  $r.AddHeader('Access-Control-Allow-Methods', 'GET, POST, DELETE, OPTIONS')
  $r.AddHeader('Access-Control-Allow-Headers', 'Content-Type')
  $r.AddHeader('Cache-Control', 'no-store')
  $r.ContentLength64 = $bytes.Length
  if ($bytes.Length -gt 0) { $r.OutputStream.Write($bytes, 0, $bytes.Length) }
  $r.OutputStream.Close()
}
function EnviarJson($ctx, [int]$status, $obj) {
  $json = ConvertTo-Json -InputObject $obj -Depth 6 -Compress
  Enviar $ctx $status ($utf8.GetBytes($json)) 'application/json; charset=utf-8'
}
function Error-Json($ctx, [int]$status, [string]$mensaje) { EnviarJson $ctx $status ([ordered]@{ ok = $false; error = $mensaje }) }

$TIPOS = @{
  '.html' = 'text/html; charset=utf-8'; '.css' = 'text/css; charset=utf-8'; '.js' = 'text/javascript; charset=utf-8'
  '.json' = 'application/json; charset=utf-8'; '.png' = 'image/png'; '.jpg' = 'image/jpeg'; '.svg' = 'image/svg+xml'
  '.ico' = 'image/x-icon'; '.webp' = 'image/webp'; '.woff2' = 'font/woff2'
}

function Servir-Estatico($ctx, [string]$ruta) {
  $rel = $ruta.TrimStart('/')
  if ($rel -eq '') { $rel = 'index.html' }
  $full = [IO.Path]::GetFullPath((Join-Path $publico $rel))
  if (-not $full.StartsWith($publico, [StringComparison]::OrdinalIgnoreCase)) { Error-Json $ctx 403 'Prohibido'; return }
  if (Test-Path $full -PathType Container) { $full = Join-Path $full 'index.html' }
  if (-not (Test-Path $full -PathType Leaf)) { Error-Json $ctx 404 'No encontrado'; return }
  $ext = [IO.Path]::GetExtension($full).ToLowerInvariant()
  $tipo = $TIPOS[$ext]; if (-not $tipo) { $tipo = 'application/octet-stream' }
  Enviar $ctx 200 ([IO.File]::ReadAllBytes($full)) $tipo
}

# ---------------- API ----------------
function Crear-Nota($ctx) {
  $req = $ctx.Request
  if ($req.ContentLength64 -gt 8000) { Error-Json $ctx 413 'El mensaje es demasiado largo.'; return }
  $lector = New-Object IO.StreamReader($req.InputStream, $utf8)
  $cuerpo = $lector.ReadToEnd()
  try { $datos = ConvertFrom-Json $cuerpo } catch { Error-Json $ctx 400 'Datos inválidos.'; return }

  $para = Limpiar ([string]$datos.para)
  $mensaje = Limpiar ([string]$datos.mensaje)
  $color = ([string]$datos.color).ToLowerInvariant()

  if ($para.Length -lt 1) { Error-Json $ctx 400 'Escribí para quién es.'; return }
  if ($para.Length -gt $MAX_PARA) { Error-Json $ctx 400 "El nombre puede tener hasta $MAX_PARA caracteres."; return }
  if ($mensaje.Length -lt $MIN_MSG) { Error-Json $ctx 400 'Escribí un poco más en tu mensaje.'; return }
  if ($mensaje.Length -gt $MAX_MSG) { Error-Json $ctx 400 "El mensaje puede tener hasta $MAX_MSG caracteres."; return }
  if (-not $COLORES.ContainsKey($color)) { $color = 'verde' }

  $revisar = Normalizar "$para $mensaje"
  if ($revisar -match $regexProhibidas) { EnviarJson $ctx 400 ([ordered]@{ ok = $false; motivo = 'ofensivo'; error = 'No está permitido publicar este tipo de contenido.' }); return }
  if ($revisar -match '(https?://|www\.|\.com\b|\.ar\b)') { EnviarJson $ctx 400 ([ordered]@{ ok = $false; motivo = 'link'; error = 'No se pueden incluir links.' }); return }

  $ip = $req.RemoteEndPoint.Address.ToString()
  $ahora = Get-Date
  if ($ultimoPorIp.ContainsKey($ip) -and ($ahora - $ultimoPorIp[$ip]).TotalSeconds -lt $ESPERA_SEG) {
    Error-Json $ctx 429 'Esperá unos segundos antes de publicar otra dedicatoria.'; return
  }
  $ultimoPorIp[$ip] = $ahora

  $script:ultimoId++
  $nota = [pscustomobject][ordered]@{
    id      = $script:ultimoId
    para    = $para
    mensaje = $mensaje
    color   = $color
    hex     = $COLORES[$color]
    tamano  = (Get-Tamano $mensaje.Length)
    creada  = $ahora.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
  }
  [void]$notas.Add($nota)
  Guardar-Notas
  Write-Host ("  + nota #{0} para {1} ({2}, {3})" -f $nota.id, $nota.para, $nota.color, $nota.tamano) -ForegroundColor Green
  EnviarJson $ctx 201 ([ordered]@{ ok = $true; nota = $nota })
}

function Listar-Notas($ctx) {
  $q = $ctx.Request.QueryString
  $n = 0
  $desde = 0; if ([int]::TryParse([string]$q['desde'], [ref]$n)) { $desde = $n }
  $limite = 200; if ([int]::TryParse([string]$q['limite'], [ref]$n) -and $n -gt 0) { $limite = $n }
  $lista = @($notas | Where-Object { [int]$_.id -gt $desde })
  if ($lista.Count -gt $limite) { $lista = @($lista | Select-Object -Last $limite) }
  EnviarJson $ctx 200 ([ordered]@{
    ok         = $true
    notas      = $lista
    ultimoId   = $script:ultimoId
    total      = $notas.Count
    eliminadas = @($eliminadas.ToArray())
  })
}

function Eliminar-Nota($ctx, [int]$id) {
  if ([string]$ctx.Request.QueryString['clave'] -ne $ClaveAdmin) { Error-Json $ctx 403 'Clave incorrecta.'; return }
  $nota = $notas | Where-Object { [int]$_.id -eq $id } | Select-Object -First 1
  if (-not $nota) { Error-Json $ctx 404 'No existe esa nota.'; return }
  $notas.Remove($nota)
  [void]$eliminadas.Add($id)
  Guardar-Notas
  Write-Host "  - nota #$id ocultada" -ForegroundColor Yellow
  EnviarJson $ctx 200 ([ordered]@{ ok = $true; id = $id })
}

function Atender($ctx) {
  $req = $ctx.Request
  $ruta = [Uri]::UnescapeDataString($req.Url.AbsolutePath)
  $metodo = $req.HttpMethod

  if ($metodo -eq 'OPTIONS') { Enviar $ctx 204 ([byte[]]@()) 'text/plain'; return }
  if ($ruta -eq '/api/notas' -and $metodo -eq 'POST') { Crear-Nota $ctx; return }
  if ($ruta -eq '/api/notas' -and $metodo -eq 'GET') { Listar-Notas $ctx; return }
  if ($ruta -match '^/api/notas/(\d+)$' -and $metodo -eq 'DELETE') { Eliminar-Nota $ctx ([int]$Matches[1]); return }
  if ($ruta -eq '/api/salud') { EnviarJson $ctx 200 ([ordered]@{ ok = $true; notas = $notas.Count }); return }
  if ($ruta.StartsWith('/api/')) { Error-Json $ctx 404 'Ruta inexistente.'; return }
  if ($metodo -ne 'GET' -and $metodo -ne 'HEAD') { Error-Json $ctx 405 'Método no permitido.'; return }
  Servir-Estatico $ctx $ruta
}

# ---------------- arranque ----------------
$enRed = $true
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://+:$Puerto/")
try { $listener.Start() }
catch {
  $enRed = $false
  $listener = New-Object System.Net.HttpListener
  $listener.Prefixes.Add("http://localhost:$Puerto/")
  $listener.Start()
}

Write-Host ''
Write-Host '  post. - dedicatorias' -ForegroundColor Green
Write-Host "  En esta compu:   http://localhost:$Puerto/"
Write-Host "  Modo tablet:     http://localhost:$Puerto/?modo=tablet"
Write-Host "  API para Unity:  http://localhost:$Puerto/api/notas?desde=0"
if ($enRed) {
  $ips = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) | Where-Object { $_.AddressFamily -eq 'InterNetwork' -and -not $_.ToString().StartsWith('169.254') }
  foreach ($ip in $ips) { Write-Host "  Desde el celu:   http://$($ip):$Puerto/" -ForegroundColor Cyan }
} else {
  Write-Host '  (Solo accesible desde esta compu. Para abrirlo desde celulares en la misma red,' -ForegroundColor Yellow
  Write-Host '   ejecuta una vez habilitar-red.bat como administrador y volve a iniciar.)' -ForegroundColor Yellow
}
Write-Host "  Notas guardadas: $($notas.Count)  -  Ctrl+C para cerrar"
Write-Host ''

try {
  while ($listener.IsListening) {
    $async = $listener.BeginGetContext($null, $null)
    while (-not $async.AsyncWaitHandle.WaitOne(250)) { }
    $ctx = $listener.EndGetContext($async)
    try {
      Atender $ctx
    } catch {
      Write-Host "  ! $($_.Exception.Message)" -ForegroundColor Red
      try { Error-Json $ctx 500 'Error interno del servidor.' } catch { }
    }
  }
} finally {
  $listener.Stop()
  $listener.Close()
}
