# Dedicatorias post.

Extensión de la app SPOT!: 3 pantallas (escribir → vista previa → publicada) para dejar una
dedicatoria anónima que aparece en las pantallas de la fachada del cubo post. en el Obelisco.

- Sitio: carpeta [`docs/`](docs/) (publicada con GitHub Pages).
- Base de datos: Supabase ([`supabase.sql`](supabase.sql)).
- Modo tablet (para la persona de la marca en la fila): agregar `?modo=tablet` al link.
  Oculta "Volver a la app", no guarda borradores y vuelve solo al inicio 20 s después de publicar.

## Configurar la base de datos (una sola vez)

1. Crear una cuenta en <https://supabase.com> y un proyecto nuevo (plan gratis).
2. En el proyecto: **SQL Editor → New query**, pegar todo [`supabase.sql`](supabase.sql) y tocar **Run**.
3. En **Project Settings → API** copiar la **Project URL** y la clave **anon / publishable**.
4. Pegarlas en [`docs/config.js`](docs/config.js) (`SUPABASE_URL` y `SUPABASE_ANON_KEY`) y subir el cambio.

La clave anon es pública por diseño: las reglas de `supabase.sql` solo permiten **crear** notas
y **leer** las visibles. Nadie puede editar ni borrar desde afuera.

## Moderación

- **Filtro automático**: insultos, contenido sexual u ofensivo, links y teléfonos. Detecta variantes
  (acentos, mayúsculas, "puuuto", "put0", "p.u.t.o", "p u t o"). Corre en el sitio (aviso rojo en la
  pantalla) y también en la base, así que no se puede saltear.
  Lista de palabras: [`docs/moderacion.js`](docs/moderacion.js) y la función `post_moderar` de `supabase.sql`
  (mantener las dos iguales, escritas sin letras dobles: "forro" → `for[oa]s?`).
- **Sacar una nota de pantalla**: Supabase → Table Editor → `notas` → poner `visible` en `false`.

## API para Unity

```
GET {SUPABASE_URL}/rest/v1/notas?select=id,para,mensaje,color,hex,tamano,creada&id=gt.{ultimoId}&order=id.asc
Header: apikey: {SUPABASE_ANON_KEY}
```

Devuelve una lista de notas nuevas (solo las visibles):

```json
[{ "id": 12, "para": "nacho", "mensaje": "Gracias por esperarme…", "color": "verde",
   "hex": "#49b867", "tamano": "M", "creada": "2026-09-28T04:57:27+00:00" }]
```

- Consultar cada 1–2 s pasando el último `id` mostrado.
- `tamano` es S / M / L / XL (tarjetita1–4 del Figma): S ≤ 40 caracteres, M ≤ 120, L ≤ 170, XL ≤ 250.
  Ancho fijo por tamaño (619 / 956 / 1044 / 1299), alto según el texto.
- Para detectar notas moderadas: `GET …/rest/v1/notas?select=id` devuelve los ids que siguen visibles.

## Unity: el cubo con las 4 pantallas (`unity/CuboPost`)

Proyecto de Unity 6 (6000.3.6f1, URP). Abrir la carpeta `unity/CuboPost` con Unity Hub y la escena
`Assets/Scenes/CuboPost.unity`, y tocar ▶.

- **Cubo a escala real**: planta 13,5 × 9,5 m, 3 m de alto (2,8 m de pantalla LED desde los 20 cm),
  túnel de entrada de 1,8 m al frente, Plaza de la República con el Obelisco de fondo y gente de 1,70 m como escala.
- **Pantallas**: fondo negro con luces leves verde y lila que recorren las 4 paredes sin cortes, puntitos
  (paleta sprout: lima, aqua, violeta) y los anillos, puntos y estrellas de post.
- **Pared de atrás**: composición post. del Figma, centrada, con cada elemento flotando lento y a destiempo.
  Las notas van a los costados: la elipse central (43 % del ancho) está bloqueada.
- **Dedicatorias**: se leen de Supabase cada 2 s. Las nuevas aparecen grandes y se acomodan; las moderadas
  (`visible = false`) desaparecen solas. Mientras haya pocas reales, se completan con notas de ejemplo.
- **Cámara**: arrastrar para girar · rueda para acercar · 1-4 cada pared a la altura de la fila · 0 vista general · R giro automático.
- **Reconstruir la escena** (si se cambian medidas en el código): menú **post. → Construir escena del cubo**.

Código en `Assets/CuboPost/Scripts` (notas, pantallas, Supabase, cámara), el shader del fondo en
`Assets/CuboPost/Shaders` y el constructor de la escena en `Assets/CuboPost/Editor/ConstruirCubo.cs`.

## Probar en la compu sin Supabase (opcional)

Con `SUPABASE_URL` vacío, el sitio usa el servidor local: doble clic en `iniciar.bat` y abrir
<http://localhost:8080/>. Las notas quedan en `data/notas.json` y la API es
`GET http://localhost:8080/api/notas?desde=<ultimoId>`. Para abrirlo desde celulares en la misma
red, ejecutar una vez `habilitar-red.bat` como administrador.
