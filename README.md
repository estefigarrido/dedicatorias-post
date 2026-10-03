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
- `tamano` es S / M / L (tarjetita1–3 del Figma): S ≤ 40 caracteres, M ≤ 120, L hasta el máximo de 150. XL solo aparece en notas viejas de más de 170.
- `color` puede ser azul, verde, violeta, rosa o crema (#ede8db).
- En Unity cada nota aparece en 1 s (opacidad 0 → 100 %), queda 15 s y desaparece en 1 s. Las nuevas salen apenas se publican; mientras tanto rotan las ya enviadas. La más chica mide 70 cm de ancho.
  Ancho fijo por tamaño (619 / 956 / 1044 / 1299), alto según el texto.
- Para detectar notas moderadas: `GET …/rest/v1/notas?select=id` devuelve los ids que siguen visibles.

## Unity: el cubo con las 4 pantallas (`unity/CuboPost`)

Proyecto de Unity 6 (6000.3.6f1, URP). Abrir la carpeta `unity/CuboPost` con Unity Hub y la escena
`Assets/Scenes/CuboPost.unity`, y tocar ▶.

- **Cubo a escala real**: planta 20 × 10 m, 4 m de alto (3,3 m de pantalla LED desde los 20 cm, hasta los 3,50 m,
  más 0,50 m de coronamiento), entrada de 3 × 2,8 m centrada en el lateral izquierdo, Plaza de la República con
  el Obelisco de fondo y gente de 1,70 m como escala. Las medidas están al principio de
  `Assets/CuboPost/Editor/ConstruirCubo.cs`: al cambiarlas, Unity actualiza el cubo de la escena solo
  (o con el menú **post. → Actualizar solo el cubo**), sin tocar la plaza.
- **Pantallas**: fondo negro de la marca (#252525) con luces leves verde y lila que recorren las 4 paredes sin
  cortes, y los puntos del componente "Punto" del Figma (marca POST) dispersos: círculos S, M y L en sus 5
  variantes y las estrellas de 12 y 7 puntas, al tamaño del Figma. Cantidad: `puntosPorMetro` en cada pantalla.
- **Paredes largas (frente y fondo)**: gráfica post. del Figma (logo con sus puntos y estrellas), centrada, con
  cada elemento flotando lento y a destiempo. Las notas y los puntos sueltos van a los costados: la elipse
  central (la zona roja del Figma, 6,66 m de ancho por todo el alto) está bloqueada para que nada tape el logo.
- **Sin apretar ▶** las pantallas muestran una vista previa quieta (gráfica y puntos, sin notas). Esa vista no
  se guarda en la escena: se rearma sola al abrirla.
- **Dedicatorias**: por ahora salen solo en la pantalla del frente (`recibeNotas` en cada pantalla; se define
  en `ConstruirCubo.cs`). Se leen de Supabase cada 2 s. Cada una aparece en 1 s, queda 15 s y se va en 1 s.
  Las nuevas salen apenas se publican; mientras tanto rotan las ya enviadas (o las de ejemplo si no hay
  ninguna). Las moderadas (`visible = false`) desaparecen solas.
- **Cámara**: arrastrar para girar · rueda para acercar · 1-4 cada pared a la altura de la fila · 0 vista general · R giro automático.
- **Reconstruir la escena** (si se cambian medidas en el código): menú **post. → Construir escena del cubo**.

Código en `Assets/CuboPost/Scripts` (notas, pantallas, Supabase, cámara), el shader del fondo en
`Assets/CuboPost/Shaders` y el constructor de la escena en `Assets/CuboPost/Editor/ConstruirCubo.cs`.

## Probar en la compu sin Supabase (opcional)

Con `SUPABASE_URL` vacío, el sitio usa el servidor local: doble clic en `iniciar.bat` y abrir
<http://localhost:8080/>. Las notas quedan en `data/notas.json` y la API es
`GET http://localhost:8080/api/notas?desde=<ultimoId>`. Para abrirlo desde celulares en la misma
red, ejecutar una vez `habilitar-red.bat` como administrador.
