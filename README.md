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

- **Filtro automático**: insultos, contenido sexual u ofensivo, links y teléfonos. Ante un insulto el
  aviso rojo dice "Ese tipo de insultos o vocabulario no está permitido."
  - Palabras disfrazadas: acentos, mayúsculas, "puuuto", números o símbolos en lugar de letras ("p3lotudo",
    "put@", "p!ja"), letras de otros alfabetos que se ven iguales, separadas ("p.u.t.o", "p u t o", "pelo tudo")
    o escritas como suenan ("kulo", "berga", "zorete", "conxa").
  - Por contexto: palabras que solas no insultan pero sí dirigidas a alguien ("sos un inútil", "qué basura que
    sos"), frases que agreden ("ojalá te mueras", "nadie te quiere", "me das asco") y sarcasmo ("gracias por
    nada"). Los elogios del deporte ("sos un animal", "sos una bestia", "rata de gimnasio") pasan.
  - Corre en el sitio y también en la base (función `post_ofensivo`), así que no se puede saltear.
    Listas: [`docs/moderacion.js`](docs/moderacion.js) y `post_ofensivo` en `supabase.sql` (mantener las dos
    iguales, escritas sin acentos ni letras dobles: "forro" → `for[oa]s?`).
- **Comienzos de dedicatoria**: 4 frases para arrancar el mensaje (opcionales). Se cambian en `docs/index.html`
  (botones `.comienzo`).
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
- En Unity cada nota aparece en 1 s (opacidad 0 → 100 %), queda 15 s y desaparece en 1 s. Las nuevas salen apenas se publican; mientras tanto rotan las ya enviadas. La más chica mide 84 cm de ancho (eran 70 cm: con las pantallas más altas todas crecieron un 20 %).
  Ancho fijo por tamaño (619 / 956 / 1044 / 1299), alto según el texto.
- Para detectar notas moderadas: `GET …/rest/v1/notas?select=id` devuelve los ids que siguen visibles.

## Unity: el cubo con las 4 pantallas (`unity/CuboPost`)

Proyecto de Unity 6 (6000.3.6f1, URP). Abrir la carpeta `unity/CuboPost` con Unity Hub y la escena
`Assets/Scenes/CuboPost.unity`, y tocar ▶.

- **Cubo a escala real**: planta 20 × 10 m, 4 m de alto (3,3 m de pantalla LED desde los 20 cm, hasta los 3,50 m,
  más 0,50 m de coronamiento), entrada de 3 × 2,8 m centrada en el lateral izquierdo, Plaza de la República con
  el Obelisco de fondo y personas 3D (Meshy, `Assets/CuboPost/Modelos`) haciendo la fila y sentadas en los pufs. Las medidas están al principio de
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
  Las nuevas salen apenas se publican; mientras tanto rotan las ya enviadas (o, si no hay ninguna, las cuatro
  tarjetitas del Figma: nacho, cami, delfi y Agustin, en `ControladorCubo.cs`). Las moderadas (`visible = false`) desaparecen solas.
- **Ejercicio de respiración** (para la fila): en la pantalla del lado este, a la derecha de la puerta de entrada
  (la carita mide Ø 1,47 m y los anillos hasta Ø 3,58 m; los puntos no cruzan esa zona). Aparece solo cada
  5 minutos o con la tecla **R** (con R de nuevo se corta): entra creciendo, hace 2 respiraciones (5 s de inhalar y
  5 s de exhalar) y se va achicándose. Al inhalar aparece un anillo más grande detrás de la carita cada segundo y al
  exhalar se va uno por segundo. La cuenta ("inhalá 3", "exhalá 2") va arriba de los anillos sin tocarlos, en el
  estilo de los títulos de post.: Sora Bold en minúscula, violeta #AB8AE5, con una onda subrayando (los puntos de la
  pantalla no pasan por ahí). Código: `Scripts/EjercicioRespiracion.cs`, caritas en `Resources/Respiracion`.
- **Pantalla de espera** (tecla **E**): entre las dos puertas del lado este (4,4 × 4,1 m). "La siguiente clase comienza
  en: N min" (las clases arrancan cada 20 minutos del reloj), con anillos verdes que se abren desde el centro y un
  puntito que gira. Entra creciendo, queda 30 s y se va achicándose. Código: `Scripts/PantallaEspera.cs`.
