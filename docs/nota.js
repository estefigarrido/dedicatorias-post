// Piezas compartidas entre el formulario (index.html) y el monitor (monitor.html):
// la notita con borde festoneado, los talles y los objetos flotantes de post.
(() => {
  'use strict';

  const COLORES = { azul: '#7aa1ff', verde: '#49b867', violeta: '#ab8ae6', rosa: '#f79ee1', crema: '#ede8db' };

  // Ancho de tarjetita1..4 del Figma (unidades de diseño) y su alto con el texto de ejemplo.
  // El alto real es HUG: crece con el mensaje.
  const DIM = { S: [619, 715], M: [956, 869], L: [1044, 946], XL: [1299, 1023] };
  const TALLES = [
    { t: 'S', min: 0, max: 40 },
    { t: 'M', min: 41, max: 120 },
    { t: 'L', min: 121, max: 170 },
    { t: 'XL', min: 171, max: 250 },
  ];

  const limpiar = (s) => String(s || '').replace(/\s+/g, ' ').trim();
  const talle = (largo) => (TALLES.find((x) => largo <= x.max) || TALLES[TALLES.length - 1]).t;

  function crear() {
    const wrap = document.createElement('div');
    wrap.className = 'nota-wrap';
    const nota = document.createElement('div');
    nota.className = 'nota';
    const para = document.createElement('div');
    para.className = 'nota__para';
    const pref = document.createElement('span');
    pref.textContent = 'para: ';
    const nombre = document.createElement('span');
    nombre.className = 'nota__nombre';
    para.append(pref, nombre);
    const msg = document.createElement('p');
    msg.className = 'nota__msg';
    nota.append(para, msg);
    wrap.append(nota);
    return wrap;
  }

  // datos: { para, mensaje, color } — devuelve el talle aplicado.
  function pintar(wrap, datos, { placeholder = false } = {}) {
    const nota = wrap.firstElementChild;
    const para = limpiar(datos.para);
    const msg = limpiar(datos.mensaje);
    const t = datos.tamano || talle(msg.length);
    nota.dataset.talle = t;
    nota.style.setProperty('--nota-bg', rgba(COLORES[datos.color] || COLORES.verde, 0.8));
    nota.classList.toggle('sin-nombre', placeholder && !para);
    nota.classList.toggle('vacia', placeholder && !msg);
    nota.querySelector('.nota__nombre').textContent = para || (placeholder ? '···' : '');
    nota.querySelector('.nota__msg').textContent = msg || (placeholder ? 'Tu mensaje va a aparecer acá.' : '');
    return t;
  }

  const rgba = (hex, a) =>
    `rgba(${parseInt(hex.slice(1, 3), 16)}, ${parseInt(hex.slice(3, 5), 16)}, ${parseInt(hex.slice(5, 7), 16)}, ${a})`;

  // Ajusta --u (px por unidad de diseño) para que la nota entre en maxW x maxH.
  // El ancho sale del tamaño; el alto es HUG (depende del texto), así que se mide.
  function escalar(wrap, t, maxW, maxH, maxU) {
    let u = Math.max(0.05, Math.min(maxW / DIM[t][0], maxU));
    wrap.style.setProperty('--u', u.toFixed(4) + 'px');
    const alto = wrap.firstElementChild.offsetHeight;
    if (alto > maxH) {
      u *= maxH / alto;
      wrap.style.setProperty('--u', u.toFixed(4) + 'px');
    }
    return u;
  }

  // Estrella "flor" como las de la pieza post.
  function estrellaPath(puntas = 9, radio = 45, amp = 4) {
    let d = '';
    for (let i = 0; i <= 180; i++) {
      const a = (i / 180) * Math.PI * 2;
      const r = radio + amp * Math.cos(puntas * a);
      d += (i ? 'L' : 'M') + (50 + r * Math.cos(a)).toFixed(2) + ' ' + (50 + r * Math.sin(a)).toFixed(2);
    }
    return d + 'Z';
  }

  // items: [{ t: 'ring'|'dot'|'star'|'star-dark', x, y, s, d?, fx?, fy?, fr? }]
  function decorar(cont, items) {
    cont.textContent = '';
    items.forEach((it, i) => {
      const el = document.createElement('span');
      el.className = 'deco__item deco__' + it.t;
      el.style.setProperty('--x', it.x);
      el.style.setProperty('--y', it.y);
      el.style.setProperty('--s', it.s + 'px');
      el.style.setProperty('--d', (it.d || 5 + (i % 4) * 1.3) + 's');
      el.style.setProperty('--dl', (-(i * 0.9)).toFixed(1) + 's');
      el.style.setProperty('--fx', (it.fx ?? (i % 2 ? 5 : -5)) + 'px');
      el.style.setProperty('--fy', (it.fy ?? -10) + 'px');
      el.style.setProperty('--fr', (it.fr ?? (i % 2 ? 14 : -10)) + 'deg');
      if (it.t === 'star' || it.t === 'star-dark') {
        el.innerHTML =
          '<svg viewBox="0 0 100 100" aria-hidden="true"><path d="' + estrellaPath(it.t === 'star' ? 9 : 11) +
          '" vector-effect="non-scaling-stroke"/></svg>';
      }
      cont.append(el);
    });
  }

  window.PostNota = { COLORES, DIM, TALLES, limpiar, talle, crear, pintar, escalar, decorar };
})();
