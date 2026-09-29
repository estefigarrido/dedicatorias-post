// Dedicatorias post. — flujo de 3 pantallas: escribir → vista previa → publicada.
(() => {
  'use strict';

  const CFG = Object.assign({ API_URL: '', APP_URL: '', TABLET_RESET_SEG: 20 }, window.POST_CONFIG || {});
  const N = window.PostNota;
  const params = new URLSearchParams(location.search);
  const MODO_TABLET = params.get('modo') === 'tablet';
  const REDUCIDO = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const MAX_PARA = 20;
  const MAX_MSG = 150;
  const MIN_MSG = 3;
  const CLAVE_BORRADOR = 'post-dedicatoria-borrador';

  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const esperar = (ms) => new Promise((r) => setTimeout(r, ms));
  const api = (ruta) => CFG.API_URL.replace(/\/$/, '') + ruta;

  const pantallas = { escribir: $('#p-escribir'), previa: $('#p-previa'), exito: $('#p-exito') };
  const inPara = $('#in-para');
  const inMsg = $('#in-msg');
  const form = $('#form');
  const btnContinuar = $('#btn-continuar');
  const btnPublicar = $('#btn-publicar');
  const dlgSalir = $('#dlg-salir');

  const estado = { para: '', mensaje: '', color: 'verde' };
  let actual = 'escribir';
  let transicionando = false;
  let transicion = Promise.resolve();
  let enviando = false;
  let intentoContinuar = false;
  let ultimoTalle = null;
  let publicada = null;
  let temporizadorTablet = null;

  // ---------- notas ----------
  const notaPreview = N.crear();
  const notaPrevia = N.crear();
  const notaExito = N.crear();
  $('#slot-preview').append(notaPreview);
  $('#slot-previa').append(notaPrevia);
  $('#slot-exito').append(notaExito);

  N.decorar($('#deco-preview'), [
    { t: 'ring', x: '9%', y: '26%', s: 14 },
    { t: 'dot', x: '88%', y: '13%', s: 12 },
    { t: 'star', x: '79%', y: '32%', s: 34 },
    { t: 'ring', x: '86%', y: '56%', s: 22 },
    { t: 'dot', x: '12%', y: '64%', s: 16 },
  ]);
  N.decorar($('#deco-previa'), [
    { t: 'ring', x: '0%', y: '4%', s: 16 },
    { t: 'ring', x: '46%', y: '-3%', s: 10 },
    { t: 'ring', x: '68%', y: '-6%', s: 24 },
    { t: 'dot', x: '93%', y: '2%', s: 20 },
    { t: 'star', x: '91%', y: '40%', s: 36 },
    { t: 'star-dark', x: '-3%', y: '66%', s: 42 },
    { t: 'dot', x: '18%', y: '96%', s: 14 },
    { t: 'ring', x: '90%', y: '92%', s: 24 },
  ]);
  N.decorar($('#deco-exito'), [
    { t: 'ring', x: '9%', y: '4%', s: 18 },
    { t: 'ring', x: '74%', y: '-4%', s: 12 },
    { t: 'dot', x: '83%', y: '3%', s: 26 },
    { t: 'star-dark', x: '4%', y: '30%', s: 52 },
    { t: 'star', x: '85%', y: '46%', s: 44 },
    { t: 'dot', x: '13%', y: '74%', s: 14 },
    { t: 'ring', x: '82%', y: '78%', s: 30 },
    { t: 'dot', x: '6%', y: '95%', s: 18 },
    { t: 'ring', x: '90%', y: '100%', s: 10 },
  ]);

  const anchoDisponible = () => Math.min(window.innerWidth, 440) - 40;

  function prepararNota(wrap, datos, { maxW, maxH, maxU }) {
    const t = N.pintar(wrap, datos);
    N.escalar(wrap, t, maxW, maxH, maxU);
  }

  // ---------- estado del formulario ----------
  function errores() {
    const e = {};
    const para = N.limpiar(estado.para);
    const msg = N.limpiar(estado.mensaje);
    if (!para) e.para = 'Escribí para quién es.';
    if (!msg) e.mensaje = 'Escribí tu mensaje.';
    else if (msg.length < MIN_MSG) e.mensaje = 'Escribí un poco más.';
    return e;
  }

  function mostrarErrores(e) {
    $('#campo-para').classList.toggle('campo--error', !!e.para);
    $('#ayuda-para').textContent = e.para || 'Nombre o apodo, sin apellido';
    $('#campo-msg').classList.toggle('campo--error', !!e.mensaje);
    $('#err-msg').textContent = e.mensaje || '';
    inPara.setAttribute('aria-invalid', String(!!e.para));
    inMsg.setAttribute('aria-invalid', String(!!e.mensaje));
  }

  function render() {
    const t = N.pintar(notaPreview, estado, { placeholder: true });
    N.escalar(notaPreview, t, 300, 172, 0.17);
    $$('.talle').forEach((b) => {
      const on = b.dataset.talle === t;
      b.classList.toggle('activo', on);
      b.setAttribute('aria-pressed', String(on));
    });
    if (ultimoTalle && t !== ultimoTalle && !REDUCIDO) {
      notaPreview.animate({ scale: ['.9', '1.05', '1'] }, { duration: 420, easing: 'cubic-bezier(.3,1.5,.5,1)' });
    }
    ultimoTalle = t;

    $('#cont-para').textContent = `${estado.para.length} / ${MAX_PARA}`;
    const contMsg = $('#cont-msg');
    contMsg.textContent = `${estado.mensaje.length} / ${MAX_MSG}`;
    contMsg.classList.toggle('cerca', estado.mensaje.length >= MAX_MSG - 20);

    const e = errores();
    // Después de un intento bloqueado, el aviso se actualiza mientras corrigen y desaparece al quedar limpio.
    const bloqueo = bloqueado ? revisarContenido() : null;
    if (bloqueado) {
      if (bloqueo) mostrarBloqueo(bloqueo, { animar: false });
      else ocultarBloqueo();
    }
    btnContinuar.setAttribute('aria-disabled', String(Object.keys(e).length > 0 || !!bloqueo));
    if (intentoContinuar) mostrarErrores(e);
    guardarBorrador();
  }

  // ---------- moderación ----------
  const alertaMod = $('#alerta-mod');
  let bloqueado = false;      // hubo un intento con contenido no permitido
  let bloqueoServidor = null; // rechazo que vino de la API (se limpia al editar)

  function revisarContenido() {
    if (bloqueoServidor) return bloqueoServidor;
    return window.PostModeracion.revisar({ para: estado.para, mensaje: estado.mensaje });
  }

  function mostrarBloqueo(r, { animar = true } = {}) {
    bloqueado = true;
    const donde = r.campo === 'para' ? 'En el nombre' : r.campo === 'ambos' ? 'Revisá el nombre y el mensaje' : 'En tu mensaje';
    $('#alerta-mod-detalle').textContent = `${donde}: ${r.mensaje} Editalo para poder publicar.`;
    $('#campo-para').classList.toggle('campo--bloqueado', r.campo === 'para' || r.campo === 'ambos');
    $('#campo-msg').classList.toggle('campo--bloqueado', r.campo !== 'para');
    alertaMod.hidden = false;
    if (animar) {
      alertaMod.classList.remove('aparecer');
      void alertaMod.offsetWidth;
      alertaMod.classList.add('aparecer');
    }
  }

  function ocultarBloqueo() {
    bloqueado = false;
    bloqueoServidor = null;
    alertaMod.hidden = true;
    $('#campo-para').classList.remove('campo--bloqueado');
    $('#campo-msg').classList.remove('campo--bloqueado');
  }
  alertaMod.addEventListener('animationend', () => alertaMod.classList.remove('aparecer'));

  // ---------- borrador (solo en el teléfono de la persona, no en la tablet) ----------
  function guardarBorrador() {
    if (MODO_TABLET) return;
    try {
      if (estado.para || estado.mensaje) localStorage.setItem(CLAVE_BORRADOR, JSON.stringify(estado));
      else localStorage.removeItem(CLAVE_BORRADOR);
    } catch (_) { /* almacenamiento no disponible */ }
  }
  function borrarBorrador() {
    try { localStorage.removeItem(CLAVE_BORRADOR); } catch (_) { /* nada */ }
  }
  function cargarBorrador() {
    if (MODO_TABLET) return;
    try {
      const b = JSON.parse(localStorage.getItem(CLAVE_BORRADOR) || 'null');
      if (!b) return;
      estado.para = String(b.para || '').slice(0, MAX_PARA);
      estado.mensaje = String(b.mensaje || '').slice(0, MAX_MSG);
      if (N.COLORES[b.color]) estado.color = b.color;
    } catch (_) { /* borrador inválido */ }
  }

  // El textarea crece con el texto para que el contador nunca lo tape.
  function autoAlto() {
    inMsg.style.height = 'auto';
    inMsg.style.height = inMsg.scrollHeight + 'px';
  }

  function volcarEstadoEnFormulario() {
    inPara.value = estado.para;
    inMsg.value = estado.mensaje;
    autoAlto();
    $$('input[name="color"]').forEach((r) => { r.checked = r.value === estado.color; });
  }

  // ---------- navegación entre pantallas ----------
  function irA(destino, opciones) {
    if (destino === actual || transicionando) return transicion;
    transicion = hacerTransicion(destino, opciones);
    return transicion;
  }

  async function hacerTransicion(destino, { dir = 1, flip = true } = {}) {
    transicionando = true;
    const desde = pantallas[actual];
    const hacia = pantallas[destino];

    const notaDesde = flip ? $('.nota-wrap', desde) : null;
    const rDesde = notaDesde ? notaDesde.getBoundingClientRect() : null;

    hacia.hidden = false;
    // Después de mostrarla: el alto de la nota es HUG y hay que medirlo.
    if (destino === 'previa') prepararNota(notaPrevia, estado, { maxW: anchoDisponible() - 60, maxH: 300, maxU: 0.31 });
    if (destino === 'exito') prepararNota(notaExito, publicada || estado, { maxW: anchoDisponible() * 0.64, maxH: 250, maxU: 0.27 });
    hacia.classList.add('entrando');
    desde.classList.add('saliendo');
    window.scrollTo(0, 0);
    actual = destino;

    const esperas = [];
    if (REDUCIDO) {
      esperas.push(desde.animate({ opacity: [1, 0] }, { duration: 120, fill: 'forwards' }).finished);
      hacia.animate({ opacity: [0, 1] }, { duration: 200 });
    } else {
      esperas.push(desde.animate(
        { opacity: [1, 0], translate: ['0 0', `${-28 * dir}px 0`] },
        { duration: 240, easing: 'ease-in', fill: 'forwards' },
      ).finished);
      $$('[data-anim]', hacia).forEach((el, i) => {
        el.animate(
          { opacity: [0, 1], translate: [`${28 * dir}px 10px`, '0 0'] },
          { duration: 460, delay: 110 + i * 55, easing: 'cubic-bezier(.2,.8,.2,1)', fill: 'backwards' },
        );
      });
      const notaHacia = $('.nota-wrap', hacia);
      if (rDesde && notaHacia) {
        // La nota "viaja" de una pantalla a la otra.
        const r = notaHacia.getBoundingClientRect();
        const s = rDesde.width / r.width;
        esperas.push(notaHacia.animate([
          { transformOrigin: '0 0', transform: `translate(${rDesde.left - r.left}px, ${rDesde.top - r.top}px) scale(${s})` },
          { transformOrigin: '0 0', transform: 'none' },
        ], { duration: 620, easing: 'cubic-bezier(.2,.9,.25,1)' }).finished);
      }
    }

    // Tope de tiempo: si el navegador pausa las animaciones (pestaña en segundo plano),
    // la navegación no queda trabada.
    await Promise.race([Promise.all(esperas).catch(() => {}), esperar(1100)]);
    desde.hidden = true;
    desde.classList.remove('saliendo');
    hacia.classList.remove('entrando');
    desde.getAnimations({ subtree: true }).forEach((a) => a.cancel());
    transicionando = false;
    $('h1', hacia).focus({ preventScroll: true });
  }

  // ---------- pantalla 1 ----------
  inPara.addEventListener('input', () => {
    estado.para = inPara.value;
    bloqueoServidor = null;
    render();
  });
  inPara.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); inMsg.focus(); }
  });
  $('.input').addEventListener('click', () => inPara.focus());

  inMsg.addEventListener('input', () => {
    if (/[\r\n]/.test(inMsg.value)) {
      const pos = inMsg.selectionStart;
      inMsg.value = inMsg.value.replace(/[\r\n]+/g, ' ');
      inMsg.setSelectionRange(pos, pos);
    }
    estado.mensaje = inMsg.value;
    bloqueoServidor = null;
    autoAlto();
    render();
  });
  inMsg.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    if (e.ctrlKey || e.metaKey) form.requestSubmit();
    else inMsg.blur();
  });

  $$('input[name="color"]').forEach((r) => r.addEventListener('change', () => {
    estado.color = r.value;
    render();
    if (!REDUCIDO) notaPreview.animate({ scale: ['1', '1.06', '1'] }, { duration: 360, easing: 'cubic-bezier(.3,1.5,.5,1)' });
  }));

  const RANGOS = { S: 'hasta 40 caracteres', M: 'de 41 a 120 caracteres', L: `de 121 a ${MAX_MSG} caracteres` };
  $$('.talle').forEach((b) => b.addEventListener('click', () => {
    toast(`Tamaño ${b.dataset.talle}: ${RANGOS[b.dataset.talle]}. Se ajusta solo mientras escribís.`);
  }));

  function sacudirContinuar() {
    btnContinuar.classList.remove('sacudir');
    void btnContinuar.offsetWidth;
    btnContinuar.classList.add('sacudir');
  }

  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const errs = errores();
    if (Object.keys(errs).length) {
      intentoContinuar = true;
      mostrarErrores(errs);
      sacudirContinuar();
      (errs.para ? inPara : inMsg).focus();
      return;
    }
    const bloqueo = revisarContenido();
    if (bloqueo) {
      mostrarBloqueo(bloqueo);
      render();
      sacudirContinuar();
      return;
    }
    document.activeElement && document.activeElement.blur();
    irA('previa', { dir: 1 });
  });
  btnContinuar.addEventListener('animationend', () => btnContinuar.classList.remove('sacudir'));

  $('#btn-salir').addEventListener('click', () => {
    if (N.limpiar(estado.para) || N.limpiar(estado.mensaje)) dlgSalir.showModal();
    else salirALaApp();
  });
  $('#dlg-seguir').addEventListener('click', () => dlgSalir.close());
  $('#dlg-confirmar').addEventListener('click', () => { dlgSalir.close(); salirALaApp(); });
  dlgSalir.addEventListener('click', (e) => { if (e.target === dlgSalir) dlgSalir.close(); });

  // ---------- pantalla 2 ----------
  function volverAEditar(enfocar) {
    irA('escribir', { dir: -1 }).then(() => {
      if (!enfocar) return;
      inMsg.focus();
      inMsg.setSelectionRange(inMsg.value.length, inMsg.value.length);
    });
  }
  $('#btn-volver').addEventListener('click', () => volverAEditar(false));
  $('#btn-editar').addEventListener('click', () => volverAEditar(true));
  btnPublicar.addEventListener('click', publicar);

  function setCargando(btn, on) {
    btn.classList.toggle('cargando', on);
    btn.setAttribute('aria-busy', String(on));
    $('.btn__texto', btn).textContent = on ? 'Publicando…' : 'Publicar en pantalla';
  }

  async function publicar() {
    if (enviando) return;
    enviando = true;
    if (transicionando) await transicion;
    setCargando(btnPublicar, true);
    const cuerpo = { para: N.limpiar(estado.para), mensaje: N.limpiar(estado.mensaje), color: estado.color };
    try {
      // Última revisión antes de enviar (por si llegaron acá sin pasar por "Continuar").
      const bloqueo = window.PostModeracion.revisar(cuerpo);
      if (bloqueo) throw Object.assign(new Error(bloqueo.mensaje), { tipo: 'moderacion', bloqueo });

      const [nota] = await Promise.all([enviarNota(cuerpo), esperar(REDUCIDO ? 0 : 450)]);
      publicada = nota || { ...cuerpo };
      await animarEnvio();
      borrarBorrador();
      await irA('exito', { flip: false });
      celebrar();
    } catch (err) {
      $$('[data-anim], .nota-wrap', pantallas.previa).forEach((el) => el.getAnimations().forEach((a) => a.cancel()));
      if (err.tipo === 'moderacion') {
        // Vuelve a editar y muestra el aviso rojo.
        bloqueoServidor = err.bloqueo;
        irA('escribir', { dir: -1 }).then(() => { mostrarBloqueo(err.bloqueo); render(); });
      } else if (err.tipo === 'contenido') {
        toast(err.message, { accion: 'Editar', alAccionar: () => volverAEditar(true) });
      } else if (err.tipo === 'espera') {
        toast(err.message);
      } else {
        toast('No pudimos conectar con las pantallas. Revisá tu conexión.', { accion: 'Reintentar', alAccionar: publicar });
      }
    } finally {
      enviando = false;
      setCargando(btnPublicar, false);
    }
  }

  // ---------- envío (Supabase o servidor local) ----------
  const MOTIVOS = {
    ofensivo: 'Tiene insultos o palabras inapropiadas.',
    link: 'No se pueden incluir links.',
    datos: 'No se pueden incluir números de teléfono.',
  };

  function errorDeModeracion(motivo, cuerpo) {
    // La base no dice en qué campo estaba: si el filtro local lo encuentra, usamos ese.
    const local = window.PostModeracion.revisar(cuerpo);
    const campo = local ? local.campo : 'ambos';
    return Object.assign(new Error(MOTIVOS[motivo]), { tipo: 'moderacion', bloqueo: { campo, motivo, mensaje: MOTIVOS[motivo] } });
  }

  async function enviarNota(cuerpo) {
    const url = (CFG.SUPABASE_URL || '').replace(/\/$/, '');
    const clave = CFG.SUPABASE_ANON_KEY || '';
    const usarSupabase = !!(url && clave);
    let res;
    try {
      if (usarSupabase) {
        const headers = { apikey: clave, 'Content-Type': 'application/json', Prefer: 'return=representation' };
        if (clave.startsWith('eyJ')) headers.Authorization = 'Bearer ' + clave; // clave "anon" clásica (JWT)
        res = await fetch(url + '/rest/v1/notas', { method: 'POST', headers, body: JSON.stringify(cuerpo) });
      } else {
        res = await fetch(api('/api/notas'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(cuerpo),
        });
      }
    } catch (_) {
      throw Object.assign(new Error('red'), { tipo: 'red' });
    }
    let data = null;
    try { data = await res.json(); } catch (_) { /* respuesta vacía */ }

    if (usarSupabase) {
      if (res.ok && Array.isArray(data) && data[0]) return data[0];
      const detalle = data && data.details;
      if (MOTIVOS[detalle]) throw errorDeModeracion(detalle, cuerpo);
      if (detalle === 'espera') throw Object.assign(new Error(data.message), { tipo: 'espera' });
      if (data && data.code === '23514') throw Object.assign(new Error('Revisá el nombre y el mensaje.'), { tipo: 'contenido' });
      throw Object.assign(new Error('servidor'), { tipo: 'servidor' });
    }

    if (res.ok && data && data.ok) return data.nota;
    if (data && MOTIVOS[data.motivo]) throw errorDeModeracion(data.motivo, cuerpo);
    const tipo = res.status === 429 ? 'espera' : res.status === 400 && data ? 'contenido' : 'servidor';
    throw Object.assign(new Error((data && data.error) || 'No pudimos publicar tu dedicatoria.'), { tipo });
  }

  // La nota sale volando hacia "las pantallas".
  function animarEnvio() {
    const p = pantallas.previa;
    if (REDUCIDO) return esperar(0);
    $$('[data-anim]', p).forEach((el) => el.animate({ opacity: [1, 0] }, { duration: 300, delay: 150, fill: 'forwards' }));
    const vuelo = notaPrevia.animate([
      { transform: 'none' },
      { transform: 'translateY(16px) scale(1.03)', offset: 0.22 },
      { transform: 'translateY(-115vh) rotate(-16deg) scale(.5)' },
    ], { duration: 780, easing: 'cubic-bezier(.55,0,.7,.35)', fill: 'forwards' }).finished;
    return Promise.race([vuelo.catch(() => {}), esperar(1200)]);
  }

  // ---------- pantalla 3 ----------
  function celebrar() {
    const p = pantallas.exito;
    if (!REDUCIDO) {
      notaExito.animate([
        { transform: 'translateY(-75vh) rotate(12deg)', opacity: 0 },
        { transform: 'translateY(14px) rotate(-2deg)', opacity: 1, offset: 0.7 },
        { transform: 'none', opacity: 1 },
      ], { duration: 850, easing: 'cubic-bezier(.2,.8,.3,1)' });
      $$('.deco__item', p).forEach((el, i) => el.animate(
        { scale: ['0', '1.25', '1'], opacity: [0, 1, 1] },
        { duration: 520, delay: 380 + i * 60, easing: 'cubic-bezier(.3,1.5,.5,1)', fill: 'backwards' },
      ));
      $$('.bandas i', p).forEach((el, i) => el.animate(
        { opacity: [0, 1], translate: ['-40% 0', '0 0'] },
        { duration: 700, delay: 200 + i * 120, easing: 'cubic-bezier(.2,.8,.2,1)', fill: 'backwards' },
      ));
      $('.sticker', p).animate(
        { scale: ['.4', '1.12', '1'], opacity: [0, 1, 1], rotate: ['-8deg', '2deg', '0deg'] },
        { duration: 620, delay: 560, easing: 'cubic-bezier(.3,1.5,.5,1)', fill: 'backwards' },
      );
    }
    if (MODO_TABLET) iniciarCuentaTablet();
  }

  function iniciarCuentaTablet() {
    detenerCuentaTablet();
    const total = Math.max(5, Number(CFG.TABLET_RESET_SEG) || 20);
    const aviso = $('#aviso-tablet');
    const barra = $('#btn-otra .btn__progreso');
    let quedan = total;
    aviso.hidden = false;
    aviso.textContent = `Volvemos al inicio en ${quedan} s`;
    barra.animate({ transform: ['scaleX(0)', 'scaleX(1)'] }, { duration: total * 1000, easing: 'linear', fill: 'forwards' });
    temporizadorTablet = setInterval(() => {
      quedan -= 1;
      aviso.textContent = `Volvemos al inicio en ${quedan} s`;
      if (quedan <= 0) nuevaDedicatoria({ enfocar: false });
    }, 1000);
  }
  function detenerCuentaTablet() {
    clearInterval(temporizadorTablet);
    temporizadorTablet = null;
    $('#aviso-tablet').hidden = true;
    $('#btn-otra .btn__progreso').getAnimations().forEach((a) => a.cancel());
  }

  function nuevaDedicatoria({ enfocar = true } = {}) {
    detenerCuentaTablet();
    estado.para = '';
    estado.mensaje = '';
    estado.color = 'verde';
    publicada = null;
    intentoContinuar = false;
    volcarEstadoEnFormulario();
    mostrarErrores({});
    ocultarBloqueo();
    ultimoTalle = null;
    render();
    irA('escribir', { dir: -1, flip: false }).then(() => { if (enfocar) inPara.focus(); });
  }

  $('#btn-otra').addEventListener('click', () => nuevaDedicatoria({ enfocar: true }));
  $('#btn-cerrar').addEventListener('click', () => nuevaDedicatoria({ enfocar: false }));
  $('#btn-app').addEventListener('click', salirALaApp);

  function salirALaApp() {
    if (CFG.APP_URL) { location.href = CFG.APP_URL; return; }
    if (document.referrer && history.length > 1) { history.back(); return; }
    toast('Volvé a la app SPOT! para seguir. Ya podés cerrar esta pestaña.');
  }

  // ---------- toast ----------
  let temporizadorToast = null;
  function toast(texto, { accion, alAccionar } = {}) {
    const el = $('#toast');
    $('.toast__texto', el).textContent = texto;
    const b = $('.toast__accion', el);
    b.hidden = !accion;
    b.onclick = null;
    if (accion) {
      b.textContent = accion;
      b.onclick = () => { ocultarToast(); if (alAccionar) alAccionar(); };
    }
    el.hidden = false;
    el.classList.remove('visible');
    void el.offsetWidth;
    el.classList.add('visible');
    clearTimeout(temporizadorToast);
    temporizadorToast = setTimeout(ocultarToast, accion ? 7000 : 4200);
  }
  function ocultarToast() {
    const el = $('#toast');
    el.classList.remove('visible');
    clearTimeout(temporizadorToast);
    temporizadorToast = setTimeout(() => { el.hidden = true; }, 350);
  }

  // ---------- inicio ----------
  if (MODO_TABLET) {
    document.documentElement.classList.add('modo-tablet');
    $('#btn-salir').hidden = true;
    $('#btn-salir').insertAdjacentHTML('afterend', '<span class="header__lado" aria-hidden="true"></span>');
    $('#btn-app').hidden = true;
  }
  cargarBorrador();
  volcarEstadoEnFormulario();
  render();

  window.addEventListener('resize', () => {
    if (actual === 'previa') prepararNota(notaPrevia, estado, { maxW: anchoDisponible() - 60, maxH: 300, maxU: 0.31 });
    if (actual === 'exito') prepararNota(notaExito, publicada || estado, { maxW: anchoDisponible() * 0.64, maxH: 250, maxU: 0.27 });
  });
})();
