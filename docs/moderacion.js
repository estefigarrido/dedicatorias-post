// Filtro de moderación de las dedicatorias. Trabaja en dos capas, como un clasificador:
//  1. Palabras prohibidas (insultos, discriminación, contenido sexual, drogas) aunque vengan
//     disfrazadas: acentos y mayúsculas, letras repetidas ("puuuto"), números o símbolos en lugar
//     de letras ("p3lotudo", "put@", "b0lud0", "p!ja"), letras de otros alfabetos que se ven
//     iguales, separadas ("p.u.t.o", "p u t o", "pelo tudo") o escritas como suenan ("kulo",
//     "berga", "zorete", "conxa").
//  2. Contexto: palabras que solas no son un insulto ("inútil", "basura", "rata") pero sí cuando
//     van dirigidas a alguien ("sos un inútil", "qué basura que sos", un nombre que es solo
//     "fracasado"), frases que agreden o le desean un mal a alguien ("ojalá te mueras", "nadie te
//     quiere", "me das asco") y el sarcasmo ("gracias por nada"). Los elogios del deporte que
//     suenan fuerte ("sos un animal", "sos una bestia", "rata de gimnasio") pasan.
// La base (post_moderar en supabase.sql) aplica el mismo criterio: no se puede saltear.
(() => {
  'use strict';

  const AVISO = 'Ese tipo de insultos o vocabulario no está permitido.';

  // Palabras enteras. Se escriben normal (sin acentos y sin letras dobles: "forro" → "for[oa]s?");
  // al cargar pasan por el mismo plegado que el texto ("verga" → "berga").
  const PROHIBIDAS = [
    // insultos
    'put[oa]s?', 'putit[oa]s?', 'putaz[oa]s?', 'hij[oa]s? de put[oa]', 'hdp', 'hdep', 'lpm', 'lptm',
    'pelotud[oa]s?', 'bolud[oa]s?', 'for[oa]s?', 'conchud[oa]s?', 'la concha', 'concha de (tu|su)',
    'cul[ie]ad[oa]s?', 'mierdas?', 'sorete?s?', 'cagon(a|es)?', 'garca', 'garcas',
    'idiotas?', 'imbecil(es)?', 'estupid[oa]s?', 'tarad[oa]s?', 'mogolic[oa]s?', 'retrasad[oa]s?',
    'retardad[oa]s?', 'pajer[oa]s?', 'chupala', 'chupame', 'chupa ?pija', 'mamaguev[oa]', 'malparid[oa]s?',
    'gonorea', 'cabron(a|es)?', 'pendej[oa]s?', 'zora', 'zoras', 'pera de mierda',
    // discriminación
    'trol[oa]s?', 'marica', 'maricon(es)?', 'tortiler[oa]s?', 'negr[oa]s? de mierda', 'sudacas?',
    'vilero?s?', 'vilera?s?', 'nazi', 'nazis', 'hitler',
    // sexual
    'pija', 'pijas', 'pijud[oa]', 'poronga', 'porongas', 'verga', 'vergas', 'garcha', 'garchar',
    'coger', 'cogerte', 'cogida', 'pete', 'peter[oa]', 'orto', 'ojete', 'culo', 'culos', 'tetas?',
    'pene', 'vagina', 'porno', 'sexo', 'prostitut[oa]s?', 'chot[oa]', 'paja',
    // drogas
    'falopa', 'merca', 'faso',
    // inglés
    'fuck', 'fucking', 'fucker', 'motherfucker', 'shit', 'bitch', 'bitches', 'ashole', 'dick',
    'cunt', 'pusy', 'niger', 'niga', 'whore', 'slut', 'bastard',
  ];
  // Las largas también se buscan con los espacios sacados ("pelo tudo", "im becil").
  const PROHIBIDAS_JUNTAS = ['pelotud', 'conchud', 'imbecil', 'mogolic', 'retrasad', 'retardad',
    'malparid', 'prostitut', 'motherfucker', 'hijodeput'];

  // Despectivas: insultan cuando se las dicen a alguien.
  const DESPECTIVAS = [
    'inutil(es)?', 'basuras?', 'lacras?', 'ratas?', 'escoria', 'cerd[oa]s?', 'asqueros[oa]s?',
    'fracasad[oa]s?', 'perdedor(a|es)?', 'mediocres?', 'patetic[oa]s?', 'ridicul[oa]s?', 'payas[oa]s?',
    'tont[oa]s?', 'bob[oa]s?', 'gil(es)?', 'salames?', 'nab[oa]s?', 'chantas?', 'mentiros[oa]s?',
    'traidor(a|es)?', 'cobardes?', 'vag[oa]s?', 'fe[oa]s?', 'horribles?', 'despreciables?', 'miserables?',
    'infeliz', 'infelices', 'ignorantes?', 'egoistas?', 'envidios[oa]s?', 'resentid[oa]s?', 'chor[oa]s?',
    'ladron(a|es)?', 'falsa?o?s?', 'loser', 'stupid', 'idiot', 'dumb', 'ugly',
  ];
  // Lo que puede ir entre el verbo y la palabra: "sos un re inútil", "sos una tremenda basura".
  const RELLENO = '(?:(?:un|una|unos|unas|el|la|los|las|re|muy|tan|mas|alto|alta|flor de|tremend[oa]|'
    + 'terrible|medio|media|bastante|bien|tal|un pedazo de|siempre|solo|nada mas que|tremenda|gran|menudo)\\s+){0,3}';
  const DIRIGIDAS = [
    // A vos: "sos un inútil", "seguís siendo una fracasada", "te ves horrible"
    `(sos|seas|eres|fuiste|seguis siendo|siempre fuiste|pareces|quedaste como|te ves)\\s+${RELLENO}(DESP)`,
    // A otro, con artículo ("es un inútil", "son unos chantas"); sin artículo puede hablar de una
    // cosa ("la subida es horrible") y no se bloquea.
    `(es|son|sea|sean|fue|era|eran)\\s+(?:(?:re|muy|tan|mas|alto|alta|tremend[oa]|terrible|flor de)\\s+)?(?:un|una|unos|unas)\\s+${RELLENO}(DESP)`,
    // "qué basura que sos", "qué inútil sos"
    `que\\s+(?:(?:re|tan|mas|gran)\\s+)?(DESP)\\s+(?:que\\s+)?(sos|eres)`,
    // "ey, inútil" / "para la basura"
    `^(?:(?:che|ey|eh|vos|para|a la|al|el|la|sos|un|una|re|muy|tal)\\s+){0,3}(DESP)(?:\\s+(?:total|absolut[oa]|de persona|humana?o?))?$`,
  ];
  // Frases que agreden, amenazan o le desean un mal a alguien, aunque no tengan malas palabras.
  const AGRESIONES = [
    'ojala (que )?(te|se) (mueras?|muera|mueran|pudras?|pudra|lesiones|caigas|pise un auto)',
    'ojala (que )?(pierdas|nunca llegues|abandones)',
    'te (voy|vamos) a (matar|cagar a palos|romper la cara|fajar)',
    'and(a|ate) a (morir|cagar)', 'morite', 'muerete', 'matate', 'suicidate', 'pegate un tiro',
    'desaparece de mi vida', 'deja de existir', 'no deberias existir', 'ni deberias existir',
    'nadie te (quiere|soporta|banca|aguanta|extrana|necesita)', 'nadie los (quiere|soporta|banca)',
    'no (servis|sirves|vales|sabes) (para|ni) nada', 'no (servis|sirves|vales) nada', 'no sabes hacer nada',
    '(me |nos )?(das|dabas) (asco|pena|verguenza|lastima|bronca|vomito)',
    'te (odio|detesto|desprecio|aborrezco)', '(los|las) (odio|detesto|desprecio)',
    'sos lo peor', 'lo peor que me paso', 'me arruinaste la vida', 'arruinaste mi vida',
    'gracias por nada', 'te deseo lo peor', 'les deseo lo peor', 'ojala te vaya mal',
    'que te (pise|parta|coja|atropelle)', 'cerra el pico', 'nunca te voy a perdonar',
  ];
  // Elogios del deporte que suenan fuerte: se sacan antes de buscar despectivas.
  const ELOGIOS = '(ratas? de (gimnasio|gym|biblioteca|pista)|rata de calle)';

  // Letras de otros alfabetos que se ven iguales (а е о р с у х і ѕ ο α ρ τ υ ν κ ι ε).
  const PARECIDAS = { 'а': 'a', 'е': 'e', 'о': 'o', 'р': 'p', 'с': 'c', 'у': 'y', 'х': 'x', 'і': 'i',
    'ј': 'j', 'ѕ': 's', 'ԁ': 'd', 'ο': 'o', 'α': 'a', 'ρ': 'p', 'τ': 't', 'υ': 'u', 'ν': 'v', 'κ': 'k',
    'ι': 'i', 'ε': 'e', 'ɡ': 'g', 'ß': 'b' };
  const NUMEROS = { 0: 'o', 1: 'i', 3: 'e', 4: 'a', 5: 's', 6: 'g', 7: 't', 8: 'b', 9: 'g' };

  // Cómo suena: k/q → c, v → b, z → s, y → i, x → ch. Se aplica igual al texto y a las listas.
  const plegar = (t) => t.replace(/[kq]/g, 'c').replace(/v/g, 'b').replace(/z/g, 's')
    .replace(/y/g, 'i').replace(/x/g, 'ch').replace(/([a-z])\1+/g, '$1');

  function normalizar(texto) {
    let t = String(texto || '').normalize('NFKC').toLowerCase()
      .replace(/[​-‏⁠﻿­]/g, '')                     // caracteres invisibles
      .replace(/[^\x00-\x7f]/g, (c) => PARECIDAS[c] || c)
      .normalize('NFD').replace(/[̀-ͯ]/g, '')                       // acentos
      .replace(/[0-9]/g, (d) => NUMEROS[d] || d)
      .replace(/[@ª]/g, 'a').replace(/[$§]/g, 's').replace(/€/g, 'e')
      .replace(/([a-z])[!|¡](?=[a-z])/g, '$1i').replace(/([a-z])\+(?=[a-z])/g, '$1t');
    t = t.replace(/([a-z])[^a-z0-9\s]+(?=[a-z])/g, '$1');                     // p.u.t.o, p*u*t*o → puto
    t = t.replace(/\b(?:[a-z] ){2,}[a-z]\b/g, (m) => m.replace(/ /g, ''));   // p u t o → puto
    t = t.replace(/([a-z])\1+/g, '$1');                                       // puuuto → puto
    return plegar(t);
  }

  const lista = (palabras) => palabras.map(plegar).join('|');
  const RE_PROHIBIDAS = new RegExp(`(^|[^a-z])(${lista(PROHIBIDAS)})(?=$|[^a-z])`);
  const RE_JUNTAS = new RegExp(lista(PROHIBIDAS_JUNTAS));
  const DESP = lista(DESPECTIVAS);
  const RE_DIRIGIDAS = DIRIGIDAS.map((p) => new RegExp(`(^|[^a-z])${plegar(p).replace('(DESP)', `(${DESP})`)}(?=$|[^a-z])`));
  const RE_AGRESIONES = new RegExp(`(^|[^a-z])(${lista(AGRESIONES)})(?=$|[^a-z])`);
  const RE_ELOGIOS = new RegExp(`(^|[^a-z])${plegar(ELOGIOS)}(?=$|[^a-z])`, 'g');
  const RE_LINK = /(https?:\/\/|www\.|[a-z0-9-]+\.(com|ar|net|org|io|me|ly|gg|tv|app)\b)/i;
  const RE_TELEFONO = /\d[\d\s.-]{7,}\d/;

  // Por qué se bloquea (o null): 'palabra', 'palabra disfrazada', 'dirigida' o 'agresion'.
  function analizar(texto) {
    const t = normalizar(texto);
    // Solo letras y espacios, para leer las frases sin la puntuación.
    const frase = t.replace(/[^a-z]+/g, ' ').trim();
    if (RE_PROHIBIDAS.test(t) || RE_PROHIBIDAS.test(frase)) return 'palabra';
    if (RE_JUNTAS.test(frase.replace(/ /g, ''))) return 'palabra disfrazada';
    const sinElogios = frase.replace(RE_ELOGIOS, ' ').replace(/ +/g, ' ').trim();
    if (RE_DIRIGIDAS.some((re) => re.test(sinElogios))) return 'dirigida';
    if (RE_AGRESIONES.test(frase)) return 'agresion';
    return null;
  }

  // Devuelve null si está todo bien, o { campo, motivo, mensaje } si hay que bloquear.
  function revisar({ para, mensaje }) {
    const campos = [['para', para], ['mensaje', mensaje]];
    for (const [campo, valor] of campos) {
      const crudo = String(valor || '');
      const regla = analizar(crudo);
      if (regla) return { campo, motivo: 'ofensivo', regla, mensaje: AVISO };
      if (RE_LINK.test(crudo)) {
        return { campo, motivo: 'link', mensaje: 'No se pueden incluir links.' };
      }
      if (RE_TELEFONO.test(crudo)) {
        return { campo, motivo: 'datos', mensaje: 'No se pueden incluir números de teléfono.' };
      }
    }
    return null;
  }

  window.PostModeracion = { revisar, normalizar, analizar, AVISO };
})();
