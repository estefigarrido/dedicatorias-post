# Dedicatorias post. — sitio web

Extensión de la app SPOT!: 3 pantallas (escribir → vista previa → publicada) para dejar una
dedicatoria anónima que aparece en las pantallas de la fachada del cubo post. en el Obelisco.

## Cómo usarlo

1. Doble clic en **`iniciar.bat`**. No hace falta instalar nada (usa PowerShell, que ya viene en Windows).
2. Abrí **http://localhost:8080/** en el navegador.
   - Modo tablet (para la persona de la marca en la fila): **http://localhost:8080/?modo=tablet**
     Oculta "Volver a la app", no guarda borradores y vuelve solo al inicio 20 s después de publicar.
3. Para cerrarlo: `Ctrl+C` en la ventana negra.

### Abrirlo desde celulares y la tablet (misma red Wi-Fi)

Una sola vez, ejecutá **`habilitar-red.bat`** (pide permisos de administrador: habilita el puerto 8080
en Windows y en el firewall). Después, al iniciar, la ventana muestra la dirección para el celu,
por ejemplo `http://192.168.0.15:8080/`.

## Configuración (`public/config.js`)

| Clave | Para qué |
|---|---|
| `APP_URL` | A dónde lleva "Volver a la app" y la flecha de la pantalla 1. Vacío = página anterior. |
| `API_URL` | Dónde está la API. Vacío = el mismo servidor. |
| `TABLET_RESET_SEG` | Segundos antes de volver al inicio en modo tablet. |

## API (la que va a leer Unity)

`GET /api/notas?desde=<ultimoId>` → las notas nuevas desde ese id:

```json
{
  "ok": true,
  "notas": [
    { "id": 12, "para": "nacho", "mensaje": "Gracias por esperarme…", "color": "verde",
      "hex": "#49b867", "tamano": "M", "creada": "2026-09-28T04:57:27Z" }
  ],
  "ultimoId": 12,
  "total": 12,
  "eliminadas": []
}
```

- Unity consulta cada 1–2 s pasando el último `id` que ya mostró.
- `tamano` es S / M / L / XL (tarjetita1–4 del Figma): S ≤ 40 caracteres, M ≤ 120, L ≤ 170, XL ≤ 250.
- `eliminadas` son los ids que se moderaron: Unity las saca de pantalla.

Otras rutas:

- `POST /api/notas` con `{ "para", "mensaje", "color" }`: la usa el sitio.
- `DELETE /api/notas/<id>?clave=post-admin`: ocultar una nota (moderación). La clave se cambia al iniciar:
  `iniciar.bat -ClaveAdmin otraClave`.
- `GET /api/salud`: para chequear que el servidor está vivo.

## Reglas del servidor

- Nombre: 1 a 20 caracteres. Mensaje: 3 a 250. Colores: azul, verde, violeta, rosa.
- Filtro de palabras ofensivas (lista editable en `servidor.ps1`, variable `$PROHIBIDAS`) y bloqueo de links.
- Máximo una publicación cada 10 s por dispositivo.
- Las notas se guardan en `data/notas.json`.
