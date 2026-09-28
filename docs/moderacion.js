// Filtro de moderación de las dedicatorias.
// Detecta insultos, contenido sexual u ofensivo, links y números de teléfono,
// aunque estén escritos con acentos, mayúsculas, letras repetidas ("puuuto"),
// números en lugar de letras ("put0") o separados ("p.u.t.o", "p u t o").
(() => {
  'use strict';

  // Formas "compactas": minúsculas, sin acentos y sin letras dobles (ej. "asshole" → "ashole").
  // Cada entrada es una expresión regular que tiene que coincidir con palabras enteras.
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
  const RE_PROHIBIDAS = new RegExp('(^|[^a-z])(' + PROHIBIDAS.join('|') + ')(?=$|[^a-z])');
  const RE_LINK = /(https?:\/\/|www\.|[a-z0-9-]+\.(com|ar|net|org|io|me|ly|gg|tv|app)\b)/i;
  const RE_TELEFONO = /\d[\d\s.-]{7,}\d/;

  function normalizar(texto) {
    let t = String(texto || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
    t = t.replace(/0/g, 'o').replace(/1/g, 'i').replace(/3/g, 'e').replace(/4/g, 'a')
      .replace(/5/g, 's').replace(/7/g, 't').replace(/@/g, 'a').replace(/\$/g, 's');
    t = t.replace(/([a-z])[.\-_*·,'"]+(?=[a-z])/g, '$1');            // p.u.t.o → puto
    t = t.replace(/\b(?:[a-z] ){2,}[a-z]\b/g, (m) => m.replace(/ /g, '')); // p u t o → puto
    t = t.replace(/([a-z])\1+/g, '$1');                                // puuuto → puto
    return t;
  }

  // Devuelve null si está todo bien, o { campo, motivo, mensaje } si hay que bloquear.
  function revisar({ para, mensaje }) {
    const campos = [['para', para], ['mensaje', mensaje]];
    for (const [campo, valor] of campos) {
      const crudo = String(valor || '');
      if (RE_PROHIBIDAS.test(normalizar(crudo))) {
        return { campo, motivo: 'ofensivo', mensaje: 'Tiene insultos o palabras inapropiadas.' };
      }
      if (RE_LINK.test(crudo)) {
        return { campo, motivo: 'link', mensaje: 'No se pueden incluir links.' };
      }
      if (RE_TELEFONO.test(crudo)) {
        return { campo, motivo: 'datos', mensaje: 'No se pueden incluir números de teléfono.' };
      }
    }
    return null;
  }

  window.PostModeracion = { revisar, normalizar };
})();