- **Videos en las pantallas largas** (frente y fondo), cada 7 minutos o con la tecla **M**: un barrido verde entra de
  izquierda a derecha en 2 s y se pasan seguidos `gente-corriendo.mp4`, `tomemos-un-break.mp4`, el mismo en reversa
  (`tomemos-un-break-reversa.mp4`, armado a partir del original) y `cierre-de-circulo.mp4`. En el cierre el negro puro
  es transparente (shader `Resources/VideoSinNegro.shader`): el círculo se cierra sobre la pantalla de post. de siempre.
  Los videos llenan la pantalla (recortan apenas arriba y abajo). Código: `Scripts/VideoPantallas.cs`.
- **Animación de la insignia** en la pantalla oeste (la lateral sin puertas), a los 2:30 y después cada 5 minutos, o
  con la tecla **I**: aparece y se va directo, sin barrido (`StreamingAssets/animacion-insignia.mp4`). Los dos videos no se pisan nunca: si a uno le toca mientras el otro está
  en pantalla, espera a que termine y deja 20 s de separación.
- **Velas de sombra** (plano "01 · Implantación" del Figma): 8 velas circulares (Ø 6,40 y 4,14 m) a 5,10–5,40 m,
  tela PES verde y violeta de la marca, semitraslúcida, en aros de aluminio unidos arriba. Los 4 postes de carga
  hacen de soporte; los demás palos van sobre el pasto, con asientos redondos tapizados. Se arman solas al
  recompilar o con **post. → Construir velas de sombra** (`Editor/ConstruirVelas.cs`).
- **Momentos de la clase** (adentro de la cabina, `Scripts/ControlClase.cs`, objeto "Control de la clase (V · N)"):
  - **Sin comando**: las pantallas de la sala están en negro con los vectores flotando y las dicroicas apagadas.
  - **V · calibración**: dicroicas en blanco. Las 8 pantallas de cada pared muestran "Extendé los brazos…" 2 s y
    después "¡Calibración lista!" hasta que se toca otro comando.
  - **N · en pareja**: dicroicas en verde y las personas de la zona de stretching paradas con los brazos al
    costado. A los 2 s las pantallas pasan `StreamingAssets/en-pareja.mp4` y las dicroicas van de verde a
    violeta en 1 s. Al terminar el video, vuelve a reposo.
  - **B · estirando hombro**: dicroicas en verde y, en cada una de las 8 pantallas de cada pared, el video de Lucas
    (`StreamingAssets/estirando-hombro.mp4`) del segundo 9 al 14, en loop hasta que se toca otro comando.
    Las personas 3D no cambian.
  - La misma tecla otra vez vuelve a reposo.
  - Las personas en pose T para la V todavía no están (las sube Mateo). Cuando estén, se arrastra el grupo al
    campo `personasEnT`; mientras tanto quedan las de siempre.
- **Música de la clase** (tecla **C**, `Scripts/MusicaClase.cs`): 20 minutos de música en loop
  (`Audio/Musica de la clase.ogg`, se lee del disco). Entra con un fade in de 5 s. Con la C otra vez se baja
  en 1,5 s y se corta. Al caminar, ver al personaje desde atrás pasó a la tecla **X**.
- **Dicroicas** (`Scripts/LucesDicroicas.cs`): 16 en la sala y 4 en los pasillos. La tecla **L** las pasa a mano por
  blanco, verde y violeta.
- **Puertas** (`Scripts/PuertaInteractiva.cs`): se apuntan con el mouse y se abren o cierran con la **P**. Si el cursor
  está tomado al caminar, vale la del centro de la vista, a menos de 4 m. La puerta apuntada se ilumina apenas.
- **Cámara**: arrastrar para girar · rueda para acercar · 1-4 cada pared a la altura de la fila · 0 vista general · G giro automático.
- **Reconstruir la escena** (si se cambian medidas en el código): menú **post. → Construir escena del cubo**.

Código en `Assets/CuboPost/Scripts` (notas, pantallas, Supabase, cámara), el shader del fondo en
`Assets/CuboPost/Shaders` y el constructor de la escena en `Assets/CuboPost/Editor/ConstruirCubo.cs`.

## Probar en la compu sin Supabase (opcional)

Con `SUPABASE_URL` vacío, el sitio usa el servidor local: doble clic en `iniciar.bat` y abrir
<http://localhost:8080/>. Las notas quedan en `data/notas.json` y la API es
`GET http://localhost:8080/api/notas?desde=<ultimoId>`. Para abrirlo desde celulares en la misma
red, ejecutar una vez `habilitar-red.bat` como administrador.
